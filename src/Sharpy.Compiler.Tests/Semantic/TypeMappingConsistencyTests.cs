using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Microsoft.CodeAnalysis.CSharp;
using DiscoveryTypeMapper = Sharpy.Compiler.Discovery.ClrTypeMapper;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests that verify type mapping consistency across the codebase.
/// These tests ensure that PrimitiveCatalog is the single source of truth
/// for primitive type information.
/// </summary>
public class TypeMappingConsistencyTests
{
    [Fact]
    public void PrimitiveCatalog_CoversAllSemanticTypeSingletons()
    {
        // All SemanticType singletons should be in PrimitiveCatalog
        var singletons = new[]
        {
            ("int", SemanticType.Int),
            ("long", SemanticType.Long),
            ("float", SemanticType.Float),
            ("double", SemanticType.Double),
            ("bool", SemanticType.Bool),
            ("str", SemanticType.Str),
        };

        foreach (var (name, semanticType) in singletons)
        {
            var info = PrimitiveCatalog.GetByName(name);
            info.Should().NotBeNull($"Primitive '{name}' should be in catalog");

            if (semanticType is BuiltinType builtin)
            {
                info!.ClrType.Should().Be(builtin.ClrType,
                    $"CLR type for '{name}' should match");
            }
        }
    }

    [Fact]
    public void CodeGenTypeMapper_UsesAllPrimitiveTypesFromCatalog()
    {
        // Verify that all primitive types from PrimitiveCatalog are usable
        // by verifying that they all have a valid CSharpName
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            info.CSharpName.Should().NotBeNullOrEmpty(
                $"Primitive '{name}' should have a C# name");
        }
    }

    [Fact]
    public void DiscoveryTypeMapper_MapsAllPrimitiveClrTypes()
    {
        // For each primitive in catalog with a CLR type, verify Discovery TypeMapper maps correctly
        var discoveryMapper = new DiscoveryTypeMapper();

        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            // Skip void: not a mappable value type in Sharpy (void is not user-expressible)
            if (info.ClrType == typeof(void))
                continue;

            // Discovery mapper: CLR -> SemanticType
            var semanticType = discoveryMapper.MapClrTypeToSemanticType(info.ClrType);

            // The mapped SemanticType should have a valid display name
            // Note: For aliases (str/string, None/void), the catalog and SemanticType may use different names
            var displayName = semanticType.GetDisplayName();
            displayName.Should().NotBeNullOrEmpty(
                $"Mapper should map {info.ClrType} to a SemanticType with a valid display name");

            // Verify the type is not Unknown
            semanticType.Should().NotBe(SemanticType.Unknown,
                $"Mapper should map {info.ClrType} to a known SemanticType");
        }
    }

    [Theory]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(float), "float32")]     // C# float -> Sharpy float32
    [InlineData(typeof(double), "double")]     // C# double -> Sharpy double (last registered canonical name)
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(string), "string")]     // Note: "str" is alias, "string" is canonical by CLR type
    [InlineData(typeof(sbyte), "sbyte")]
    [InlineData(typeof(byte), "byte")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(ushort), "ushort")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(char), "char")]
    [InlineData(typeof(decimal), "decimal")]
    public void DiscoveryTypeMapper_MapsClrTypeCorrectly(Type clrType, string expectedSharpyName)
    {
        var discoveryMapper = new DiscoveryTypeMapper();

        var semanticType = discoveryMapper.MapClrTypeToSemanticType(clrType);

        // Get the primitive info from catalog
        var info = PrimitiveCatalog.GetByClrType(clrType);
        info.Should().NotBeNull($"CLR type {clrType} should be in PrimitiveCatalog");
        info!.SharpyName.Should().Be(expectedSharpyName);

        // Verify the mapped semantic type is valid
        semanticType.Should().NotBe(SemanticType.Unknown);
    }

    [Fact]
    public void PrimitiveCatalog_HasConsistentCSharpNames()
    {
        // Verify all primitives have C# names that are valid
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            info.CSharpName.Should().NotBeNullOrEmpty($"'{name}' should have CSharpName");

            // C# names should be valid C# keywords or type names
            var validCSharpNames = new[]
            {
                "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong",
                "float", "double", "decimal", "bool", "char", "string", "void", "object"
            };

            validCSharpNames.Should().Contain(info.CSharpName,
                $"'{info.CSharpName}' should be a valid C# type name");
        }
    }

    [Fact]
    public void PrimitiveCatalog_AllNumericTypesHaveValidSizes()
    {
        // Verify all numeric primitives have valid sizes
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            if (info.Kind != PrimitiveCatalog.NumericKind.None)
            {
                info.SizeInBits.Should().BeOneOf(
                    new[] { 8, 16, 32, 64, 128 },
                    $"Numeric type '{name}' should have a valid size");
            }
        }
    }

    [Theory]
    [InlineData("int", "int")]
    [InlineData("str", "string")]
    [InlineData("None", "void")]
    [InlineData("void", "void")]
    [InlineData("bool", "bool")]
    [InlineData("long", "long")]
    public void PrimitiveCatalog_SharpyNameMapsToCSharpName(string sharpyName, string expectedCSharpName)
    {
        var info = PrimitiveCatalog.GetByName(sharpyName);

        info.Should().NotBeNull($"'{sharpyName}' should be in PrimitiveCatalog");
        info!.CSharpName.Should().Be(expectedCSharpName);
    }

    [Fact]
    public void CodeGenTypeMapper_ProducesCorrectTypeSyntaxForAllPrimitives()
    {
        // Create minimal CodeGenContext for testing
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins);
        var mapper = new Sharpy.Compiler.CodeGen.TypeMapper(context);

        // Test each primitive (excluding void, which cannot be used as a value type)
        // Per spec: Sharpy 'float' -> C# 'double', 'float32' -> C# 'float'
        var primitiveTests = new Dictionary<string, string>
        {
            { "int", "int" },
            { "long", "long" },
            { "float", "double" },      // Sharpy float -> C# double
            { "float32", "float" },     // Sharpy float32 -> C# float
            { "float64", "double" },    // Sharpy float64 -> C# double
            { "double", "double" },
            { "bool", "bool" },
            { "str", "string" },
            { "byte", "byte" },
            { "sbyte", "sbyte" },
            { "short", "short" },
            { "ushort", "ushort" },
            { "uint", "uint" },
            { "ulong", "ulong" },
            { "char", "char" },
            { "decimal", "decimal" },
            { "object", "object" }
        };

        foreach (var (sharpyName, expectedCSharpType) in primitiveTests)
        {
            var typeAnnotation = new TypeAnnotation { Name = sharpyName };
            var typeSyntax = mapper.MapType(typeAnnotation);
            var syntaxText = typeSyntax.ToString();

            syntaxText.Should().Be(expectedCSharpType,
                $"TypeMapper should map '{sharpyName}' to '{expectedCSharpType}'");
        }
    }
}
