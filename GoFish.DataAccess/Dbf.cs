﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoFish.DataAccess.Extensions;
using GoFish.DataAccess.Helpers;
using GoFish.DataAccess.VisualFoxPro;

namespace GoFish.DataAccess;

public class Dbf
{
    private const byte FieldDescriptorTerminator = 0x0D;
    private readonly string dbfPath;
    private readonly string memoPath;
    private readonly Encoding encoding;
    private static readonly ArrayPool<byte> bufferPool = ArrayPool<byte>.Shared;

    public Dbf(string filePath)
    : this(filePath, DbfFilePathHelper.GetMemoPath(filePath))
    { }
    public Dbf(string filePath, Encoding encoding)
    : this(filePath, DbfFilePathHelper.GetMemoPath(filePath), encoding)
    { }

    public Dbf(string filePath, string memoFilePath)
        : this(filePath, memoFilePath, CodePagesEncodingProvider.Instance.GetEncoding(1252))
    { }
    public Dbf(string filePath, string memoFilePath, Encoding encoding)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File not found: {filePath}", filePath);
        this.dbfPath = filePath;
        if (File.Exists(memoFilePath))
        {
            this.memoPath = memoFilePath;
        }
        this.encoding = encoding;
    }

    public Stream OpenMemo()
    {
        if (string.IsNullOrEmpty(memoPath)) throw new InvalidOperationException($"No associated MEMO-file ({DbfFilePathHelper.GetMemoExtension(Path.GetExtension(dbfPath))}) found.");
        return new FileStream(memoPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete, short.MaxValue);
    }

    public Stream OpenReadOnly()
    {
        return new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete, 4096);
    }

    public Stream OpenReadOnlyAsync()
    {
        return new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete, 4096, FileOptions.Asynchronous);
    }

    public DbfHeader GetHeader()
    {
        var header = new DbfHeader();
        using (var fs = OpenReadOnly())
        {
            header.Type = (DbfType)fs.ReadByte();
            fs.Position = 4;
            header.RecordCount = fs.ReadIntLE();
            header.HeaderSize = fs.ReadShortLE();
            header.RecordLength = fs.ReadShortLE();
            header.FileSize = fs.Length;

            fs.Position = 28;
            header.Flags = (DbfHeaderFlags)fs.ReadByte();

            switch (header.Type)
            {
                case DbfType.VisualFoxPro:
                case DbfType.VisualFoxProAutoInc:
                case DbfType.VisualFoxProVar:
                    DatabaseTable dbTable = null;
                    if ((header.Flags & DbfHeaderFlags.DBC) == 0)
                    {
                        FillBacklink(fs, header);
                        if (!string.IsNullOrEmpty(header.Backlink))
                        {
                            var db = Database.FromDbf(new Dbf(Path.Combine(Path.GetDirectoryName(dbfPath), header.Backlink)));
                            dbTable = db.Tables.Find(t => t.Name.Equals(Path.GetFileNameWithoutExtension(dbfPath), StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    FillVFPFields(fs, header, dbTable);
                    break;
                default:
                    throw new NotSupportedException($"Field Data for DBF-Type {header.Type} is not supported.");
            }
        }

        return header;
    }

    private void FillBacklink(Stream fs, DbfHeader header)
    {
        const int maxBacklinkLengh = 263;
        fs.Seek(header.HeaderSize - maxBacklinkLengh, SeekOrigin.Begin);
        var fieldBuf = bufferPool.Rent(maxBacklinkLengh);
        var read = fs.Read(fieldBuf, 0, maxBacklinkLengh);
        var backlinkLength = read;
        if (read != 0)
        {
            var nullIdx = GetIndexOf(fieldBuf, 0, 0);
            if (nullIdx != -1)
            {
                backlinkLength = nullIdx;
            }
        }
        header.Backlink = encoding.GetString(fieldBuf, 0, backlinkLength);
        bufferPool.Return(fieldBuf);
    }

    private int GetIndexOf(byte[] fieldBuf, int offset, byte v)
    {
        for (var i = offset; i < fieldBuf.Length; i++)
        {
            if (fieldBuf[i] == v)
            {
                return i;
            }
        }
        return -1;
    }

    public void SetHeader(int recordCount, short headerSize, short recordSize)
    {
        using (var fs = File.Open(dbfPath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            if (fs.Length < 12)
            {
                throw new Exception($"Invalid file size ({fs.Length} B)");
            }
            fs.Seek(4, SeekOrigin.Begin);
            fs.WriteInt(recordCount);
            fs.WriteShort(headerSize);
            fs.WriteShort(recordSize);
        }
    }
    public void SetRecordCount(int recordCount)
    {
        using (var fs = File.Open(dbfPath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            if (fs.Length < 12)
            {
                throw new Exception($"Invalid file size ({fs.Length} B)");
            }
            fs.Seek(4, SeekOrigin.Begin);
            fs.WriteInt(recordCount);
        }
    }

    private void FillVFPFields(Stream fs, DbfHeader header, DatabaseTable dbTable)
    {
        const int fieldSize = 32;
        header.Fields = new List<DbfField>();
        fs.Seek(32, SeekOrigin.Begin);
        var fieldBuf = bufferPool.Rent(fieldSize);
        var index = 0;
        int nullFieldIndex = -1;
        while (fs.Read(fieldBuf, 0, fieldSize) != 0 && fieldBuf[0] != FieldDescriptorTerminator)
        {
            var field = new VfpField
            {
                Name = encoding.GetString(fieldBuf, 0, 10).TrimEnd('\0'),
                Type = (char)fieldBuf[11],
                Displacement = BTOI(fieldBuf, 12),
                Length = fieldBuf[16],
                DecimalCount = fieldBuf[17],
                Flags = (DbfFieldFlags)fieldBuf[18],
                NextAutoIncrement = BTOI(fieldBuf, 19),
                AutoIncrementStep = fieldBuf[23],
                Index = index,
                VarLengthSizeIndex = -1,
                NullFieldIndex = -1,
            };
            if (field.Type == 'V' || field.Type == 'Q')
            {
                nullFieldIndex++;
                field.VarLengthSizeIndex = nullFieldIndex;
            }
            if ((field.Flags & DbfFieldFlags.Null) != 0)
            {
                nullFieldIndex++;
                field.NullFieldIndex = nullFieldIndex;
            }
            Buffer.BlockCopy(fieldBuf, 24, field.Reserved24To31 = new byte[8], 0, 8);
            if (dbTable != null && dbTable.Fields.Count > index)
            {
                field.Name = dbTable.Fields[index].Name;
            }
            header.Fields.Add(field);
            index++;
        }
        bufferPool.Return(fieldBuf);
    }

    private int BTOI(in byte[] buf, in int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(offset, 4));
        return buf[offset] | buf[offset + 1] << 8 | buf[offset + 2] << 16 | buf[offset + 3] << 24;
    }
}
