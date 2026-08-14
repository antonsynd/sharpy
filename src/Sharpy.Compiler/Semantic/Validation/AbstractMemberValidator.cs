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
        // Resolved through the enclosing type's NestedTypes, not by bare name (#1461). The bare
        // `SymbolTable.LookupType(classDef.Name)` this replaces returned null for every NESTED
        // class, so the `case ClassDef nested` recursion below reached the class and then took the
        // early return — the refusal was dead for exactly the shape that needs it, and a nested
        // @abstract member in a non-abstract class produced CS0513 behind SPY0908 where the
        // top-level twin drew a clean SPY0493. The regression arrived with 8fac96434, which dropped
        // the TypeChecker's duplicate SPY0247 arm (that arm saved and restored `_currentClass`, so
        // it reached nested classes) and left this validator owning the rule.
        var typeSymbol = _context.LookupDeclaredType(classDef, classDef.Name);
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
