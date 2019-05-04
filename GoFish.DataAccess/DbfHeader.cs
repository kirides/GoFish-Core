using System.Collections.Generic;

namespace GoFish.DataAccess
{
    public class DbfHeader
    {
        public DbfType Type { get; set; }
        public int RecordCount { get; set; }
        public short HeaderSize { get; set; }
        public short RecordLength { get; set; }
        public long FileSize { get; set; }
        public DbfHeaderFlags Flags { get; set; }
        public List<DbfField> Fields { get; set; }
        public string Backlink { get; set; }
        public int CalculatedRecordCount => (int)(FileSize - HeaderSize) / RecordLength;
    }
}
