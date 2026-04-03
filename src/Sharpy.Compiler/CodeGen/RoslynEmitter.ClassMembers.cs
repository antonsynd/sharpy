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
/// RoslynEmitter partial class: Class member generation (orchestrator, interface members, dunder registry)
/// </summary>
internal partial class RoslynEmitter
{
    private List<MemberDeclarationSyntax> GenerateClassMembers(
        IReadOnlyList<Statement> body, string className, string originalTypeName)
    {
        var members = new List<MemberDeclarationSyntax>();

        // First pass: generate fields and build mappings for use in constructor
        var fieldMapping = new Dictionary<string, string>();
        var fieldTypeMapping = new Dictionary<string, TypeAnnotation>();
        var fieldMembers = new List<MemberDeclarationSyntax>();

        var typeSymbol = _context.LookupSymbol(originalTypeName) as TypeSymbol;
        var previousTypeSymbol = _currentTypeSymbol;
        _currentTypeSymbol = typeSymbol;

        bool isDataclass = typeSymbol is { IsDataclass: true };
        bool isFrozen = typeSymbol is { DataclassInfo.Frozen: true };

        foreach (var stmt in body.Where(s => s is VariableDeclaration))
        {
            var varDecl = (VariableDeclaration)stmt;
            var fieldSymbol = typeSymbol?.Fields.FirstOrDefault(f => f.Name == varDecl.Name);
            var codeGenInfo = fieldSymbol != null ? GetCodeGenInfo(fieldSymbol) : null;
            var fieldName = codeGenInfo?.CSharpName ?? NameMangler.ToPascalCase(varDecl.Name);

            if (isDataclass && !varDecl.Decorators.Any(d => d.Name == DecoratorNames.Static))
            {
                var propDecl = GenerateDataclassProperty(varDecl, fieldName, isFrozen);
                fieldMembers.Add(propDecl);
            }
            else
            {
                // Regular field
                var fieldDecl = GenerateField(varDecl, codeGenInfo?.CSharpName);
                fieldMembers.Add(fieldDecl);
                // Extract the field name from the generated declaration
                var variable = ((FieldDeclarationSyntax)fieldDecl).Declaration.Variables.First();
                fieldName = variable.Identifier.Text;
            }

            fieldMapping[varDecl.Name] = fieldName;

            // Also track the field's declared type for contextual type inference
            if (varDecl.Type != null)
            {
                fieldTypeMapping[varDecl.Name] = varDecl.Type;
            }
        }

        // Also register auto-property names in fieldMapping so self.name = value
        // in constructors resolves to this.Name = value
        foreach (var stmt in body.Where(s => s is PropertyDef pd && !pd.IsFunctionStyle))
        {
            var propDef = (PropertyDef)stmt;
            var propName = NameMangler.ToPascalCase(propDef.Name);
            fieldMapping[propDef.Name] = propName;
            if (propDef.Type != null)
            {
                fieldTypeMapping[propDef.Name] = propDef.Type;
            }
        }

        // Register auto-event names in fieldMapping so self.event_name resolves correctly
        foreach (var stmt in body.Where(s => s is EventDef ed && !ed.IsFunctionStyle))
        {
            var eventDef = (EventDef)stmt;
            var eventName = NameMangler.ToPascalCase(eventDef.Name);
            fieldMapping[eventDef.Name] = eventName;
        }

        // Add field members first
        members.AddRange(fieldMembers);

        // Second pass: generate methods, constructors, and operator overloads
        // Collect all __init__ methods for constructor generation (supports overloading)
        var initMethods = new List<FunctionDef>();

        // Collect __getitem__ and __setitem__ for indexer generation
        FunctionDef? getItemFunc = null;
        FunctionDef? setItemFunc = null;

        // Collect all PropertyDef nodes, grouped by name for combining getter/setter
        var propertyGroups = new Dictionary<string, List<PropertyDef>>();

        // Collect all EventDef nodes, grouped by name for combining add/remove accessors
        var eventGroups = new Dictionary<string, List<EventDef>>();

        // Track which dunder methods are present for complementary operator generation
        var dunders = new HashSet<string>();
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef fd && DunderMapping.IsDunderMethod(fd.Name))
            {
                dunders.Add(fd.Name);
            }
        }

        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef:
                    // Special cases handled outside the registry:
                    // __init__ requires collecting multiple defs for overloads
                    // __getitem__/__setitem__ are collected for combined indexer generation
                    if (funcDef.Name == DunderNames.Init)
                    {
                        initMethods.Add(funcDef);
                    }
                    else if (funcDef.Name == DunderNames.GetItem)
                    {
                        getItemFunc = funcDef;
                    }
                    else if (funcDef.Name == DunderNames.SetItem)
                    {
                        setItemFunc = funcDef;
                    }
                    // Registry-based dispatch for dunders with special codegen
                    else if (_dunderRegistry.TryGetHandler(funcDef.Name, out var handler))
                    {
                        var ctx = new DunderCodeGenRegistry.DunderCodeGenContext(
                            className, dunders, body);
                        members.AddRange(handler!(funcDef, ctx));
                    }
                    // Remaining dunders: attempt operator synthesis with inlining
                    else if (DunderMapping.IsDunderMethod(funcDef.Name))
                    {
                        members.AddRange(HandleDefaultDunderMethod(funcDef, className));
                    }
                    // Regular methods
                    else
                    {
                        members.Add(GenerateClassMethod(funcDef));
                    }
                    break;

                case PropertyDef propDef:
                    // Collect for grouped generation (getter+setter combine into one C# property)
                    if (!propertyGroups.TryGetValue(propDef.Name, out var group))
                    {
                        group = new List<PropertyDef>();
                        propertyGroups[propDef.Name] = group;
                    }
                    group.Add(propDef);
                    break;

                case EventDef eventDef:
                    // Collect for grouped generation (add+remove combine into one C# event)
                    if (!eventGroups.TryGetValue(eventDef.Name, out var eventGroup))
                    {
                        eventGroup = new List<EventDef>();
                        eventGroups[eventDef.Name] = eventGroup;
                    }
                    eventGroup.Add(eventDef);
                    break;

                case VariableDeclaration _:
                    // Already processed in first pass
                    break;

                case PassStatement:
                    // Ignore pass in class body
                    break;

                case ExpressionStatement exprStmt when exprStmt.Expression is EllipsisLiteral:
                    // Ignore ellipsis in class body
                    break;

                case TypeAlias:
                    // Type aliases are compile-time only, no C# output
                    break;

                default:
                    _context.AddError(
                        $"Internal: unrecognized statement type '{stmt.GetType().Name}' in class body was not emitted. This is a compiler bug — please report it.",
                        DiagnosticCodes.CodeGen.UnrecognizedStatementType,
                        stmt.LineStart,
                        stmt.ColumnStart);
                    break;
            }
        }

        // Generate all properties (grouped by name to combine getter/setter)
        foreach (var (propName, propGroup) in propertyGroups)
        {
            members.AddRange(GenerateGroupedProperty(propGroup));
        }

        // Generate all events (grouped by name to combine add/remove accessors)
        foreach (var (eventName, eventGroup) in eventGroups)
        {
            members.Add(GenerateGroupedEvent(eventGroup));
        }

        // Generate indexer from __getitem__/__setitem__ (combined into single C# indexer)
        if (getItemFunc != null || setItemFunc != null)
        {
            members.Add(GenerateIndexer(getItemFunc, setItemFunc));
        }

        // Generate constructors: either explicit __init__ or synthesized @dataclass constructor
        if (_currentTypeSymbol is { IsDataclass: true } && initMethods.Count == 0)
        {
            // Full dataclass synthesis: constructor + Equals + GetHashCode + ToString
            members.AddRange(GenerateDataclassMembers(_currentTypeSymbol, className, body));
        }
        else
        {
            // Generate all constructors (supports overloading)
            foreach (var initMethod in initMethods)
            {
                members.Add(GenerateConstructor(initMethod, className, fieldMapping, fieldTypeMapping));
            }

            // Generate forwarding constructors if this class has no __init__ and inherits
            // from a class with constructors. C# doesn't inherit constructors, so subclasses
            // without __init__ need forwarding constructors to call the parent's constructor.
            if (initMethods.Count == 0 && _currentTypeSymbol?.BaseType != null)
            {
                members.AddRange(GenerateForwardingConstructors(className));
            }

            // For @dataclass with explicit __init__, still generate Equals/GetHashCode/ToString
            if (_currentTypeSymbol is { IsDataclass: true } && initMethods.Count > 0)
            {
                var options = _currentTypeSymbol.DataclassInfo!;
                var fields = _currentTypeSymbol.DataclassFields ?? new List<VariableSymbol>();

                if (options.Eq)
                {
                    members.Add(GenerateDataclassEquals(className, fields));
                    members.Add(GenerateDataclassGetHashCode(fields));
                    members.Add(GenerateDataclassOperatorEquals(className));
                    members.Add(GenerateDataclassOperatorNotEquals(className));
                }

                if (options.Repr)
                {
                    members.Add(GenerateDataclassToString(_currentTypeSymbol.Name, fields));
                }
            }
        }

        // Generate complementary operators for C# requirements
        // If __bool__ is defined, operator true was generated above — also generate operator false
        if (dunders.Contains(DunderNames.Bool))
        {
            members.Add(GenerateBoolOperatorFalse(className));
        }

        // If __eq__ is defined but not __ne__, generate operator != for each __eq__ overload
        if (dunders.Contains(DunderNames.Eq) && !dunders.Contains(DunderNames.Ne))
        {
            var eqMethods = body.OfType<FunctionDef>().Where(f => f.Name == DunderNames.Eq);
            foreach (var eqMethod in eqMethods)
            {
                members.Add(GenerateComplementaryNotEqualsOperator(eqMethod, className));
            }
        }
        // If __ne__ is defined but not __eq__, generate operator ==
        if (dunders.Contains(DunderNames.Ne) && !dunders.Contains(DunderNames.Eq))
        {
            members.Add(GenerateComplementaryEqualsOperator(className));
        }

        _currentTypeSymbol = previousTypeSymbol;
        return members;
    }

    private List<MemberDeclarationSyntax> GenerateInterfaceMembers(IReadOnlyList<Statement> body)
    {
        var members = new List<MemberDeclarationSyntax>();

        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef:
                    // Interface methods have no body
                    members.Add(GenerateInterfaceMethod(funcDef));
                    break;

                case PropertyDef propDef:
                    members.Add(GenerateInterfacePropertyFromDef(propDef));
                    break;

                case EventDef eventDef:
                    members.Add(GenerateInterfaceEvent(eventDef));
                    break;

                case VariableDeclaration varDecl:
                    // Interface properties (get/set accessors)
                    members.Add(GenerateInterfaceProperty(varDecl));
                    break;

                case PassStatement:
                    // Ignore pass in interface body
                    break;

                case ExpressionStatement exprStmt when exprStmt.Expression is EllipsisLiteral:
                    // Ignore ellipsis in interface body
                    break;

                default:
                    _context.AddError(
                        $"Internal: unrecognized statement type '{stmt.GetType().Name}' in interface body was not emitted. This is a compiler bug — please report it.",
                        DiagnosticCodes.CodeGen.UnrecognizedStatementType,
                        stmt.LineStart,
                        stmt.ColumnStart);
                    break;
            }
        }

        return members;
    }

    /// <summary>
    /// Check if an __eq__ FunctionDef has parameter type 'object', meaning it
    /// should generate 'override bool Equals(object)' instead of a new overload.
    /// </summary>
    private static bool IsEqualsObjectOverload(FunctionDef func)
    {
        var otherParam = func.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        return otherParam?.Type is TypeAnnotation { Name: "object" };
    }

    /// <summary>
    /// Builds the dunder codegen registry with handlers for each dunder that requires
    /// special code generation (as opposed to regular method emission).
    /// </summary>
    private DunderCodeGenRegistry BuildDunderRegistry()
    {
        var registry = new DunderCodeGenRegistry();

        // __len__ → Count property (ISized protocol)
        registry.Register(DunderNames.Len, (funcDef, _) =>
            new[] { GenerateLenProperty(funcDef) });

        // __bool__ → IsTrue property + operator true (operator false added in complementary pass)
        registry.Register(DunderNames.Bool, (funcDef, ctx) =>
            new MemberDeclarationSyntax[]
            {
                GenerateBoolProperty(funcDef),
                GenerateBoolOperatorTrue(ctx.ClassName)
            });

        // __next__ → IEnumerator<T> protocol members (MoveNext, Current, etc.)
        registry.Register(DunderNames.Next, (funcDef, _) =>
            GenerateIteratorProtocolMembers(funcDef));

        // __iter__ → IEnumerable<T> protocol (depends on whether __next__ is also present)
        registry.Register(DunderNames.Iter, (funcDef, ctx) =>
        {
            if (ctx.DundersPresent.Contains(DunderNames.Next))
            {
                // Self-iterating class: __iter__ returns self → GetEnumerator() => this
                var nextFunc = ctx.Body.OfType<FunctionDef>()
                    .FirstOrDefault(f => f.Name == DunderNames.Next);
                TypeSyntax elemType = nextFunc?.ReturnType != null
                    ? _typeMapper.MapType(nextFunc.ReturnType)
                    : PredefinedType(Token(SyntaxKind.ObjectKeyword));
                return GenerateEnumerableBridgeMembers(elemType);
            }
            else if (_context.SemanticInfo?.IsGenerator(funcDef) == true)
            {
                // Generator __iter__: body contains yield → emit IEnumerator<T> GetEnumerator()
                return GenerateGeneratorIterMethod(funcDef);
            }
            else
            {
                // Iterable-only: just generate GetEnumerator() with user body
                return new[] { GenerateClassMethod(funcDef) };
            }
        });

        // __reversed__ → GetReverseEnumerator() with IEnumerator<T> return type
        registry.Register(DunderNames.Reversed, (funcDef, _) =>
        {
            using var _gen = SetGeneratorScope(_context.SemanticInfo?.IsGenerator(funcDef) == true);
            using var _asyncRev = SetAsyncScope(funcDef.IsAsync);
            return new[] { GenerateReverseEnumeratorMethod(funcDef) };
        });

        // __eq__/__ne__ → Equals()/method + operator ==/!=
        registry.Register(DunderNames.Eq, (funcDef, ctx) =>
        {
            var result = new List<MemberDeclarationSyntax> { GenerateClassMethod(funcDef) };
            var eqOp = TryGenerateOperatorOverload(funcDef, ctx.ClassName);
            if (eqOp != null)
                result.Add(eqOp);
            return result;
        });

        registry.Register(DunderNames.Ne, (funcDef, ctx) =>
        {
            var result = new List<MemberDeclarationSyntax> { GenerateClassMethod(funcDef) };
            var eqOp = TryGenerateOperatorOverload(funcDef, ctx.ClassName);
            if (eqOp != null)
                result.Add(eqOp);
            return result;
        });

        return registry;
    }

    /// <summary>
    /// Handles a dunder method that is not in the registry — attempts operator synthesis
    /// with inlining, falling back to regular method generation.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> HandleDefaultDunderMethod(
        FunctionDef funcDef, string className)
    {
        var inlined = TryGenerateInlinedOperatorOverload(funcDef, className);
        if (inlined != null)
        {
            return inlined;
        }
        else
        {
            // Fallback for dunders that don't map to operators
            return new[] { GenerateClassMethod(funcDef) };
        }
    }

}
