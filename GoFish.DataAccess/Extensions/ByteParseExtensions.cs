using System;
using System.IO;
using System.Threading.Tasks;

namespace GoFish.DataAccess.Extensions
{
    // ---- EXTENSIONS -----------------------
    public static class ByteParseExtensions
    {
        private static void ReadNumber(Stream s, byte[] buffer, int offset, int count, bool bigEndian)
        {
            count = s.Read(buffer, offset, count);
            if (!BitConverter.IsLittleEndian && !bigEndian || BitConverter.IsLittleEndian && bigEndian)
                Array.Reverse(buffer, offset, count);
        }

        public static int ReadInt(this Stream s, bool bigEndian = false)
        {
            Span<byte> buf = stackalloc byte[4];
            s.Read(buf);
            if (bigEndian)
            {
                return buf[0] << 24 | buf[1] << 16 | buf[2] << 8 | buf[3];
            }
            else
            {
                return BitConverter.ToInt32(buf);
            }
            //var buf = new byte[sizeof(int)];
            //ReadNumber(s, buf, 0, sizeof(int), bigEndian);
            //return BitConverter.ToInt32(buf, 0);
        }

        public static short ReadShort(this Stream s, bool bigEndian = false)
        {
            var buf = new byte[sizeof(short)];
            ReadNumber(s, buf, 0, sizeof(short), bigEndian);
            return BitConverter.ToInt16(buf, 0);
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
}
