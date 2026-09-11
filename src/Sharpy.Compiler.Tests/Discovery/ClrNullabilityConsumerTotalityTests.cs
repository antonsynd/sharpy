using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// The scan guard #1705 promised: every site that derives a semantic type FROM A CLR MEMBER either
/// goes through the nullability-aware seam (<see cref="ClrTypeBridge.MapPropertyType"/>,
/// <c>MapFieldType</c>, <c>MapReturnType</c>, <c>MapParameterType</c>, or
/// <c>OverloadIndexBuilder.CreateParameterTypeSignature</c>) or is listed here with the issue that
/// owns it. A reflected <see cref="Type"/> is NRT-blind — <c>string?</c> and <c>string</c> are the
/// same <c>typeof(string)</c> — so a site that maps the raw Type silently drops the member's
/// declared nullability, and the drop is invisible in every diagnostic-level check.
///
/// <para>
/// The roster is keyed by file plus the call's own source line, not by line number: a line number
/// rots on the next edit above it, and a rostered line that disappears fails loudly ("drain on
/// fix"). A NEW raw mapping call anywhere under <c>Semantic/</c> or <c>Discovery/</c> fails until it
/// is classified here — that is the falsifiable arm.
/// </para>
/// </summary>
public class ClrNullabilityConsumerTotalityTests
{
    private readonly ITestOutputHelper _output;

    public ClrNullabilityConsumerTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>Why a raw mapping call is allowed to be raw.</summary>
    private enum Verdict
    {
        /// <summary>The mapped Type does not come from a member: a type ARGUMENT, an element type,
        /// a receiver's own type. There is no member to read an annotation from.</summary>
        NotMemberDerived,

        /// <summary>The site reads the member's declared nullability itself, beside this call.</summary>
        AppliesDeclaredNullability,

        /// <summary>A member-derived site that does NOT apply it, with the issue that owns it.</summary>
        KnownBypass
    }

    private sealed record Site(string File, string Line, Verdict Verdict, string Reason);

    /// <summary>
    /// Every raw <c>MapClrTypeToSemanticType</c>/<c>CreateTypeSignatureFromClr</c> call under
    /// <c>Semantic/</c> and <c>Discovery/</c>, classified. <see cref="ClrTypeBridge"/> itself is the
    /// mapper and is excluded — it is the thing being wrapped, not a consumer.
    /// </summary>
    private static IReadOnlyList<Site> Roster() => new[]
    {
        new Site("Semantic/TypeInferenceService.cs",
            "return _clrTypeMapper.Value.MapClrTypeToSemanticType(clrElementType);",
            Verdict.NotMemberDerived,
            "the ELEMENT type of an iterated CLR sequence — a type argument, not a member"),
        new Site("Semantic/TypeInferenceService.cs",
            "return _clrTypeMapper.Value.MapClrTypeToSemanticType(elementType);",
            Verdict.NotMemberDerived,
            "same: an element type reached through the receiver's own type arguments"),
        new Site("Semantic/TypeChecker.Expressions.Access.cs",
            "var mapped = _bclGenericMethodBridge.MapClrTypeToSemanticType(t);",
            Verdict.NotMemberDerived,
            "a type ARGUMENT of a constructed receiver, lowered to its Sharpy spelling"),
        new Site("Semantic/TypeChecker.Expressions.Access.cs",
            "var mapped = _bclGenericMethodBridge.MapClrTypeToSemanticType(clrArg);",
            Verdict.NotMemberDerived,
            "a type ARGUMENT of the receiver's CLR type"),
        new Site("Semantic/GenericReferenceResolver.cs",
            "loweredTypeArgs.Add(_clrTypeBridge.Value.MapClrTypeToSemanticType(clrArg));",
            Verdict.NotMemberDerived,
            "a type ARGUMENT vector (two sites: the staged and the completed extension call)"),
        new Site("Semantic/GenericReferenceResolver.cs",
            "_clrTypeBridge.Value.MapClrTypeToSemanticType(partial.ParameterTypes[i]),",
            Verdict.AppliesDeclaredNullability,
            "the staged extension symbol's parameter — migrated: the declaring ParameterInfo's "
            + "annotation is applied beside this call"),
        new Site("Semantic/GenericReferenceResolver.cs",
            "_clrTypeBridge.Value.MapClrTypeToSemanticType(partial.ReturnType),",
            Verdict.AppliesDeclaredNullability,
            "the staged extension symbol's return — migrated: the open MethodInfo's annotation is "
            + "applied beside this call"),
        new Site("Semantic/Registry/BuiltinRegistry.cs",
            "return _clrTypeMapper.MapClrTypeToSemanticType(arg);",
            Verdict.NotMemberDerived,
            "a type ARGUMENT of a registered builtin's CLR type"),
        new Site("Semantic/Registry/BuiltinRegistry.cs",
            "CreateTypeSignatureFromClr(method.ReturnType, typeMapper),",
            Verdict.AppliesDeclaredNullability,
            "an extension method's RETURN — migrated: WrapIfDeclaredNullable reads the MethodInfo"),
        new Site("Semantic/Registry/BuiltinRegistry.cs",
            "CreateTypeSignatureFromClr(param.ParameterType, typeMapper),",
            Verdict.AppliesDeclaredNullability,
            "an extension method's PARAMETER — migrated: WrapIfDeclaredNullable reads the ParameterInfo"),
        new Site("Semantic/Registry/BuiltinRegistry.cs",
            ".Select(t => CreateTypeSignatureFromClr(t, typeMapper))",
            Verdict.NotMemberDerived,
            "the TYPE ARGUMENTS of an already-mapped signature"),
        new Site("Semantic/Registry/BuiltinRegistry.cs",
            "CreateTypeSignatureFromClr(elementType, typeMapper)",
            Verdict.NotMemberDerived,
            "an ARRAY ELEMENT type of an already-mapped signature"),
        new Site("Semantic/Registry/BuiltinRegistry.cs",
            "var semanticType = typeMapper.MapClrTypeToSemanticType(clrType);",
            Verdict.NotMemberDerived,
            "the shared Type→signature body; its two member-derived callers apply the annotation"),
        new Site("Semantic/Registry/ModuleRegistry.cs",
            "var mapped = bridge.MapClrTypeToSemanticType(arg);",
            Verdict.NotMemberDerived,
            "a type ARGUMENT of a module-registered CLR type"),
        new Site("Discovery/ClrMemberTypeResolver.cs",
            "var candidate = _bridge.MapClrTypeToSemanticType(clrType);",
            Verdict.AppliesDeclaredNullability,
            "TryMapFaithfully applies ClrDeclaredNullability.Apply to this candidate on the next line"),
        new Site("Discovery/Caching/OverloadIndexBuilder.cs",
            "var semanticType = _typeMapper.MapClrTypeToSemanticType(clrType);",
            Verdict.NotMemberDerived,
            "the shared Type→TypeSignature body; the member-derived entry points "
            + "(CreateParameterTypeSignature, CreateReturnTypeSignature) wrap it"),
    };

