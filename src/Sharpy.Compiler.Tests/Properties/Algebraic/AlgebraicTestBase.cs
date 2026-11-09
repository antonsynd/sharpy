using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

public abstract class AlgebraicTestBase : IntegrationTestBase
{
    protected AlgebraicTestBase(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    protected string? RunAndCapture(Module module)
    {
        var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
        var result = CompileAndExecute(source);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        if (!result.Success)
            Output.WriteLine("RunAndCapture: the program did not run (compile path muted, #2438): " + result.FailureSummary());
        return result.Success ? result.StandardOutput.TrimEnd() : null;
    }

    protected string? RunAndCapture(string source)
    {
        var result = CompileAndExecute(source);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        if (!result.Success)
            Output.WriteLine("RunAndCapture: the program did not run (compile path muted, #2438): " + result.FailureSummary());
        return result.Success ? result.StandardOutput.TrimEnd() : null;
    }
}
