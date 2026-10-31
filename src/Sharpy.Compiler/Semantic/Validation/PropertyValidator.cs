using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates property declarations in classes and structs.
///
/// Rules (a property sharing its name with a field or method is not one of them: that is the member
/// seam's SPY0204 at the later declaration, NameResolver.TryDeclareMember — SPY0405/SPY0406 retired
/// into it, P21g D3, #2360):
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
/// 9. An accessor's signature must be expressible as a C# accessor's (SPY0496): its parameter
///    list, and a setter's return annotation — <c>None</c> or nothing (#2370, R-JD)
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
            ValidateTypeStatement(stmt);
        }

        ValidateModuleProperties(module.Body);

        // Rule 9 runs over EVERY property, in whatever declares it. The rules above are container-
        // shaped — they compare a property against its type's fields and methods — but the shape of
        // an accessor's own parameter list is a property of the accessor alone, and it is wrong in
        // exactly the same way at module level and in an interface. Those two are unreachable from
        // the walk above: `Validate` dispatches only on class/struct/interface, so a module-level
        // PropertyDef is never visited at all, and the interface arm runs only the observer rule
        // (#1406).
        foreach (var propDef in EnumerateAllProperties(module.Body))
        {
            ValidateAccessorParameterShape(propDef);
            ValidateWriteAccessorReturn(propDef);
        }
    }

    /// <summary>
    /// Validates one type declaration's property rules, and recurses into the types nested inside
    /// it.
    ///
    /// <para>The recursion is new with #1461 and is the other half of that fix. Resolving a nested
    /// declaration through <c>NestedTypes</c> is worth nothing to a validator that never reaches a
    /// nested declaration, and this walk stopped at <c>module.Body</c>: every container-shaped
    /// property rule — mixed styles, backing-field collisions, override checks — was silently
    /// unenforced inside a nested class. Rule 9 already walked to any depth (it needs no symbol), so
    /// the two halves of this validator disagreed about how deep the module went.</para>
    /// </summary>
    private void ValidateTypeStatement(Statement stmt)
    {
        switch (stmt.UnwrapDecorated())
        {
            case ClassDef classDef:
                ValidateTypeBody(classDef, classDef.Name, classDef.Body);
                ValidateNestedTypes(classDef.Body);
                break;
            case StructDef structDef:
                ValidateTypeBody(structDef, structDef.Name, structDef.Body);
                ValidateNestedTypes(structDef.Body);
                break;
            case InterfaceDef interfaceDef:
                // Interfaces have no concrete accessors, so any observer on an interface
                // property is invalid (#416). The rest of PropertyValidator's rules do not
                // apply to interface members.
                foreach (var propDef in interfaceDef.Body.OfType<PropertyDef>())
                    ValidatePropertyObservers(interfaceDef.Name, propDef, isInterface: true);
                ValidateNestedTypes(interfaceDef.Body);
                break;
        }
    }

    private void ValidateNestedTypes(IReadOnlyList<Statement> body)
    {
        foreach (var member in body)
        {
            if (member.UnwrapDecorated() is ClassDef or StructDef or InterfaceDef)
                ValidateTypeStatement(member);
        }
    }

    /// <summary>
    /// Every <see cref="PropertyDef"/> the module declares, at any depth: module level, and inside
    /// classes, structs and interfaces including nested ones.
    /// </summary>
    private static IEnumerable<PropertyDef> EnumerateAllProperties(IEnumerable<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case PropertyDef propDef:
                    yield return propDef;
                    break;
                case ClassDef classDef:
                    foreach (var nested in EnumerateAllProperties(classDef.Body))
                        yield return nested;
                    break;
                case StructDef structDef:
                    foreach (var nested in EnumerateAllProperties(structDef.Body))
                        yield return nested;
                    break;
                case InterfaceDef interfaceDef:
                    foreach (var nested in EnumerateAllProperties(interfaceDef.Body))
                        yield return nested;
                    break;
            }
        }
    }

    /// <summary>
    /// Rule 9: a property accessor receives exactly the one value C# hands it — a setter or init
    /// accessor gets the implicit <c>value</c>, a getter gets nothing — so <c>self</c> plus at most
    /// one value parameter is the whole expressible shape, fixed by construction (#1406).
    ///
    /// <para>A variadic here is not merely unusable: it bound as its ELEMENT type, so a body
    /// treating it as a sequence was refused with a type error about the element that never
    /// mentioned the real mistake. #1292's array-wrap cannot fix it the way it fixed ordinary
    /// parameters, because no C# accessor has the <c>params T[]</c> backing to wrap onto — wrapping
    /// would type-check and then emit a length call against a single value. A getter's extra
    /// parameter has nowhere to bind at all and reached codegen as CS0103 behind SPY0908 (#1405).</para>
    /// </summary>
    private void ValidateAccessorParameterShape(PropertyDef propDef)
    {
        if (!propDef.IsFunctionStyle)
            return;

        bool writesValue = propDef.Accessor is PropertyAccessor.Set or PropertyAccessor.Init;
        var accessorWord = writesValue ? propDef.Accessor.ToString().ToLowerInvariant() : "get";
        var valueParams = propDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var param in valueParams)
        {
            if (!param.IsVariadic)
                continue;

            AddError(_context,
                $"Variadic parameter '*{param.Name}' cannot appear in the '{accessorWord}' accessor of "
                + $"property '{propDef.Name}'. A property accessor receives exactly one value from C# "
                + "and has no argument list to vary — take a single 'list[T]' parameter instead, or "
                + "write a method if the caller really has several arguments to pass.",
                param.LineStart, param.ColumnStart,
                code: DiagnosticCodes.Validation.AccessorParameterNotExpressible,
                span: param.Span);
            return;
        }

        // A getter has no incoming value, so any non-self parameter is unbindable. Reported on the
        // first one; naming them all would repeat one fact per parameter.
        if (!writesValue && valueParams.Count > 0)
        {
            var extra = valueParams[0];
            AddError(_context,
                $"Parameter '{extra.Name}' cannot appear in the 'get' accessor of property "
                + $"'{propDef.Name}'. A getter receives no value from C#, so there is nothing for the "
                + "name to denote — write a method if it needs an argument.",
                extra.LineStart, extra.ColumnStart,
                code: DiagnosticCodes.Validation.AccessorParameterNotExpressible,
                span: extra.Span);
            return;
        }

        // More than one value parameter on a setter is the same rule's third face, but nothing in
        // the corpus reaches it and the parser may not admit it; left unreported rather than
        // guessed at.
    }

    /// <summary>
    /// Rule 9's return face (#2370, R-JD): a setter returns nothing to C#, so its return annotation
    /// is <c>None</c> or absent. The property's type is the value parameter's
    /// (<see cref="StatementExtensions.DeclaredTypeAnnotation(PropertyDef)"/>), so any other
    /// annotation is a type the accessor can never return — it was silently ignored. Reads the
    /// annotation as the checker resolved it in a return position, so an alias of <c>None</c> is
    /// <c>None</c>; an annotation that failed to resolve was already reported and is not reported
    /// again. (A function-style <c>init</c> accessor is SPY0408 and never reaches here meaningfully.)
    /// </summary>
    private void ValidateWriteAccessorReturn(PropertyDef propDef)
    {
        if (!propDef.IsFunctionStyle || propDef.Accessor != PropertyAccessor.Set || propDef.ReturnType is not { } annotation)
            return;

        var resolved = _context.SemanticInfo.GetTypeAnnotation(annotation);
        if (resolved is null or UnknownType or VoidType || _context.SemanticInfo.IsAnnotationErrorRecovery(annotation))
            return;

        AddError(_context,
            $"The 'set' accessor of property '{propDef.Name}' returns nothing to C#, so its return "
            + $"annotation can only be 'None'; '{resolved.GetDisplayName()}' is never returned. The "
            + "property's type is the value parameter's — drop the return annotation, or write a "
            + "method if the operation must produce a value.",
            annotation.LineStart, annotation.ColumnStart,
            code: DiagnosticCodes.Validation.AccessorParameterNotExpressible,
            span: annotation.Span);
    }

    private void ValidateTypeBody(Statement declaration, string typeName, IReadOnlyList<Statement> body)
    {
        // Collect fields and properties from the body
        var fieldDecls = new List<VariableDeclaration>();
        var propertyDefs = new List<PropertyDef>();

        foreach (var member in body)
        {
            switch (member)
            {
                case VariableDeclaration varDecl:
                    fieldDecls.Add(varDecl);
                    break;
                case PropertyDef propDef:
                    propertyDefs.Add(propDef);
                    break;
            }
        }

        // Resolve the type symbol for override validation. Through NestedTypes, not by bare name:
        // a nested class's symbol is defined inside its enclosing class's scope, so the bare
        // lookup returned null and every symbol-dependent rule below silently did nothing (#1461).
        var typeSymbol = _context.LookupDeclaredType(declaration, typeName);

        // Group properties by member key to detect mixed styles — a bare `area` and an explicit
        // `IShape.area` are two properties (R-JH, #2390). Only the halves the member seam ADMITTED:
        // a refused half already drew its SPY0204/SPY0617 and is not part of the property (#2075, D7).
        var propertyGroups = new Dictionary<(string Name, string? ExplicitInterface), List<PropertyDef>>();
        foreach (var propDef in propertyDefs)
        {
            if (typeSymbol?.RefusedDeclarations.Contains(propDef) == true)
                continue;
            if (!propertyGroups.TryGetValue(propDef.MemberKey(), out var group))
            {
                group = new List<PropertyDef>();
                propertyGroups[propDef.MemberKey()] = group;
            }
            group.Add(propDef);
        }

        // Check each property definition
        foreach (var propDef in propertyDefs)
        {
            ValidateInitOnlyFunctionStyle(typeName, propDef);
            ValidateAbstractPropertyBody(typeName, propDef);
            ValidateFinalNotWithAbstractOrVirtual(typeName, propDef);
            ValidatePropertyOverride(declaration, typeName, propDef, typeSymbol);
            ValidatePropertyObservers(typeName, propDef, isInterface: false);
        }

        // Check for mixed auto/function-style per property name
        foreach (var ((propName, _), group) in propertyGroups)
        {
            ValidateMixedPropertyStyle(typeName, propName, group);
            ValidateBackingFieldCollision($"in '{typeName}'", "self.", propName, group, fieldDecls);
        }

        // Check that init properties without defaults are assigned in constructors
        ValidateInitPropertyAssignment(typeName, body, propertyDefs);

        // Check that @readonly properties are not assigned outside __init__
        ValidateReadonlyPropertyAssignment(typeName, body, propertyDefs);
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

        if (ReportTwoDefaultedAutoHalves($"in '{typeName}'", propName, group))
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
    /// Rule 3's split-auto face (<see cref="ReportTwoDefaultedAutoHalves"/>) at module level. A module's
    /// split accessors "are merged … following the same rules as class-level split accessors"
    /// (properties_function_style.md, Module-Level Properties), and the emitter merges them with the
    /// class-level arm (<c>GenerateModuleLevelProperty</c> → <c>GenerateGroupedProperty</c> →
    /// <c>GenerateSplitAutoProperty</c>), so two defaulted auto halves (<c>property get a: int = 5</c> +
    /// <c>property set a: int = 6</c>) emitted ONE initializer and silently dropped the other (P21h
    /// verify; owner ruling 2026-10-28 (a): refused, not dropped). Only that face: Rule 3's other faces
    /// (an auto half pair beside a function-style accessor, an accessor-less function-style half) run at
    /// module level as they did before — ledgered, not widened in a verify round. Only the halves the
    /// name binder admitted are judged — a refused half already drew its SPY0204.
    ///
    /// <para>Rule 3b runs here too (#2409, R-KB): a module's property group is emitted onto the module
    /// class by the same arms, so a form that synthesizes <c>_Name</c> collides with a module variable
    /// <c>_name</c> exactly as it does with a field — CS0102 behind SPY0908 while the class and struct
    /// hosts drew SPY0407.</para>
    /// </summary>
    private void ValidateModuleProperties(IReadOnlyList<Statement> body)
    {
        var moduleVariables = body.OfType<VariableDeclaration>().ToList();
        var groups = new Dictionary<(string Name, string? ExplicitInterface), List<PropertyDef>>();
        foreach (var propDef in body.OfType<PropertyDef>())
        {
            if (!groups.TryGetValue(propDef.MemberKey(), out var group))
            {
                group = new List<PropertyDef>();
                groups[propDef.MemberKey()] = group;
            }
            group.Add(propDef);
        }

        foreach (var ((propName, _), halves) in groups)
        {
            var admitted = AdmittedModuleHalves(propName, halves);
            ReportTwoDefaultedAutoHalves("at module level", propName, admitted);
            ValidateBackingFieldCollision("at module level", "", propName, admitted, moduleVariables);
        }
    }

    /// <summary>
    /// Rule 3's split-auto face: split auto halves (<c>property get a: int = 5</c> + <c>property set a:
    /// int = 6</c>) are one auto property, and no C# auto property holds two initializers (#2382, owner
    /// ruling 2026-10-28 (a)) — SPY0407 at the later defaulted half. Shared by a type body and module
    /// level, which emit the group through the same arm.
    /// </summary>
    /// <param name="where">The host phrase after the property's name: <c>in 'C'</c> for a type body,
    /// <c>at module level</c> for a module's properties.</param>
    /// <returns>Whether it reported.</returns>
    private bool ReportTwoDefaultedAutoHalves(string where, string propName, List<PropertyDef> group)
    {
        if (group.Any(p => p.IsFunctionStyle))
            return false;

        var defaultedAutoDefs = group.Where(p => p.DefaultValue != null).ToList();
        if (defaultedAutoDefs.Count < 2)
            return false;

        AddError(_context,
            $"Property '{propName}' {where} has duplicate auto-property declarations: two auto halves carry a default; keep one",
            defaultedAutoDefs[1].LineStart, defaultedAutoDefs[1].ColumnStart,
            code: DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
            span: defaultedAutoDefs[1].Span);
        return true;
    }

    /// <summary>
    /// The halves of one module property name that NameResolver.ResolveModulePropertyDeclaration
    /// admitted, replayed with its own <see cref="NameResolver.AccessorDeclarations"/> rule in source
    /// order: the first half defined the module property symbol (else another binding owns the name and
    /// every half was refused), and a later half merges only when <c>Admits</c> its form. A type body
    /// records its refused halves (<see cref="TypeSymbol.RefusedDeclarations"/>); module level has no
    /// owning type symbol to carry that set.
    /// </summary>
    private List<PropertyDef> AdmittedModuleHalves(string propName, List<PropertyDef> halves)
    {
        if (_context.SymbolTable.Lookup(propName) is not VariableSymbol { IsModuleProperty: true } symbol
            || symbol.DeclarationSpan != halves[0].Span)
            return new List<PropertyDef>();

        var admitted = new List<PropertyDef> { halves[0] };
        var accessors = new NameResolver.AccessorDeclarations(isEvent: false, halves[0].LineStart);
        accessors.Add(NameResolver.AccessorDeclarations.FormOf(halves[0]));
        foreach (var half in halves.Skip(1))
        {
            var form = NameResolver.AccessorDeclarations.FormOf(half);
            if (!accessors.Admits(form))
                continue;
            accessors.Add(form);
            admitted.Add(half);
        }
        return admitted;
    }

    /// <summary>
    /// Rule 3b: a property form that synthesizes a NAMED backing field needs that name to be free.
    /// The spec's own function-style-setter example declares <c>_celsius</c> beside a
    /// <c>celsius</c> auto getter and hits CS0102/CS0229 on the duplicate <c>_Celsius</c> (#1307) —
    /// the storage is what the form supplies, so declaring it again is the error.
    ///
    /// <para>THREE forms synthesize that field, and this check used to know about one. All spell it
    /// <c>"_" + NameCasing.ResolveField(def.Name, def.IsNameBacktickEscaped)</c>: the mixed
    /// auto/function-style form (<c>GenerateMixedAutoCustomProperty</c>), the OBSERVER form
    /// (<c>GenerateObserverProperty</c>, #416) and the lone auto SET half (<c>GenerateAutoProperty</c>:
    /// C# has no set-only auto property, so its storage is a named field, #2395). An observer property is a pure auto-property — no
    /// function-style def in its group — so the old guard returned before looking at the field list
    /// while the emitter went on writing the field, and the collision came back as CS0102 behind
    /// SPY0908 where its mixed-form twin drew this clean SPY0407 (#1456).</para>
    ///
    /// <para>The predicate is therefore "does this group synthesize a named backing field", asked of
    /// the emitter's three forms, rather than the shape of one of them. Any other pure auto-property
    /// still gets C#'s compiler-generated field and is still not this rule's business.</para>
    ///
    /// <para>Mutation-tested: reverting the guard to <c>!group.Any(p => p.IsFunctionStyle)</c> turns
    /// <c>experimental/property_observers_backing_field_collision_1456</c> red.</para>
    ///
    /// <para>Every host that emits these forms runs it: a type body against its fields, and module
    /// level against its module variables (#2409) — the module class holds both.</para>
    /// </summary>
    /// <param name="where">The host phrase after the property's name: <c>in 'C'</c> or <c>at module level</c>.</param>
    /// <param name="receiver">How an accessor body spells the field: <c>self.</c> in a type, nothing at module level.</param>
    private void ValidateBackingFieldCollision(string where, string receiver, string propName,
        List<PropertyDef> group, List<VariableDeclaration> fieldDecls)
    {
        var autoDef = group.FirstOrDefault(p => !p.IsFunctionStyle);
        if (autoDef == null)
            return;

        var isMixedForm = group.Any(p => p.IsFunctionStyle);
        var isObserverForm = group.Any(p => !p.IsFunctionStyle && p.Observers.Length > 0);
        // An abstract lone set half declares no storage (`abstract T Name { set; }`), so no field to collide with.
        var isLoneAutoSetter = group.Count == 1 && autoDef.Accessor == PropertyAccessor.Set
            && !autoDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract);
        if (!isMixedForm && !isObserverForm && !isLoneAutoSetter)
            return;

        var backingField = "_" + NameCasing.ResolveField(autoDef.Name, autoDef.IsNameBacktickEscaped);
        var collision = fieldDecls.FirstOrDefault(f =>
            NameCasing.ResolveField(f.Name, f.IsNameBacktickEscaped) == backingField);
        if (collision == null)
            return;

        // The mixed form is named by what it mixes; the observer form is named by its observers.
        // All three supply the storage, so the remedy is the same sentence — except that a
        // write-only property has no accessor body to go on writing the field.
        var supplier = isMixedForm
            ? "The mixed auto/function-style form supplies the storage"
            : isObserverForm
                ? "An observed property supplies the storage"
                : "A write-only auto-property supplies the storage";
        var remedy = isMixedForm || isObserverForm
            ? $"('property {propName}: ... = ...'); the accessors go on writing '{receiver}{collision.Name}'"
            : $"('property set {propName}: ... = ...')";

        AddError(_context,
            $"Property '{propName}' {where} synthesizes the backing field '{backingField}', "
            + $"which the declared field '{collision.Name}' already occupies. {supplier} — drop the "
            + $"field declaration and move its default onto the property {remedy}",
            collision.LineStart, collision.ColumnStart,
            code: DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty,
            span: collision.Span);
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
        bool isAbstract = Shared.MemberClassification.HasAbstractDecorator(propDef.Decorators);
        if (!isAbstract || !propDef.IsFunctionStyle)
            return;

        bool isEllipsisBody = AstHelper.IsAbstractStubBody(propDef.Body);

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

        bool isAbstract = Shared.MemberClassification.HasAbstractDecorator(propDef.Decorators);
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
    private void ValidatePropertyOverride(
        Statement declaration, string typeName, PropertyDef propDef, TypeSymbol? typeSymbol)
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

        var (baseProp, baseOwner) = TypeHierarchyService.FindProperty(baseType, propDef.Name);
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

        // A property is named by the method rule (NameCasing.ResolveMethod), so a mixed-escape
        // override emits a different C# name than the property it overrides — refused by name, as the
        // method twin is (SPY0248), rather than as CS0115 behind SPY0908 (#2033).
        if (baseOwner?.ClrType == null
            && Shared.MemberClassification.MethodSpellingsDiffer(
                propDef.Name, baseProp.IsNameBacktickEscaped, propDef.IsNameBacktickEscaped))
        {
            AddError(_context,
                $"Property {Shared.MemberClassification.Spelling(propDef.Name, propDef.IsNameBacktickEscaped)} "
                + $"overrides '{baseOwner?.Name}.{propDef.Name}', which is declared as "
                + $"{Shared.MemberClassification.Spelling(baseProp.Name, baseProp.IsNameBacktickEscaped)}; "
                + "implement it with the same spelling",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.Validation.InvalidPropertyOverride,
                span: propDef.Span);
            return;
        }

        // Check covariant return type: the derived property type must be assignable to the base property type
        var derivedPropSymbol = typeSymbol.FindProperty(propDef.Name, propDef.ExplicitInterface);
        if (derivedPropSymbol != null && baseProp.Type is not UnknownType && derivedPropSymbol.Type is not UnknownType)
        {
            if (!derivedPropSymbol.Type.IsAssignableTo(baseProp.Type))
            {
                AddError(_context,
                    $"Property '{propDef.Name}' in '{typeName}' has type '{derivedPropSymbol.Type.GetDisplayName()}' which is not compatible with base property type '{baseProp.Type.GetDisplayName()}'",
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
        if (Shared.MemberClassification.HasAbstractDecorator(propDef.Decorators))
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
            case MatchStatement matchStmt:
                foreach (var matchCase in matchStmt.Cases)
                    yield return matchCase.Body;
                break;
            case DeferStatement deferStmt:
                yield return deferStmt.Body;
                break;
            case DecoratedStatement decorated:
                // @suppress wrapper (#1024): suppression is warning-only metadata; the readonly
                // assignment *error* inside must still be found (errors are never suppressible).
                yield return new[] { decorated.Statement };
                break;
            default:
                // walker-default-contract: any kind not listed above is deliberately ignored by
                // this walker (rostered in DispatchSiteInventoryTests).
                break;
        }
    }
}
