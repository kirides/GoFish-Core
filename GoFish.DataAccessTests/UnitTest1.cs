using Xunit;

namespace GoFish.DataAccessTests
{
    using GoFish.DataAccess;
    using GoFish.DataAccess.VisualFoxPro;
    using System.IO;
    using System.Linq;

    public class UnitTest1
    {
        private static readonly string testDir = @"C:\Users\johnh\Documents\Visual FoxPro Projects\testProj\";

        [Fact]
        public void Test1()
        {
            //var dbf = new Dbf(Path.Combine(testDir, "dbTable2.dbf"));
            var dbf = new Dbf(Path.Combine(@"C:\vfp projects\WindowsShell", "varchr.dbf"));
            var reader = new DbfReader(dbf);
            var row = reader.ReadRow(0);
            var row2 = reader.ReadRow(1);
            var row3 = reader.ReadRow(2);

            var header = dbf.GetHeader();
            //var rows = reader.ReadRows(includeMemo: true).ToList();

            //Assert.Equal(6, reader.ReadRow(1)[1].ToString().Length);
            //Assert.Equal(254, reader.ReadRow(0)[1].ToString().Length);
            //Assert.Equal(254, reader.ReadRow(2)[1].ToString().Length);
        }

        [Fact]
        public void TestDBC()
        {
            var dbf = new Dbf(Path.Combine(testDir, "largedb.dbc"), Path.Combine(testDir, "largedb.dct"));
            var reader = new DbfReader(dbf);
            var rows = reader.ReadRows(includeMemo: true);
            var db = Database.FromDbf(dbf);

            //Assert.Equal(6, reader.ReadRow(1)[1].ToString().Length);
            //Assert.Equal(254, reader.ReadRow(0)[1].ToString().Length);
            Assert.Equal(254, reader.ReadRow(2)[1].ToString().Length);
        }
    }
}
