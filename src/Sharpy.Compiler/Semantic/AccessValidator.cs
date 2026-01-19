using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates access level rules in Sharpy code:
/// - Private members (__name) only accessible within the same class
/// - Protected members (_name) only accessible within class hierarchy
/// - Public members accessible everywhere
/// </summary>
/// <remarks>
/// MIGRATION NOTE: This validator should be migrated to the new validation pipeline
/// by implementing ISemanticValidator (see ControlFlowValidatorV2 as reference).
/// New code should use ValidationPipelineFactory.CreateDefault() instead of
/// instantiating this class directly.
/// </remarks>
public class AccessValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    private TypeSymbol? _currentClass = null;

    public AccessValidator(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _logger = logger ?? NullLogger.Instance;
    }

    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Set the current class context for access validation
    /// </summary>
    public void EnterClass(TypeSymbol classSymbol)
    {
        _currentClass = classSymbol;
    }

    /// <summary>
    /// Clear the current class context
    /// </summary>
    public void ExitClass()
    {
        _currentClass = null;
    }

    /// <summary>
    /// Validates access to a member of a type
    /// </summary>
    public void ValidateMemberAccess(string memberName, TypeSymbol owningType, int? lineStart, int? columnStart)
    {
        var accessLevel = DetermineAccessLevel(memberName);

        switch (accessLevel)
        {
            case AccessLevel.Private:
                // Private members only accessible within the same class
                if (_currentClass != owningType)
                {
                    AddError($"Cannot access private member '{memberName}' of '{owningType.Name}' from outside the class",
                        lineStart, columnStart);
                }
                break;

            case AccessLevel.Protected:
                // Protected members accessible within the class hierarchy
                if (_currentClass == null || !IsInHierarchy(_currentClass, owningType))
                {
                    AddError($"Cannot access protected member '{memberName}' of '{owningType.Name}' from outside the class hierarchy",
                        lineStart, columnStart);
                }
                break;

            case AccessLevel.Public:
                // Public members accessible everywhere
                break;
        }
    }

    /// <summary>
    /// Validates access to a field
    /// </summary>
    public void ValidateFieldAccess(VariableSymbol field, TypeSymbol owningType, int? lineStart, int? columnStart)
    {
        ValidateMemberAccess(field.Name, owningType, lineStart, columnStart);
    }

    /// <summary>
    /// Validates access to a method
    /// </summary>
    public void ValidateMethodAccess(FunctionSymbol method, TypeSymbol owningType, int? lineStart, int? columnStart)
    {
        ValidateMemberAccess(method.Name, owningType, lineStart, columnStart);
    }

    /// <summary>
    /// Determine access level from member name
    /// </summary>
    private AccessLevel DetermineAccessLevel(string name)
    {
        if (name.StartsWith("__") && !name.EndsWith("__"))
            return AccessLevel.Private;

        if (name.StartsWith("_") && !name.StartsWith("__"))
            return AccessLevel.Protected;

        return AccessLevel.Public;
    }

    /// <summary>
    /// Check if currentClass is in the hierarchy of targetClass
    /// (either the same class, a subclass, or a superclass)
    /// </summary>
    private bool IsInHierarchy(TypeSymbol currentClass, TypeSymbol targetClass)
    {
        // Same class
        if (currentClass == targetClass)
            return true;

        // Check if currentClass is a subclass of targetClass
        var baseType = currentClass.BaseType;
        while (baseType != null)
        {
            if (baseType == targetClass)
                return true;
            baseType = baseType.BaseType;
        }

        // Check if currentClass is a superclass of targetClass
        baseType = targetClass.BaseType;
        while (baseType != null)
        {
            if (baseType == currentClass)
                return true;
            baseType = baseType.BaseType;
        }

        return false;
    }

    private void AddError(string message, int? line, int? column)
    {
        _errors.Add(new SemanticError(message, line, column));
    }
}
