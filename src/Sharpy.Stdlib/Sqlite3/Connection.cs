using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Sharpy
{
    /// <summary>Represents a connection to an SQLite database.</summary>
    [SharpyModuleType("sqlite3", "Connection")]
    public class Sqlite3Connection : IDisposable
    {
        private SqliteConnection? _connection;
        private SqliteTransaction? _transaction;

        /// <summary>Gets or sets the row factory used to create row objects from query results.</summary>
        public Func<Sqlite3Cursor, object?[], object>? RowFactory { get; set; }

        internal Sqlite3Connection(string database)
        {
            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = database
            }.ToString();
            _connection = new SqliteConnection(connectionString);
            try
            {
                _connection.Open();
            }
            catch (SqliteException ex)
            {
                throw WrapException(ex);
            }
        }

        internal SqliteConnection GetRawConnection()
        {
            if (_connection == null)
            {
                throw new Sqlite3ProgrammingError("Cannot operate on a closed database.");
            }

            return _connection;
        }

        /// <summary>Create a new cursor object for this connection.</summary>
        /// <returns>A new <see cref="Sqlite3Cursor"/>.</returns>
        public Sqlite3Cursor Cursor()
        {
            EnsureOpen();
            return new Sqlite3Cursor(this);
        }

        /// <summary>Create a cursor, execute a single SQL statement, and return the cursor.</summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="parameters">Optional parameters to bind to placeholders in the SQL.</param>
        /// <returns>The cursor that executed the statement.</returns>
        public Sqlite3Cursor Execute(string sql, System.Collections.IEnumerable? parameters = null)
        {
            var cursor = Cursor();
            cursor.Execute(sql, parameters);
            return cursor;
        }

        /// <summary>Create a cursor and execute an SQL statement against all parameter sequences.</summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="seqOfParameters">An iterable of parameter sequences.</param>
        /// <returns>The cursor that executed the statements.</returns>
        public Sqlite3Cursor Executemany(string sql, System.Collections.IEnumerable seqOfParameters)
        {
            var cursor = Cursor();
            cursor.Executemany(sql, seqOfParameters);
            return cursor;
        }

        /// <summary>Create a cursor and execute a script of one or more SQL statements.</summary>
        /// <param name="sqlScript">A string containing one or more SQL statements separated by semicolons.</param>
        /// <returns>The cursor that executed the script.</returns>
        public Sqlite3Cursor Executescript(string sqlScript)
        {
            var cursor = Cursor();
            cursor.Executescript(sqlScript);
            return cursor;
        }

        /// <summary>Commit the current transaction.</summary>
        public void Commit()
        {
            EnsureOpen();
            if (_transaction != null)
            {
                try
                {
                    _transaction.Commit();
                }
                catch (SqliteException ex)
                {
                    throw WrapException(ex);
                }
                finally
                {
                    _transaction.Dispose();
                    _transaction = null;
                }
            }
        }

        /// <summary>Roll back the current transaction.</summary>
        public void Rollback()
        {
            EnsureOpen();
            if (_transaction != null)
            {
                try
                {
                    _transaction.Rollback();
                }
                catch (SqliteException ex)
                {
                    throw WrapException(ex);
                }
                finally
                {
                    _transaction.Dispose();
                    _transaction = null;
                }
            }
        }

        /// <summary>Close the connection. Any pending transaction is rolled back.</summary>
        public void Close()
        {
            if (_connection != null)
            {
                if (_transaction != null)
                {
                    try
                    {
                        _transaction.Rollback();
                    }
                    catch (SqliteException)
                    {
                    }

                    _transaction.Dispose();
                    _transaction = null;
                }

                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }

        /// <summary>Dispose the connection, releasing all resources.</summary>
        public void Dispose()
        {
            Close();
        }

        internal void EnsureTransaction()
        {
            EnsureOpen();
            if (_transaction == null)
            {
                _transaction = _connection!.BeginTransaction();
            }
        }

        internal SqliteTransaction? GetTransaction()
        {
            return _transaction;
        }

        internal void CommitPendingTransaction()
        {
            if (_transaction != null)
            {
                try
                {
                    _transaction.Commit();
                }
                catch (SqliteException ex)
                {
                    throw WrapException(ex);
                }
                finally
                {
                    _transaction.Dispose();
                    _transaction = null;
                }
            }
        }

        private void EnsureOpen()
        {
            if (_connection == null)
            {
                throw new Sqlite3ProgrammingError("Cannot operate on a closed database.");
            }
        }

        internal static Exception WrapException(SqliteException ex)
        {
            int code = ex.SqliteErrorCode;
            if (code == 19) // SQLITE_CONSTRAINT
            {
                return new Sqlite3IntegrityError(ex.Message, ex);
            }

            return new Sqlite3OperationalError(ex.Message, ex);
        }
    }
}
