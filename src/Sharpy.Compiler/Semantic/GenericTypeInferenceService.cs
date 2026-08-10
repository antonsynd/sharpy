using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Result of generic type argument inference.
/// </summary>
internal record InferenceResult
{
    /// <summary>
    /// Whether inference succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The inferred type arguments, in order matching the function's type parameters.
    /// Only valid when Success is true.
    /// </summary>
    public List<SemanticType>? InferredTypes { get; init; }

    /// <summary>
    /// Error message when inference fails.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The kind of error that occurred.
    /// </summary>
    public InferenceErrorKind? ErrorKind { get; init; }

    public static InferenceResult Succeeded(List<SemanticType> types) =>
        new() { Success = true, InferredTypes = types };

    public static InferenceResult Failed(InferenceErrorKind kind, string message) =>
        new() { Success = false, ErrorKind = kind, ErrorMessage = message };
}

/// <summary>
/// Types of inference errors.
/// </summary>
internal enum InferenceErrorKind
{
    /// <summary>
    /// No arguments provide type information for a type parameter.
    /// Example: create_empty[T]() called as create_empty()
    /// </summary>
    NoArgumentsForTypeParameter,

    /// <summary>
    /// Different arguments suggest different types for the same type parameter.
    /// Example: pair[T](1, "hello") where T would be both int and str
    /// </summary>
    ConflictingTypes,

    /// <summary>
    /// Inferred type doesn't satisfy the constraint.
    /// Example: find_max[T: IComparable](NonComparableClass())
    /// </summary>
    ConstraintNotSatisfied,

    /// <summary>
    /// Multiple equally valid inferences exist.
    /// </summary>
    AmbiguousTypes
}

/// <summary>
/// Service for inferring generic type arguments from function call arguments.
///
/// This service implements constraint-based type unification to infer type arguments
/// when a generic function is called without explicit type arguments.
/// </summary>
/// <remarks>
/// Design notes:
/// - Inference is left-to-right from arguments
/// - Returns InferenceResult with success/failure info and error messages
/// - Does NOT report errors directly (caller handles that)
/// - Checks type constraints after inference
/// </remarks>
internal class GenericTypeInferenceService
{
    /// <summary>
    /// Prefix for synthetic type parameters created by SynthesizePrimitiveFunctionType.
    /// Type parameters with this prefix are treated as unconstrained wildcards during
    /// unification — a subsequent concrete binding replaces them.
    /// </summary>
    internal const string SyntheticTypeParameterPrefix = "__synth_T";

    /// <summary>
    /// Returns true if the type is a synthetic TypeParameterType used as a placeholder
    /// in synthesized primitive function types (e.g., bool used as filter predicate).
    /// </summary>
    internal static bool IsSyntheticTypeParameter(SemanticType type)
        => type is TypeParameterType tp && tp.Name.StartsWith(SyntheticTypeParameterPrefix, StringComparison.Ordinal);

    private readonly SymbolTable _symbolTable;
    private TypeResolver? _typeResolver;

    /// <summary>
    /// Optional SemanticBinding for reading inheritance data.
    /// When set, helpers prefer this over direct symbol property access.
    /// </summary>
    public SemanticBinding SemanticBinding { get; set; } = new();

    public GenericTypeInferenceService(SymbolTable symbolTable, TypeResolver? typeResolver = null)
    {
        _symbolTable = symbolTable;
        _typeResolver = typeResolver;
    }

