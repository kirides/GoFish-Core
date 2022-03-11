using GoFish.DataAccess.Extensions;
using GoFish.DataAccess.Helpers;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Numerics;
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
            var result = new byte[buffer.Length];
            buffer.CopyTo(result);
            return result;
        }

        private readonly Dbf dbf;
        private readonly DbfHeader dbfHeader;

        public Encoding TextEncoding { get; }

        private readonly DbfField nullField;
        private readonly RowHandler nullFieldDataHandler;
        private readonly RowHandler nullFieldHandlerFactory;

        public DbfReader(Dbf dbf, Encoding textEncoding)
        {
            DbfTypeMap.Add('C', (i, b, f, e) =>
            {
                Span<char> str = stackalloc char[f.Length];
                var nChars = e.GetChars(b, str);

                if (f.Length == 1)
                {
                    return str[0];
                }

                str = str.Slice(0, nChars).TrimEnd('\0');
                return str.ToString();
            });
            DbfTypeMap.Add('M', (i, b, f, e) => BinaryPrimitives.ReadInt32LittleEndian(b));
            DbfTypeMap.Add('W', (i, b, f, e) => BinaryPrimitives.ReadInt32LittleEndian(b));
            DbfTypeMap.Add('G', (i, b, f, e) => BinaryPrimitives.ReadInt32LittleEndian(b));
            DbfTypeMap.Add('Y', (i, b, f, e) => BinaryPrimitives.ReadInt64LittleEndian(b) / 10000m); // Stored as int64 with 4 implicit decimal places
            DbfTypeMap.Add('D', (i, b, f, e) =>
            {
                Span<char> dateStr = stackalloc char[b.Length];
                var nChars = e.GetChars(b, dateStr);
                dateStr = dateStr.Slice(0, nChars).Trim();

               return dateStr.IsEmpty ? DateTime.MinValue : DateTime.ParseExact(dateStr, "yyyyMMdd", null);
            });
            DbfTypeMap.Add('T', (i, b, f, e) => JulianDateHelper.FromULongBuffer(b));
            DbfTypeMap.Add('N', (i, b, f, e) =>
            {
                Span<char> numStr = stackalloc char[f.Length];
                var nChars = e.GetChars(b, numStr);
                numStr = numStr.Slice(0, nChars).Trim();

                if (f.DecimalCount == 0)
                {
                    if (f.Length < 3)
                    {
                        if (numStr.IsEmpty) return (byte)0;
                        return byte.Parse(numStr);
                    }
                    else if (f.Length < 5)
                    {
                        if (numStr.IsEmpty) return (short)0;
                        return short.Parse(numStr);
                    }
                    else if (f.Length < 10)
                    {
                        if (numStr.IsEmpty) return (int)0; 
                        return int.Parse(numStr);
                    }
                    else if (f.Length < 19)
                    {
                        if (numStr.IsEmpty) return (long)0; 
                        return long.Parse(numStr);
                    }
                    else
                    {
                        if (numStr.IsEmpty) return (BigInteger)0;
                        return BigInteger.Parse(numStr);
                    }
                }
                return numStr.IsEmpty ? 0m : decimal.Parse(numStr);
            });
            DbfTypeMap.Add('B', (i, b, f, e) => BinaryPrimitives.ReadInt32LittleEndian(b));
            DbfTypeMap.Add('O', (i, b, f, e) => BinaryPrimitives.ReadDoubleLittleEndian(b));
            DbfTypeMap.Add('F', (i, b, f, e) =>
            {
                Span<char> numStr = stackalloc char[30];
                var nChars = e.GetChars(b, numStr);
                numStr = numStr.Slice(0, nChars).Trim();

                return numStr.IsEmpty ? 0f : float.Parse(numStr);
            });
            DbfTypeMap.Add('I', (i, b, f, e) => BinaryPrimitives.ReadInt32LittleEndian(b));
            DbfTypeMap.Add('L', (i, b, f, e) => BitConverter.ToBoolean(b));
            DbfTypeMap.Add('Q', CopyFieldBuffer);
            DbfTypeMap.Add('P', CopyFieldBuffer);

            this.dbf = dbf;
            TextEncoding = textEncoding;
            dbfHeader = dbf.GetHeader();

            if (IsVisualFoxPro(dbfHeader))
            { // Special handling for VFP
                DbfTypeMap['B'] = (i, b, f, e) => BinaryPrimitives.ReadDoubleLittleEndian(b);

                // VarChar/VarBinary
                // SPECIAL CASE for VARCHAR/BINARY
                DbfTypeMap['V'] = (i, b, f, e) => f.Flags.HasFlag(DbfFieldFlags.Binary) ? CopyFieldBuffer(i, b, f, e) : e.GetString(b);

                // _NullFlags
                nullField = dbfHeader.Fields.FirstOrDefault(x => x.Type == '0');
                nullFieldDataHandler = DbfTypeMap['0'] = CopyFieldBuffer;

                if (nullField != null)
                {
                    if (nullField.Length == 1)
                    {
                        nullFieldHandlerFactory = (i, b, f, e) => new UIntNullFieldHandler(b[0]);
                    }
                    else
                    {
                        nullFieldHandlerFactory = (i, b, f, e) => new BitArrayNullFieldHandler(b.ToArray());
                    }
                }
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
            INullFieldHandler nullFieldHandler = null;
            if (nullField != null)
            {
                rowData[nullField.Index] = nullFieldDataHandler(index, rowBuf.Slice(nullField.Displacement, nullField.Length), nullField, null);
                nullFieldHandler = (INullFieldHandler)nullFieldHandlerFactory(index, rowBuf.Slice(nullField.Displacement, nullField.Length), nullField, null);
            }
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                if (field == nullField) continue;

                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                var hasNullFlag = false;

                if (nullFieldHandler != null && field.CanBeNull)
                {
                    hasNullFlag = nullFieldHandler.IsNull(field.NullFieldIndex);
                }
                if (field.Type == 'M' || field.Type == 'W' || field.Type == 'G')
                {
                    if (hasNullFlag) continue;

                    var memoPointer = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
                    rowData[i] = $"MEMO@{memoPointer}";
                }
                else if (field.Type == 'V' || field.Type == 'Q')
                {
                    if (hasNullFlag)
                    {
                        rowData[i] = null;
                    }
                    else if (nullFieldHandler.IsNull(field.VarLengthSizeIndex))
                    {
                        var valueLength = rowBuf[field.Displacement + field.Length - 1];
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, valueLength), field, TextEncoding);
                    }
                    else
                    {
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
                    }
                }
                else
                {
                    if (hasNullFlag) continue;
                    rowData[i] = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
                }
            }
            return rowData;
        }
        private object[] ReadRowFromBufferMemo(ReadOnlySpan<byte> rowBuf, int index, Stream memofs, short memoBlocksize)
        {
            var rowData = new object[dbfHeader.Fields.Count];
            INullFieldHandler nullFieldHandler = null;
            if (nullField != null)
            {
                rowData[nullField.Index] = nullFieldDataHandler(index, rowBuf.Slice(nullField.Displacement, nullField.Length), nullField, null);
                nullFieldHandler = (INullFieldHandler)nullFieldHandlerFactory(index, rowBuf.Slice(nullField.Displacement, nullField.Length), nullField, null);
            }
            Span<byte> intBuf = stackalloc byte[4];
            for (var i = 0; i < rowData.Length; i++)
            {
                var field = dbfHeader.Fields[i];
                if (field == nullField) continue;

                var hasHandler = DbfTypeMap.TryGetValue(field.Type, out var handler);
                if (!hasHandler) continue;

                var hasNullFlag = false;
                if (nullFieldHandler != null && field.CanBeNull)
                {
                    hasNullFlag = nullFieldHandler.IsNull(field.NullFieldIndex);
                }

                if (field.Type == 'M')
                {
                    if (hasNullFlag) continue;
                    var offset = (int)handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
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
                else if (field.Type == 'V' || field.Type == 'Q')
                {
                    if (hasNullFlag)
                    {
                        rowData[i] = null;
                    }
                    else if (nullFieldHandler.IsNull(field.VarLengthSizeIndex))
                    {
                        var valueLength = rowBuf[field.Displacement + field.Length - 1];
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, valueLength), field, TextEncoding);
                    }
                    else
                    {
                        rowData[i] = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
                    }
                }
                else
                {
                    if (hasNullFlag) continue;
                    rowData[i] = handler(index, rowBuf.Slice(field.Displacement, field.Length), field, TextEncoding);
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
