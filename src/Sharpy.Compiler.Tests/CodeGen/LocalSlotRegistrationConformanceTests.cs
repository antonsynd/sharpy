using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Conformance guard for local variable slot registration (#1386, #1276).
///
/// <para>
/// The emitter tracks locals in two maps that are really one: <c>_variableVersions</c> (base name →
/// version) and <c>_slotSpellings</c> (base name → the SOURCE spelling that claimed the slot). The
/// lookup in <c>GetMangledVariableName</c> answers a slot only when the reference's spelling is the
/// one that claimed it, which is what stops a local <c>zed</c> from answering a reference to a class
/// <c>Zed</c> (CS1061 behind SPY0908). That is sound only while <c>_slotSpellings</c> is TOTAL over
/// <c>_variableVersions</c>: a slot with no recorded owner has to be answered somehow, and the
/// fail-open form ("no owner ⇒ answers everyone") is exactly the hole #1386 tracks.
/// </para>
///
/// <para>
/// Totality cannot be maintained by sweeping the seeding sites once — the count drifted from 23 to
/// 33 direct writes while #1386 sat open. So it is maintained structurally: the slot maps are
/// mutated only by the registration helpers in <c>RoslynEmitter.cs</c>, and this scan fails the
/// build on any other write. The helpers are the three shapes a slot mutation can legitimately
/// take: a fresh claim (<c>RegisterLocalSlot</c>), a version bump or scoped rebinding
/// (<c>SetSlotVersion</c>), a release (<c>ReleaseLocalSlot</c>), and a wholesale snapshot restore
/// (<c>RestoreSlotTable</c>).
/// </para>
///
/// <para>
/// <c>Clear()</c> is not policed: clearing one map without the other leaves spellings with no
/// versions, which is self-healing (the version lookup fails first, and the next claim on that base
/// name overwrites the stale owner). The dangerous direction — a version with no owner — is only
/// reachable through the mutations this scan bans.
/// </para>
///
/// <para>
/// <b>What this scan structurally CANNOT see: scoping.</b> It polices WHO writes the slot table, not
/// WHETHER the write is scoped. A helper call that is perfectly legitimate in isolation — a
/// <c>RegisterLocalSlot</c> for a comprehension's loop target — is a defect when it is made against
/// the ENCLOSING function's table with no save/restore, and this scan passes it either way.
/// <c>GenerateDictSpreadComprehension</c> did exactly that: the loop target collided with an
/// enclosing local (CS0136 behind SPY0908) AND reset that local's slot version with no restore, so
/// later reads of the outer local emitted the wrong name — wrong code, silently, with this
/// conformance test green throughout (#1498).
/// </para>
///
/// <para>
/// The limit is recorded rather than papered over, because the guard for it is a different shape:
/// scoping is a property of a PROGRAM, not of a call site, so it is pinned by executing fixtures —
/// <c>comprehensions/spread_unpack_dict_enclosing_collision_1498</c> for the dict-spread form, whose
/// non-colliding twin <c>spread_unpack_dict</c> keeps a byte-identical C# snapshot across the fix.
/// A future scoping scan would have to compare each registration against a save/restore pair that
/// dominates it, which is a dataflow question this regex-level scan does not attempt.
/// </para>
/// </summary>
public class LocalSlotRegistrationConformanceTests
{
    /// <summary>The two halves of the slot table. A write to either outside a helper can desync them.</summary>
    private static readonly string[] SlotFields = { "_variableVersions", "_slotSpellings" };

    /// <summary>
    /// Mutating members banned outside the helpers. <c>Clear</c> is deliberately absent — see the
    /// class remarks.
    /// </summary>
    private static readonly string[] BannedMembers = { "Add", "TryAdd", "Remove" };

    /// <summary>
    /// The only methods that may mutate the slot table. Named rather than allowlisted by line, so
    /// the exemption survives the files moving and cannot silently grow.
    /// </summary>
    private static readonly HashSet<string> SanctionedWriters = new(StringComparer.Ordinal)
    {
        "RegisterLocalSlot",
        "SetSlotVersion",
        "ReleaseLocalSlot",
        "RestoreSlotTable",
    };

    [Fact]
    public void CodeGen_MutatesLocalSlotsOnlyThroughTheRegistrationHelpers()
    {
        var dir = FindCodeGenDirectory();
        Directory.Exists(dir).Should().BeTrue($"the emitter sources should exist at {dir}");

        var violations = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            violations.AddRange(ScanSource(Path.GetFileName(file), File.ReadAllText(file)));
        }

        violations.Should().BeEmpty(
            "a local variable slot must be claimed through RegisterLocalSlot / SetSlotVersion / "
            + "ReleaseLocalSlot / RestoreSlotTable, or it gets a version with no owning spelling and "
            + "the case-variant lookup in GetMangledVariableName has to fail open again (#1386). "
            + "Use RegisterLocalSlot(baseName, sourceSpelling) for a fresh claim, "
            + "SetSlotVersion(baseName, version, sourceSpelling) for a bump or a scoped rebinding "
            + "(CaptureSlot/RestoreSlot wrap that pair), and ReleaseLocalSlot(baseName) to drop "
            + "one.\nViolations:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// The scan is not toothless: a direct write in an ordinary emitter method is flagged with its
    /// file and line, the same write inside a sanctioned helper is not, and the banned mutating
    /// members are recognized on both halves of the table.
    /// </summary>
    [Fact]
    public void Scan_FlagsDirectWrites_AndExemptsOnlyTheSanctionedHelpers()
    {
        const string offender = @"
class RoslynEmitter
{
    void GenerateForEach()
    {
        _variableVersions[loopVar] = 0;
    }
}";
        var flagged = ScanSource("RoslynEmitter.Statements.ControlFlow.cs", offender);
        flagged.Should().ContainSingle();
        flagged[0].Should().Contain("RoslynEmitter.Statements.ControlFlow.cs:6");
        flagged[0].Should().Contain("_variableVersions");
        flagged[0].Should().Contain("GenerateForEach");

        const string sanctioned = @"
class RoslynEmitter
{
    void RegisterLocalSlot(string baseName, string sourceSpelling)
    {
        _variableVersions[baseName] = 0;
        _slotSpellings[baseName] = sourceSpelling;
    }

    void ReleaseLocalSlot(string baseName)
    {
        _variableVersions.Remove(baseName);
        _slotSpellings.Remove(baseName);
    }
}";
        ScanSource("RoslynEmitter.cs", sanctioned).Should().BeEmpty();

        const string sneaky = @"
class RoslynEmitter
{
    void GenerateSomething()
    {
        _variableVersions.TryAdd(k, v);
        _slotSpellings.Remove(k);
        _variableVersions.Clear();
    }
}";
        // Both mutating calls are flagged; Clear() is not policed.
        ScanSource("RoslynEmitter.Other.cs", sneaky).Should().HaveCount(2);

        // Reads are never violations.
        const string reads = @"
class RoslynEmitter
{
    bool Look() => _variableVersions.ContainsKey(k) && _slotSpellings.TryGetValue(k, out var o)
        && _variableVersions[k] == 0;
}";
        ScanSource("RoslynEmitter.Other.cs", reads).Should().BeEmpty();
    }

    /// <summary>
    /// Returns one entry per slot-table mutation that sits outside <see cref="SanctionedWriters"/>.
    /// Roslyn does the scoping so the check is on the syntax, not on a line-oriented heuristic that
    /// a reformat could defeat.
    /// </summary>
    private static List<string> ScanSource(string fileName, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var violations = new List<string>();

        foreach (var node in root.DescendantNodes())
        {
            var (field, description) = Classify(node);
            if (field == null)
                continue;

            var enclosing = node.FirstAncestorOrSelf<MethodDeclarationSyntax>()?.Identifier.Text;
            if (enclosing != null && SanctionedWriters.Contains(enclosing))
                continue;

            var line = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
            violations.Add(
                $"{fileName}:{line} [{field}] {description} in {enclosing ?? "<no enclosing method>"}");
        }

        return violations;
    }

    /// <summary>
    /// Classifies a node as a slot-table mutation, returning the field it touches and a
    /// human-readable description, or <c>(null, null)</c> when it is not one.
    /// </summary>
    private static (string? Field, string? Description) Classify(SyntaxNode node)
    {
        // _variableVersions[key] = value / += / etc.
        if (node is AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax element }
            && NameOfSlotField(element.Expression) is { } assignedField)
        {
            return (assignedField, "direct indexer write");
        }

        // _slotSpellings.Remove(key), _variableVersions.TryAdd(k, v), ...
        if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
            && BannedMembers.Contains(member.Name.Identifier.Text, StringComparer.Ordinal)
            && NameOfSlotField(member.Expression) is { } invokedField)
        {
            return (invokedField, $"call to {member.Name.Identifier.Text}(...)");
        }

        return (null, null);
    }

    /// <summary>Returns the slot field this expression names, or null if it names something else.</summary>
    private static string? NameOfSlotField(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax identifier
            && SlotFields.Contains(identifier.Identifier.Text, StringComparer.Ordinal)
            ? identifier.Identifier.Text
            : null;

    private static string FindCodeGenDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler", "CodeGen");
            if (Directory.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Sharpy.Compiler", "CodeGen"));
    }
}
