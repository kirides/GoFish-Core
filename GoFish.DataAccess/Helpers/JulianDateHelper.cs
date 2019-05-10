using System;

namespace GoFish.DataAccess.Helpers
{
    public static class JulianDateHelper
    {
        /// <summary>
        /// Reads UInt64 from buffer offset and converts it to DateTimeOffset.
        /// </summary>
        /// <param name="buffer">Buffer</param>
        /// <param name="offset">Where the UInt64 starts</param>
        public static DateTimeOffset FromULongBuffer(ReadOnlySpan<byte> buffer)
        {
            return FromLong(
                System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(buffer));
        }
        public static DateTimeOffset FromLong(ulong julianDateLong)
        {
            var dateWord = (int)julianDateLong;
            var timeWord = (int)(julianDateLong >> 32);

            // Convert date word to DateTime using Julian calendar
            var date = JulianToDateTime(dateWord);

            // Get hour, minute, second from time word
            var hour = timeWord / 3600000;
            timeWord -= hour * 3600000;
            var minute = timeWord / 60000;
            timeWord -= minute * 60000;
            var second = timeWord / 1000;

            // Add time to DateTime
            return new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, second, TimeSpan.Zero);
        }
        // Convert a Julian Date as long to a .NET DateTime structure 
        // (see http://en.wikipedia.org/wiki/Julian_day)
        private static DateTime JulianToDateTime(long julianDateAsLong)
        {
            if (julianDateAsLong == 0) return DateTime.MinValue;
            var p = (double)julianDateAsLong;
            var s1 = p + 68569;
            var n = Math.Floor(4 * s1 / 146097);
            var s2 = s1 - Math.Floor(((146097 * n) + 3) / 4);
            var i = Math.Floor(4000 * (s2 + 1) / 1461001);
            var s3 = s2 - Math.Floor(1461 * i / 4) + 31;
            var q = Math.Floor(80 * s3 / 2447);
            var d = s3 - Math.Floor(2447 * q / 80);
            var s4 = Math.Floor(q / 11);
            var m = q + 2 - (12 * s4);
            var j = (100 * (n - 49)) + i + s4;
            return new DateTime((int)j, (int)m, (int)d);
        }
    }
}
