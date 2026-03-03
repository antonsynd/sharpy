using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates event declarations in classes and structs.
///
/// Rules:
/// 1. Function-style event must have both add and remove accessors (SPY0420)
/// 2. Event cannot have the same name as a field (SPY0421)
/// 3. Event cannot have the same name as a method (SPY0422)
/// 4. @abstract event must have ellipsis body (SPY0423)
/// 5. @final cannot be combined with @abstract or @virtual (SPY0410, reuses property code)
/// 6. @override event must have matching virtual/abstract base event (future)
/// </summary>
internal class EventValidator : SemanticValidatorBase
{
    public override string Name => "EventValidator";
    public override int Order => 412; // Between PropertyValidator (410) and VarianceValidator (415)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting event validation");

        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateTypeBody(classDef.Name, classDef.Body);
                    break;
                case StructDef structDef:
                    ValidateTypeBody(structDef.Name, structDef.Body);
                    break;
                case InterfaceDef interfaceDef:
                    ValidateInterfaceEvents(interfaceDef.Name, interfaceDef.Body);
                    break;
            }
        }
    }

    private void ValidateTypeBody(string typeName, IReadOnlyList<Statement> body)
    {
        // Collect fields, methods, and events from the body
        var fieldNames = new HashSet<string>();
        var methodNames = new HashSet<string>();
        var eventDefs = new List<EventDef>();

        foreach (var member in body)
        {
            switch (member)
            {
                case VariableDeclaration varDecl:
                    fieldNames.Add(varDecl.Name);
                    break;
                case FunctionDef funcDef:
                    methodNames.Add(funcDef.Name);
                    break;
                case EventDef eventDef:
                    eventDefs.Add(eventDef);
                    break;
            }
        }

        // Group events by name to detect unpaired accessors
        var eventGroups = new Dictionary<string, List<EventDef>>();
        foreach (var eventDef in eventDefs)
        {
            if (!eventGroups.TryGetValue(eventDef.Name, out var group))
            {
                group = new List<EventDef>();
                eventGroups[eventDef.Name] = group;
            }
            group.Add(eventDef);
        }

        // Check each event definition
        foreach (var eventDef in eventDefs)
        {
            ValidateEventAgainstFields(typeName, eventDef, fieldNames);
            ValidateEventAgainstMethods(typeName, eventDef, methodNames);
            ValidateAbstractEventBody(typeName, eventDef);
            ValidateFinalNotWithAbstractOrVirtual(typeName, eventDef);
        }

        // Check for unpaired function-style accessors
        foreach (var (eventName, group) in eventGroups)
        {
            ValidateUnpairedAccessors(typeName, eventName, group);
        }
    }

    private void ValidateInterfaceEvents(string typeName, IReadOnlyList<Statement> body)
    {
        // Interface events are simpler — only auto-events allowed
        foreach (var member in body)
        {
            if (member is EventDef eventDef)
            {
                ValidateAbstractEventBody(typeName, eventDef);
            }
        }
    }

    /// <summary>
    /// Rule 1: Function-style events must have both add and remove accessors.
    /// </summary>
    private void ValidateUnpairedAccessors(string typeName, string eventName, List<EventDef> group)
    {
        // Only applies to function-style events
        if (!group.Any(e => e.IsFunctionStyle))
            return;

        bool hasAdd = group.Any(e => e.Accessor == EventAccessor.Add);
        bool hasRemove = group.Any(e => e.Accessor == EventAccessor.Remove);

        if (hasAdd && !hasRemove)
        {
            var addDef = group.First(e => e.Accessor == EventAccessor.Add);
            AddError(_context,
                $"Event '{eventName}' in '{typeName}' has an 'event add' accessor but no matching 'event remove'",
                addDef.LineStart, addDef.ColumnStart,
                code: DiagnosticCodes.Validation.UnpairedEventAccessor,
                span: addDef.Span);
        }
        else if (hasRemove && !hasAdd)
        {
            var removeDef = group.First(e => e.Accessor == EventAccessor.Remove);
            AddError(_context,
                $"Event '{eventName}' in '{typeName}' has an 'event remove' accessor but no matching 'event add'",
                removeDef.LineStart, removeDef.ColumnStart,
                code: DiagnosticCodes.Validation.UnpairedEventAccessor,
                span: removeDef.Span);
        }
    }

    /// <summary>
    /// Rule 2: Event cannot share a name with a field.
    /// </summary>
    private void ValidateEventAgainstFields(string typeName, EventDef eventDef, HashSet<string> fieldNames)
    {
        if (fieldNames.Contains(eventDef.Name))
        {
            AddError(_context,
                $"Event '{eventDef.Name}' in '{typeName}' conflicts with a field of the same name",
                eventDef.LineStart, eventDef.ColumnStart,
                code: DiagnosticCodes.Validation.EventFieldNameConflict,
                span: eventDef.Span);
        }
    }

    /// <summary>
    /// Rule 3: Event cannot share a name with a method.
    /// </summary>
    private void ValidateEventAgainstMethods(string typeName, EventDef eventDef, HashSet<string> methodNames)
    {
        if (methodNames.Contains(eventDef.Name))
        {
            AddError(_context,
                $"Event '{eventDef.Name}' in '{typeName}' conflicts with a method of the same name",
                eventDef.LineStart, eventDef.ColumnStart,
                code: DiagnosticCodes.Validation.EventMethodNameConflict,
                span: eventDef.Span);
        }
    }

    /// <summary>
    /// Rule 4: @abstract event with function-style body must have ellipsis/pass.
    /// Auto-events that are abstract have no body, so this only applies to function-style.
    /// </summary>
    private void ValidateAbstractEventBody(string typeName, EventDef eventDef)
    {
        bool isAbstract = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        if (!isAbstract || !eventDef.IsFunctionStyle)
            return;

        bool isEllipsisBody = eventDef.Body.Length == 1
            && (eventDef.Body[0] is PassStatement
                || (eventDef.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral));

        if (!isEllipsisBody)
        {
            AddError(_context,
                $"@abstract event '{eventDef.Name}' in '{typeName}' must have '...' (ellipsis) body",
                eventDef.LineStart, eventDef.ColumnStart,
                code: DiagnosticCodes.Validation.AbstractEventWithBody,
                span: eventDef.Span);
        }
    }

    /// <summary>
    /// Rule 5: @final cannot be combined with @abstract or @virtual.
    /// </summary>
    private void ValidateFinalNotWithAbstractOrVirtual(string typeName, EventDef eventDef)
    {
        bool isFinal = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Final);
        if (!isFinal)
            return;

        bool isAbstract = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool isVirtual = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual);

        if (isAbstract)
        {
            AddError(_context,
                $"Event '{eventDef.Name}' in '{typeName}' cannot be both @final and @abstract",
                eventDef.LineStart, eventDef.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
                span: eventDef.Span);
        }

        if (isVirtual)
        {
            AddError(_context,
                $"Event '{eventDef.Name}' in '{typeName}' cannot be both @final and @virtual",
                eventDef.LineStart, eventDef.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
                span: eventDef.Span);
        }
    }
}
