using System;
using System.Collections.Generic;
using System.Reflection;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Classifies a member name written on a Sharpy BUILTIN receiver (<c>list</c>, <c>dict</c>,
/// <c>set</c>, <c>frozenset</c>, <c>frozendict</c>, <c>array</c>, <c>str</c>, <c>bytes</c>) by which
/// vocabulary the spelling belongs to (R-AP, #1851). The wrapper's .NET surface leaks otherwise: a
/// PascalCase <c>xs.Count</c> binds <c>Sharpy.List&lt;T&gt;.Count(T)</c> as a method group and prints
/// a <c>System.Func</c>, and <c>d.Keys</c> prints an internal view type — silent wrong output rather
/// than a diagnostic.
///
/// <para>
/// A Sharpy-surface name (<c>xs.count(3)</c>, <c>d.keys()</c>) never reaches this classifier: it
/// resolves against the registry symbol earlier. What reaches here is everything the registry did NOT
/// answer, split four ways:
/// <list type="bullet">
/// <item><b>Escaped</b> — a backtick spelling (<c>xs.`Length`</c>) deliberately reaches the wrapper's
/// CLR member verbatim; the classifier does not refuse it.</item>
/// <item><b>ClrSpelling</b> — a verbatim PascalCase CLR member of the wrapper (<c>Count</c>,
/// <c>Length</c>, <c>Keys</c>, <c>Add</c>). Refused by name (SPY0203) with the Sharpy-spelling
/// <see cref="Steer"/>.</item>
/// <item><b>ReverseMangledClr</b> — the reverse-mangled snake spelling of a CLR member
/// (<c>s.length</c>, <c>s.to_upper()</c>). NOT refused: <c>bcl_member_on_builtin_receiver_typed</c>
/// pins it as typed. The column is named so widening R-AP later is a one-line change.</item>
/// <item><b>Absent</b> — neither spelling matches any wrapper member; the caller's own absence proof
/// (or the extension seam) answers it.</item>
/// </list>
/// </para>
///
/// <para>
/// Reflection lives here in Semantic (never the emitter, Critical Rule 2) — the same place
/// <c>ClrMemberTypeResolver</c> reflects. The <c>tuple</c> receiver is deliberately NOT a caller of
/// this classifier: <c>t.item1</c> / <c>t.Item1</c> stay typed from the element types (#1783).
/// </para>
/// </summary>
internal static class SharpyReceiverSpelling
{
    internal enum Spelling
    {
        /// <summary>A backtick escape — resolve verbatim against the wrapper's CLR member.</summary>
        Escaped,

        /// <summary>A verbatim PascalCase CLR spelling of a wrapper member — refused (SPY0203).</summary>
        ClrSpelling,

        /// <summary>The reverse-mangled snake spelling of a CLR member — typed as today.</summary>
        ReverseMangledClr,

        /// <summary>No wrapper member matches either spelling.</summary>
        Absent,
    }

    /// <summary>
    /// Classifies <paramref name="memberName"/> against <paramref name="wrapperClrType"/>'s public
    /// instance surface. A verbatim match beats a reverse-mangled one, so a name that is both a CLR
    /// spelling and (coincidentally) the reverse-mangle of another member is refused.
    /// </summary>
    internal static Spelling Classify(string memberName, bool isBacktickEscaped, Type wrapperClrType)
    {
        if (isBacktickEscaped)
            return Spelling.Escaped;

        var reverseMatch = false;
        foreach (var (clrName, context) in EnumerateInstanceMembers(wrapperClrType))
        {
            if (clrName == memberName)
                return Spelling.ClrSpelling;

            if (!reverseMatch && NameMangler.ToSharpyName(clrName, context) == memberName)
                reverseMatch = true;
        }

        return reverseMatch ? Spelling.ReverseMangledClr : Spelling.Absent;
    }

    /// <summary>
    /// The Sharpy spelling a reader should reach for instead of the refused CLR one, or null when
    /// none is known (the refusal then only names the member). One table, keyed on the CLR member
    /// name: sized-receiver length/count is <c>len(x)</c>, the dict views are <c>keys()/values()</c>,
    /// and the CLR collection verbs invert <see cref="NameMangler.ClrCollectionVerbMap"/>.
    /// </summary>
    internal static string? Steer(string clrMemberName, string receiverExpr)
    {
        switch (clrMemberName)
        {
            case "Count":
            case "Length":
                return $"len({receiverExpr})";
            case "Keys":
                return $"{receiverExpr}.keys()";
            case "Values":
                return $"{receiverExpr}.values()";
        }

        foreach (var verb in NameMangler.ClrCollectionVerbMap)
        {
            if (verb.Value == clrMemberName)
                return $"{receiverExpr}.{verb.Key}(...)";
        }

        return null;
    }

    private static IEnumerable<(string Name, ReverseNameContext Context)> EnumerateInstanceMembers(Type t)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        foreach (var method in t.GetMethods(flags))
        {
            if (!method.IsSpecialName)
                yield return (method.Name, ReverseNameContext.Method);
        }

        foreach (var property in t.GetProperties(flags))
        {
            if (property.GetIndexParameters().Length == 0)
                yield return (property.Name, ReverseNameContext.Property);
        }

        foreach (var field in t.GetFields(flags))
            yield return (field.Name, ReverseNameContext.Property);
    }
}
