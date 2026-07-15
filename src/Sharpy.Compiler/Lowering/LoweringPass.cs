using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Builds the lowering IR from the type-checked AST. A single deterministic top-down walk visits
/// each AST node exactly once and produces exactly one IR node per lowered construct, keyed back to
/// the originating AST node in the <see cref="IrCompilation.Index"/> (Design Decision 1a).
/// </summary>
/// <remarks>
/// <para>
/// The pass runs <b>once per project, after <c>SemanticInfo.MergeFrom</c></b> (Design Decision 2),
/// so it observes the merged, project-level semantic facts rather than a per-file slice. It reads
/// each materialized fact — never re-deriving one — so no new inference enters code generation
/// (emitter purity, B2, is preserved).
/// </para>
/// <para>
/// In E2 Phase 1 the pass emits only totality wrappers
/// (<see cref="IrOpaqueExpression"/>/<see cref="IrOpaqueStatement"/>); the typed nodes exist in the
/// vocabulary and later migration phases switch individual constructs onto them. The output is not
/// yet read by any backend.
/// </para>
/// </remarks>
internal sealed class LoweringPass
{
    /// <summary>
    /// Lowers every module to its IR form and returns the whole-project
    /// <see cref="IrCompilation"/> together with the AST-to-IR index.
    /// </summary>
    /// <param name="modules">The modules to lower, each paired with its source file path. The
    /// caller supplies these in a deterministic (source-path-ordered) sequence so the resulting
    /// module order does not depend on enumeration order.</param>
    /// <param name="semanticInfo">The merged, project-level semantic facts.</param>
    /// <param name="symbolTable">The global symbol table. Consumed by later migration phases; the
    /// v1 opaque lowering does not need it.</param>
    public IrCompilation Lower(
        IReadOnlyList<(string FilePath, Module Ast)> modules,
        SemanticInfo semanticInfo,
        SymbolTable symbolTable)
    {
        // Reference-keyed: AST nodes are records with value equality, but the index must
        // distinguish structurally-identical nodes at different positions (same discipline as
        // SemanticInfo's side-tables).
        var index = new Dictionary<Node, IrNode>(ReferenceEqualityComparer.Instance);

        var irModules = ImmutableArray.CreateBuilder<IrModule>(modules.Count);
        foreach (var (filePath, ast) in modules)
        {
            var body = ImmutableArray.CreateBuilder<IrStatement>(ast.Body.Length);
            foreach (var statement in ast.Body)
                body.Add(LowerStatement(statement, semanticInfo, index));
            irModules.Add(new IrModule(filePath, body.ToImmutable()));
        }

        return new IrCompilation(irModules.ToImmutable(), new IrIndex(index));
    }

    private static IrStatement LowerStatement(Statement statement, SemanticInfo semanticInfo, Dictionary<Node, IrNode> index)
    {
        var children = LowerChildren(statement, semanticInfo, index);
        var ir = new IrOpaqueStatement(statement, SpanOf(statement), children);
        index[statement] = ir;
        return ir;
    }

    private static IrExpression LowerExpression(Expression expression, SemanticInfo semanticInfo, Dictionary<Node, IrNode> index)
    {
        var children = LowerChildren(expression, semanticInfo, index);
        var ir = new IrOpaqueExpression(expression, semanticInfo.GetExpressionType(expression), SpanOf(expression), children);
        index[expression] = ir;
        return ir;
    }

    private static ImmutableArray<IrNode> LowerChildren(Node node, SemanticInfo semanticInfo, Dictionary<Node, IrNode> index)
    {
        ImmutableArray<IrNode>.Builder? builder = null;
        foreach (var child in node.GetChildNodes())
        {
            builder ??= ImmutableArray.CreateBuilder<IrNode>();
            switch (child)
            {
                case Statement statement:
                    builder.Add(LowerStatement(statement, semanticInfo, index));
                    break;
                case Expression expression:
                    builder.Add(LowerExpression(expression, semanticInfo, index));
                    break;
                default:
                    // Structural helper nodes (comprehension clauses, subscript dimensions, ...)
                    // are neither Expression nor Statement and get no IR node of their own in v1;
                    // flatten through them so their Expression/Statement descendants still lower to
                    // exactly one IR node each.
                    builder.AddRange(LowerChildren(child, semanticInfo, index));
                    break;
            }
        }

        return builder?.ToImmutable() ?? ImmutableArray<IrNode>.Empty;
    }

    private static TextSpan SpanOf(Node node) => node.Span ?? TextSpan.Empty;
}

/// <summary>
/// The lowering IR for an entire project: one <see cref="IrModule"/> per source file, plus the
/// <see cref="IrIndex"/> mapping each originating AST node back to its IR node.
/// </summary>
/// <param name="Modules">The per-file IR modules, in source-path order.</param>
/// <param name="Index">The AST-to-IR index (transitional scaffolding, Design Decision 1a).</param>
internal sealed record IrCompilation(ImmutableArray<IrModule> Modules, IrIndex Index);

/// <summary>
/// The lowering IR for a single source file: the lowered top-level statements.
/// </summary>
/// <param name="FilePath">The source file this module was lowered from.</param>
/// <param name="Body">The lowered top-level statements, in source order.</param>
internal sealed record IrModule(string FilePath, ImmutableArray<IrStatement> Body);

/// <summary>
/// A reference-keyed map from AST node to the IR node it lowered to. This is transitional
/// scaffolding: while the emitter still dispatches over the AST, a migrated fact is read by looking
/// the AST node up here and reading the IR node's field (Design Decision 1a). It is owned by the
/// <c>Lowering</c> component, built once post-merge, and never itself merged.
/// </summary>
internal sealed class IrIndex : IReadOnlyDictionary<Node, IrNode>
{
    private readonly IReadOnlyDictionary<Node, IrNode> _map;

    public IrIndex(IReadOnlyDictionary<Node, IrNode> map) => _map = map;

    /// <inheritdoc/>
    public IrNode this[Node key] => _map[key];

    /// <inheritdoc/>
    public IEnumerable<Node> Keys => _map.Keys;

    /// <inheritdoc/>
    public IEnumerable<IrNode> Values => _map.Values;

    /// <inheritdoc/>
    public int Count => _map.Count;

    /// <inheritdoc/>
    public bool ContainsKey(Node key) => _map.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(Node key, [MaybeNullWhen(false)] out IrNode value) => _map.TryGetValue(key, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<Node, IrNode>> GetEnumerator() => _map.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
}
