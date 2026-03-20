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
        }

        // Check for mixed auto/function-style per property name
        foreach (var (propName, group) in propertyGroups)
        {
            ValidateMixedPropertyStyle(typeName, propName, group);
        }

        // Check that init properties without defaults are assigned in constructors
        ValidateInitPropertyAssignment(typeName, body, propertyDefs);
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
                $"Property '{propName}' in '{typeName}' cannot mix auto-property and function-style definitions",
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
            var assignedNames = CollectGuaranteedSelfAssignments(initMethod.Body, propNames);
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
    private static HashSet<string> CollectGuaranteedSelfAssignments(IReadOnlyList<Statement> body, IReadOnlyCollection<string> allProperties)
    {
        var builder = new ControlFlowGraphBuilder();
        var cfg = builder.Build(body);

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

    /// <summary>
    /// Walks the base class hierarchy to find a property with the given name.
    /// </summary>
}
