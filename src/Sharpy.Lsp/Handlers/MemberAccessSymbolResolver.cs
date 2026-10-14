using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// The ONE resolution of a <see cref="MemberAccess"/> under the cursor to the symbol it names, shared
/// by go-to-definition, go-to-implementation and hover (#2242, plan-6ce1a5 D7).
/// </summary>
/// <remarks>
/// <para>
/// Every step reads a fact the checker recorded where it bound the spelling; none re-derives a
/// binding rule. In order:
/// </para>
/// <list type="number">
///   <item><see cref="ISemanticQuery.GetMemberAccessResolution"/> — a static, const or enum member
///     the checker resolved on a TYPE receiver (<c>Color.RED</c>, <c>Config.MAX</c>).</item>
///   <item><see cref="ISemanticQuery.GetModuleMemberResolution"/> — the export a MODULE-qualified
///     spelling bound (<c>lib.build</c>, <c>l.build</c> after <c>import lib as l</c>,
///     <c>pkg.lib.build</c>, <c>lib.Widget</c>), recorded after the checker's #1328 escape-identity
///     check and its .NET PascalCase fallback. Re-deriving it here
///     (<c>Lookup(receiver) as ModuleSymbol → Exports[member]</c>) would be a second copy of both
///     rules that drifts from the checker's (#1933) — so it is read, never recomputed. This is the
///     bare path re-entered: <c>from lib import build</c> → <c>build</c> navigates through the
///     identifier's recorded symbol, and <c>lib.build</c> through this one.</item>
///   <item>The enclosing <see cref="FunctionCall"/>'s <see cref="ISemanticQuery.GetCallTarget"/>
///     when the access is that call's <see cref="FunctionCall.Function"/> (<c>items.count(2)</c>,
///     <c>w.render()</c>) — the overload the checker selected.</item>
///   <item>A user-defined receiver's own method or field named by the member
///     (<c>w.render</c> as a value, <c>w.label</c>).</item>
/// </list>
/// <para>
/// The two private <c>ResolveFromMemberAccess</c> copies Definition and Implementation carried read
/// only the last step, so <c>lib.build</c> — a <see cref="ModuleType"/> receiver — answered nothing
/// on a compiled unit and an extracted module alike, while hover answered the call spelling only
/// through step 3 and the value spelling <c>f = lib.build</c> nowhere.
/// </para>
/// </remarks>
internal static class MemberAccessSymbolResolver
{
    /// <summary>
    /// The symbol <paramref name="memberAccess"/> names, or null when no recorded fact names one.
    /// </summary>
    /// <param name="query">The semantic facts of the file the access is in.</param>
    /// <param name="memberAccess">The access under the cursor.</param>
    /// <param name="enclosingCall">The innermost call around the cursor, if any; consulted only
    /// when <paramref name="memberAccess"/> is its callee.</param>
    public static Symbol? Resolve(ISemanticQuery query, MemberAccess memberAccess, FunctionCall? enclosingCall)
    {
        var typeMember = query.GetMemberAccessResolution(memberAccess);
        if (typeMember != null)
            return typeMember.Value.Member;

        var moduleMember = query.GetModuleMemberResolution(memberAccess);
        if (moduleMember != null)
            return moduleMember.Value.Member;

        if (enclosingCall != null && ReferenceEquals(enclosingCall.Function, memberAccess))
        {
            var target = query.GetCallTarget(enclosingCall);
            if (target != null)
                return target;
        }

        if (query.GetEffectiveType(memberAccess.Object) is UserDefinedType { Symbol: { } owner })
        {
            var memberName = memberAccess.Member;

            var method = owner.Methods.Find(m =>
                string.Equals(m.Name, memberName, StringComparison.Ordinal));
            if (method != null)
                return method;

            var field = owner.Fields.Find(f =>
                string.Equals(f.Name, memberName, StringComparison.Ordinal));
            if (field != null)
                return field;
        }

        return null;
    }
}
