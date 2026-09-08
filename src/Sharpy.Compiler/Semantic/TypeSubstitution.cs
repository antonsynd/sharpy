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
    /// <param name="substituteNamedUserTypes">
    /// When true, a <see cref="UserDefinedType"/> whose name matches a substitution key is also
    /// replaced. Imported generic definitions sometimes materialize a type-parameter reference in
    /// a member signature as a bare <c>UserDefinedType</c> named after the parameter (e.g. the
    /// return type <c>T</c> of an imported <c>Box[T].get()</c>) rather than a
    /// <see cref="TypeParameterType"/>. Within the scope of the owning generic, such a name
    /// unambiguously denotes the type parameter, so callers resolving members on a specific
    /// instantiation opt in to substitute it. Off by default to preserve behavior for callers
    /// that only substitute genuine type parameters.
    /// </param>
    /// <returns>The type with all matching type parameters replaced.</returns>
    internal static SemanticType Apply(
        SemanticType type,
        IReadOnlyDictionary<string, SemanticType> substitutions,
        bool substituteNamedUserTypes = false)
    {
        return type switch
        {
            TypeParameterType tpt when substitutions.TryGetValue(tpt.Name, out var subst) => subst,
            UserDefinedType udt when substituteNamedUserTypes && substitutions.TryGetValue(udt.Name, out var namedSubst) => namedSubst,
            GenericType gt => new GenericType
            {
                Name = gt.Name,
                TypeArguments = gt.TypeArguments.Select(t => Apply(t, substitutions, substituteNamedUserTypes)).ToList(),
                GenericDefinition = gt.GenericDefinition,
                ClrOriginTypeName = gt.ClrOriginTypeName
            },
            // `T | None` written on a TYPE PARAMETER is C#'s unconstrained `T?`, which is the
            // nullable-reference ANNOTATION: for a reference instantiation it is `T?` (nullable),
            // for a VALUE instantiation it is plain `T` — there is no `Nullable<T>` behind it and
            // `null` cannot be passed (CS1503 `<null>` to `int` at the emitted call). The closed
            // slot must say what the emitted parameter is, so a value-type binding collapses the
            // nullable wrapper (#1797, plan-499995 Design Decision 3; Axiom 1). A `T | None` on
            // anything but a bare type parameter (`list[T] | None`) is an ordinary reference slot.
            NullableType { UnderlyingType: TypeParameterType tp } when substitutions.TryGetValue(tp.Name, out var bound)
                && bound.IsValueType && bound is not TypeParameterType => bound,
            NullableType nt => new NullableType
            {
                UnderlyingType = Apply(nt.UnderlyingType, substitutions, substituteNamedUserTypes)
            },
            OptionalType ot => new OptionalType
            {
                UnderlyingType = Apply(ot.UnderlyingType, substitutions, substituteNamedUserTypes)
            },
            ResultType rt => new ResultType
            {
                OkType = Apply(rt.OkType, substitutions, substituteNamedUserTypes),
                ErrorType = Apply(rt.ErrorType, substitutions, substituteNamedUserTypes)
            },
            FunctionType ft => new FunctionType
            {
                ParameterTypes = ft.ParameterTypes.Select(t => Apply(t, substitutions, substituteNamedUserTypes)).ToList(),
                ReturnType = Apply(ft.ReturnType, substitutions, substituteNamedUserTypes),
                OptionalParameterCount = ft.OptionalParameterCount,
                VariadicParameterIndex = ft.VariadicParameterIndex
            },
            TupleType tt => new TupleType
            {
                ElementTypes = tt.ElementTypes.Select(t => Apply(t, substitutions, substituteNamedUserTypes)).ToList()
            },
            UnionType ut => new UnionType
            {
                Name = ut.Name,
                Symbol = ut.Symbol,
                CaseTypes = ut.CaseTypes.Select(t => Apply(t, substitutions, substituteNamedUserTypes)).ToList()
            },
            TaskType { ResultType: not null } taskT => new TaskType
            {
                ResultType = Apply(taskT.ResultType, substitutions, substituteNamedUserTypes)
            },
            _ => type
        };
    }
}
