using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

internal partial class NameResolver
{
    private void ResolveFunctionDeclaration(FunctionDef functionDef)
    {
        _logger.LogDebug($"Resolving function declaration: {functionDef.Name}");

        // Add parameters to the function symbol (types will be resolved later by TypeChecker)
        var parameters = functionDef.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            IsNameBacktickEscaped = p.IsNameBacktickEscaped,
            Type = SemanticType.Unknown,  // Will be resolved during type checking
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic,
            IsPositionalOnly = p.Kind == ParameterKind.PositionalOnly,
            IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly,
            IsLateBound = p.IsLateBound,
            Modifier = p.Modifier
        }).ToList();

        var funcSymbol = new FunctionSymbol
        {
            Name = functionDef.Name,
            Kind = SymbolKind.Function,
            IsNameBacktickEscaped = functionDef.IsNameBacktickEscaped,
            AccessLevel = AccessLevel.Public,
            Parameters = parameters,
            TypeParameters = functionDef.TypeParameters.ToList(),
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = functionDef.Span,
            DeclarationLine = functionDef.LineStart,
            DeclarationColumn = functionDef.ColumnStart,
            NameDeclarationLine = functionDef.NameLineStart,
            NameDeclarationColumn = functionDef.NameColumnStart,
            NameDeclarationColumnEnd = functionDef.NameColumnEnd,
            SignatureKey = GetMethodSignatureKey(functionDef),
            Documentation = functionDef.DocString,
            DeprecationMessage = GetDeprecationMessage(functionDef.Decorators),
            IsMustUse = HasMustUse(functionDef.Decorators)
        };

        var existingSymbol = _symbolTable.Lookup(functionDef.Name, searchParents: false);
        if (existingSymbol != null)
        {
            // If this function was already registered by the pre-pass, skip it
            if (existingSymbol is FunctionSymbol && existingSymbol.DeclarationSpan == functionDef.Span)
            {
                return;
            }

            // Allow shadowing builtins (which have no source location)
            // This matches Python behavior where user code can shadow builtins
            bool isBuiltin = existingSymbol.DeclarationLine == null;
            if (isBuiltin)
            {
                // Replace the builtin symbol with the user-defined function
                _symbolTable.Define(funcSymbol);
                return;
            }

            // Module-level function overloading by arity/signature:
            // when the existing symbol is a user-defined function, allow additional
            // overloads as long as they have distinct signatures.
            if (existingSymbol is FunctionSymbol existingFunc)
            {
                // Reuse the existing overload list if one was already registered,
                // otherwise seed a new list with the primary (first) symbol.
                var overloads = _symbolTable.LookupFunctionOverloads(functionDef.Name)
                    ?? new List<FunctionSymbol> { existingFunc };

                // If this exact overload was already registered (e.g., by an earlier
                // resolution pass over the same FunctionDef), skip re-registering it.
                if (overloads.Any(o => o.DeclarationSpan == functionDef.Span))
                {
                    return;
                }

                bool isDuplicate = overloads.Any(o => o.SignatureKey == funcSymbol.SignatureKey);
                if (isDuplicate)
                {
                    AddError($"Function '{functionDef.Name}' is already defined with the same signature",
                        functionDef.LineStart, functionDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.DuplicateDefinition, span: functionDef.Span);
                    return;
                }

                overloads.Add(funcSymbol);
                // The first overload remains the primary symbol in the symbol table;
                // all overloads (including the primary) are tracked in the overload list.
                _symbolTable.DefineFunctionOverloads(functionDef.Name, overloads);
                _logger.LogDebug($"Registered module-level function overload: {functionDef.Name} (params: {functionDef.Parameters.Length})");
                return;
            }

            // Existing symbol is not a function (e.g., variable, type) — genuine duplicate.
            AddError($"Function '{functionDef.Name}' is already defined",
                functionDef.LineStart, functionDef.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: functionDef.Span);
            return;
        }

        _symbolTable.Define(funcSymbol);
    }

    private void ResolveMethodDeclaration(FunctionDef method, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving method declaration: {owningType.Name}.{method.Name}");

        var classification = Shared.MemberClassification.Classify(method, owningType.TypeKind, owningType.IsAbstract);
        var accessLevel = classification.Access;
        bool isStatic = classification.IsStatic;
        bool isAbstract = classification.IsAbstract;
        bool isVirtual = classification.IsVirtual;
        bool isOverride = classification.IsOverride;

        // Add parameters to the method symbol (types will be resolved later by TypeChecker)
        var parameters = method.Parameters.Select(p => new ParameterSymbol
        {
            Name = p.Name,
            IsNameBacktickEscaped = p.IsNameBacktickEscaped,
            Type = SemanticType.Unknown,  // Will be resolved during type checking
            HasDefault = p.DefaultValue != null,
            DefaultValue = p.DefaultValue,
            IsVariadic = p.IsVariadic,
            IsPositionalOnly = p.Kind == ParameterKind.PositionalOnly,
            IsKeywordOnly = p.Kind == ParameterKind.KeywordOnly,
            IsLateBound = p.IsLateBound,
            Modifier = p.Modifier
        }).ToList();

        var funcSymbol = new FunctionSymbol
        {
            Name = method.Name,
            Kind = SymbolKind.Function,
            IsNameBacktickEscaped = method.IsNameBacktickEscaped,
            AccessLevel = accessLevel,
            ExplicitAccessLevel = classification.ExplicitAccess,
            Parameters = parameters,
            TypeParameters = method.TypeParameters.ToList(),
            IsStatic = isStatic,
            IsAbstract = isAbstract,
            IsVirtual = isVirtual,
            IsOverride = isOverride,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = method.Span,
            DeclarationLine = method.LineStart,
            DeclarationColumn = method.ColumnStart,
            NameDeclarationLine = method.NameLineStart,
            NameDeclarationColumn = method.NameColumnStart,
            NameDeclarationColumnEnd = method.NameColumnEnd,
            SignatureKey = GetMethodSignatureKey(method),
            Documentation = method.DocString,
            DeprecationMessage = GetDeprecationMessage(method.Decorators),
            IsMustUse = HasMustUse(method.Decorators)
        };

        owningType.Methods.Add(funcSymbol);

        // The member seam (P21g D2, #2360): the first method of a name claims it; a later method of
        // the name is an OVERLOAD (the holder is a method — constructors, operator/protocol dunders
        // and regular methods alike); any other holder is a second meaning, SPY0204 at this method's
        // name. A refused method stays in Methods so the checker still sees its body — the scope
        // keeps the FIRST meaning (D10).
        if (TryDeclareMember(method.Name, funcSymbol, owningType, mergesWith: null,
                method.NameLineStart, method.NameColumnStart, span: null) == MemberClaim.Overload)
        {
            _logger.LogDebug($"Method overload registered (not in symbol table): {owningType.Name}.{method.Name}");
        }

        // Constructors (__init__) may be overloaded; every overload is tracked in Constructors.
        if (method.Name == DunderNames.Init)
        {
            owningType.Constructors.Add(funcSymbol);
            _logger.LogDebug($"Registered constructor overload: {owningType.Name}.__init__ (params: {method.Parameters.Length})");
        }

        // Track regular method overloads in MethodOverloads dictionary
        if (!DunderDetector.IsDunderMethod(method.Name))
        {
            if (!owningType.MethodOverloads.TryGetValue(method.Name, out var methodOverloads))
            {
                methodOverloads = new List<FunctionSymbol>();
                owningType.MethodOverloads[method.Name] = methodOverloads;
            }

            // Check for duplicate signatures (same parameter count and type annotations)
            // Build a signature key from parameter type annotation names
            var newSignature = GetMethodSignatureKey(method);
            bool isDuplicate = methodOverloads.Any(existing =>
                existing.SignatureKey == newSignature);

            if (isDuplicate)
            {
                AddError($"Method '{owningType.Name}.{method.Name}' is already defined with the same signature",
                    method.LineStart, method.ColumnStart,
                    code: DiagnosticCodes.Semantic.DuplicateMethodSignature);
            }
            else
            {
                methodOverloads.Add(funcSymbol);
            }
        }

        // Register operator/protocol dunder methods in their tables (validation moved to
        // SignatureValidator) — the rule a cache restore re-applies (TypeSymbol.DeriveMethodTables).
        if (owningType.RegisterDunderMethod(funcSymbol) is { } table)
        {
            _logger.LogDebug($"Registered {table} method: {owningType.Name}.{method.Name}");
        }
    }

    /// <summary>
    /// Builds a signature key from a method's AST parameter type annotations (excluding self).
    /// Used for detecting duplicate method signatures during overload registration.
    /// </summary>
    /// <remarks>
    /// Internal rather than private because <c>ModuleLoader</c> stamps the same key onto imported
    /// functions (#1365). Two keys computed by two formulas would compare unequal for the same
    /// signature, which is the #1145 Class G shape — so the formula stays here and the import path
    /// CALLS it rather than mirroring it.
    /// </remarks>
    internal static string GetMethodSignatureKey(FunctionDef method)
    {
        var paramTypes = method.Parameters
            .Where(p => p.Name != PythonNames.Self)
            .Select(p => TypeAnnotationKey.Of(p.Type));
        return string.Join(",", paramTypes);
    }

    private void ResolveFieldDeclaration(VariableDeclaration field, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving field declaration: {owningType.Name}.{field.Name}");

        // The same authority ModuleLoader.ExtractFields consumes, so a declared field and an
        // imported one are classified by one formula rather than two (#1441, the #1267 rule
        // extended from methods to fields).
        var classification = Shared.MemberClassification.ClassifyField(field, owningType.TypeKind);
        var accessLevel = classification.Access;
        var explicitAccess = classification.ExplicitAccess;
        bool isStatic = classification.IsStatic;
        bool isFinal = classification.IsFinal;

        var varSymbol = new VariableSymbol
        {
            Name = field.Name,
            Kind = SymbolKind.Variable,
            IsNameBacktickEscaped = field.IsNameBacktickEscaped,
            AccessLevel = accessLevel,
            ExplicitAccessLevel = explicitAccess,
            IsConstant = field.IsConst,
            IsStatic = isStatic,
            IsFinal = isFinal,
            HasDefaultValue = field.InitialValue != null,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = field.Span,
            DeclarationLine = field.LineStart,
            DeclarationColumn = field.ColumnStart,
            NameDeclarationLine = field.NameLineStart,
            NameDeclarationColumn = field.NameColumnStart,
            NameDeclarationColumnEnd = field.NameColumnEnd
        };

        // The member seam (P21g D2, #2360): a field over ANY same-named member — a field included
        // (field × field was a silent Scope.Define replacement and CS0102 under run) — is SPY0204 at
        // the field's name. A const field takes the keyword position, which is its statement start.
        var (line, column) = field.IsConst
            ? (field.LineStart, field.ColumnStart)
            : (field.NameLineStart, field.NameColumnStart);
        if (TryDeclareMember(field.Name, varSymbol, owningType, mergesWith: null, line, column, field.Span)
            != MemberClaim.Declared)
            return;

        // An interface's NON-const field claims its name like every member, but is not a Fields
        // entry: the interface walk never registered one (only its consts), and what such a field
        // IS for the checker is outside this seam (findings ledger, P21g Phase 1).
        if (owningType.TypeKind != TypeKind.Interface || field.IsConst)
            owningType.Fields.Add(varSymbol);
    }

    private void ResolvePropertyDeclaration(PropertyDef propDef, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving property declaration: {owningType.Name}.{propDef.Name}");

        // The member seam (P21g D2 (b), #2360): a property over a same-named PROPERTY merges when it
        // brings the complementary half (AccessorDeclarations.Admits); a duplicate half, or any other
        // holder, is SPY0204 at the property keyword. The key is (Name, ExplicitInterface) (R-JH,
        // #2390): an explicit-interface implementation (`property IShape.area`) is not a member of the
        // type's own namespace — C# reaches it only through the interface — so it claims no bare name
        // and collides only with another half of the same explicit key.
        var form = AccessorDeclarations.FormOf(propDef);
        var claim = TryDeclareMember(propDef.Name, member: null, owningType,
            mergesWith: holder => holder is PropertySymbol
                && AccessorDeclarationsOf(owningType, propDef.Name, propDef.ExplicitInterface) is { IsEvent: false } declared
                && declared.Admits(form),
            propDef.LineStart, propDef.ColumnStart, propDef.Span, propDef.ExplicitInterface);
        if (claim == MemberClaim.Refused)
        {
            owningType.RefuseDeclaration(propDef);
            return;
        }
        if (claim == MemberClaim.Declared)
            RecordAccessorDeclarations(owningType, propDef.Name, propDef.ExplicitInterface, isEvent: false, propDef.LineStart);
        var accessors = AccessorDeclarationsOf(owningType, propDef.Name, propDef.ExplicitInterface)!;
        var body = AccessorDeclarations.BodyOf(propDef, owningType);

        // R-JG (#2382): an interface's accessors are all abstract or all default
        // implementations — `{ get; set { … } }` is a field-backed semi-auto property to C#
        // (CS0525). So a complementary half that leaves one accessor abstract and the other
        // bodied is refused at the later half, whichever order the halves come in. Like a
        // refused claim, the half is not added.
        if (claim == MemberClaim.Merged
            && owningType.TypeKind == TypeKind.Interface
            && accessors.MixesAbstractWithBodied(form, body))
        {
            AddError(
                $"'{propDef.Name}' mixes an abstract accessor with an accessor that has a body in interface "
                + $"'{owningType.Name}' (line {accessors.Line}): C# interface accessors are all abstract or all "
                + "default implementations — give both halves a body, or make both abstract ('...')",
                propDef.LineStart, propDef.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.InterfaceAccessorsMustAgreeOnBody, span: propDef.Span);
            owningType.RefuseDeclaration(propDef);
            return;
        }

        accessors.Add(form, body);

        // The record a merge REPLACES (getter + setter combined into one PropertySymbol). Not a
        // verdict: whether this declaration may join it was MemberHolderOf's and Admits' above; this
        // finds the object to rewrite, by the same (Name, ExplicitInterface) key.
        var existingProp = owningType.FindProperty(propDef.Name, propDef.ExplicitInterface);

        bool isStatic = propDef.Decorators.Any(d => d.Name == DecoratorNames.Static)
            || !propDef.Parameters.Any(p => string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            && propDef.IsFunctionStyle;
        bool isVirtual = propDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isAbstract = Shared.MemberClassification.IsAbstract(
            propDef, owningType.TypeKind, owningType.IsAbstract);
        bool isOverride = propDef.Decorators.Any(d => d.Name == DecoratorNames.Override);
        bool isFinal = propDef.Decorators.Any(d => d.Name == DecoratorNames.Final);

        bool hasGetter = propDef.Accessor == PropertyAccessor.Get || propDef.Accessor == PropertyAccessor.None;
        bool hasSetter = propDef.Accessor == PropertyAccessor.Set;
        bool hasInit = propDef.Accessor == PropertyAccessor.Init;

        // The one access rule with the host axis (#1937) — a struct's `_p` is private, not protected.
        var (accessLevel, _) = Shared.MemberClassification.ClassifyAccess(
            propDef.Name, propDef.Decorators, owningType.TypeKind, propDef.IsNameBacktickEscaped);

        if (existingProp != null)
        {
            // Merge: combine accessor info from additional PropertyDef
            // This handles the case where getter and setter are defined separately
            var merged = existingProp with
            {
                HasGetter = existingProp.HasGetter || hasGetter,
                HasSetter = existingProp.HasSetter || hasSetter,
                HasInit = existingProp.HasInit || hasInit,
                GetterAccess = hasGetter && !existingProp.HasGetter ? accessLevel : existingProp.GetterAccess,
                SetterAccess = hasSetter || hasInit ? accessLevel : existingProp.SetterAccess,
            };

            // Replace existing in the list
            var index = owningType.Properties.IndexOf(existingProp);
            owningType.Properties[index] = merged;
        }
        else
        {
            var propSymbol = new PropertySymbol
            {
                Name = propDef.Name,
                IsNameBacktickEscaped = propDef.IsNameBacktickEscaped,
                HasGetter = hasGetter,
                HasSetter = hasSetter,
                HasInit = hasInit,
                IsStatic = isStatic,
                IsVirtual = isVirtual,
                IsAbstract = isAbstract,
                IsOverride = isOverride,
                IsFinal = isFinal,
                GetterAccess = hasGetter ? accessLevel : AccessLevel.Public,
                SetterAccess = hasSetter || hasInit ? accessLevel : AccessLevel.Public,
                ExplicitInterface = propDef.ExplicitInterface,
                Observers = propDef.Observers,
            };

            owningType.Properties.Add(propSymbol);
        }
    }

    private void ResolveEventDeclaration(EventDef eventDef, TypeSymbol owningType)
    {
        _logger.LogDebug($"Resolving event declaration: {owningType.Name}.{eventDef.Name}");

        // The member seam (P21g D2 (c), #2360): an event over a same-named EVENT merges only when it
        // is the complementary function-style accessor (`event add` beside `event remove`); an auto
        // event twice — silently merged away before, so `event e: Action[int]` + `event e:
        // Action[str]` ran with the second type dropped — or any other holder is SPY0204 at the
        // event keyword.
        var form = AccessorDeclarations.FormOf(eventDef);
        var claim = TryDeclareMember(eventDef.Name, member: null, owningType,
            mergesWith: holder => holder is EventSymbol
                && AccessorDeclarationsOf(owningType, eventDef.Name, explicitInterface: null) is { IsEvent: true } declared
                && declared.Admits(form),
            eventDef.LineStart, eventDef.ColumnStart, eventDef.Span);
        if (claim == MemberClaim.Refused)
        {
            owningType.RefuseDeclaration(eventDef);
            return;
        }
        if (claim == MemberClaim.Declared)
            RecordAccessorDeclarations(owningType, eventDef.Name, explicitInterface: null, isEvent: true, eventDef.LineStart);
        AccessorDeclarationsOf(owningType, eventDef.Name, explicitInterface: null)!.Add(form);

        // The record a merge REPLACES (add + remove combined into one EventSymbol). Not a verdict:
        // MemberHolderOf and Admits above decided whether this declaration may join it.
        var existingEvent = owningType.Events.FirstOrDefault(e => e.Name == eventDef.Name);

        bool isStatic = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Static)
            || eventDef.IsFunctionStyle
            && !eventDef.Parameters.Any(p => string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        bool isVirtual = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isAbstract = Shared.MemberClassification.IsAbstract(
            eventDef, owningType.TypeKind, owningType.IsAbstract);
        bool isOverride = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Override);
        bool isFinal = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Final);

        bool hasAdd = eventDef.Accessor == EventAccessor.Add;
        bool hasRemove = eventDef.Accessor == EventAccessor.Remove;

        // Auto-events have both add and remove implicitly
        if (!eventDef.IsFunctionStyle)
        {
            hasAdd = true;
            hasRemove = true;
        }

        // The one access rule with the host axis (#1937) — a struct's `_e` is private, not protected.
        var (accessLevel, _) = Shared.MemberClassification.ClassifyAccess(
            eventDef.Name, eventDef.Decorators, owningType.TypeKind, eventDef.IsNameBacktickEscaped);

        if (existingEvent != null)
        {
            // Merge: combine accessor info from additional EventDef (add + remove pair)
            var merged = existingEvent with
            {
                HasAdd = existingEvent.HasAdd || hasAdd,
                HasRemove = existingEvent.HasRemove || hasRemove,
                AddAccessLevel = hasAdd ? accessLevel : existingEvent.AddAccessLevel,
                RemoveAccessLevel = hasRemove ? accessLevel : existingEvent.RemoveAccessLevel,
            };

            // Replace existing in the list
            var index = owningType.Events.IndexOf(existingEvent);
            owningType.Events[index] = merged;
        }
        else
        {
            var eventSymbol = new EventSymbol
            {
                Name = eventDef.Name,
                IsNameBacktickEscaped = eventDef.IsNameBacktickEscaped,
                HasAdd = hasAdd,
                HasRemove = hasRemove,
                IsStatic = isStatic,
                IsVirtual = isVirtual,
                IsAbstract = isAbstract,
                IsOverride = isOverride,
                IsFinal = isFinal,
                AccessLevel = accessLevel,
                AddAccessLevel = hasAdd ? accessLevel : AccessLevel.Public,
                RemoveAccessLevel = hasRemove ? accessLevel : AccessLevel.Public,
            };

            owningType.Events.Add(eventSymbol);
        }
    }

    private void ResolveConstantDeclaration(VariableDeclaration constDecl)
    {
        _logger.LogDebug($"Resolving constant declaration: {constDecl.Name}");

        if (_symbolTable.Lookup(constDecl.Name, searchParents: false) != null)
        {
            AddError($"Constant '{constDecl.Name}' is already defined",
                constDecl.LineStart, constDecl.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: constDecl.Span);
            return;
        }

        var varSymbol = new VariableSymbol
        {
            Name = constDecl.Name,
            Kind = SymbolKind.Variable,
            IsNameBacktickEscaped = constDecl.IsNameBacktickEscaped,
            AccessLevel = AccessLevel.Public,
            IsConstant = true,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = constDecl.Span,
            DeclarationLine = constDecl.LineStart,
            DeclarationColumn = constDecl.ColumnStart,
            NameDeclarationLine = constDecl.NameLineStart,
            NameDeclarationColumn = constDecl.NameColumnStart,
            NameDeclarationColumnEnd = constDecl.NameColumnEnd
        };

        _symbolTable.Define(varSymbol);
    }

    private void ResolveModulePropertyDeclaration(PropertyDef propDef)
    {
        _logger.LogDebug($"Resolving module-level property declaration: {propDef.Name}");

        // Mirror class-level accessor classification (see ResolvePropertyDeclaration):
        // auto-properties (Accessor.None) and `property get` declare a getter,
        // `property set` declares a setter. An `init` accessor records neither —
        // it is meaningless at module level (no construction phase) and is
        // rejected during validation, not during name binding.
        bool hasGetter = propDef.Accessor is PropertyAccessor.Get or PropertyAccessor.None;
        bool hasSetter = propDef.Accessor == PropertyAccessor.Set;

        var existingSymbol = _symbolTable.Lookup(propDef.Name, searchParents: false);
        if (existingSymbol != null)
        {
            // Merge accessors when the getter and setter are declared as separate
            // `property get`/`property set` definitions for the same name.
            if (existingSymbol is VariableSymbol { IsModuleProperty: true } existingProp)
            {
                // If this exact declaration was already resolved (e.g., re-resolution
                // of the same AST), skip it — mirrors ResolveFunctionDeclaration.
                if (existingProp.DeclarationSpan == propDef.Span)
                    return;

                // The type-body merge rule (D2 (b), AccessorDeclarations.Admits): a complementary half
                // merges — at most one auto and one function-style half per accessor slot — and a slot
                // filled twice in one category is a duplicate. The accessor FLAGS cannot tell an auto property from a function-style
                // getter (both set HasPropertyGetter), which refused `property A: int = 5` + `property
                // get A()` while `property set A()` merged; the recorded forms can.
                var form = AccessorDeclarations.FormOf(propDef);
                bool duplicateAccessor = _modulePropertyAccessors.TryGetValue(existingProp, out var declared)
                    ? !declared.Admits(form)
                    : (hasGetter && existingProp.HasPropertyGetter) || (hasSetter && existingProp.HasPropertySetter);
                if (duplicateAccessor)
                {
                    AddError($"Property '{propDef.Name}' is already defined",
                        propDef.LineStart, propDef.ColumnStart,
                        code: DiagnosticCodes.Semantic.DuplicateDefinition, span: propDef.Span);
                    return;
                }

                declared?.Add(form);
                existingProp.HasPropertyGetter |= hasGetter;
                existingProp.HasPropertySetter |= hasSetter;
                return;
            }

            // Allow shadowing builtins (which have no source location),
            // matching module-level function resolution.
            bool isBuiltin = existingSymbol.DeclarationLine == null;
            if (!isBuiltin)
            {
                AddError($"Property '{propDef.Name}' is already defined",
                    propDef.LineStart, propDef.ColumnStart,
                    code: DiagnosticCodes.Semantic.DuplicateDefinition, span: propDef.Span);
                return;
            }
        }

        var propSymbol = new VariableSymbol
        {
            Name = propDef.Name,
            Kind = SymbolKind.Variable,
            IsNameBacktickEscaped = propDef.IsNameBacktickEscaped,
            AccessLevel = AccessLevel.Public,
            // Module-level members are implicitly static (emitted on the static module class)
            IsStatic = true,
            IsModuleProperty = true,
            HasPropertyGetter = hasGetter,
            HasPropertySetter = hasSetter,
            HasDefaultValue = propDef.DefaultValue != null,
            DeclaringFilePath = _currentFilePath,
            DeclarationSpan = propDef.Span,
            DeclarationLine = propDef.LineStart,
            DeclarationColumn = propDef.ColumnStart,
            NameDeclarationLine = propDef.NameLineStart,
            NameDeclarationColumn = propDef.NameColumnStart,
            NameDeclarationColumnEnd = propDef.NameColumnEnd
        };

        _symbolTable.Define(propSymbol);
        var accessors = new AccessorDeclarations(isEvent: false, propDef.LineStart);
        accessors.Add(AccessorDeclarations.FormOf(propDef));
        _modulePropertyAccessors[propSymbol] = accessors;
    }

    private void ResolveTypeAliasDeclaration(TypeAlias typeAlias)
    {
        _logger.LogDebug($"Resolving type alias declaration: {typeAlias.Name}");

        // Check for redefinition
        if (_symbolTable.Lookup(typeAlias.Name, searchParents: false) != null)
        {
            AddError($"Type alias '{typeAlias.Name}' is already defined",
                typeAlias.LineStart, typeAlias.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateDefinition, span: typeAlias.Span);
            return;
        }

        // Validate that exactly one of Type or FunctionType is set
        if (typeAlias.Type == null && typeAlias.FunctionType == null)
        {
            AddError($"Type alias '{typeAlias.Name}' must have a type",
                typeAlias.LineStart, typeAlias.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        if (typeAlias.Type != null && typeAlias.FunctionType != null)
        {
            AddError($"Type alias '{typeAlias.Name}' cannot have both Type and FunctionType",
                typeAlias.LineStart, typeAlias.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        _symbolTable.Define(TypeAliasSymbol.CreateFrom(typeAlias, _currentFilePath));
    }

    // -----------------------------------------------------------------------------------------------
    // The member-declaration seam (P21g D2, #2360, R-JB): in one type body a name has ONE meaning.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The ONE dispatcher a type body's member statements go through — the class, struct, interface
    /// and union walks each hand it their statements (the union walk only its methods: a union body
    /// spells nothing else, SPY0104), so a member kind is registered by one arm in every host and
    /// cannot skip <see cref="TryDeclareMember"/> in a host whose walk forgot it. Per-host LEGALITY
    /// (a <c>let</c>, an interface method's body) stays where it is; this only registers.
    /// </summary>
    private void ResolveTypeBodyMember(Statement statement, TypeSymbol owner)
    {
        switch (statement)
        {
            case FunctionDef method:
                // An interface method may not carry an implementation (only `...` or `pass`).
                if (owner.TypeKind == TypeKind.Interface)
                    ValidateInterfaceMethod(method, owner.Name);
                ResolveMethodDeclaration(method, owner);
                break;

            case VariableDeclaration { IsLet: true }:
                // A `let` in a type body is refused (SPY0340) by TypeBodyStatementValidator; it
                // must not also register a field (#1974).
                break;

            case VariableDeclaration field:
                ResolveFieldDeclaration(field, owner);
                break;

            case PropertyDef propDef:
                ResolvePropertyDeclaration(propDef, owner);
                break;

            case EventDef eventDef:
                ResolveEventDeclaration(eventDef, owner);
                break;

            default:
                // One classifier decides the seven type-declaring kinds (#1729, R-I); anything else in
                // a type body is a statement later passes own.
                if (statement.TryGetNestedDeclaration(out _))
                    ResolveNestedTypeDeclaration(statement, owner);
                break;
        }
    }

    /// <summary>What <see cref="TryDeclareMember"/> did with a member's name.</summary>
    private enum MemberClaim
    {
        /// <summary>The name was free; the member now holds it.</summary>
        Declared,

        /// <summary>A method of the name already holds it, and the incoming member is a method.</summary>
        Overload,

        /// <summary>The holder accepted the incoming declaration as its complementary accessor half.</summary>
        Merged,

        /// <summary>A second meaning: reported SPY0204 at the incoming declaration.</summary>
        Refused,
    }

    /// <summary>
    /// The only place a type-body member's NAME is claimed. The holder decides: (a) a method holder
    /// and a method incoming is an overload (same-signature duplicates stay SPY0355, the overload
    /// table's rule); (b)/(c) a property or event holder that <paramref name="mergesWith"/> accepts is
    /// the complementary accessor half; (d) any other holder — field over field included — is SPY0204
    /// at the LATER declaration, at <paramref name="line"/>:<paramref name="column"/> (D2's per-kind
    /// position: a method's name token, a field's name, every other kind's first token).
    /// </summary>
    /// <param name="member">The symbol entering the type scope, or null for a member that is not a
    /// <see cref="Symbol"/> (a property or event — it lives in the owner's Properties/Events —, or a
    /// nested type whose own resolver defines its symbol).</param>
    /// <param name="explicitInterface">The interface a property implements explicitly
    /// (<c>property IShape.area</c>), or null for a member of the type's own namespace — every other
    /// kind. With the name it is the member key (R-JH, #2390): an explicit member collides only with
    /// the same key.</param>
    /// <remarks>
    /// Properties and events are standalone records, not <see cref="Symbol"/>s, so they cannot enter
    /// the scope; <see cref="MemberHolderOf"/> is the ONE holder lookup and reads the scope, then the
    /// owner's Properties, then its Events. Mutating this method's refusal arm away turns every
    /// refused member pair green-silent in every host — that is the guard's mutation (Phase 2 (a)).
    /// </remarks>
    private MemberClaim TryDeclareMember(
        string name, Symbol? member, TypeSymbol owner, Func<object, bool>? mergesWith,
        int line, int column, Text.TextSpan? span, string? explicitInterface = null)
    {
        // A holder with no source position is a builtin, which a declaration may shadow
        // (Scope.Define replaces it) — a type scope holds none today; the rule is Scope.Define's.
        if (MemberHolderOf(name, explicitInterface, owner) is not { } holder
            || holder.Holder is Symbol { DeclarationLine: null })
        {
            if (member != null)
                _symbolTable.Define(member);
            return MemberClaim.Declared;
        }

        if (member is FunctionSymbol && holder.Holder is FunctionSymbol)
            return MemberClaim.Overload;

        if (mergesWith != null && mergesWith(holder.Holder))
            return MemberClaim.Merged;

        AddError(MemberConflictMessage(name, owner, holder.Kind, holder.Line), line, column,
            code: DiagnosticCodes.Semantic.DuplicateDefinition, span: span);
        return MemberClaim.Refused;
    }

    /// <summary>The one SPY0204 text for a second meaning of a name in a type body.</summary>
    private static string MemberConflictMessage(string name, TypeSymbol owner, string holderKind, int? holderLine)
        => $"'{name}' is already declared in '{owner.Name}' as {holderKind}"
           + (holderLine is int priorLine ? $" (line {priorLine})" : string.Empty)
           + "; one name has one meaning in a type body";

    /// <summary>What holds a name in the type body being walked, and how the refusal names it.</summary>
    private readonly record struct MemberHolder(object Holder, string Kind, int? Line);

    /// <summary>
    /// The ONE holder lookup of the seam: the type scope (methods, fields, consts, nested types,
    /// aliases, type parameters, union cases) ?? the owner's <see cref="TypeSymbol.Properties"/> ??
    /// its <see cref="TypeSymbol.Events"/>. PropertySymbol and EventSymbol are not
    /// <see cref="Symbol"/>s (standalone records), so they never enter a scope and are read where
    /// they live (D2 correction, lead ruling (a)); no arm consults the scope or those lists for a
    /// verdict on its own. The key is (<paramref name="name"/>, <paramref name="explicitInterface"/>)
    /// (R-JH, #2390): an explicit-interface property (<c>property IShape.area</c>) is not a member of
    /// the type's own namespace, so the bare key reads the scope, the own properties
    /// (<see cref="TypeSymbol.FindOwnProperty"/>) and the events, and an explicit key reads only the
    /// owner's property of that key. The accessor-slot record
    /// (<see cref="AccessorDeclarations"/>) is the merge's bookkeeping, read only once a property or
    /// event holder was found here — for <see cref="AccessorDeclarations.Admits"/> and the line the
    /// refusal quotes.
    /// </summary>
    private MemberHolder? MemberHolderOf(string name, string? explicitInterface, TypeSymbol owner)
    {
        if (explicitInterface != null)
        {
            return owner.FindProperty(name, explicitInterface) is { } explicitProperty
                ? new MemberHolder(explicitProperty, $"the explicit implementation of '{explicitInterface}.{name}'",
                    AccessorDeclarationsOf(owner, name, explicitInterface)?.Line)
                : null;
        }

        if (_symbolTable.Lookup(name, searchParents: false) is { } symbol)
            return new MemberHolder(symbol, MemberKindPhrase(symbol, owner), symbol.DeclarationLine);

        if (owner.FindOwnProperty(name) is { } property)
            return new MemberHolder(property, "a property", AccessorDeclarationsOf(owner, name, explicitInterface: null)?.Line);

        if (owner.Events.FirstOrDefault(e => e.Name == name) is { } @event)
            return new MemberHolder(@event, "an event", AccessorDeclarationsOf(owner, name, explicitInterface: null)?.Line);

        return null;
    }

    /// <summary>How the refusal names the member already holding a name.</summary>
    private static string MemberKindPhrase(Symbol holder, TypeSymbol owner) => holder switch
    {
        VariableSymbol { IsConstant: true } => "a constant",
        VariableSymbol => "a field",
        FunctionSymbol => "a method",
        TypeAliasSymbol => "a type alias",
        TypeParameterSymbol => "a type parameter",
        TypeSymbol nested when owner.UnionCases.Contains(nested) => "a union case",
        TypeSymbol { TypeKind: TypeKind.Struct } => "a nested struct",
        TypeSymbol { TypeKind: TypeKind.Interface } => "a nested interface",
        TypeSymbol { TypeKind: TypeKind.Enum } => "a nested enum",
        TypeSymbol { TypeKind: TypeKind.Union } => "a nested union",
        TypeSymbol { TypeKind: TypeKind.Delegate } => "a nested delegate",
        TypeSymbol => "a nested class",
        _ => "a member",
    };

    /// <summary>
    /// The accessor halves declared so far for one property or event name, as the accessor SLOTS
    /// they fill, and the first declaration's line. A half is AUTO (field-style: it declares the type
    /// and default and owns the backing field) or FUNCTION-STYLE (a body), and fills the getter slot,
    /// the setter slot, or both: <c>property A: T</c> is an auto half of both slots, <c>property get A:
    /// T</c> an auto getter, <c>property set A: T</c> / <c>property init A: T</c> an auto setter,
    /// <c>property get A(self)</c> a function-style getter, <c>property set A(self, v)</c> a
    /// function-style setter. The merged <see cref="PropertySymbol"/> flags cannot tell these apart
    /// (an auto property and a function-style getter both set <c>HasGetter</c>), so the forms are
    /// recorded here.
    /// </summary>
    private sealed class AccessorDeclarations
    {
        [Flags]
        internal enum Form
        {
            None = 0,
            AutoGet = 1,
            AutoSet = 2,
            FunctionGet = 4,
            FunctionSet = 8,
            Auto = AutoGet | AutoSet,
        }

        internal AccessorDeclarations(bool isEvent, int line)
        {
            IsEvent = isEvent;
            Line = line;
        }

        /// <summary>What a property half puts in the accessor slots it fills (D2, R-JG): no body
        /// (<see cref="Abstract"/> — an auto half without a default, a stub), a default value (an auto
        /// half with one — never judged here: an interface's defaulted auto half is #2383's), or a
        /// function-style body. Ordered so a slot's class is the strongest of the halves filling it — a
        /// function-style getter beside an auto half supplies the getter's body.</summary>
        internal enum Body
        {
            None = 0,
            Abstract = 1,
            Defaulted = 2,
            Bodied = 3,
        }

        internal bool IsEvent { get; }
        internal int Line { get; }
        private Form _filled;
        private Body _getBody;
        private Body _setBody;

        /// <summary>A property's half: the slots it fills (see the class summary) — <c>set</c> and
        /// <c>init</c> are both the write slot.</summary>
        internal static Form FormOf(PropertyDef propDef)
        {
            bool writes = propDef.Accessor is PropertyAccessor.Set or PropertyAccessor.Init;
            if (propDef.IsFunctionStyle)
                return writes ? Form.FunctionSet : Form.FunctionGet;
            return propDef.Accessor == PropertyAccessor.Get ? Form.AutoGet
                : writes ? Form.AutoSet
                : Form.Auto;
        }

        /// <summary>An event's half: an auto event fills both slots; <c>add</c> is the getter slot's
        /// function-style twin and <c>remove</c> the setter slot's.</summary>
        internal static Form FormOf(EventDef eventDef)
            => !eventDef.IsFunctionStyle ? Form.Auto
                : eventDef.Accessor == EventAccessor.Remove ? Form.FunctionSet
                : Form.FunctionGet;

        /// <summary>
        /// Whether <paramref name="incoming"/> is a complementary half (D2 (b)/(c)): it fills no slot
        /// already filled by a half of its own category. A property: at most one auto half and at most
        /// one function-style half per slot, in any combination — split auto halves (<c>property get
        /// name: str</c> + <c>@private property set name: str</c>, the spec's "Mixed Access Modifiers"),
        /// an auto half beside function-style accessors (<c>properties_function_style.md</c> "paired with
        /// an auto-property of the same name supplying the complementary half"; auto + getter + setter
        /// is the fixture <c>property_mixed_auto_both_custom</c>), a function-style getter + setter. A
        /// slot filled twice in one category is the duplicate (CS0161/CS1007 behind SPY0908 before). An
        /// event: <c>add</c> + <c>remove</c> only — an auto event already fills both slots and takes no
        /// other half.
        /// </summary>
        internal bool Admits(Form incoming)
        {
            if (IsEvent)
                return (incoming & Form.Auto) == 0 && (_filled & Form.Auto) == 0 && (_filled & incoming) == 0;

            return (_filled & incoming) == 0;
        }

        /// <summary>A property half's <see cref="Body"/>: an auto half is <see cref="Body.Defaulted"/>
        /// with a default and <see cref="Body.Abstract"/> without; a function-style half is abstract by
        /// the one abstractness rule (a stub in an interface, <c>@abstract</c>), else
        /// <see cref="Body.Bodied"/>.</summary>
        internal static Body BodyOf(PropertyDef propDef, TypeSymbol owner)
        {
            if (!propDef.IsFunctionStyle)
                return propDef.DefaultValue != null ? Body.Defaulted : Body.Abstract;
            return Shared.MemberClassification.IsAbstract(propDef, owner.TypeKind, owner.IsAbstract)
                ? Body.Abstract
                : Body.Bodied;
        }

        /// <summary>
        /// Whether adding <paramref name="incoming"/> (a half <see cref="Admits"/> accepted) would leave
        /// one accessor abstract and the other bodied — R-JG's interface refusal. Read from the slots,
        /// not the order: an auto half of both slots beside a function-style getter leaves the setter
        /// abstract; an auto getter beside a function-style getter fills ONE slot and is a body for it
        /// (the same-slot merge stays legal); a defaulted slot is neither side.
        /// </summary>
        internal bool MixesAbstractWithBodied(Form incoming, Body body)
        {
            var get = Max(_getBody, (incoming & (Form.AutoGet | Form.FunctionGet)) != 0 ? body : Body.None);
            var set = Max(_setBody, (incoming & (Form.AutoSet | Form.FunctionSet)) != 0 ? body : Body.None);
            return (get, set) is (Body.Abstract, Body.Bodied) or (Body.Bodied, Body.Abstract);
        }

        internal void Add(Form form, Body body = Body.None)
        {
            _filled |= form;
            if ((form & (Form.AutoGet | Form.FunctionGet)) != 0)
                _getBody = Max(_getBody, body);
            if ((form & (Form.AutoSet | Form.FunctionSet)) != 0)
                _setBody = Max(_setBody, body);
        }

        private static Body Max(Body a, Body b) => a > b ? a : b;
    }

    /// <summary>The merge's accessor-slot bookkeeping per type-body property or event, keyed by the owner
    /// symbol (reference identity — <see cref="Symbol"/> overrides record equality) and the member key
    /// (the name and, for an explicit-interface property, its interface — R-JH). Not a name registry: <see cref="MemberHolderOf"/> finds the holder in the owner's lists, and this
    /// is read only after it has (the slots a merged record's flags cannot tell apart, and its first
    /// line).</summary>
    private readonly Dictionary<(TypeSymbol Owner, string Name, string? ExplicitInterface), AccessorDeclarations> _accessorDeclarations = new();

    /// <summary>Accessor declarations per MODULE property symbol — the same merge rule at module level
    /// ("split accessors are merged … following the same rules as class-level split accessors").</summary>
    private readonly Dictionary<VariableSymbol, AccessorDeclarations> _modulePropertyAccessors = new();

    private void RecordAccessorDeclarations(TypeSymbol owner, string name, string? explicitInterface, bool isEvent, int line)
        => _accessorDeclarations[(owner, name, explicitInterface)] = new AccessorDeclarations(isEvent, line);

    private AccessorDeclarations? AccessorDeclarationsOf(TypeSymbol owner, string name, string? explicitInterface)
        => _accessorDeclarations.GetValueOrDefault((owner, name, explicitInterface));
}
