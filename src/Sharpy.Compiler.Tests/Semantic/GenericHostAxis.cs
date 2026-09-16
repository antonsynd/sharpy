namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The shared roster of generic-type HOST shapes a static member is reached through, so a change to
/// the constructed-generic host set is made in ONE place. <see cref="TypeDenotingReceiverMatrixTests"/>
/// iterates it for the bracketed-generic spelling (#1817), and P10's
/// <c>SynthesizedInterfaceVisibilityTests</c> reuses it for its constructed-generic host column
/// (#1865, #1859) — never a private copy.
///
/// <para>Each host declares a type carrying <c>const K: int = 3</c> and names the constructed
/// RECEIVER a static member is read through (<c>G[int].K</c>). <c>Plain</c> is the non-generic
/// control (the mechanism must not disturb it) and <c>DerivedOfGeneric</c> is the inheritance
/// control (a static member reached through a derived generic reference).</para>
/// </summary>
internal static class GenericHostAxis
{
    /// <summary>A generic-host shape: its type declaration and the constructed receiver expression.</summary>
    internal sealed record Host(string Name, string Declaration, string Receiver);

    /// <summary>Anchored to a literal so the totality assertion cannot be vacuous against the array.</summary>
    public const int HostCount = 6;

    public static readonly Host[] Hosts =
    {
        new("Plain",
            "class HPlain:\n    const K: int = 3\n",
            "HPlain"),
        new("ConstructedClass",
            "class HClass[T]:\n    const K: int = 3\n",
            "HClass[int]"),
        new("NestedConstructed",
            "class HClass[T]:\n    const K: int = 3\n",
            "HClass[HClass[int]]"),
        new("ConstructedStruct",
            "struct HStruct[T]:\n    const K: int = 3\n",
            "HStruct[int]"),
        new("ConstructedInterface",
            "interface HInterface[T]:\n    const K: int = 3\n",
            "HInterface[int]"),
        new("DerivedOfGeneric",
            "class HBase[T]:\n    const K: int = 3\n\n\nclass HDerived[T](HBase[T]):\n    pass\n",
            "HDerived[int]"),
    };
}
