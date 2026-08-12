using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1346: <c>Iterator</c> and the three dict views were registered against <c>typeof(object)</c>
/// placeholders. That is not inert — <c>PopulateClrInterfaces</c> short-circuits on
/// <c>typeof(object)</c>, so none of them carried a CLR interface list, and every consumer that
/// reads interface metadata (ProtocolValidator, OperatorValidator, overload resolution) saw them
/// as bare classes.
/// </summary>
/// <remarks>
/// The obvious acceptance — "verify ClrTypeBridge maps them, add a round-trip cell" — is VACUOUS:
/// <c>ClrTypeBridge</c> holds zero references to <c>BuiltinRegistry</c>. It maps
/// <c>Sharpy.DictKeyView`2</c> by a hard-coded full-name constant and <c>Iterator</c> via
/// <c>ClrTypeHelper</c> on the CLR type, so a round-trip cell is green whether or not the registry
/// still says <c>typeof(object)</c>. The discriminating observation is the REGISTERED SYMBOL — its
/// <c>ClrType</c>, and the interface list that only exists once the placeholder is gone.
/// </remarks>
public class BuiltinRegistryRealClrTypesTests
{
    private static TypeSymbol Registered(string name)
    {
        var symbol = new BuiltinRegistry().GetType(name);
        symbol.Should().NotBeNull($"'{name}' must be registered for this test to mean anything");
        return symbol!;
    }

    [Theory]
    [InlineData(BuiltinNames.Iterator)]
    [InlineData(BuiltinNames.DictKeyView)]
    [InlineData(BuiltinNames.DictItemsView)]
    [InlineData(BuiltinNames.DictValuesView)]
    public void PlaceholderRegistrations_AreGone(string name)
    {
        var clrType = Registered(name).ClrType;

        clrType.Should().NotBeNull();
        clrType.Should().NotBe(typeof(object),
            "typeof(object) is the placeholder PopulateClrInterfaces skips, so the symbol carries "
            + "no interface metadata while it is there");
        clrType!.FullName.Should().StartWith("Sharpy.", "the registration must name the real type");
    }

    /// <summary>
    /// The consequence, and the reason the change is not cosmetic: interface population is switched
    /// ON by naming the real type. This is the assertion a bridge round-trip cannot make.
    /// </summary>
    [Fact]
    public void RealRegistration_TurnsOnClrInterfacePopulation()
    {
        Registered(BuiltinNames.Iterator).Interfaces
            .Should().NotBeEmpty("Sharpy.Iterator<T> implements IEnumerator<T>/IEnumerable<T>");

        Registered(BuiltinNames.DictKeyView).Interfaces
            .Should().NotBeEmpty("Sharpy.DictKeyView<K,V> implements IReadOnlyCollection<K>");
    }

    /// <summary>
    /// <c>IsAbstract</c> is read from the CLR type, which is what lets the value-position rule refuse
    /// a reference to <c>Iterator</c> as non-constructible (SPY0346) rather than as merely unfamilied.
    /// A placeholder is not abstract, so this was unreachable before.
    /// </summary>
    [Fact]
    public void IteratorIsAbstract_AndTheDictViewsAreNot()
    {
        Registered(BuiltinNames.Iterator).IsAbstract.Should().BeTrue();
        Registered(BuiltinNames.DictKeyView).IsAbstract.Should().BeFalse(
            "the views are sealed and concrete — their refusal is about constructor accessibility, "
            + "which is a separate arm still open on #1346");
    }
}
