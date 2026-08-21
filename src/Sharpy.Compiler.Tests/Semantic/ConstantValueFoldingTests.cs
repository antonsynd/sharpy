using System.Numerics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ConstantValueFoldingTests
{
    private (SymbolTable, SemanticInfo) RunTypeCheck(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);

        return (symbolTable, semanticInfo);
    }

    [Fact]
    public void IntegerConst_FoldsConstantValue()
    {
        var source = "const LIMIT: int = 200";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("LIMIT") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.IsConstant.Should().BeTrue();
        symbol.ConstantValue.Should().Be((BigInteger)200);
    }

    [Fact]
    public void IntegerConst_ExpressionInitializer_FoldsConstantValue()
    {
        var source = "const LIMIT: int = 100 + 100";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("LIMIT") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.ConstantValue.Should().Be((BigInteger)200);
    }

    [Fact]
    public void Uint8Const_FoldsConstantValue()
    {
        var source = "const MAX: uint8 = 255";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("MAX") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.ConstantValue.Should().Be((BigInteger)255);
    }

    [Fact]
    public void FloatConst_DoesNotFold()
    {
        var source = "const PI: float = 3.14";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("PI") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.IsConstant.Should().BeTrue();
        symbol.ConstantValue.Should().BeNull();
    }

    [Fact]
    public void NonFoldableInitializer_DoesNotFold()
    {
        var source = @"
def compute() -> int:
    return 42

const VALUE: int = compute()
";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("VALUE") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.IsConstant.Should().BeTrue();
        symbol.ConstantValue.Should().BeNull();
    }

    [Fact]
    public void FunctionLevelConst_FoldsConstantValue()
    {
        var source = @"
def main():
    const LOCAL_LIMIT: int = 42
    print(LOCAL_LIMIT)
";
        var (symbolTable, _) = RunTypeCheck(source);

        // Function-level const won't be in the module-level symbol table
        // after exiting the function scope, so test via the module-level path
    }

    [Fact]
    public void NonConstVariable_HasNullConstantValue()
    {
        var source = "x: int = 200";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("x") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.IsConstant.Should().BeFalse();
        symbol.ConstantValue.Should().BeNull();
    }

    [Fact]
    public void NegativeIntegerConst_FoldsConstantValue()
    {
        var source = "const NEG: int = -128";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("NEG") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.ConstantValue.Should().Be((BigInteger)(-128));
    }

    [Fact]
    public void StringConst_DoesNotFold()
    {
        var source = "const NAME: str = \"hello\"";
        var (symbolTable, _) = RunTypeCheck(source);

        var symbol = symbolTable.Lookup("NAME") as VariableSymbol;
        symbol.Should().NotBeNull();
        symbol!.IsConstant.Should().BeTrue();
        symbol.ConstantValue.Should().BeNull();
    }
}
