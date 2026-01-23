using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Adapter that wraps a legacy validator action to work with the new pipeline.
/// This enables gradual migration of existing validators.
/// </summary>
/// <remarks>
/// DEPRECATED: This adapter is for legacy code that hasn't migrated to V2 validators.
/// New code should use ValidationPipelineFactory.CreateDefault() with V2 validators.
/// </remarks>
#pragma warning disable CS0618 // Intentionally using obsolete legacy validators for backward compatibility
public class LegacyValidatorAdapter : ISemanticValidator
{
    private readonly Action<Module, SemanticContext> _validateAction;
    private readonly Func<IReadOnlyList<SemanticError>>? _getErrors;

    public string Name { get; }
    public int Order { get; }

    public LegacyValidatorAdapter(
        string name,
        int order,
        Action<Module, SemanticContext> validateAction,
        Func<IReadOnlyList<SemanticError>>? getErrors = null)
    {
        Name = name;
        Order = order;
        _validateAction = validateAction;
        _getErrors = getErrors;
    }

    public void Validate(Module module, SemanticContext context)
    {
        _validateAction(module, context);

        // If the legacy validator has an error collection, merge them
        if (_getErrors != null)
        {
            context.MergeFromLegacyErrors(_getErrors());
        }
    }

    /// <summary>
    /// Create an adapter for ControlFlowValidator.
    /// </summary>
    public static LegacyValidatorAdapter ForControlFlowValidator(
        ControlFlowValidator validator,
        ICompilerLogger? logger = null)
    {
        return new LegacyValidatorAdapter(
            "ControlFlowValidator",
            400, // Run after type checking
            (module, context) =>
            {
                // ControlFlowValidator validates functions individually
                // We need to traverse the module and validate each function
                foreach (var stmt in module.Body)
                {
                    if (stmt is FunctionDef funcDef)
                    {
                        var returnType = GetFunctionReturnType(funcDef, context);
                        validator.ValidateFunction(funcDef, returnType);
                    }
                    else if (stmt is ClassDef classDef)
                    {
                        foreach (var member in classDef.Body)
                        {
                            if (member is FunctionDef methodDef)
                            {
                                var returnType = GetFunctionReturnType(methodDef, context);
                                validator.ValidateFunction(methodDef, returnType);
                            }
                        }
                    }
                    else if (stmt is StructDef structDef)
                    {
                        foreach (var member in structDef.Body)
                        {
                            if (member is FunctionDef methodDef)
                            {
                                var returnType = GetFunctionReturnType(methodDef, context);
                                validator.ValidateFunction(methodDef, returnType);
                            }
                        }
                    }
                }
            },
            () => validator.Errors
        );
    }

    /// <summary>
    /// Helper to get the return type of a function from its type annotation.
    /// </summary>
    private static SemanticType GetFunctionReturnType(FunctionDef funcDef, SemanticContext context)
    {
        if (funcDef.ReturnType == null)
            return SemanticType.Void;

        // Try to get from semantic info cache first
        var cachedType = context.SemanticInfo.GetTypeAnnotation(funcDef.ReturnType);
        if (cachedType != null)
            return cachedType;

        // Fall back to resolving the type annotation
        return context.TypeResolver.ResolveTypeAnnotation(funcDef.ReturnType);
    }

    /// <summary>
    /// Create an adapter for AccessValidator.
    /// Note: AccessValidator is typically called during expression type-checking,
    /// so this adapter is mainly for testing.
    /// </summary>
    public static LegacyValidatorAdapter ForAccessValidator(
        AccessValidator validator,
        ICompilerLogger? logger = null)
    {
        return new LegacyValidatorAdapter(
            "AccessValidator",
            350, // Run during/after type checking
            (module, context) =>
            {
                // AccessValidator is called on-demand during type checking
                // This adapter is mainly for completeness
            },
            () => validator.Errors
        );
    }
}
#pragma warning restore CS0618
