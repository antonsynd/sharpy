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
/// 7. An accessor's parameter list must be expressible as a C# accessor's (SPY0496)
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
            ValidateTypeStatement(stmt);
        }

        // Rule 7 runs over every event regardless of what declares it — see the twin in
        // PropertyValidator. An event accessor receives exactly the one handler C# hands it as the
        // implicit `value`, so it has no argument list to vary (#1406).
        foreach (var eventDef in EnumerateAllEvents(module.Body))
            ValidateAccessorParameterShape(eventDef);
    }

    /// <summary>Every <see cref="EventDef"/> the module declares, at any nesting depth.</summary>
    private static IEnumerable<EventDef> EnumerateAllEvents(IEnumerable<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case EventDef eventDef:
                    yield return eventDef;
                    break;
                case ClassDef classDef:
                    foreach (var nested in EnumerateAllEvents(classDef.Body))
                        yield return nested;
                    break;
                case StructDef structDef:
                    foreach (var nested in EnumerateAllEvents(structDef.Body))
                        yield return nested;
                    break;
                case InterfaceDef interfaceDef:
                    foreach (var nested in EnumerateAllEvents(interfaceDef.Body))
                        yield return nested;
                    break;
            }
        }
    }

    /// <summary>
    /// Rule 7: an event accessor receives exactly one handler, as C#'s implicit <c>value</c>, so
    /// <c>self</c> plus one handler parameter is the whole expressible shape (#1406). A variadic
    /// bound as its ELEMENT type, so a body treating it as a sequence was refused with a type error
    /// about the delegate rather than about the declaration; wrapping it as an array — #1292's fix
    /// for ordinary parameters — would type-check and then emit a length call against a single
    /// delegate, because a C# event accessor has no <c>params T[]</c> backing to wrap onto.
    /// </summary>
    private void ValidateAccessorParameterShape(EventDef eventDef)
    {
        if (!eventDef.IsFunctionStyle)
            return;

        var accessorWord = eventDef.Accessor == EventAccessor.Remove ? "remove" : "add";

        foreach (var param in eventDef.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!param.IsVariadic)
                continue;

            AddError(_context,
                $"Variadic parameter '*{param.Name}' cannot appear in the '{accessorWord}' accessor of "
                + $"event '{eventDef.Name}'. An event accessor receives exactly one handler from C# "
                + "and has no argument list to vary — take a single handler parameter, and let the "
                + "caller subscribe more than once.",
                param.LineStart, param.ColumnStart,
                code: DiagnosticCodes.Validation.AccessorParameterNotExpressible,
                span: param.Span);
            return;
        }
    }

    /// <summary>
    /// Validates one type declaration and, recursively, the types NESTED inside it. Nested types were
    /// never event-validated at all before — this walked <c>module.Body</c> only — so an unpaired
    /// accessor or an abstractness disagreement inside a nested class reached codegen unreported.
    /// Mirrors <c>AbstractMemberValidator.ValidateClass</c>, which already recurses.
    /// </summary>
    /// <param name="ownerSymbol">
    /// The declaring type's symbol. Threaded rather than looked up by name because a NESTED type's
    /// symbol lives in its enclosing type's scope, where the global <c>LookupType</c> cannot see it
    /// (#1371); without this the owner would read as non-abstract and every implicit-stub verdict
    /// inside a nested type would be wrong.
    /// </param>
    /// <summary>
    /// Validates one type declaration and every type nested inside it.
    ///
    /// <para>This validator used to thread the enclosing symbol down the recursion and walk
    /// <c>NestedTypes</c> by hand — the right answer, arrived at independently, in one validator out
    /// of thirteen. It now asks <see cref="SemanticContext.LookupDeclaredType"/>, which is that same
    /// walk shared by all of them (#1461). The hand-rolled copy is deleted rather than left beside
    /// the shared one: two spellings of the same resolution is how the answers drift apart.</para>
    /// </summary>
    private void ValidateTypeStatement(Statement stmt)
    {
        switch (stmt)
        {
            case ClassDef classDef:
                ValidateTypeBody(classDef.Name, classDef.Body,
                    _context.LookupDeclaredType(classDef, classDef.Name));
                ValidateNestedTypes(classDef.Body);
                break;
            case StructDef structDef:
                ValidateTypeBody(structDef.Name, structDef.Body,
                    _context.LookupDeclaredType(structDef, structDef.Name));
                ValidateNestedTypes(structDef.Body);
                break;
            case InterfaceDef interfaceDef:
                ValidateInterfaceEvents(interfaceDef.Name, interfaceDef.Body);
                ValidateNestedTypes(interfaceDef.Body);
                break;
        }
    }

    private void ValidateNestedTypes(IReadOnlyList<Statement> body)
    {
        foreach (var member in body)
        {
            if (TypeStatementName(member) != null)
                ValidateTypeStatement(member);
        }
    }

    private static string? TypeStatementName(Statement stmt) => stmt switch
    {
        ClassDef c => c.Name,
        StructDef s => s.Name,
        InterfaceDef i => i.Name,
        _ => null
    };

    private void ValidateTypeBody(string typeName, IReadOnlyList<Statement> body, TypeSymbol? ownerSymbol)
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

        // Check for unpaired function-style accessors and abstractness agreement
        foreach (var (eventName, group) in eventGroups)
        {
            ValidateUnpairedAccessors(typeName, eventName, group);
            ValidateAccessorAbstractnessAgreement(typeName, eventName, group, ownerSymbol);
        }
    }

    private void ValidateInterfaceEvents(string typeName, IReadOnlyList<Statement> body)
    {
        var fieldNames = new HashSet<string>();
        var methodNames = new HashSet<string>();
        var eventDefs = new List<EventDef>();
        var eventGroups = new Dictionary<string, List<EventDef>>();

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
                    if (!eventGroups.TryGetValue(eventDef.Name, out var group))
                    {
                        group = new List<EventDef>();
                        eventGroups[eventDef.Name] = group;
                    }
                    group.Add(eventDef);
                    break;
            }
        }

        foreach (var eventDef in eventDefs)
        {
            ValidateAbstractEventBody(typeName, eventDef);
            ValidateEventAgainstFields(typeName, eventDef, fieldNames);
            ValidateEventAgainstMethods(typeName, eventDef, methodNames);
        }

        foreach (var (eventName, group) in eventGroups)
        {
            ValidateUnpairedAccessors(typeName, eventName, group);
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
        bool isAbstract = Shared.MemberClassification.HasAbstractDecorator(eventDef.Decorators);
        if (!isAbstract || !eventDef.IsFunctionStyle)
            return;

        bool isEllipsisBody = AstHelper.IsAbstractStubBody(eventDef.Body);

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

        bool isAbstract = Shared.MemberClassification.HasAbstractDecorator(eventDef.Decorators);
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

    /// <summary>
    /// Rule 6: All function-style accessors of one event must agree about abstractness (#1264).
    /// </summary>
    private void ValidateAccessorAbstractnessAgreement(
        string typeName, string eventName, List<EventDef> group, TypeSymbol? ownerSymbol)
    {
        if (group.Count < 2 || !group.Any(e => e.IsFunctionStyle))
            return;

        bool ownerIsAbstract = ownerSymbol?.IsAbstract == true;

        bool hasAbstract = false;
        bool hasConcrete = false;
        foreach (var eventDef in group)
        {
            bool isAbstract = Shared.MemberClassification.IsAbstract(
                eventDef, ownerSymbol?.TypeKind ?? TypeKind.Class, ownerIsAbstract);
            if (isAbstract)
                hasAbstract = true;
            else
                hasConcrete = true;
        }

        if (hasAbstract && hasConcrete)
        {
            var first = group[0];
            AddError(_context,
                $"Event '{eventName}' in '{typeName}' has accessors that disagree about abstractness — "
                + "all accessors must be abstract or all must be concrete",
                first.LineStart, first.ColumnStart,
                code: DiagnosticCodes.Validation.EventAccessorAbstractnessDisagreement,
                span: first.Span);
        }
    }
}
