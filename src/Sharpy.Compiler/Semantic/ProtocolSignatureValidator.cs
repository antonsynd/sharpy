using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates protocol method (non-operator dunder) signatures for Sharpy types.
/// Complements OperatorSignatureValidator for non-operator dunders.
/// </summary>
public static class ProtocolSignatureValidator
{
    /// <summary>
    /// Checks if a method name is a recognized protocol dunder method.
    /// </summary>
    public static bool IsProtocolDunder(string methodName)
        => ProtocolRegistry.IsProtocolDunder(methodName);

    /// <summary>
    /// Validates the signature of a protocol dunder method.
    /// Returns a list of semantic errors if the signature is invalid.
    /// </summary>
    public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var errors = new List<SemanticError>();
        var methodName = funcDef.Name;

        var protocol = ProtocolRegistry.GetProtocol(methodName);
        if (protocol == null)
        {
            // Not a protocol dunder, no validation needed
            return errors;
        }

        // Validate based on protocol-specific rules
        ValidateParameterCount(funcDef, protocol, owningType, errors);
        ValidateReturnType(funcDef, protocol, owningType, errors);
        ValidateSelfParameter(funcDef, protocol, owningType, errors);

        return errors;
    }

    private static void ValidateParameterCount(
        FunctionDef funcDef,
        ProtocolInfo protocol,
        TypeSymbol owningType,
        List<SemanticError> errors)
    {
        var actualCount = funcDef.Parameters.Count;
        var expectedCount = protocol.ExpectedParamCount;

        // -1 means any count (e.g., __init__ can have any number of params)
        if (expectedCount == -1)
            return;

        if (actualCount != expectedCount)
        {
            var paramDescription = expectedCount switch
            {
                1 => "(self)",
                2 => "(self, other)",
                3 => "(self, key, value)",
                _ => $"({expectedCount} parameters)"
            };

            errors.Add(new SemanticError(
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have exactly " +
                $"{expectedCount} parameter{(expectedCount == 1 ? "" : "s")} {paramDescription}, got {actualCount}. " +
                (protocol.SharpyCoreInterface != null
                    ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
                    : ""),
                funcDef.LineStart,
                funcDef.ColumnStart));
        }
    }

    private static void ValidateReturnType(
        FunctionDef funcDef,
        ProtocolInfo protocol,
        TypeSymbol owningType,
        List<SemanticError> errors)
    {
        // Skip if no return type expectation (null means any type is valid)
        if (protocol.ExpectedReturnType == null)
            return;

        // Skip if no return type annotation (will be inferred or default to void)
        if (funcDef.ReturnType == null)
            return;

        var actualReturnType = GetTypeAnnotationName(funcDef.ReturnType);

        // Normalize: "None" and "void" are equivalent
        var expectedNormalized = protocol.ExpectedReturnType == "None" ? "void" : protocol.ExpectedReturnType;
        var actualNormalized = actualReturnType == "None" ? "void" : actualReturnType;

        if (!string.Equals(actualNormalized, expectedNormalized, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actualReturnType, protocol.ExpectedReturnType, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new SemanticError(
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must return " +
                $"'{protocol.ExpectedReturnType}', got '{actualReturnType}'. " +
                (protocol.SharpyCoreInterface != null
                    ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
                    : ""),
                funcDef.LineStart,
                funcDef.ColumnStart));
        }
    }

    /// <summary>Helper to get string name from TypeAnnotation.</summary>
    private static string GetTypeAnnotationName(TypeAnnotation? typeAnnotation)
    {
        if (typeAnnotation == null)
            return "void";
        // Handle generic types like list[int]
        if (typeAnnotation.TypeArguments.Count > 0)
        {
            var args = string.Join(", ", typeAnnotation.TypeArguments.Select(GetTypeAnnotationName));
            return $"{typeAnnotation.Name}[{args}]";
        }
        return typeAnnotation.Name;
    }

    private static void ValidateSelfParameter(
        FunctionDef funcDef,
        ProtocolInfo protocol,
        TypeSymbol owningType,
        List<SemanticError> errors)
    {
        // All protocol dunders must have 'self' as first parameter (except static, but protocols aren't static)
        if (funcDef.Parameters.Count == 0)
        {
            // Don't add an error here if parameter count was already validated - 
            // this would be a duplicate error. The parameter count error is more specific.
            // Only add error if expected count is -1 (variable) but we have 0 params
            if (protocol.ExpectedParamCount == -1)
            {
                errors.Add(new SemanticError(
                    $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have 'self' as first parameter",
                    funcDef.LineStart,
                    funcDef.ColumnStart));
            }
            return;
        }

        if (funcDef.Parameters[0].Name != "self")
        {
            errors.Add(new SemanticError(
                $"First parameter of protocol method '{protocol.DunderName}' on '{owningType.Name}' must be " +
                $"'self', got '{funcDef.Parameters[0].Name}'",
                funcDef.LineStart,
                funcDef.ColumnStart));
        }
    }
}
