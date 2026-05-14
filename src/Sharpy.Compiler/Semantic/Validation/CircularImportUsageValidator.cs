using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that symbols imported from deferred circular imports (stubs) are
/// used only in type annotation positions. Runtime usage (constructor calls,
/// static method access, base class references, isinstance checks) emits SPY0302.
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
        if (_deferredSymbols == null || _deferredSymbols.Count == 0)
            return;

        // Only enforce annotation-only usage in files that are part of the cycle
        if (context.DeferredCycleFiles != null && context.CurrentFilePath != null)
        {
            var normalizedPath = Sharpy.Compiler.Utilities.PathNormalizer.Normalize(context.CurrentFilePath);
            if (!context.DeferredCycleFiles.Contains(normalizedPath))
                return;
        }

        base.Validate(module, context);
    }

    public override void VisitIdentifier(Identifier node)
    {
        if (_deferredSymbols!.Contains(node.Name))
        {
            var symbol = Context.SemanticInfo.GetIdentifierSymbol(node);
            if (symbol is TypeSymbol)
            {
                AddError(
                    $"Circular import of '{node.Name}' requires type-annotation-only usage, " +
                    $"but '{node.Name}' is used at runtime (line {node.LineStart}). " +
                    $"Move '{node.Name}' to a non-circular import or restructure to break the cycle.",
                    node.LineStart, node.ColumnStart,
                    code: DiagnosticCodes.Semantic.CircularImport,
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
            if (_deferredSymbols!.Contains(baseClass.Name))
            {
                AddError(
                    $"Cannot use '{baseClass.Name}' from a circular import as a base type for '{typeName}'. " +
                    $"Base types require full type information at compile time. " +
                    $"Move '{baseClass.Name}' to a non-circular import or restructure to break the cycle.",
                    line, column,
                    code: DiagnosticCodes.Semantic.CircularImport,
                    span: span);
            }
        }
    }
}
