using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Integration;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

public abstract class AlgebraicTestBase : IntegrationTestBase
{
    protected AlgebraicTestBase(ITestOutputHelper output) : base(output) { }

    protected string? RunAndCapture(Module module)
    {
        var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
        var result = CompileAndExecute(source);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        return result.Success ? result.StandardOutput.TrimEnd() : null;
    }

    protected string? RunAndCapture(string source)
    {
        var result = CompileAndExecute(source);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        return result.Success ? result.StandardOutput.TrimEnd() : null;
    }
}
