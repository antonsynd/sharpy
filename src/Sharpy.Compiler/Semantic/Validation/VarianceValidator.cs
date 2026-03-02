using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

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

    // TODO(#256): Implement nested variance flip rules for generic type arguments.
    // Currently only checks direct type parameter references at the top level.
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
    }
}
