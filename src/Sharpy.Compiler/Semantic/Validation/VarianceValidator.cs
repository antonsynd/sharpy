using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates type parameter variance annotations:
/// - Class/struct/method/function type parameters must not have variance (SPY0417)
/// - Covariant (out) type params must only appear in output positions (SPY0418)
/// - Contravariant (in) type params must only appear in input positions (SPY0419)
/// Variance is only valid on interface and delegate type parameters.
///
/// <para>A position is one of THREE things, not two (C#'s "must be invariantly valid" rule, CS1961): output,
/// input, or <b>invariant</b> — the argument position of a generic whose own type parameter declares
/// no variance. A variant type parameter is illegal in an invariant position REGARDLESS of direction,
/// so <c>def get_list(self) -> list[T]</c> under <c>interface ICovariant[out T]</c> is refused here
/// (SPY0418) instead of reaching Roslyn as CS1961 behind SPY0908. The two-valued model this replaced
/// read an invariant argument position as "unchanged", which let every such declaration through
/// (#1748).</para>
/// </summary>
internal class VarianceValidator : SemanticValidatorBase
{
    public override string Name => "VarianceValidator";
    public override int Order => 415; // After PropertyValidator (410), before UnusedVariableValidator (420)

    /// <summary>
    /// Where a type is written, as variance sees it. <see cref="Invariant"/> is a position in its own
    /// right — not "either of the other two" — because a generic that declares no variance on the
    /// parameter it is instantiated at admits NEITHER direction there.
    /// </summary>
    private enum VariancePosition
    {
        Covariant,
        Contravariant,
        Invariant,
    }

    public override void Validate(Module module, SemanticContext context)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ValidateNoVarianceOnClassOrStruct(classDef.Name, classDef.TypeParameters, stmt, context);
                    break;

                case StructDef structDef:
                    ValidateNoVarianceOnClassOrStruct(structDef.Name, structDef.TypeParameters, stmt, context);
                    break;

                case InterfaceDef interfaceDef:
                    ValidateVariancePositions(interfaceDef.Name, interfaceDef.TypeParameters, interfaceDef.Body, context);
                    break;

