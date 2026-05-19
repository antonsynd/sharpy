using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Sqlite3CursorTests : IDisposable
{
    private readonly Sqlite3Connection _conn;

    public Sqlite3CursorTests()
    {
        _conn = Sqlite3.Connect(":memory:");
    }

    public void Dispose()
    {
        _conn.Close();
    }

    private void CreateAndPopulateTable()
    {
        _conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT, score REAL)");
        _conn.Execute("INSERT INTO t VALUES (1, 'Alice', 9.5)");
        _conn.Execute("INSERT INTO t VALUES (2, 'Bob', 8.0)");
        _conn.Execute("INSERT INTO t VALUES (3, 'Charlie', 7.5)");
        _conn.Commit();
    }

    #region Fetchone

    [Fact]
    public void Fetchone_ReturnsSingleRow()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id, name FROM t WHERE id = 1");
        var row = cursor.Fetchone() as object?[];
        row.Should().NotBeNull();
        row![0].Should().Be(1L);
        row[1].Should().Be("Alice");
    }

    [Fact]
    public void Fetchone_ReturnsNullWhenNoMoreRows()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t WHERE id = 1");
        cursor.Fetchone(); // Consume the one row
        var result = cursor.Fetchone();
        result.Should().BeNull();
    }

    [Fact]
    public void Fetchone_NoResults_ReturnsNull()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t WHERE id = 999");
        var result = cursor.Fetchone();
        result.Should().BeNull();
    }

    #endregion

    #region Fetchmany

    [Fact]
    public void Fetchmany_ReturnsRequestedNumberOfRows()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t ORDER BY id");
        var rows = cursor.Fetchmany(2);
        rows.Should().HaveCount(2);
    }

    [Fact]
    public void Fetchmany_DefaultUsesArraysize()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t ORDER BY id");
        cursor.Arraysize = 2;
        var rows = cursor.Fetchmany();
        rows.Should().HaveCount(2);
    }

    [Fact]
    public void Fetchmany_ReturnsFewerWhenNotEnoughRows()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t ORDER BY id");
        var rows = cursor.Fetchmany(10);
        rows.Should().HaveCount(3);
    }

    [Fact]
    public void Fetchmany_NoReader_ReturnsEmptyList()
    {
        _conn.Execute("CREATE TABLE t (id INTEGER)");
        var cursor = _conn.Cursor();
        cursor.Execute("INSERT INTO t VALUES (1)");
        // After DML, reader is null, so Fetchmany returns empty
        var rows = cursor.Fetchmany(5);
        rows.Should().BeEmpty();
    }

    #endregion

    #region Fetchall

    [Fact]
    public void Fetchall_ReturnsAllRows()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t ORDER BY id");
        var rows = cursor.Fetchall();
        rows.Should().HaveCount(3);

        var firstRow = rows[0] as object?[];
        firstRow![0].Should().Be(1L);

        var lastRow = rows[2] as object?[];
        lastRow![0].Should().Be(3L);
    }

    [Fact]
    public void Fetchall_NoResults_ReturnsEmptyList()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t WHERE id = 999");
        var rows = cursor.Fetchall();
        rows.Should().BeEmpty();
    }

    [Fact]
    public void Fetchall_AfterPartialFetch_ReturnsRemaining()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t ORDER BY id");
        cursor.Fetchone(); // Consume first row
        var remaining = cursor.Fetchall();
        remaining.Should().HaveCount(2);
    }

    #endregion

    #region Rowcount

    [Fact]
    public void Rowcount_AfterInsert_ReturnsAffectedCount()
    {
        _conn.Execute("CREATE TABLE t (val INTEGER)");
        var cursor = _conn.Execute("INSERT INTO t VALUES (1)");
        cursor.Rowcount.Should().Be(1);
    }

    [Fact]
    public void Rowcount_AfterUpdate_ReturnsAffectedCount()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("UPDATE t SET score = 10.0 WHERE score < 9.0");
        cursor.Rowcount.Should().Be(2); // Bob and Charlie
    }

    [Fact]
    public void Rowcount_AfterDelete_ReturnsAffectedCount()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("DELETE FROM t WHERE id = 1");
        cursor.Rowcount.Should().Be(1);
    }

    [Fact]
    public void Rowcount_AfterSelect_IsMinusOne()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t");
        cursor.Rowcount.Should().Be(-1);
    }

    [Fact]
    public void Rowcount_InitialValue_IsMinusOne()
    {
        var cursor = _conn.Cursor();
        cursor.Rowcount.Should().Be(-1);
    }

    #endregion

    #region Parameterized Queries

    [Fact]
    public void Execute_ParameterizedInsert_BindsValues()
    {
        _conn.Execute("CREATE TABLE t (a TEXT, b INTEGER, c REAL)");
        _conn.Execute("INSERT INTO t VALUES (?, ?, ?)", new object?[] { "hello", 42, 3.14 });
        _conn.Commit();

        var cursor = _conn.Execute("SELECT a, b, c FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().Be("hello");
        row[1].Should().Be(42L);
        row[2].Should().Be(3.14);
    }

    [Fact]
    public void Execute_ParameterizedSelect_FiltersCorrectly()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT name FROM t WHERE id = ?", new object?[] { 2 });
        var row = cursor.Fetchone() as object?[];
        row![0].Should().Be("Bob");
    }

    [Fact]
    public void Execute_NullParameter_InsertsNull()
    {
        _conn.Execute("CREATE TABLE t (val TEXT)");
        _conn.Execute("INSERT INTO t VALUES (?)", new object?[] { null });
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().BeNull();
    }

    #endregion

    #region Description

    [Fact]
    public void Description_AfterSelect_ContainsColumnInfo()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id, name, score FROM t");
        cursor.Description.Should().NotBeNull();
        cursor.Description.Should().HaveCount(3);
        cursor.Description![0][0].Should().Be("id");
        cursor.Description[1][0].Should().Be("name");
        cursor.Description[2][0].Should().Be("score");
    }

    [Fact]
    public void Description_AfterInsert_IsNull()
    {
        _conn.Execute("CREATE TABLE t (val INTEGER)");
        var cursor = _conn.Execute("INSERT INTO t VALUES (1)");
        cursor.Description.Should().BeNull();
    }

    [Fact]
    public void Description_SevenElementTuples()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t");
        cursor.Description.Should().NotBeNull();
        // Python sqlite3 description is a 7-tuple per column
        cursor.Description![0].Should().HaveCount(7);
    }

    #endregion

    #region Lastrowid

    [Fact]
    public void Lastrowid_AfterInsert_ReturnsRowId()
    {
        _conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
        var cursor = _conn.Execute("INSERT INTO t (val) VALUES ('test')");
        cursor.Lastrowid.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Lastrowid_AfterMultipleInserts_ReturnsLastId()
    {
        _conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY, val TEXT)");
        _conn.Execute("INSERT INTO t (val) VALUES ('first')");
        var cursor = _conn.Execute("INSERT INTO t (val) VALUES ('second')");
        cursor.Lastrowid.Should().Be(2);
    }

    [Fact]
    public void Lastrowid_InitialValue_IsMinusOne()
    {
        var cursor = _conn.Cursor();
        cursor.Lastrowid.Should().Be(-1);
    }

    #endregion

    #region Executemany

    [Fact]
    public void Executemany_InsertsAllParameterSets()
    {
        _conn.Execute("CREATE TABLE t (name TEXT)");

        var paramSets = new System.Collections.Generic.List<object?[]>
        {
            new object?[] { "Alice" },
            new object?[] { "Bob" },
            new object?[] { "Charlie" }
        };

        var cursor = _conn.Cursor();
        cursor.Executemany("INSERT INTO t VALUES (?)", paramSets);
        cursor.Rowcount.Should().Be(3);
        _conn.Commit();

        var selectCursor = _conn.Execute("SELECT COUNT(*) FROM t");
        var row = selectCursor.Fetchone() as object?[];
        row![0].Should().Be(3L);
    }

    [Fact]
    public void Executemany_EmptySequence_RowcountIsZero()
    {
        _conn.Execute("CREATE TABLE t (val INTEGER)");
        var cursor = _conn.Cursor();
        cursor.Executemany("INSERT INTO t VALUES (?)", new System.Collections.Generic.List<object?[]>());
        cursor.Rowcount.Should().Be(0);
    }

    #endregion

    #region Iterator Protocol

    [Fact]
    public void Foreach_IteratesAllRows()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT id FROM t ORDER BY id");

        var ids = new System.Collections.Generic.List<long>();
        foreach (var row in cursor)
        {
            var values = row as object?[];
            ids.Add((long)values![0]!);
        }

        ids.Should().BeEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Fact]
    public void Linq_WorksOnCursor()
    {
        CreateAndPopulateTable();
        var cursor = _conn.Execute("SELECT name FROM t ORDER BY name");

        var names = cursor.Cast<object?[]>().Select(r => (string)r[0]!).ToList();
        names.Should().BeEquivalentTo(new[] { "Alice", "Bob", "Charlie" });
    }

    #endregion

    #region Type Mapping

    [Fact]
    public void TypeMapping_Integer_ReturnsLong()
    {
        _conn.Execute("CREATE TABLE t (val INTEGER)");
        _conn.Execute("INSERT INTO t VALUES (42)");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().BeOfType<long>();
        row[0].Should().Be(42L);
    }

    [Fact]
    public void TypeMapping_Text_ReturnsString()
    {
        _conn.Execute("CREATE TABLE t (val TEXT)");
        _conn.Execute("INSERT INTO t VALUES ('hello')");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().BeOfType<string>();
        row[0].Should().Be("hello");
    }

    [Fact]
    public void TypeMapping_Real_ReturnsDouble()
    {
        _conn.Execute("CREATE TABLE t (val REAL)");
        _conn.Execute("INSERT INTO t VALUES (3.14)");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().BeOfType<double>();
        ((double)row[0]!).Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void TypeMapping_Null_ReturnsNull()
    {
        _conn.Execute("CREATE TABLE t (val TEXT)");
        _conn.Execute("INSERT INTO t VALUES (NULL)");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().BeNull();
    }

    [Fact]
    public void TypeMapping_Blob_ReturnsBytes()
    {
        _conn.Execute("CREATE TABLE t (val BLOB)");
        var blobData = new Bytes(new byte[] { 0x01, 0x02, 0x03 });
        _conn.Execute("INSERT INTO t VALUES (?)", new object?[] { blobData });
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t");
        var row = cursor.Fetchone() as object?[];
        row![0].Should().BeOfType<Bytes>();
        var result = (Bytes)row[0]!;
        result[0].Should().Be(1);
        result[1].Should().Be(2);
        result[2].Should().Be(3);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Execute_BadSql_ThrowsOperationalError()
    {
        var act = () => _conn.Execute("INVALID SQL STATEMENT");
        act.Should().Throw<Sqlite3OperationalError>();
    }

    [Fact]
    public void Execute_ConstraintViolation_ThrowsIntegrityError()
    {
        _conn.Execute("CREATE TABLE t (id INTEGER PRIMARY KEY)");
        _conn.Execute("INSERT INTO t VALUES (1)");

        var act = () => _conn.Execute("INSERT INTO t VALUES (1)");
        act.Should().Throw<Sqlite3IntegrityError>();
    }

    [Fact]
    public void Execute_UniqueConstraintViolation_ThrowsIntegrityError()
    {
        _conn.Execute("CREATE TABLE t (name TEXT UNIQUE)");
        _conn.Execute("INSERT INTO t VALUES ('Alice')");

        var act = () => _conn.Execute("INSERT INTO t VALUES ('Alice')");
        act.Should().Throw<Sqlite3IntegrityError>();
    }

    [Fact]
    public void Execute_NotNullViolation_ThrowsIntegrityError()
    {
        _conn.Execute("CREATE TABLE t (name TEXT NOT NULL)");

        var act = () => _conn.Execute("INSERT INTO t VALUES (NULL)");
        act.Should().Throw<Sqlite3IntegrityError>();
    }

    #endregion

    #region Close and Use After Close

    [Fact]
    public void Cursor_Close_ThenExecute_ThrowsProgrammingError()
    {
        var cursor = _conn.Cursor();
        cursor.Close();

        var act = () => cursor.Execute("SELECT 1");
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Cursor_Close_ThenFetchone_ThrowsProgrammingError()
    {
        var cursor = _conn.Cursor();
        cursor.Close();

        var act = () => cursor.Fetchone();
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Cursor_Close_ThenFetchall_ThrowsProgrammingError()
    {
        var cursor = _conn.Cursor();
        cursor.Close();

        var act = () => cursor.Fetchall();
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Cursor_Close_ThenFetchmany_ThrowsProgrammingError()
    {
        var cursor = _conn.Cursor();
        cursor.Close();

        var act = () => cursor.Fetchmany(5);
        act.Should().Throw<Sqlite3ProgrammingError>()
            .WithMessage("*closed*");
    }

    [Fact]
    public void Cursor_Dispose_ClosesAndThrowsOnUse()
    {
        var cursor = _conn.Cursor();
        cursor.Dispose();

        var act = () => cursor.Execute("SELECT 1");
        act.Should().Throw<Sqlite3ProgrammingError>();
    }

    #endregion

    #region Arraysize

    [Fact]
    public void Arraysize_DefaultIsOne()
    {
        var cursor = _conn.Cursor();
        cursor.Arraysize.Should().Be(1);
    }

    [Fact]
    public void Arraysize_CanBeSet()
    {
        var cursor = _conn.Cursor();
        cursor.Arraysize = 10;
        cursor.Arraysize.Should().Be(10);
    }

    #endregion

    #region Executescript via Cursor

    [Fact]
    public void Executescript_BadSql_ThrowsOperationalError()
    {
        var cursor = _conn.Cursor();
        var act = () => cursor.Executescript("INVALID; SQL; HERE");
        act.Should().Throw<Sqlite3OperationalError>();
    }

    #endregion

    #region Multiple Cursors

    [Fact]
    public void MultipleCursors_OnSameConnection_WorkIndependently()
    {
        CreateAndPopulateTable();

        var cur1 = _conn.Execute("SELECT id FROM t ORDER BY id");
        var cur2 = _conn.Execute("SELECT name FROM t ORDER BY name");

        var row1 = cur1.Fetchone() as object?[];
        row1![0].Should().Be(1L);

        var row2 = cur2.Fetchone() as object?[];
        row2![0].Should().Be("Alice");

        var remaining1 = cur1.Fetchall();
        remaining1.Should().HaveCount(2);

        var remaining2 = cur2.Fetchall();
        remaining2.Should().HaveCount(2);
    }

    #endregion
}
