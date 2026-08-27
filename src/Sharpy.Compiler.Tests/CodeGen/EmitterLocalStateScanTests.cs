using System.Collections.Generic;
using System.IO;
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
/// mentioning the old name as historical context is not a violation. The scan is an absence
/// assertion, so <see cref="Scanner_DetectsABannedTokenInSyntheticSource"/> is its positive
/// control: the same matcher, fed one banned token, must report it.
/// </para>
/// </summary>
public class EmitterLocalStateScanTests
{
    /// <summary>Tokens of the deleted local-state tracking machinery.</summary>
    internal static readonly string[] DeletedTokens =
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
        "IsLocalSlotInScope",
        "ResolveLocalName",
        "ComputeNextVersion",
        "SetSlotVersion",
        "ReleaseLocalSlot",
        "RestoreSlotTable",
        "RestoreSlot",
        "SlotAnswersSpelling",
        "TryFindLocalSlot",
        "ProbeLocalSlot",
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
            violations.AddRange(FindViolations(Path.GetFileName(file), File.ReadAllLines(file)));

        violations.Should().BeEmpty(
            "CodeGen source may not reference the deleted local-state tracking machinery. " +
            "All local names are now pre-computed by LocalNameAllocator at CodeGenInfo " +
            "materialization time (#1560, #1647). If you need a local's C# name, read " +
            "Symbol.CodeGenInfo.GetVersionedCSharpName(). If you need to know whether a " +
            "binding site declares or rebinds, read SemanticInfo.GetTargetBinding(node).\n" +
            "Violations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// Positive control for the absence assertion above: the matcher must hit on a source line
    /// that names a banned token, and must not hit when the token is only in a line comment.
    /// </summary>
    [Fact]
    public void Scanner_DetectsABannedTokenInSyntheticSource()
    {
        var lines = new[]
        {
            "private readonly Dictionary<string, int> _variableVersions = new();",
            "// _slotSpellings was deleted (historical note, not a violation)",
            "var next = ComputeNextVersion(name, 0, sourceNames);",
        };

        var violations = FindViolations("Synthetic.cs", lines);

        violations.Should().HaveCount(2);
        violations[0].Should().StartWith("Synthetic.cs:1").And.Contain("_variableVersions");
        violations[1].Should().StartWith("Synthetic.cs:3").And.Contain("ComputeNextVersion");
    }

    /// <summary>Every token must be detectable on its own — no token is dead in the list.</summary>
    [Fact]
    public void Scanner_DetectsEveryListedToken()
    {
        foreach (var token in DeletedTokens)
        {
            // Quoted, because one token can be a prefix of another (RestoreSlot / RestoreSlotTable)
            // and the probe line itself is echoed in every violation message.
            FindViolations("T.cs", new[] { $"var probe = {token};" })
                .Should().ContainSingle(v => v.Contains($"'{token}'"), token);
        }
    }

    internal static List<string> FindViolations(string fileName, string[] lines)
    {
        var violations = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var code = StripLineComment(lines[i]);
            foreach (var token in DeletedTokens)
            {
                if (code.Contains(token, System.StringComparison.Ordinal))
                    violations.Add($"{fileName}:{i + 1} — references deleted '{token}': {lines[i].Trim()}");
            }
        }

        return violations;
    }

    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", System.StringComparison.Ordinal);
        return idx >= 0 ? line.Substring(0, idx) : line;
    }
}
