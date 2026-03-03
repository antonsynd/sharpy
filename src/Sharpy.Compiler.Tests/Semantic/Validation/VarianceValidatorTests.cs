using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Tests for VarianceValidator, which validates type parameter variance annotations
/// including nested variance flip rules for generic type parameters.
/// </summary>
public class VarianceValidatorTests
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

        // Run name resolution (needed for symbol lookup during variance checking)
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    #region SPY0417 — Variance on class/struct

    [Fact]
    public void ClassWithVariance_ProducesError()
    {
        var code = @"
class Box[out T]:
    pass
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0417" && d.Message.Contains("class/struct"));
    }

    [Fact]
    public void StructWithVariance_ProducesError()
    {
        var code = @"
struct Wrap[in T]:
    pass
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0417");
    }

    [Fact]
    public void InterfaceWithVariance_NoError()
    {
        var code = @"
interface IBox[out T]:
    def get(self) -> T: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region SPY0418 — Covariant in contravariant position

    [Fact]
    public void CovariantTypeParam_InReturnType_NoError()
    {
        var code = @"
interface IProducer[out T]:
    def produce(self) -> T: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void CovariantTypeParam_InParameterType_ProducesError()
    {
        var code = @"
interface IConsumer[out T]:
    def consume(self, value: T) -> None: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0418" && d.Message.Contains("contravariant"));
    }

    #endregion

    #region SPY0419 — Contravariant in covariant position

    [Fact]
    public void ContravariantTypeParam_InParameterType_NoError()
    {
        var code = @"
interface IConsumer[in T]:
    def consume(self, value: T) -> None: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ContravariantTypeParam_InReturnType_ProducesError()
    {
        var code = @"
interface IFactory[in T]:
    def create(self) -> T: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0419" && d.Message.Contains("covariant"));
    }

    #endregion

    #region Nested variance flip rules

    [Fact]
    public void NestedFlip_FunctionParam_CovariantInContravariantFlipsToCovariant_NoError()
    {
        // (T) -> None is in parameter position (contravariant).
        // T is in the function's param position (contravariant).
        // Contravariant * contravariant = covariant → OK for out T.
        var code = @"
interface IFactory[out T]:
    def create(self, consumer: (T) -> None) -> None: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NestedFlip_FunctionReturn_CovariantInContravariantPosition_ProducesError()
    {
        // () -> T is in parameter position (contravariant).
        // T is in the function's return position (covariant).
        // Covariant * contravariant = contravariant → ERROR for out T.
        var code = @"
interface IBad[out T]:
    def process(self, factory: () -> T) -> None: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0418");
    }

    [Fact]
    public void NestedFlip_CovariantProducer_InReturnType_NoError()
    {
        // IProducer[out T] in return (covariant).
        // T is in covariant position of IProducer.
        // Covariant * covariant = covariant → OK for out T.
        var code = @"
interface IProducer[out T]:
    def get(self) -> T: ...

interface IContainer[out T]:
    def get_producer(self) -> IProducer[T]: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NestedFlip_ContravariantConsumer_InReturnType_NoError()
    {
        // IConsumer[in T] in return (covariant).
        // T is in contravariant position of IConsumer.
        // Contravariant * covariant = contravariant → OK for in T.
        var code = @"
interface IConsumer[in T]:
    def accept(self, value: T) -> None: ...

interface IHandler[in T]:
    def get_consumer(self) -> IConsumer[T]: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void NestedFlip_ContravariantConsumer_InParamType_ProducesError()
    {
        // IConsumer[in T] in param position (contravariant).
        // T is in contravariant position of IConsumer.
        // Contravariant * contravariant = covariant → ERROR for in T.
        var code = @"
interface IConsumer[in T]:
    def accept(self, value: T) -> None: ...

interface IBad[in T]:
    def process(self, consumer: IConsumer[T]) -> None: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0419");
    }

    [Fact]
    public void InvariantTypeParam_NoVarianceChecking()
    {
        // No variance annotation → no checking at all
        var code = @"
interface IBox[T]:
    def get(self) -> T: ...
    def set(self, value: T) -> None: ...
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Delegate variance

    [Fact]
    public void Delegate_CovariantReturn_NoError()
    {
        var code = @"
delegate Producer[out T]() -> T
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Delegate_ContravariantParam_NoError()
    {
        var code = @"
delegate Consumer[in T](value: T) -> None
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Delegate_CovariantParam_ProducesError()
    {
        var code = @"
delegate BadConsumer[out T](value: T) -> None
";
        var (module, context) = Parse(code);
        var validator = new VarianceValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetAll(),
            d => d.Code == "SPY0418");
    }

    #endregion
}
