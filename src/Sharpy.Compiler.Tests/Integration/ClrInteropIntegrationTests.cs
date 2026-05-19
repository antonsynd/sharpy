using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

public class ClrInteropIntegrationTests : IntegrationTestBase
{
    public ClrInteropIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ClrVirtualOverride_JSONEncoder_Default_Compiles()
    {
        var source = @"
from json import JSONEncoder

class CustomEncoder(JSONEncoder):
    @override
    def default(self, obj: object) -> object:
        return ""custom""

def main():
    pass
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
    }

    [Fact]
    public void ClrVirtualOverride_ErrorOnNonVirtualMethod_Preserved()
    {
        var source = @"
from json import JSONEncoder

class BadEncoder(JSONEncoder):
    @override
    def encode(self, obj: object) -> str:
        return ""bad""

def main():
    pass
";
        var result = CompileAndExecute(source);
        Assert.False(result.Success);
    }

    [Fact]
    public void KeywordArgSkipDefaults_JsonDumps_Compiles()
    {
        var source = @"
import json

def main():
    data = {""name"": ""test"", ""value"": ""hello""}
    result = json.dumps(data, sort_keys=True)
    print(result)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Contains("name", result.StandardOutput);
    }

    [Fact]
    public void CollectionAssignability_ListToIEnumerable_Sqlite3Params()
    {
        var source = @"
import sqlite3

def main():
    conn = sqlite3.connect("":memory:"")
    cur = conn.cursor()
    cur.execute(""CREATE TABLE t (a TEXT, b INTEGER)"")
    cur.execute(""INSERT INTO t VALUES (?, ?)"", [""Alice"", 30])
    conn.commit()
    cur.execute(""SELECT COUNT(*) FROM t"")
    rows = cur.fetchall()
    print(len(rows))
    conn.close()
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("1\n", result.StandardOutput);
    }

    [Fact]
    public void CollectionAssignability_NestedListToIEnumerable_Sqlite3Executemany()
    {
        var source = @"
import sqlite3

def main():
    conn = sqlite3.connect("":memory:"")
    cur = conn.cursor()
    cur.execute(""CREATE TABLE t (name TEXT, score INTEGER)"")
    data = [[""Alice"", 95], [""Bob"", 87]]
    cur.executemany(""INSERT INTO t VALUES (?, ?)"", data)
    conn.commit()
    print(cur.rowcount)
    conn.close()
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("2\n", result.StandardOutput);
    }
}
