using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The standing source scan promised by plan-f164be (P10) Design Decision 3, #1832: the class-member
/// emitters must not re-derive an enumerator's element type from the dunder's own return ANNOTATION.
///
/// <para><b>The defect this guards.</b> <c>IterableElementDecider</c> is the ONE decider for "what is
/// a producer's element" — a generator's annotation IS the element, a non-generator's names the
/// producer (<c>Iterator[T]</c>/<c>IEnumerator[T]</c>/<c>IEnumerable[T]</c>) and is unpeeled to
/// <c>T</c>. Synthesis records the answer on the class as a <c>SynthesizedInterfaces</c> row keyed by
/// triggering dunder. Before <c>da71f0ca3</c>, FOUR emitter sites asked the question a second time by
/// mapping <c>funcDef.ReturnType</c>/<c>nextFunc.ReturnType</c> straight to a <c>TypeSyntax</c>
/// (three in <c>RoslynEmitter.ClassMembers.Iterators.cs</c>, one in
/// <c>RoslynEmitter.ClassMembers.cs</c>) — so a non-generator <c>__iter__</c> annotated
/// <c>-&gt; Iterator[T]</c> emitted <c>Sharpy.Iterator&lt;T&gt;</c> where the synthesized interface
/// required <c>IEnumerator&lt;T&gt;</c>. That is Critical Rule 2 pattern (a): the fact is
/// symbol-keyed on <c>CodeGenInfo</c>, and the emitter reads it through
/// <c>GetSynthesizedElementType</c>. Nothing mechanically stopped a new arm from reaching back for
/// the annotation. This does.
///
/// <para><b>Two guards, deliberately different in width.</b>
/// <see cref="ClassMembersEmitters_ReDeriveNoEnumeratorElementFromTheAnnotation"/> is the scan the
/// plan promised: the two exact pre-fix spellings across <i>every</i>
/// <c>RoslynEmitter.ClassMembers*.cs</c> partial (0 hits; the pre-fix control was 4, measured at
/// <c>da71f0ca3^</c>). <see cref="EnumeratorSeamFiles_MapNoReturnAnnotationAtAll"/> is the wider
/// one: inside the two files that held those four sites, ANY <c>MapType(x.ReturnType)</c> is banned
/// regardless of the local's name, so renaming <c>funcDef</c> to <c>f</c> does not evade the guard.
/// The wide ban is scoped to those two files on purpose — the OTHER <c>ClassMembers</c> partials have
/// eight legitimate <c>MapType(x.ReturnType)</c> sites (ordinary method and property return types,
/// which genuinely ARE the annotation), so a repo-wide version of the wide ban would be an
/// allowlist-bearing guard whose exemptions outnumber its subject.</para>
///
/// <para><b>Non-vacuity.</b> Two positive controls, both anchored to literals:
/// <see cref="OperatorEmitters_HoldTheSixLegitimateSites_PositiveControl"/> runs the SAME detector
/// over <c>RoslynEmitter.Operators.cs</c>, where the pattern legitimately occurs (an operator
/// overload's C# return type IS its annotation) and must find exactly 6; and
/// <see cref="Detector_FlagsEachSpelling_PositiveControl"/> plants each spelling in synthetic source.
/// Without them an all-green run would be evidence about a detector that matches nothing.</para>
///
/// <para><b>Mutation record</b> (executed at authoring time; also in the commit body): planting
/// <c>var t = _typeMapper.MapType(funcDef.ReturnType);</c> in
/// <c>RoslynEmitter.ClassMembers.Iterators.cs</c> (file copied aside first, restored with
/// <c>cp</c>) turns both guards red naming that line; restoring turns them green.</para>
/// </summary>
public class EmitterAnnotationRederivationScanTests
{
    /// <summary>
    /// The exact local names the four pre-fix sites used. This narrow spelling set is what the
    /// plan's promised scan is about; <see cref="EnumeratorSeamFiles_MapNoReturnAnnotationAtAll"/>
    /// drops the name restriction inside the seam files.
    /// </summary>
    private static readonly string[] PreFixReceivers = { "funcDef", "nextFunc" };

    /// <summary>The two partials that held the four pre-fix sites (3 + 1) and emit the enumerator
    /// members today. Named so a move or rename fails loudly instead of emptying the scan.</summary>
    private static readonly string[] EnumeratorSeamFiles =
    {
        "RoslynEmitter.ClassMembers.Iterators.cs",
        "RoslynEmitter.ClassMembers.cs",
    };

