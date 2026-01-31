using Sharpy.Compiler.Diagnostics;
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
    /// Returns a list of diagnostics if the signature is invalid.
    /// </summary>
    public static List<CompilerDiagnostic> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var errors = new List<CompilerDiagnostic>();
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
        List<CompilerDiagnostic> errors)
    {
        var actualCount = funcDef.Parameters.Length;
        var expectedCount = protocol.ExpectedParamCount;

        // -1 means any count (e.g., __init__ can have any number of params)
        if (expectedCount == -1)
            return;

        if (actualCount != expectedCount)
        {
            // Provide context-specific parameter descriptions based on the protocol
            var paramDescription = (expectedCount, protocol.DunderName) switch
            {
                (1, _) => "(self)",
                (2, "__contains__") => "(self, item)",
                (2, "__getitem__" or "__delitem__") => "(self, index)",
                (2, _) => "(self, other)",
                (3, "__setitem__") => "(self, index, value)",
                (3, _) => "(self, key, value)",
                _ => $"({expectedCount} parameters)"
            };

            errors.Add(new CompilerDiagnostic(
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have exactly " +
                $"{expectedCount} parameter{(expectedCount == 1 ? "" : "s")} {paramDescription}, got {actualCount}. " +
                (protocol.SharpyCoreInterface != null
                    ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
                    : ""),
                CompilerDiagnosticSeverity.Error,
                funcDef.LineStart,
                funcDef.ColumnStart,
                Phase: CompilerPhase.Validation));
        }
    }

    private static void ValidateReturnType(
        FunctionDef funcDef,
        ProtocolInfo protocol,
        TypeSymbol owningType,
        List<CompilerDiagnostic> errors)
    {
        // Skip if no return type expectation (null means any type is valid)
        if (protocol.ExpectedReturnType == null)
            return;

        // Skip if no return type annotation (will be inferred or default to void)
        if (funcDef.ReturnType == null)
            return;

        var actualReturnType = TypeAnnotationHelper.GetName(funcDef.ReturnType);

        // Normalize: "None" and "void" are equivalent
        var expectedNormalized = protocol.ExpectedReturnType == "None" ? "void" : protocol.ExpectedReturnType;
        var actualNormalized = actualReturnType == "None" ? "void" : actualReturnType;

        if (!string.Equals(actualNormalized, expectedNormalized, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CompilerDiagnostic(
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must return " +
                $"'{protocol.ExpectedReturnType}', got '{actualReturnType}'. " +
                (protocol.SharpyCoreInterface != null
                    ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
                    : ""),
                CompilerDiagnosticSeverity.Error,
                funcDef.LineStart,
                funcDef.ColumnStart,
                Phase: CompilerPhase.Validation));
        }
    }

    private static void ValidateSelfParameter(
        FunctionDef funcDef,
        ProtocolInfo protocol,
        TypeSymbol owningType,
        List<CompilerDiagnostic> errors)
    {
        // Check if there are any parameters to validate
        if (funcDef.Parameters.Length == 0)
        {
            // For protocols with fixed param count, the parameter count error is already raised.
            // Only add a 'self' error for variable-param protocols (like __init__) with 0 params.
            if (protocol.ExpectedParamCount == -1)
            {
                errors.Add(new CompilerDiagnostic(
                    $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have 'self' as first parameter",
                    CompilerDiagnosticSeverity.Error,
                    funcDef.LineStart,
                    funcDef.ColumnStart,
                    Phase: CompilerPhase.Validation));
            }
            return;
        }

        // Validate that the first parameter is named 'self'
        if (funcDef.Parameters[0].Name != "self")
        {
            errors.Add(new CompilerDiagnostic(
                $"First parameter of protocol method '{protocol.DunderName}' on '{owningType.Name}' must be " +
                $"'self', got '{funcDef.Parameters[0].Name}'",
                CompilerDiagnosticSeverity.Error,
                funcDef.LineStart,
                funcDef.ColumnStart,
                Phase: CompilerPhase.Validation));
        }
    }
}
