using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Event generation
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Generates a C# event from a group of EventDef AST nodes.
    /// A single auto-event EventDef produces an event field declaration.
    /// Multiple function-style EventDefs with the same name (add + remove) combine
    /// into a single C# event with custom accessors.
    /// </summary>
    private MemberDeclarationSyntax GenerateGroupedEvent(List<EventDef> eventGroup)
    {
        var first = eventGroup[0];

        if (!first.IsFunctionStyle)
        {
            // Auto-event: single EventDef → event field declaration
            return GenerateAutoEvent(first);
        }

        // Function-style event: add + remove accessors → event with accessor list
        return GenerateFunctionStyleEvent(eventGroup);
    }

    /// <summary>
    /// Generates a C# event field declaration for an auto-event.
    /// <c>event on_click: EventHandler</c> → <c>public event EventHandler? OnClick;</c>
    /// <c>event on_hover: EventHandler[MouseEventArgs]</c> → <c>public event EventHandler&lt;MouseEventArgs&gt;? OnHover;</c>
    /// </summary>
    private MemberDeclarationSyntax GenerateAutoEvent(EventDef eventDef)
    {
        var eventName = NameCasing.ResolveMethod(eventDef.Name, eventDef.IsNameBacktickEscaped);

        // Map the delegate type (nullable for auto-events, since no subscribers initially)
        var delegateType = eventDef.Type != null
            ? _typeMapper.MapType(eventDef.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // Auto-events are nullable (no subscribers initially → null)
        var nullableType = NullableType(delegateType);

        // Build modifiers from decorators
        var modifiers = GenerateMethodModifiers(eventDef.Name, eventDef.Decorators);

        // Check for abstract: abstract events don't need nullable type
        bool isAbstract = eventDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract)
            || _isInAbstractClass;

        // For abstract events, the type is not nullable (abstract events have no backing field)
        var eventType = isAbstract ? delegateType : nullableType;

        // EventFieldDeclaration: public event EventHandler? OnClick;
        var eventField = BuildEventFieldDeclaration(eventType, eventName, modifiers);

        // Add C# attributes from unknown decorators
        var eventAttributes = GenerateAttributeListsFromDecorators(eventDef.Decorators);
        if (eventAttributes.Count > 0)
        {
            eventField = eventField.WithAttributeLists(eventAttributes);
        }

        return eventField;
    }

    /// <summary>
    /// Builds the bare C# event field declaration shape — <c>&lt;modifiers&gt; event T Name;</c> — with
    /// no accessor list. Shared by auto-events and by abstract function-style events, which C# forbids
    /// from using accessor syntax at all (CS8712).
    /// </summary>
    private static EventFieldDeclarationSyntax BuildEventFieldDeclaration(
        TypeSyntax eventType, string eventName, SyntaxTokenList modifiers)
    {
        var declaration = VariableDeclaration(eventType)
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier(eventName))));

        return EventFieldDeclaration(declaration).WithModifiers(modifiers);
    }

    /// <summary>
    /// True when a function-style event accessor is abstract: it carries <c>@abstract</c>, or it sits
    /// in an abstract class with an ellipsis body (implicit abstract). The implicit arm uses
    /// <see cref="AstHelper.IsEllipsisStubBody"/> — the abstract-class rule, matching
    /// <c>NameResolver</c>/<c>TypeChecker</c>, under which a <c>pass</c> body is a concrete empty
    /// implementation rather than a stub.
    /// </summary>
    private bool IsAbstractEventAccessor(EventDef eventDef)
    {
        return eventDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract)
            || (_isInAbstractClass && AstHelper.IsEllipsisStubBody(eventDef.Body));
    }

    /// <summary>
    /// Generates a C# event with custom add/remove accessors from function-style EventDef nodes.
    /// Rewrites the explicit <c>handler</c> parameter references to C#'s implicit <c>value</c> keyword
    /// and strips the <c>self</c> parameter.
    /// </summary>
    private MemberDeclarationSyntax GenerateFunctionStyleEvent(List<EventDef> eventGroup)
    {
        var first = eventGroup[0];
        var eventName = NameCasing.ResolveMethod(first.Name, first.IsNameBacktickEscaped);

        // Determine the event type from the handler parameter of the first accessor
        TypeSyntax eventType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        var handlerParam = first.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        if (handlerParam?.Type != null)
        {
            eventType = _typeMapper.MapType(handlerParam.Type);
        }

        // Determine event-level modifiers from decorators
        var modifiers = GenerateMethodModifiers(first.Name, first.Decorators);

        // Handle static: if any accessor has self, event is not static
        bool hasSelfParameter = eventGroup.Any(e => e.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase)));
        if (hasSelfParameter && modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.StaticKeyword)));
        }
        if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        // Add C# attributes from unknown decorators
        var funcEventAttributes = GenerateAttributeListsFromDecorators(first.Decorators);

        // C# forbids accessor syntax on an abstract event (CS8712): when every accessor in the pair
        // is abstract and every body is a stub — so there is no implementation to drop — the pair
        // lowers to the bare declaration form instead of an accessor list.
        // "Body is a stub" is AstHelper.IsAbstractStubBody, the same predicate EventValidator uses
        // to accept an @abstract event body, so every body the validator admits is lowerable here.
        if (eventGroup.All(IsAbstractEventAccessor)
            && eventGroup.All(e => AstHelper.IsAbstractStubBody(e.Body)))
        {
            if (!modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
            {
                modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
            }
            modifiers = ResolveModifierConflicts(modifiers);

            var abstractEvent = BuildEventFieldDeclaration(eventType, eventName, modifiers);
            return funcEventAttributes.Count > 0
                ? abstractEvent.WithAttributeLists(funcEventAttributes)
                : abstractEvent;
        }

        // Determine event-level access modifier (widest access)
        var eventAccess = GetWidestAccessModifier(modifiers);

        var accessors = new List<AccessorDeclarationSyntax>();

        foreach (var eventDef in eventGroup)
        {
            // Clear method scope tracking for each accessor
            ResetMethodScope();
            CollectSourceVariableNames(eventDef.Body);

            // Identify the handler parameter name (to rewrite to 'value')
            string? handlerParamName = null;
            foreach (var param in eventDef.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                handlerParamName = param.Name;
                // Don't register the handler param — it maps to C#'s implicit 'value'
            }

            SyntaxKind accessorKind = eventDef.Accessor switch
            {
                EventAccessor.Remove => SyntaxKind.RemoveAccessorDeclaration,
                _ => SyntaxKind.AddAccessorDeclaration,
            };

            var accessor = AccessorDeclaration(accessorKind);

            if (IsAbstractEventAccessor(eventDef))
            {
                accessor = accessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                // Generate body, rewriting handler parameter references to 'value'
                var previousHandlerRewrite = _eventHandlerParamName;
                _eventHandlerParamName = handlerParamName;
                var bodyStatements = GenerateSuite(eventDef.Body);
                accessor = accessor.WithBody(Block(bodyStatements));
                _eventHandlerParamName = previousHandlerRewrite;
            }

            // Apply accessor-level access modifier if it differs from event-level
            var accessorModifiers = GenerateMethodModifiers(eventDef.Name, eventDef.Decorators);
            var accessorAccess = GetAccessModifier(accessorModifiers);

            if (accessorAccess != null && accessorAccess != eventAccess)
            {
                accessor = accessor.WithModifiers(TokenList(Token(accessorAccess.Value)));
            }

            accessors.Add(accessor);
        }

        var eventDecl = EventDeclaration(eventType, eventName)
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(List(accessors)));

        if (funcEventAttributes.Count > 0)
        {
            eventDecl = eventDecl.WithAttributeLists(funcEventAttributes);
        }

        return eventDecl;
    }

    /// <summary>
    /// Generates a C# event declaration for an interface event.
    /// Interface events are abstract (semicolon-only accessors).
    /// <c>event property_changed: EventHandler[PropertyChangedEventArgs]</c>
    /// → <c>event EventHandler&lt;PropertyChangedEventArgs&gt; PropertyChanged;</c>
    /// </summary>
    private MemberDeclarationSyntax GenerateInterfaceEvent(EventDef eventDef)
    {
        var eventName = NameCasing.ResolveMethod(eventDef.Name, eventDef.IsNameBacktickEscaped);

        TypeSyntax eventType;
        if (eventDef.Type != null)
        {
            eventType = _typeMapper.MapType(eventDef.Type);
        }
        else
        {
            // Function-style interface event: get type from handler parameter
            var handlerParam = eventDef.Parameters
                .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
            eventType = handlerParam?.Type != null
                ? _typeMapper.MapType(handlerParam.Type)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Interface events are always abstract event field declarations, and an interface permits
        // no modifiers on them
        return BuildEventFieldDeclaration(eventType, eventName, TokenList());
    }
}
