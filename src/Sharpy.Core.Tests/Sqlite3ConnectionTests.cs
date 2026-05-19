using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Sqlite3ConnectionTests : IDisposable
{
    private Sqlite3Connection? _conn;

    private Sqlite3Connection CreateConnection()
    {
        _conn = Sqlite3.Connect(":memory:");
        return _conn;
    }

    public void Dispose()
    {
        _conn?.Close();
    }

    #region Connect

    [Fact]
    public void Connect_MemoryDatabase_Succeeds()
    {
        var conn = CreateConnection();
        conn.Should().NotBeNull();
    }

    [Fact]
    public void Connect_ReturnsConnectionInstance()
    {
        var conn = CreateConnection();
        conn.Should().BeOfType<Sqlite3Connection>();
    }

    #endregion

    #region Execute Convenience

    [Fact]
    public void Execute_CreateTable_Succeeds()
    {
        var conn = CreateConnection();
        var cursor = conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT)");
        cursor.Should().NotBeNull();
    }

    [Fact]
    public void Execute_InsertAndSelect_ReturnsData()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT)");
        conn.Execute("INSERT INTO t VALUES (1, 'Alice')");
        conn.Commit();

        var cursor = conn.Execute("SELECT id, name FROM t");
        var row = cursor.Fetchone() as object?[];
        row.Should().NotBeNull();
        row![0].Should().Be(1L);
        row[1].Should().Be("Alice");
    }

    [Fact]
    public void Execute_WithParameters_BindsCorrectly()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (id INTEGER, name TEXT)");
        conn.Execute("INSERT INTO t VALUES (?, ?)", new object?[] { 1, "Bob" });
        conn.Commit();

        var cursor = conn.Execute("SELECT name FROM t WHERE id = ?", new object?[] { 1 });
        var row = cursor.Fetchone() as object?[];
        row.Should().NotBeNull();
        row![0].Should().Be("Bob");
    }

    #endregion

    #region Commit and Rollback

    [Fact]
    public void Commit_PersistsData()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (val INTEGER)");
        conn.Execute("INSERT INTO t VALUES (42)");
        conn.Commit();

        var cursor = conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row.Should().NotBeNull();
        row![0].Should().Be(42L);
    }

    [Fact]
    public void Rollback_DiscardsUncommittedData()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (val INTEGER)");
        conn.Commit();

        conn.Execute("INSERT INTO t VALUES (99)");
        conn.Rollback();

        var cursor = conn.Execute("SELECT COUNT(*) FROM t");
        var row = cursor.Fetchone() as object?[];
        row.Should().NotBeNull();
        row![0].Should().Be(0L);
    }

    [Fact]
    public void Commit_WithNoTransaction_DoesNotThrow()
    {
        var conn = CreateConnection();
        var act = () => conn.Commit();
        act.Should().NotThrow();
    }

    [Fact]
    public void Rollback_WithNoTransaction_DoesNotThrow()
    {
        var conn = CreateConnection();
        var act = () => conn.Rollback();
        act.Should().NotThrow();
    }

    #endregion

    #region Close and Operate

    [Fact]
    public void Close_ThenExecute_ThrowsProgrammingError()
    {
        var conn = CreateConnection();
        conn.Close();
        _conn = null; // Already closed, prevent double close in Dispose

        var act = () => conn.Execute("SELECT 1");
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Close_ThenCursor_ThrowsProgrammingError()
    {
        var conn = CreateConnection();
        conn.Close();
        _conn = null;

        var act = () => conn.Cursor();
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Close_ThenCommit_ThrowsProgrammingError()
    {
        var conn = CreateConnection();
        conn.Close();
        _conn = null;

        var act = () => conn.Commit();
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Close_ThenRollback_ThrowsProgrammingError()
    {
        var conn = CreateConnection();
        conn.Close();
        _conn = null;

        var act = () => conn.Rollback();
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Close_CalledTwice_DoesNotThrow()
    {
        var conn = CreateConnection();
        conn.Close();
        _conn = null;

        var act = () => conn.Close();
        act.Should().NotThrow();
    }

    #endregion

    #region IDisposable

    [Fact]
    public void UsingStatement_ClosesConnection()
    {
        Sqlite3Connection conn;
        using (conn = Sqlite3.Connect(":memory:"))
        {
            conn.Execute("CREATE TABLE t (id INTEGER)");
        }

        // After using, connection is closed
        var act = () => conn.Execute("SELECT 1");
        act.Should().Throw<Sqlite3ProgrammingError>();
    }

    [Fact]
    public void Dispose_ClosesConnection()
    {
        var conn = Sqlite3.Connect(":memory:");
        conn.Dispose();

        var act = () => conn.Execute("SELECT 1");
        act.Should().Throw<Sqlite3ProgrammingError>();
    }

    #endregion

    #region Executemany

    [Fact]
    public void Executemany_InsertsMultipleRows()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (id INTEGER, name TEXT)");

        var paramSets = new System.Collections.Generic.List<object?[]>
        {
            new object?[] { 1, "Alice" },
            new object?[] { 2, "Bob" },
            new object?[] { 3, "Charlie" }
        };

        conn.Executemany("INSERT INTO t VALUES (?, ?)", paramSets);
        conn.Commit();

        var cursor = conn.Execute("SELECT COUNT(*) FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().Be(3L);
    }

    [Fact]
    public void Executemany_ReturnsCorrectRowcount()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (val INTEGER)");

        var paramSets = new System.Collections.Generic.List<object?[]>
        {
            new object?[] { 1 },
            new object?[] { 2 }
        };

        var cursor = conn.Executemany("INSERT INTO t VALUES (?)", paramSets);
        cursor.Rowcount.Should().Be(2);
    }

    #endregion

    #region Executescript

    [Fact]
    public void Executescript_ExecutesMultipleStatements()
    {
        var conn = CreateConnection();

        conn.Executescript(@"
            CREATE TABLE t1 (id INTEGER);
            CREATE TABLE t2 (id INTEGER);
            INSERT INTO t1 VALUES (1);
            INSERT INTO t2 VALUES (2);
        ");

        var cursor1 = conn.Execute("SELECT id FROM t1");
        var row1 = cursor1.Fetchone() as object?[];
        row1![0].Should().Be(1L);

        var cursor2 = conn.Execute("SELECT id FROM t2");
        var row2 = cursor2.Fetchone() as object?[];
        row2![0].Should().Be(2L);
    }

    [Fact]
    public void Executescript_CommitsPendingTransaction()
    {
        var conn = CreateConnection();
        conn.Execute("CREATE TABLE t (val INTEGER)");
        conn.Execute("INSERT INTO t VALUES (10)");
        // Executescript should commit the pending transaction first
        conn.Executescript("CREATE TABLE t2 (val INTEGER)");

        // The insert from before executescript should be committed
        var cursor = conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().Be(10L);
    }

    #endregion

    #region RowFactory Property

    [Fact]
    public void RowFactory_DefaultIsNull()
    {
        var conn = CreateConnection();
        conn.RowFactory.Should().BeNull();
    }

    [Fact]
    public void RowFactory_CanBeSet()
    {
        var conn = CreateConnection();
        conn.RowFactory = Sqlite3.Row;
        conn.RowFactory.Should().NotBeNull();
    }

    #endregion
}
