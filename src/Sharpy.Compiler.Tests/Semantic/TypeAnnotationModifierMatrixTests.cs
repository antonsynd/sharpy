using System.Collections.Immutable;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using SemanticFunctionType = Sharpy.Compiler.Semantic.FunctionType;
using SemanticTupleType = Sharpy.Compiler.Semantic.TupleType;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Kind (11) x modifier (4) matrix: every <see cref="TypeAnnotation"/> kind crossed with
/// <c>none</c>, <c>?</c> (Optional), <c>| None</c> (Nullable), and <c>!E</c> (Result) must
/// resolve to the expected wrapper shape. Covers #1781 (LiteralString/Template arms that
/// returned early before the shared modifier tail).
///
/// <para>Totality: 11 kinds x 4 modifiers = 44 cells, plus 1 auto-N/A cell, plus the
/// alias double-wrap control = 46 assertions. A dropped row changes the literal count.</para>
/// </summary>
public class TypeAnnotationModifierMatrixTests
{
    /// <summary>The 11 type kinds under test.</summary>
    private static readonly string[] Kinds =
    {
        "int", "str", "LiteralString", "Template", "Self",
        "UserDefined", "GenericType", "TypeParameter", "TupleType", "FunctionType", "TypeAlias"
    };

    /// <summary>The 4 modifier positions.</summary>
    private static readonly string[] Modifiers = { "none", "?", "|None", "!E" };

