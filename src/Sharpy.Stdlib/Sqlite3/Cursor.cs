using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Sharpy
{
    /// <summary>Represents a database cursor used to execute SQL statements and fetch results.</summary>
    [SharpyModuleType("sqlite3", "Cursor")]
    public class Sqlite3Cursor : IEnumerable<object>, IDisposable
    {
        private readonly Sqlite3Connection _connection;
        private SqliteDataReader? _reader;
        private bool _closed;

        /// <summary>Gets or sets the number of rows to fetch at a time with <see cref="Fetchmany"/>. Default is 1.</summary>
        public int Arraysize { get; set; }
        /// <summary>Gets the row ID of the last modified row, or -1 if no row was inserted.</summary>
        public long Lastrowid { get; private set; }
        /// <summary>Gets the number of rows affected by the last DML statement, or -1 for queries.</summary>
        public int Rowcount { get; private set; }
        /// <summary>Gets column descriptions for the last query, or null if no query has been executed.</summary>
        public System.Collections.Generic.List<object?[]>? Description { get; private set; }
        internal string[]? ColumnNames { get; private set; }

        internal Sqlite3Cursor(Sqlite3Connection connection)
        {
            _connection = connection;
            Arraysize = 1;
            Lastrowid = -1;
            Rowcount = -1;
        }

        /// <summary>Execute a single SQL statement, optionally binding parameters.</summary>
        /// <param name="sql">The SQL statement to execute. Use <c>?</c> as a placeholder for positional parameters.</param>
        /// <param name="parameters">Optional parameters to bind to placeholders in the SQL.</param>
        /// <returns>This cursor instance.</returns>
        public Sqlite3Cursor Execute(string sql, System.Collections.IEnumerable? parameters = null)
        {
            EnsureOpen();
            CloseReader();

            string trimmed = sql.TrimStart();
            bool isDml = IsDml(trimmed);
            bool isDdl = IsDdl(trimmed);

            if (isDml)
            {
                _connection.EnsureTransaction();
            }

            SqliteConnection rawConn = _connection.GetRawConnection();
            SqliteCommand command = rawConn.CreateCommand();
            object?[]? paramArray = ToArray(parameters);
            command.CommandText = RewritePositionalParams(sql, paramArray);
            SqliteTransaction? txn = _connection.GetTransaction();
            if (txn != null)
            {
                command.Transaction = txn;
            }

            BindParameters(command, paramArray);

            try
            {
                if (isDml || isDdl)
                {
                    Rowcount = command.ExecuteNonQuery();
                    Lastrowid = GetLastRowId(rawConn);
                    Description = null;
                    ColumnNames = null;
                }
                else
                {
                    _reader = command.ExecuteReader();
                    Rowcount = -1;
                    BuildDescription();
                }
            }
            catch (SqliteException ex)
            {
                throw Sqlite3Connection.WrapException(ex);
            }

            return this;
        }

        /// <summary>Execute an SQL statement against all parameter sequences in the given iterable.</summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="seqOfParameters">An iterable of parameter sequences.</param>
        /// <returns>This cursor instance.</returns>
        public Sqlite3Cursor Executemany(string sql, System.Collections.IEnumerable seqOfParameters)
        {
            EnsureOpen();
            int totalRows = 0;
            foreach (object parameters in seqOfParameters)
            {
                Execute(sql, parameters as System.Collections.IEnumerable);
                if (Rowcount >= 0)
                {
                    totalRows += Rowcount;
                }
            }

            Rowcount = totalRows;
            return this;
        }

        /// <summary>Execute a script of one or more SQL statements, committing any pending transaction first.</summary>
        /// <param name="sqlScript">A string containing one or more SQL statements separated by semicolons.</param>
        /// <returns>This cursor instance.</returns>
        public Sqlite3Cursor Executescript(string sqlScript)
        {
            EnsureOpen();
            CloseReader();

            _connection.CommitPendingTransaction();

            SqliteConnection rawConn = _connection.GetRawConnection();
            SqliteCommand command = rawConn.CreateCommand();
            command.CommandText = sqlScript;

            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                throw Sqlite3Connection.WrapException(ex);
            }

            Description = null;
            ColumnNames = null;
            Rowcount = -1;
            return this;
        }

        /// <summary>Fetch the next row of a query result, returning null if no more data is available.</summary>
        /// <returns>The next row as an array of values, or null.</returns>
        public object? Fetchone()
        {
            EnsureOpen();
            if (_reader == null || !_reader.Read())
            {
                return null;
            }

            return MaterializeRow();
        }

        /// <summary>Fetch the next set of rows, returning a list. An empty list is returned when no more rows are available.</summary>
        /// <param name="size">The maximum number of rows to fetch. Defaults to <see cref="Arraysize"/>.</param>
        /// <returns>A list of row objects.</returns>
        public System.Collections.Generic.List<object> Fetchmany(int size = -1)
        {
            EnsureOpen();
            if (size < 0)
            {
                size = Arraysize;
            }

            var rows = new System.Collections.Generic.List<object>();
            if (_reader == null)
            {
                return rows;
            }

            for (int i = 0; i < size; i++)
            {
                if (!_reader.Read())
                {
                    break;
                }

                rows.Add(MaterializeRow());
            }

            return rows;
        }

        /// <summary>Fetch all remaining rows of a query result, returning a list.</summary>
        /// <returns>A list of all remaining row objects.</returns>
        public System.Collections.Generic.List<object> Fetchall()
        {
            EnsureOpen();
            var rows = new System.Collections.Generic.List<object>();
            if (_reader == null)
            {
                return rows;
            }

            while (_reader.Read())
            {
                rows.Add(MaterializeRow());
            }

            return rows;
        }

        /// <summary>Close the cursor. The cursor will be unusable from this point forward.</summary>
        public void Close()
        {
            CloseReader();
            _closed = true;
        }

        /// <summary>Dispose the cursor, releasing all resources.</summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>Return an enumerator that iterates over the remaining rows of the query result.</summary>
        public IEnumerator<object> GetEnumerator()
        {
            EnsureOpen();
            if (_reader == null)
            {
                yield break;
            }

            while (_reader.Read())
            {
                yield return MaterializeRow();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private object MaterializeRow()
        {
            int fieldCount = _reader!.FieldCount;
            var values = new object?[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                if (_reader.IsDBNull(i))
                {
                    values[i] = null;
                }
                else
                {
                    object raw = _reader.GetValue(i);
                    values[i] = ConvertValue(raw);
                }
            }

            Func<Sqlite3Cursor, object?[], object>? factory = _connection.RowFactory;
            if (factory != null)
            {
                return factory(this, values);
            }

            return values;
        }

        private static object ConvertValue(object raw)
        {
            if (raw is byte[] bytes)
            {
                return new Bytes(bytes);
            }

            if (raw is int intVal)
            {
                return (long)intVal;
            }

            if (raw is float floatVal)
            {
                return (double)floatVal;
            }

            if (raw is decimal decVal)
            {
                return (double)decVal;
            }

            return raw;
        }

        private void BuildDescription()
        {
            if (_reader == null)
            {
                Description = null;
                ColumnNames = null;
                return;
            }

            int fieldCount = _reader.FieldCount;
            Description = new System.Collections.Generic.List<object?[]>(fieldCount);
            ColumnNames = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                string name = _reader.GetName(i);
                ColumnNames[i] = name;
                Description.Add(new object?[] { name, null, null, null, null, null, null });
            }
        }

        private static void BindParameters(SqliteCommand command, object?[]? parameters)
        {
            if (parameters == null)
            {
                return;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                object? value = parameters[i];
                SqliteParameter param = command.CreateParameter();
                param.ParameterName = "$p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                param.Value = value ?? DBNull.Value;
                if (value is Bytes b)
                {
                    param.Value = b.ToArray();
                }

                command.Parameters.Add(param);
            }
        }

        private static long GetLastRowId(SqliteConnection rawConn)
        {
            using (SqliteCommand cmd = rawConn.CreateCommand())
            {
                cmd.CommandText = "SELECT last_insert_rowid()";
                object? result = cmd.ExecuteScalar();
                if (result is long l)
                {
                    return l;
                }

                return -1;
            }
        }

        private static bool IsDml(string trimmedSql)
        {
            return StartsWithKeyword(trimmedSql, "INSERT")
                || StartsWithKeyword(trimmedSql, "UPDATE")
                || StartsWithKeyword(trimmedSql, "DELETE")
                || StartsWithKeyword(trimmedSql, "REPLACE");
        }

        private static bool IsDdl(string trimmedSql)
        {
            return StartsWithKeyword(trimmedSql, "CREATE")
                || StartsWithKeyword(trimmedSql, "DROP")
                || StartsWithKeyword(trimmedSql, "ALTER");
        }

        private static bool StartsWithKeyword(string sql, string keyword)
        {
            if (sql.Length < keyword.Length)
            {
                return false;
            }

            for (int i = 0; i < keyword.Length; i++)
            {
                char c = sql[i];
                if (c >= 'a' && c <= 'z')
                {
                    c = (char)(c - 32);
                }

                if (c != keyword[i])
                {
                    return false;
                }
            }

            return sql.Length == keyword.Length || sql[keyword.Length] == ' ' || sql[keyword.Length] == '\t' || sql[keyword.Length] == '\n' || sql[keyword.Length] == '\r';
        }

        private static string RewritePositionalParams(string sql, object?[]? parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return sql;
            }

            var sb = new System.Text.StringBuilder(sql.Length + parameters.Length * 4);
            int paramIndex = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < sql.Length; i++)
            {
                char c = sql[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == stringChar)
                    {
                        inString = false;
                    }
                }
                else if (c == '\'' || c == '"')
                {
                    sb.Append(c);
                    inString = true;
                    stringChar = c;
                }
                else if (c == '?')
                {
                    sb.Append("$p");
                    sb.Append(paramIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    paramIndex++;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private void CloseReader()
        {
            if (_reader != null)
            {
                _reader.Close();
                _reader.Dispose();
                _reader = null;
            }
        }

        private static object?[]? ToArray(System.Collections.IEnumerable? seq)
        {
            if (seq == null)
            {
                return null;
            }

            if (seq is object?[] arr)
            {
                return arr;
            }

            var result = new System.Collections.Generic.List<object?>();
            foreach (object? item in seq)
            {
                result.Add(item);
            }

            return result.ToArray();
        }

        private void EnsureOpen()
        {
            if (_closed)
            {
                throw new Sqlite3ProgrammingError("Cannot operate on a closed cursor.");
            }
        }
    }
}
