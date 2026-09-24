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

        // A NESTED type's symbol is defined by NameResolver inside the enclosing class's scope, so the
        // global lookup below cannot see it and `_currentTypeSymbol` was null for every nested type
        // (#1371) — which is why an implicitly-abstract member of a nested @abstract class emitted a
        // silently-throwing concrete member instead of an abstract one. The enclosing symbol's
        // NestedTypes is the index NameResolver already populates, and `_currentTypeSymbol` still holds
        // that enclosing symbol here, because this method is re-entered through the nested type's
        // generation before the outer frame restores it.
        //
        // Nested is consulted FIRST: an inner name shadows an outer one, so when both exist the inner
        // symbol is the right answer. This can only change what a nested type resolves to, since
        // `_currentTypeSymbol` is null at module level.
        var typeSymbol =
            _currentTypeSymbol?.NestedTypes.FirstOrDefault(n => n.Name == originalTypeName)
            ?? _context.LookupSymbol(originalTypeName) as TypeSymbol;
        var previousTypeSymbol = _currentTypeSymbol;
        _currentTypeSymbol = typeSymbol;

        bool isDataclass = typeSymbol is { IsDataclass: true };
        bool isFrozen = typeSymbol is { DataclassInfo.Frozen: true };

        // Whether a SYNTHESIZED constructor will own this type's field defaults: the same predicate
        // the constructor-generation switch below uses (a @dataclass, or a struct, with no explicit
        // __init__ — an explicit __init__ suppresses both syntheses, and then the member's own
        // initializer is the only thing that assigns the field). Read here because a member
        // initializer runs on EVERY construction, so for R-A's per-instance family it duplicated the
        // constructor's evaluation of the same default (#1901) — see
        // GeneratePerInstanceDefaultAssignment.
        bool hasExplicitInit = body.OfType<FunctionDef>().Any(f => f.Name == DunderNames.Init);
        bool synthesizedConstructorOwnsDefaults = !hasExplicitInit
            && (isDataclass || typeSymbol is { TypeKind: Semantic.TypeKind.Struct });

        foreach (var stmt in body.Where(s => s is VariableDeclaration))
        {
            var varDecl = (VariableDeclaration)stmt;
            var fieldSymbol = typeSymbol?.Fields.FirstOrDefault(f => f.Name == varDecl.Name);
            var codeGenInfo = fieldSymbol != null ? GetCodeGenInfo(fieldSymbol) : null;
            var fieldName = codeGenInfo?.CSharpName ?? NameCasing.ResolveField(varDecl.Name, varDecl.IsNameBacktickEscaped);
            bool constructorOwnsDefault = synthesizedConstructorOwnsDefaults
                && codeGenInfo?.RequiresPerInstanceDefault == true;

            // A dataclass's INSTANCE fields become auto-properties; its class-level storage
            // (a `const`, a `@static` field) is emitted as a FIELD, exactly as in a plain class.
            // Read from the AST decorator list, this arm printed
            // `public int SCALE { get; set; } = 3;` for a `const` and `P.SCALE` came back as
            // CS0120 behind SPY0908 — the same roster defect one host over, and an AST read in
            // CodeGen (#1794, Critical Rule 2). `IsSynthesizedConstructorField` is the one
            // predicate, reading `VariableSymbol.IsStatic` and `CodeGenInfo.IsConstant`.
            if (isDataclass && IsSynthesizedConstructorField(varDecl))
            {
                var propDecl = GenerateDataclassProperty(
                    varDecl, fieldName, isFrozen, constructorOwnsDefault);
                fieldMembers.Add(propDecl);
            }
            else
            {
                // Regular field
                var fieldDecl = GenerateField(varDecl, codeGenInfo?.CSharpName, constructorOwnsDefault);
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
            var propName = NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped);
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
            var eventName = NameCasing.ResolveMethod(eventDef.Name, eventDef.IsNameBacktickEscaped);
            fieldMapping[eventDef.Name] = eventName;
        }

        // Add field members first
        members.AddRange(fieldMembers);

        // Second pass: generate methods, constructors, and operator overloads
        // Collect all __init__ methods for constructor generation (supports overloading)
        var initMethods = new List<FunctionDef>();

        // Collect __getitem__ and __setitem__ for indexer generation (supports overloads)
        var getItemFuncs = new List<FunctionDef>();
        var setItemFuncs = new List<FunctionDef>();

        // Collect all PropertyDef nodes, grouped by name for combining getter/setter
        var propertyGroups = new Dictionary<string, List<PropertyDef>>();

        // Collect all EventDef nodes, grouped by name for combining add/remove accessors
        var eventGroups = GroupEventsByName(body);

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
                        getItemFuncs.Add(funcDef);
                    }
                    else if (funcDef.Name == DunderNames.SetItem)
                    {
                        setItemFuncs.Add(funcDef);
                    }
                    // Memoization decorators expand into multiple members (cache field +
                    // private body + public wrapper + accessor methods). Handled before
                    // the dunder registry/synthesis paths because cached dunders are not
                    // supported and would conflict with operator/protocol generation.
                    else if (IsLruCacheDecorated(funcDef))
                    {
                        members.AddRange(GenerateLruCacheWrappedFunction(funcDef, isModuleLevel: false));
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

                case EventDef:
                    // Already grouped by GroupEventsByName; emitted below so an add+remove
                    // pair combines into one C# event
                    break;

                case VariableDeclaration _:
                    // Already processed in first pass
                    break;

                case PassStatement:
                    // Ignore pass in class body
                    break;

                case ExpressionStatement exprStmt when AstHelper.TryGetEllipsisStub(exprStmt, out _):
                    // Ignore ellipsis in class body
                    break;

                case TypeAlias:
                    // Type aliases are compile-time only, no C# output
                    break;

                case ClassDef nestedClass:
                    members.Add(ReplaceAccessModifier(
                        GenerateClassDeclaration(nestedClass), NestedTypeAccessKeyword(nestedClass.Name, nestedClass)));
                    break;

                case StructDef nestedStruct:
                    members.Add(ReplaceAccessModifier(
                        GenerateStructDeclaration(nestedStruct), NestedTypeAccessKeyword(nestedStruct.Name, nestedStruct)));
                    break;

                case InterfaceDef nestedInterface:
                    members.Add(ReplaceAccessModifier(
                        GenerateInterfaceDeclaration(nestedInterface), NestedTypeAccessKeyword(nestedInterface.Name, nestedInterface)));
                    break;

                case EnumDef nestedEnum:
                    var nestedEnumNode = GenerateEnumDeclaration(nestedEnum);
                    if (nestedEnumNode is MemberDeclarationSyntax nestedEnumMember)
                    {
                        members.Add(ReplaceAccessModifier(
                            nestedEnumMember, NestedTypeAccessKeyword(nestedEnum.Name, nestedEnum)));
                    }
                    break;

                // A nested union/delegate reaches here as a UnionDef/DelegateDef statement in the
                // host body — name resolution leaves the immutable AST in place (#1729, R-I). Reuse
                // the module-level emitters as members; the union base + sealed cases and the C#
                // delegate are all legal nested in a class, struct, or interface (C# 8).
                case UnionDef nestedUnion:
                    if (GenerateUnionDeclaration(nestedUnion) is MemberDeclarationSyntax nestedUnionMember)
                    {
                        members.Add(ReplaceAccessModifier(
                            nestedUnionMember, NestedTypeAccessKeyword(nestedUnion.Name, nestedUnion)));
                    }
                    break;

                // DelegateDef carries no decorators, so its symbol's access level comes from the
                // name convention alone (with the host axis, #1937).
                case DelegateDef nestedDelegate:
                    members.Add(ReplaceAccessModifier(
                        GenerateDelegateDeclaration(nestedDelegate),
                        NestedTypeAccessKeyword(nestedDelegate.Name, nestedDelegate)));
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

        // Generate indexer(s) from __getitem__/__setitem__ — one C# indexer per key signature.
        // Group getters and setters by the CLR-mapped key parameter type so that int-keyed
        // and str-keyed overloads each emit their own indexer.
        if (getItemFuncs.Count > 0 || setItemFuncs.Count > 0)
        {
            var indexerGroups = GroupIndexersByKeySignature(getItemFuncs, setItemFuncs);
            foreach (var (getter, setter) in indexerGroups)
            {
                members.Add(GenerateIndexer(getter, setter));
            }
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

            // Generate auto-constructor(s) for structs with instance fields but no explicit __init__
            if (initMethods.Count == 0
                && _currentTypeSymbol is { TypeKind: Semantic.TypeKind.Struct }
                && body.OfType<VariableDeclaration>().Any(IsSynthesizedConstructorField))
            {
                members.AddRange(GenerateStructAutoConstructors(className, body));
            }
            // Generate forwarding constructors if this class has no __init__ and inherits
            // from a class with constructors. C# doesn't inherit constructors, so subclasses
            // without __init__ need forwarding constructors to call the parent's constructor.
            else if (initMethods.Count == 0 && _currentTypeSymbol?.BaseType != null)
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
        // If __ne__ is defined but not __eq__, generate operator == for each __ne__ overload — the
        // same parameter shape as the != it complements, or C# refuses the pair (CS0216).
        if (dunders.Contains(DunderNames.Ne) && !dunders.Contains(DunderNames.Eq))
        {
            foreach (var neMethod in body.OfType<FunctionDef>().Where(f => f.Name == DunderNames.Ne))
            {
                members.Add(GenerateComplementaryEqualsOperator(neMethod, className));
            }
        }

        // Ordering operators: C# requires < with >, and <= with >=.
        // Emit a throwing mirror for the missing half of each pair (#1806).
        var orderingMirrors = new (string defined, string mirror, SyntaxKind mirrorToken, string pythonOp)[]
        {
            (DunderNames.Lt, DunderNames.Gt, SyntaxKind.GreaterThanToken, ">"),
            (DunderNames.Gt, DunderNames.Lt, SyntaxKind.LessThanToken, "<"),
            (DunderNames.Le, DunderNames.Ge, SyntaxKind.GreaterThanEqualsToken, ">="),
            (DunderNames.Ge, DunderNames.Le, SyntaxKind.LessThanEqualsToken, "<="),
        };
        foreach (var (defined, mirror, mirrorToken, pythonOp) in orderingMirrors)
        {
            if (dunders.Contains(defined) && !dunders.Contains(mirror))
            {
                var definedFunc = body.OfType<FunctionDef>().FirstOrDefault(f => f.Name == defined);
                if (definedFunc != null)
                    members.Add(GenerateThrowingMirrorOperator(definedFunc, className, mirrorToken, pythonOp));
            }
        }

        _currentTypeSymbol = previousTypeSymbol;
        return members;
    }

    /// <summary>
    /// Groups the <see cref="EventDef"/> members of a type body by event name, in declaration order.
    /// A function-style <c>add</c>/<c>remove</c> pair shares one name and lowers to a single C# event,
    /// so every member path — class and interface alike — emits one member per group. This is the only
    /// event grouping: a second implementation is how the two paths drift apart (the interface path had
    /// none, so a pair emitted twice — CS0102, #1239).
    /// </summary>
    private static string IndexerKeySignature(FunctionDef func)
    {
        var keyParam = func.Parameters
            .FirstOrDefault(p =>
                !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase));
        return keyParam?.Type?.Name ?? "object";
    }

    private static List<(FunctionDef? Getter, FunctionDef? Setter)> GroupIndexersByKeySignature(
        List<FunctionDef> getters, List<FunctionDef> setters)
    {
        var groups = new Dictionary<string, (FunctionDef? Getter, FunctionDef? Setter)>();
        foreach (var g in getters)
        {
            var sig = IndexerKeySignature(g);
            groups[sig] = groups.TryGetValue(sig, out var existing)
                ? (g, existing.Setter)
                : (g, null);
        }
        foreach (var s in setters)
        {
            var sig = IndexerKeySignature(s);
            groups[sig] = groups.TryGetValue(sig, out var existing)
                ? (existing.Getter, s)
                : (null, s);
        }
        return groups.Values.ToList();
    }

    private static Dictionary<string, List<EventDef>> GroupEventsByName(IReadOnlyList<Statement> body)
    {
        var eventGroups = new Dictionary<string, List<EventDef>>();

        foreach (var stmt in body)
        {
            if (stmt is not EventDef eventDef)
                continue;

            if (!eventGroups.TryGetValue(eventDef.Name, out var eventGroup))
            {
                eventGroup = new List<EventDef>();
                eventGroups[eventDef.Name] = eventGroup;
            }
            eventGroup.Add(eventDef);
        }

        return eventGroups;
    }

    private List<MemberDeclarationSyntax> GenerateInterfaceMembers(IReadOnlyList<Statement> body)
    {
        var members = new List<MemberDeclarationSyntax>();
        var eventGroups = GroupEventsByName(body);

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
                    // One C# event per group: the add and remove halves share a name, so only the
                    // first accessor emits — at its declaration position — and the rest are skipped
                    var eventGroup = eventGroups[eventDef.Name];
                    if (ReferenceEquals(eventGroup[0], eventDef))
                    {
                        members.Add(GenerateInterfaceEvent(eventGroup));
                    }
                    break;

                case VariableDeclaration { IsConst: true } constDecl:
                    {
                        var constSymbol = _currentTypeSymbol?.Fields.FirstOrDefault(f => f.Name == constDecl.Name);
                        var constCodeGenInfo = constSymbol != null ? GetCodeGenInfo(constSymbol) : null;
                        members.Add(GenerateField(constDecl, constCodeGenInfo?.CSharpName));
                        break;
                    }

                case VariableDeclaration varDecl:
                    // Interface properties (get/set accessors)
                    members.Add(GenerateInterfaceProperty(varDecl));
                    break;

                case PassStatement:
                    // Ignore pass in interface body
                    break;

                case ExpressionStatement exprStmt when AstHelper.TryGetEllipsisStub(exprStmt, out _):
                    // Ignore ellipsis in interface body
                    break;

                case TypeAlias:
                    // Type aliases are compile-time only, no C# output (#1729)
                    break;

                case ClassDef nestedClass:
                    members.Add(GenerateClassDeclaration(nestedClass));
                    break;

                case StructDef nestedStruct:
                    members.Add(GenerateStructDeclaration(nestedStruct));
                    break;

                case InterfaceDef nestedInterface:
                    members.Add(GenerateInterfaceDeclaration(nestedInterface));
                    break;

                case EnumDef nestedEnum:
                    var nestedEnumNode = GenerateEnumDeclaration(nestedEnum);
                    if (nestedEnumNode is MemberDeclarationSyntax nestedEnumMember)
                        members.Add(nestedEnumMember);
                    break;

                // Nested union/delegate in an interface body are legal C# (nested types in
                // interfaces, C# 8) — supported, not refused (#1729, R-I).
                case UnionDef nestedUnion:
                    if (GenerateUnionDeclaration(nestedUnion) is MemberDeclarationSyntax nestedUnionMember)
                        members.Add(nestedUnionMember);
                    break;

                case DelegateDef nestedDelegate:
                    members.Add(GenerateDelegateDeclaration(nestedDelegate));
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
                // Self-iterating class: __iter__ returns self → GetEnumerator() => this. The
                // element is __next__'s materialized IEnumerator<T> row — the same read
                // GenerateIteratorProtocolMembers uses for __next__ itself (#1832).
                TypeSyntax elemType = GetSynthesizedElementType(DunderNames.Next);
                return GenerateEnumerableBridgeMembers(elemType);
            }
            else if (_context.Ir.IsGenerator(funcDef))
            {
                // Generator __iter__: body contains yield → emit IEnumerator<T> GetEnumerator()
                return GenerateGeneratorIterMethod(funcDef);
            }
            else if (FindSynthesizedInterface(DunderNames.Iter) is { InterfaceName: "IEnumerable" })
            {
                // Non-generator producer __iter__ (e.g. `return iter(self.items)`): synthesis
                // recognized the annotation as a producer and added the IEnumerable<T> row, so the
                // class needs the real enumerator method plus the non-generic bridge, not a plain
                // method returning whatever the annotation names (#1832; c03 is the guard —
                // without the bridge, CS0535 leaks).
                return GenerateNonGeneratorIterMethod(funcDef);
            }
            else
            {
                // Not an iteration producer at all (e.g. a bare `int` return) — plain method.
                return new[] { GenerateClassMethod(funcDef) };
            }
        });

        // __reversed__ → GetReverseEnumerator() with IEnumerator<T> return type
        registry.Register(DunderNames.Reversed, (funcDef, _) =>
        {
            using var _gen = SetGeneratorScope(_context.Ir.IsGenerator(funcDef));
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

    private static readonly SyntaxKind[] s_accessKinds =
    {
        SyntaxKind.PublicKeyword,
        SyntaxKind.ProtectedKeyword,
        SyntaxKind.PrivateKeyword,
        SyntaxKind.InternalKeyword
    };

    private static T ReplaceAccessModifier<T>(T declaration, SyntaxKind newAccess) where T : MemberDeclarationSyntax
    {

        var modifiers = declaration.Modifiers;
        var newModifiers = new SyntaxTokenList();
        bool replaced = false;

        foreach (var modifier in modifiers)
        {
            if (s_accessKinds.Contains(modifier.Kind()))
            {
                if (!replaced)
                {
                    newModifiers = newModifiers.Add(Token(newAccess));
                    replaced = true;
                }
            }
            else
            {
                newModifiers = newModifiers.Add(modifier);
            }
        }

        if (!replaced)
        {
            newModifiers = newModifiers.Insert(0, Token(newAccess));
        }

        return (T)declaration.WithModifiers(newModifiers);
    }

}
