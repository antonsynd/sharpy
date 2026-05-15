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
        var eventName = NameMangler.ToPascalCase(eventDef.Name);

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
        var declaration = VariableDeclaration(eventType)
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier(eventName))));

        var eventField = EventFieldDeclaration(declaration)
            .WithModifiers(modifiers);

        // Add C# attributes from unknown decorators
        var eventAttributes = GenerateAttributeListsFromDecorators(eventDef.Decorators);
        if (eventAttributes.Count > 0)
        {
            eventField = eventField.WithAttributeLists(eventAttributes);
        }

        return eventField;
    }

    /// <summary>
    /// Generates a C# event with custom add/remove accessors from function-style EventDef nodes.
    /// Rewrites the explicit <c>handler</c> parameter references to C#'s implicit <c>value</c> keyword
    /// and strips the <c>self</c> parameter.
    /// </summary>
    private MemberDeclarationSyntax GenerateFunctionStyleEvent(List<EventDef> eventGroup)
    {
        var first = eventGroup[0];
        var eventName = NameMangler.ToPascalCase(first.Name);

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

            bool hasEllipsisBody = eventDef.Body.Length == 1
                && eventDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
            bool isAbstract = eventDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract)
                || (_isInAbstractClass && hasEllipsisBody);

            if (isAbstract)
            {
                accessor = accessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                // Generate body, rewriting handler parameter references to 'value'
                var previousHandlerRewrite = _eventHandlerParamName;
                _eventHandlerParamName = handlerParamName;
                var bodyStatements = eventDef.Body.SelectMany(GenerateBodyStatements);
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

        // Add C# attributes from unknown decorators
        var funcEventAttributes = GenerateAttributeListsFromDecorators(first.Decorators);
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
        var eventName = NameMangler.ToPascalCase(eventDef.Name);

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

        // Interface events are always abstract event field declarations
        var declaration = VariableDeclaration(eventType)
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier(eventName))));

        return EventFieldDeclaration(declaration)
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }
}
