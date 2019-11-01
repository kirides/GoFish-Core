using GoFish.DataAccess.Extensions;
using GoFish.DataAccess.Helpers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;

namespace GoFish.DataAccess
{
    public class DbfReader
    {
        private delegate object RowHandler(int row, ReadOnlySpan<byte> buffer, DbfField field, Encoding encoding);
        private readonly Dictionary<char, RowHandler> DbfTypeMap = new Dictionary<char, RowHandler>();
        private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Shared;
        private static object CopyFieldBuffer(int rowIndex, ReadOnlySpan<byte> buffer, DbfField field, Encoding encoding)
        {
            var result = new byte[field.Length];
            buffer.CopyTo(result);
            return result;
        }

        private static byte[] CloneBuffer(ReadOnlySpan<byte> buffer)
        {
            var result = new byte[buffer.Length];
            buffer.CopyTo(result);
            return result;
        }

        private readonly Dbf dbf;
        private readonly DbfHeader dbfHeader;

        public Encoding TextEncoding { get; }

        private readonly DbfField nullField;
        private readonly RowHandler nullFieldHandler;

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
            DbfTypeMap.Add('Q', CopyFieldBuffer);
            DbfTypeMap.Add('P', CopyFieldBuffer);

            this.dbf = dbf;
            TextEncoding = textEncoding;
            dbfHeader = dbf.GetHeader();

