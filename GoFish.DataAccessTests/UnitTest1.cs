using Xunit;

namespace GoFish.DataAccessTests
{
    using GoFish.DataAccess;
    using GoFish.DataAccess.VisualFoxPro;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Xunit.Abstractions;

    public class UnitTest1
    {
        private static readonly string testDir = @"C:\Users\john heckendorf\Documents\Visual FoxPro Projects\testProj\";

        private readonly ITestOutputHelper output;

        public UnitTest1(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void PosHistorieAusgebenTable()
        {
            var dbf = new Dbf(Path.Combine(testDir, "poshistorie.dbf"));
            var reader = new DbfReader(dbf);
            var header = dbf.GetHeader();
            using (var sw = new StreamWriter(Path.Combine(testDir, $"Fact_{nameof(PosHistorieAusgebenTable)}.md")))
            {
                //reader.ReadRows((i, v) => i > (header.CalculatedRecordCount - 10), true).Count();
                //OutputHelper.MarkdownTable(header.Fields, reader.ReadRows(true).Take(50), h => h, sw.WriteLine);
                OutputHelper.MarkdownTable(
                    new[] { "RAW CONTENT" },
                    reader.ReadRowsRaw().Select(x=> new [] { OutputHelper.ByteArrayToX2(x) }).Take(50).ToList(),
                    h => h,
                    sw.WriteLine);
            }
        }

        [Fact]
        public void Test1()
        {
            var dbf = new Dbf(Path.Combine(testDir, "dbTable2.dbf"));
            //var dbf = new Dbf(Path.Combine(@"C:\vfp projects\WindowsShell", "varchr.dbf"));
            var reader = new DbfReader(dbf);

            var row = reader.ReadRow(0);
            var header = dbf.GetHeader();

            OutputHelper.MarkdownTable(header.Fields, reader.ReadRows(), h => h, output.WriteLine);

            var row2 = reader.ReadRow(1);
            var row3 = reader.ReadRow(2);

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

    public static class OutputHelper
    {
        public static void MarkdownTable<THeader, TRow>(
            IEnumerable<THeader> headers,
            IEnumerable<IEnumerable<TRow>> rows,
            Func<THeader, object> headerSelector,
            Action<string> writeLine)
        {
            var sb = new StringBuilder(256);
            sb.Append('|');
            foreach (var header in headers)
            {
                sb.Append(headerSelector(header));
                sb.Append('|');
            }
            writeLine(sb.ToString());
            sb.Clear();
            foreach (var header in headers)
            {
                sb.Append(":---:");
                sb.Append('|');
            }
            writeLine(sb.ToString());
            sb.Clear();
            foreach (var row in rows)
            {
                sb.Append('|');
                foreach (var value in row)
                {
                    sb.Append(value);
                    sb.Append('|');
                }
                writeLine(sb.ToString());
                sb.Clear();
            }
        }

        public static string ByteArrayToX2(byte[] bytes)
        {
            return string.Create(bytes.Length * 2, bytes, (c, buf) =>
            {
                for (int i = 0; i < buf.Length; i += 2)
                {
                    var x = buf[i].ToString("X2");
                    c[i] = x[0];
                    c[i + 1] = x[1];
                }
            });
        }
    }
}
