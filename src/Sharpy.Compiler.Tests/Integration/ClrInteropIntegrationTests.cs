using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

public class ClrInteropIntegrationTests : StdlibAwareIntegrationTestBase
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

    // ───────────────────────────────────────────────────────────────────
    // CLR-inherited method resolution (#669)
    //
    // When a Sharpy class inherits from a CLR base type, methods discovered
    // on the CLR base must be callable through the subclass. CLR-discovered
    // FunctionSymbols do not carry a synthetic 'self' first parameter the
    // way Sharpy-defined ones do; the type checker must not blindly skip
    // the first parameter when building bound-method FunctionTypes.
    // ───────────────────────────────────────────────────────────────────

    [Fact]
    public void ClrInheritedMethod_CalledThroughSharpySubclass_PreservesArgumentCount()
    {
        // encode(obj) is defined on the CLR JSONEncoder base. Calling it
        // through a Sharpy subclass must accept the obj argument rather than
        // dropping it (the original bug: "Function expects 0 arguments but got 1").
        var source = @"
from json import JSONEncoder

class PassthroughEncoder(JSONEncoder):
    pass

def main():
    encoder = PassthroughEncoder()
    print(encoder.encode(42))
    print(encoder.encode(""hi""))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("42\n\"hi\"\n", result.StandardOutput);
    }

    [Fact]
    public void ClrInheritedMethod_OverriddenInSharpySubclass_ShadowsBaseImplementation()
    {
        // default() is virtual on JSONEncoder. When overridden in a Sharpy
        // subclass, encode() must dispatch through the override.
        var source = @"
from json import JSONEncoder

class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

class PointEncoder(JSONEncoder):
    @override
    def default(self, obj: object) -> object:
        if isinstance(obj, Point):
            d: dict[str, int] = {""x"": obj.x, ""y"": obj.y}
            return d
        raise TypeError(""not serializable"")

def main():
    p = Point(1, 2)
    encoder = PointEncoder()
    print(encoder.encode(p))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("{\"x\": 1, \"y\": 2}\n", result.StandardOutput);
    }

    [Fact]
    public void ClrInheritedProperty_AccessedThroughSharpySubclass_ResolvesType()
    {
        // System.Exception.Message is a CLR property inherited by Sharpy's
        // TypeError. A user subclass of TypeError must be able to read .message.
        // (property name mangling 'Message' -> 'message')
        var source = @"
class MyError(TypeError):
    def __init__(self, msg: str):
        super().__init__(msg)

def main():
    err = MyError(""boom"")
    print(err.message)
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("boom\n", result.StandardOutput);
    }

    [Fact]
    public void ClrInheritedMethod_MultiLevelInheritance_ResolvesThroughChain()
    {
        // Sharpy -> Sharpy -> CLR: encode() lives two levels up the chain.
        var source = @"
from json import JSONEncoder

class MidEncoder(JSONEncoder):
    pass

class LeafEncoder(MidEncoder):
    pass

def main():
    encoder = LeafEncoder()
    print(encoder.encode([1, 2, 3]))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("[1, 2, 3]\n", result.StandardOutput);
    }

    [Fact]
    public void ClrInheritedMethod_OverloadResolutionStillWorks_AfterSelfFix()
    {
        // Regression guard: after fixing the unconditional Skip(1), overload
        // resolution on Sharpy-defined types (which DO have a 'self' first
        // parameter) must continue to skip it correctly.
        var source = @"
class Calc:
    def add(self, a: int, b: int) -> int:
        return a + b

    def add(self, a: int, b: int, c: int) -> int:
        return a + b + c

def main():
    c = Calc()
    print(c.add(1, 2))
    print(c.add(1, 2, 3))
";
        var result = CompileAndExecute(source);
        Assert.True(result.Success, $"Compilation failed: {string.Join("; ", result.CompilationErrors)}");
        Assert.Equal("3\n6\n", result.StandardOutput);
    }
}