            if (IsVisualFoxPro(dbfHeader))
            { // Special handling for VFP
                DbfTypeMap['B'] = (i, b, f, e) => BitConverter.ToDouble(b.Slice(f.Displacement));
                // _NullFlags
                nullFieldHandler = DbfTypeMap['0'] = (i, b, f, e) =>
                { // Handle dynamic Null-Field sizes
                    if (f.Length == 1) return (uint)b[f.Displacement];
                    if (f.Length == 5) return BitConverter.ToUInt32(b.Slice(f.Displacement));

                    throw new NotSupportedException($"Nullfield size '{f.Length}' not supported");
                };
                // VarChar/VarBinary
                // SPECIAL CASE for VARCHAR/BINARY
                DbfTypeMap['V'] = (i, b, f, e) => f.Flags.HasFlag(DbfFieldFlags.Binary) ? (object)CloneBuffer(b) : e.GetString(b);

                nullField = dbfHeader.Fields.FirstOrDefault(x => x.Type == '0');
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

        /// <summary>
        /// <para/>WARNING: Internal buffer is re-used, don't reference the returned buffer! Don't ToList()!
        /// </summary>
        /// <param name="includeDeleted"></param>
        /// <returns></returns>
        public IEnumerable<byte[]> ReadRowsRaw(bool includeDeleted = false)
        {
            var buffer = new byte[dbfHeader.RecordLength];
            using (var fs = dbf.OpenReadOnly())
            {
                fs.Position = dbfHeader.HeaderSize + dbfHeader.RecordLength;
                int read;
                do
                {
                    read = fs.Read(buffer, 0, dbfHeader.RecordLength);
                    if (read != dbfHeader.RecordLength) break;
                    if (buffer[0] != '*')
                    {
                        yield return buffer;
                    }
                } while (read != 0);
            }
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
            if (nullField != null)
            {
                rowData[nullField.Index] = nullFieldHandler(index, rowBuf, nullField, null);
            }
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                if (field == nullField) continue;

                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                if (field.Type == 'M' || field.Type == 'W' || field.Type == 'G')
                {
                    var memoPointer = handler(index, rowBuf, field, TextEncoding);
                    rowData[i] = $"MEMO@{memoPointer}";
                }
                else if (field.Type == 'V')
                {
                    bool hasNullFlag = ((uint)rowData[nullField.Index] & 1 << field.Index) != 0;
                    if (hasNullFlag)
                    {
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, rowBuf[field.Displacement + field.Length - 1]), field, TextEncoding);
                    }
                    else
                    {
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
                    }
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
            if (nullField != null)
            {
                rowData[nullField.Index] = nullFieldHandler(index, rowBuf, nullField, null);
            }
            Span<byte> intBuf = stackalloc byte[4];
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                if (field == nullField) continue;

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
                        memofs.Position = targetPos;
                    }
                    var len = memofs.ReadIntBE(intBuf);

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
                else if (field.Type == 'V')
                {
                    bool hasNullFlag = ((uint)rowData[nullField.Index] & 1 << field.Index) != 0;
                    if (hasNullFlag)
                    {
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, rowBuf[field.Displacement + field.Length - 1]), field, TextEncoding);
                    }
                    else
                    {
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
                    }
                }
                else
                {
                    rowData[i] = handler(index, rowBuf, field, TextEncoding);
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
        private object[] ReadRowSequence(ReadOnlySequence<byte> sequence, int rowIndex)
        {
            if (sequence.IsSingleSegment)
            {
                return ReadRowFromBuffer(sequence.FirstSpan, rowIndex);
            }
            // TODO: use buffer pool if recordlength is larger than 1024
            Span<byte> span = stackalloc byte[(int)sequence.Length];
            sequence.CopyTo(span);
            return ReadRowFromBuffer(span, rowIndex);
        }
        private object[] ReadRowSequenceMemo(ReadOnlySequence<byte> sequence, int rowIndex, Stream memofs, short memoBlocksize)
        {
            if (sequence.IsSingleSegment)
            {
                return ReadRowFromBufferMemo(sequence.FirstSpan, rowIndex, memofs, memoBlocksize);
            }

            // TODO: use buffer pool if recordlength is larger than 1024
            Span<byte> span = sequence.Length > 1024 ? new byte[sequence.Length] : stackalloc byte[(int)sequence.Length];
            sequence.CopyTo(span);
            return ReadRowFromBufferMemo(span, rowIndex, memofs, memoBlocksize);
        }
        private static bool TryReadRecord(int recordLength, ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> record)
        {
            if (buffer.Length < recordLength)
            {
                record = default;
                return false;
            }

            record = buffer.Slice(0, recordLength);
            buffer = buffer.Slice(buffer.GetPosition(recordLength));

            return true;
        }
        public async IAsyncEnumerable<object[]> ReadRowsAsync(Func<int, object[], bool> predicate, bool includeMemo = false, bool includeDeleted = false)
        {
            using (var fs = dbf.OpenReadOnlyAsync())
            {
                int recordLength = dbfHeader.RecordLength;
                fs.Position = dbfHeader.HeaderSize;
                var rdr = PipeReader.Create(fs);
                if (includeMemo && (dbfHeader.Flags & DbfHeaderFlags.Memo) != 0)
                {
                    using (var memofs = dbf.OpenMemo())
                    {
                        memofs.Position = 6;
                        var memoBlocksize = memofs.ReadShort(bigEndian: true);
                        for (var index = 0; index < dbfHeader.RecordCount;)
                        {
                            var readRes = await rdr.ReadAsync();
                            var buffer = readRes.Buffer;
                            while (index < dbfHeader.RecordCount && TryReadRecord(recordLength, ref buffer, out var record))
                            {
                                if (record.FirstSpan[0] == 0x2A && !includeDeleted)
                                {
                                    index++;
                                    continue; // Entry is marked Deleted(*)
                                }
                                var rowData = ReadRowSequenceMemo(record, index, memofs, memoBlocksize);

                                if (predicate(index, rowData))
                                    yield return rowData;
                                index++;
                            }
                            rdr.AdvanceTo(buffer.Start, buffer.End);
                            if (readRes.IsCompleted)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    for (var index = 0; index < dbfHeader.RecordCount;)
                    {
                        var readRes = await rdr.ReadAsync();
                        var buffer = readRes.Buffer;
                        while (index < dbfHeader.RecordCount && TryReadRecord(recordLength, ref buffer, out var record))
                        {
                            if (record.FirstSpan[0] == 0x2A && !includeDeleted)
                            {
                                index++;
                                continue; // Entry is marked Deleted(*)
                            }
                            var rowData = ReadRowSequence(record, index);
                            if (predicate(index, rowData))
                                yield return rowData;
                            index++;
                        }
                        rdr.AdvanceTo(buffer.Start, buffer.End);
                        if (readRes.IsCompleted)
                        {
                            break;
                        }
                    }
                }
            }
        }
    }
}
