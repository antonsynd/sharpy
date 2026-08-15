using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// <c>TypeSymbol.Constructors.Count == 0</c> must mean ONE thing: the type exposes no public
/// instance constructor.
///
/// <para>#1473: it meant two. <c>BuiltinRegistry.RegisterType</c> filled the list from
/// <c>ClrConstructorSurface</c> and <c>ModuleRegistry</c> did the same, but
/// <c>LoadBuiltinTypes</c> — the DISCOVERY path — inserted symbols into the table and filled it
/// nowhere. So an empty list meant "no public constructor" for a registered type and "never
/// populated" for a discovered one, indistinguishable at every read site. Every Sharpy.Core
/// exception sits on the discovery path, so this was not a hypothetical: #1346 wrongly refused
/// <c>ValueError</c> on exactly this confusion, and its fix had to reach past the field to
/// reflection.</para>
///
/// <para>The two cells below are the two realities the issue shows apart, and they must be read
/// TOGETHER: a fix that populated everything unconditionally would make the first pass while
/// silently breaking the second, which is the answer #1346 depends on.</para>
/// </summary>
public class DiscoveryPathConstructorSurfaceTests
{
    private readonly ITestOutputHelper _output;

    public DiscoveryPathConstructorSurfaceTests(ITestOutputHelper output) => _output = output;

    private static TypeSymbol Resolve(string name)
    {
        var symbol = new BuiltinRegistry().GetType(name);
        return symbol.Should().NotBeNull(
            $"'{name}' must resolve through the builtin registry, or the cell below is vacuous")
            .And.Subject as TypeSymbol ?? throw new InvalidOperationException("unreachable");
    }

    /// <summary>
    /// The POSITIVE cell. <c>ValueError</c> declares two public constructors
    /// (<c>ValueError(string)</c> and <c>ValueError(string, Exception)</c>) and reaches the registry
    /// through discovery — the path that populated nothing.
    /// </summary>
    [Fact]
    public void ADiscoveredException_CarriesItsConstructorSurface()
    {
        var valueError = Resolve("ValueError");

        _output.WriteLine($"ValueError.Constructors = {valueError.Constructors.Count}");
        foreach (var ctor in valueError.Constructors)
            _output.WriteLine("  " + string.Join(", ", ctor.Parameters.Select(p => $"{p.Name}: {p.Type.GetDisplayName()}")));

        valueError.Constructors.Should().HaveCount(2,
            "ValueError declares ValueError(string) and ValueError(string, Exception). Before #1473 "
            + "the discovery path reported zero, which is the reading that made #1346 refuse it");

        // Every collected constructor is an __init__ carrying the Sharpy `self` convention, as the
        // registered and module paths both produce. A surface that disagreed in SHAPE would satisfy
        // the count above while still being useless to the emitter's forwarder synthesis.
        valueError.Constructors.Should().OnlyContain(c => c.Name == DunderNames.Init,
            "the surface is expressed as __init__ symbols, as on the other two registry paths");
        valueError.Constructors.Should().OnlyContain(
            c => c.Parameters.Count > 0 && c.Parameters[0].Name == PythonNames.Self,
            "ClrConstructorSurface puts `self` first and the type checker skips it — a surface "
            + "without it would be off by one at every arity check");
    }

    /// <summary>
    /// The DISCRIMINATING NEGATIVE. <c>DictKeyView</c>'s only constructor is <c>internal</c>, so its
    /// empty surface is the TRUE answer, not an unpopulated one. This is the cell that fails if the
    /// fix populates indiscriminately — and #1346's refusal of <c>d.keys()</c>-only construction
    /// rests on this answer staying empty.
    /// </summary>
    [Fact]
    public void ADiscoveredViewType_WithOnlyAnInternalConstructor_StaysEmpty()
    {
        var view = Resolve("DictKeyView");

        _output.WriteLine($"DictKeyView.Constructors = {view.Constructors.Count}");

        view.Constructors.Should().BeEmpty(
            "DictKeyView's only constructor is internal, so empty is the CORRECT answer here. "
            + "#1473 makes empty mean empty — it does not make every discovered type look "
            + "constructible. If this cell ever goes non-empty, #1346's refusal of a direct "
            + "DictKeyView() construction has quietly lost its basis");
    }
}
