using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates type parameter variance annotations:
/// - Class/struct type parameters must not have variance (SPY0417)
/// - Covariant (out) type params must only appear in output positions (SPY0418)
/// - Contravariant (in) type params must only appear in input positions (SPY0419)
/// </summary>
internal class VarianceValidator : SemanticValidatorBase
{
    public override string Name => "VarianceValidator";
    public override int Order => 415; // After PropertyValidator (410), before UnusedVariableValidator (420)

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
                    code: DiagnosticCodes.Validation.VarianceOnClassOrStruct,
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
                CheckTypeInPosition(param.Type, variantParams, isCovariantPosition: false, context);
            }
        }

        // Check return type (covariant position)
        if (delegateDef.ReturnType != null)
        {
            CheckTypeInPosition(delegateDef.ReturnType, variantParams, isCovariantPosition: true, context);
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
                        CheckTypeInPosition(param.Type, variantParams, isCovariantPosition: false, context);
                    }
                }

                // Return type is in covariant position
                if (method.ReturnType != null)
                {
                    CheckTypeInPosition(method.ReturnType, variantParams, isCovariantPosition: true, context);
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
        bool isCovariantPosition,
        SemanticContext context)
    {
        // Check if the type annotation directly names a variant type parameter
        if (variantParams.TryGetValue(typeAnnotation.Name, out var variance))
        {
            if (variance == TypeParameterVariance.Covariant && !isCovariantPosition)
            {
                AddError(context,
                    $"Covariant type parameter '{typeAnnotation.Name}' cannot appear in contravariant position (parameter type)",
                    typeAnnotation.LineStart, typeAnnotation.ColumnStart,
                    code: DiagnosticCodes.Validation.CovariantInContravariantPosition,
                    span: typeAnnotation.Span);
            }
            else if (variance == TypeParameterVariance.Contravariant && isCovariantPosition)
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
            RecurseIntoGenericTypeArguments(typeAnnotation, variantParams, isCovariantPosition, context);
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
        bool isCovariantPosition,
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
                bool effectivePosition = !isCovariantPosition;
                CheckTypeInPosition(typeAnnotation.TypeArguments[i], variantParams, effectivePosition, context);
            }

            // The last type argument is the return type (covariant position: same as context)
            if (typeAnnotation.TypeArguments.Length > 0)
            {
                var returnArg = typeAnnotation.TypeArguments[typeAnnotation.TypeArguments.Length - 1];
                CheckTypeInPosition(returnArg, variantParams, isCovariantPosition, context);
            }

            return;
        }

        // For named generic types (e.g., IProducer[T], IConsumer[T]), look up
        // the type's declared type parameters to determine their variance
        var typeParamVariances = LookupTypeParameterVariances(typeAnnotation.Name, context);

        for (int i = 0; i < typeAnnotation.TypeArguments.Length; i++)
        {
            var argVariance = (typeParamVariances.HasValue && i < typeParamVariances.Value.Length)
                ? typeParamVariances.Value[i]
                : TypeParameterVariance.None;

            bool effectivePosition = CombineVariance(isCovariantPosition, argVariance);
            CheckTypeInPosition(typeAnnotation.TypeArguments[i], variantParams, effectivePosition, context);
        }
    }

    /// <summary>
    /// Looks up the declared type parameter variances for a named generic type.
    /// Returns null if the type is not found or has no type parameters.
    /// </summary>
    private static System.Collections.Immutable.ImmutableArray<TypeParameterVariance>?
        LookupTypeParameterVariances(string typeName, SemanticContext context)
    {
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
    private static bool CombineVariance(bool isCovariantPosition, TypeParameterVariance argVariance)
    {
        // Invariant type parameter: position is unchanged (treated as both input and output)
        if (argVariance == TypeParameterVariance.None)
            return isCovariantPosition;

        // Covariant (out) type parameter: same direction as context
        if (argVariance == TypeParameterVariance.Covariant)
            return isCovariantPosition;

        // Contravariant (in) type parameter: flip the context!
        // covariant context + contravariant arg = contravariant
        // contravariant context + contravariant arg = covariant (double flip)
        return !isCovariantPosition;
    }
}
