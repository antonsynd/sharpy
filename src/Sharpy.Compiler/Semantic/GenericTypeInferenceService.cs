using Sharpy.Compiler.Parser.Ast;

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
                // PEP 696: try using the type parameter default
                if (typeParam.DefaultType != null && _typeResolver != null)
                {
                    inferredType = _typeResolver.ResolveTypeAnnotation(typeParam.DefaultType);
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

            var constraintResult = CheckConstraints(typeParam, inferredType, substitutions);
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
    private InferenceResult Unify(SemanticType formal, SemanticType actual, Dictionary<string, SemanticType> substitutions)
    {
        // Case 1: Formal type is a type parameter
        if (formal is TypeParameterType typeParam)
        {
            return UnifyTypeParameter(typeParam.Name, actual, substitutions);
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
    private InferenceResult UnifyTypeParameter(string paramName, SemanticType actual, Dictionary<string, SemanticType> substitutions)
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
            // Different generic types — can't extract type parameter bindings from this pair,
            // but don't abort inference (other arguments may provide bindings)
            return InferenceResult.Succeeded(new List<SemanticType>());
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
    /// Check that an inferred type satisfies all constraints on a type parameter.
    /// </summary>
    private InferenceResult CheckConstraints(TypeParameterDef typeParam, SemanticType inferredType, Dictionary<string, SemanticType> substitutions)
    {
        foreach (var constraint in typeParam.Constraints)
        {
            var result = CheckSingleConstraint(typeParam.Name, inferredType, constraint, substitutions);
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
    private InferenceResult CheckSingleConstraint(string paramName, SemanticType inferredType, ConstraintClause constraint, Dictionary<string, SemanticType> substitutions)
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

            // Handle "new()" constraint
            case Parser.Ast.NewConstraint:
                // For now, accept all types for new() constraint
                // A more complete implementation would check for default constructor
                return InferenceResult.Succeeded(new List<SemanticType>());

            // Handle interface/type constraint
            case Parser.Ast.TypeConstraint tc:
                var constraintTypeName = GetTypeAnnotationName(tc.Type);

                // Substitute type parameters in the constraint
                // E.g., for T: IComparable[T], substitute T with the inferred type
                var substitutedConstraint = SubstituteInConstraint(constraintTypeName, substitutions);

                // Check if inferredType implements/extends the constraint type
                if (!TypeSatisfiesConstraint(inferredType, substitutedConstraint))
                {
                    return InferenceResult.Failed(
                        InferenceErrorKind.ConstraintNotSatisfied,
                        $"Inferred type '{inferredType.GetDisplayName()}' does not satisfy constraint " +
                        $"'{substitutedConstraint}' for type parameter '{paramName}'");
                }
                return InferenceResult.Succeeded(new List<SemanticType>());

            default:
                // Unknown constraint type - accept by default
                return InferenceResult.Succeeded(new List<SemanticType>());
        }
    }

    /// <summary>
    /// Get a string representation of a type annotation for constraint checking.
    /// </summary>
    private string GetTypeAnnotationName(Parser.Ast.TypeAnnotation typeAnnotation)
    {
        var baseName = typeAnnotation.Name;

        // Add type arguments if present
        if (typeAnnotation.TypeArguments.Length > 0)
        {
            baseName = $"{baseName}[{string.Join(", ", typeAnnotation.TypeArguments.Select(GetTypeAnnotationName))}]";
        }

        // Add nullable suffix if nullable
        if (typeAnnotation.IsOptional)
        {
            baseName = $"{baseName}?";
        }

        return baseName;
    }

    /// <summary>
    /// Substitute type parameters in a constraint type name.
    /// </summary>
    private string SubstituteInConstraint(string constraintTypeName, Dictionary<string, SemanticType> substitutions)
    {
        var result = constraintTypeName;
        foreach (var (paramName, inferredType) in substitutions)
        {
            result = result.Replace(paramName, inferredType.GetDisplayName(), StringComparison.Ordinal);
        }
        return result;
    }

    /// <summary>
    /// Check if a type satisfies a constraint (implements interface or extends class).
    /// </summary>
    private bool TypeSatisfiesConstraint(SemanticType type, string constraintTypeName)
    {
        // For now, accept all constraints as satisfied for primitive types
        // This is a simplification - full constraint checking requires looking up the constraint type
        // and checking interface implementation
        if (type is BuiltinType)
        {
            // Primitive types satisfy common constraints like IComparable
            // A more complete implementation would check the actual interface implementation
            return true;
        }

        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            // Check if the type implements the constraint interface
            // Look for the constraint type name in the symbol's interfaces
            foreach (var iface in TypeHierarchyService.GetAllInterfaces(udt.Symbol, SemanticBinding))
            {
                if (InterfaceMatchesConstraint(iface, constraintTypeName))
                {
                    return true;
                }
            }

            // Check base types
            foreach (var baseSymbol in TypeHierarchyService.GetAllBaseTypes(udt.Symbol, SemanticBinding))
            {
                var baseType = new UserDefinedType { Symbol = baseSymbol, Name = baseSymbol.Name };
                if (TypeSatisfiesConstraint(baseType, constraintTypeName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if an interface matches a constraint name (possibly with type arguments).
    /// </summary>
    private bool InterfaceMatchesConstraint(TypeSymbol iface, string constraintTypeName)
    {
        // Simple name match
        if (iface.Name == constraintTypeName)
            return true;

        // Check for generic constraint like IComparable[int]
        // Parse out the base name from IComparable[int] -> IComparable
        var bracketIndex = constraintTypeName.IndexOf('[', StringComparison.Ordinal);
        if (bracketIndex > 0)
        {
            var baseName = constraintTypeName.Substring(0, bracketIndex);
            if (iface.Name == baseName)
                return true;
        }

        return false;
    }
}
