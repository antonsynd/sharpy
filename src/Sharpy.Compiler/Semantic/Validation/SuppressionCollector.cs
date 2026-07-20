using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// A single scoped-suppression region gathered from one <c>@suppress(...)</c> decorator: the
/// suppressible diagnostic codes it silences, the span of the annotated declaration or statement
/// (which covers its body), and the decorator's own location (used to anchor the SPY0481
/// "unused suppression" warning). <see cref="DroppedAny"/> is flipped by the filter pass.
/// </summary>
internal sealed class SuppressionRegion
{
    public IReadOnlyList<string> Codes { get; init; } = System.Array.Empty<string>();
    public TextSpan? Span { get; init; }
    public int LineStart { get; init; }
    public int LineEnd { get; init; }
    public TextSpan? DecoratorSpan { get; init; }
    public int DecoratorLine { get; init; }
    public int DecoratorColumn { get; init; }

    /// <summary>True once this region has silenced at least one diagnostic.</summary>
    public bool DroppedAny { get; set; }

    /// <summary>
    /// True when <paramref name="diagnostic"/> lies within this region: precise char-offset span
    /// containment when both the region and the diagnostic carry a span, otherwise a line-range
    /// fallback for diagnostics anchored only by line/column.
    /// </summary>
    public bool Contains(CompilerDiagnostic diagnostic)
    {
        if (Span is TextSpan region && diagnostic.Span is TextSpan diagnosticSpan)
            return diagnosticSpan.Start >= region.Start && diagnosticSpan.Start < region.End;

        if (diagnostic.Line is int line)
            return line >= LineStart && line <= LineEnd;

        return false;
    }
}

/// <summary>
/// Walks a module and collects the <see cref="SuppressionRegion"/>s declared by <c>@suppress</c>
/// decorators. Only suppressible codes (Warning/Hint/Info, per <see cref="DiagnosticCodeCatalog"/>)
/// are recorded, so a suppressor whose codes are all invalid — already flagged SPY0482 by the
/// DecoratorValidator — never yields a region and therefore never also trips SPY0481.
/// </summary>
internal sealed class SuppressionCollector : AstVisitor
{
    private readonly List<SuppressionRegion> _regions = new();

    private SuppressionCollector() { }

    public static IReadOnlyList<SuppressionRegion> Collect(Module module)
    {
        var collector = new SuppressionCollector();
        collector.Visit(module);
        return collector._regions;
    }

    private void AddRegionsFrom(ImmutableArray<Decorator> decorators, Node node)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name != DecoratorNames.Suppress)
                continue;

            var codes = ExtractSuppressibleCodes(decorator);
            if (codes.Count == 0)
                continue;

            _regions.Add(new SuppressionRegion
            {
                Codes = codes,
                Span = node.Span,
                LineStart = node.LineStart,
                LineEnd = node.LineEnd,
                DecoratorSpan = decorator.Span,
                DecoratorLine = decorator.LineStart,
                DecoratorColumn = decorator.ColumnStart,
            });
        }
    }

    private static IReadOnlyList<string> ExtractSuppressibleCodes(Decorator decorator)
    {
        List<string>? codes = null;
        foreach (var argument in decorator.Arguments)
        {
            if (argument is StringLiteral literal
                && DiagnosticCodeCatalog.IsSuppressible(literal.Value))
            {
                codes ??= new List<string>();
                if (!codes.Contains(literal.Value))
                    codes.Add(literal.Value);
            }
        }

        return (IReadOnlyList<string>?)codes ?? System.Array.Empty<string>();
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitFunctionDef(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitInterfaceDef(node);
    }

    public override void VisitEnumDef(EnumDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitEnumDef(node);
    }

    public override void VisitUnionDef(UnionDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitUnionDef(node);
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitEventDef(node);
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        AddRegionsFrom(node.Decorators, node);
        base.VisitVariableDeclaration(node);
    }
}
