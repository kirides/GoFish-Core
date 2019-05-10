using GoFish.DataAccess.Extensions;
using GoFish.DataAccess.Helpers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GoFish.DataAccess
{
    public class DbfReader
    {
        private delegate object RowHandler(int row, ReadOnlySpan<byte> buffer, DbfField field, Encoding encoding);
        private readonly Dictionary<char, RowHandler> DbfTypeMap = new Dictionary<char, RowHandler>();
        private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Shared;
        private static object CopyBuffer(int rowIndex, ReadOnlySpan<byte> buffer, DbfField field, Encoding encoding)
        {
            var result = new byte[field.Length];
            buffer.CopyTo(result);
            return result;
        }

        private readonly Dbf dbf;
        private readonly DbfHeader dbfHeader;

        public Encoding TextEncoding { get; }

        public DbfReader(Dbf dbf, Encoding textEncoding)
        {
            DbfTypeMap.Add('C', (i, b, f, e) => e.GetString(b.Slice(f.Displacement, f.Length)).TrimEnd('\0'));
            DbfTypeMap.Add('M', (i, b, f, e) => BitConverter.ToInt32(b.Slice(f.Displacement)));
            DbfTypeMap.Add('W', (i, b, f, e) => BitConverter.ToInt32(b.Slice(f.Displacement)));
            DbfTypeMap.Add('G', (i, b, f, e) => BitConverter.ToInt32(b.Slice(f.Displacement)));
            DbfTypeMap.Add('Y', (i, b, f, e) => BitConverter.ToInt64(b.Slice(f.Displacement)) / 10000m); // Stored as int64 with 4 implicit decimal places
            DbfTypeMap.Add('D', (i, b, f, e) =>
            {
                var dateStr = e.GetString(b.Slice(f.Displacement, f.Length)).Trim(); return dateStr == "" ? DateTime.MinValue : DateTime.ParseExact(dateStr, "yyyyMMdd", null);
            });
            DbfTypeMap.Add('T', (i, b, f, e) => JulianDateHelper.FromULongBuffer(b.Slice(f.Displacement)));
            DbfTypeMap.Add('N', (i, b, f, e) => { var numStr = e.GetString(b.Slice(f.Displacement, f.Length)).Trim(); return numStr == "" ? 0m : decimal.Parse(numStr); });
            DbfTypeMap.Add('B', (i, b, f, e) => BitConverter.ToInt32(b.Slice(f.Displacement)));
            DbfTypeMap.Add('O', (i, b, f, e) => BitConverter.ToDouble(b.Slice(f.Displacement)));
            DbfTypeMap.Add('F', (i, b, f, e) => { var numStr = e.GetString(b.Slice(f.Displacement, f.Length)).Trim(); return numStr == "" ? 0f : float.Parse(numStr); });
            DbfTypeMap.Add('I', (i, b, f, e) => BitConverter.ToInt32(b.Slice(f.Displacement)));
            DbfTypeMap.Add('L', (i, b, f, e) => BitConverter.ToBoolean(b.Slice(f.Displacement)));
            DbfTypeMap.Add('Q', CopyBuffer);
            DbfTypeMap.Add('P', CopyBuffer);

            this.dbf = dbf;
            TextEncoding = textEncoding;
            dbfHeader = dbf.GetHeader();

            if (IsVisualFoxPro(dbfHeader))
            { // Special handling for VFP
                DbfTypeMap['B'] = (i, b, f, e) => BitConverter.ToDouble(b.Slice(f.Displacement));
                // _NullFlags
                DbfTypeMap['0'] = (i, b, f, e) => b[f.Displacement];
                // VarChar/VarBinary
                DbfTypeMap['V'] = (i, b, f, e) =>
                { // TODO: FIXME - highly likely to return wrong results if VARCHAR field is 254 length
                    var o = f.Displacement;
                    var c = f.Length;
                    if (!(b.Length - (o + c) > 2)) return e.GetString(b.Slice(o, c));
                    var varCharLength = b[o + c - 1];
                    if (b[o + c - 2] == ' ' || !char.IsLetterOrDigit((char)b[o + c - 2]))
                    {
                        return (f.Flags & DbfFieldFlags.Binary) != 0 ? CopyBuffer(i, b, f, e) : e.GetString(b.Slice(o, varCharLength) );
                    }
                    return (f.Flags & DbfFieldFlags.Binary) != 0 ? CopyBuffer(i, b, f, e) : e.GetString(b.Slice(o, c));
                };
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

        private object[] ReadRowFromBuffer(ReadOnlySpan<byte> rowBuf, int index)
        {
            var rowData = new object[dbfHeader.Fields.Count];
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                //TrySetNullData(rowBuf, rowData, field);
                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                if (field.Type == 'M' || field.Type == 'W' || field.Type == 'G')
                {
                    var memoPointer = handler(index, rowBuf, field, TextEncoding);
                    rowData[i] = $"MEMO@{memoPointer}";
                }
                else
                {
                    rowData[i] = handler(index, rowBuf, field, TextEncoding);
                }
            }
            return rowData;
        }
        private object[] ReadRowFromBufferMemo(ReadOnlySpan<byte> rowBuf, int index, Stream memofs, short memoBlocksize)
        {
            var rowData = new object[dbfHeader.Fields.Count];
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                //TrySetNullData(rowBuf, rowData, field);
                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                if (field.Type == 'M')
                {
                    var offset = (int)handler(index, rowBuf, field, TextEncoding);
                    if (offset == 0)
                    {
                        rowData[i] = "";
                        continue;
                    }
                    var targetPos = 4 + offset * memoBlocksize;
                    var memoPos = memofs.Position;
                    if (memoPos < targetPos)
                    {
                        memofs.Seek(targetPos - memoPos, SeekOrigin.Current);
                    }
                    else
                    {
                        memofs.Seek(targetPos, SeekOrigin.Begin);
                    }
                    var len = memofs.ReadInt(bigEndian: true);

                    if ((field.Flags & DbfFieldFlags.Binary) != 0)
                    {
                        var memoBuf = new byte[len];
                        memofs.Read(memoBuf, 0, len);
                        rowData[i] = memoBuf;
                    }
                    else
                    {
                        var memoBuf = bufferPool.Rent(len);
                        memofs.Read(memoBuf, 0, len);
                        try
                        {
                            rowData[i] = TextEncoding.GetString(memoBuf, 0, len);
                        }
                        finally
                        {
                            bufferPool.Return(memoBuf);
                        }
                    }
                }
                else
                {
                    rowData[i] = handler(index, rowBuf, field, TextEncoding);
                }
            }
            return rowData;
        }

        private void TrySetNullData(ReadOnlySpan<byte> rowBuf, object[] rowData, DbfField field)
        {
            if ((field.Flags & DbfFieldFlags.System) != 0
                    && field.Name.Equals("_NullFlags", StringComparison.Ordinal))
            {
                byte nullFlags = rowBuf[field.Displacement];
                byte fcCount = 1;
                for (int fc = 0; fc < dbfHeader.Fields.Count; fc++)
                {
                    var f = dbfHeader.Fields[fc];
                    if ((f.Flags & DbfFieldFlags.Null) != 0)
                    {
                        if ((nullFlags & fcCount) != 0)
                        {
                            rowData[fc] = null;
                        }
                        fcCount <<= 1;
                    }
                }
            }
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
                //var rowBuf = new byte[dbfHeader.RecordLength];
                var rowBuffer = bufferPool.Rent(dbfHeader.RecordLength);
                var rowBuf = rowBuffer.AsMemory().Slice(0, dbfHeader.RecordLength);
                try
                {
                    fs.Position = dbfHeader.HeaderSize;
                    if (includeMemo && (dbfHeader.Flags & DbfHeaderFlags.Memo) != 0)
                    {
                        using (var memofs = dbf.OpenMemo())
                        {
                            memofs.Position = 6;
                            var memoBlocksize = memofs.ReadShort(bigEndian: true);
                            for (var index = 0; index < dbfHeader.RecordCount; index++)
                            {
                                var read = fs.Read(rowBuf.Span);
                                if (read != dbfHeader.RecordLength)
                                    throw new InvalidOperationException($"Could not read Row at Index {index}");
                                if (rowBuf.Span[0] == 0x2A && !includeDeleted) continue; // Entry is marked Deleted(*)

                                var rowData = ReadRowFromBufferMemo(rowBuf.Span, index, memofs, memoBlocksize);
                                if (predicate(index, rowData))
                                    yield return rowData;
                            }
                        }
                    }
                    else
                    {
                        for (var index = 0; index < dbfHeader.RecordCount; index++)
                        {
                            var read = fs.Read(rowBuf.Span);
                            if (read != dbfHeader.RecordLength)
                                throw new InvalidOperationException($"Could not read Row at Index {index}");

                            if (rowBuf.Span[0] == 0x2A && !includeDeleted) continue; // Entry is marked Deleted(*)
                            var rowData = ReadRowFromBuffer(rowBuf.Span, index);
                            yield return rowData;
                        }
                    }
                }
                finally
                {
                    bufferPool.Return(rowBuffer);
                }
            }
        }
    }
}