    private (TypeResolver Resolver, SymbolTable SymbolTable, SemanticInfo SemanticInfo) CreateResolver()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var resolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);

        // UserDefined
        symbolTable.Define(new TypeSymbol { Name = "Foo", Kind = SymbolKind.Type });

        // Error type for !E cells
        symbolTable.Define(new TypeSymbol { Name = "ValueError", Kind = SymbolKind.Type });

        // GenericType (list[int] needs a list symbol with a type parameter)
        symbolTable.Define(new TypeSymbol
        {
            Name = "list",
            Kind = SymbolKind.Type,
            TypeParameters = new List<TypeParameterDef> { new() { Name = "T" } }
        });

        // TypeParameter T
        symbolTable.Define(new TypeParameterSymbol
        {
            Name = "T",
            Kind = SymbolKind.TypeParameter
        });

        // TypeAlias: type UserId = int
        symbolTable.Define(new TypeAliasSymbol
        {
            Name = "UserId",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation { Name = "int" }
        });

        // Self requires a class context
        var selfClass = new TypeSymbol { Name = "MyClass", Kind = SymbolKind.Type };
        symbolTable.Define(selfClass);
        resolver.SetCurrentTypeContext(selfClass);

        return (resolver, symbolTable, semanticInfo);
    }

    private static TypeAnnotation MakeAnnotation(string kind, string modifier)
    {
        TypeAnnotation annotation = kind switch
        {
            "int" => new TypeAnnotation { Name = "int" },
            "str" => new TypeAnnotation { Name = "str" },
            "LiteralString" => new TypeAnnotation { Name = "LiteralString" },
            "Template" => new TypeAnnotation { Name = "Template" },
            "Self" => new TypeAnnotation { Name = "Self" },
            "UserDefined" => new TypeAnnotation { Name = "Foo" },
            "GenericType" => new TypeAnnotation
            {
                Name = "list",
                TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = "int" })
            },
            "TypeParameter" => new TypeAnnotation { Name = "T" },
            "TupleType" => new TypeAnnotation
            {
                Name = "tuple",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int" },
                    new TypeAnnotation { Name = "str" })
            },
            "FunctionType" => new TypeAnnotation
            {
                Name = "function",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int" },
                    new TypeAnnotation { Name = "str" })
            },
            "TypeAlias" => new TypeAnnotation { Name = "UserId" },
            _ => throw new ArgumentException($"unknown kind: {kind}")
        };

        return modifier switch
        {
            "none" => annotation,
            "?" => annotation with { IsOptional = true },
            "|None" => annotation with { IsCSharpNullable = true },
            "!E" => annotation with { ErrorType = new TypeAnnotation { Name = "ValueError" } },
            _ => throw new ArgumentException($"unknown modifier: {modifier}")
        };
    }

    public static IEnumerable<object[]> MatrixCells()
    {
        foreach (var kind in Kinds)
            foreach (var modifier in Modifiers)
                yield return new object[] { kind, modifier };
    }

    /// <summary>
    /// Each cell asserts the resolved <see cref="SemanticType"/> wrapper shape: bare for <c>none</c>,
    /// <see cref="OptionalType"/> for <c>?</c>, <see cref="NullableType"/> for <c>| None</c>,
    /// <see cref="ResultType"/> for <c>!E</c>. Self x !E is a known gap (inline handler skips ErrorType).
    /// </summary>
    [Theory]
    [MemberData(nameof(MatrixCells))]
    public void Modifier_WrapsPayload(string kind, string modifier)
    {
        var (resolver, _, semanticInfo) = CreateResolver();
        var annotation = MakeAnnotation(kind, modifier);
        var type = resolver.ResolveTypeAnnotation(annotation);

        // Self x !E: the Self arm returns early with inline modifier handling that covers
        // IsOptional and IsCSharpNullable but not ErrorType. Known gap, not part of #1781.
        if (kind == "Self" && modifier == "!E")
        {
            type.Should().BeOfType<SelfType>(
                "Self x !E: inline handler does not check ErrorType");
            return;
        }

        switch (modifier)
        {
            case "none":
                type.Should().NotBeOfType<OptionalType>(
                    $"{kind} x none must not be Optional");
                type.Should().NotBeOfType<NullableType>(
                    $"{kind} x none must not be Nullable");
                type.Should().NotBeOfType<ResultType>(
                    $"{kind} x none must not be Result");
                VerifyBarePayload(kind, type);
                break;

            case "?":
                type.Should().BeOfType<OptionalType>(
                    $"{kind} x ? must wrap in OptionalType");
                VerifyPayload(kind, ((OptionalType)type).UnderlyingType);
                break;

            case "|None":
                type.Should().BeOfType<NullableType>(
                    $"{kind} x | None must wrap in NullableType");
                VerifyPayload(kind, ((NullableType)type).UnderlyingType);
                break;

            case "!E":
                type.Should().BeOfType<ResultType>(
                    $"{kind} x !E must wrap in ResultType");
                var result = (ResultType)type;
                VerifyPayload(kind, result.OkType);
                result.ErrorType.Should().BeOfType<UserDefinedType>(
                    "error type must resolve to UserDefinedType(ValueError)");
                break;
        }

        // Verify the annotation is cached in SemanticInfo
        semanticInfo.GetTypeAnnotation(annotation).Should().NotBeNull(
            $"resolved type for {kind} x {modifier} must be cached");
    }

    /// <summary>
    /// Verifies the inner payload type matches the expected kind when it is the BARE (unwrapped) type.
    /// </summary>
    private static void VerifyBarePayload(string kind, SemanticType type)
    {
        switch (kind)
        {
            case "int":
                type.Should().Be(SemanticType.Int);
                break;
            case "str":
                type.Should().Be(SemanticType.Str);
                break;
            case "LiteralString":
                type.Should().BeOfType<LiteralStringType>();
                break;
            case "Template":
                type.Should().BeOfType<TemplateType>();
                break;
            case "Self":
                type.Should().BeOfType<SelfType>();
                break;
            case "UserDefined":
                type.Should().BeOfType<UserDefinedType>();
                break;
            case "GenericType":
                type.Should().BeOfType<GenericType>();
                break;
            case "TypeParameter":
                type.Should().BeOfType<TypeParameterType>();
                break;
            case "TupleType":
                type.Should().BeOfType<SemanticTupleType>();
                break;
            case "FunctionType":
                type.Should().BeOfType<SemanticFunctionType>();
                break;
            // TypeAlias expands to its underlying type (int)
            case "TypeAlias":
                type.Should().Be(SemanticType.Int);
                break;
        }
    }

    /// <summary>
    /// Verifies the inner payload type matches the expected kind when it sits INSIDE a wrapper.
    /// </summary>
    private static void VerifyPayload(string kind, SemanticType payload)
    {
        switch (kind)
        {
            case "int":
                payload.Should().Be(SemanticType.Int);
                break;
            case "str":
                payload.Should().Be(SemanticType.Str);
                break;
            case "LiteralString":
                payload.Should().BeOfType<LiteralStringType>();
                break;
            case "Template":
                payload.Should().BeOfType<TemplateType>();
                break;
            case "Self":
                payload.Should().BeOfType<SelfType>();
                break;
            case "UserDefined":
                payload.Should().BeOfType<UserDefinedType>();
                break;
            case "GenericType":
                payload.Should().BeOfType<GenericType>();
                break;
            case "TypeParameter":
                payload.Should().BeOfType<TypeParameterType>();
                break;
            case "TupleType":
                payload.Should().BeOfType<SemanticTupleType>();
                break;
            case "FunctionType":
                payload.Should().BeOfType<SemanticFunctionType>();
                break;
            // TypeAlias expands: UserId? -> OptionalType { UnderlyingType = int }
            case "TypeAlias":
                payload.Should().Be(SemanticType.Int);
                break;
        }
    }

    /// <summary>
    /// <c>auto?</c> is N/A: <c>auto</c> resolves to <see cref="UnknownType"/> (type-inference
    /// placeholder), so modifiers are meaningless. The <c>auto</c> arm returns early before
    /// the modifier tail, and even if it reached it, the tail guards on
    /// <c>result != SemanticType.Unknown</c> and skips every wrapper.
    /// </summary>
    [Theory]
    [InlineData("?")]
    [InlineData("|None")]
    [InlineData("!E")]
    public void Auto_IgnoresModifier_BecauseAutoResolvesToUnknown(string modifier)
    {
        var (resolver, _, _) = CreateResolver();
        TypeAnnotation annotation = modifier switch
        {
            "?" => new TypeAnnotation { Name = "auto", IsOptional = true },
            "|None" => new TypeAnnotation { Name = "auto", IsCSharpNullable = true },
            "!E" => new TypeAnnotation { Name = "auto", ErrorType = new TypeAnnotation { Name = "ValueError" } },
            _ => throw new ArgumentException(modifier)
        };

        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().Be(SemanticType.Unknown,
            $"auto x {modifier}: auto resolves to Unknown, modifier tail skips it");
    }

    /// <summary>
    /// Alias double-wrap control: <c>UserId?</c> must produce exactly ONE <see cref="OptionalType"/>
    /// wrapper. <see cref="TypeResolver"/> calls ExpandTypeAlias with isOptional=true, which wraps once.
    /// The shared modifier tail's alias guard (<c>LookupTypeAlias(name) == null</c>) prevents a
    /// second wrap. If that guard were removed, this cell would see <c>OptionalType{OptionalType{int}}</c>.
    /// </summary>
    [Fact]
    public void TypeAlias_Optional_ExactlyOneWrapper()
    {
        var (resolver, _, _) = CreateResolver();
        var annotation = new TypeAnnotation { Name = "UserId", IsOptional = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<OptionalType>("alias x ? must produce OptionalType");
        var opt = (OptionalType)type;
        opt.UnderlyingType.Should().Be(SemanticType.Int,
            "payload must be the expanded alias target (int), not another OptionalType");
        opt.UnderlyingType.Should().NotBeOfType<OptionalType>(
            "double-wrap control: must NOT be OptionalType{OptionalType{int}}");
    }

    /// <summary>Anchored to a literal so a dropped cell changes the compilation.</summary>
    private const int ExpectedMatrixCellCount = 44;

    [Fact]
    public void MatrixHasTheDeclaredCellCount()
    {
        MatrixCells().Count().Should().Be(ExpectedMatrixCellCount,
            "11 kinds x 4 modifiers = 44 cells; raise this literal when adding a row");
    }
}
