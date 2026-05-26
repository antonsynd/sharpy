using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
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

        // Class should be INSIDE the module class (no SourceFilePath → fallback name "Module")
        // No namespace wrapper in single-file mode (global namespace)
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var pointIndex = csharp.IndexOf("public class Point");

        Assert.True(moduleIndex >= 0, "Module class should exist");
        Assert.True(pointIndex >= 0, "Point class should exist");

        // Find closing brace of Module class (counting braces)
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        Assert.True(pointIndex < moduleEnd, "Point should appear inside Module class");
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

        // Verify it's nested in Module class (no SourceFilePath → fallback name "Module")
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var vectorIndex = csharp.IndexOf("public struct Vector");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        Assert.True(vectorIndex < moduleEnd, "Vector should appear inside Module class");
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

        // Verify it's nested in Module class (no SourceFilePath → fallback name "Module")
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var interfaceIndex = csharp.IndexOf("public interface IDrawable");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        Assert.True(interfaceIndex < moduleEnd, "IDrawable should appear inside Module class");
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

        // Verify it's nested in Module class (no SourceFilePath → fallback name "Module")
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var enumIndex = csharp.IndexOf("public enum Color");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        Assert.True(enumIndex < moduleEnd, "Color should appear inside Module class");
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

        // Module class should have: Counter, VERSION (constants preserve SCREAMING_SNAKE_CASE), Helper, Main
        Assert.Contains("public static int Counter", csharp);
        Assert.Contains("VERSION", csharp);
        Assert.Contains("public static int Helper()", csharp);
        Assert.Contains("public static void Main()", csharp);

        // Types should exist
        Assert.Contains("public class Point", csharp);
        Assert.Contains("public struct Vector", csharp);

        // Verify types are nested inside Module class (no SourceFilePath → fallback name "Module")
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        var pointIndex = csharp.IndexOf("public class Point");
        var vectorIndex = csharp.IndexOf("public struct Vector");

        Assert.True(pointIndex < moduleEnd, "Point should be inside Module class");
        Assert.True(vectorIndex < moduleEnd, "Vector should be inside Module class");
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

        // All types should be nested inside Module class (no SourceFilePath → fallback name "Module")
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);

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
            Assert.True(position < moduleEnd, $"{name} should be inside Module class");
        }
    }

    [Fact]
    public void LibraryModule_TypesExtractedAsNamespaceSiblings()
    {
        // Non-entry-point single-file module (library mode). Top-level types are extracted out
        // of the module class and emitted as namespace siblings annotated with
        // [SharpyModuleType], while module-level functions stay on the module class. (#702)
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

        // Point is extracted: it appears AFTER the module class closes (a namespace sibling)
        // and carries the [SharpyModuleType] attribute.
        var moduleIndex = csharp.IndexOf("public static partial class Module");
        var moduleEnd = FindClosingBrace(csharp, moduleIndex);
        var pointIndex = csharp.IndexOf("public class Point");

        Assert.True(pointIndex > moduleEnd, "Point should be a sibling of (not nested in) the Module class");
        Assert.Contains("SharpyModuleType", csharp);

        // The module-level function stays on the module class.
        Assert.Contains("public static Point GetOrigin()", csharp);
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

        // Should have Module class with Empty nested inside it (no SourceFilePath → fallback name "Module")
        Assert.Contains("public static partial class Module", csharp);
        Assert.Contains("public class Empty", csharp);

        // Verify Empty is nested inside Module class
        var moduleEnd = FindClosingBrace(csharp, csharp.IndexOf("public static partial class Module"));
        var emptyIndex = csharp.IndexOf("public class Empty");
        Assert.True(emptyIndex < moduleEnd, "Empty should be inside Module class");
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
