using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: a read is typed whatever the order (plan-777361 D2, rule N1). Every
/// declared type of a module — its annotated variables and properties beside its consts, and each
/// type's annotated members (fields, consts, properties, events, method signatures, union cases, a
/// <c>@dataclass</c>'s generated members), nested types included — is resolved before any body or
/// initializer is checked. An unannotated module-level or type-level value is checked at its first
/// read, in its own declaration's scope state, under an in-progress mark: a read that re-enters its
/// own inference stays <see cref="UnknownType"/>, as before, and adds no refusal.
/// </summary>
internal partial class TypeChecker
{
    /// <summary>A class, struct or union declared in a module body or a type body, with the type that encloses it.</summary>
    private sealed class TypeSite(Statement definition, TypeSymbol symbol, TypeSite? enclosing)
    {
        public Statement Definition { get; } = definition;
        public TypeSymbol Symbol { get; } = symbol;
        public TypeSite? Enclosing { get; } = enclosing;
        public bool Resolving { get; set; }
        public bool Resolved { get; set; }
    }

    private enum ValueState
    {
        Pending,
        InProgress,
        Done,
    }

    /// <summary>An unannotated module-level or type-level value with an initializer, and the type it is declared in.</summary>
    private sealed class ValueSite(VariableDeclaration declaration, TypeSite? host)
    {
        public VariableDeclaration Declaration { get; } = declaration;
        public TypeSite? Host { get; } = host;
        public ValueState State { get; set; }
    }

    private readonly Dictionary<TypeSymbol, TypeSite> _typeSites = new(ReferenceEqualityComparer.Instance);
    private readonly List<TypeSite> _typeSitesInOrder = new();
    private readonly Dictionary<VariableSymbol, ValueSite> _valueSites = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<VariableDeclaration, ValueSite> _valueSitesByDeclaration = new(ReferenceEqualityComparer.Instance);

    // The module scope and narrowing flow the module's own statements are checked in: the state a
    // value's first-read check restores, wherever the read stands.
    private Scope? _declarationModuleScope;
    private Analysis.ControlFlow.NarrowingFlowResult? _moduleNarrowingFlow;

    /// <summary>
    /// The pre-pass of D2: every declared type of the module before any body or initializer. Runs
    /// after the module functions' signatures and the module consts' declared types. It is deliberately
    /// partial over the module's statements: module variables and properties here, types through
    /// their sites; every other declaration is resolved by its own checker, as before.
    /// </summary>
    private void ResolveDeclaredTypesBeforeBodies(Module module)
    {
        _declarationModuleScope = _symbolTable.CurrentScope;
        _typeSites.Clear();
        _typeSitesInOrder.Clear();
        _valueSites.Clear();
        _valueSitesByDeclaration.Clear();
        CollectDeclarationSites(module);

        foreach (var statement in module.Body)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            switch (statement)
            {
                case VariableDeclaration { IsConst: false } variable
                    when DeclaredModuleVariable(variable) is { } declared && GetVariableTypeRaw(declared) is UnknownType:
                    var declaredType = _typeResolver.ResolveTypeAnnotation(variable.Type, AnnotationPosition.Value);
                    if (declaredType is not UnknownType)
                    {
                        declared.Type = declaredType;
                        SemanticBinding.SetVariableType(declared, declaredType);
                    }
                    break;

                case PropertyDef property:
                    ResolveModulePropertyDeclaredType(property);
                    break;
            }
        }

