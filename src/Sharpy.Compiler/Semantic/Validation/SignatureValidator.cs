using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates method signatures for dunder methods (operators and protocols).
/// Uses OperatorRegistry and ProtocolRegistry for dunder classification.
///
/// This validator runs early in the pipeline (Order 150) to catch signature
/// errors before type checking attempts to use the methods.
/// </summary>
internal class SignatureValidator : SemanticValidatorBase
{
    public override string Name => "SignatureValidator";
    public override int Order => 150; // Before type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting signature validation");

        foreach (var stmt in module.Body)
        {
            ValidateTopLevelStatement(stmt);
        }
    }

    private void ValidateTopLevelStatement(Statement stmt)
    {
        switch (stmt)
        {
            case ClassDef classDef:
                ValidateClassSignatures(classDef);
                break;
            case StructDef structDef:
                ValidateStructSignatures(structDef);
                break;
        }
    }

    private void ValidateClassSignatures(ClassDef classDef)
    {
        var typeSymbol = _context.SymbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol == null)
        {
            _logger.LogDebug($"Type symbol not found for class: {classDef.Name}");
            return;
        }

        foreach (var member in classDef.Body)
        {
            if (member is FunctionDef funcDef)
            {
                ValidateMethodSignature(funcDef, typeSymbol);
            }
        }
    }

    private void ValidateStructSignatures(StructDef structDef)
    {
        var typeSymbol = _context.SymbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol == null)
        {
            _logger.LogDebug($"Type symbol not found for struct: {structDef.Name}");
            return;
        }

        foreach (var member in structDef.Body)
        {
            if (member is FunctionDef funcDef)
            {
                ValidateMethodSignature(funcDef, typeSymbol);
            }
        }
    }

    private void ValidateMethodSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var methodName = funcDef.Name;

        // Check operator dunders
        if (OperatorRegistry.IsOperatorDunder(methodName))
        {
            ValidateOperatorSignature(funcDef, owningType);
        }
        // Check protocol dunders
        else if (ProtocolRegistry.IsProtocolDunder(methodName))
        {
            ValidateProtocolSignature(funcDef, owningType);
        }
    }

    #region Operator Signature Validation

    private void ValidateOperatorSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var methodName = funcDef.Name;
        var paramCount = funcDef.Parameters.Length;
        var expectedParamCount = OperatorRegistry.GetExpectedParamCount(methodName);

        if (expectedParamCount.HasValue && paramCount != expectedParamCount.Value)
        {
            var label = OperatorRegistry.IsUnaryOperator(methodName) ? "Unary" : "Binary";
            var paramDesc = expectedParamCount.Value == 1 ? "(self)" : "(self, other)";
            AddError(_context,
                $"{label} operator method '{methodName}' on '{owningType.Name}' must have exactly {expectedParamCount.Value} parameter{(expectedParamCount.Value == 1 ? "" : "s")} {paramDesc}, got {paramCount}",
                funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidOperatorSignature,
                span: funcDef.Span);
        }

        // Validate return type if we have the annotation
        if (funcDef.ReturnType != null)
        {
            ValidateOperatorReturnType(funcDef, methodName, owningType.Name);
        }
    }

    private void ValidateOperatorReturnType(FunctionDef funcDef, string methodName, string owningTypeName)
    {
        var returnType = funcDef.ReturnType;
        if (returnType == null)
            return;

        // For comparison operators, return type must be bool
        if (OperatorRegistry.IsComparisonOperator(methodName))
        {
            if (!IsTypeAnnotationBool(returnType))
            {
                AddError(_context,
                    $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{TypeAnnotationHelper.GetName(returnType)}'",
                    funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidOperatorSignature,
                    span: funcDef.Span);
            }
        }
        // For other operators, return type must be non-void
        else if (IsTypeAnnotationVoid(returnType))
        {
            AddError(_context,
                $"Operator method '{methodName}' on '{owningTypeName}' must return a non-void type",
                funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.InvalidOperatorSignature,
                span: funcDef.Span);
        }
    }

    #endregion

    #region Protocol Signature Validation

    private void ValidateProtocolSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var methodName = funcDef.Name;
        var protocol = ProtocolRegistry.GetProtocol(methodName);

        if (protocol == null)
            return;

        ValidateProtocolParameterCount(funcDef, protocol, owningType);
        ValidateProtocolReturnType(funcDef, protocol, owningType);
        ValidateProtocolSelfParameter(funcDef, protocol, owningType);
    }

    private void ValidateProtocolParameterCount(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
    {
        var actualCount = funcDef.Parameters.Length;
        var expectedCount = protocol.ExpectedParamCount;

        // -1 means any count (e.g., __init__ can have any number of params)
        if (expectedCount == -1)
            return;

        if (actualCount != expectedCount)
        {
            // Provide context-specific parameter descriptions
            var paramDescription = (expectedCount, protocol.DunderName) switch
            {
                (1, _) => "(self)",
                (2, DunderNames.Contains) => "(self, item)",
                (2, DunderNames.GetItem or DunderNames.DelItem) => "(self, index)",
                (2, _) => "(self, other)",
                (3, DunderNames.SetItem) => "(self, index, value)",
                (3, _) => "(self, key, value)",
                _ => $"({expectedCount} parameters)"
            };

            var interfaceHint = protocol.SharpyCoreInterface != null
                ? $" See interface '{protocol.SharpyCoreInterface}' for expected signature."
                : "";

            AddError(_context,
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have exactly " +
                $"{expectedCount} parameter{(expectedCount == 1 ? "" : "s")} {paramDescription}, got {actualCount}.{interfaceHint}",
                funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: funcDef.Span);
        }
    }

    private void ValidateProtocolReturnType(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
    {
        // Skip if no return type expectation
        if (protocol.ExpectedReturnType == null)
            return;

        // Skip if no return type annotation
        if (funcDef.ReturnType == null)
            return;

        var actualReturnType = TypeAnnotationHelper.GetName(funcDef.ReturnType);

        // Normalize: "None" and "void" are equivalent
        var expectedNormalized = protocol.ExpectedReturnType == "None" ? "void" : protocol.ExpectedReturnType;
        var actualNormalized = actualReturnType == "None" ? "void" : actualReturnType;

        if (!string.Equals(actualNormalized, expectedNormalized, StringComparison.OrdinalIgnoreCase))
        {
            var interfaceHint = protocol.SharpyCoreInterface != null
                ? $" See interface '{protocol.SharpyCoreInterface}' for expected signature."
                : "";

            AddError(_context,
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must return " +
                $"'{protocol.ExpectedReturnType}', got '{actualReturnType}'.{interfaceHint}",
                funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: funcDef.Span);
        }
    }

    private void ValidateProtocolSelfParameter(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
    {
        // Check if there are any parameters
        if (funcDef.Parameters.Length == 0)
        {
            // For variable-param protocols (like __init__) with 0 params, error about 'self'
            if (protocol.ExpectedParamCount == -1)
            {
                AddError(_context,
                    $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have 'self' as first parameter",
                    funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                    span: funcDef.Span);
            }
            return;
        }

        // Validate that the first parameter is named 'self'
        if (funcDef.Parameters[0].Name != "self")
        {
            AddError(_context,
                $"First parameter of protocol method '{protocol.DunderName}' on '{owningType.Name}' must be " +
                $"'self', got '{funcDef.Parameters[0].Name}'",
                funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: funcDef.Span);
        }
    }

    #endregion

    #region Helper Methods

    private static bool IsTypeAnnotationBool(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == "bool" && typeAnnotation.TypeArguments.Length == 0 && !typeAnnotation.IsOptional;
    }

    private static bool IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == "None" && typeAnnotation.TypeArguments.Length == 0;
    }

    #endregion
}