    /// <summary>
    /// Attempt to infer type arguments for a generic function call.
    /// </summary>
    /// <param name="genericFunc">The generic function being called</param>
    /// <param name="argumentTypes">The types of the arguments passed to the function</param>
    /// <returns>InferenceResult with inferred types or error information</returns>
    public InferenceResult InferTypeArguments(FunctionSymbol genericFunc, List<SemanticType> argumentTypes)
    {
        if (!genericFunc.IsGeneric)
        {
            // Not a generic function - nothing to infer
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        var typeParams = genericFunc.TypeParameters;
        var parameters = genericFunc.Parameters;

        // Create substitution map: type parameter name -> inferred type
        var substitutions = new Dictionary<string, SemanticType>();

        // Process each parameter and argument pair
        int argIndex = 0;
        foreach (var param in parameters)
        {
            if (argIndex >= argumentTypes.Count)
                break;

            var formalType = param.Type;
            var actualType = argumentTypes[argIndex];

            // Attempt to unify formal with actual
            var unifyResult = Unify(formalType, actualType, substitutions);
            if (!unifyResult.Success)
            {
                return unifyResult;
            }

            argIndex++;
        }

        // Check that all type parameters were inferred (or have defaults)
        var inferredTypes = new List<SemanticType>();
        foreach (var typeParam in typeParams)
        {
            if (!substitutions.TryGetValue(typeParam.Name, out var inferredType))
            {
                // PEP 696: try using the type parameter default. Resolved through
                // ResolveTypeParameterDefault so a default naming an earlier parameter takes that
                // parameter's INFERRED value (#1245) — `inferredTypes` is the vector settled so
                // far, in declaration order, which is exactly what the earlier positions bound to.
                if (typeParam.DefaultType != null && _typeResolver != null)
                {
                    inferredType = _typeResolver.ResolveTypeParameterDefault(
                        typeParams, inferredTypes.Count, inferredTypes);
                    substitutions[typeParam.Name] = inferredType;
                }
                else
                {
                    return InferenceResult.Failed(
                        InferenceErrorKind.NoArgumentsForTypeParameter,
                        $"Type parameter '{typeParam.Name}' cannot be inferred; no arguments provide type information. " +
                        $"Use explicit syntax: {genericFunc.Name}[{string.Join(", ", typeParams.Select(tp => tp.Name))}](...)");
                }
            }
            inferredTypes.Add(inferredType);
        }

        // Check constraints
        for (int i = 0; i < typeParams.Count; i++)
        {
            var typeParam = typeParams[i];
            var inferredType = inferredTypes[i];

            var constraintResult = CheckConstraints(typeParam, inferredType, typeParams);
            if (!constraintResult.Success)
            {
                return constraintResult;
            }
        }

        return InferenceResult.Succeeded(inferredTypes);
    }

    /// <summary>
    /// Unify parallel lists of formal and actual types, returning the collected
    /// type-parameter substitutions.  Returns null on unification failure;
    /// returns an empty dictionary (not null) when no type parameters were bound.
    /// </summary>
    public Dictionary<string, SemanticType>? UnifyTypes(
        IReadOnlyList<SemanticType> formalTypes,
        IReadOnlyList<SemanticType> actualTypes)
    {
        var substitutions = new Dictionary<string, SemanticType>();
        var count = Math.Min(formalTypes.Count, actualTypes.Count);

        for (int i = 0; i < count; i++)
        {
            var result = Unify(formalTypes[i], actualTypes[i], substitutions);
            if (!result.Success)
            {
                return null;
            }
        }

        return substitutions;
    }

    /// <summary>
    /// Replace every <see cref="TypeParameterType"/> in <paramref name="type"/>
    /// whose name appears in <paramref name="substitutions"/> with the mapped
    /// concrete type.  Delegates to <see cref="TypeSubstitution.Apply"/>.
    /// </summary>
    public static SemanticType SubstituteTypeParameters(
        SemanticType type,
        Dictionary<string, SemanticType> substitutions)
    {
        return TypeSubstitution.Apply(type, substitutions);
    }

    /// <summary>
    /// Attempt to unify a formal type with an actual type, binding type parameters.
    /// </summary>
    /// <param name="variance">
    /// Variance of the position being unified (from the enclosing generic definition's
    /// type parameter), used to resolve conflicting bindings when unifying through
    /// variance-annotated supertypes (#827). Defaults to invariant.
    /// </param>
    private InferenceResult Unify(
        SemanticType formal,
        SemanticType actual,
        Dictionary<string, SemanticType> substitutions,
        TypeParameterVariance variance = TypeParameterVariance.None)
    {
        // Case 1: Formal type is a type parameter
        if (formal is TypeParameterType typeParam)
        {
            return UnifyTypeParameter(typeParam.Name, actual, substitutions, variance);
        }

        // Case 2: Both are generic types (e.g., list[T] vs list[int])
        if (formal is GenericType formalGeneric && actual is GenericType actualGeneric)
        {
            return UnifyGenericTypes(formalGeneric, actualGeneric, substitutions);
        }

        // Case 3: Both are function types (e.g., (T) -> U vs (str) -> int)
        if (formal is FunctionType formalFunc && actual is FunctionType actualFunc)
        {
            return UnifyFunctionTypes(formalFunc, actualFunc, substitutions);
        }

        // Case 4: Both are nullable types (e.g., T? vs int?)
        if (formal is NullableType formalNullable && actual is NullableType actualNullable)
        {
            return Unify(formalNullable.UnderlyingType, actualNullable.UnderlyingType, substitutions);
        }

        // Case 5: Formal is nullable, actual is non-nullable (T? vs int)
        if (formal is NullableType formalNullable2)
        {
            return Unify(formalNullable2.UnderlyingType, actual, substitutions);
        }

        // Case 6: Both are tuple types
        if (formal is TupleType formalTuple && actual is TupleType actualTuple)
        {
            return UnifyTupleTypes(formalTuple, actualTuple, substitutions);
        }

        // Case 7: Both are optional types (e.g., T? vs int?)
        if (formal is OptionalType formalOpt && actual is OptionalType actualOpt)
        {
            return Unify(formalOpt.UnderlyingType, actualOpt.UnderlyingType, substitutions);
        }

        // Case 8: Both are result types (e.g., Result[T, E] vs Result[int, str])
        if (formal is ResultType formalResult && actual is ResultType actualResult)
        {
            var okResult = Unify(formalResult.OkType, actualResult.OkType, substitutions);
            if (!okResult.Success)
                return okResult;
            return Unify(formalResult.ErrorType, actualResult.ErrorType, substitutions);
        }

        // Case 9: No type parameters involved - types should match
        // We're lenient here: the purpose of unification is to extract type parameter bindings,
        // not to validate argument types (that's done by CheckFunctionCall). If concrete types
        // don't match, we simply have no bindings to extract from this argument pair.
        if (actual.IsAssignableTo(formal))
        {
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        // Concrete types don't match — still return success because type validation
        // is the caller's responsibility. Returning failure here would abort inference
        // prematurely and prevent binding type parameters from other arguments.
        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// Unify a type parameter with a concrete type.
    /// </summary>
    private InferenceResult UnifyTypeParameter(
        string paramName,
        SemanticType actual,
        Dictionary<string, SemanticType> substitutions,
        TypeParameterVariance variance = TypeParameterVariance.None)
    {
        if (substitutions.TryGetValue(paramName, out var existing))
        {
            // If the existing binding is a synthetic type parameter (from synthesized
            // primitive function types), replace it with the concrete type. Synthetic
            // parameters carry no type information and should not block later bindings.
            if (IsSyntheticTypeParameter(existing))
            {
                substitutions[paramName] = actual;
                return InferenceResult.Succeeded(new List<SemanticType>());
            }

            // Already bound - check consistency
            if (!TypesAreCompatible(existing, actual))
            {
                return InferenceResult.Failed(
                    InferenceErrorKind.ConflictingTypes,
                    $"Conflicting types for type parameter '{paramName}': " +
                    $"inferred '{existing.GetDisplayName()}' earlier, but now got '{actual.GetDisplayName()}'");
            }

            // Variance-aware refinement (#827): when unifying through a variance-annotated
            // supertype position, reconcile compatible-but-different bindings.
            // Covariant (out) positions require every source to be assignable to the final
            // binding, so widen to the more general type (e.g., T bound to Dog then Animal
            // arrives → T becomes Animal). Contravariant (in) positions require the final
            // binding to be assignable from the parameter's perspective by every source,
            // so narrow to the more specific type. Invariant positions keep the existing
            // binding (lenient, matching prior behavior).
            if (variance == TypeParameterVariance.Covariant
                && existing.IsAssignableTo(actual) && !actual.IsAssignableTo(existing))
            {
                substitutions[paramName] = actual;
            }
            else if (variance == TypeParameterVariance.Contravariant
                && actual.IsAssignableTo(existing) && !existing.IsAssignableTo(actual))
            {
                substitutions[paramName] = actual;
            }

            // Already bound to compatible type - success
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        // Skip binding if actual is a synthetic type parameter — it carries no
        // type information, so defer binding to a later argument with a concrete type.
        if (IsSyntheticTypeParameter(actual))
        {
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        // Bind the type parameter
        substitutions[paramName] = actual;
        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// Unify two generic types (e.g., list[T] with list[int]).
    /// </summary>
    private InferenceResult UnifyGenericTypes(GenericType formal, GenericType actual, Dictionary<string, SemanticType> substitutions)
    {
        // Names must match (e.g., both must be "list")
        if (formal.Name != actual.Name)
        {
            // The actual type may implement/extend the formal generic type (e.g.,
            // list[int] implements IEnumerable[int]). Walk the supertype hierarchy
            // to extract type-parameter bindings before giving up (#827).
            return UnifyThroughSupertypes(formal, actual, substitutions);
        }

        // Must have same number of type arguments
        if (formal.TypeArguments.Count != actual.TypeArguments.Count)
        {
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        // Unify each type argument
        for (int i = 0; i < formal.TypeArguments.Count; i++)
        {
            var result = Unify(formal.TypeArguments[i], actual.TypeArguments[i], substitutions);
            if (!result.Success)
            {
                return result;
            }
        }

        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// Unify a formal generic type with an actual generic type of a different name by
    /// walking the actual type's supertype hierarchy (#827). For example, unifying
    /// <c>IEnumerable[T]</c> with <c>list[int]</c> finds that list implements
    /// <c>IEnumerable[int]</c> and binds <c>T → int</c>.
    /// </summary>
    /// <remarks>
    /// Recursion terminates because nested <see cref="Unify"/> calls always operate on
    /// strictly smaller formal types; the walker itself guards cycles with a visited set.
    /// </remarks>
    private InferenceResult UnifyThroughSupertypes(GenericType formal, GenericType actual, Dictionary<string, SemanticType> substitutions)
    {
        foreach (var supertype in GenericInstantiationWalker.EnumerateSupertypes(
                     actual, _symbolTable, SemanticBinding, _typeResolver))
        {
            if (supertype.Definition.Name != formal.Name
                || supertype.TypeArguments.Count != formal.TypeArguments.Count)
            {
                continue;
            }

            return UnifyInstantiatedArguments(formal, supertype, substitutions);
        }

        // CLR reflection fallback: the actual type's TypeSymbol may lack interface data
        // (e.g., module-discovered types registered before interface population existed).
        if (TryUnifyViaClrReflection(formal, actual, substitutions, out var clrResult))
            return clrResult;

        // No supertype match — different generic types; can't extract type parameter
        // bindings from this pair, but don't abort inference (other arguments may
        // provide bindings).
        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// Unify the formal generic type's arguments against an instantiated supertype's
    /// arguments, threading per-position variance from the supertype definition.
    /// </summary>
    private InferenceResult UnifyInstantiatedArguments(
        GenericType formal,
        GenericInstantiationWalker.InstantiatedSupertype supertype,
        Dictionary<string, SemanticType> substitutions)
    {
        for (int i = 0; i < formal.TypeArguments.Count; i++)
        {
            var variance = i < supertype.Definition.TypeParameters.Count
                ? supertype.Definition.TypeParameters[i].Variance
                : TypeParameterVariance.None;

            var result = Unify(formal.TypeArguments[i], supertype.TypeArguments[i], substitutions, variance);
            if (!result.Success)
            {
                return result;
            }
        }

        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// CLR reflection fallback for supertype unification: resolves the actual type's open
    /// CLR generic definition and scans its interfaces for one matching the formal.
    /// Only interface arguments that map directly to the actual type's own generic
    /// parameters are unified; concrete CLR argument positions are skipped.
    ///
    /// <para>
    /// A formal matches an interface by CLR PROVENANCE when it has any, and by Sharpy name otherwise
    /// (#1260, #1252). Name-only matching is what made a lambda returning a CLR <c>List[int]</c> decline
    /// silently: <c>SelectMany</c>'s selector formal is <c>Func[int, list[TCollection]]</c> because the
    /// bridge maps <c>IEnumerable&lt;T&gt;</c> to <c>list</c>, and <c>"IEnumerable" != "list"</c>, so
    /// <c>TCollection</c> never bound and the whole staged call was abandoned with no diagnostic. The
    /// two rules are alternatives rather than a replacement, so nothing that matched by name before
    /// stops matching now.
    /// </para>
    /// </summary>
    private bool TryUnifyViaClrReflection(
        GenericType formal,
        GenericType actual,
        Dictionary<string, SemanticType> substitutions,
        out InferenceResult result)
    {
        result = InferenceResult.Succeeded(new List<SemanticType>());

        var clrDefinition = _symbolTable.BuiltinRegistry.GetType(actual.Name)?.ClrType
            ?? actual.GenericDefinition?.ClrType
            ?? _symbolTable.LookupType(actual.Name)?.ClrType;

        if (clrDefinition is not { IsGenericTypeDefinition: true }
            || clrDefinition.GetGenericArguments().Length != actual.TypeArguments.Count)
        {
            return false;
        }

        // One closure walk for the whole compiler (#1145): Discovery owns the enumeration, exactly
        // as it owns the origin-match rule inside FormalMatchesClrDefinition.
        foreach (var clrInterface in ClrTypeHelper.SupertypeClosureOf(clrDefinition))
        {
            if (!clrInterface.IsGenericType)
                continue;

            var interfaceDefinition = clrInterface.GetGenericTypeDefinition();
            if (!FormalMatchesClrDefinition(formal, interfaceDefinition))
                continue;

            var interfaceArguments = clrInterface.GetGenericArguments();
            if (interfaceArguments.Length != formal.TypeArguments.Count)
                continue;

            var definitionArguments = interfaceDefinition.GetGenericArguments();
            for (int i = 0; i < interfaceArguments.Length; i++)
            {
                if (!interfaceArguments[i].IsGenericParameter)
                    continue;

                var position = interfaceArguments[i].GenericParameterPosition;
                if (position >= actual.TypeArguments.Count)
                    continue;

                var variance = ClrTypeBridge.GetClrVariance(definitionArguments[i]);
                var unifyResult = Unify(
                    formal.TypeArguments[i], actual.TypeArguments[position], substitutions, variance);
                if (!unifyResult.Success)
                {
                    result = unifyResult;
                    return true;
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a formal generic names the CLR definition <paramref name="clrDefinition"/>. Provenance
    /// decides when the formal has it — the formal's Sharpy spelling is the bridge's collapsed name
    /// (<c>list</c>) while the CLR definition's is its own (<c>IEnumerable`1</c>), so they never agree
    /// textually; the match rule itself is Discovery's (<see cref="ClrTypeHelper.DefinitionIsOrigin"/>),
    /// the same one assignability resolves provenance through, so the two consumers cannot drift
    /// (#1145). The Sharpy-name comparison remains as the alternative for every formal written in
    /// source, which is what unified through CLR interfaces before provenance existed.
    /// </summary>
    private static bool FormalMatchesClrDefinition(GenericType formal, Type clrDefinition)
        => (formal.ClrOriginTypeName is { Length: > 0 } origin
                && ClrTypeHelper.DefinitionIsOrigin(clrDefinition, origin))
           || ClrNameHelper.StripArity(clrDefinition.Name) == formal.Name;

    /// <summary>
    /// Unify two function types (e.g., (T) -> U with (str) -> int).
    /// </summary>
    private InferenceResult UnifyFunctionTypes(FunctionType formal, FunctionType actual, Dictionary<string, SemanticType> substitutions)
    {
        // Must have same number of parameters
        if (formal.ParameterTypes.Count != actual.ParameterTypes.Count)
        {
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        // Unify each parameter type (contravariant position)
        for (int i = 0; i < formal.ParameterTypes.Count; i++)
        {
            var result = Unify(formal.ParameterTypes[i], actual.ParameterTypes[i], substitutions);
            if (!result.Success)
            {
                return result;
            }
        }

        // Unify return types (covariant position)
        return Unify(formal.ReturnType, actual.ReturnType, substitutions);
    }

    /// <summary>
    /// Unify two tuple types.
    /// </summary>
    private InferenceResult UnifyTupleTypes(TupleType formal, TupleType actual, Dictionary<string, SemanticType> substitutions)
    {
        if (formal.ElementTypes.Count != actual.ElementTypes.Count)
        {
            return InferenceResult.Succeeded(new List<SemanticType>());
        }

        for (int i = 0; i < formal.ElementTypes.Count; i++)
        {
            var result = Unify(formal.ElementTypes[i], actual.ElementTypes[i], substitutions);
            if (!result.Success)
            {
                return result;
            }
        }

        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// Check if two types are compatible for unification purposes.
    /// </summary>
    private bool TypesAreCompatible(SemanticType a, SemanticType b)
    {
        // Exact match
        if (a.Equals(b))
            return true;

        // One is assignable to the other
        if (a.IsAssignableTo(b) || b.IsAssignableTo(a))
            return true;

        return false;
    }

    /// <summary>
    /// Checks the WRITTEN type arguments of an explicit generic reference — <c>describe[Circle](c)</c> —
    /// against their parameters' constraints (#1289). The inference path has always checked what it
    /// infers; this is the same check for what the user writes, so one comparator answers the question
    /// for both call paths instead of the explicit one reaching Roslyn and returning CS0311 behind
    /// SPY0908.
    /// <para>Positions beyond the shorter of the two lists are not checked here: an arity mismatch is
    /// its own diagnostic, already emitted by the caller before this runs.</para>
    /// </summary>
    public InferenceResult CheckWrittenTypeArguments(
        IReadOnlyList<TypeParameterDef> typeParameters, IReadOnlyList<SemanticType> typeArgs)
    {
        for (int i = 0; i < typeParameters.Count && i < typeArgs.Count; i++)
        {
            var result = CheckConstraints(
                typeParameters[i], typeArgs[i], typeParameters, ConstraintSubject.Written);
            if (!result.Success)
                return result;
        }

        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// How the type being checked was arrived at, for the diagnostic's leading noun. The check itself
    /// is identical either way — that is the point of #1289's second call path.
    /// </summary>
    private static class ConstraintSubject
    {
        public const string Inferred = "Inferred type";
        public const string Written = "Type argument";
    }

    /// <summary>
    /// Check that a type satisfies all constraints on a type parameter.
    /// </summary>
    private InferenceResult CheckConstraints(
        TypeParameterDef typeParam, SemanticType type,
        IReadOnlyList<TypeParameterDef> declaringParameters,
        string subject = ConstraintSubject.Inferred)
    {
        foreach (var constraint in typeParam.Constraints)
        {
            var result = CheckSingleConstraint(
                typeParam.Name, type, constraint, declaringParameters, subject);
            if (!result.Success)
            {
                return result;
            }
        }

        return InferenceResult.Succeeded(new List<SemanticType>());
    }

    /// <summary>
    /// Check a single constraint.
    /// </summary>
    private InferenceResult CheckSingleConstraint(
        string paramName, SemanticType inferredType, ConstraintClause constraint,
        IReadOnlyList<TypeParameterDef> declaringParameters, string subject)
    {
        switch (constraint)
        {
            // Handle "class" constraint
            case Parser.Ast.ClassConstraint:
                if (inferredType.IsValueType)
                {
                    return InferenceResult.Failed(
                        InferenceErrorKind.ConstraintNotSatisfied,
                        $"Inferred type '{inferredType.GetDisplayName()}' for '{paramName}' is a value type, " +
                        $"but constraint requires a reference type (class)");
                }
                return InferenceResult.Succeeded(new List<SemanticType>());

            // Handle "struct" constraint
            case Parser.Ast.StructConstraint:
                if (!inferredType.IsValueType)
                {
                    return InferenceResult.Failed(
                        InferenceErrorKind.ConstraintNotSatisfied,
                        $"Inferred type '{inferredType.GetDisplayName()}' for '{paramName}' is a reference type, " +
                        $"but constraint requires a value type (struct)");
                }
                return InferenceResult.Succeeded(new List<SemanticType>());

            // Handle "notnull" constraint
            case Parser.Ast.NotnullConstraint:
                return InferenceResult.Succeeded(new List<SemanticType>());

            // Handle "new()" constraint
            case Parser.Ast.NewConstraint:
                // For now, accept all types for new() constraint
                // A more complete implementation would check for default constructor
                return InferenceResult.Succeeded(new List<SemanticType>());

            // Handle interface/type constraint
            case Parser.Ast.TypeConstraint tc:
                // The constraint is RESOLVED, not stringified (#1289): a declaration, not the text
                // that names it, is what the inferred type is compared against. Resolution mirrors the
                // type-parameter DEFAULT seam (TypeChecker.Definitions.cs), down to its rule for an
                // annotation that does not resolve: no proof either way, so no refusal — the emitted
                // C# still carries the constraint and Roslyn remains the backstop.
                var constraintType = ResolveConstraint(tc.Type, declaringParameters);
                if (constraintType == null)
                    return InferenceResult.Succeeded(new List<SemanticType>());

                if (!TypeSatisfiesConstraint(inferredType, constraintType))
                {
                    return InferenceResult.Failed(
                        InferenceErrorKind.ConstraintNotSatisfied,
                        $"{subject} '{inferredType.GetDisplayName()}' does not satisfy constraint " +
                        $"'{constraintType.GetDisplayName()}' for type parameter '{paramName}'");
                }
                return InferenceResult.Succeeded(new List<SemanticType>());

            default:
                // Unknown constraint type - accept by default
                return InferenceResult.Succeeded(new List<SemanticType>());
        }
    }

    /// <summary>
    /// The constraint annotation as a resolved type, or <c>null</c> when it does not resolve (#1289).
    /// <para>Resolution goes through the <see cref="TypeResolver"/> — the same authority every other
    /// annotation goes through — rather than the name-keyed lookup it replaces, which stripped the
    /// annotation at <c>[</c> and asked the symbol table for the remaining text. That text lookup is
    /// why the check could only ever compare spellings; going through the resolver is what lets the
    /// comparison be between declarations.</para>
    /// <para>A MODULE-QUALIFIED constraint (<c>T: shapes.Shape</c>) resolves here but is still refused
    /// downstream, and not by this check: with no generics anywhere, <c>s: shapes.Shape = c</c> is
    /// refused the same way, so the inheritance is invisible through that spelling (#1407).</para>
    /// </summary>
    /// <param name="declaringParameters">
    /// The type parameters the constraint was WRITTEN alongside. They are put back in scope for the
    /// resolution, because a constraint may name them — <c>T: Comparable[T]</c> is the ordinary
    /// spelling of a self-comparable bound — and nothing named <c>T</c> exists at the call site this
    /// check runs from. Without it the resolver reports "Type 'T' not found" against the declaration,
    /// once per compilation, for a program that is correct.
    /// </param>
    private SemanticType? ResolveConstraint(
        Parser.Ast.TypeAnnotation annotation, IReadOnlyList<TypeParameterDef> declaringParameters)
    {
        if (_typeResolver == null)
            return null;

        var scoped = PushConstraintScope(declaringParameters);
        try
        {
            var resolved = _typeResolver.ResolveTypeAnnotation(annotation);
            return resolved is UnknownType ? null : resolved;
        }
        finally
        {
            if (scoped)
                _symbolTable.ExitScope();
        }
    }

    /// <summary>
    /// Defines <paramref name="declaringParameters"/> in a scope of their own so a constraint
    /// annotation resolves the way its declaration reads it. Returns whether a scope was pushed —
    /// the caller pops exactly what it pushed.
    /// </summary>
    private bool PushConstraintScope(IReadOnlyList<TypeParameterDef> declaringParameters)
    {
        if (declaringParameters.Count == 0)
            return false;

        _symbolTable.EnterScope("constraint-resolution");
        foreach (var typeParam in declaringParameters)
        {
            _symbolTable.Define(new TypeParameterSymbol
            {
                Name = typeParam.Name,
                Kind = SymbolKind.TypeParameter,
                Constraints = typeParam.Constraints,
                Variance = typeParam.Variance,
                IsNameBacktickEscaped = typeParam.IsNameBacktickEscaped
            });
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="type"/> satisfies the resolved <paramref name="constraint"/> (#1289):
    /// the declarations are the same, one inherits the other, or the constraint is an interface the
    /// type implements. Self, subclass and interface all fall out of one comparator, and every
    /// comparison is between SYMBOLS — a cross-module spelling, an alias and a bare import name the
    /// same declaration, and only symbols can see that.
    /// <para>Type arguments are deliberately not compared: the constraint <c>T: Comparable[T]</c> is
    /// satisfied by a declaration that implements <c>Comparable</c> at all. The emitted C# carries the
    /// constructed constraint, so Roslyn remains the authority on the argument vector; re-deciding it
    /// here would be a second, weaker implementation of the rule.</para>
    /// </summary>
    private bool TypeSatisfiesConstraint(SemanticType type, SemanticType constraint)
    {
        // A primitive satisfies any type constraint. Long-standing behaviour, unchanged here: the
        // registry's builtins do not carry the interface lists (IComparable and friends) that would
        // let this be answered honestly, so refusing would reject working code.
        if (type is BuiltinType)
            return true;

        var constraintSymbol = ConstraintSymbolOf(constraint);
        if (constraintSymbol == null)
            return true; // a constraint with no declaration behind it proves nothing

        // A type parameter forwarded into another constrained call (`def outer[U: Shape]` calling
        // `inner(y)` where `inner` wants `T: Shape`) satisfies the callee when one of ITS OWN
        // constraints does — the same symbol comparison, one level in.
        if (type is TypeParameterType typeParameter)
        {
            // Its own constraints resolve in ITS declaration's scope, which for a self-referential
            // bound is the parameter itself.
            var ownScope = new[]
            {
                new TypeParameterDef { Name = typeParameter.Name, Constraints = typeParameter.Constraints }
            };

            foreach (var own in typeParameter.Constraints)
            {
                if (own is Parser.Ast.TypeConstraint ownConstraint
                    && ResolveConstraint(ownConstraint.Type, ownScope) is { } ownResolved
                    && SymbolSatisfiesConstraint(ConstraintSymbolOf(ownResolved), constraintSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        return SymbolSatisfiesConstraint(ConstraintSymbolOf(type), constraintSymbol);
    }

    /// <summary>
    /// The declaration a type names, for constraint comparison: a class/struct/interface reference names
    /// its symbol, and a constructed generic names its definition (<c>Comparable[Money]</c> is a
    /// <c>Comparable</c>).
    /// </summary>
    private static TypeSymbol? ConstraintSymbolOf(SemanticType type) => type switch
    {
        UserDefinedType { Symbol: { } symbol } => symbol,
        GenericType { GenericDefinition: { } definition } => definition,
        _ => null
    };

    private bool SymbolSatisfiesConstraint(TypeSymbol? candidate, TypeSymbol constraintSymbol)
    {
        if (candidate == null)
            return false;

        if (TypeHierarchyService.IsSameType(candidate, constraintSymbol))
            return true;

        if (TypeHierarchyService.InheritsFrom(candidate, constraintSymbol, SemanticBinding))
            return true;

        return TypeHierarchyService.GetAllInterfaces(candidate, SemanticBinding)
            .Any(iface => TypeHierarchyService.IsSameType(iface, constraintSymbol));
    }
}
