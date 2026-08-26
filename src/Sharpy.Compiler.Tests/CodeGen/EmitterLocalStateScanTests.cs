using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Scans every source file under <c>src/Sharpy.Compiler/CodeGen/</c> for tokens of the emitter's
/// deleted local-state tracking machinery (#1560, #1647). Since the LocalNameAllocator now
/// pre-computes all local C# names at CodeGenInfo materialization time, no CodeGen source may
/// reference the old tracking fields, methods, or types. A hit means a leftover was not cleaned
/// up, or new code re-introduced something the allocator makes unnecessary.
///
/// <para>
/// Modelled on <see cref="EmitterBannedTokenScanTests"/>: comment-stripped, so a doc comment
/// mentioning the old name as historical context is not a violation.
/// </para>
/// </summary>
public class EmitterLocalStateScanTests
{
    /// <summary>Tokens of the deleted local-state tracking machinery.</summary>
    private static readonly string[] DeletedTokens =
    {
        "_variableVersions",
        "_slotSpellings",
        "_declaredVariables",
        "_constVariables",
        "_sourceVariableNames",
        "_localVariableTypes",
        "_localFunctionNames",
        "SaveScope",
        "RestoreScope",
        "ScopeSnapshot",
        "RegisterLocalSlot",
        "CaptureSlot",
        "CarryForwardOuterSlot",
        "CollectSourceVariableNames",
        "HasComparableConstraint",
    };

    [Fact]
    public void CodeGenSources_ContainNoDeletedLocalStateTokens()
    {
        var codeGenDir = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        Directory.Exists(codeGenDir).Should().BeTrue(
            $"CodeGen source directory should exist at {codeGenDir}");

        var files = Directory.GetFiles(codeGenDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("Should find CodeGen source files");

        var violations = new List<string>();

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = StripLineComment(lines[i]);
                foreach (var token in DeletedTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{fileName}:{i + 1} — references deleted '{token}': {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "CodeGen source may not reference the deleted local-state tracking machinery. " +
            "All local names are now pre-computed by LocalNameAllocator at CodeGenInfo " +
            "materialization time (#1560, #1647). If you need a local's C# name, read " +
            "Symbol.CodeGenInfo.GetVersionedCSharpName(). If you need to know whether a " +
            "binding site declares or rebinds, read SemanticInfo.GetTargetBinding(node).\n" +
            "Violations:\n" + string.Join("\n", violations));
    }

    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }
}
