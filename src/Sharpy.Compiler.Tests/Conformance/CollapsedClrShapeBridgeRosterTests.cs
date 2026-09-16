using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Compiler-side twin of the Core test <c>CollapsedClrShapesConstructTheWrapper</c> (#1866). The Core
/// test's roster is a literal, so it cannot notice a NEW bridge collapse arm; this test drives the
/// same literal list through <see cref="ClrTypeBridge.MapClrTypeToSemanticType"/> and asserts each
/// shape actually collapses onto the named builtin — so a collapse arm added without a Core
/// constructor row is red here, and a Core row without a constructor is red there.
///
/// <para>
/// It also pins the four identity controls the bridge must NOT collapse (<c>List&lt;&gt;</c>,
/// <c>Dictionary&lt;,&gt;</c>, <c>HashSet&lt;&gt;</c>, <c>IOrderedEnumerable&lt;&gt;</c>, #1517/#1390):
/// each keeps its own CLR name. Adding a fake collapse of <c>IOrderedEnumerable&lt;&gt;</c> to the
/// bridge turns that control row red.
/// </para>
/// </summary>
public class CollapsedClrShapeBridgeRosterTests
{
    public sealed record CollapseRow(string Builtin, Type ClrShape);

    /// <summary>The nine arms <c>ClrTypeBridge.MapGenericTypeCore</c> collapses onto a builtin
    /// collection: six onto list (the interface half plus <c>ReadOnlyCollection&lt;T&gt;</c>), one onto
    /// set, two onto dict. There is no <c>IReadOnlySet&lt;T&gt;</c> arm.</summary>
    private static readonly CollapseRow[] CollapsedArms =
    {
        new(BuiltinNames.List, typeof(IList<int>)),
        new(BuiltinNames.List, typeof(ICollection<int>)),
        new(BuiltinNames.List, typeof(IEnumerable<int>)),
        new(BuiltinNames.List, typeof(IReadOnlyList<int>)),
        new(BuiltinNames.List, typeof(IReadOnlyCollection<int>)),
        new(BuiltinNames.List, typeof(ReadOnlyCollection<int>)),

        new(BuiltinNames.Set, typeof(ISet<int>)),

        new(BuiltinNames.Dict, typeof(IDictionary<string, int>)),
        new(BuiltinNames.Dict, typeof(IReadOnlyDictionary<string, int>)),
    };

    /// <summary>Concrete .NET collection types that keep their own identity — the bridge names them
    /// after their CLR definition rather than a builtin (#1517, #1390).</summary>
    private static readonly (string ClrName, Type ClrShape)[] IdentityControls =
    {
        ("List", typeof(List<int>)),
        ("Dictionary", typeof(Dictionary<string, int>)),
        ("HashSet", typeof(HashSet<int>)),
        ("IOrderedEnumerable", typeof(IOrderedEnumerable<int>)),
    };

    [Fact]
    public void EveryCollapsedArm_MapsToItsBuiltin()
    {
        var bridge = new ClrTypeBridge();
        var wrong = new List<string>();

        foreach (var row in CollapsedArms)
        {
            var mapped = bridge.MapClrTypeToSemanticType(row.ClrShape);
            if (mapped is not GenericType generic || !string.Equals(generic.Name, row.Builtin, StringComparison.Ordinal))
            {
                wrong.Add($"{row.ClrShape.Name} -> {mapped.GetDisplayName()} (expected {row.Builtin})");
            }
        }

        wrong.Should().BeEmpty(
            "every shape the bridge collapses must map to its Sharpy builtin, and the Core totality "
            + "test asserts each has a wrapper constructor; a collapse arm added here without a Core "
            + "constructor row makes the Core test red (#1866).\nWrong:\n" + string.Join("\n", wrong));
    }

    [Fact]
    public void IdentityControls_KeepTheirClrName()
    {
        var bridge = new ClrTypeBridge();
        var collapsed = new List<string>();

        foreach (var (clrName, clrShape) in IdentityControls)
        {
            var mapped = bridge.MapClrTypeToSemanticType(clrShape);
            if (mapped is not GenericType generic || !string.Equals(generic.Name, clrName, StringComparison.Ordinal))
            {
                collapsed.Add($"{clrShape.Name} -> {mapped.GetDisplayName()} (expected identity '{clrName}')");
            }
        }

        collapsed.Should().BeEmpty(
            "a concrete .NET collection keeps its own identity — collapsing one onto a builtin loses "
            + "the type the user named (#1517, #1390).\nCollapsed:\n" + string.Join("\n", collapsed));
    }
}
