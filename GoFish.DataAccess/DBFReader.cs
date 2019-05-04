using GoFish.DataAccess.Extensions;
using GoFish.DataAccess.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GoFish.DataAccess
{
    public class DbfReader
    {
        private delegate object RowHandler(int row, byte[] buffer, int offset, int count, Encoding encoding);
        private readonly Dictionary<char, RowHandler> DbfTypeMap = new Dictionary<char, RowHandler>();

        private static object CopyBuffer(int rowIndex, byte[] buffer, int offset, int count, Encoding encoding)
        {
            var result = new byte[count];
            Array.Copy(buffer, offset, result, 0, count);
            return result;
        }

        private readonly Dbf dbf;
        private readonly DbfHeader dbfHeader;

        public Encoding TextEncoding { get; }

        public DbfReader(Dbf dbf, Encoding textEncoding)
        {
            DbfTypeMap.Add('C', (i, b, o, c, e) => e.GetString(b, o, c).TrimEnd('\0'));
            DbfTypeMap.Add('V', (i, b, o, c, e) =>
            { // TODO: FIXME - highly likely to return wrong results if VARCHAR field is 254 length
                if (!(b.Length - (o + c) > 2)) return e.GetString(b, o, c);
                var varCharLength = b[o + c - 1];
                if (b[o + c - 2] == ' ' || !char.IsLetterOrDigit((char)b[o + c - 2]))
                {
                    return e.GetString(b, o, varCharLength);
                }
                return e.GetString(b, o, c);
            });
            DbfTypeMap.Add('M', (i, b, o, c, e) => BitConverter.ToInt32(b, o));
            DbfTypeMap.Add('W', (i, b, o, c, e) => BitConverter.ToInt32(b, o));
            DbfTypeMap.Add('G', (i, b, o, c, e) => BitConverter.ToInt32(b, o));
            DbfTypeMap.Add('Y', (i, b, o, c, e) => BitConverter.ToInt64(b, o) / 10000m); // Stored as int64 with 4 implicit decimal places
            DbfTypeMap.Add('D', (i, b, o, c, e) =>
            {
                var dateStr = e.GetString(b, o, c).Trim(); return dateStr == "" ? DateTime.MinValue : DateTime.ParseExact(dateStr, "yyyyMMdd", null);
            });
            DbfTypeMap.Add('T', (i, b, o, c, e) => JulianDateHelper.FromULongBuffer(b, o));
            DbfTypeMap.Add('N', (i, b, o, c, e) => { var numStr = e.GetString(b, o, c).Trim(); return numStr == "" ? 0m : decimal.Parse(numStr); });
            DbfTypeMap.Add('B', (i, b, o, c, e) => BitConverter.ToInt32(b, o));
            DbfTypeMap.Add('O', (i, b, o, c, e) => BitConverter.ToDouble(b, o));
            DbfTypeMap.Add('F', (i, b, o, c, e) => { var numStr = e.GetString(b, o, c).Trim(); return numStr == "" ? 0f : float.Parse(numStr); });
            DbfTypeMap.Add('I', (i, b, o, c, e) => BitConverter.ToInt32(b, o));
            DbfTypeMap.Add('L', (i, b, o, c, e) => BitConverter.ToBoolean(b, o));
            DbfTypeMap.Add('Q', CopyBuffer);
            DbfTypeMap.Add('P', CopyBuffer);

            this.dbf = dbf;
            TextEncoding = textEncoding;
            dbfHeader = dbf.GetHeader();

            if (IsVisualFoxPro(dbfHeader))
            { // Special handling for VFP
                DbfTypeMap['B'] = (i, b, o, c, e) => BitConverter.ToDouble(b, o);
            }
        }

        private bool IsVisualFoxPro(DbfHeader dbfHeader)
        {
            return dbfHeader.Type == DbfType.VisualFoxPro || dbfHeader.Type == DbfType.VisualFoxProVar || dbfHeader.Type == DbfType.VisualFoxProAutoInc;
        }

        /// <summary>
        /// Creates an instance with <see cref="TextEncoding"/> being codepage 1252.
        /// </summary>
        public DbfReader(Dbf dbf)
         : this(dbf, CodePagesEncodingProvider.Instance.GetEncoding(1252)) { }

        public byte[] ReadRowRaw(int index, bool includeDeleted = false)
        {
            var buf = new byte[dbfHeader.RecordLength];
            ReadRowRaw(index, buf, 0, includeDeleted);
            return buf;
        }

        public int ReadRowRaw(int index, byte[] buffer, int offset, bool includeDeleted = false)
        {
			if (buffer.Length - offset < dbfHeader.RecordLength)
			{
				throw new ArgumentException($"Buffer is not large enough {buffer.Length - offset} / {dbfHeader.RecordLength}");
			}
            using (var fs = dbf.OpenReadOnly())
            {
                fs.Position = dbfHeader.HeaderSize + (dbfHeader.RecordLength * index);
                int read;
                do
                {
                    read = fs.Read(buffer, 0, dbfHeader.RecordLength);
                    if (read != dbfHeader.RecordLength)
                        throw new InvalidOperationException($"Could not read Row at Index {index}");
                } while (buffer[0] == '*' && !includeDeleted); // Skip deleted entries;

                return read;
            }
        }

        public object[] ReadRow(int index, bool includeDeleted = false)
        {
            using (var fs = dbf.OpenReadOnly())
            {
                if ((dbfHeader.Flags & DbfHeaderFlags.Memo) != 0)
                {
                    using (var memofs = dbf.OpenMemo())
                    {
                        return ReadRowMemo(index, fs, memofs, includeDeleted);
                    }
                }
                else
                {
                    return ReadRow(index, fs, includeDeleted);
                }
            }
        }

        private object[] ReadRowMemo(int index, Stream fs, Stream memofs, bool includeDeleted = false)
        {
            var rowBuf = new byte[dbfHeader.RecordLength];
            fs.Position = dbfHeader.HeaderSize + (dbfHeader.RecordLength * index);
            do
            {
                var read = fs.Read(rowBuf, 0, dbfHeader.RecordLength);
                if (read != dbfHeader.RecordLength)
                    throw new InvalidOperationException($"Could not read Row at Index {index}");
            } while (rowBuf[0] == '*' && !includeDeleted); // Skip deleted entries;

            memofs.Position = 6;
            var memoBlocksize = memofs.ReadShort(bigEndian: true);

            var result = ReadRowFromBufferMemo(rowBuf, index, memofs, memoBlocksize);
            return result;
        }

        private object[] ReadRow(int index, Stream fs, bool includeDeleted = false)
        {
            var rowBuf = new byte[dbfHeader.RecordLength];
            fs.Position = dbfHeader.HeaderSize + (dbfHeader.RecordLength * index);
            do
            {
                var read = fs.Read(rowBuf, 0, dbfHeader.RecordLength);
                if (read != dbfHeader.RecordLength)
                    throw new InvalidOperationException($"Could not read Row at Index {index}");
            } while (rowBuf[0] == '*' && !includeDeleted); // Skip deleted entries;

            var rowData = ReadRowFromBuffer(rowBuf, index);
            return rowData;
        }

        private object[] ReadRowFromBuffer(byte[] rowBuf, int index)
        {
            var rowData = new object[dbfHeader.Fields.Count];
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                if (field.Type == 'M' || field.Type == 'W' || field.Type == 'G')
                {
                    var memoPointer = handler(index, rowBuf, field.Displacement, field.Length, TextEncoding);
                    rowData[i] = $"MEMO@{memoPointer}";
                }
                else
                {
                    rowData[i] = handler(index, rowBuf, field.Displacement, field.Length, TextEncoding);
                }
            }
            return rowData;
        }
        private object[] ReadRowFromBufferMemo(byte[] rowBuf, int index, Stream memofs, short memoBlocksize)
        {
            var rowData = new object[dbfHeader.Fields.Count];
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                if (field.Type == 'M')
                {
                    var offset = (int)handler(index, rowBuf, field.Displacement, field.Length, TextEncoding);
                    if (offset == 0)
                    {
                        rowData[i] = "";
                        continue;
                    }

                    memofs.Position = 4 + (offset * memoBlocksize);
                    var len = memofs.ReadInt(bigEndian: true);
                    var memoBuf = new byte[len];
                    memofs.Read(memoBuf, 0, len);
                    rowData[i] = TextEncoding.GetString(memoBuf);
                }
                else
                {
                    rowData[i] = handler(index, rowBuf, field.Displacement, field.Length, TextEncoding);
                }
            }
            return rowData;
        }

        public IEnumerable<object[]> ReadRows(bool includeMemo = false, bool includeDeleted = false)
        {
            return ReadRows(returnTrue, includeMemo, includeDeleted);

            bool returnTrue(int idx, object[] row) => true;
        }
        public IEnumerable<object[]> ReadRows(Func<int, object[], bool> predicate, bool includeMemo = false, bool includeDeleted = false)
        {
            using (var fs = dbf.OpenReadOnly())
            {
                var rowBuf = new byte[dbfHeader.RecordLength];
                fs.Position = dbfHeader.HeaderSize;
                if (includeMemo && (dbfHeader.Flags & DbfHeaderFlags.Memo) != 0)
                {
                    using (var memofs = dbf.OpenMemo())
                    {
                        memofs.Position = 6;
                        var memoBlocksize = memofs.ReadShort(bigEndian: true);
                        for (var index = 0; index < dbfHeader.RecordCount; index++)
                        {
                            var read = fs.Read(rowBuf, 0, dbfHeader.RecordLength);
                            if (read != dbfHeader.RecordLength)
                                throw new InvalidOperationException($"Could not read Row at Index {index}");
                            if (rowBuf[0] == '*' && !includeDeleted) continue; // Entry is marked Deleted(*)

                            var rowData = ReadRowFromBufferMemo(rowBuf, index, memofs, memoBlocksize);
                            if (predicate(index, rowData))
                                yield return rowData;
                        }
                    }
                }
                else
                {
                    for (var index = 0; index < dbfHeader.RecordCount; index++)
                    {
                        var read = fs.Read(rowBuf, 0, dbfHeader.RecordLength);
                        if (read != dbfHeader.RecordLength)
                            throw new InvalidOperationException($"Could not read Row at Index {index}");

                        if (rowBuf[0] == '*' && !includeDeleted) continue; // Entry is marked Deleted(*)
                        var rowData = ReadRowFromBuffer(rowBuf, index);
                        yield return rowData;
                    }
                }
            }
        }
    }
}
