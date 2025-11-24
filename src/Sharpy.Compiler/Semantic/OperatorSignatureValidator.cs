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
    /// Returns a list of semantic errors if the signature is invalid.
    /// </summary>
    public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var errors = new List<SemanticError>();
        var methodName = funcDef.Name;

        if (!IsOperatorDunder(methodName))
        {
            // Not an operator dunder, no validation needed
            return errors;
        }

        var paramCount = funcDef.Parameters.Count;

        // Determine expected parameter count based on operator type
        if (UnaryOps.Contains(methodName))
        {
            // Unary operators: just 'self' (1 parameter)
            if (paramCount != 1)
            {
                errors.Add(new SemanticError(
                    $"Unary operator method '{methodName}' on '{owningType.Name}' must have exactly 1 parameter (self), got {paramCount}",
                    funcDef.LineStart,
                    funcDef.ColumnStart));
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
                errors.Add(new SemanticError(
                    $"Binary operator method '{methodName}' on '{owningType.Name}' must have exactly 2 parameters (self, other), got {paramCount}",
                    funcDef.LineStart,
                    funcDef.ColumnStart));
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
    private static void ValidateReturnType(FunctionDef funcDef, string methodName, string owningTypeName, List<SemanticError> errors)
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
                errors.Add(new SemanticError(
                    $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{GetTypeAnnotationName(returnType)}'",
                    funcDef.LineStart,
                    funcDef.ColumnStart));
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
                errors.Add(new SemanticError(
                    $"Operator method '{methodName}' on '{owningTypeName}' must return a non-void type",
                    funcDef.LineStart,
                    funcDef.ColumnStart));
            }
        }
    }

    /// <summary>
    /// Helper to check if a type annotation represents 'bool'
    /// </summary>
    private static bool IsTypeAnnotationBool(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == "bool" && typeAnnotation.TypeArguments.Count == 0 && !typeAnnotation.IsNullable;
    }

    /// <summary>
    /// Helper to check if a type annotation represents 'None' (void)
    /// </summary>
    private static bool IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == "None" && typeAnnotation.TypeArguments.Count == 0;
    }

    /// <summary>
    /// Helper to get a readable name from a type annotation
    /// </summary>
    private static string GetTypeAnnotationName(TypeAnnotation typeAnnotation)
    {
        if (typeAnnotation.TypeArguments.Count == 0)
        {
            return typeAnnotation.IsNullable ? $"{typeAnnotation.Name}?" : typeAnnotation.Name;
        }
        
        var typeArgs = string.Join(", ", typeAnnotation.TypeArguments.Select(GetTypeAnnotationName));
        var baseName = $"{typeAnnotation.Name}[{typeArgs}]";
        return typeAnnotation.IsNullable ? $"{baseName}?" : baseName;
    }
}
