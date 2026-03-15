using System.Text.Json;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.Analysis;

[Trait("Category", "GapDiscovery")]
public class CompletionFuzzTests
{
    private readonly ITestOutputHelper _output;
    private readonly CompilerApi _api = new();

    private static readonly string FixturesPath = IOPath.GetFullPath(IOPath.Combine(
        IOPath.GetDirectoryName(typeof(CompletionFuzzTests).Assembly.Location)!,
        "..", "..", "..", "..", "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    public CompletionFuzzTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllFixtureMemberAccesses_NoCompletionCrashes()
    {
        var crashes = new List<CrashInfo>();
        var nullReceiverType = new List<MemberAccessInfo>();
        var unknownReceiverType = new List<MemberAccessInfo>();
        var missingMember = new List<MemberAccessInfo>();

        int totalFixtures = 0;
        int analyzedFixtures = 0;
        int skippedFixtures = 0;
        int totalMemberAccesses = 0;
        int memberAccessesWithTypeInfo = 0;

        var fixtureFiles = Directory.GetFiles(FixturesPath, "*.spy", SearchOption.AllDirectories);

        foreach (var file in fixtureFiles)
        {
            var relativePath = IOPath.GetRelativePath(FixturesPath, file);
            var expectedFile = IOPath.ChangeExtension(file, ".expected");
            var errorFile = IOPath.ChangeExtension(file, ".error");
            var skipFile = IOPath.ChangeExtension(file, ".skip");

            if (File.Exists(skipFile))
            {
                skippedFixtures++;
                continue;
            }

            if (!File.Exists(expectedFile) && File.Exists(errorFile))
            {
                skippedFixtures++;
                continue;
            }

            totalFixtures++;

            var source = File.ReadAllText(file);
            SemanticResult result;
            try
            {
                result = _api.Analyze(source);
            }
            catch
            {
                skippedFixtures++;
                continue;
            }

            if (!result.Success || result.Ast == null || result.SemanticInfo == null)
            {
                skippedFixtures++;
                continue;
            }

            analyzedFixtures++;

            var positions = IdentifierPositionCollector.CollectPositions(result.Ast)
                .Where(p => p.NodeType == "MemberAccess")
                .ToList();

            foreach (var pos in positions)
            {
                totalMemberAccesses++;

                Node? node;
                try
                {
                    node = _api.FindNodeAtPosition(result.Ast, pos.Line, pos.Column);
                }
                catch (Exception ex)
                {
                    crashes.Add(new CrashInfo(relativePath, pos.Line, pos.Column, pos.Name, "FindNodeAtPosition", ex.Message));
                    continue;
                }

                if (node is not MemberAccess ma)
                {
                    // FindNodeAtPosition may return a different node at this position;
                    // try to find the MemberAccess specifically
                    try
                    {
                        var maNode = _api.FindNodeOfType<MemberAccess>(result.Ast, pos.Line, pos.Column);
                        if (maNode != null)
                            ma = maNode;
                        else
                            continue;
                    }
                    catch (Exception ex)
                    {
                        crashes.Add(new CrashInfo(relativePath, pos.Line, pos.Column, pos.Name, "FindNodeOfType<MemberAccess>", ex.Message));
                        continue;
                    }
                }

                // Probe receiver type and member existence
                try
                {
                    var receiverType = result.SemanticInfo.GetEffectiveType(ma.Object);
                    if (receiverType == null)
                        nullReceiverType.Add(new MemberAccessInfo(relativePath, pos.Line, pos.Column, pos.Name));
                    else if (receiverType is UnknownType)
                        unknownReceiverType.Add(new MemberAccessInfo(relativePath, pos.Line, pos.Column, pos.Name));
                    else
                    {
                        memberAccessesWithTypeInfo++;

                        // Check if the accessed member actually exists on the receiver type
                        var memberName = ma.Member;
                        if (receiverType is UserDefinedType udt && udt.Symbol != null)
                        {
                            var hasMethod = udt.Symbol.Methods.Any(m =>
                                string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase));
                            var hasField = udt.Symbol.Fields.Any(f =>
                                string.Equals(f.Name, memberName, StringComparison.OrdinalIgnoreCase));
                            var hasProperty = udt.Symbol.Properties.Any(p =>
                                string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase));

                            if (!hasMethod && !hasField && !hasProperty)
                                missingMember.Add(new MemberAccessInfo(relativePath, pos.Line, pos.Column, pos.Name));
                        }
                    }
                }
                catch (Exception ex)
                {
                    crashes.Add(new CrashInfo(relativePath, pos.Line, pos.Column, pos.Name, "GetEffectiveType(receiver)", ex.Message));
                }
            }
        }

        var coveragePercent = totalMemberAccesses > 0
            ? System.Math.Round(100.0 * memberAccessesWithTypeInfo / totalMemberAccesses, 1)
            : 0.0;

        var report = new
        {
            totalFixtures,
            analyzedFixtures,
            skippedFixtures,
            totalMemberAccesses,
            crashes = crashes.Select(c => new { c.File, c.Line, c.Column, c.Name, c.Method, c.Error }),
            nullReceiverType = nullReceiverType.Take(50).Select(i => new { i.File, i.Line, i.Column, i.Name }),
            unknownReceiverType = unknownReceiverType.Take(50).Select(i => new { i.File, i.Line, i.Column, i.Name }),
            missingMember = missingMember.Take(50).Select(i => new { i.File, i.Line, i.Column, i.Name }),
            nullReceiverTypeCount = nullReceiverType.Count(),
            unknownReceiverTypeCount = unknownReceiverType.Count(),
            missingMemberCount = missingMember.Count(),
            memberAccessesWithTypeInfo,
            coveragePercent
        };

        var reportPath = IOPath.GetFullPath(IOPath.Combine(
            IOPath.GetDirectoryName(typeof(CompletionFuzzTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "..", ".claude", "tmp", "completion-fuzz-report.json"));
        Directory.CreateDirectory(IOPath.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"Report written to {reportPath}");

        _output.WriteLine($"Fixtures: {analyzedFixtures} analyzed, {skippedFixtures} skipped");
        _output.WriteLine($"Member accesses: {totalMemberAccesses} total, {memberAccessesWithTypeInfo} with type info ({coveragePercent}%)");
        _output.WriteLine($"Gaps: {nullReceiverType.Count} null receiver, {unknownReceiverType.Count} unknown receiver, {missingMember.Count} missing member");
        _output.WriteLine($"Crashes: {crashes.Count}");

        Assert.Empty(crashes);
    }

    private record CrashInfo(string File, int Line, int Column, string Name, string Method, string Error);
    private record MemberAccessInfo(string File, int Line, int Column, string Name);
}
