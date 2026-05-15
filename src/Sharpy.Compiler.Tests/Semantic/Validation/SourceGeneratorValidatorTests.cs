using System.Collections.Immutable;
using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Unit tests for <see cref="SourceGeneratorValidator"/> covering signature shape,
/// abstract-generator detection, target validation, and cycle detection.
///
/// The tests bypass the runtime SourceGenerator inheritance check by directly
/// flipping <see cref="TypeSymbol.IsSourceGenerator"/> on the relevant symbols
/// after name resolution. This keeps the tests self-contained without requiring
/// <c>Sharpy.Core</c> to be loaded into the test harness's ModuleRegistry.
/// </summary>
public class SourceGeneratorValidatorTests
{
    private static (Module module, SemanticContext context) Parse(string code)
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

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    /// <summary>
    /// Marks the type with the given name as a source generator in the symbol
    /// table — simulating what NameResolver.ResolveInheritance would do when the
    /// class extends the runtime <c>SourceGenerator</c> type.
    /// </summary>
    private static TypeSymbol MarkAsGenerator(SemanticContext context, string typeName)
    {
        var symbol = context.SymbolTable.LookupType(typeName);
        Assert.NotNull(symbol);
        symbol!.IsSourceGenerator = true;
        return symbol;
    }

    [Fact]
    public void ValidGenerator_NoDiagnostics()
    {
        var code = @"
class MyGen:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors,
            string.Join("; ", context.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void Generator_WithoutGenerateMethod_ReportsInvalidSignature()
    {
        var code = @"
class MyGen:
    def helper(self) -> int:
        return 0
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var error = Assert.Single(context.Diagnostics.GetErrors());
        Assert.Equal(DiagnosticCodes.Validation.InvalidGeneratorSignature, error.Code);
        Assert.Contains("'generate'", error.Message);
    }

    [Fact]
    public void Generator_WithMultipleGenerateMethods_ReportsInvalidSignature()
    {
        var code = @"
class MyGen:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()

    def generate(self, ctx: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.InvalidGeneratorSignature
                 && e.Message.Contains("exactly one"));
    }

    [Fact]
    public void Generator_WithWrongParameterCount_ReportsInvalidSignature()
    {
        var code = @"
class MyGen:
    def generate(self) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var error = Assert.Single(context.Diagnostics.GetErrors());
        Assert.Equal(DiagnosticCodes.Validation.InvalidGeneratorSignature, error.Code);
        Assert.Contains("2 parameters", error.Message);
    }

    [Fact]
    public void Generator_WithFirstParameterNotSelf_ReportsInvalidSignature()
    {
        var code = @"
class MyGen:
    def generate(this, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.InvalidGeneratorSignature
                 && e.Message.Contains("'self'"));
    }

    [Fact]
    public void Generator_WithWrongContextParameterType_ReportsInvalidSignature()
    {
        var code = @"
class MyGen:
    def generate(self, ctx: int) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.InvalidGeneratorSignature
                 && e.Message.Contains("GeneratorContext"));
    }

    [Fact]
    public void Generator_WithWrongReturnType_ReportsInvalidSignature()
    {
        var code = @"
class MyGen:
    def generate(self, context: GeneratorContext) -> int:
        return 0
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.InvalidGeneratorSignature
                 && e.Message.Contains("GeneratorOutput"));
    }

