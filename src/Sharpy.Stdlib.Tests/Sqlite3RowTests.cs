using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Sqlite3RowTests : IDisposable
{
    private readonly Sqlite3Connection _conn;

    public Sqlite3RowTests()
    {
        _conn = Sqlite3.Connect(":memory:");
    }

    public void Dispose()
    {
        _conn.Close();
    }

    private Sqlite3Row CreateRow(object?[] values, string[] columnNames)
    {
        // Use the Sqlite3.Row factory through a real query to construct rows,
        // or construct directly via reflection for unit-level tests.
        // Since the constructor is internal, we test via the connection/cursor flow.
        _conn.RowFactory = Sqlite3.Row;
        _conn.Execute("CREATE TABLE IF NOT EXISTS test_row (id INTEGER, name TEXT, score REAL)");
        _conn.Execute("DELETE FROM test_row");
        _conn.Execute("INSERT INTO test_row VALUES (1, 'Alice', 9.5)");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT id, name, score FROM test_row");
        return (Sqlite3Row)cursor.Fetchone()!;
    }

    #region Index Access

    [Fact]
    public void IndexAccess_FirstElement()
    {
        var row = CreateRow(null!, null!);
        row[0].Should().Be(1L);
    }

    [Fact]
    public void IndexAccess_SecondElement()
    {
        var row = CreateRow(null!, null!);
        row[1].Should().Be("Alice");
    }

    [Fact]
    public void IndexAccess_ThirdElement()
    {
        var row = CreateRow(null!, null!);
        ((double)row[2]!).Should().BeApproximately(9.5, 0.001);
    }

    #endregion

    #region Negative Index Access

    [Fact]
    public void NegativeIndex_LastElement()
    {
        var row = CreateRow(null!, null!);
        ((double)row[-1]!).Should().BeApproximately(9.5, 0.001);
    }

    [Fact]
    public void NegativeIndex_FirstElement()
    {
        var row = CreateRow(null!, null!);
        row[-3].Should().Be(1L);
    }

    [Fact]
    public void NegativeIndex_SecondFromEnd()
    {
        var row = CreateRow(null!, null!);
        row[-2].Should().Be("Alice");
    }

    #endregion

    #region Index Out of Range

    [Fact]
    public void Index_TooLarge_ThrowsIndexError()
    {
        var row = CreateRow(null!, null!);
        var act = () => { var _ = row[10]; };
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Index_TooNegative_ThrowsIndexError()
    {
        var row = CreateRow(null!, null!);
        var act = () => { var _ = row[-10]; };
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region Column Name Access

    [Fact]
    public void ColumnNameAccess_ValidName()
    {
        var row = CreateRow(null!, null!);
        row["name"].Should().Be("Alice");
    }

    [Fact]
    public void ColumnNameAccess_CaseInsensitive()
    {
        var row = CreateRow(null!, null!);
        row["NAME"].Should().Be("Alice");
        row["Name"].Should().Be("Alice");
        row["nAmE"].Should().Be("Alice");
    }

    [Fact]
    public void ColumnNameAccess_AllColumns()
    {
        var row = CreateRow(null!, null!);
        row["id"].Should().Be(1L);
        row["name"].Should().Be("Alice");
        ((double)row["score"]!).Should().BeApproximately(9.5, 0.001);
    }

    [Fact]
    public void ColumnNameAccess_InvalidName_ThrowsIndexError()
    {
        var row = CreateRow(null!, null!);
        var act = () => { var _ = row["nonexistent"]; };
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region Keys

    [Fact]
    public void Keys_ReturnsColumnNames()
    {
        var row = CreateRow(null!, null!);
        var keys = row.Keys();
        keys.Should().HaveCount(3);
        keys.Should().Contain("id");
        keys.Should().Contain("name");
        keys.Should().Contain("score");
    }

    [Fact]
    public void Keys_ReturnsNewListEachCall()
    {
        var row = CreateRow(null!, null!);
        var keys1 = row.Keys();
        var keys2 = row.Keys();
        keys1.Should().NotBeSameAs(keys2);
        keys1.Should().BeEquivalentTo(keys2);
    }

    #endregion

    #region Count (ISized)

    [Fact]
    public void Count_ReturnsNumberOfColumns()
    {
        var row = CreateRow(null!, null!);
        row.Count.Should().Be(3);
    }

    [Fact]
    public void ISized_Count_ReturnsNumberOfColumns()
    {
        var row = CreateRow(null!, null!);
        ISized sized = row;
        sized.Count.Should().Be(3);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ContainsColumnNamesAndValues()
    {
        var row = CreateRow(null!, null!);
        var str = row.ToString();
        str.Should().StartWith("<sqlite3.Row");
        str.Should().EndWith(">");
        str.Should().Contain("id=1");
        str.Should().Contain("name='Alice'");
        str.Should().Contain("score=");
    }

    [Fact]
    public void ToString_NullValue_ShowsNone()
    {
        _conn.RowFactory = Sqlite3.Row;
        _conn.Execute("CREATE TABLE t_null (id INTEGER, val TEXT)");
        _conn.Execute("INSERT INTO t_null VALUES (1, NULL)");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT id, val FROM t_null");
        var row = (Sqlite3Row)cursor.Fetchone()!;
        var str = row.ToString();
        str.Should().Contain("val=None");
    }

    [Fact]
    public void ToString_StringValue_IsQuoted()
    {
        var row = CreateRow(null!, null!);
        var str = row.ToString();
        str.Should().Contain("name='Alice'");
    }

    #endregion

    #region Row Factory Integration

    [Fact]
    public void RowFactory_ConnectSetRowFactory_ReturnsRowInstances()
    {
        _conn.RowFactory = Sqlite3.Row;
        _conn.Execute("CREATE TABLE t_rf (id INTEGER, name TEXT)");
        _conn.Execute("INSERT INTO t_rf VALUES (1, 'test')");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT id, name FROM t_rf");
        var result = cursor.Fetchone();
        result.Should().BeOfType<Sqlite3Row>();

        var row = (Sqlite3Row)result!;
        row["id"].Should().Be(1L);
        row["name"].Should().Be("test");
    }

    [Fact]
    public void RowFactory_FetchallReturnsRowInstances()
    {
        _conn.RowFactory = Sqlite3.Row;
        _conn.Execute("CREATE TABLE t_rf2 (id INTEGER, name TEXT)");
        _conn.Execute("INSERT INTO t_rf2 VALUES (1, 'Alice')");
        _conn.Execute("INSERT INTO t_rf2 VALUES (2, 'Bob')");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT id, name FROM t_rf2 ORDER BY id");
        var rows = cursor.Fetchall();
        rows.Should().HaveCount(2);

        var row1 = rows[0] as Sqlite3Row;
        row1.Should().NotBeNull();
        row1!["name"].Should().Be("Alice");

        var row2 = rows[1] as Sqlite3Row;
        row2.Should().NotBeNull();
        row2!["name"].Should().Be("Bob");
    }

    [Fact]
    public void RowFactory_IteratorReturnsRowInstances()
    {
        _conn.RowFactory = Sqlite3.Row;
        _conn.Execute("CREATE TABLE t_rf3 (id INTEGER, name TEXT)");
        _conn.Execute("INSERT INTO t_rf3 VALUES (1, 'test')");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT id, name FROM t_rf3");
        foreach (var obj in cursor)
        {
            obj.Should().BeOfType<Sqlite3Row>();
            var row = (Sqlite3Row)obj;
            row["id"].Should().Be(1L);
        }
    }

    [Fact]
    public void RowFactory_WithoutRowFactory_ReturnsTupleArrays()
    {
        _conn.Execute("CREATE TABLE t_no_rf (id INTEGER, name TEXT)");
        _conn.Execute("INSERT INTO t_no_rf VALUES (1, 'test')");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT id, name FROM t_no_rf");
        var result = cursor.Fetchone();
        result.Should().BeOfType<object?[]>();
    }

    #endregion

    #region Single Column Row

    [Fact]
    public void SingleColumnRow_IndexAndNameAccess()
    {
        _conn.RowFactory = Sqlite3.Row;
        _conn.Execute("CREATE TABLE t_single (val INTEGER)");
        _conn.Execute("INSERT INTO t_single VALUES (42)");
        _conn.Commit();

        var cursor = _conn.Execute("SELECT val FROM t_single");
        var row = (Sqlite3Row)cursor.Fetchone()!;

        row[0].Should().Be(42L);
        row[-1].Should().Be(42L);
        row["val"].Should().Be(42L);
        row.Count.Should().Be(1);
        row.Keys().Should().HaveCount(1);
    }

    #endregion
}