        foreach (var site in _typeSitesInOrder)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            using (EnterDeclarationContext(site))
                ResolveTypeDeclarations(site.Symbol);
        }
    }

    /// <summary>
    /// A module property's declared type, from its declaring annotation (the first annotated half of
    /// a split property), on the symbol NameResolver merged its halves onto. An unannotated
    /// auto-property keeps inferring from its default when its declaration is checked.
    /// </summary>
    private void ResolveModulePropertyDeclaredType(PropertyDef property)
    {
        if (property.DeclaredTypeAnnotation() is not { } annotation
            || _symbolTable.Lookup(property.Name, searchParents: false) is not VariableSymbol { IsModuleProperty: true } symbol
            || GetVariableTypeRaw(symbol) is not UnknownType)
            return;

        var propertyType = _typeResolver.ResolveTypeAnnotation(annotation, AnnotationPosition.Value);
        if (propertyType is UnknownType)
            return;
        symbol.Type = propertyType;
        SemanticBinding.SetVariableType(symbol, propertyType);
    }

    /// <summary>
    /// The module variable NameResolver declared for <paramref name="variable"/> (plan-777361 D1), or
    /// null when the declaration owns none (its name is held by another binding).
    /// </summary>
    private VariableSymbol? DeclaredModuleVariable(VariableDeclaration variable)
        => _symbolTable.Lookup(variable.Name, searchParents: false) is VariableSymbol declared
            && declared.IsDeclaredAt(variable)
            && string.Equals(declared.DeclaringFilePath, _currentFilePath, StringComparison.Ordinal)
            && _symbolTable.CurrentScope.LookupImportBinding(variable.Name) == null
                ? declared
                : null;

    /// <summary>The module const NameResolver declared for <paramref name="constant"/>, or null.</summary>
    private VariableSymbol? DeclaredModuleConst(VariableDeclaration constant)
        => _symbolTable.Lookup(constant.Name, searchParents: false) is VariableSymbol { IsConstant: true } declared
            && declared.IsDeclaredAt(constant)
                ? declared
                : null;

    private static bool IsUnannotated(VariableDeclaration declaration)
        => declaration.Type is null or { Name: "auto", IsNameBacktickEscaped: false };

    /// <summary>
    /// The sites of the module: its unannotated values with an initializer, and its classes, structs
    /// and unions with their nested ones, deliberately partial: an interface, enum or delegate holds
    /// no site (its members are its own checker's, as before; S0b excludes an interface's members).
    /// </summary>
    private void CollectDeclarationSites(Module module)
    {
        foreach (var statement in module.Body)
        {
            switch (statement)
            {
                case VariableDeclaration { InitialValue: not null } variable when IsUnannotated(variable):
                    if ((variable.IsConst ? DeclaredModuleConst(variable) : DeclaredModuleVariable(variable)) is { } symbol)
                        AddValueSite(symbol, variable, host: null);
                    break;

                case ClassDef or StructDef or UnionDef:
                    if (_symbolTable.Lookup(TypeDefinitionName(statement)!, searchParents: false) is TypeSymbol type
                        && IsDeclaredBy(type, statement))
                        AddTypeSite(statement, type, enclosing: null);
                    break;
            }
        }
    }

    /// <summary>The name of a site's type definition; deliberately only the three kinds a site holds.</summary>
    private static string? TypeDefinitionName(Statement statement) => statement switch
    {
        ClassDef c => c.Name,
        StructDef s => s.Name,
        UnionDef u => u.Name,
        _ => null,
    };

    /// <summary>The body of a site's type definition; deliberately only the three kinds a site holds.</summary>
    private static ImmutableArray<Statement> TypeDefinitionBody(Statement statement) => statement switch
    {
        ClassDef c => c.Body,
        StructDef s => s.Body,
        UnionDef u => u.Body,
        _ => ImmutableArray<Statement>.Empty,
    };

    private void AddTypeSite(Statement definition, TypeSymbol symbol, TypeSite? enclosing)
    {
        if (_typeSites.ContainsKey(symbol))
            return;
        var site = new TypeSite(definition, symbol, enclosing);
        _typeSites[symbol] = site;
        _typeSitesInOrder.Add(site);

        foreach (var member in TypeDefinitionBody(definition))
        {
            var statement = member.UnwrapDecorated();
            if (statement is VariableDeclaration { InitialValue: not null } field && IsUnannotated(field)
                && symbol.Fields.FirstOrDefault(f => f.Name == field.Name && f.IsDeclaredAt(field)) is { } fieldSymbol)
            {
                AddValueSite(fieldSymbol, field, site);
            }
            else if (statement is ClassDef or StructDef or UnionDef
                && symbol.NestedTypes.FirstOrDefault(t => IsDeclaredBy(t, statement)) is { } nested)
            {
                AddTypeSite(statement, nested, site);
            }
        }
    }

    private void AddValueSite(VariableSymbol symbol, VariableDeclaration declaration, TypeSite? host)
    {
        if (_valueSites.ContainsKey(symbol))
            return;
        var site = new ValueSite(declaration, host);
        _valueSites[symbol] = site;
        _valueSitesByDeclaration[declaration] = site;
    }

    /// <summary>
    /// The type a variable symbol carries now, with no first-read check: what a declaration's own
    /// resolution asks of its symbol.
    /// </summary>
    private SemanticType GetVariableTypeRaw(VariableSymbol symbol)
    {
        var bindingType = SemanticBinding.GetVariableType(symbol);
        return bindingType != SemanticType.Unknown ? bindingType : symbol.Type;
    }

    /// <summary>
    /// D2's first read: an unannotated module-level or type-level value not yet checked is checked
    /// now, in its own declaration's scope state. A read inside its own inference (the mark is
    /// <see cref="ValueState.InProgress"/>) finds it as it is, Unknown.
    /// </summary>
    private void CheckValueAtFirstRead(VariableSymbol symbol)
    {
        if (!_valueSites.TryGetValue(symbol, out var site) || site.State != ValueState.Pending)
            return;
        site.State = ValueState.InProgress;
        try
        {
            using (EnterDeclarationContext(site.Host))
            {
                // The statement walk's narrowing facts for the declaration, then its check: not
                // CheckStatement, whose claim would see this check in progress and skip it.
                if (_narrowingFlow != null && _narrowingFlow.IsTracked(site.Declaration))
                    _currentFacts = _narrowingFlow.FactsBefore(site.Declaration);
                CheckVariableDeclaration(site.Declaration);
            }
        }
        finally
        {
            site.State = ValueState.Done;
        }
    }

    /// <summary>
    /// Whether the in-order walk reaches a value declaration its first read already checked: it is
    /// checked once. Marks a pending one as being checked here.
    /// </summary>
    private bool ClaimValueDeclarationCheck(VariableDeclaration declaration)
    {
        if (!_valueSitesByDeclaration.TryGetValue(declaration, out var site))
            return true;
        if (site.State != ValueState.Pending)
            return false;
        site.State = ValueState.InProgress;
        return true;
    }

    private void CompleteValueDeclarationCheck(VariableDeclaration declaration)
    {
        if (_valueSitesByDeclaration.TryGetValue(declaration, out var site))
            site.State = ValueState.Done;
    }

    /// <summary>
    /// A type's declared members, once: field and const types, a dataclass's generated members,
    /// property, event and method signature types, union case fields. A base declared in this module
    /// is resolved first, in its own scope state, so a derived dataclass reads its base's fields
    /// whatever the order. The caller stands in the type's body context.
    /// </summary>
    private void ResolveTypeDeclarations(TypeSymbol symbol)
    {
        if (!_typeSites.TryGetValue(symbol, out var site) || site.Resolved || site.Resolving)
            return;
        site.Resolving = true;
        try
        {
            if (symbol.BaseType is { } baseType && _typeSites.TryGetValue(baseType, out var baseSite)
                && !baseSite.Resolved && !baseSite.Resolving)
            {
                using (EnterDeclarationContext(baseSite))
                    ResolveTypeDeclarations(baseType);
            }

            ResolveOwnTypeDeclarations(site.Definition, symbol, site.Enclosing?.Symbol);
        }
        finally
        {
            site.Resolving = false;
            site.Resolved = true;
        }
    }

    /// <summary>
    /// A type checker's entry to its declared members (CheckClass, CheckStruct, CheckUnion): already
    /// resolved by the module's pre-pass for a type it holds a site for, else resolved here, once, in
    /// the body context the checker stands in. <paramref name="enclosing"/> is the type around a union,
    /// whose case fields resolve in that context.
    /// </summary>
    private void ResolveDeclaredMembers(Statement definition, TypeSymbol symbol, TypeSymbol? enclosing)
    {
        if (_typeSites.ContainsKey(symbol))
            ResolveTypeDeclarations(symbol);
        else
            ResolveOwnTypeDeclarations(definition, symbol, enclosing);
    }

    /// <summary>
    /// One type's declared members, by its kind: deliberately the three kinds a site holds: an
    /// interface's are its checker's (CheckInterface), and an enum or delegate declares none.
    /// </summary>
    private void ResolveOwnTypeDeclarations(Statement definition, TypeSymbol symbol, TypeSymbol? enclosing)
    {
        switch (definition)
        {
            case ClassDef classDef:
                ResolveFieldTypes(symbol, classDef.Body);
                // A dataclass's generated members, after its field types (and its base's: above).
                ProcessDataclassDecorator(symbol, classDef);
                ResolvePropertyTypes(symbol, classDef.Body);
                ResolveEventTypes(symbol, classDef.Body);
                ResolveClassMethodSignatures(classDef.Body);
                break;

            case StructDef structDef:
                ResolveFieldTypes(symbol, structDef.Body);
                ResolvePropertyTypes(symbol, structDef.Body);
                ResolveEventTypes(symbol, structDef.Body);
                ResolveClassMethodSignatures(structDef.Body);
                break;

            case UnionDef unionDef:
                ResolveUnionCaseFields(unionDef, symbol, enclosing);
                ResolveClassMethodSignatures(unionDef.Body);
                break;
        }
    }

    /// <summary>A class's or struct's field and const types, from their annotations, in place on the declared symbols.</summary>
    private void ResolveFieldTypes(TypeSymbol symbol, ImmutableArray<Statement> body)
    {
        for (int i = 0; i < symbol.Fields.Count; i++)
        {
            var fieldSymbol = symbol.Fields[i];
            if (GetVariableTypeRaw(fieldSymbol) != SemanticType.Unknown)
                continue;

            // Find the corresponding VariableDeclaration in the AST
            var fieldDecl = body.OfType<VariableDeclaration>().FirstOrDefault(v => v.Name == fieldSymbol.Name);
            if (fieldDecl == null)
                continue;

            // In place, never a copy: a use checked before this body (a function declared above the
            // type) already bound this symbol, and the emitter reads the facts CodeGenInfoComputer
            // records on it (plan-775c50 D2, D4). A copy left that use on a symbol with none.
            var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type, AnnotationPosition.Value);
            fieldSymbol.Type = resolvedType;
            SemanticBinding.SetVariableType(fieldSymbol, resolvedType);
        }
    }

    /// <summary>
    /// A type body's context, entered as its checker enters it: its scope with its type parameters,
    /// aliases and nested types, and the class context the annotation resolutions read, deliberately for
    /// only the three kinds a site holds.
    /// </summary>
    private IDisposable EnterTypeBody(TypeSite site)
    {
        var symbol = site.Symbol;
        ImmutableArray<TypeParameterDef> typeParameters;
        switch (site.Definition)
        {
            case ClassDef c:
                _symbolTable.EnterScope($"class:{c.Name}");
                typeParameters = c.TypeParameters;
                break;
            case StructDef s:
                _symbolTable.EnterScope($"struct:{s.Name}");
                typeParameters = s.TypeParameters;
                break;
            case UnionDef u:
                _symbolTable.EnterScope($"union:{u.Name}");
                typeParameters = u.TypeParameters;
                break;
            default:
                throw new InvalidOperationException($"no type body for {site.Definition.GetType().Name}");
        }
        foreach (var typeParam in typeParameters)
        {
            _symbolTable.Define(new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                DeclaringType = symbol,
                Constraints = typeParam.Constraints,
                Variance = typeParam.Variance,
                DeclarationLine = typeParam.LineStart,
                DeclarationColumn = typeParam.ColumnStart,
                NameDeclarationLine = typeParam.LineStart,
                NameDeclarationColumn = typeParam.ColumnStart
            });
        }

        // A union's checker registers neither aliases nor nested types (CheckUnion).
        if (site.Definition is not UnionDef)
        {
            foreach (var statement in TypeDefinitionBody(site.Definition))
            {
                if (statement is TypeAlias typeAlias && IsDeclaredTypeBodyAlias(symbol, typeAlias))
                    RegisterScopedTypeAlias(typeAlias);
            }
            foreach (var nestedType in symbol.NestedTypes)
                _symbolTable.Define(nestedType);
        }

        var previousClass = _currentClass;
        var previousTypeBody = _currentTypeBody;
        var previousTypeContext = _typeResolver.CurrentTypeContext;
        _currentClass = symbol;
        _currentTypeBody = TypeDefinitionBody(site.Definition);
        _typeResolver.SetCurrentTypeContext(symbol);
        return new Restore(() =>
        {
            _currentClass = previousClass;
            _currentTypeBody = previousTypeBody;
            _typeResolver.SetCurrentTypeContext(previousTypeContext);
            _symbolTable.ExitScope();
        });
    }

    /// <summary>
    /// The scope state a module-level declaration (<paramref name="host"/> null) or a type-level one
    /// is checked in, whatever the checker stands in now: the module scope alone, no function, block,
    /// call, store or narrowing in flight, and the enclosing type bodies entered outermost first.
    /// Disposing restores the checker exactly.
    /// </summary>
    private IDisposable EnterDeclarationContext(TypeSite? host)
    {
        var saved = CaptureCheckerContext();
        ResetCheckerContextToModuleLevel();
        // The expectation, the parameter-typed argument and the store context go through the store
        // seam (TypeChecker.StoreConversion), never this record.
        var store = EnterDeclarationStore();
        var scope = _symbolTable.EnterDeclarationScope(_declarationModuleScope ?? _symbolTable.CurrentScope);
        var narrowings = _narrowingContext.EnterIsolatedScope();

        var chain = new List<TypeSite>();
        for (var site = host; site != null; site = site.Enclosing)
            chain.Insert(0, site);
        var bodies = new List<IDisposable>();
        foreach (var site in chain)
            bodies.Add(EnterTypeBody(site));

        return new Restore(() =>
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
                bodies[i].Dispose();
            narrowings.Dispose();
            scope.Dispose();
            RestoreCheckerContext(saved);
            store.Dispose();
        });
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        private Action? _restore = restore;

        public void Dispose()
        {
            _restore?.Invoke();
            _restore = null;
        }
    }

    /// <summary>The checker's position-dependent state: what a function, block, call, store or type body sets.</summary>
    private sealed record CheckerContext(
        SemanticType? CurrentFunctionReturnType,
        TypeSymbol? CurrentClass,
        ImmutableArray<Statement> CurrentTypeBody,
        TypeSymbol? TypeResolverContext,
        bool TypeResolverStatic,
        ImmutableHashSet<string> FunctionTypeParametersInScope,
        Analysis.ControlFlow.NarrowingFlowResult? NarrowingFlow,
        IReadOnlyCollection<Analysis.ControlFlow.NarrowingFact> CurrentFacts,
        Expression? TypeTestOperand,
        Expression? TypeTestTypeArgument,
        Expression? MatchSubjectOperand,
        bool InTestFunction,
        Expression? CurrentCallCallee,
        Expression? CurrentStatementExpression,
        Expression? CurrentMemberAccessQualifier,
        bool InErrorRecoveredAnnotationSlot,
        HashSet<Expression>? CurrentCallArguments,
        Expression? CurrentIterationSource,
        HashSet<Expression>? CurrentIndexArguments,
        ClassBodyReader? InitializerOwner,
        FunctionCall? CurrentCall,
        IndexStoreTarget? IndexStoreTarget,
        Expression? PlainStoreTarget,
        bool InExceptBlock,
        bool InFinally,
        bool InLambdaBody,
        bool InExceptStarBlock,
        string? CurrentMethodName,
        FunctionSymbol? CurrentMethodSymbol,
        bool CurrentMethodIsOverride,
        bool CurrentMethodIsDunder,
        bool CurrentFunctionIsGenerator,
        bool CurrentFunctionIsAsync,
        int ControlFlowDepth,
        bool SuperInitCalled,
        bool SelfInitCalled,
        FunctionCall? HoistedInitializerCall,
        bool HoistedInitializerCallRepeated,
        int PendingOverloadWatermark);

    private CheckerContext CaptureCheckerContext() => new(
        _currentFunctionReturnType, _currentClass, _currentTypeBody,
        _typeResolver.CurrentTypeContext, _typeResolver.IsStaticContext, _functionTypeParametersInScope,
        _narrowingFlow, _currentFacts, _typeTestOperand, _typeTestTypeArgument, _matchSubjectOperand,
        _inTestFunction, _currentCallCallee, _currentStatementExpression, _currentMemberAccessQualifier,
        _inErrorRecoveredAnnotationSlot, _currentCallArguments,
        _currentIterationSource, _currentIndexArguments, _initializerOwner, _currentCall, _indexStoreTarget,
        _plainStoreTarget, _inExceptBlock, _inFinally, _inLambdaBody, _inExceptStarBlock, _currentMethodName,
        _currentMethodSymbol, _currentMethodIsOverride, _currentMethodIsDunder, _currentFunctionIsGenerator,
        _currentFunctionIsAsync, _controlFlowDepth, _superInitCalled, _selfInitCalled, _hoistedInitializerCall,
        _hoistedInitializerCallRepeated, _pendingOverloadWatermark);

    private void RestoreCheckerContext(CheckerContext c)
    {
        _currentFunctionReturnType = c.CurrentFunctionReturnType;
        _currentClass = c.CurrentClass;
        _currentTypeBody = c.CurrentTypeBody;
        _typeResolver.SetCurrentTypeContext(c.TypeResolverContext);
        _typeResolver.SetIsStaticContext(c.TypeResolverStatic);
        _functionTypeParametersInScope = c.FunctionTypeParametersInScope;
        _narrowingFlow = c.NarrowingFlow;
        _currentFacts = c.CurrentFacts;
        _typeTestOperand = c.TypeTestOperand;
        _typeTestTypeArgument = c.TypeTestTypeArgument;
        _matchSubjectOperand = c.MatchSubjectOperand;
        _inTestFunction = c.InTestFunction;
        _currentCallCallee = c.CurrentCallCallee;
        _currentStatementExpression = c.CurrentStatementExpression;
        _currentMemberAccessQualifier = c.CurrentMemberAccessQualifier;
        _inErrorRecoveredAnnotationSlot = c.InErrorRecoveredAnnotationSlot;
        _currentCallArguments = c.CurrentCallArguments;
        _currentIterationSource = c.CurrentIterationSource;
        _currentIndexArguments = c.CurrentIndexArguments;
        _initializerOwner = c.InitializerOwner;
        _currentCall = c.CurrentCall;
        _indexStoreTarget = c.IndexStoreTarget;
        _plainStoreTarget = c.PlainStoreTarget;
        _inExceptBlock = c.InExceptBlock;
        _inFinally = c.InFinally;
        _inLambdaBody = c.InLambdaBody;
        _inExceptStarBlock = c.InExceptStarBlock;
        _currentMethodName = c.CurrentMethodName;
        _currentMethodSymbol = c.CurrentMethodSymbol;
        _currentMethodIsOverride = c.CurrentMethodIsOverride;
        _currentMethodIsDunder = c.CurrentMethodIsDunder;
        _currentFunctionIsGenerator = c.CurrentFunctionIsGenerator;
        _currentFunctionIsAsync = c.CurrentFunctionIsAsync;
        _controlFlowDepth = c.ControlFlowDepth;
        _superInitCalled = c.SuperInitCalled;
        _selfInitCalled = c.SelfInitCalled;
        _hoistedInitializerCall = c.HoistedInitializerCall;
        _hoistedInitializerCallRepeated = c.HoistedInitializerCallRepeated;
        _pendingOverloadWatermark = c.PendingOverloadWatermark;
    }

    /// <summary>The state the module's own statements are checked in (CheckModule).</summary>
    private void ResetCheckerContextToModuleLevel()
        => RestoreCheckerContext(new CheckerContext(
            CurrentFunctionReturnType: null, CurrentClass: null, CurrentTypeBody: default,
            TypeResolverContext: null, TypeResolverStatic: false,
            FunctionTypeParametersInScope: ImmutableHashSet<string>.Empty,
            NarrowingFlow: _moduleNarrowingFlow, CurrentFacts: System.Array.Empty<Analysis.ControlFlow.NarrowingFact>(),
            TypeTestOperand: null, TypeTestTypeArgument: null, MatchSubjectOperand: null, InTestFunction: false,
            CurrentCallCallee: null, CurrentStatementExpression: null, CurrentMemberAccessQualifier: null,
            InErrorRecoveredAnnotationSlot: false, CurrentCallArguments: null,
            CurrentIterationSource: null, CurrentIndexArguments: null,
            InitializerOwner: null, CurrentCall: null, IndexStoreTarget: null, PlainStoreTarget: null,
            InExceptBlock: false, InFinally: false, InLambdaBody: false, InExceptStarBlock: false,
            CurrentMethodName: null, CurrentMethodSymbol: null, CurrentMethodIsOverride: false,
            CurrentMethodIsDunder: false, CurrentFunctionIsGenerator: false, CurrentFunctionIsAsync: false,
            ControlFlowDepth: 0, SuperInitCalled: false, SelfInitCalled: false, HoistedInitializerCall: null,
            HoistedInitializerCallRepeated: false, PendingOverloadWatermark: -1));
}
