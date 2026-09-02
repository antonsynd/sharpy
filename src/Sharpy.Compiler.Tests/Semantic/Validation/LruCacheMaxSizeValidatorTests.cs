using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Executing cells for SPY0442 (<c>DecoratorValidator.ValidateLruCacheMaxSizeValue</c>). The
/// validator is the LOUD half of a by-design disagreement: it refuses malformed <c>maxsize</c>
/// shapes where <c>TypeChecker.ExtractCacheConfig</c> defaults to 128. Both now read the one
/// literal classifier (<c>AstHelper.TryGetLiteralValue</c>); the verify round of plan-950124
/// measured these shapes at e523ceec3 and 277f54543 and this class pins that the classifier
/// routing changed none of them (there was no SPY0442 test before it).
/// mutation (verify round 2026-09-02): the non-negative branch's <c>text.StartsWith('-')</c>
/// inverted → <c>NegativeMaxSize_IsRefused</c> red and <c>ValidMaxSize_IsAccepted(0)</c> red;
/// restored → green.
/// </summary>
public class LruCacheMaxSizeValidatorTests
{
    private static (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors,
            string.Join("; ", parser.Diagnostics.GetErrors().Select(e => e.Message)));

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    private static string[] Validate(string decoratorArgs)
    {
        var (module, context) = Parse(
            $"@lru_cache({decoratorArgs})\ndef f(x: int) -> int:\n    return x\n");
        var validator = new DecoratorValidator();
        validator.Validate(module, context);
        return context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Validation.LruCacheInvalidMaxSize)
            .Select(e => e.Message)
            .ToArray();
    }

    [Theory]
    [InlineData("maxsize=0")]
    [InlineData("maxsize=128")]
    [InlineData("maxsize=None")]
    [InlineData("8")]
    [InlineData("")]
    public void ValidMaxSize_IsAccepted(string args)
        => Assert.Empty(Validate(args));

    [Fact]
    public void NegativeMaxSize_IsRefused()
    {
        var errors = Validate("maxsize=-1");
        var message = Assert.Single(errors);
        Assert.Contains("non-negative", message);
    }

    [Theory]
    [InlineData("maxsize=\"x\"")]
    [InlineData("maxsize=2**4")]
    [InlineData("maxsize=+8")]
    [InlineData("maxsize=1.5")]
    public void NonIntegerMaxSize_IsRefused_AsIntegerOrNone(string args)
    {
        var errors = Validate(args);
        var message = Assert.Single(errors);
        Assert.Contains("integer literal or None", message);
    }
}
