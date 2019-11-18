namespace GoFish.DataAccess
{
    public class DbfField
    {
        public string Name { get; set; }
        public char Type { get; set; }
        public byte Length { get; set; }
        public byte DecimalCount { get; set; }
        public int NextAutoIncrement { get; set; }
        public int Index { get; set; }
        public byte[] Reserved24To31 { get; set; }

        public DbfFieldFlags Flags { get; set; }
        public int Displacement { get; set; }
        public byte AutoIncrementStep { get; set; }
        public int NullFieldIndex { get; set; }
        public int VarCharIsPartialIndex { get; set; }

        public override string ToString()
         => $"{Name} {Type}({Length}{(DecimalCount > 0 ? $", {DecimalCount}" : "")})";

        public bool CanBeNull => (Flags & DbfFieldFlags.Null) != 0;
    }
}
