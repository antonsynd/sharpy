using System;

namespace Sharpy
{
    /// <summary>Provides functions for working with SQLite databases, similar to Python's sqlite3 module.</summary>
    public static partial class Sqlite3
    {
        /// <summary>Open a connection to an SQLite database.</summary>
        /// <param name="database">The path to the database file, or ":memory:" for an in-memory database.</param>
        /// <returns>A new <see cref="Sqlite3Connection"/> to the specified database.</returns>
        public static Sqlite3Connection Connect(string database)
        {
            return new Sqlite3Connection(database);
        }

        /// <summary>A factory function that returns <see cref="Sqlite3Row"/> objects for query results.</summary>
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
