using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Sharpy
{
    [SharpyModuleType("sqlite3", "Connection")]
    public class Sqlite3Connection : IDisposable
    {
        private SqliteConnection? _connection;
        private SqliteTransaction? _transaction;

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

        public Sqlite3Cursor Cursor()
        {
            EnsureOpen();
            return new Sqlite3Cursor(this);
        }

        public Sqlite3Cursor Execute(string sql, System.Collections.IEnumerable? parameters = null)
        {
            var cursor = Cursor();
            cursor.Execute(sql, parameters);
            return cursor;
        }

        public Sqlite3Cursor Executemany(string sql, System.Collections.IEnumerable seqOfParameters)
        {
            var cursor = Cursor();
            cursor.Executemany(sql, seqOfParameters);
            return cursor;
        }

        public Sqlite3Cursor Executescript(string sqlScript)
        {
            var cursor = Cursor();
            cursor.Executescript(sqlScript);
            return cursor;
        }

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
