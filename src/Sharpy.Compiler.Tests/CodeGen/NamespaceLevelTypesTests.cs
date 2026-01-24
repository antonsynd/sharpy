using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for namespace-level type generation.
/// Verifies that type declarations (classes, structs, interfaces, enums) are placed
/// at namespace level as siblings to the module class, not nested inside it.
/// </summary>
public class NamespaceLevelTypesTests
{
    private static string CompileToCSharp(string source, bool isEntryPoint = true)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        // Name resolution
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: isEntryPoint);

        // Code generation
        var context = new CodeGenContext(symbolTable, builtinRegistry)
        {
            IsEntryPoint = isEntryPoint
        };
        var emitter = new RoslynEmitter(context);
        var compilationUnit = emitter.GenerateCompilationUnit(module);

        return compilationUnit.NormalizeWhitespace().ToFullString();
    }

    [Fact]
    public void ClassDef_GeneratesAtNamespaceLevel_NotNestedInModuleClass()
    {
        var source = @"
class Point:
    x: int
    y: int

def main():
    p: Point = Point()
";
        var csharp = CompileToCSharp(source);

        // Class should be at namespace level
        Assert.Contains("public class Point", csharp);

        // Class should NOT be inside the Program class
        // Check structure: namespace { Program { ... } Point { ... } }
        var programIndex = csharp.IndexOf("public static class Program");
        var pointIndex = csharp.IndexOf("public class Point");

        Assert.True(programIndex >= 0, "Program class should exist");
        Assert.True(pointIndex >= 0, "Point class should exist");

        // Find closing brace of Program class (counting braces)
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(pointIndex > programEnd, "Point should appear after Program class closes");
    }

    [Fact]
    public void StructDef_GeneratesAtNamespaceLevel()
    {
        var source = @"
struct Vector:
    dx: float
    dy: float

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Struct should be at namespace level
        Assert.Contains("public struct Vector", csharp);

        // Verify it's not nested in Program
        var programIndex = csharp.IndexOf("public static class Program");
        var vectorIndex = csharp.IndexOf("public struct Vector");
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(vectorIndex > programEnd, "Vector should appear after Program class closes");
    }

    [Fact]
    public void InterfaceDef_GeneratesAtNamespaceLevel()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Interface should be at namespace level
        Assert.Contains("public interface IDrawable", csharp);

        // Verify it's not nested in Program
        var programIndex = csharp.IndexOf("public static class Program");
        var interfaceIndex = csharp.IndexOf("public interface IDrawable");
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(interfaceIndex > programEnd, "IDrawable should appear after Program class closes");
    }

    [Fact]
    public void EnumDef_GeneratesAtNamespaceLevel()
    {
        var source = @"
enum Color:
    RED = 0
    GREEN = 1
    BLUE = 2

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Enum should be at namespace level
        Assert.Contains("public enum Color", csharp);

        // Verify it's not nested in Program
        var programIndex = csharp.IndexOf("public static class Program");
        var enumIndex = csharp.IndexOf("public enum Color");
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(enumIndex > programEnd, "Color should appear after Program class closes");
    }

    [Fact]
    public void MixedDeclarations_CorrectlyPartitioned()
    {
        var source = @"
counter: int = 0
const VERSION: str = ""1.0""

class Point:
    x: int
    y: int

struct Vector:
    dx: float

def helper() -> int:
    return 42

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Module class should have: Counter, VERSION, Helper, Main
        Assert.Contains("public static int Counter", csharp);
        Assert.Contains("VERSION", csharp);
        Assert.Contains("public static int Helper()", csharp);
        Assert.Contains("public static void Main()", csharp);

        // Types should be at namespace level
        Assert.Contains("public class Point", csharp);
        Assert.Contains("public struct Vector", csharp);

        // Verify structure
        var programIndex = csharp.IndexOf("public static class Program");
        var programEnd = FindClosingBrace(csharp, programIndex);
        var pointIndex = csharp.IndexOf("public class Point");
        var vectorIndex = csharp.IndexOf("public struct Vector");

        Assert.True(pointIndex > programEnd, "Point should be after Program class");
        Assert.True(vectorIndex > programEnd, "Vector should be after Program class");
    }

    [Fact]
    public void MultipleTypes_AllAtNamespaceLevel()
    {
        var source = @"
class Point:
    x: int
    y: int

struct Vector:
    dx: float
    dy: float

interface IShape:
    def area(self) -> float:
        ...

enum Color:
    RED = 0
    GREEN = 1

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // All types should be at namespace level
        var programIndex = csharp.IndexOf("public static class Program");
        var programEnd = FindClosingBrace(csharp, programIndex);

        var typePositions = new[]
        {
            ("Point", csharp.IndexOf("public class Point")),
            ("Vector", csharp.IndexOf("public struct Vector")),
            ("IShape", csharp.IndexOf("public interface IShape")),
            ("Color", csharp.IndexOf("public enum Color"))
        };

        foreach (var (name, position) in typePositions)
        {
            Assert.True(position > 0, $"{name} should exist in output");
            Assert.True(position > programEnd, $"{name} should be after Program class");
        }
    }

    [Fact]
    public void LibraryModule_TypesAtNamespaceLevel()
    {
        // Non-entry-point module (library)
        var source = @"
class Point:
    x: int
    y: int

def get_origin() -> Point:
    return Point()
";
        var csharp = CompileToCSharp(source, isEntryPoint: false);

        // Should have Exports class for library
        Assert.Contains("public static class Exports", csharp);

        // Types should still be at namespace level
        var exportsIndex = csharp.IndexOf("public static class Exports");
        var exportsEnd = FindClosingBrace(csharp, exportsIndex);
        var pointIndex = csharp.IndexOf("public class Point");

        Assert.True(pointIndex > exportsEnd, "Point should be after Exports class");
    }

    // Note: Nested classes within user classes are not currently supported by the language.
    // When support is added, a test like this should be added:
    // [Fact]
    // public void NestedClassesWithinUserClass_StayNested() { ... }

    [Fact]
    public void TypeReferences_ResolveCorrectly()
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def create_point() -> Point:
    return Point(0, 0)

def main():
    p: Point = create_point()
    print(p.x)
";
        var csharp = CompileToCSharp(source);

        // Function should reference Point correctly (without qualifying with Exports)
        Assert.Contains("public static Point CreatePoint()", csharp);
        Assert.Contains("return new Point(0, 0)", csharp);
    }

    [Fact]
    public void EmptyModuleWithTypes_GeneratesCorrectStructure()
    {
        // Entry point with only a type and main
        var source = @"
class Empty:
    pass

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Should have both Program and Empty at namespace level
        Assert.Contains("public static class Program", csharp);
        Assert.Contains("public class Empty", csharp);

        // Verify structure
        var programEnd = FindClosingBrace(csharp, csharp.IndexOf("public static class Program"));
        var emptyIndex = csharp.IndexOf("public class Empty");
        Assert.True(emptyIndex > programEnd);
    }

    /// <summary>
    /// Finds the position of the closing brace that matches the first opening brace
    /// found at or after the given start position.
    /// </summary>
    private static int FindClosingBrace(string code, int startPos)
    {
        // Find the first opening brace
        int openBracePos = code.IndexOf('{', startPos);
        if (openBracePos < 0) return -1;

        int depth = 1;
        int pos = openBracePos + 1;
        while (pos < code.Length && depth > 0)
        {
            if (code[pos] == '{') depth++;
            else if (code[pos] == '}') depth--;
            pos++;
        }
        return pos - 1; // Position of closing brace
    }
}