    /// <summary>The file where the pattern is LEGITIMATE, and its measured site count — an operator
    /// overload's emitted C# return type genuinely is its declared annotation, nothing synthesizes
    /// an interface row for it. Anchored to a literal so the detector cannot silently stop
    /// matching.</summary>
    private const string ControlFileName = "RoslynEmitter.Operators.cs";
    private const int ControlSiteCount = 6;

    // ---- the promised scan -------------------------------------------------------------------

    [Fact]
    public void ClassMembersEmitters_ReDeriveNoEnumeratorElementFromTheAnnotation()
    {
        var dir = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        var files = Directory.GetFiles(dir, "RoslynEmitter.ClassMembers*.cs", SearchOption.TopDirectoryOnly);

        files.Should().NotBeEmpty("the ClassMembers partials are the subject of this scan");
        foreach (var seamFile in EnumeratorSeamFiles)
        {
            files.Select(Path.GetFileName).Should().Contain(seamFile,
                $"{seamFile} held the pre-fix sites; if it moved, update this guard's file table " +
                "rather than letting the scan quietly cover nothing");
        }

        var violations = new List<string>();
        foreach (var file in files)
        {
            foreach (var (line, receiver) in FindReturnAnnotationMappings(file))
            {
                if (Array.IndexOf(PreFixReceivers, receiver) >= 0)
                    violations.Add($"{Path.GetFileName(file)}:{line} — MapType({receiver}.ReturnType)");
            }
        }

        violations.Should().BeEmpty(
            "the enumerator element type is a materialized fact (Critical Rule 2 pattern (a), #1832): " +
            "synthesis recorded it as the class's SynthesizedInterfaces row for the triggering dunder, " +
            "and the emitter reads it through GetSynthesizedElementType. Mapping the dunder's own " +
            "return annotation asks IterableElementDecider's question a second time, with a narrower " +
            "rule, and silently disagrees for every non-generator producer annotation. If an arm needs " +
            "something GetSynthesizedElementType cannot give it, that is a missing field on the " +
            $"recorded row.\nPre-fix control: {PreFixSiteCount} sites, measured at da71f0ca3^.\n" +
            "Violations:\n" + string.Join("\n", violations));
    }

    /// <summary>The pre-fix site count, for the failure message: 3 in Iterators.cs + 1 in
    /// ClassMembers.cs, measured at <c>da71f0ca3^</c>.</summary>
    private const int PreFixSiteCount = 4;

    // ---- the wider ban inside the seam files -------------------------------------------------

    [Theory]
    [InlineData("RoslynEmitter.ClassMembers.Iterators.cs")]
    [InlineData("RoslynEmitter.ClassMembers.cs")]
    public void EnumeratorSeamFiles_MapNoReturnAnnotationAtAll(string fileName)
    {
        var file = Path.Combine(EmitterBannedTokenScanTests.FindCodeGenSourceDirectory(), fileName);
        File.Exists(file).Should().BeTrue($"{fileName} is an enumerator-seam partial this guard protects");

        var violations = FindReturnAnnotationMappings(file)
            .Select(hit => $"{fileName}:{hit.Line} — MapType({hit.Receiver}.ReturnType)")
            .ToList();

        violations.Should().BeEmpty(
            $"{fileName} emits the enumerator members, whose element type is the RECORDED " +
            "SynthesizedInterfaces row — never an annotation, under any local name. (The name-free " +
            "ban is scoped to the enumerator-seam partials: the other ClassMembers partials map " +
            "ordinary method and property return types, which legitimately ARE their annotation.)\n" +
            "Violations:\n" + string.Join("\n", violations));
    }

    // ---- positive controls -------------------------------------------------------------------

    /// <summary>
    /// The same detector, run where the pattern is legitimate and must be FOUND. An operator
    /// overload's emitted return type is its declared annotation — no interface row exists to read
    /// instead — so <see cref="ControlFileName"/> holds <see cref="ControlSiteCount"/> sites, and a
    /// detector that has stopped matching shows up here as a red rather than as a vacuous green
    /// above.
    /// </summary>
    [Fact]
    public void OperatorEmitters_HoldTheSixLegitimateSites_PositiveControl()
    {
        var file = Path.Combine(EmitterBannedTokenScanTests.FindCodeGenSourceDirectory(), ControlFileName);
        File.Exists(file).Should().BeTrue($"{ControlFileName} is this scan's positive control");

        var hits = FindReturnAnnotationMappings(file).ToList();

        hits.Should().HaveCount(ControlSiteCount,
            $"{ControlFileName} holds {ControlSiteCount} legitimate MapType(x.ReturnType) sites — an " +
            "operator overload's C# return type IS its annotation. If the operator emitters genuinely " +
            "changed, update this literal; if this count reached zero, the detector broke and the scans " +
            "above are vacuous. Found: " +
            string.Join(", ", hits.Select(h => $"{h.Line}:MapType({h.Receiver}.ReturnType)")));

        hits.Select(h => h.Receiver).Should().OnlyContain(r => r == "funcDef",
            "the control sites all read the operator's own funcDef, the same spelling the guard bans " +
            "in the enumerator seam — so the control exercises the banned shape, not a near-miss");
    }

