namespace Sharpy.Compiler.Shared;

/// <summary>
/// The member names of the sealed class a string-backed enum lowers to (#1284) — ONE authority read
/// by both the emitter (<c>GenerateStringEnumClass</c>) and the SPY0525 enclosing-type walk
/// (<c>CodeGenInfoComputer</c>, #1871), so the refusal checks exactly the identifiers the class
/// declares. A C# class may not declare a member named like itself (CS0542), so every name here —
/// the synthesized members and each member's singleton field — is a name the enum must not emit as.
/// An int-backed enum is a real C# <c>enum</c>, whose members MAY share the enum's name; it has no
/// entry here.
/// </summary>
internal static class StringEnumShape
{
    /// <summary>The member-name property (<c>.name</c>).</summary>
    public const string NameProperty = "Name";

    /// <summary>The backing-string property (<c>.value</c>).</summary>
    public const string ValueProperty = "Value";

    /// <summary>The static all-members list iteration reads in place of <c>Enum.GetValues&lt;T&gt;()</c>.</summary>
    public const string ValuesList = "Values";

    /// <summary>The <c>ToString()</c> override and the <c>IFormattable.ToString(format, provider)</c> member.</summary>
    public const string ToStringMethod = "ToString";

    /// <summary>
    /// The <c>Sharpy.IRepr.Repr()</c> member — python's <c>repr</c>, <c>&lt;Mood.HAPPY: 'h'&gt;</c> (#2007).
    /// Implemented EXPLICITLY, so it adds no member named <c>Repr</c> to the class: a member or an enum
    /// spelled <c>repr</c>/<c>Repr</c> stays legal (python accepts both), and it is deliberately NOT in
    /// <see cref="SynthesizedMembers"/>.
    /// </summary>
    public const string ReprMethod = "Repr";

    /// <summary>Every member the lowering synthesizes whatever the enum declares.</summary>
    public static readonly string[] SynthesizedMembers = { NameProperty, ValueProperty, ValuesList, ToStringMethod };

    /// <summary>
    /// The C# name of the singleton field an UNESCAPED declared member compiles to — read only through
    /// <see cref="NameCasing.ResolveEnumMember"/>, which applies the declaration's escape (#2037).
    /// </summary>
    public static string MemberFieldName(string memberName)
        => NameMangler.Transform(memberName, NameContext.Constant);
}
