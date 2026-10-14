using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that symbols imported from deferred circular imports (stubs) are
/// used only in type annotation positions. Runtime usage (constructor calls,
/// static method access, isinstance checks) emits SPY0308. Base class references emit SPY0309.
///
/// Type annotations use <see cref="TypeAnnotation"/> nodes (not <see cref="Identifier"/>),
/// so any <see cref="Identifier"/> matching a deferred-cycle symbol is runtime usage.
/// Base class references in ClassDef/StructDef use TypeAnnotation and are checked separately.
/// </summary>
internal class CircularImportUsageValidator : ValidatingAstWalker
{
    public override string Name => "CircularImportUsageValidator";
    public override int Order => 52;

    private IReadOnlySet<string>? _deferredSymbols;

    public override void Validate(Module module, SemanticContext context)
    {
        _deferredSymbols = context.DeferredCycleSymbols;
        _genericReferenceNames.Clear();
        if (_deferredSymbols == null || _deferredSymbols.Count == 0)
            return;

        // Only enforce annotation-only usage in files that are part of the cycle
        if (context.DeferredCycleFiles != null && context.CurrentFilePath != null)
        {
            var normalizedPath = PathNormalizer.Normalize(context.CurrentFilePath);
            if (!context.DeferredCycleFiles.Contains(normalizedPath))
                return;
        }

        base.Validate(module, context);
    }

    /// <summary>
    /// Identifiers of a generic reference this validator does not judge: the bare callee
    /// (<c>Box</c> in <c>Box[int]()</c>) and every name in the type-argument list (<c>B</c> in
    /// <c>ident[B](…)</c>).
    /// </summary>
    /// <remarks>
    /// Those positions are bound by the generic-reference resolver, never by <c>CheckIdentifier</c>,
    /// so until #2242 recorded their identifier symbols for the editor this validator never saw a
    /// symbol there and never fired. Recording a navigation fact must not change a verdict, so they
    /// stay unjudged: whether constructing a deferred-cycle type through its generic spelling is
    /// runtime usage is a rule of its own (measured @ c245ece62: <c>Box[int]()</c> on a deferred
    /// stub compiles and runs while <c>Box()</c> is SPY0308 — the generic-spelling cell of #2219),
    /// and a type argument is a type position, which this validator admits.
    /// </remarks>
    private readonly HashSet<Identifier> _genericReferenceNames = new(ReferenceEqualityComparer.Instance);

    public override void VisitIndexAccess(IndexAccess node)
    {
        if (Context.SemanticInfo.GetGenericReference(node) != null)
        {
            if (node.Object is Identifier callee)
                _genericReferenceNames.Add(callee);
            CollectTypeArgumentNames(node.Index);
        }

        base.VisitIndexAccess(node);
    }

    private void CollectTypeArgumentNames(Node node)
    {
        if (node is Identifier name)
            _genericReferenceNames.Add(name);
        foreach (var child in node.GetChildNodes())
        {
            if (child != null)
                CollectTypeArgumentNames(child);
        }
    }

    public override void VisitIdentifier(Identifier node)
    {
        if (_deferredSymbols?.Contains(node.Name) == true && !_genericReferenceNames.Contains(node))
        {
            var symbol = Context.SemanticInfo.GetIdentifierSymbol(node);
            if (symbol is TypeSymbol)
            {
                AddError(
                    $"Circular import of '{node.Name}' requires type-annotation-only usage, " +
                    $"but '{node.Name}' is used at runtime (line {node.LineStart}). " +
                    $"Move '{node.Name}' to a non-circular import or restructure to break the cycle.",
                    node.LineStart, node.ColumnStart,
                    code: DiagnosticCodes.Semantic.CircularImportRuntimeUsage,
                    span: node.Span);
            }
        }

        base.VisitIdentifier(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        CheckBaseClassesForCircularImport(node.Name, node.BaseClasses, node.LineStart, node.ColumnStart, node.Span);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        CheckBaseClassesForCircularImport(node.Name, node.BaseClasses, node.LineStart, node.ColumnStart, node.Span);
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        CheckBaseClassesForCircularImport(node.Name, node.BaseInterfaces, node.LineStart, node.ColumnStart, node.Span);
        base.VisitInterfaceDef(node);
    }

    private void CheckBaseClassesForCircularImport(
        string typeName, IReadOnlyList<TypeAnnotation> bases,
        int? line, int? column, Text.TextSpan? span)
    {
        foreach (var baseClass in bases)
        {
            if (_deferredSymbols?.Contains(baseClass.Name) == true)
            {
                AddError(
                    $"Cannot use '{baseClass.Name}' from a circular import as a base type for '{typeName}'. " +
                    $"Base types require full type information at compile time. " +
                    $"Move '{baseClass.Name}' to a non-circular import or restructure to break the cycle.",
                    line, column,
                    code: DiagnosticCodes.Semantic.CircularImportBaseClass,
                    span: span);
            }
        }
    }
}
