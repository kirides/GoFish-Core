using System;
using System.Buffers.Binary;
using System.IO;

namespace GoFish.DataAccess.Extensions;

// ---- EXTENSIONS -----------------------
public static class ByteParseExtensions
{
    public static int ReadIntLE(this Stream s)
    {
        Span<byte> buf = stackalloc byte[4];
        s.Read(buf);
        return BinaryPrimitives.ReadInt32LittleEndian(buf);
    }

    public static int ReadIntBE(this Stream s, Span<byte> buffer)
    {
        s.Read(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public static short ReadShortBE(this Stream s)
    {
        Span<byte> buf = stackalloc byte[2];
        s.Read(buf);
        return BinaryPrimitives.ReadInt16BigEndian(buf);
    }

    public static short ReadShortLE(this Stream s)
    {
        Span<byte> buf = stackalloc byte[2];
        s.Read(buf);
        return BinaryPrimitives.ReadInt16LittleEndian(buf);
    }

    public static void WriteNumber(this Stream s, byte[] buffer, int offset, byte count, bool bigEndian)
    {
        if (!BitConverter.IsLittleEndian && !bigEndian || BitConverter.IsLittleEndian && bigEndian)
            Array.Reverse(buffer, offset, count);
        s.Write(buffer, offset, count);
    }

    public static void WriteInt(this Stream s, int value, bool bigEndian = false)
    {
        var buf = BitConverter.GetBytes(value);
        WriteNumber(s, buf, 0, sizeof(int), bigEndian);
    }

    public static void WriteShort(this Stream s, short value, bool bigEndian = false)
    {
        var buf = BitConverter.GetBytes(value);
        WriteNumber(s, buf, 0, sizeof(short), bigEndian);
    }
}