    /// <summary>
    /// The scan itself. Red when a call site exists that the roster does not name, or when a
    /// rostered site has disappeared (its classification is stale and must be re-read).
    /// </summary>
    [Fact]
    public void EveryClrMappingConsumer_IsRosteredWithItsNullabilityVerdict()
    {
        var root = FindRepoRoot();
        var compiler = Path.Combine(root, "src", "Sharpy.Compiler");

        var found = new List<(string File, string Line)>();
        foreach (var dir in new[] { "Semantic", "Discovery" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(compiler, dir), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(compiler, file).Replace('\\', '/');

                // The bridge IS the mapper, not a consumer of it.
                if (relative == "Discovery/ClrTypeBridge.cs")
                    continue;

                foreach (var raw in File.ReadLines(file))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("//", StringComparison.Ordinal)
                        || line.StartsWith("///", StringComparison.Ordinal))
                        continue;

                    if (line.Contains("MapClrTypeToSemanticType(", StringComparison.Ordinal)
                        || line.Contains("CreateTypeSignatureFromClr(", StringComparison.Ordinal))
                    {
                        // The declaration of the helper is not a call of it.
                        if (line.Contains("private static TypeSignature CreateTypeSignatureFromClr", StringComparison.Ordinal))
                            continue;
                        found.Add((relative, line));
                    }
                }
            }
        }

        var roster = Roster();
        var rosterKeys = roster.Select(s => (s.File, s.Line)).ToHashSet();
        var foundKeys = found.Select(f => (f.File, f.Line)).ToHashSet();

        var unrostered = foundKeys.Where(k => !rosterKeys.Contains(k)).OrderBy(k => k.Item1).ToList();
        var stale = rosterKeys.Where(k => !foundKeys.Contains(k)).OrderBy(k => k.Item1).ToList();

        _output.WriteLine($"CLR mapping call sites found: {found.Count} (distinct {foundKeys.Count}); rostered: {roster.Count}");
        foreach (var site in roster.Where(s => s.Verdict == Verdict.KnownBypass))
            _output.WriteLine($"  KNOWN BYPASS {site.File}: {site.Reason}");

        Assert.True(unrostered.Count == 0,
            "Un-rostered CLR type-mapping call site(s). A member-derived site must apply the member's "
            + "declared nullability (#1705); a non-member site must say so here:\n"
            + string.Join("\n", unrostered.Select(u => $"  {u.Item1}: {u.Item2}")));

        Assert.True(stale.Count == 0,
            "Rostered call site(s) no longer present — the classification is stale and the roster "
            + "must be re-read (drain on fix):\n"
            + string.Join("\n", stale.Select(u => $"  {u.Item1}: {u.Item2}")));
    }

    /// <summary>
    /// The roster's own shape: every entry carries a reason, and the KnownBypass entries — and only
    /// those — cite an issue. Anchored to the literal verdict set so a new verdict kind cannot be
    /// added without deciding what it means here.
    /// </summary>
    [Fact]
    public void RosterEntries_CarryAReason_AndOnlyBypassesCiteAnIssue()
    {
        Assert.Equal(3, Enum.GetValues<Verdict>().Length);

        foreach (var site in Roster())
        {
            Assert.False(string.IsNullOrWhiteSpace(site.Reason), $"{site.File}: {site.Line} has no reason");

            if (site.Verdict == Verdict.KnownBypass)
                Assert.Contains("#", site.Reason, StringComparison.Ordinal);
        }
    }

    // ── The predicate itself, probed on types declared HERE, where the annotation is known ──

    private sealed class BareAndAnnotated<T>
    {
        public T Bare { get; set; } = default!;
        public T? Annotated { get; set; }
        public T BareField = default!;
        public string NonNullableRef { get; set; } = "";
        public string? NullableRef { get; set; }
        public T BareReturn() => default!;
        public void BareParameter(T value) { _ = value; }
        public void NullableRefParameter(string? value) { _ = value; }
        public void NonNullableRefParameter(string value) { _ = value; }
    }

    /// <summary>
    /// A member declared with a BARE type parameter is not declared-nullable — on the generic type
    /// DEFINITION, which is the shape discovery and the overload index reflect over.
    /// <see cref="NullabilityInfoContext"/> answers <see cref="NullabilityState.Nullable"/> for an
    /// unconstrained <c>T</c>, so without the definition arm in
    /// <c>ClrDeclaredNullability.DeclaredAsGenericParameter</c> every bare-<c>T</c> member is typed
    /// <c>T | None</c>: <c>list[str].pop(0)</c> came back <c>str | None</c> and
    /// <c>list.append</c>'s slot became <c>Box[int] | None</c> (#1705 B1).
    /// </summary>
    [Fact]
    public void BareTypeParameterMember_OnTheDefinition_IsNotDeclaredNullable()
    {
        var definition = typeof(BareAndAnnotated<>);

        Assert.False(ClrDeclaredNullability.DeclaresNullable(definition.GetProperty("Bare")!));
        Assert.False(ClrDeclaredNullability.DeclaresNullable(definition.GetField("BareField")!));
        Assert.False(ClrDeclaredNullability.DeclaresNullableReturn(definition.GetMethod("BareReturn")!));
        Assert.False(ClrDeclaredNullability.DeclaresNullableArgument(
            definition.GetMethod("BareParameter")!.GetParameters()[0]));
    }

    /// <summary>
    /// The positive control for the test above, and the instrument check for the whole seam: a
    /// REFERENCE-typed member on the same definition still reports its declared annotation, so the
    /// definition arm suppresses the type-parameter case only. Without this control the assertions
    /// above would pass just as well if the predicate answered <c>false</c> for everything.
    /// </summary>
    [Fact]
    public void ReferenceTypedMembers_OnTheSameDefinition_KeepTheirDeclaredAnnotation()
    {
        var definition = typeof(BareAndAnnotated<>);

        Assert.True(ClrDeclaredNullability.DeclaresNullable(definition.GetProperty("NullableRef")!));
        Assert.False(ClrDeclaredNullability.DeclaresNullable(definition.GetProperty("NonNullableRef")!));
        Assert.True(ClrDeclaredNullability.DeclaresNullableArgument(
            definition.GetMethod("NullableRefParameter")!.GetParameters()[0]));
        Assert.False(ClrDeclaredNullability.DeclaresNullableArgument(
            definition.GetMethod("NonNullableRefParameter")!.GetParameters()[0]));
    }

    /// <summary>
    /// What the runtime can and cannot tell us about <c>T</c> versus <c>T?</c> on a generic
    /// definition — measured, not assumed, because the answer decides whether a declared <c>T?</c>
    /// member (<c>List&lt;T&gt;.Find</c>, <c>Enumerable.FirstOrDefault</c>) can ever be typed
    /// <c>T | None</c>. The assertion pins TODAY's answer; if a future runtime distinguishes them,
    /// this test goes red and the arm can be made more precise.
    /// </summary>
    [Fact]
    public void AnnotatedTypeParameterMember_IsMeasured_NotAssumed()
    {
        var definition = typeof(BareAndAnnotated<>);
        var context = new NullabilityInfoContext();

        var bare = context.Create(definition.GetProperty("Bare")!);
        var annotated = context.Create(definition.GetProperty("Annotated")!);

        _output.WriteLine($"bare T      : Read={bare.ReadState} Write={bare.WriteState}");
        _output.WriteLine($"annotated T?: Read={annotated.ReadState} Write={annotated.WriteState}");

        Assert.True(
            bare.ReadState == annotated.ReadState,
            $"NullabilityInfoContext now distinguishes `T` (Read={bare.ReadState}) from `T?` "
            + $"(Read={annotated.ReadState}) on a generic definition. The suppression in "
            + "ClrDeclaredNullability.DeclaredAsGenericParameter can be narrowed to the bare case, "
            + "which would let List<T>.Find and Enumerable.FirstOrDefault be typed `T | None`.");
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException(
            $"Could not find repository root starting from '{AppContext.BaseDirectory}'.");
    }
}
