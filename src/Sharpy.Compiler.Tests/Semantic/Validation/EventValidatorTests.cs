using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class EventValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code)
    {
        var (module, context, _) = ParseWithResolver(code);
        return (module, context);
    }

    /// <summary>As <see cref="Parse"/>, also handing back NameResolver's diagnostics — where the member
    /// seam's SPY0204 is reported (P21g D3: SPY0421/SPY0422 retired into it).</summary>
    private (Module module, SemanticContext context, DiagnosticBag resolverDiagnostics) ParseWithResolver(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context, nameResolver.Diagnostics);
    }

    [Fact]
    public void ValidAutoEvent_NoDiagnostics()
    {
        var code = @"
delegate MyHandler(sender: object, data: str) -> None

class Publisher:
    event on_change: MyHandler
";
        var (module, context) = Parse(code);
        var validator = new EventValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    // SPY0421 / SPY0422 RETIRED into SPY0204 (P21g D3, #2360): an event sharing its name with a field or a
    // method is the member seam's verdict — ONE SPY0204 at the LATER declaration, reported by NameResolver —
    // and the validator says nothing about it. The resolver's refusal on the same input is the positive
    // control for the validator's silence.

    [Fact]
    public void EventAfterField_IsTheMemberSeamsDuplicateDefinition_AtTheEvent()
    {
        var code = @"
delegate MyHandler(sender: object, data: str) -> None

class Publisher:
    on_change: int = 0
    event on_change: MyHandler
";
        var (module, context, resolverDiagnostics) = ParseWithResolver(code);
        var validator = new EventValidator();
        validator.Validate(module, context);

        var refusal = Assert.Single(resolverDiagnostics.GetErrors());
        Assert.Equal(DiagnosticCodes.Semantic.DuplicateDefinition, refusal.Code);
        Assert.Equal((6, 5), (refusal.Line, refusal.Column));
        Assert.DoesNotContain(context.Diagnostics.GetErrors(), e => e.Code == DiagnosticCodes.Validation.EventFieldNameConflict);
    }

    [Fact]
    public void EventAfterMethod_IsTheMemberSeamsDuplicateDefinition_AtTheEvent()
    {
        var code = @"
delegate MyHandler(sender: object, data: str) -> None

class Publisher:
    def on_change(self) -> None:
        pass

    event on_change: MyHandler
";
        var (module, context, resolverDiagnostics) = ParseWithResolver(code);
        var validator = new EventValidator();
        validator.Validate(module, context);

        var refusal = Assert.Single(resolverDiagnostics.GetErrors());
        Assert.Equal(DiagnosticCodes.Semantic.DuplicateDefinition, refusal.Code);
        Assert.Equal((8, 5), (refusal.Line, refusal.Column));
        Assert.DoesNotContain(context.Diagnostics.GetErrors(), e => e.Code == DiagnosticCodes.Validation.EventMethodNameConflict);
    }

    /// <summary>
    /// Grouping is transparent at the stub seam (#1214): an abstract event body written
    /// <c>(...)</c> is the same stub as <c>...</c>, so SPY0423 must not fire. There is no
    /// end-to-end fixture for this shape — a function-style abstract event emits uncompilable
    /// C# for both spellings alike, which is a separate pre-existing defect — so the validator
    /// is asserted directly.
    /// </summary>
    [Theory]
    [InlineData("(...)")]
    [InlineData("((...))")]
    [InlineData("...")]
    [InlineData("pass")]
    public void AbstractEventWithStubBody_NoError(string body)
    {
        var code = $@"
delegate MyHandler(sender: object, data: str) -> None

@abstract
class Publisher:
    @abstract
    event add on_change(self, handler: MyHandler):
        {body}

    @abstract
    event remove on_change(self, handler: MyHandler):
        {body}
";
        var (module, context) = Parse(code);
        var validator = new EventValidator();
        validator.Validate(module, context);

        Assert.DoesNotContain(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.AbstractEventWithBody);
    }

    [Fact]
    public void AbstractEventWithRealBody_StillEmitsError()
    {
        var code = @"
delegate MyHandler(sender: object, data: str) -> None

@abstract
class Publisher:
    @abstract
    event add on_change(self, handler: MyHandler):
        print(""not a stub"")

    @abstract
    event remove on_change(self, handler: MyHandler):
        print(""not a stub"")
";
        var (module, context) = Parse(code);
        var validator = new EventValidator();
        validator.Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.AbstractEventWithBody);
    }

    [Fact]
    public void UnpairedEventAccessor_EmitsError()
    {
        var code = @"
delegate MyHandler(sender: object, data: str) -> None

class Publisher:
    event add on_change(self, handler: MyHandler):
        pass
";
        var (module, context) = Parse(code);
        var validator = new EventValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("event add") && e.Message.Contains("no matching"));
    }
}
