using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates property declarations in classes and structs.
///
/// Rules:
/// 1. Property cannot have the same name as a field (SPY0405)
/// 2. Property cannot have the same name as a method (SPY0406)
/// 3. Cannot mix auto-property and function-style for the same name UNLESS complementary (SPY0407)
///    - auto + custom setter → allowed (auto defines backing field + getter)
///    - auto + custom getter → allowed (auto defines backing field + setter)
///    - auto + custom getter + custom setter → allowed (auto defines backing field only)
///    - auto + auto duplicate → still rejected
/// 4. 'property init' is only valid for auto-properties (SPY0408)
/// 5. @abstract properties must have ellipsis body (SPY0409)
/// 6. @final cannot be combined with @abstract or @virtual (SPY0410)
/// 7. @override property must have matching virtual/abstract base property (SPY0411)
/// 8. Init properties without defaults must be assigned in every constructor (SPY0426)
/// </summary>
internal class PropertyValidator : SemanticValidatorBase
{
    public override string Name => "PropertyValidator";
    public override int Order => 410; // After control flow (400), before unused variables (420)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting property validation");

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
                    // Interfaces have no concrete accessors, so any observer on an interface
                    // property is invalid (#416). The rest of PropertyValidator's rules do not
                    // apply to interface members.
                    foreach (var propDef in interfaceDef.Body.OfType<PropertyDef>())
                        ValidatePropertyObservers(interfaceDef.Name, propDef, isInterface: true);
                    break;
            }
        }
    }

    private void ValidateTypeBody(string typeName, IReadOnlyList<Statement> body)
    {
        // Collect fields, methods, and properties from the body
        var fieldNames = new HashSet<string>();
        var methodNames = new HashSet<string>();
        var propertyDefs = new List<PropertyDef>();

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
                case PropertyDef propDef:
                    propertyDefs.Add(propDef);
                    break;
            }
        }

        // Group properties by name to detect mixed styles
        var propertyGroups = new Dictionary<string, List<PropertyDef>>();
        foreach (var propDef in propertyDefs)
        {
            if (!propertyGroups.TryGetValue(propDef.Name, out var group))
            {
                group = new List<PropertyDef>();
                propertyGroups[propDef.Name] = group;
            }
            group.Add(propDef);
        }

        // Resolve the type symbol for override validation
        var typeSymbol = _context.SymbolTable.LookupType(typeName);

        // Check each property definition
        foreach (var propDef in propertyDefs)
        {
            ValidatePropertyAgainstFields(typeName, propDef, fieldNames);
            ValidatePropertyAgainstMethods(typeName, propDef, methodNames);
            ValidateInitOnlyFunctionStyle(typeName, propDef);
            ValidateAbstractPropertyBody(typeName, propDef);
            ValidateFinalNotWithAbstractOrVirtual(typeName, propDef);
            ValidatePropertyOverride(typeName, propDef, typeSymbol);
            ValidatePropertyObservers(typeName, propDef, isInterface: false);
        }

        // Check for mixed auto/function-style per property name
        foreach (var (propName, group) in propertyGroups)
        {
            ValidateMixedPropertyStyle(typeName, propName, group);
        }

        // Check that init properties without defaults are assigned in constructors
        ValidateInitPropertyAssignment(typeName, body, propertyDefs);

        // Check that @readonly properties are not assigned outside __init__
        ValidateReadonlyPropertyAssignment(typeName, body, propertyDefs);
    }

    /// <summary>
    /// Rule 1: Property cannot share a name with a field.
    /// </summary>
    private void ValidatePropertyAgainstFields(string typeName, PropertyDef propDef, HashSet<string> fieldNames)
    {
        if (fieldNames.Contains(propDef.Name))
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' conflicts with a field of the same name",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.PropertyFieldNameConflict,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 2: Property cannot share a name with a method.
    /// </summary>
    private void ValidatePropertyAgainstMethods(string typeName, PropertyDef propDef, HashSet<string> methodNames)
    {
        if (methodNames.Contains(propDef.Name))
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' conflicts with a method of the same name",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.PropertyMethodNameConflict,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 3: Cannot mix auto-property and function-style for the same name,
    /// UNLESS the function-style definitions are complementary custom accessors
    /// (e.g., auto + custom setter, auto + custom getter, auto + both custom).
    /// </summary>
    private void ValidateMixedPropertyStyle(string typeName, string propName, List<PropertyDef> group)
    {
        if (group.Count < 2)
            return;

        var autoDefs = group.Where(p => !p.IsFunctionStyle).ToList();
        var functionDefs = group.Where(p => p.IsFunctionStyle).ToList();

        if (autoDefs.Count == 0 || functionDefs.Count == 0)
            return;

        // Multiple auto-property definitions for the same name is always invalid
        if (autoDefs.Count > 1)
        {
            AddError(_context,
                $"Property '{propName}' in '{typeName}' has duplicate auto-property declarations",
                autoDefs[1].LineStart, autoDefs[1].ColumnStart,
                code: DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
                span: autoDefs[1].Span);
            return;
        }

        // Exactly one auto-property: function-style defs must be explicit get/set accessors
        // (not PropertyAccessor.None which means it's another auto-like definition)
        foreach (var funcDef in functionDefs)
        {
            if (funcDef.Accessor != PropertyAccessor.Get && funcDef.Accessor != PropertyAccessor.Set)
            {
                AddError(_context,
                    $"Property '{propName}' in '{typeName}' cannot mix auto-property and function-style definitions",
                    funcDef.LineStart, funcDef.ColumnStart,
                    code: DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
                    span: funcDef.Span);
                return;
            }
        }

        // Check for duplicate accessors (e.g., two custom setters)
        var customGetters = functionDefs.Count(p => p.Accessor == PropertyAccessor.Get);
        var customSetters = functionDefs.Count(p => p.Accessor == PropertyAccessor.Set);

        if (customGetters > 1 || customSetters > 1)
        {
            var duplicate = functionDefs.Last();
            AddError(_context,
                $"Property '{propName}' in '{typeName}' cannot mix auto-property and function-style definitions",
                duplicate.LineStart, duplicate.ColumnStart,
                code: DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
                span: duplicate.Span);
        }

        // Otherwise: auto + complementary custom accessors is allowed
    }

    /// <summary>
    /// Rule 4: 'property init' is only valid for auto-properties.
    /// </summary>
    private void ValidateInitOnlyFunctionStyle(string typeName, PropertyDef propDef)
    {
        if (propDef.Accessor == PropertyAccessor.Init && propDef.IsFunctionStyle)
        {
            AddError(_context,
                $"'property init' for '{propDef.Name}' in '{typeName}' is only valid for auto-properties, not function-style",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.InitOnlyFunctionStyleProperty,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 5: @abstract properties must have ellipsis body.
    /// An abstract function-style property body must be exactly one statement
    /// that is either an ellipsis literal or a pass statement.
    /// </summary>
    private void ValidateAbstractPropertyBody(string typeName, PropertyDef propDef)
    {
        bool isAbstract = propDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        if (!isAbstract || !propDef.IsFunctionStyle)
            return;

        bool isEllipsisBody = propDef.Body.Length == 1
            && (propDef.Body[0] is PassStatement
                || (propDef.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral));

        if (!isEllipsisBody)
        {
            AddError(_context,
                $"@abstract property '{propDef.Name}' in '{typeName}' must have '...' (ellipsis) body",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.AbstractPropertyMustHaveEllipsisBody,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 6: @final cannot be combined with @abstract or @virtual.
    /// </summary>
    private void ValidateFinalNotWithAbstractOrVirtual(string typeName, PropertyDef propDef)
    {
        bool isFinal = propDef.Decorators.Any(d => d.Name == DecoratorNames.Final);
        if (!isFinal)
            return;

        bool isAbstract = propDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool isVirtual = propDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual);

        if (isAbstract)
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' cannot be both @final and @abstract",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
                span: propDef.Span);
        }

        if (isVirtual)
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' cannot be both @final and @virtual",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithAbstractOrVirtual,
                span: propDef.Span);
        }
    }

    /// <summary>
    /// Rule 7: @override property must have a matching virtual/abstract property in the base class
    /// with a compatible (covariant) return type.
    /// </summary>
    private void ValidatePropertyOverride(string typeName, PropertyDef propDef, TypeSymbol? typeSymbol)
    {
        bool isOverride = propDef.Decorators.Any(d => d.Name == DecoratorNames.Override);
        if (!isOverride || typeSymbol == null)
            return;

        var baseType = typeSymbol.BaseType;
        if (baseType == null)
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' is marked @override but the class has no base class",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidPropertyOverride,
                span: propDef.Span);
            return;
        }

        var (baseProp, _) = TypeHierarchyService.FindProperty(baseType, propDef.Name);
        if (baseProp == null)
        {
            AddError(_context,
                $"Property '{propDef.Name}' in '{typeName}' is marked @override but no matching property exists in base class '{baseType.Name}'",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidPropertyOverride,
                span: propDef.Span);
            return;
        }

        if (!baseProp.IsVirtual && !baseProp.IsAbstract && !baseProp.IsOverride)
        {
            AddError(_context,
                $"Cannot override property '{propDef.Name}' because the base class property in '{baseType.Name}' is not marked @virtual or @abstract",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidPropertyOverride,
                span: propDef.Span);
            return;
        }

        // Check covariant return type: the derived property type must be assignable to the base property type
        var derivedPropSymbol = typeSymbol.Properties.FirstOrDefault(p => p.Name == propDef.Name);
        if (derivedPropSymbol != null && baseProp.Type is not UnknownType && derivedPropSymbol.Type is not UnknownType)
        {
            if (!derivedPropSymbol.Type.IsAssignableTo(baseProp.Type))
            {
                AddError(_context,
                    $"Property '{propDef.Name}' in '{typeName}' has type '{derivedPropSymbol.Type}' which is not compatible with base property type '{baseProp.Type}'",
                    propDef.LineStart, propDef.ColumnStart,
                    code: DiagnosticCodes.Validation.InvalidPropertyOverride,
                    span: propDef.Span);
            }
        }
    }

    /// <summary>
    /// Rule 9 (#416): 'before_set'/'after_set' observers are only valid on a settable
    /// auto-property. Rejects observers on function-style, @abstract, @override, interface,
    /// @readonly / get-only / init-only targets (SPY0490), and rejects a duplicate observer of
    /// the same kind (SPY0491).
    /// </summary>
    private void ValidatePropertyObservers(string typeName, PropertyDef propDef, bool isInterface)
    {
        if (propDef.Observers.IsEmpty)
            return;

        var firstObserver = propDef.Observers[0];
        string? invalidReason = DetermineObserverTargetError(propDef, isInterface);
        if (invalidReason != null)
        {
            AddError(_context,
                $"Property observers on '{propDef.Name}' in '{typeName}' are only valid on a settable " +
                $"auto-property, but this is {invalidReason}",
                firstObserver.LineStart, firstObserver.ColumnStart,
                code: DiagnosticCodes.Validation.PropertyObserverInvalidTarget,
                span: firstObserver.Span ?? propDef.Span);
        }

        // Each observer kind may appear at most once.
        bool seenBeforeSet = false;
        bool seenAfterSet = false;
        foreach (var observer in propDef.Observers)
        {
            bool duplicate = observer.Kind == ObserverKind.BeforeSet ? seenBeforeSet : seenAfterSet;
            if (duplicate)
            {
                var keyword = observer.Kind == ObserverKind.BeforeSet ? "before_set" : "after_set";
                AddError(_context,
                    $"Property '{propDef.Name}' in '{typeName}' has more than one '{keyword}' observer",
                    observer.LineStart, observer.ColumnStart,
                    code: DiagnosticCodes.Validation.DuplicatePropertyObserver,
                    span: observer.Span ?? propDef.Span);
            }

            if (observer.Kind == ObserverKind.BeforeSet)
                seenBeforeSet = true;
            else
                seenAfterSet = true;
        }
    }

    /// <summary>
    /// Returns a user-facing noun phrase describing why <paramref name="propDef"/> cannot carry
    /// observers, or null if it is a valid (settable auto-property) target.
    /// </summary>
    private static string? DetermineObserverTargetError(PropertyDef propDef, bool isInterface)
    {
        if (isInterface)
            return "an interface property";
        if (propDef.IsFunctionStyle)
            return "a function-style property";
        if (propDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract))
            return "an @abstract property";
        if (propDef.Decorators.Any(d => d.Name == DecoratorNames.Override))
            return "an @override property";
        if (propDef.Decorators.Any(d => d.Name == DecoratorNames.Readonly))
            return "a @readonly property (no setter)";
        return propDef.Accessor switch
        {
            PropertyAccessor.Get => "a get-only property (no setter)",
            PropertyAccessor.Init => "an init-only property (no setter)",
            PropertyAccessor.Set => "a set-only property (no standard get/set accessor)",
            _ => null,
        };
    }

    /// <summary>
    /// Rule 8: Init properties without defaults must be assigned in every constructor.
    /// </summary>
    private void ValidateInitPropertyAssignment(string typeName, IReadOnlyList<Statement> body, List<PropertyDef> propertyDefs)
    {
        // Collect init properties without defaults
        var initPropsWithoutDefaults = propertyDefs
            .Where(p => p.Accessor == PropertyAccessor.Init && !p.IsFunctionStyle && p.DefaultValue == null)
            .ToList();

        if (initPropsWithoutDefaults.Count == 0)
            return;

        // Find all __init__ methods (constructors)
        var initMethods = body.OfType<FunctionDef>().Where(f => f.Name == DunderNames.Init).ToList();

        if (initMethods.Count == 0)
        {
            // No constructor at all — all init properties without defaults are unassigned
            foreach (var prop in initPropsWithoutDefaults)
            {
                AddError(_context,
                    $"Init property '{prop.Name}' in '{typeName}' must be assigned in every constructor",
                    prop.LineStart, prop.ColumnStart,
                    code: DiagnosticCodes.Validation.InitPropertyNotAssigned,
                    span: prop.Span);
            }
            return;
        }

        // Check each constructor
        var propNames = initPropsWithoutDefaults.Select(p => p.Name).ToList();
        foreach (var initMethod in initMethods)
        {
            var assignedNames = CollectGuaranteedSelfAssignments(initMethod, propNames, _context.ControlFlowGraphs);
            foreach (var prop in initPropsWithoutDefaults)
            {
                if (!assignedNames.Contains(prop.Name))
                {
                    AddError(_context,
                        $"Init property '{prop.Name}' in '{typeName}' must be assigned in every constructor",
                        initMethod.LineStart, initMethod.ColumnStart,
                        code: DiagnosticCodes.Validation.InitPropertyNotAssigned,
                        span: initMethod.Span);
                }
            }
        }
    }

    /// <summary>
    /// Collects names of members that are guaranteed to be assigned via self.{name} = ...
    /// on all paths through the method body, using CFG-based forward "must-assign" analysis.
    /// </summary>
    private static HashSet<string> CollectGuaranteedSelfAssignments(
        FunctionDef initMethod,
        IReadOnlyCollection<string> allProperties,
        ControlFlowGraphCache cfgCache)
    {
        // Key on the FunctionDef so this graph is shared with ControlFlowValidator/StructRulesValidator.
        var cfg = cfgCache.GetOrBuild(initMethod);

        var rpo = cfg.GetReversePostOrder();

        // For each block, compute which self.{name} assignments it contains
        var blockAssignments = new Dictionary<BasicBlock, HashSet<string>>();
        foreach (var block in cfg.Blocks)
        {
            var assigns = new HashSet<string>();
            foreach (var stmt in block.Statements)
            {
                if (stmt is Assignment { Operator: AssignmentOperator.Assign, Target: MemberAccess { Object: Identifier { Name: "self" }, Member: var member } })
                {
                    assigns.Add(member);
                }
            }
            blockAssignments[block] = assigns;
        }

        // Forward "must-assign" dataflow analysis using worklist algorithm
        // mustAssignIn[block] = intersection of:
        //   - mustAssignOut[pred] for normal predecessors
        //   - mustAssignIn[pred] for exception predecessors (conservative: assumes no
        //     statements in the excepting block completed)
        // mustAssignOut[block] = mustAssignIn[block] union blockAssignments[block]
        var mustAssignIn = new Dictionary<BasicBlock, HashSet<string>>();
        var mustAssignOut = new Dictionary<BasicBlock, HashSet<string>>();
        // Optimistic initialization for forward must-analysis: non-entry blocks start as
        // "all properties assigned." Intersection with predecessors drains any overestimates
        // during worklist iteration. This produces the same fixpoint as empty initialization
        // but converges faster on straight-line constructors (the common case).
        // NOTE: This pattern is correct for must-analysis (intersection) but would be wrong
        // for may-analysis (union), where blocks must start empty.
        var universalSet = new HashSet<string>(allProperties);
        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry)
            {
                mustAssignIn[block] = new HashSet<string>();
                mustAssignOut[block] = new HashSet<string>();
            }
            else
            {
                mustAssignIn[block] = new HashSet<string>(universalSet);
                mustAssignOut[block] = new HashSet<string>(universalSet);
            }
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in rpo)
            {
                if (block == cfg.Entry)
                    continue;

                // Compute mustIn as intersection of all predecessor contributions
                HashSet<string>? computedIn = null;

                // Normal predecessors: use their output (assignments completed)
                foreach (var pred in block.Predecessors)
                {
                    if (computedIn == null)
                        computedIn = new HashSet<string>(mustAssignOut[pred]);
                    else
                        computedIn.IntersectWith(mustAssignOut[pred]);
                }

                // Exception predecessors: use their input (conservative — exception may
                // have occurred before any statement in the predecessor completed)
                foreach (var pred in block.ExceptionPredecessors)
                {
                    if (computedIn == null)
                        computedIn = new HashSet<string>(mustAssignIn[pred]);
                    else
                        computedIn.IntersectWith(mustAssignIn[pred]);
                }

                computedIn ??= new HashSet<string>();

                // mustAssignOut = mustIn union block's own assignments
                var newOut = new HashSet<string>(computedIn);
                newOut.UnionWith(blockAssignments[block]);

                if (!computedIn.SetEquals(mustAssignIn[block]) || !newOut.SetEquals(mustAssignOut[block]))
                {
                    mustAssignIn[block] = computedIn;
                    mustAssignOut[block] = newOut;
                    changed = true;
                }
            }
        }

        return mustAssignOut[cfg.Exit];
    }

    private void ValidateReadonlyPropertyAssignment(string typeName, IReadOnlyList<Statement> body, List<PropertyDef> propertyDefs)
    {
        var readonlyProps = new HashSet<string>(
            propertyDefs
                .Where(p => p.Decorators.Any(d => d.Name == DecoratorNames.Readonly))
                .Select(p => p.Name));

        if (readonlyProps.Count == 0)
            return;

        foreach (var member in body)
        {
            if (member is not FunctionDef method || method.Name == DunderNames.Init)
                continue;

            WalkForReadonlyAssignments(typeName, method.Body, readonlyProps);
        }
    }

    private void WalkForReadonlyAssignments(string typeName, IReadOnlyList<Statement> statements, HashSet<string> readonlyProps)
    {
        foreach (var stmt in statements)
        {
            if (stmt is Assignment { Target: MemberAccess { Object: Identifier { Name: "self" }, Member: var member } } assign
                && readonlyProps.Contains(member))
            {
                AddError(_context,
                    $"Cannot assign to @readonly property '{member}' outside of __init__",
                    assign.LineStart, assign.ColumnStart,
                    code: DiagnosticCodes.Validation.ReadonlyPropertyAssignment,
                    span: assign.Span);
            }

            foreach (var child in GetChildStatements(stmt))
            {
                WalkForReadonlyAssignments(typeName, child, readonlyProps);
            }
        }
    }

    private static IEnumerable<IReadOnlyList<Statement>> GetChildStatements(Statement stmt)
    {
        switch (stmt)
        {
            case IfStatement ifStmt:
                yield return ifStmt.ThenBody;
                foreach (var elif in ifStmt.ElifClauses)
                    yield return elif.Body;
                if (ifStmt.ElseBody.Length > 0)
                    yield return ifStmt.ElseBody;
                break;
            case ForStatement forStmt:
                yield return forStmt.Body;
                break;
            case WhileStatement whileStmt:
                yield return whileStmt.Body;
                break;
            case TryStatement tryStmt:
                yield return tryStmt.Body;
                foreach (var handler in tryStmt.Handlers)
                    yield return handler.Body;
                if (tryStmt.FinallyBody.Length > 0)
                    yield return tryStmt.FinallyBody;
                if (tryStmt.ElseBody.Length > 0)
                    yield return tryStmt.ElseBody;
                break;
            case WithStatement withStmt:
                yield return withStmt.Body;
                break;
        }
    }
}
