using System;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for <see cref="TypeChecker.ResolveClrParameterType"/>, focusing on the
/// AQN-strip fallback path (#967): when <see cref="Type.GetType(string)"/> fails to
/// resolve an assembly-qualified name, the assembly qualifier is stripped and the
/// resulting namespace-qualified name is searched across loaded assemblies.
/// </summary>
public class ResolveClrParameterTypeTests
{
    // These AQNs name a real, always-loaded type but pair it with a bogus assembly
    // identity. Type.GetType binds against the named assembly and so returns null,
    // forcing the fallback to strip the qualifier and search loaded assemblies by
    // namespace-qualified name. (A wrong Version/PublicKeyToken on the *correct*
    // CoreLib assembly is NOT sufficient: the .NET loader still satisfies it from the
    // already-loaded CoreLib, bypassing the fallback we want to cover.)
    private const string UriWithBogusAssembly =
        "System.Uri, Bogus.Phantom.Assembly, Version=99.0.0.0, Culture=neutral, PublicKeyToken=null";

    private const string OpenLinkedListWithBogusAssembly =
        "System.Collections.Generic.LinkedList`1, Bogus.Phantom.Assembly, Version=99.0.0.0, Culture=neutral, PublicKeyToken=null";

    private const string NonexistentAqn =
        "Totally.Bogus.Namespace.NoSuchType, Some.Phantom.Assembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

    private static TypeChecker CreateTypeChecker()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        return new TypeChecker(symbolTable, semanticInfo, typeResolver);
    }

    private static FunctionSymbol FunctionWithParamClrTypeName(string? clrTypeName) =>
        new()
        {
            Name = "f",
            ClrMethod = null,
            Parameters =
            {
                new ParameterSymbol { Name = "x", ClrTypeName = clrTypeName },
            },
        };

    [Fact]
    public void AqnWithBogusAssembly_StripsQualifierAndResolvesFromLoadedAssemblies()
    {
        // Guard: the test is only meaningful if the full AQN genuinely fails to bind.
        Type.GetType(UriWithBogusAssembly).Should().BeNull(
            "the test relies on the full AQN failing to bind so the strip fallback runs");

        var checker = CreateTypeChecker();
        var func = FunctionWithParamClrTypeName(UriWithBogusAssembly);

        var result = checker.ResolveClrParameterType(func, 0, SemanticType.Unknown);

        result.Should().Be(typeof(Uri));
    }

    [Fact]
    public void OpenGenericAqnWithBogusAssembly_StripsQualifierAndClosesWithObject()
    {
        Type.GetType(OpenLinkedListWithBogusAssembly).Should().BeNull(
            "the test relies on the full AQN failing to bind so the strip fallback runs");

        var checker = CreateTypeChecker();
        var func = FunctionWithParamClrTypeName(OpenLinkedListWithBogusAssembly);

        var result = checker.ResolveClrParameterType(func, 0, SemanticType.Unknown);

        // The stripped name resolves to the open definition LinkedList`1, which the
        // method closes over object so Type.IsAssignableFrom comparisons work.
        result.Should().Be(typeof(System.Collections.Generic.LinkedList<object>));
    }

    [Fact]
    public void StrippedNameNotFoundAnywhere_FallsBackToSemanticTypeMapping()
    {
        // Both the full-AQN lookup and the assembly search return null, so resolution
        // falls through to TryGetClrType(semanticType).
        Type.GetType(NonexistentAqn).Should().BeNull();

        var checker = CreateTypeChecker();
        var func = FunctionWithParamClrTypeName(NonexistentAqn);

        var result = checker.ResolveClrParameterType(func, 0, SemanticType.Int);

        result.Should().Be(typeof(int));
    }

    [Fact]
    public void NonAqnName_ResolvedDirectlyWithoutFallback()
    {
        // A bare namespace-qualified name (no comma) is resolved by the first
        // Type.GetType attempt; the strip branch is not needed.
        var checker = CreateTypeChecker();
        var func = FunctionWithParamClrTypeName("System.Int32");

        var result = checker.ResolveClrParameterType(func, 0, SemanticType.Unknown);

        result.Should().Be(typeof(int));
    }

    [Fact]
    public void ClrMethodPresent_UsesReflectionMetadataBeforeClrTypeName()
    {
        // When ClrMethod is set, the parameter's CLR type comes from reflection and the
        // ClrTypeName/strip path is never consulted. A deliberately unresolvable
        // ClrTypeName proves the method short-circuited on ClrMethod.
        var clrMethod = typeof(ResolveClrParameterTypeTests)
            .GetMethod(nameof(SampleTarget),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var func = new FunctionSymbol
        {
            Name = "f",
            ClrMethod = clrMethod,
            Parameters =
            {
                new ParameterSymbol { Name = "value", ClrTypeName = NonexistentAqn },
            },
        };

        var checker = CreateTypeChecker();

        var result = checker.ResolveClrParameterType(func, 0, SemanticType.Unknown);

        result.Should().Be(typeof(string));
    }

    private static void SampleTarget(string value)
    {
        _ = value;
    }
}
