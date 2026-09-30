using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

public class RoslynEmitterFactoryTests
{
    [Fact]
    public void Create_ReturnsNonNullEmitter()
    {
        var factory = new RoslynEmitterFactory();
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };

        var emitter = factory.Create(context);

        Assert.NotNull(emitter);
    }

    [Fact]
    public void Create_ReturnsICodeEmitter()
    {
        var factory = new RoslynEmitterFactory();
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };

        var emitter = factory.Create(context);

        Assert.IsAssignableFrom<ICodeEmitter>(emitter);
    }

    [Fact]
    public void Create_ReturnsFreshInstanceEachCall()
    {
        var factory = new RoslynEmitterFactory();
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context1 = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };
        var context2 = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };

        var emitter1 = factory.Create(context1);
        var emitter2 = factory.Create(context2);

        Assert.NotSame(emitter1, emitter2);
    }

    [Fact]
    public void Create_EmitterGeneratesCompilationUnitForTrivialModule()
    {
        var factory = new RoslynEmitterFactory();
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };

        var emitter = factory.Create(context);
        // The emitter reads the module layout semantic analysis records (#2102); record it the way
        // CodeGenInfoComputer does for a module with no file identity.
        var module = new Module();
        context.SemanticInfo = new SemanticInfo();
        context.SemanticInfo.SetModuleLayout(module, ModuleLayout.ForModule(module, null, null));
        var result = emitter.GenerateCompilationUnit(module);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Members);
    }

    [Fact]
    public void RoslynEmitter_ImplementsICodeEmitter()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };

        var emitter = new RoslynEmitter(context);

        Assert.IsAssignableFrom<ICodeEmitter>(emitter);
    }
}
