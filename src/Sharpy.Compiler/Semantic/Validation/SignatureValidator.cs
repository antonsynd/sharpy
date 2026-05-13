using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

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
            case InterfaceDef interfaceDef:
                ValidateInterfaceDunders(interfaceDef);
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

    private void ValidateInterfaceDunders(InterfaceDef interfaceDef)
    {
        foreach (var member in interfaceDef.Body)
        {
            if (member is FunctionDef funcDef && DunderDetector.IsDunderMethod(funcDef.Name))
            {
                AddError(_context,
                    $"Dunder method '{funcDef.Name}' cannot be declared in a user-defined interface. " +
                    "Only standard library interfaces may declare dunder methods.",
                    funcDef.LineStart, funcDef.ColumnStart,
                    code: DiagnosticCodes.Validation.DunderInUserInterface,
                    span: funcDef.Span);
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
        // Reject unknown dunders
        else if (DunderDetector.IsDunderMethod(methodName))
        {
            AddError(_context,
                $"Unknown dunder method '{methodName}' on '{owningType.Name}'. " +
                "Only recognized operator and protocol dunder methods are supported.",
                funcDef.LineStart, funcDef.ColumnStart,
                code: DiagnosticCodes.Validation.UnknownDunderMethod,
                span: funcDef.Span);
        }
    }

    #region Operator Signature Validation

    private void ValidateOperatorSignature(FunctionDef funcDef, TypeSymbol owningType)
    {
        var methodName = funcDef.Name;

        // Conversion operators are validated by ConversionOperatorValidator
        if (OperatorRegistry.IsConversionOperator(methodName))
            return;

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
        ValidateExitParameterTypes(funcDef, protocol, owningType);
    }

    /// <summary>
    /// Validates parameter types for the 4-arg form of __exit__/__aexit__.
    /// Expected: (self, exc_type: type?, exc_val: Exception?, exc_tb: object?)
    /// The exc_type parameter accepts 'type?' or 'object?' (since the .NET equivalent is Type?).
    /// Only validates when annotations are present — untyped parameters are allowed.
    /// </summary>
    private void ValidateExitParameterTypes(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
    {
        // Only apply to __exit__ and __aexit__ in their 4-arg form
        if (protocol.DunderName != DunderNames.Exit && protocol.DunderName != DunderNames.Aexit)
            return;
        if (funcDef.Parameters.Length != 4)
            return;

        // Parameter 1 is 'self' — validated elsewhere
        var excTypeParam = funcDef.Parameters[1];
        var excValParam = funcDef.Parameters[2];
        var excTbParam = funcDef.Parameters[3];

        // exc_type: type? or object?
        if (excTypeParam.Type != null && !IsValidExcTypeAnnotation(excTypeParam.Type))
        {
            AddError(_context,
                $"Parameter '{excTypeParam.Name}' of '{protocol.DunderName}' on '{owningType.Name}' must be 'type?' or 'object?', got '{TypeAnnotationHelper.GetName(excTypeParam.Type)}'.",
                excTypeParam.LineStart, excTypeParam.ColumnStart,
                code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: excTypeParam.Span ?? funcDef.Span);
        }

        // exc_val: Exception?
        if (excValParam.Type != null && !IsValidExcValAnnotation(excValParam.Type))
        {
            AddError(_context,
                $"Parameter '{excValParam.Name}' of '{protocol.DunderName}' on '{owningType.Name}' must be 'Exception?', got '{TypeAnnotationHelper.GetName(excValParam.Type)}'.",
                excValParam.LineStart, excValParam.ColumnStart,
                code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: excValParam.Span ?? funcDef.Span);
        }

        // exc_tb: object?
        if (excTbParam.Type != null && !IsValidExcTbAnnotation(excTbParam.Type))
        {
            AddError(_context,
                $"Parameter '{excTbParam.Name}' of '{protocol.DunderName}' on '{owningType.Name}' must be 'object?', got '{TypeAnnotationHelper.GetName(excTbParam.Type)}'.",
                excTbParam.LineStart, excTbParam.ColumnStart,
                code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
                span: excTbParam.Span ?? funcDef.Span);
        }
    }

    private static bool IsValidExcTypeAnnotation(TypeAnnotation annotation)
    {
        // Accept 'type?' or 'object?' (both must be nullable)
        if (!annotation.IsOptional)
            return false;
        if (annotation.TypeArguments.Length != 0)
            return false;
        return annotation.Name == BuiltinNames.Type || annotation.Name == BuiltinNames.Object;
    }

    private static bool IsValidExcValAnnotation(TypeAnnotation annotation)
    {
        // Accept 'Exception?'
        if (!annotation.IsOptional)
            return false;
        if (annotation.TypeArguments.Length != 0)
            return false;
        return annotation.Name == "Exception";
    }

    private static bool IsValidExcTbAnnotation(TypeAnnotation annotation)
    {
        // Accept 'object?'
        if (!annotation.IsOptional)
            return false;
        if (annotation.TypeArguments.Length != 0)
            return false;
        return annotation.Name == BuiltinNames.Object;
    }

    private void ValidateProtocolParameterCount(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
    {
        var actualCount = funcDef.Parameters.Length;
        var expectedCount = protocol.ExpectedParamCount;
        var alternateCount = protocol.AlternateParamCount;

        // -1 means any count (e.g., __init__ can have any number of params)
        if (expectedCount == -1)
            return;

        // Accept either the expected count or the alternate count (e.g., __exit__ accepts 1 or 4)
        if (actualCount == expectedCount || (alternateCount.HasValue && actualCount == alternateCount.Value))
            return;

        // Provide context-specific parameter descriptions
        var paramDescription = DescribeExpectedParameters(expectedCount, protocol.DunderName);
        var alternateDescription = alternateCount.HasValue
            ? $" or {alternateCount.Value} parameter{(alternateCount.Value == 1 ? "" : "s")} {DescribeExpectedParameters(alternateCount.Value, protocol.DunderName)}"
            : "";

        var interfaceHint = protocol.SharpyCoreInterface != null
            ? $" See interface '{protocol.SharpyCoreInterface}' for expected signature."
            : "";

        AddError(_context,
            $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have exactly " +
            $"{expectedCount} parameter{(expectedCount == 1 ? "" : "s")} {paramDescription}{alternateDescription}, got {actualCount}.{interfaceHint}",
            funcDef.LineStart, funcDef.ColumnStart, code: DiagnosticCodes.Semantic.ProtocolMissingMethod,
            span: funcDef.Span);
    }

    private static string DescribeExpectedParameters(int count, string dunderName)
    {
        return (count, dunderName) switch
        {
            (1, _) => "(self)",
            (2, DunderNames.Contains) => "(self, item)",
            (2, DunderNames.GetItem) => "(self, index)",
            (2, _) => "(self, other)",
            (3, DunderNames.SetItem) => "(self, index, value)",
            (3, _) => "(self, key, value)",
            (4, DunderNames.Exit) or (4, DunderNames.Aexit) => "(self, exc_type, exc_val, exc_tb)",
            _ => $"({count} parameters)"
        };
    }

    private void ValidateProtocolReturnType(FunctionDef funcDef, ProtocolInfo protocol, TypeSymbol owningType)
    {
        // Skip if no return type expectation
        if (protocol.ExpectedReturnType == null)
            return;

        // Skip if no return type annotation
        if (funcDef.ReturnType == null)
            return;

        // Select expected return type based on which param-count form was used.
        // For protocols with an alternate signature (e.g., __exit__), the 4-arg
        // form may have a different expected return type (e.g., bool to suppress).
        var actualParamCount = funcDef.Parameters.Length;
        var expectedReturnType = protocol.ExpectedReturnType;
        if (protocol.AlternateParamCount.HasValue
            && actualParamCount == protocol.AlternateParamCount.Value
            && protocol.AlternateReturnType != null)
        {
            expectedReturnType = protocol.AlternateReturnType;
        }

        var actualReturnType = TypeAnnotationHelper.GetName(funcDef.ReturnType);

        // Normalize: "None" and "void" are equivalent
        var expectedNormalized = expectedReturnType == BuiltinNames.None ? "void" : expectedReturnType;
        var actualNormalized = actualReturnType == BuiltinNames.None ? "void" : actualReturnType;

        if (!string.Equals(actualNormalized, expectedNormalized, StringComparison.OrdinalIgnoreCase))
        {
            var interfaceHint = protocol.SharpyCoreInterface != null
                ? $" See interface '{protocol.SharpyCoreInterface}' for expected signature."
                : "";

            AddError(_context,
                $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must return " +
                $"'{expectedReturnType}', got '{actualReturnType}'.{interfaceHint}",
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
        if (funcDef.Parameters[0].Name != PythonNames.Self)
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
        return typeAnnotation.Name == BuiltinNames.Bool && typeAnnotation.TypeArguments.Length == 0 && !typeAnnotation.IsOptional;
    }

    private static bool IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)
    {
        return typeAnnotation.Name == BuiltinNames.None && typeAnnotation.TypeArguments.Length == 0;
    }

    #endregion
}
