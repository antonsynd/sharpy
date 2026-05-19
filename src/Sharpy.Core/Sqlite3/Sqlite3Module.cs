using System;

namespace Sharpy
{
    public static partial class Sqlite3
    {
        public static Sqlite3Connection Connect(string database)
        {
            return new Sqlite3Connection(database);
        }

        public static readonly Func<Sqlite3Cursor, object?[], object> Row = Sqlite3RowFactory;

        private static object Sqlite3RowFactory(Sqlite3Cursor cursor, object?[] values)
        {
            if (cursor.ColumnNames == null)
            {
                throw new Sqlite3ProgrammingError("No description available.");
            }

            return new Sqlite3Row(values, cursor.ColumnNames);
        }
    }
}
