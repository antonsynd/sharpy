using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #2180: <c>IntegrationTestBase</c>'s three execute arms (<c>CompileAndExecute</c>,
/// <c>CompileAndExecuteProject</c>, <c>CompileAndExecuteEntryFile</c>) deploy and run the emitted
/// program through ONE helper, <c>RunEmittedProgram</c>. Before it existed the copy-and-spawn
/// block was written out twice, so a per-call cost cured in one copy (or a behaviour fixed in
/// one) silently stayed in the other.
///
/// <para><b>Contract.</b> Exactly one <c>new ProcessStartInfo</c> and exactly one
/// <c>CopyRuntimeClosure(...)</c> call in <c>IntegrationTestBase.cs</c>. Counted on the syntax
/// tree, so a mention in a comment or a string does not count; the counter's own discrimination
/// is shown by <see cref="Counter_SeesCreationsAndCalls_ButNotCommentsOrStrings"/>.</para>
/// </summary>
public class IntegrationTestBaseExecutePathTests
{
    private static string ReadIntegrationTestBase()
    {
        var path = Path.Combine(DispatchSiteScan.FindRepoRoot(),
            "src", "Sharpy.TestInfrastructure", "Integration", "IntegrationTestBase.cs");
        // A moved or renamed file throws here rather than scanning nothing.
        return File.ReadAllText(path);
    }

    private static int CountProcessStartInfoCreations(string source)
        => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Count(n => n.Type switch
            {
                IdentifierNameSyntax id => id.Identifier.Text == "ProcessStartInfo",
                QualifiedNameSyntax q => q.Right.Identifier.Text == "ProcessStartInfo",
                _ => false
            });

    private static int CountInvocationsOf(string source, string methodName)
        => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(n => n.Expression is IdentifierNameSyntax id && id.Identifier.Text == methodName);

    [Fact]
    public void IntegrationTestBase_SpawnsProcesses_AtExactlyOneSite()
    {
        Assert.Equal(1, CountProcessStartInfoCreations(ReadIntegrationTestBase()));
    }

    [Fact]
    public void IntegrationTestBase_CopiesTheRuntimeClosure_AtExactlyOneSite()
    {
        Assert.Equal(1, CountInvocationsOf(ReadIntegrationTestBase(), "CopyRuntimeClosure"));
    }

    [Fact]
    public void Counter_SeesCreationsAndCalls_ButNotCommentsOrStrings()
    {
        const string source = """
            using System.Diagnostics;
            class C
            {
                // var x = new ProcessStartInfo(); CopyRuntimeClosure(a, b, c);
                const string S = "new ProcessStartInfo CopyRuntimeClosure(a, b, c)";
                void M()
                {
                    var a = new ProcessStartInfo { FileName = "dotnet" };
                    var b = new System.Diagnostics.ProcessStartInfo();
                    CopyRuntimeClosure(1, 2, 3);
                    CopyRuntimeClosure(4, 5, 6);
                }
                static void CopyRuntimeClosure(int a, int b, int c) { }
            }
            """;

        Assert.Equal(2, CountProcessStartInfoCreations(source));
        Assert.Equal(2, CountInvocationsOf(source, "CopyRuntimeClosure"));
    }
}
