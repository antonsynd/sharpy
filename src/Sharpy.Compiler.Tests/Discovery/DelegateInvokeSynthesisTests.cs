using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Guards the #1512 delegate arms: every kind ladder that marks a CLR type
/// <see cref="TypeKind.Delegate"/> obtains its synthesized <c>Invoke</c> from the single
/// helper <c>ClrTypeBridge.SynthesizeDelegateInvoke</c>, and the resulting symbol carries
/// EXACTLY ONE <c>Invoke</c> with <c>ClrMethod</c> metadata.
///
/// <para>
/// The single-Invoke assertion is the regression the cache-hydration path nearly shipped:
/// the overload index's reflection walk collects <c>Invoke</c> (public, non-special-name),
/// so <c>CachedModuleDiscovery.PopulateTypeSymbolMembers</c> converting the cached signature
/// NEXT TO the skeleton's synthesized one double-adds it — the population loop now skips a
/// delegate's cached <c>Invoke</c> explicitly.
/// </para>
///
/// <para>
/// Two arms are unreachable through any public seam today and are guarded structurally
/// (shared helper + the cache-path skip) rather than end-to-end: the cache-hydration arm
/// (no stdlib assembly ships a public delegate) and the bridge's generic-definition arm
/// (<c>GetOrCreateClrDefinitionSymbol</c> is only called from the IEnumerable/List mapping
/// flows). The reachable arms are asserted directly below.
/// </para>
/// </summary>
public class DelegateInvokeSynthesisTests
{
    [Theory]
    [InlineData(typeof(System.Threading.ThreadStart))]
    [InlineData(typeof(EventHandler))]
    public void ModuleRegistryArm_DelegateCarriesExactlyOneInvoke(Type delegateType)
    {
        var registry = new ModuleRegistry(NullLogger.Instance);

        var sym = registry.CreateTypeSymbolFromClrType(delegateType);

        sym.Should().NotBeNull();
        sym!.TypeKind.Should().Be(TypeKind.Delegate);
        sym.Methods.Where(m => m.Name == "Invoke").Should().ContainSingle(
                "one synthesized Invoke, never a duplicate (#1512)")
            .Which.ClrMethod.Should().NotBeNull("the synthesized Invoke keeps the CLR metadata");
    }

    [Fact]
    public void BuiltinRegistryArm_DelegateCarriesExactlyOneInvoke()
    {
        var registry = new BuiltinRegistry();

        var sym = registry.TryResolveClrType("EventHandler");

        sym.Should().NotBeNull("bare EventHandler resolves through the builtin CLR fallback");
        sym!.TypeKind.Should().Be(TypeKind.Delegate);
        sym.Methods.Where(m => m.Name == "Invoke").Should().ContainSingle()
            .Which.ClrMethod.Should().NotBeNull();
    }

    [Fact]
    public void BridgeNonGenericArm_DelegateCarriesExactlyOneInvoke()
    {
        var bridge = new ClrTypeBridge();

        var mapped = bridge.MapClrTypeToSemanticType(typeof(System.Threading.ThreadStart));

        var udt = mapped.Should().BeOfType<UserDefinedType>().Subject;
        udt.Symbol.Should().NotBeNull();
        udt.Symbol!.TypeKind.Should().Be(TypeKind.Delegate);
        udt.Symbol.Methods.Where(m => m.Name == "Invoke").Should().ContainSingle()
            .Which.ClrMethod.Should().NotBeNull();
    }
}
