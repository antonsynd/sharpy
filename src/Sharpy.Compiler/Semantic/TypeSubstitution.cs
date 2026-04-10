namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Shared utility for applying type-parameter substitutions to semantic types.
/// Recursively handles all composite type forms (GenericType, NullableType, etc.).
/// </summary>
internal static class TypeSubstitution
{
    /// <summary>
    /// Applies type parameter substitutions to a semantic type, recursively handling
    /// all composite type forms.
    /// </summary>
    /// <param name="type">The type to substitute into.</param>
    /// <param name="substitutions">Map from type parameter name to concrete type.</param>
    /// <returns>The type with all matching type parameters replaced.</returns>
    internal static SemanticType Apply(SemanticType type, IReadOnlyDictionary<string, SemanticType> substitutions)
    {
        return type switch
        {
            TypeParameterType tpt when substitutions.TryGetValue(tpt.Name, out var subst) => subst,
            GenericType gt => new GenericType
            {
                Name = gt.Name,
                TypeArguments = gt.TypeArguments.Select(t => Apply(t, substitutions)).ToList(),
                GenericDefinition = gt.GenericDefinition
            },
            NullableType nt => new NullableType
            {
                UnderlyingType = Apply(nt.UnderlyingType, substitutions)
            },
            OptionalType ot => new OptionalType
            {
                UnderlyingType = Apply(ot.UnderlyingType, substitutions)
            },
            ResultType rt => new ResultType
            {
                OkType = Apply(rt.OkType, substitutions),
                ErrorType = Apply(rt.ErrorType, substitutions)
            },
            FunctionType ft => new FunctionType
            {
                ParameterTypes = ft.ParameterTypes.Select(t => Apply(t, substitutions)).ToList(),
                ReturnType = Apply(ft.ReturnType, substitutions),
                OptionalParameterCount = ft.OptionalParameterCount,
                VariadicParameterIndex = ft.VariadicParameterIndex
            },
            TupleType tt => new TupleType
            {
                ElementTypes = tt.ElementTypes.Select(t => Apply(t, substitutions)).ToList()
            },
            _ => type // For types that don't contain type parameters, return as-is
        };
    }
}
