using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class InterfaceConflictValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var semanticBinding = new SemanticBinding();
        var nameResolver = new NameResolver(symbolTable, semanticBinding: semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        // Run type checking to resolve parameter types (needed for IEquatable<T> synthesis)
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void ConflictingEqTypes_InHierarchy_ReportsError()
    {
        var code = @"
class Base:
    def __eq__(self, other: str) -> bool:
        return True

    def __hash__(self) -> int:
        return 0

class Derived(Base):
    def __eq__(self, other: int) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new InterfaceConflictValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Semantic.ConflictingSynthesizedInterface);
    }

    [Fact]
    public void SameEqTypes_InHierarchy_NoConflict()
    {
        var code = @"
class Base:
    def __eq__(self, other: str) -> bool:
        return True

    def __hash__(self) -> int:
        return 0

class Derived(Base):
    def __eq__(self, other: str) -> bool:
        return False

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new InterfaceConflictValidator();
        validator.Validate(module, context);

        var conflictErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Semantic.ConflictingSynthesizedInterface)
            .ToList();
        Assert.Empty(conflictErrors);
    }

    [Fact]
    public void NoEqMethod_NoConflict()
    {
        var code = @"
class Base:
    def greet(self) -> str:
        return ""hello""

class Derived(Base):
    def greet(self) -> str:
        return ""hi""
";
        var (module, context) = Parse(code);

        var validator = new InterfaceConflictValidator();
        validator.Validate(module, context);

        var conflictErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Semantic.ConflictingSynthesizedInterface)
            .ToList();
        Assert.Empty(conflictErrors);
    }
}
