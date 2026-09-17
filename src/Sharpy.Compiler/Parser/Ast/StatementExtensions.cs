using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// The seven statement kinds that declare a type or type alias (#1729, R-I). Kept separate from
/// <c>TypeKind</c> (<c>Semantic/Symbol.cs</c>) — that enum classifies a resolved <c>TypeSymbol</c> and
/// has no case for <see cref="Alias"/>, since a <c>TypeAliasSymbol</c> is not a <c>TypeSymbol</c>; this
/// one classifies the AST statement, before any symbol exists.
/// </summary>
public enum NestedDeclarationKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Union,
    Delegate,
    Alias
}

/// <summary>
/// A type-declaring statement's shape, independent of which of the seven AST node types carries it.
/// Returned by <see cref="StatementExtensions.TryGetNestedDeclaration"/>.
/// </summary>
/// <param name="Kind">Which of the seven type-declaring statement kinds this is.</param>
/// <param name="Name">The declared name.</param>
/// <param name="Body">
/// The statement list a further nested declaration could appear inside. Empty for a kind that has no
/// such slot: <see cref="NestedDeclarationKind.Enum"/> declares <c>Members</c>, not statements (as
/// <c>NestedTypeIndex.TypeDeclarationOf</c> already treats it); <see cref="NestedDeclarationKind.Delegate"/>
/// declares a signature (<c>Parameters</c> / <c>ReturnType</c>); <see cref="NestedDeclarationKind.Alias"/>
/// declares a target type (<c>Type</c> / <c>FunctionType</c>). None of the three can legally contain a
/// nested declaration, so a caller that recurses into <c>Body</c> to find one needs no special case for
/// them — they fall out as leaves.
/// </param>
/// <param name="Decorators">
/// The statement's decorators. Empty for <see cref="NestedDeclarationKind.Delegate"/> and
/// <see cref="NestedDeclarationKind.Alias"/> — neither <c>DelegateDef</c> nor <c>TypeAlias</c> carries a
/// <c>Decorators</c> property.
/// </param>
public sealed record NestedDeclaration(
    NestedDeclarationKind Kind,
    string Name,
    ImmutableArray<Statement> Body,
    ImmutableArray<Decorator> Decorators);

/// <summary>
/// Extension methods for <see cref="Statement"/> nodes used by scan sites that walk
/// <c>module.Body</c> and type-test the statements they find.
/// </summary>
public static class StatementExtensions
{
    /// <summary>
    /// Returns the statement carried by a <see cref="DecoratedStatement"/> wrapper, or the
    /// statement itself when it is not decorated. Single-level by construction: the parser never
    /// nests a <see cref="DecoratedStatement"/> inside another, so a single unwrap is exhaustive.
    /// </summary>
    /// <remarks>
    /// Every <c>module.Body</c> scan that type-tests for a concrete statement kind (e.g.
    /// <see cref="ImportStatement"/>, <see cref="FromImportStatement"/>) must call this first so a
    /// suppress-decorated statement is classified by its inner target rather than silently skipped
    /// (#1124). <c>DecoratedImportConformanceTests</c> guards this contract.
    /// </remarks>
    public static Statement UnwrapDecorated(this Statement stmt)
        => stmt is DecoratedStatement decorated ? decorated.Statement : stmt;

    /// <summary>
    /// Classifies <paramref name="stmt"/> as one of the seven type-declaring statement kinds — the
    /// single authority a nesting-aware caller (name resolution, module loading, the nested-type
    /// index, CodeGenInfo, and the class/struct/interface member emitters) matches over instead of
    /// its own four-or-five-case switch (#1729, R-I). Returns <c>false</c> for any other statement,
    /// including a <see cref="DecoratedStatement"/> wrapper — call <see cref="UnwrapDecorated"/> first,
    /// exactly as every other kind-dispatching seam in this codebase does.
    /// </summary>
    public static bool TryGetNestedDeclaration(
        this Statement stmt, [MaybeNullWhen(false)] out NestedDeclaration declaration)
    {
        declaration = stmt switch
        {
            ClassDef classDef => new NestedDeclaration(
                NestedDeclarationKind.Class, classDef.Name, classDef.Body, classDef.Decorators),
            StructDef structDef => new NestedDeclaration(
                NestedDeclarationKind.Struct, structDef.Name, structDef.Body, structDef.Decorators),
            InterfaceDef interfaceDef => new NestedDeclaration(
                NestedDeclarationKind.Interface, interfaceDef.Name, interfaceDef.Body, interfaceDef.Decorators),
            EnumDef enumDef => new NestedDeclaration(
                NestedDeclarationKind.Enum, enumDef.Name, ImmutableArray<Statement>.Empty, enumDef.Decorators),
            UnionDef unionDef => new NestedDeclaration(
                NestedDeclarationKind.Union, unionDef.Name, unionDef.Body, unionDef.Decorators),
            DelegateDef delegateDef => new NestedDeclaration(
                NestedDeclarationKind.Delegate, delegateDef.Name, ImmutableArray<Statement>.Empty,
                ImmutableArray<Decorator>.Empty),
            TypeAlias typeAlias => new NestedDeclaration(
                NestedDeclarationKind.Alias, typeAlias.Name, ImmutableArray<Statement>.Empty,
                ImmutableArray<Decorator>.Empty),
            _ => null
        };
        return declaration != null;
    }
}
