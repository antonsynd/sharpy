using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Discovery;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// An absence verdict must not outlive the facts it summarizes.
///
/// <para>#1493: <c>ClrAttributeResolver</c>'s type cache was <c>static readonly</c> and held BOTH
/// polarities, on the premise that the only event which can change the answer is the class's own
/// framework loading — which cleared it. That premise covered loads this class performs and no
/// others. <c>ModuleRegistry.LoadReference</c> never cleared it, so in a long-lived process (the
/// LSP) a name probed before its assembly was loaded stayed "absent" until the process restarted:
/// an SPY0495 refusal that is order-dependent, silent, and unfixable by editing the file.</para>
///
/// <para>The shape below is the LSP's: probe (miss), an assembly arrives, probe again in a NEW
/// compilation. Only the second probe's verdict is under test — within one compilation a stable
/// answer is correct and desirable.</para>
/// </summary>
public class AttributeAbsenceVerdictScopeTests
{
    private readonly ITestOutputHelper _output;

    public AttributeAbsenceVerdictScopeTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// A real, loadable assembly declaring one attribute in a namespace nothing else uses. Built
    /// rather than referenced so the "before" state is genuinely absent — a type from an assembly
    /// the test host might already have loaded would make the first probe's miss accidental.
    /// </summary>
    private static byte[] EmitAttributeAssembly(string namespaceName, string attributeName, string assemblyName)
    {
        var source = $@"
namespace {namespaceName}
{{
    public sealed class {attributeName} : System.Attribute {{ }}
}}";

        // File.Exists, not just a non-empty Location: this suite runs alongside tests that build
        // and delete assemblies in temp directories, and a stale Location makes CreateFromFile
        // throw FileNotFound. Measured — it flaked exactly that way.
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        result.Success.Should().BeTrue(
            "the specimen assembly must build, or the cell measures nothing: "
            + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        return stream.ToArray();
    }

    [Fact]
    public void AnAbsenceVerdict_DoesNotSurviveIntoTheNextCompilation()
    {
        // A namespace unique to this run: a second run in the same process must not inherit the
        // first's PRESENCE verdict, which is process-wide by design.
        var tag = Guid.NewGuid().ToString("N");
        var ns = $"Sharpy.Probe.N{tag}";
        var attribute = "WidgetAttribute";
        var fullName = $"{ns}.{attribute}";

        // --- Compilation 1: the type genuinely does not exist yet. The miss is recorded.
        var first = new ClrAttributeResolver();
        first.ResolvesToClrType(fullName).Should().BeFalse(
            "nothing declares this type yet — if this were true the assembly name is not unique "
            + "and the rest of the cell is meaningless");

        // Probing twice inside one compilation must stay stable: that is the memo doing its job,
        // and it is the behaviour the fix deliberately keeps.
        first.ResolvesToClrType(fullName).Should().BeFalse(
            "within one compilation the answer is memoized and stable");

        // --- The event the old premise did not cover: an assembly arrives from somewhere other
        //     than this class's own framework loading, exactly as ModuleRegistry.LoadReference
        //     brings in a .spyproj reference.
        var loaded = Assembly.Load(EmitAttributeAssembly(ns, attribute, $"SharpyProbe{tag}"));
        loaded.GetType(fullName).Should().NotBeNull(
            "the specimen assembly must really declare the type, or the 'after' probe would be "
            + "asserting the absence of something that is still absent");

        // --- Compilation 2: a fresh resolver, as ValidationPipelineFactory builds a fresh
        //     DecoratorValidator per compilation.
        var second = new ClrAttributeResolver();
        var verdict = second.ResolvesToClrType(fullName);

        _output.WriteLine($"{fullName}: before=False, after={verdict}");

        verdict.Should().BeTrue(
            "the type exists now, so the next compilation must see it. While the absence verdict "
            + "was process-lifetime this stayed false until the process restarted — a stale "
            + "SPY0495 that no edit could clear (#1493)");
    }

    /// <summary>
    /// The other half of the contract, so the fix cannot be "scope everything and re-reflect
    /// forever": a PRESENCE verdict is still shared across compilations. Types do not unload, so
    /// that verdict can never go stale, and keeping it process-wide is what stops the hot path
    /// from paying for the fix.
    /// </summary>
    [Fact]
    public void APresenceVerdict_IsStillSharedAcrossCompilations()
    {
        // Guaranteed loaded and guaranteed present.
        const string known = "System.ObsoleteAttribute";

        new ClrAttributeResolver().ResolvesToClrType(known).Should().BeTrue(
            "System.ObsoleteAttribute exists in any process that got this far");
        new ClrAttributeResolver().ResolvesToClrType(known).Should().BeTrue(
            "and a second compilation must agree — presence is monotonic");
    }
}