                case DelegateDef delegateDef:
                    ValidateDelegateVariancePositions(delegateDef, context);
                    break;
                default:
                    // walker-default-contract: any kind not listed above is deliberately ignored by
                    // this walker (rostered in DispatchSiteInventoryTests).
                    break;
            }
        }

        // Variance is never valid on a method's or function's own type parameters
        // (C# only permits variance on interface/delegate type parameters; otherwise CS1960).
        // Walk every FunctionDef in the module — top-level functions, methods inside
        // class/struct/interface bodies, and functions nested inside other bodies.
        WalkFunctionsForVariance(module.Body, isInsideType: false, context);
    }

    /// <summary>
    /// Recursively visits every <see cref="FunctionDef"/> reachable from the given
    /// statements and rejects variance on its own type parameters. The
    /// <paramref name="isInsideType"/> flag tracks whether the current statements are
    /// the direct body of a class/struct/interface, so members are reported as
    /// "method" and free/nested functions as "function".
    /// </summary>
    private void WalkFunctionsForVariance(
        IEnumerable<Statement> statements,
        bool isInsideType,
        SemanticContext context)
    {
        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case FunctionDef fn:
                    ValidateNoVarianceOnFunction(fn, isInsideType, context);
                    // Functions/classes nested inside a function body are not type members.
                    WalkFunctionsForVariance(fn.Body, isInsideType: false, context);
                    break;

                case ClassDef classDef:
                    WalkFunctionsForVariance(classDef.Body, isInsideType: true, context);
                    break;

                case StructDef structDef:
                    WalkFunctionsForVariance(structDef.Body, isInsideType: true, context);
                    break;

                case InterfaceDef interfaceDef:
                    WalkFunctionsForVariance(interfaceDef.Body, isInsideType: true, context);
                    break;

                default:
                    // Control-flow and other compound statements: recurse into any
                    // nested statements, preserving the enclosing type context.
                    WalkFunctionsForVariance(stmt.GetChildNodes().OfType<Statement>(), isInsideType, context);
                    break;
            }
        }
    }

    /// <summary>
    /// Rejects variance annotations on a function/method's own type parameters (SPY0417).
    /// </summary>
    private void ValidateNoVarianceOnFunction(FunctionDef fn, bool isMethod, SemanticContext context)
    {
        foreach (var typeParam in fn.TypeParameters)
        {
            if (typeParam.Variance != TypeParameterVariance.None)
            {
                var kind = isMethod ? "method" : "function";
                AddError(context,
                    $"Type parameter '{typeParam.Name}' cannot have variance annotation on {kind} '{fn.Name}'",
                    typeParam.LineStart, typeParam.ColumnStart,
                    code: DiagnosticCodes.Validation.VarianceNotAllowed,
                    span: typeParam.Span);
            }
        }
    }

    private void ValidateNoVarianceOnClassOrStruct(
        string typeName,
        System.Collections.Immutable.ImmutableArray<TypeParameterDef> typeParams,
        Statement stmt,
        SemanticContext context)
    {
        foreach (var typeParam in typeParams)
        {
            if (typeParam.Variance != TypeParameterVariance.None)
            {
                AddError(context,
                    $"Type parameter '{typeParam.Name}' cannot have variance annotation on class/struct '{typeName}'",
                    typeParam.LineStart, typeParam.ColumnStart,
                    code: DiagnosticCodes.Validation.VarianceNotAllowed,
                    span: typeParam.Span);
            }
        }
    }

    private void ValidateDelegateVariancePositions(DelegateDef delegateDef, SemanticContext context)
    {
        var variantParams = GetVariantTypeParams(delegateDef.TypeParameters);
        if (variantParams.Count == 0)
            return;

        // Check parameter types (contravariant position)
        foreach (var param in delegateDef.Parameters)
        {
            if (param.Type != null)
            {
                CheckTypeInPosition(param.Type, variantParams, VariancePosition.Contravariant, invariantHost: null, context);
            }
        }

        // Check return type (covariant position)
        if (delegateDef.ReturnType != null)
        {
            CheckTypeInPosition(delegateDef.ReturnType, variantParams, VariancePosition.Covariant, invariantHost: null, context);
        }
    }

    private void ValidateVariancePositions(
        string typeName,
        System.Collections.Immutable.ImmutableArray<TypeParameterDef> typeParams,
        System.Collections.Immutable.ImmutableArray<Statement> body,
        SemanticContext context)
    {
        var variantParams = GetVariantTypeParams(typeParams);
        if (variantParams.Count == 0)
            return;

        foreach (var stmt in body)
        {
            if (stmt is FunctionDef method)
            {
                // Parameters are in contravariant position
                foreach (var param in method.Parameters)
                {
                    // Skip 'self' parameter
                    if (param.Name == "self")
                        continue;

                    if (param.Type != null)
                    {
                        CheckTypeInPosition(param.Type, variantParams, VariancePosition.Contravariant, invariantHost: null, context);
                    }
                }

                // Return type is in covariant position
                if (method.ReturnType != null)
                {
                    CheckTypeInPosition(method.ReturnType, variantParams, VariancePosition.Covariant, invariantHost: null, context);
                }
            }
        }
    }

    private static Dictionary<string, TypeParameterVariance> GetVariantTypeParams(
        System.Collections.Immutable.ImmutableArray<TypeParameterDef> typeParams)
    {
        var result = new Dictionary<string, TypeParameterVariance>();
        foreach (var tp in typeParams)
        {
            if (tp.Variance != TypeParameterVariance.None)
            {
                result[tp.Name] = tp.Variance;
            }
        }
        return result;
    }

    private void CheckTypeInPosition(
        TypeAnnotation typeAnnotation,
        Dictionary<string, TypeParameterVariance> variantParams,
        VariancePosition position,
        TypeAnnotation? invariantHost,
        SemanticContext context)
    {
        // `T?` and `T !E` are WRAPPERS, not positions of their own: the annotation that names the type
        // parameter carries the flag, so the parameter is written INSIDE `Sharpy.Optional<T>` /
        // `Result<T, E>` — invariant structs. Both were CS1961 behind SPY0908 (`-> T?` is in this
        // spec's own "valid out positions" list, and it never compiled), and `T` on the ERROR side of
        // a Result was not examined at all. `T | None` is deliberately NOT here: it emits C#'s nullable
        // ANNOTATION on `T` itself, which Roslyn accepts in a variant position (measured).
        if (typeAnnotation.IsOptional || typeAnnotation.IsResult)
        {
            position = VariancePosition.Invariant;
            invariantHost = typeAnnotation;
        }

        // Check if the type annotation directly names a variant type parameter
        if (variantParams.TryGetValue(typeAnnotation.Name, out var variance))
        {
            // An invariant position admits neither direction, and the type that MAKES it invariant is
            // what the user has to change — so the message names that type (`list[T]`), not the
            // direction. This is the CS1961 case the C# compiler used to report from behind SPY0908.
            if (position == VariancePosition.Invariant)
            {
                var host = TypeAnnotationHelper.GetName(invariantHost);
                var isCovariant = variance == TypeParameterVariance.Covariant;
                var direction = isCovariant ? "Covariant" : "Contravariant";
                var keyword = isCovariant ? "out" : "in";
                // The steer names a type that IS variant in the needed direction — a covariant
                // sequence for `out T`, a consumer callback for `in T`.
                var steer = isCovariant
                    ? $"Use a covariant interface (e.g. 'IEnumerable[{typeAnnotation.Name}]')"
                    : $"Use a contravariant position (e.g. a '({typeAnnotation.Name}) -> None' parameter)";
                AddError(context,
                    $"{direction} type parameter '{typeAnnotation.Name}' cannot appear in invariant position "
                        + $"'{host}' — '{host}' is invariant in '{typeAnnotation.Name}'. "
                        + $"{steer} or remove '{keyword}' from '{typeAnnotation.Name}'",
                    typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                    code: variance == TypeParameterVariance.Covariant
                        ? DiagnosticCodes.Validation.CovariantInContravariantPosition
                        : DiagnosticCodes.Validation.ContravariantInCovariantPosition,
                    span: typeAnnotation.Span);
            }
            else if (variance == TypeParameterVariance.Covariant && position == VariancePosition.Contravariant)
            {
                AddError(context,
                    $"Covariant type parameter '{typeAnnotation.Name}' cannot appear in contravariant position (parameter type)",
                    typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                    code: DiagnosticCodes.Validation.CovariantInContravariantPosition,
                    span: typeAnnotation.Span);
            }
            else if (variance == TypeParameterVariance.Contravariant && position == VariancePosition.Covariant)
            {
                AddError(context,
                    $"Contravariant type parameter '{typeAnnotation.Name}' cannot appear in covariant position (return type)",
                    typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                    code: DiagnosticCodes.Validation.ContravariantInCovariantPosition,
                    span: typeAnnotation.Span);
            }
        }

        // Recurse into generic type arguments with variance flipping
        if (typeAnnotation.TypeArguments.Length > 0)
        {
            RecurseIntoGenericTypeArguments(typeAnnotation, variantParams, position, invariantHost, context);
        }

        // The error side of `T !E` is a type argument of `Result<T, E>` that no TypeArguments walk
        // reaches, so it is visited here — under the same invariant position the wrapper established.
        if (typeAnnotation.ErrorType is { } errorType)
        {
            CheckTypeInPosition(errorType, variantParams, position, invariantHost, context);
        }
    }

    /// <summary>
    /// Recurses into the type arguments of a generic type, applying variance flip rules.
    /// When a type argument position has its own declared variance:
    /// - Covariant arg in covariant context → covariant (same)
    /// - Covariant arg in contravariant context → contravariant (flipped!)
    /// - Contravariant arg in covariant context → contravariant (same)
    /// - Contravariant arg in contravariant context → covariant (flipped!)
    /// </summary>
    private void RecurseIntoGenericTypeArguments(
        TypeAnnotation typeAnnotation,
        Dictionary<string, TypeParameterVariance> variantParams,
        VariancePosition position,
        TypeAnnotation? invariantHost,
        SemanticContext context)
    {
        // Function types: (T1, T2, ...) -> R
        // Represented as Name="function", TypeArguments=[param_types..., return_type]
        if (typeAnnotation.Name == BuiltinNames.Function)
        {
            // All type arguments except the last are parameter types (contravariant position)
            for (int i = 0; i < typeAnnotation.TypeArguments.Length - 1; i++)
            {
                // Parameter positions of a function are contravariant: flip the context
                CheckTypeInPosition(
                    typeAnnotation.TypeArguments[i], variantParams, Flip(position), invariantHost, context);
            }

            // The last type argument is the return type (covariant position: same as context)
            if (typeAnnotation.TypeArguments.Length > 0)
            {
                var returnArg = typeAnnotation.TypeArguments[typeAnnotation.TypeArguments.Length - 1];
                CheckTypeInPosition(returnArg, variantParams, position, invariantHost, context);
            }

            return;
        }

        // For named generic types (e.g., IProducer[T], IConsumer[T], list[T]), look up the type's
        // declared type parameters to determine their variance. A type the symbol table does not know
        // gets NO opinion (null): guessing "invariant" for an unresolved name would refuse legal
        // declarations whose generic simply has not been resolved here.
        var typeParamVariances = LookupTypeParameterVariances(typeAnnotation.Name, context);

        for (int i = 0; i < typeAnnotation.TypeArguments.Length; i++)
        {
            if (typeParamVariances is not { } variances || i >= variances.Length)
            {
                // Unknown declaration: carry the context through unchanged, as the two-valued model did.
                CheckTypeInPosition(
                    typeAnnotation.TypeArguments[i], variantParams, position, invariantHost, context);
                continue;
            }

            var argVariance = variances[i];
            var effectivePosition = CombineVariance(position, argVariance);

            // The host is the NEAREST enclosing generic whose argument position is invariant — the one
            // whose spelling the message quotes, and the one the user would change.
            var effectiveHost = effectivePosition == VariancePosition.Invariant
                ? typeAnnotation
                : invariantHost;

            CheckTypeInPosition(
                typeAnnotation.TypeArguments[i], variantParams, effectivePosition, effectiveHost, context);
        }
    }

    /// <summary>
    /// Looks up the declared type parameter variances for a named generic type.
    /// Returns null if the type is not found or has no type parameters.
    /// </summary>
    private static System.Collections.Immutable.ImmutableArray<TypeParameterVariance>?
        LookupTypeParameterVariances(string typeName, SemanticContext context)
    {
        // `list`/`set`/`dict` resolve here through the BuiltinRegistry, which reads variance off the
        // CLR type — so this validator and `TypeChecker.TypeArgumentsSatisfyVariance` agree on what
        // `list[T]` admits by construction, from one declaration (#1748).
        var typeSymbol = context.SymbolTable.LookupType(typeName);
        if (typeSymbol != null && typeSymbol.TypeParameters.Count > 0)
        {
            var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<TypeParameterVariance>(
                typeSymbol.TypeParameters.Count);
            foreach (var tp in typeSymbol.TypeParameters)
            {
                builder.Add(tp.Variance);
            }
            return builder.MoveToImmutable();
        }

        return null;
    }

    /// <summary>
    /// Combines the current context position with the declared variance of a type argument position.
    /// Returns the effective covariant-position for the nested type argument.
    /// </summary>
    private static VariancePosition CombineVariance(VariancePosition position, TypeParameterVariance argVariance)
    {
        // Invariant type parameter: the argument position is INVARIANT, whatever the context was. This
        // is C#'s "must be invariantly valid" — a variant parameter written there is CS1961 in either
        // direction. Reading it as "unchanged" is what let `-> list[T]` under `out T` through (#1748).
        if (argVariance == TypeParameterVariance.None)
            return VariancePosition.Invariant;

        // Covariant (out) type parameter: same direction as context
        if (argVariance == TypeParameterVariance.Covariant)
            return position;

        // Contravariant (in) type parameter: flip the context!
        // covariant context + contravariant arg = contravariant
        // contravariant context + contravariant arg = covariant (double flip)
        return Flip(position);
    }

    /// <summary>
    /// Direction reversal. Invariant has no direction to reverse and stays itself — once a position is
    /// invariant, no amount of nesting makes it admit a variant parameter again.
    /// </summary>
    private static VariancePosition Flip(VariancePosition position) => position switch
    {
        VariancePosition.Covariant => VariancePosition.Contravariant,
        VariancePosition.Contravariant => VariancePosition.Covariant,
        _ => VariancePosition.Invariant,
    };
}
