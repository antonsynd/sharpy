using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates operator method (dunder) signatures for Sharpy types.
/// Ensures signatures match both Sharpy syntax and .NET operator semantics.
/// </summary>
public class OperatorSignatureValidator
{
    // Mapping of dunder method names to their expected characteristics
    private static readonly HashSet<string> BinaryArithmeticOps = new()
    {
        "__add__", "__sub__", "__mul__", "__truediv__", "__floordiv__", "__mod__", "__pow__"
    };

    private static readonly HashSet<string> BinaryBitwiseOps = new()
    {
        "__and__", "__or__", "__xor__", "__lshift__", "__rshift__"
    };

    private static readonly HashSet<string> InPlaceOps = new()
    {
        "__iadd__", "__isub__", "__imul__", "__itruediv__", "__ifloordiv__", "__imod__", "__ipow__",
        "__iand__", "__ior__", "__ixor__", "__ilshift__", "__irshift__"
    };

    private static readonly HashSet<string> ComparisonOps = new()
    {
        "__eq__", "__ne__", "__lt__", "__le__", "__gt__", "__ge__"
    };

    private static readonly HashSet<string> UnaryOps = new()
    {
        "__pos__", "__neg__", "__invert__"
    };

    private static readonly HashSet<string> AllOperatorDunders;

    static OperatorSignatureValidator()
    {
        AllOperatorDunders = new HashSet<string>();
        AllOperatorDunders.UnionWith(BinaryArithmeticOps);
        AllOperatorDunders.UnionWith(BinaryBitwiseOps);
        AllOperatorDunders.UnionWith(InPlaceOps);
        AllOperatorDunders.UnionWith(ComparisonOps);
        AllOperatorDunders.UnionWith(UnaryOps);
    }

    /// <summary>
    /// Checks if a method name is a recognized operator dunder method
    /// </summary>
    public static bool IsOperatorDunder(string methodName)
    {
        return AllOperatorDunders.Contains(methodName);
    }

    /// <summary>
    /// Validates the signature of a dunder operator method.
    /// Returns a list of diagnostics if the signature is invalid.
    /// </summary>
    public static List<CompilerDiagnostic> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var errors = new List<CompilerDiagnostic>();
        var methodName = funcDef.Name;

        if (!IsOperatorDunder(methodName))
        {
            // Not an operator dunder, no validation needed
            return errors;
        }

        var paramCount = funcDef.Parameters.Length;

        // Determine expected parameter count based on operator type
        if (UnaryOps.Contains(methodName))
        {
            // Unary operators: just 'self' (1 parameter)
            if (paramCount != 1)
            {
                errors.Add(new CompilerDiagnostic(
                    $"Unary operator method '{methodName}' on '{owningType.Name}' must have exactly 1 parameter (self), got {paramCount}",
                    CompilerDiagnosticSeverity.Error,
                    funcDef.LineStart,
                    funcDef.ColumnStart,
                    Phase: CompilerPhase.Validation));
            }
        }
        else if (BinaryArithmeticOps.Contains(methodName) ||
                 BinaryBitwiseOps.Contains(methodName) ||
                 InPlaceOps.Contains(methodName) ||
                 ComparisonOps.Contains(methodName))
        {
            // Binary operators: 'self' + one other parameter (2 parameters total)
            if (paramCount != 2)
            {
                errors.Add(new CompilerDiagnostic(
                    $"Binary operator method '{methodName}' on '{owningType.Name}' must have exactly 2 parameters (self, other), got {paramCount}",
                    CompilerDiagnosticSeverity.Error,
                    funcDef.LineStart,
                    funcDef.ColumnStart,
                    Phase: CompilerPhase.Validation));
            }
        }

        // Validate return type if we have the annotation
        if (funcDef.ReturnType != null)
        {
            ValidateReturnType(funcDef, methodName, owningType.Name, errors);
        }

        return errors;
    }

    /// <summary>
    /// Validates the return type of an operator method based on .NET semantics
    /// </summary>
    private static void ValidateReturnType(FunctionDef funcDef, string methodName, string owningTypeName, List<CompilerDiagnostic> errors)
    {
        var returnType = funcDef.ReturnType;
        if (returnType == null)
        {
            return;
        }

        // For comparison operators, return type must be bool
        if (ComparisonOps.Contains(methodName))
        {
            // Check if return type is 'bool'
            if (!IsTypeAnnotationBool(returnType))
            {
                errors.Add(new CompilerDiagnostic(
                    $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{TypeAnnotationHelper.GetName(returnType)}'",
                    CompilerDiagnosticSeverity.Error,
                    funcDef.LineStart,
                    funcDef.ColumnStart,
                    Phase: CompilerPhase.Validation));
            }
        }

        // For other arithmetic and bitwise operators, return type must be non-void
        else if (BinaryArithmeticOps.Contains(methodName) ||
                 BinaryBitwiseOps.Contains(methodName) ||
                 InPlaceOps.Contains(methodName) ||
                 UnaryOps.Contains(methodName))
        {
            if (IsTypeAnnotationVoid(returnType))
            {
                errors.Add(new CompilerDiagnostic(
                    $"Operator method '{methodName}' on '{owningTypeName}' must return a non-void type",
                    CompilerDiagnosticSeverity.Error,
                    funcDef.LineStart,
                    funcDef.ColumnStart,
                    Phase: CompilerPhase.Validation));
            }
        }
    }

    /// <summary>
    /// Helper to check if a type annotation represents 'bool'
    /// </summary>
    private static bool IsTypeAnnotationBool(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == "bool" && typeAnnotation.TypeArguments.Length == 0 && !typeAnnotation.IsOptional;
    }

    /// <summary>
    /// Helper to check if a type annotation represents 'None' (void)
    /// </summary>
    private static bool IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == "None" && typeAnnotation.TypeArguments.Length == 0;
    }
}
