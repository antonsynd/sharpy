using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that @abstract members appear only in @abstract classes (#1307).
/// Covers methods, properties, events, and indexers.
/// </summary>
internal class AbstractMemberValidator : SemanticValidatorBase
{
    public override string Name => "AbstractMemberValidator";
    public override int Order => 146;

    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;

        foreach (var stmt in module.Body)
        {
            if (stmt is ClassDef classDef)
                ValidateClass(classDef);
        }
    }

    private void ValidateClass(ClassDef classDef)
    {
        var typeSymbol = _context.SymbolTable.LookupType(classDef.Name);
        if (typeSymbol == null || typeSymbol.IsAbstract)
            return;

        foreach (var member in classDef.Body)
        {
            switch (member)
            {
                case FunctionDef funcDef when HasAbstractDecorator(funcDef.Decorators):
                    Report(classDef.Name, funcDef.Name, "method", funcDef.LineStart, funcDef.ColumnStart, funcDef.Span);
                    break;
                case PropertyDef propDef when HasAbstractDecorator(propDef.Decorators):
                    Report(classDef.Name, propDef.Name, "property", propDef.LineStart, propDef.ColumnStart, propDef.Span);
                    break;
                case EventDef eventDef when HasAbstractDecorator(eventDef.Decorators):
                    Report(classDef.Name, eventDef.Name, "event", eventDef.LineStart, eventDef.ColumnStart, eventDef.Span);
                    break;
                case ClassDef nested:
                    ValidateClass(nested);
                    break;
            }
        }
    }

    private static bool HasAbstractDecorator(IEnumerable<Decorator> decorators)
        => decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract);

    private void Report(string className, string memberName, string memberKind,
        int? line, int? column, Text.TextSpan? span)
    {
        AddError(_context,
            $"@abstract {memberKind} '{memberName}' in non-abstract class '{className}' — "
            + "declare the class @abstract or remove the @abstract modifier",
            line, column,
            code: DiagnosticCodes.Validation.AbstractMemberInNonAbstractClass,
            span: span);
    }
}
