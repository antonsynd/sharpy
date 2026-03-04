using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Registry that maps dunder method names to codegen handler delegates,
/// replacing the if-else chain in <see cref="RoslynEmitter"/>'s class member generation.
/// </summary>
/// <remarks>
/// <para>
/// Each handler receives a <see cref="FunctionDef"/> and a <see cref="DunderCodeGenContext"/>
/// and returns zero or more <see cref="MemberDeclarationSyntax"/> nodes.
/// </para>
/// <para>
/// <b>Special cases not handled by this registry:</b>
/// <list type="bullet">
///   <item><c>__init__</c> — requires collecting multiple definitions for constructor overloads,
///     handled separately in the class member generation loop.</item>
///   <item><c>__getitem__</c> / <c>__setitem__</c> — collected and combined into a single C# indexer,
///     handled separately.</item>
///   <item>Complementary operators (e.g., <c>operator false</c> when <c>__bool__</c> is defined,
///     <c>operator \!=</c> when only <c>__eq__</c> is defined) — generated after the main loop.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class DunderCodeGenRegistry
{
    /// <summary>
    /// Context passed to dunder codegen handlers, providing access to class-level information
    /// needed for code generation.
    /// </summary>
    /// <param name="ClassName">The mangled C# class name.</param>
    /// <param name="DundersPresent">Set of all dunder method names present in the class body.</param>
    /// <param name="Body">The full class body statements (for cross-referencing other dunders).</param>
    internal sealed record DunderCodeGenContext(
        string ClassName,
        IReadOnlySet<string> DundersPresent,
        IReadOnlyList<Statement> Body);

    /// <summary>
    /// Delegate type for dunder codegen handlers.
    /// Returns an enumerable of member declarations (may be empty, one, or multiple).
    /// </summary>
    internal delegate IEnumerable<MemberDeclarationSyntax> DunderHandler(
        FunctionDef funcDef,
        DunderCodeGenContext context);

    private readonly Dictionary<string, DunderHandler> _handlers = new();

    /// <summary>
    /// Registers a handler for the specified dunder method name.
    /// </summary>
    public void Register(string dunderName, DunderHandler handler)
    {
        _handlers[dunderName] = handler;
    }

    /// <summary>
    /// Attempts to retrieve the handler for the given dunder method name.
    /// </summary>
    /// <param name="dunderName">The dunder method name (e.g., <c>__len__</c>).</param>
    /// <param name="handler">The handler if found; <c>null</c> otherwise.</param>
    /// <returns><c>true</c> if a handler was found; <c>false</c> for dunders that should
    /// fall through to the default path (operator synthesis or regular method generation).</returns>
    public bool TryGetHandler(string dunderName, out DunderHandler? handler)
    {
        return _handlers.TryGetValue(dunderName, out handler);
    }

    /// <summary>
    /// Returns whether a handler is registered for the given dunder name.
    /// </summary>
    public bool HasHandler(string dunderName) => _handlers.ContainsKey(dunderName);
}