    /// <summary>
    /// Each spelling the detector must recognise, planted in synthetic source: the two pre-fix
    /// spellings, an unqualified call, a renamed local (the shape the narrow scan deliberately does
    /// not flag but the wide one does), and whitespace the substring greps in the plan's text would
    /// have missed. The last case is a negative: reading the recorded fact is not a violation.
    /// </summary>
    [Theory]
    [InlineData("var t = _typeMapper.MapType(funcDef.ReturnType);", "funcDef")]
    [InlineData("var t = _typeMapper.MapType(nextFunc.ReturnType);", "nextFunc")]
    [InlineData("var t = MapType(funcDef.ReturnType);", "funcDef")]
    [InlineData("var t = _typeMapper.MapType( funcDef.ReturnType );", "funcDef")]
    [InlineData("var t = _typeMapper.MapType(f.ReturnType);", "f")]
    public void Detector_FlagsEachSpelling_PositiveControl(string statement, string expectedReceiver)
    {
        var hits = FindReturnAnnotationMappings(ParseMethodSource(statement)).ToList();

        hits.Select(h => h.Receiver).Should().Contain(expectedReceiver,
            "the detector must recognise this spelling, otherwise the scans above pass vacuously");
    }

    [Fact]
    public void Detector_IgnoresTheRecordedFactRead_NegativeControl()
    {
        FindReturnAnnotationMappings(
                ParseMethodSource("var t = GetSynthesizedElementType(DunderNames.Next);"))
            .Should().BeEmpty("reading the materialized row is the sanctioned route, not a violation");

        FindReturnAnnotationMappings(ParseMethodSource("var t = _typeMapper.MapType(funcDef.Parameters);"))
            .Should().BeEmpty("only the .ReturnType annotation is the subject");
    }

    // ---- detector ----------------------------------------------------------------------------

    /// <summary>
    /// Every <c>MapType(&lt;receiver&gt;.ReturnType)</c> in a file, as (line, receiver name). Roslyn
    /// rather than a substring grep so whitespace and <c>this.</c>/<c>_typeMapper.</c> qualification
    /// cannot evade it, and so a doc comment naming the pattern (this file's own prose does) is never
    /// a hit. Exactly one argument is required: a two-argument <c>MapType</c> overload is a different
    /// call with a different meaning, and would join the detector explicitly, not by accident.
    /// </summary>
    private static IEnumerable<(int Line, string Receiver)> FindReturnAnnotationMappings(SyntaxNode root)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsCalled(invocation.Expression, "MapType"))
                continue;
            if (invocation.ArgumentList.Arguments.Count != 1)
                continue;
            if (invocation.ArgumentList.Arguments[0].Expression is not MemberAccessExpressionSyntax
                {
                    Name: SimpleNameSyntax { Identifier.Text: "ReturnType" },
                } member)
                continue;

            var receiver = RightmostName(member.Expression);
            if (receiver == null)
                continue;

            yield return (invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1, receiver);
        }
    }

    private static IEnumerable<(int Line, string Receiver)> FindReturnAnnotationMappings(string filePath)
        => FindReturnAnnotationMappings(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)).GetRoot());

    private static SyntaxNode ParseMethodSource(string statement)
        => CSharpSyntaxTree.ParseText("class C { void M() { " + statement + " } }").GetRoot();

    /// <summary>Whether the invoked expression names <paramref name="method"/>, bare or qualified.</summary>
    private static bool IsCalled(ExpressionSyntax invoked, string method) => invoked switch
    {
        IdentifierNameSyntax id => id.Identifier.Text == method,
        GenericNameSyntax generic => generic.Identifier.Text == method,
        MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } => name.Identifier.Text == method,
        _ => false,
    };

    private static string? RightmostName(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax { Name: SimpleNameSyntax name } => name.Identifier.Text,
        _ => null,
    };
}
