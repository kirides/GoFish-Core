using System;
using System.Collections.Generic;

namespace GoFish.DataAccess.VisualFoxPro;

public class Database : DatabaseObject
{
    public List<DatabaseTable> Tables { get; private set; }
    public List<DatabaseStoredProcedure> StoredProcedures { get; private set; }
    public string Name { get; private set; }

    public static Database FromDbf(Dbf dbf)
    {
        var header = dbf.GetHeader();
        if ((header.Flags & DbfHeaderFlags.DBC) == 0)
        {
            throw new InvalidCastException("Not a Database(DBC)");
        }
        var reader = new DbfReader(dbf);
        var rows = reader.ReadRows(includeMemo: true);
        var db = ParseDatabaseFromRows(rows);

        return db;
    }

    private static Database ParseDatabaseFromRows(IEnumerable<object[]> rows)
    {
        const string TABLE_IDENTIFIER = "Table     ";
        const string FIELD_IDENTIFIER = "Field     ";
        const string INDEX_IDENTIFIER = "Index     ";
        const string DATABASE_IDENTIFIER = "Database  ";

        var db = new Database();
        db.Tables = new List<DatabaseTable>();
        var tables = new Dictionary<int, DatabaseTable>();
        foreach (var row in rows)
        {
            if (((string)row[2]).Equals(TABLE_IDENTIFIER, StringComparison.Ordinal))
            {
                var table = new DatabaseTable
                {
                    Name = ((string)row[3]).TrimEnd(),
                    Fields = new List<DatabaseTableField>(),
                    Indices = new List<DatabaseTableIndex>(),
                    Id = (int)row[0],
                    ParentId = (int)row[1],
                };
                tables[table.Id] = table;
                db.Tables.Add(table);
            }
            else if (((string)row[2]).Equals(FIELD_IDENTIFIER, StringComparison.Ordinal))
            {
                var parentId = (int)row[1];
                tables[parentId].Fields.Add(new DatabaseTableField
                {
                    Name = ((string)row[3]).TrimEnd(),
                    Id = (int)row[0],
                    ParentId = parentId,
                });
            }
            else if (((string)row[2]).Equals(INDEX_IDENTIFIER, StringComparison.Ordinal))
            {
                var parentId = (int)row[1];
                tables[parentId].Indices.Add(new DatabaseTableIndex
                {
                    Expression = ((string)row[3]).TrimEnd(),
                    Id = (int)row[0],
                    ParentId = parentId,
                });
            }
            else if (((string)row[2]).Equals(DATABASE_IDENTIFIER, StringComparison.Ordinal) && (int)row[0] == 1)
            {
                db.Id = (int)row[0];
                db.ParentId = (int)row[1];
                db.Name = ((string)row[3]).TrimEnd();
            }
        }
        return db;
    }
}

public class DatabaseObject
{
    public int Id { get; set; }
    public int ParentId { get; set; }
}
public class DatabaseStoredProcedure : DatabaseObject
{
    public string Name { get; set; }
    public string Expression { get; set; }
}
public class DatabaseTable : DatabaseObject
{
    public string Name { get; set; }
    public int Index { get; set; }
    public List<DatabaseTableField> Fields { get; set; }
    public List<DatabaseTableIndex> Indices { get; set; }
}

public class DatabaseTableIndex : DatabaseObject
{
    public string Expression { get; set; }
}
public class DatabaseTableField : DatabaseObject
{
    public string Name { get; set; }
}