    [Fact]
    public void Generator_WithUntypedContextParam_NoSignatureError()
    {
        // Untyped parameters are accepted — the validator only complains when
        // an explicit annotation disagrees with the expected type.
        var code = @"
class MyGen:
    def generate(self, context) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void AbstractGenerator_ReportsAbstractGeneratorError()
    {
        var code = @"
@abstract
class MyGen:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "MyGen");

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.AbstractGenerator
                 && e.Message.Contains("'MyGen'")
                 && e.Message.Contains("abstract"));
    }

    [Fact]
    public void NonGeneratorClass_IsIgnored()
    {
        // A regular class with a 'generate' method that doesn't match the generator
        // shape must not be flagged — the validator only inspects source generators.
        var code = @"
class Regular:
    def generate(self) -> int:
        return 42
";
        var (module, context) = Parse(code);
        // Intentionally NOT marking 'Regular' as a generator.

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void GeneratorBinding_OnFunction_NoTargetError()
    {
        var code = @"
class GenA:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()

@[GenA]
def my_func():
    pass
";
        var (module, context) = Parse(code);
        var genA = MarkAsGenerator(context, "GenA");

        var funcDef = module.Body.OfType<FunctionDef>().Single(f => f.Name == "my_func");
        var trigger = funcDef.Decorators.Single(d => d.Name == "GenA");
        context.SemanticInfo.AddGeneratorBinding(funcDef, genA, trigger);

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors,
            string.Join("; ", context.Diagnostics.GetErrors().Select(e => e.Message)));
    }

    [Fact]
    public void GeneratorBinding_OnAnotherGenerator_ReportsCycle()
    {
        var code = @"
class GenA:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()

@[GenA]
class GenB:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()
";
        var (module, context) = Parse(code);
        var genA = MarkAsGenerator(context, "GenA");
        MarkAsGenerator(context, "GenB");

        var genB = module.Body.OfType<ClassDef>().Single(c => c.Name == "GenB");
        var trigger = genB.Decorators.Single(d => d.Name == "GenA");
        context.SemanticInfo.AddGeneratorBinding(genB, genA, trigger);

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.CodeGen.GeneratorCycleDetected
                 && e.Message.Contains("GenA")
                 && e.Message.Contains("GenB"));
    }

    [Fact]
    public void GeneratorBinding_OnInvalidTarget_ReportsGeneratorOnInvalidTarget()
    {
        // Source generator bindings are only ever added by TypeChecker for
        // ClassDef/FunctionDef/StructDef. The validator nevertheless guards
        // against unexpected target types — exercise that defense by manually
        // attaching a binding to a non-declaration statement.
        var code = @"
class GenA:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()

x: int = 42
";
        var (module, context) = Parse(code);
        var genA = MarkAsGenerator(context, "GenA");

        // Find the variable declaration statement and pretend it has a binding.
        var nonDeclaration = module.Body.OfType<VariableDeclaration>().First();
        var fakeTrigger = new Decorator
        {
            QualifiedParts = ImmutableArray.Create("GenA"),
            IsBracketAttribute = true,
            LineStart = nonDeclaration.LineStart,
            ColumnStart = nonDeclaration.ColumnStart
        };
        context.SemanticInfo.AddGeneratorBinding(nonDeclaration, genA, fakeTrigger);

        var validator = new SourceGeneratorValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.GeneratorOnInvalidTarget
                 && e.Message.Contains("GenA")
                 && e.Message.Contains("class, function"));
    }

    [Fact]
    public void DecoratorValidator_SkipsConstantCheck_ForGeneratorBracketAttributes()
    {
        // The DecoratorValidator (Order 60) usually rejects non-constant
        // arguments to bracket attributes with SPY0425
        // (NonConstantDecoratorArgument). For source generator attributes
        // those arguments are runtime values, so the check must be skipped.
        var code = @"
class GenA:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()

@[GenA(1 + 2)]
class Target:
    pass
";
        var (module, context) = Parse(code);
        MarkAsGenerator(context, "GenA");

        var decoratorValidator = new DecoratorValidator();
        decoratorValidator.Validate(module, context);

        Assert.DoesNotContain(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.NonConstantDecoratorArgument);
    }

    [Fact]
    public void DecoratorValidator_StillEnforcesConstantCheck_ForNonGeneratorBracketAttributes()
    {
        // Sanity check: when the bracket attribute is NOT a source generator,
        // the constant-argument check must still apply.
        var code = @"
@[custom(1 + 2)]
class Target:
    pass
";
        var (module, context) = Parse(code);
        // No symbol marked as a generator.

        var decoratorValidator = new DecoratorValidator();
        decoratorValidator.Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.NonConstantDecoratorArgument);
    }
}
