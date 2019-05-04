using System;
using System.IO;

namespace GoFish.DataAccess.Helpers
{
    public static class DbfFilePathHelper
    {
        public static string GetMemoExtension(string dbfExtension)
        {
            switch (dbfExtension.ToUpperInvariant())
            {
                case ".DBF": return ".FPT";
                case ".SCX": return ".SCT";
                case ".VCX": return ".VCT";
                case ".PJX": return ".PJT";
                case ".DBC": return ".DCT";
                default: throw new NotSupportedException($"{dbfExtension} is not supported.");
            }
        }

        public static string GetMemoPath(string dbfPath)
        {
            return Path.Combine(Path.GetDirectoryName(dbfPath), Path.GetFileNameWithoutExtension(dbfPath) + GetMemoExtension(Path.GetExtension(dbfPath)));
        }
    }
}
