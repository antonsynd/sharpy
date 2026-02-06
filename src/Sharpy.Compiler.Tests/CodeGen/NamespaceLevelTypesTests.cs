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
/// Tests for type generation nested in the module class.
/// Verifies that type declarations (classes, structs, interfaces, enums) are placed
/// inside the module class as nested types, not as siblings at namespace level.
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
    public void ClassDef_GeneratesNestedInModuleClass()
    {
        var source = @"
class Point:
    x: int
    y: int

def main():
    p: Point = Point()
";
        var csharp = CompileToCSharp(source);

        // Class should exist
        Assert.Contains("public class Point", csharp);

        // Class should be INSIDE the Program class
        // Check structure: namespace { Program { Point { ... } ... } }
        var programIndex = csharp.IndexOf("public static partial class Program");
        var pointIndex = csharp.IndexOf("public class Point");

        Assert.True(programIndex >= 0, "Program class should exist");
        Assert.True(pointIndex >= 0, "Point class should exist");

        // Find closing brace of Program class (counting braces)
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(pointIndex < programEnd, "Point should appear inside Program class");
    }

    [Fact]
    public void StructDef_GeneratesNestedInModuleClass()
    {
        var source = @"
struct Vector:
    dx: float
    dy: float

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Struct should exist
        Assert.Contains("public struct Vector", csharp);

        // Verify it's nested in Program
        var programIndex = csharp.IndexOf("public static partial class Program");
        var vectorIndex = csharp.IndexOf("public struct Vector");
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(vectorIndex < programEnd, "Vector should appear inside Program class");
    }

    [Fact]
    public void InterfaceDef_GeneratesNestedInModuleClass()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None:
        ...

def main():
    pass
";
        var csharp = CompileToCSharp(source);

        // Interface should exist
        Assert.Contains("public interface IDrawable", csharp);

        // Verify it's nested in Program
        var programIndex = csharp.IndexOf("public static partial class Program");
        var interfaceIndex = csharp.IndexOf("public interface IDrawable");
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(interfaceIndex < programEnd, "IDrawable should appear inside Program class");
    }

    [Fact]
    public void EnumDef_GeneratesNestedInModuleClass()
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

        // Enum should exist
        Assert.Contains("public enum Color", csharp);

        // Verify it's nested in Program
        var programIndex = csharp.IndexOf("public static partial class Program");
        var enumIndex = csharp.IndexOf("public enum Color");
        var programEnd = FindClosingBrace(csharp, programIndex);
        Assert.True(enumIndex < programEnd, "Color should appear inside Program class");
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

        // Types should exist
        Assert.Contains("public class Point", csharp);
        Assert.Contains("public struct Vector", csharp);

        // Verify types are nested inside Program
        var programIndex = csharp.IndexOf("public static partial class Program");
        var programEnd = FindClosingBrace(csharp, programIndex);
        var pointIndex = csharp.IndexOf("public class Point");
        var vectorIndex = csharp.IndexOf("public struct Vector");

        Assert.True(pointIndex < programEnd, "Point should be inside Program class");
        Assert.True(vectorIndex < programEnd, "Vector should be inside Program class");
    }

    [Fact]
    public void MultipleTypes_AllNestedInModuleClass()
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

        // All types should be nested inside Program
        var programIndex = csharp.IndexOf("public static partial class Program");
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
            Assert.True(position < programEnd, $"{name} should be inside Program class");
        }
    }

    [Fact]
    public void LibraryModule_TypesNestedInModuleClass()
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

        // Should have Module class for library (fallback when no SourceFilePath is set)
        Assert.Contains("public static partial class Module", csharp);

        // Types should be nested inside the Module class
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        var pointIndex = csharp.IndexOf("public class Point");

        Assert.True(pointIndex < moduleEnd, "Point should be inside Module class");
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

        // Should have Program with Empty nested inside it
        Assert.Contains("public static partial class Program", csharp);
        Assert.Contains("public class Empty", csharp);

        // Verify Empty is nested inside Program
        var programEnd = FindClosingBrace(csharp, csharp.IndexOf("public static partial class Program"));
        var emptyIndex = csharp.IndexOf("public class Empty");
        Assert.True(emptyIndex < programEnd, "Empty should be inside Program class");
    }

    /// <summary>
    /// Finds the position of the closing brace that matches the first opening brace
    /// found at or after the given start position.
    /// </summary>
    private static int FindClosingBrace(string code, int startPos)
    {
        // Find the first opening brace
        int openBracePos = code.IndexOf('{', startPos);
        if (openBracePos < 0)
            return -1;

        int depth = 1;
        int pos = openBracePos + 1;
        while (pos < code.Length && depth > 0)
        {
            if (code[pos] == '{')
                depth++;
            else if (code[pos] == '}')
                depth--;
            pos++;
        }
        return pos - 1; // Position of closing brace
    }
}
