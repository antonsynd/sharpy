using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Type checks expressions and statements
/// </summary>
public class TypeChecker
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;
    private readonly ControlFlowValidator _controlFlowValidator;
    private readonly AccessValidator _accessValidator;
    private readonly OperatorValidator _operatorValidator;
    // Used for protocol validation (iterability, membership, indexing, len)
    private readonly ProtocolValidator _protocolValidator;
    // Used for validating default parameter values
    private readonly DefaultParameterValidator _defaultParameterValidator;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    // Track current function return type for return statement checking
    private SemanticType? _currentFunctionReturnType = null;

    // Track current class being checked (for self parameter typing)
    private TypeSymbol? _currentClass = null;

    // Track type narrowing in conditional contexts
    private Dictionary<string, SemanticType> _narrowedTypes = new();

    // Track whether we're inside an except block (for bare raise validation)
    private bool _inExceptBlock = false;

    // Track current method context for super() validation
    private string? _currentMethodName = null;
    private bool _currentMethodIsOverride = false;
    private bool _currentMethodIsDunder = false;
    private int _controlFlowDepth = 0;
    private bool _superInitCalled = false;  // Track if super().__init__() was called

    // Configuration
    public bool ContinueAfterError { get; set; } = true;
    public int MaxErrors { get; set; } = 100;

    public TypeChecker(SymbolTable symbolTable, SemanticInfo semanticInfo, TypeResolver typeResolver, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _typeResolver = typeResolver;
        _logger = logger ?? NullLogger.Instance;
        _controlFlowValidator = new ControlFlowValidator(_logger);
        _accessValidator = new AccessValidator(_symbolTable, _semanticInfo, _logger);

        // Create shared CLR member cache for efficient reflection caching across validators
        var sharedClrCache = new ClrMemberCache();

        _protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
        // Pass ProtocolValidator to OperatorValidator for 'in' operator membership checking
        _operatorValidator = new OperatorValidator(_symbolTable, _logger, _protocolValidator, sharedClrCache);
        _defaultParameterValidator = new DefaultParameterValidator(_symbolTable, _typeResolver, _logger);
    }

    public IReadOnlyList<SemanticError> Errors
    {
        get
        {
            // Combine errors from type checker, control flow validator, access validator, operator validator, protocol validator, and default parameter validator.
            var allErrors = new List<SemanticError>(_errors);
            allErrors.AddRange(_controlFlowValidator.Errors);
            allErrors.AddRange(_accessValidator.Errors);
            allErrors.AddRange(_operatorValidator.Errors);
            allErrors.AddRange(_protocolValidator.Errors);
            allErrors.AddRange(_defaultParameterValidator.Errors);
            return allErrors;
        }
    }

    /// <summary>
    /// Type check all statements in a module
    /// </summary>
    public void CheckModule(Module module)
    {
        _logger.LogInfo("Type checking module");

        foreach (var statement in module.Body)
        {
            CheckStatement(statement);
        }
    }

    private void CheckStatement(Statement statement)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                CheckFunction(functionDef);
                break;

            case ClassDef classDef:
                CheckClass(classDef);
                break;

            case StructDef structDef:
                CheckStruct(structDef);
                break;

            case InterfaceDef interfaceDef:
                // Interface methods don't have bodies to check
                break;

            case EnumDef enumDef:
                CheckEnum(enumDef);
                break;

            case Assignment assignment:
                CheckAssignment(assignment);
                break;

            case VariableDeclaration varDecl:
                CheckVariableDeclaration(varDecl);
                break;

            case ReturnStatement returnStmt:
                CheckReturn(returnStmt);
                break;

            case IfStatement ifStmt:
                CheckIf(ifStmt);
                break;

            case WhileStatement whileStmt:
                CheckWhile(whileStmt);
                break;

            case ForStatement forStmt:
                CheckFor(forStmt);
                break;

            case RaiseStatement raiseStmt:
                CheckRaise(raiseStmt);
                break;

            case TryStatement tryStmt:
                CheckTry(tryStmt);
                break;

            // TODO: When 'with' statement is implemented, ensure it creates its own scope
            // similar to try/except/finally blocks. The context manager's __enter__ and
            // __exit__ should be called, and the body should be in its own scope.

            case AssertStatement assertStmt:
                CheckAssert(assertStmt);
                break;

            case ExpressionStatement exprStmt:
                CheckExpression(exprStmt.Expression);
                break;

            case PassStatement:
            case BreakStatement:
            case ContinueStatement:
                // No type checking needed
                break;

            case ImportStatement:
            case FromImportStatement:
                // Import validation handled elsewhere
                break;

            default:
                _logger.LogWarning($"Unhandled statement type: {statement.GetType().Name}", 0, 0);
                break;
        }
    }

    private void CheckFunction(FunctionDef functionDef)
    {
        _logger.LogDebug($"Type checking function: {functionDef.Name}");

        // Look up the function symbol to update its types
        // For __init__ methods in classes, we need to look up from the Constructors list
        // since multiple overloads may exist with the same name
        FunctionSymbol? functionSymbol = null;
        if (functionDef.Name == "__init__" && _currentClass != null)
        {
            // Find the matching constructor by declaration line number
            // This uniquely identifies which overload we're checking
            functionSymbol = _currentClass.Constructors
                .FirstOrDefault(c => c.DeclarationLine == functionDef.LineStart);
        }
        else
        {
            functionSymbol = _symbolTable.LookupFunction(functionDef.Name);
        }

        // Resolve return type
        var returnType = _typeResolver.ResolveTypeAnnotation(functionDef.ReturnType);

        // Special case: __init__ always returns None/void
        // (signature validation is in ProtocolSignatureValidator)
        if (functionDef.Name == "__init__")
        {
            returnType = SemanticType.Void;
        }
        // Functions without explicit return type annotation default to void
        else if (returnType == SemanticType.Unknown && functionDef.ReturnType == null)
        {
            returnType = SemanticType.Void;
        }

        _currentFunctionReturnType = returnType;

        // Enter function scope
        _symbolTable.EnterScope($"function:{functionDef.Name}");

        // Save previous method context and set new context for super() validation
        var previousMethodName = _currentMethodName;
        var previousMethodIsOverride = _currentMethodIsOverride;
        var previousMethodIsDunder = _currentMethodIsDunder;
        var previousControlFlowDepth = _controlFlowDepth;
        var previousSuperInitCalled = _superInitCalled;

        _currentMethodName = functionDef.Name;
        _currentMethodIsOverride = functionDef.Decorators.Any(d => d.Name == "override");
        _currentMethodIsDunder = IsDunderMethod(functionDef.Name);
        _controlFlowDepth = 0;
        _superInitCalled = false;

        // Validate @override is required for dunders that override System.Object methods
        if (_currentClass != null && _currentMethodIsDunder)
        {
            bool requiresOverride = ProtocolRegistry.IsObjectOverrideDunder(functionDef.Name);

            if (requiresOverride && !_currentMethodIsOverride)
            {
                AddError(
                    $"Dunder method '{functionDef.Name}' overrides a System.Object method and requires the @override decorator",
                    functionDef.LineStart,
                    functionDef.ColumnStart);
            }
        }

        // Check for @abstract decorator
        bool isAbstract = functionDef.Decorators.Any(d => d.Name == "abstract" || d.Name == "abstractmethod");

        // Validate abstract methods
        if (isAbstract)
        {
            // Abstract methods must have ... body (Ellipsis expression)
            if (functionDef.Body.Count != 1 || functionDef.Body[0] is not ExpressionStatement exprStmt ||
                exprStmt.Expression is not EllipsisLiteral)
            {
                AddError($"Abstract method '{functionDef.Name}' must have '...' as its body",
                    functionDef.LineStart, functionDef.ColumnStart);
            }

            // Abstract methods must be in an abstract class
            if (_currentClass != null && !_currentClass.IsAbstract)
            {
                AddError($"Abstract method '{functionDef.Name}' can only be declared in an abstract class. Add @abstract decorator to class '{_currentClass.Name}'",
                    functionDef.LineStart, functionDef.ColumnStart);
            }
        }

        // Validate self parameter for instance methods
        // In Sharpy, methods without 'self' as the first parameter are treated as static methods
        // This is consistent with how the code generator handles them
        if (_currentClass != null)
        {
            // Check if this is a static method (explicitly decorated OR no self parameter)
            bool hasStaticDecorator = functionDef.Decorators.Any(d =>
                d.Name == "static" || d.Name == "staticmethod");

            bool hasSelfParameter = functionDef.Parameters.Count > 0 &&
                functionDef.Parameters[0].Name == "self";

            // Method is static if it has decorator OR doesn't have self parameter
            // No error needed - code generator will make it static
            if (!hasStaticDecorator && !hasSelfParameter)
            {
                // This is implicitly a static method - valid, no error
            }
            else if (hasSelfParameter && hasStaticDecorator)
            {
                // Warning: static decorator with self parameter - self will be ignored
                // This is allowed but could be confusing
            }
            // Instance methods with self are valid - no action needed
        }

        // Validate parameter ordering: non-default parameters cannot follow default parameters
        bool hasSeenDefault = false;
        for (int i = 0; i < functionDef.Parameters.Count; i++)
        {
            var param = functionDef.Parameters[i];

            if (param.DefaultValue != null)
            {
                hasSeenDefault = true;
            }
            else if (hasSeenDefault)
            {
                AddError($"Non-default parameter '{param.Name}' cannot follow default parameters",
                    param.LineStart, param.ColumnStart);
            }
        }

        // Validate default parameter values (compile-time constants, no mutable defaults, None for nullable types only)
        _defaultParameterValidator.ValidateFunctionDefaults(functionDef);

        // Register parameters in scope and update the function symbol's parameter types
        for (int i = 0; i < functionDef.Parameters.Count; i++)
        {
            var param = functionDef.Parameters[i];
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);

            // Special handling for 'self' parameter in methods
            if (i == 0 && param.Name == "self" && _currentClass != null)
            {
                paramType = new UserDefinedType { Symbol = _currentClass };
            }
            else if (param.Type == null)
            {
                // Require type annotations on all parameters except 'self'
                AddError($"Parameter '{param.Name}' requires a type annotation",
                    param.LineStart, param.ColumnStart);
            }

            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true,
                DeclarationLine = null,
                DeclarationColumn = null
            };
            _symbolTable.Define(paramSymbol);

            // Update the function symbol's parameter type
            if (functionSymbol != null && i < functionSymbol.Parameters.Count)
            {
                functionSymbol.Parameters[i] = functionSymbol.Parameters[i] with { Type = paramType };
            }

            // Type check default value if present
            if (param.DefaultValue != null)
            {
                var defaultType = CheckExpression(param.DefaultValue);
                if (!IsAssignable(defaultType, paramType))
                {
                    AddError($"Default value type '{defaultType.GetDisplayName()}' is not assignable to parameter type '{paramType.GetDisplayName()}'",
                        null, null);
                }
            }
        }

        // Update the function symbol's return type
        if (functionSymbol != null)
        {
            // Create a new FunctionSymbol with updated return type
            var updatedSymbol = functionSymbol with { ReturnType = returnType };
            // The symbol table stores symbols by reference, so we need to update it
            // Unfortunately, we can't directly update a record in the symbol table
            // This is a limitation of the current design
        }

        // Check function body
        foreach (var statement in functionDef.Body)
        {
            CheckStatement(statement);
        }

        // Validate control flow
        _controlFlowValidator.ValidateFunction(functionDef, returnType);

        // Restore previous method context
        _currentMethodName = previousMethodName;
        _currentMethodIsOverride = previousMethodIsOverride;
        _currentMethodIsDunder = previousMethodIsDunder;
        _controlFlowDepth = previousControlFlowDepth;
        _superInitCalled = previousSuperInitCalled;

        _symbolTable.ExitScope();
        _currentFunctionReturnType = null;
    }

    private void CheckClass(ClassDef classDef)
    {
        _logger.LogDebug($"Type checking class: {classDef.Name}");

        // Look up the class symbol
        var classSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (classSymbol == null)
        {
            AddError($"Class symbol for '{classDef.Name}' not found", classDef.LineStart, classDef.ColumnStart);
            return;
        }

        // Enter class scope
        _symbolTable.EnterScope($"class:{classDef.Name}");

        // Resolve field types first (before checking methods that might reference them)
        for (int i = 0; i < classSymbol.Fields.Count; i++)
        {
            var fieldSymbol = classSymbol.Fields[i];
            if (fieldSymbol.Type == SemanticType.Unknown)
            {
                // Find the corresponding VariableDeclaration in the AST
                var fieldDecl = classDef.Body
                    .OfType<VariableDeclaration>()
                    .FirstOrDefault(v => v.Name == fieldSymbol.Name);

                if (fieldDecl != null)
                {
                    var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
                    classSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
                }
            }
        }

        // Set current class for method type checking and access validation
        var previousClass = _currentClass;
        _currentClass = classSymbol;
        _accessValidator.EnterClass(classSymbol);

        // Check all members
        foreach (var statement in classDef.Body)
        {
            CheckStatement(statement);
        }

        // Validate constructor overloads after all members are checked
        ValidateConstructorOverloads(classSymbol);

        // Restore previous class
        _currentClass = previousClass;
        _accessValidator.ExitClass();

        _symbolTable.ExitScope();
    }

    private void CheckStruct(StructDef structDef)
    {
        _logger.LogDebug($"Type checking struct: {structDef.Name}");

        // Look up the struct symbol
        var structSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (structSymbol == null)
        {
            AddError($"Struct symbol for '{structDef.Name}' not found", structDef.LineStart, structDef.ColumnStart);
            return;
        }

        // Enter struct scope
        _symbolTable.EnterScope($"struct:{structDef.Name}");

        // Resolve field types first (before checking methods that might reference them)
        for (int i = 0; i < structSymbol.Fields.Count; i++)
        {
            var fieldSymbol = structSymbol.Fields[i];
            if (fieldSymbol.Type == SemanticType.Unknown)
            {
                // Find the corresponding VariableDeclaration in the AST
                var fieldDecl = structDef.Body
                    .OfType<VariableDeclaration>()
                    .FirstOrDefault(v => v.Name == fieldSymbol.Name);

                if (fieldDecl != null)
                {
                    var resolvedType = _typeResolver.ResolveTypeAnnotation(fieldDecl.Type);
                    structSymbol.Fields[i] = fieldSymbol with { Type = resolvedType };
                }
            }
        }

        // Set current class for method type checking (structs behave like classes)
        var previousClass = _currentClass;
        _currentClass = structSymbol;
        _accessValidator.EnterClass(structSymbol);

        // Check all members
        foreach (var statement in structDef.Body)
        {
            CheckStatement(statement);
        }

        // Validate struct-specific rules
        ValidateStructRules(structSymbol, structDef);

        // Restore previous class
        _currentClass = previousClass;
        _accessValidator.ExitClass();

        _symbolTable.ExitScope();
    }

    private void CheckEnum(EnumDef enumDef)
    {
        _logger.LogDebug($"Type checking enum: {enumDef.Name}");

        // Validate enum-specific rules
        ValidateEnumRules(enumDef);
    }

    private void CheckAssignment(Assignment assignment)
    {
        // Validate that 'self' cannot be reassigned
        if (assignment.Target is Identifier selfId && selfId.Name == "self")
        {
            AddError("Cannot reassign 'self'",
                assignment.LineStart, assignment.ColumnStart);
            return;
        }

        // Handle tuple unpacking: x, y = expr
        if (assignment.Operator == AssignmentOperator.Assign && assignment.Target is TupleLiteral targetTuple)
        {
            var tupleValueType = CheckExpression(assignment.Value);

            // Value must be a tuple type
            if (tupleValueType is not TupleType tupleType)
            {
                AddError($"Cannot unpack non-tuple type '{tupleValueType.GetDisplayName()}' into tuple",
                    assignment.LineStart, assignment.ColumnStart);
                return;
            }

            // Check element count matches
            if (targetTuple.Elements.Count != tupleType.ElementTypes.Count)
            {
                AddError($"Cannot unpack {tupleType.ElementTypes.Count} values into {targetTuple.Elements.Count} variables",
                    assignment.LineStart, assignment.ColumnStart);
                return;
            }

            // Type-check each unpacking element
            for (int i = 0; i < targetTuple.Elements.Count; i++)
            {
                var targetElem = targetTuple.Elements[i];
                var valueElemType = tupleType.ElementTypes[i];

                if (targetElem is Identifier tupleTargetId)
                {
                    var existingSymbol = _symbolTable.Lookup(tupleTargetId.Name, searchParents: false);

                    // Check if trying to reassign a constant
                    if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
                    {
                        AddError($"Cannot reassign constant variable '{tupleTargetId.Name}' in tuple unpacking",
                            tupleTargetId.LineStart, tupleTargetId.ColumnStart);
                        continue;
                    }

                    // In Sharpy, tuple unpacking creates new variable versions
                    // Create/redefine with inferred type from tuple element
                    var newSymbol = new VariableSymbol
                    {
                        Name = tupleTargetId.Name,
                        Kind = SymbolKind.Variable,
                        Type = valueElemType,
                        IsConstant = false,
                        DeclarationLine = tupleTargetId.LineStart,
                        DeclarationColumn = tupleTargetId.ColumnStart,
                        AccessLevel = AccessLevel.Public
                    };
                    _symbolTable.Define(newSymbol);
                    _semanticInfo.SetIdentifierSymbol(tupleTargetId, newSymbol);
                    _semanticInfo.SetExpressionType(tupleTargetId, valueElemType);
                }
                else
                {
                    // For more complex targets (like attributes), just check type compatibility
                    var targetElemType = CheckExpression(targetElem);
                    if (!IsAssignable(valueElemType, targetElemType))
                    {
                        AddError($"Cannot assign type '{valueElemType.GetDisplayName()}' to '{targetElemType.GetDisplayName()}' in tuple unpacking",
                            targetElem.LineStart, targetElem.ColumnStart);
                    }
                }
            }

            return;
        }

        // Check if this is a simple assignment to an identifier (type inference and redefinition case)
        if (assignment.Operator == AssignmentOperator.Assign && assignment.Target is Identifier targetId)
        {
            // Check current scope first
            var existingSymbol = _symbolTable.Lookup(targetId.Name, searchParents: false);

            // Check if trying to reassign a constant in current scope
            if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
            {
                AddError($"Cannot reassign constant variable '{targetId.Name}'",
                    assignment.LineStart, assignment.ColumnStart);
                return;
            }

            // Also check parent scopes for consts (can't reassign outer scope const)
            var parentSymbol = _symbolTable.Lookup(targetId.Name, searchParents: true);
            if (parentSymbol is VariableSymbol parentVar && parentVar.IsConstant)
            {
                AddError($"Cannot reassign constant variable '{targetId.Name}'",
                    assignment.LineStart, assignment.ColumnStart);
                return;
            }

            // In Sharpy, simple assignments (x = value) create new variable versions
            // This enables Python-like behavior where variables can be reassigned to different types
            var inferredType = CheckExpression(assignment.Value);

            // Create a new variable symbol with the inferred type (or redefine existing)
            var newSymbol = new VariableSymbol
            {
                Name = targetId.Name,
                Kind = SymbolKind.Variable,
                Type = inferredType,
                IsConstant = false,
                DeclarationLine = assignment.LineStart,
                DeclarationColumn = assignment.ColumnStart,
                AccessLevel = AccessLevel.Public
            };
            _symbolTable.Define(newSymbol);
            _semanticInfo.SetIdentifierSymbol(targetId, newSymbol);

            // Cache the expression type for the identifier
            _semanticInfo.SetExpressionType(targetId, inferredType);
            return;
        }

        // Check target and value types
        var targetType = CheckExpression(assignment.Target);
        var valueType = CheckExpression(assignment.Value);

        // Handle augmented assignment operators (+=, -=, *=, /=, //=, %=, **=, &=, |=, ^=, <<=, >>=)
        if (assignment.Operator != AssignmentOperator.Assign)
        {
            // Check if trying to use augmented assignment on a constant
            if (assignment.Target is Identifier augTargetId)
            {
                var symbol = _symbolTable.Lookup(augTargetId.Name, searchParents: true);
                if (symbol is VariableSymbol varSym && varSym.IsConstant)
                {
                    AddError($"Cannot use augmented assignment on constant variable '{augTargetId.Name}'",
                        assignment.LineStart, assignment.ColumnStart);
                    return;
                }
            }

            // For augmented assignments, delegate to OperatorValidator which handles:
            // - Preferring in-place dunder methods (e.g., __iadd__) when available
            // - Falling back to binary operators (e.g., __add__) otherwise
            // - Verifying result type is assignable to target type
            // - Logging appropriate errors when operators are not supported
            _operatorValidator.ValidateAugmentedAssignment(
                assignment.Operator,
                targetType,
                valueType,
                assignment.LineStart,
                assignment.ColumnStart);
            return;
        }

        // Otherwise, check as a regular simple assignment
        if (!IsAssignable(valueType, targetType))
        {
            // Special case: Allow None for nullable types but provide better error message
            if (valueType is VoidType && targetType is not NullableType)
            {
                AddError($"Cannot assign 'None' to non-nullable type '{targetType.GetDisplayName()}'",
                    assignment.LineStart, assignment.ColumnStart);
            }
            else
            {
                AddError($"Cannot assign type '{valueType.GetDisplayName()}' to '{targetType.GetDisplayName()}'",
                    assignment.LineStart, assignment.ColumnStart);
            }
        }
    }

    private void CheckVariableDeclaration(VariableDeclaration varDecl)
    {
        var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);

        if (varDecl.InitialValue != null)
        {
            var initType = CheckExpression(varDecl.InitialValue);

            // Handle type inference for 'auto'
            if (declaredType is UnknownType)
            {
                declaredType = initType;
                if (varDecl.Type != null)
                {
                    _semanticInfo.SetTypeAnnotation(varDecl.Type, initType);
                }
            }
            else if (!IsAssignable(initType, declaredType))
            {
                // Special case: Allow None for nullable types (VoidType.IsAssignableTo handles this)
                // but provide better error message for non-nullable types
                if (initType is VoidType && declaredType is not NullableType)
                {
                    AddError($"Cannot assign 'None' to non-nullable type '{declaredType.GetDisplayName()}'",
                        varDecl.LineStart, varDecl.ColumnStart);
                }
                else
                {
                    AddError($"Cannot assign type '{initType.GetDisplayName()}' to variable of type '{declaredType.GetDisplayName()}'",
                        varDecl.LineStart, varDecl.ColumnStart);
                }
            }
        }
        else if (declaredType is UnknownType)
        {
            AddError($"Variable '{varDecl.Name}' declared with 'auto' must have an initializer",
                varDecl.LineStart, varDecl.ColumnStart);
        }

        // Check if symbol already exists in current scope
        var existingSymbol = _symbolTable.Lookup(varDecl.Name, searchParents: false);

        // For constants:
        // - Module-level consts are already created by NameResolver, so we skip creation
        // - Function-level consts are NOT created by NameResolver, so we need to create them
        if (varDecl.IsConst)
        {
            if (existingSymbol != null)
            {
                // Module-level const was already created by NameResolver
                // Just do type checking and return
                return;
            }

            // Function-level const - we need to create it
            var constSymbol = new VariableSymbol
            {
                Name = varDecl.Name,
                Kind = SymbolKind.Variable,
                Type = declaredType,
                IsConstant = true,
                DeclarationLine = varDecl.LineStart,
                DeclarationColumn = varDecl.ColumnStart
            };
            _symbolTable.Define(constSymbol);
            return;
        }

        if (existingSymbol is VariableSymbol existingVar)
        {
            // In Sharpy, variables can be redefined in the same scope (Python-like behavior)
            // However, constants cannot be redefined
            if (existingVar.IsConstant)
            {
                AddError($"Cannot redefine constant variable '{varDecl.Name}'",
                    varDecl.LineStart, varDecl.ColumnStart);
                return;
            }

            // For non-const variables, allow redefinition with new type
            // This enables Python-like behavior where variables can be reassigned to different types
            // The Scope.Define will replace the existing symbol
        }

        // Create new variable symbol (or redefine existing non-const variable)
        var newSymbol = new VariableSymbol
        {
            Name = varDecl.Name,
            Kind = SymbolKind.Variable,
            Type = declaredType,
            IsConstant = false,  // Non-const variable
            DeclarationLine = varDecl.LineStart,
            DeclarationColumn = varDecl.ColumnStart
        };
        _symbolTable.Define(newSymbol);
    }

    private void CheckReturn(ReturnStatement returnStmt)
    {
        if (_currentFunctionReturnType == null)
        {
            AddError("Return statement outside of function",
                returnStmt.LineStart, returnStmt.ColumnStart);
            return;
        }

        if (returnStmt.Value != null)
        {
            var returnType = CheckExpression(returnStmt.Value);
            if (!IsAssignable(returnType, _currentFunctionReturnType))
            {
                AddError($"Cannot return type '{returnType.GetDisplayName()}' from function expecting '{_currentFunctionReturnType.GetDisplayName()}'",
                    returnStmt.LineStart, returnStmt.ColumnStart);
            }
        }
        else if (_currentFunctionReturnType != SemanticType.Void)
        {
            AddError($"Function expects return type '{_currentFunctionReturnType.GetDisplayName()}' but got no return value",
                returnStmt.LineStart, returnStmt.ColumnStart);
        }
    }

    private void CheckIf(IfStatement ifStmt)
    {
        var condType = CheckExpression(ifStmt.Test);
        if (condType != SemanticType.Bool && !(condType is UnknownType))
        {
            AddError($"If condition must be boolean, got '{condType.GetDisplayName()}'",
                ifStmt.LineStart, ifStmt.ColumnStart);
        }

        // Check for type narrowing patterns
        var narrowedTypesInThen = ExtractNarrowedTypes(ifStmt.Test, true);
        var narrowedTypesInElse = ExtractNarrowedTypes(ifStmt.Test, false);

        // Apply narrowed types in then branch
        var savedNarrowedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);
        foreach (var kvp in narrowedTypesInThen)
        {
            _narrowedTypes[kvp.Key] = kvp.Value;
        }

        // Enter scope for if-then block
        _symbolTable.EnterScope("if-then");
        _controlFlowDepth++;
        foreach (var stmt in ifStmt.ThenBody)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();

        // Check elif clauses
        foreach (var elif in ifStmt.ElifClauses)
        {
            var elifCondType = CheckExpression(elif.Test);
            if (elifCondType != SemanticType.Bool && !(elifCondType is UnknownType))
            {
                AddError($"Elif condition must be boolean, got '{elifCondType.GetDisplayName()}'",
                    elif.LineStart, elif.ColumnStart);
            }

            _narrowedTypes = new Dictionary<string, SemanticType>(savedNarrowedTypes);
            var narrowedTypesInElif = ExtractNarrowedTypes(elif.Test, true);
            foreach (var kvp in narrowedTypesInElif)
            {
                _narrowedTypes[kvp.Key] = kvp.Value;
            }

            _symbolTable.EnterScope("elif");
            _controlFlowDepth++;
            foreach (var stmt in elif.Body)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Apply narrowed types in else branch
        _narrowedTypes = new Dictionary<string, SemanticType>(savedNarrowedTypes);
        foreach (var kvp in narrowedTypesInElse)
        {
            _narrowedTypes[kvp.Key] = kvp.Value;
        }

        // Enter scope for if-else block only if there are statements
        if (ifStmt.ElseBody.Count > 0)
        {
            _symbolTable.EnterScope("if-else");
            _controlFlowDepth++;
            foreach (var stmt in ifStmt.ElseBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Restore original narrowed types
        _narrowedTypes = savedNarrowedTypes;
    }

    private void CheckWhile(WhileStatement whileStmt)
    {
        var condType = CheckExpression(whileStmt.Test);
        if (condType != SemanticType.Bool && !(condType is UnknownType))
        {
            AddError($"While condition must be boolean, got '{condType.GetDisplayName()}'",
                whileStmt.LineStart, whileStmt.ColumnStart);
        }

        // Check for type narrowing patterns (similar to if statement)
        var narrowedTypesInBody = ExtractNarrowedTypes(whileStmt.Test, true);

        // Apply narrowed types in the loop body
        var savedNarrowedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);
        foreach (var kvp in narrowedTypesInBody)
        {
            _narrowedTypes[kvp.Key] = kvp.Value;
        }

        // Enter scope for while-body block
        _symbolTable.EnterScope("while-body");
        _controlFlowDepth++;
        foreach (var stmt in whileStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();

        // Restore original narrowed types
        _narrowedTypes = savedNarrowedTypes;
    }

    private void CheckFor(ForStatement forStmt)
    {
        var iterType = CheckExpression(forStmt.Iterator);

        // Validate that the iterator type is iterable and extract element type
        // This uses ProtocolValidator which checks for __iter__ protocol support
        var elementType = _protocolValidator.ValidateIteration(
            iterType,
            forStmt.Iterator.LineStart,
            forStmt.Iterator.ColumnStart);

        // Enter scope for for-body block FIRST
        // This ensures loop variables are scoped to the loop
        _symbolTable.EnterScope("for-body");

        // Handle tuple unpacking: for x, y in items
        if (forStmt.Target is TupleLiteral targetTuple)
        {
            // Element type must be a tuple type
            if (elementType is not TupleType tupleType)
            {
                AddError($"Cannot unpack non-tuple type '{elementType.GetDisplayName()}' in for loop",
                    forStmt.LineStart, forStmt.ColumnStart);
            }
            else
            {
                // Check element count matches
                if (targetTuple.Elements.Count != tupleType.ElementTypes.Count)
                {
                    AddError($"Cannot unpack {tupleType.ElementTypes.Count} values into {targetTuple.Elements.Count} variables in for loop",
                        forStmt.LineStart, forStmt.ColumnStart);
                }
                else
                {
                    // Define loop variables with inferred types INSIDE the for-body scope
                    for (int i = 0; i < targetTuple.Elements.Count; i++)
                    {
                        var targetElem = targetTuple.Elements[i];
                        var elemType = tupleType.ElementTypes[i];

                        if (targetElem is Identifier id)
                        {
                            var loopVarSymbol = new VariableSymbol
                            {
                                Name = id.Name,
                                Kind = SymbolKind.Variable,
                                Type = elemType,
                                AccessLevel = AccessLevel.Public,
                                DeclarationLine = id.LineStart,
                                DeclarationColumn = id.ColumnStart
                            };

                            // Check if already defined in this scope
                            if (_symbolTable.Lookup(id.Name, searchParents: false) == null)
                            {
                                _symbolTable.Define(loopVarSymbol);
                                _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                            }

                            _semanticInfo.SetExpressionType(targetElem, elemType);
                        }
                        else
                        {
                            // For more complex targets, just check the expression
                            CheckExpression(targetElem);
                        }
                    }
                }
            }

            _semanticInfo.SetExpressionType(forStmt.Target, elementType);
        }
        // Add loop variable to scope
        // The target is typically an Identifier or TupleExpression
        else if (forStmt.Target is Identifier id)
        {
            // Infer the type of the loop variable from the iterator
            var loopVarSymbol = new VariableSymbol
            {
                Name = id.Name,
                Kind = SymbolKind.Variable,
                Type = elementType,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart
            };

            // Check if already defined in this scope
            if (_symbolTable.Lookup(id.Name, searchParents: false) == null)
            {
                _symbolTable.Define(loopVarSymbol);
                _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
            }

            _semanticInfo.SetExpressionType(forStmt.Target, elementType);
        }

        // Check loop body statements
        _controlFlowDepth++;
        foreach (var stmt in forStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;

        // Exit for-body scope
        _symbolTable.ExitScope();
    }

    private void CheckRaise(RaiseStatement raiseStmt)
    {
        // Bare raise (no exception) is only valid inside an except block
        if (raiseStmt.Exception == null && !_inExceptBlock)
        {
            AddError("Bare 'raise' statement can only be used inside an exception handler",
                raiseStmt.LineStart, raiseStmt.ColumnStart);
        }

        if (raiseStmt.Exception != null)
        {
            CheckExpression(raiseStmt.Exception);
        }
    }

    private void CheckTry(TryStatement tryStmt)
    {
        // Try block has its own scope
        _symbolTable.EnterScope("try");
        _controlFlowDepth++;
        foreach (var stmt in tryStmt.Body)
            CheckStatement(stmt);
        _controlFlowDepth--;
        _symbolTable.ExitScope();

        // Each exception handler has its own scope
        foreach (var handler in tryStmt.Handlers)
        {
            _symbolTable.EnterScope("except");
            _controlFlowDepth++;
            _inExceptBlock = true;
            foreach (var stmt in handler.Body)
                CheckStatement(stmt);
            _inExceptBlock = false;
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }

        // Finally block has its own scope
        if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
        {
            _symbolTable.EnterScope("finally");
            _controlFlowDepth++;
            foreach (var stmt in tryStmt.FinallyBody)
                CheckStatement(stmt);
            _controlFlowDepth--;
            _symbolTable.ExitScope();
        }
    }

    private void CheckAssert(AssertStatement assertStmt)
    {
        var testType = CheckExpression(assertStmt.Test);
        if (assertStmt.Message != null)
        {
            CheckExpression(assertStmt.Message);
        }
    }

    /// <summary>
    /// Type check an expression and return its type
    /// </summary>
    public SemanticType CheckExpression(Expression expr)
    {
        // Check cache
        var cached = _semanticInfo.GetExpressionType(expr);
        if (cached != null)
            return cached;

        SemanticType type = expr switch
        {
            IntegerLiteral => SemanticType.Int,
            FloatLiteral => SemanticType.Double,
            StringLiteral => SemanticType.Str,
            BooleanLiteral => SemanticType.Bool,
            NoneLiteral => SemanticType.Void,
            Identifier id => CheckIdentifier(id),
            BinaryOp binOp => CheckBinaryOp(binOp),
            UnaryOp unOp => CheckUnaryOp(unOp),
            ComparisonChain chain => CheckComparisonChain(chain),
            SuperExpression superExpr => CheckSuperExpression(superExpr),
            MemberAccess memberAccess => CheckMemberAccess(memberAccess),
            IndexAccess indexAccess => CheckIndexAccess(indexAccess),
            FunctionCall call => CheckFunctionCall(call),
            ListLiteral list => CheckListLiteral(list),
            DictLiteral dict => CheckDictLiteral(dict),
            SetLiteral set => CheckSetLiteral(set),
            TupleLiteral tuple => CheckTupleLiteral(tuple),
            ListComprehension listComp => CheckListComprehension(listComp),
            SetComprehension setComp => CheckSetComprehension(setComp),
            DictComprehension dictComp => CheckDictComprehension(dictComp),
            ConditionalExpression cond => CheckConditionalExpression(cond),
            LambdaExpression lambda => CheckLambda(lambda),
            TypeCast cast => CheckTypeCast(cast),
            TypeCheck typeCheck => CheckTypeCheck(typeCheck),
            Parenthesized paren => CheckExpression(paren.Expression),
            _ => SemanticType.Unknown
        };

        // Cache the result
        _semanticInfo.SetExpressionType(expr, type);
        return type;
    }

    private SemanticType CheckIdentifier(Identifier id)
    {
        // Special validation for 'self' - must be used inside an instance method
        if (id.Name == "self")
        {
            if (_currentClass == null)
            {
                AddError("'self' can only be used inside instance methods",
                    id.LineStart, id.ColumnStart);
                return SemanticType.Unknown;
            }
            // Normal identifier lookup will follow and find the self parameter
        }

        var symbol = _symbolTable.Lookup(id.Name);
        if (symbol == null)
        {
            AddError($"Undefined identifier '{id.Name}'",
                id.LineStart, id.ColumnStart);
            return SemanticType.Unknown;
        }

        _semanticInfo.SetIdentifierSymbol(id, symbol);

        // Check if this identifier has a narrowed type in the current context
        if (_narrowedTypes.TryGetValue(id.Name, out var narrowedType))
        {
            return narrowedType;
        }

        return symbol switch
        {
            VariableSymbol varSymbol => varSymbol.Type,
            FunctionSymbol funcSymbol => new FunctionType
            {
                ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                ReturnType = funcSymbol.ReturnType
            },
            ModuleSymbol moduleSymbol => new ModuleType { Symbol = moduleSymbol },
            TypeSymbol => SemanticType.Unknown, // Type names used as values need special handling
            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        // Handle pipe forward operator specially - it's a syntactic transformation, not a regular operator
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            return CheckPipeForward(binOp);
        }

        var leftType = CheckExpression(binOp.Left);
        var rightType = CheckExpression(binOp.Right);

        // If either operand is Unknown, return Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Delegate all operator validation to OperatorValidator
        return _operatorValidator.ValidateBinaryOp(
            binOp.Operator,
            leftType,
            rightType,
            binOp.LineStart,
            binOp.ColumnStart);
    }

    /// <summary>
    /// Type-checks the pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend x to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity)
    /// </summary>
    private SemanticType CheckPipeForward(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);

        if (leftType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Case 1: x |> f(y, z) - right side is already a function call
        // We need to re-validate with x prepended to arguments
        if (binOp.Right is FunctionCall funcCall)
        {
            return CheckPipeForwardWithFunctionCall(leftType, funcCall, binOp);
        }

        // Case 2: x |> f - right side is an identifier or expression that should be callable
        var rightType = CheckExpression(binOp.Right);

        if (rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Check if right side is a function type
        if (rightType is FunctionType ft)
        {
            // Validate that the function accepts leftType as first argument
            if (ft.ParameterTypes.Count < 1)
            {
                AddError($"Pipe target function takes no arguments, cannot pipe a value to it",
                    binOp.Right.LineStart, binOp.Right.ColumnStart);
                return SemanticType.Unknown;
            }

            if (!leftType.IsAssignableTo(ft.ParameterTypes[0]))
            {
                AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function expecting '{ft.ParameterTypes[0].GetDisplayName()}'",
                    binOp.LineStart, binOp.ColumnStart);
                return SemanticType.Unknown;
            }

            return ft.ReturnType;
        }

        // If right side is an identifier, look up the function symbol
        if (binOp.Right is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);

                if (requiredParamCount < 1)
                {
                    AddError($"Pipe target function '{id.Name}' takes no required arguments, cannot pipe a value to it",
                        binOp.Right.LineStart, binOp.Right.ColumnStart);
                    return SemanticType.Unknown;
                }

                // Validate the piped value type matches first parameter
                var firstParam = funcSymbol.Parameters[0];
                if (!leftType.IsAssignableTo(firstParam.Type))
                {
                    AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function '{id.Name}' expecting '{firstParam.Type.GetDisplayName()}'",
                        binOp.LineStart, binOp.ColumnStart);
                    return SemanticType.Unknown;
                }

                // Check if remaining required args are satisfied (they must all have defaults)
                if (requiredParamCount > 1)
                {
                    AddError($"Function '{id.Name}' requires {requiredParamCount} arguments but only 1 is provided via pipe",
                        binOp.Right.LineStart, binOp.Right.ColumnStart);
                    return SemanticType.Unknown;
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass → SomeClass(x)
                // This is allowed, handled similarly to function call
                AddError($"Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart);
                return SemanticType.Unknown;
            }

            AddError($"'{id.Name}' is not callable",
                binOp.Right.LineStart, binOp.Right.ColumnStart);
            return SemanticType.Unknown;
        }

        // Right side is some other expression that's not callable
        AddError($"Pipe target must be callable, got '{rightType.GetDisplayName()}'",
            binOp.Right.LineStart, binOp.Right.ColumnStart);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a pipe forward where the right side is a function call.
    /// x |> f(y, z) → f(x, y, z) - prepend piped value to argument list.
    /// </summary>
    private SemanticType CheckPipeForwardWithFunctionCall(SemanticType pipedType, FunctionCall call, BinaryOp binOp)
    {
        // Get the function being called
        var calleeType = CheckExpression(call.Function);

        // Collect existing argument types
        var existingArgTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            existingArgTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Build the full argument list: piped value + existing args
        var allArgTypes = new List<SemanticType> { pipedType };
        allArgTypes.AddRange(existingArgTypes);

        // Total argument count includes piped value, positional args, and keyword args
        var totalArgCount = allArgTypes.Count + kwargTypes.Count;

        // Try to resolve the function symbol for better validation
        if (call.Function is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count (include both positional and keyword args)
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
                var totalParamCount = funcSymbol.Parameters.Count;

                if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
                {
                    if (requiredParamCount == totalParamCount)
                    {
                        AddError($"Function '{id.Name}' expects {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart);
                    }
                    else
                    {
                        AddError($"Function '{id.Name}' expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart);
                    }
                    return SemanticType.Unknown;
                }

                // Validate positional argument types (piped value + call.Arguments)
                for (int i = 0; i < allArgTypes.Count; i++)
                {
                    var argType = allArgTypes[i];
                    var paramType = funcSymbol.Parameters[i].Type;

                    if (!argType.IsAssignableTo(paramType))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        AddError($"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{funcSymbol.Parameters[i].Name}' of type '{paramType.GetDisplayName()}'",
                            i == 0 ? binOp.Left.LineStart : call.Arguments[i - 1].LineStart,
                            i == 0 ? binOp.Left.ColumnStart : call.Arguments[i - 1].ColumnStart);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally (including piped value)
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < allArgTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                    }
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol typeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass(y) → SomeClass(x, y)
                AddError($"Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart);
                return SemanticType.Unknown;
            }

            if (symbol != null)
            {
                AddError($"'{id.Name}' is not callable",
                    call.LineStart, call.ColumnStart);
                return SemanticType.Unknown;
            }
        }

        // Fallback: check if callee is a FunctionType
        if (calleeType is FunctionType ft)
        {
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount} (including piped value)",
                    call.LineStart, call.ColumnStart);
                return SemanticType.Unknown;
            }

            // Validate positional argument types
            for (int i = 0; i < allArgTypes.Count; i++)
            {
                if (!allArgTypes[i].IsAssignableTo(ft.ParameterTypes[i]))
                {
                    var argDesc = i == 0 ? "piped value" : $"argument {i}";
                    AddError($"Cannot pass {argDesc} of type '{allArgTypes[i].GetDisplayName()}' where '{ft.ParameterTypes[i].GetDisplayName()}' is expected",
                        i == 0 ? binOp.Left.LineStart : call.Arguments[i - 1].LineStart,
                        i == 0 ? binOp.Left.ColumnStart : call.Arguments[i - 1].ColumnStart);
                }
            }

            return ft.ReturnType;
        }

        AddError($"Pipe target must be callable, got '{calleeType.GetDisplayName()}'",
            call.LineStart, call.ColumnStart);
        return SemanticType.Unknown;
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        var operandType = CheckExpression(unOp.Operand);

        // If operand is Unknown, return Unknown to avoid cascading errors
        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Delegate all unary operator validation to OperatorValidator
        return _operatorValidator.ValidateUnaryOp(
            unOp.Operator,
            operandType,
            unOp.LineStart,
            unOp.ColumnStart);
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // A comparison chain like "a < b < c" has:
        // - Operands: [a, b, c]
        // - Operators: [LessThan, LessThan]
        // We need to validate each adjacent pair: (a < b) and (b < c)

        // Validate chain structure: operators count should equal operands count minus 1
        // (e.g., 3 operands need 2 operators: a < b < c)
        if (chain.Operands.Count < 2 || chain.Operators.Count != chain.Operands.Count - 1)
        {
            // Malformed chain, just return bool and let parser handle errors
            return SemanticType.Bool;
        }

        // Check all operands and build their types
        var operandTypes = new List<SemanticType>();
        for (int i = 0; i < chain.Operands.Count; i++)
        {
            operandTypes.Add(CheckExpression(chain.Operands[i]));
        }

        // Validate each comparison pair using OperatorValidator
        for (int i = 0; i < chain.Operators.Count; i++)
        {
            var leftType = operandTypes[i];
            var rightType = operandTypes[i + 1];

            // Skip validation if either operand is Unknown to avoid cascading errors
            if (leftType is UnknownType || rightType is UnknownType)
            {
                continue;
            }

            // Map ComparisonOperator to BinaryOperator
            var binaryOp = OperatorValidator.ComparisonOperatorToBinaryOperator(chain.Operators[i]);

            // Validate the comparison using OperatorValidator
            // We discard the result type since comparison operators always return bool,
            // and ValidateBinaryOp already reports errors for invalid operations.
            _ = _operatorValidator.ValidateBinaryOp(
                binaryOp,
                leftType,
                rightType,
                chain.Operands[i].LineStart,
                chain.Operands[i].ColumnStart);
        }

        // All comparison chains return bool
        return SemanticType.Bool;
    }

    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        // Check for super() usage
        if (memberAccess.Object is SuperExpression superExpr)
        {
            return ValidateSuperMemberAccess(memberAccess, superExpr);
        }

        var objectType = CheckExpression(memberAccess.Object);

        // Handle null conditional access (?.)
        SemanticType memberLookupType = objectType;
        if (memberAccess.IsNullConditional)
        {
            // Null conditional can only be used on nullable types
            if (objectType is not NullableType nullableObjectType)
            {
                AddError(
                    $"Null conditional operator '?.' can only be used on nullable types, but got '{objectType.GetDisplayName()}'",
                    memberAccess.LineStart, memberAccess.ColumnStart);
                return SemanticType.Unknown;
            }
            // Use the underlying type for member lookup
            memberLookupType = nullableObjectType.UnderlyingType;
        }

        // Handle module member access (e.g., config.MAX_SIZE, utils.helper())
        if (memberLookupType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                return exportedSymbol switch
                {
                    VariableSymbol varSymbol => varSymbol.Type,
                    FunctionSymbol funcSymbol => new FunctionType
                    {
                        ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                        ReturnType = funcSymbol.ReturnType
                    },
                    TypeSymbol typeSymbol => new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol },
                    ModuleSymbol nestedModule => new ModuleType { Symbol = nestedModule },
                    _ => SemanticType.Unknown
                };
            }

            AddError($"Module '{moduleSymbol.Name}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
            return SemanticType.Unknown;
        }

        if (memberLookupType is UserDefinedType udt && udt.Symbol != null)
        {
            // Look for field or property (including inherited fields)
            var (field, fieldOwner) = FindFieldInHierarchy(udt.Symbol, memberAccess.Member);
            if (field != null && fieldOwner != null)
            {
                // Validate access level
                _accessValidator.ValidateFieldAccess(field, fieldOwner, memberAccess.LineStart, memberAccess.ColumnStart);

                var fieldType = field.Type;

                // Wrap result in nullable for null conditional access
                if (memberAccess.IsNullConditional && fieldType is not NullableType)
                {
                    return new NullableType { UnderlyingType = fieldType };
                }
                return fieldType;
            }

            // Look for method (including inherited methods)
            var (method, methodOwner) = FindMethodInHierarchy(udt.Symbol, memberAccess.Member);
            if (method != null && methodOwner != null)
            {
                // Validate access level
                _accessValidator.ValidateMethodAccess(method, methodOwner, memberAccess.LineStart, memberAccess.ColumnStart);

                // When accessing a method via member access (obj.method), the object is implicitly
                // bound as the first parameter (self), so we skip it when creating the FunctionType
                var paramTypes = method.Parameters.Skip(1).Select(p => p.Type).ToList();

                var methodFunctionType = new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = method.ReturnType
                };

                // For null conditional method access, we don't wrap the FunctionType itself,
                // but the eventual call result should be nullable (handled in CheckFunctionCall)
                return methodFunctionType;
            }

            AddError($"Type '{memberLookupType.GetDisplayName()}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Finds a field by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (VariableSymbol? Field, TypeSymbol? Owner) FindFieldInHierarchy(TypeSymbol type, string fieldName)
    {
        // First check the type itself
        var field = type.Fields.FirstOrDefault(f => f.Name == fieldName);
        if (field != null)
            return (field, type);

        // Check base class chain
        var current = type.BaseType;
        while (current != null)
        {
            field = current.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
                return (field, current);
            current = current.BaseType;
        }

        return (null, null);
    }

    /// <summary>
    /// Finds a method by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (FunctionSymbol? Method, TypeSymbol? Owner) FindMethodInHierarchy(TypeSymbol type, string methodName)
    {
        // First check the type itself
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method != null)
            return (method, type);

        // Check base class chain
        var current = type.BaseType;
        while (current != null)
        {
            method = current.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, current);
            current = current.BaseType;
        }

        // Check interfaces (for method contracts)
        foreach (var iface in type.Interfaces)
        {
            method = iface.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, iface);
        }

        return (null, null);
    }

    private SemanticType CheckIndexAccess(IndexAccess indexAccess)
    {
        // Check if this subscript expression has a narrowed type
        var narrowingKey = ExtractNarrowingKey(indexAccess);
        if (narrowingKey != null && _narrowedTypes.TryGetValue(narrowingKey, out var narrowedType))
        {
            return narrowedType;
        }

        var objectType = CheckExpression(indexAccess.Object);
        var indexType = CheckExpression(indexAccess.Index);

        // Validate that the object type supports indexing and get the element type
        // This uses ProtocolValidator which checks for __getitem__ protocol support
        return _protocolValidator.ValidateIndexAccess(
            objectType,
            indexType,
            indexAccess.LineStart,
            indexAccess.ColumnStart);
    }

    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Check if this is a null conditional method call (obj?.method())
        bool isNullConditionalCall = call.Function is MemberAccess { IsNullConditional: true };

        // Check the called expression type first
        var calleeType = CheckExpression(call.Function);

        // Track super().__init__() calls AFTER validation completes
        // (do this after CheckExpression so the validation doesn't see it as already called)
        if (call.Function is MemberAccess ma && ma.Object is SuperExpression && ma.Member == "__init__")
        {
            _superInitCalled = true;
        }

        // Check arguments and collect their types
        var argTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            argTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Total argument count includes both positional and keyword arguments
        var totalArgCount = argTypes.Count + kwargTypes.Count;

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;
        if (call.Function is Identifier id)
        {
            // Special handling for builtin len() - validate that argument supports __len__ protocol
            // TODO: Consider using a constant from BuiltinRegistry or BuiltinNames class instead of hardcoded string
            if (id.Name == "len" && argTypes.Count == 1)
            {
                return _protocolValidator.ValidateLen(
                    argTypes[0],
                    call.LineStart,
                    call.ColumnStart);
            }

            var symbol = _symbolTable.Lookup(id.Name);

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
                // Cannot instantiate abstract classes
                if (typeSymbol.IsAbstract)
                {
                    AddError($"Cannot instantiate abstract class '{typeSymbol.Name}'",
                        call.LineStart, call.ColumnStart);
                    return SemanticType.Unknown;
                }

                // Constructor call returns an instance of the type
                return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
            }

            funcSymbol = symbol as FunctionSymbol;

            // If we found a symbol but it's not a function or type, it's not callable
            if (symbol != null && funcSymbol == null && symbol is not TypeSymbol)
            {
                AddError($"'{id.Name}' is not callable (type: {calleeType.GetDisplayName()})",
                    call.LineStart, call.ColumnStart);
                return SemanticType.Unknown;
            }

            // Special handling for builtin functions with overloads
            // If there is exactly one overload, it will be handled by the regular function symbol validation below.
            var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
            if (overloads != null && overloads.Count > 1)
            {
                // First pass: filter by argument count (considering default parameters)
                var candidateOverloads = overloads.Where(o =>
                {
                    var requiredParams = o.Parameters.Count(p => !p.HasDefault);
                    var totalParams = o.Parameters.Count;
                    return totalArgCount >= requiredParams && totalArgCount <= totalParams;
                }).ToList();

                // Second pass: check type compatibility
                FunctionSymbol? matchingOverload = null;
                foreach (var overload in candidateOverloads)
                {
                    bool typesMatch = true;
                    for (int i = 0; i < argTypes.Count; i++)
                    {
                        if (!IsAssignable(argTypes[i], overload.Parameters[i].Type))
                        {
                            typesMatch = false;
                            break;
                        }
                    }
                    if (typesMatch)
                    {
                        matchingOverload = overload;
                        break;
                    }
                }

                if (matchingOverload != null)
                {
                    // Update the identifier symbol to point to the matching overload
                    _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
                    return matchingOverload.ReturnType;
                }
                else
                {
                    // No matching overload found
                    var expectedCounts = string.Join(" or ", overloads.Select(o =>
                    {
                        var required = o.Parameters.Count(p => !p.HasDefault);
                        var total = o.Parameters.Count;
                        return required == total ? total.ToString() : $"{required}-{total}";
                    }).Distinct());
                    AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                    return SemanticType.Unknown;
                }
            }
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            // Count required parameters (those without defaults)
            var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
            var totalParamCount = funcSymbol.Parameters.Count;

            // Validate argument count considering defaults (include both positional and keyword args)
            if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
            {
                if (requiredParamCount == totalParamCount)
                {
                    AddError($"Function expects {totalParamCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                }
                else
                {
                    AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart);
                }
            }
            else
            {
                // Validate positional argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], funcSymbol.Parameters[i].Type))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{funcSymbol.Parameters[i].Type.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < argTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart);
                        }
                    }
                }
            }

            var returnType = funcSymbol.ReturnType;

            // Wrap result in nullable for null conditional calls
            if (isNullConditionalCall && returnType is not NullableType)
            {
                return new NullableType { UnderlyingType = returnType };
            }
            return returnType;
        }

        // Fallback to FunctionType validation (no default parameter support)
        var funcType = CheckExpression(call.Function);

        if (funcType is FunctionType ft)
        {
            // Validate argument count (include both positional and keyword arguments)
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart);
            }
            else
            {
                // Validate positional argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], ft.ParameterTypes[i]))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{ft.ParameterTypes[i].GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart);
                    }
                }
            }

            var returnType = ft.ReturnType;

            // Wrap result in nullable for null conditional calls
            if (isNullConditionalCall && returnType is not NullableType)
            {
                return new NullableType { UnderlyingType = returnType };
            }
            return returnType;
        }

        return SemanticType.Unknown;
    }

    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Count == 0)
        {
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementTypes = list.Elements.Select(CheckExpression).ToList();
        var commonType = elementTypes[0];

        // Try to find common type
        foreach (var elemType in elementTypes.Skip(1))
        {
            if (!IsAssignable(elemType, commonType))
            {
                commonType = SemanticType.Unknown;
                break;
            }
        }

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckDictLiteral(DictLiteral dict)
    {
        if (dict.Entries.Count == 0)
        {
            return new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown, SemanticType.Unknown }
            };
        }

        var keyTypes = dict.Entries.Select(e => CheckExpression(e.Key)).ToList();
        var valueTypes = dict.Entries.Select(e => CheckExpression(e.Value)).ToList();

        var commonKeyType = keyTypes[0];
        var commonValueType = valueTypes[0];

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { commonKeyType, commonValueType }
        };
    }

    private SemanticType CheckSetLiteral(SetLiteral set)
    {
        if (set.Elements.Count == 0)
        {
            return new GenericType
            {
                Name = "set",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementTypes = set.Elements.Select(CheckExpression).ToList();
        var commonType = elementTypes[0];

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckTupleLiteral(TupleLiteral tuple)
    {
        var elementTypes = tuple.Elements.Select(CheckExpression).ToList();
        return new TupleType { ElementTypes = elementTypes };
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("list-comprehension");

        // Process clauses in order
        foreach (var clause in listComp.Clauses)
        {
            if (clause is ForClause forClause)
            {
                // Check iterator type and validate __iter__ protocol
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = _protocolValidator.ValidateIteration(
                    iterType,
                    forClause.Iterator.LineStart,
                    forClause.Iterator.ColumnStart);

                // Define loop variable (single identifier only for now)
                if (forClause.Target is Identifier id)
                {
                    var loopVarSymbol = new VariableSymbol
                    {
                        Name = id.Name,
                        Kind = SymbolKind.Variable,
                        Type = elemType,
                        AccessLevel = AccessLevel.Public,
                        DeclarationLine = id.LineStart,
                        DeclarationColumn = id.ColumnStart
                    };
                    _symbolTable.Define(loopVarSymbol);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                    _semanticInfo.SetExpressionType(forClause.Target, elemType);
                }
                else
                {
                    // For tuple unpacking or other complex targets
                    // TODO: Implement tuple unpacking in comprehensions
                    AddError($"Tuple unpacking in comprehensions not yet supported",
                        forClause.LineStart, forClause.ColumnStart);
                }
            }
            else if (clause is IfClause ifClause)
            {
                // Check condition is boolean
                var condType = CheckExpression(ifClause.Condition);
                if (!condType.IsAssignableTo(SemanticType.Bool))
                {
                    AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                        ifClause.LineStart, ifClause.ColumnStart);
                }
            }
        }

        // Check element expression
        var elementType = CheckExpression(listComp.Element);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckSetComprehension(SetComprehension setComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("set-comprehension");

        // Process clauses in order
        foreach (var clause in setComp.Clauses)
        {
            if (clause is ForClause forClause)
            {
                // Check iterator type and validate __iter__ protocol
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = _protocolValidator.ValidateIteration(
                    iterType,
                    forClause.Iterator.LineStart,
                    forClause.Iterator.ColumnStart);

                // Define loop variable (single identifier only for now)
                if (forClause.Target is Identifier id)
                {
                    var loopVarSymbol = new VariableSymbol
                    {
                        Name = id.Name,
                        Kind = SymbolKind.Variable,
                        Type = elemType,
                        AccessLevel = AccessLevel.Public,
                        DeclarationLine = id.LineStart,
                        DeclarationColumn = id.ColumnStart
                    };
                    _symbolTable.Define(loopVarSymbol);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                    _semanticInfo.SetExpressionType(forClause.Target, elemType);
                }
                else
                {
                    // For tuple unpacking or other complex targets
                    AddError($"Tuple unpacking in comprehensions not yet supported",
                        forClause.LineStart, forClause.ColumnStart);
                }
            }
            else if (clause is IfClause ifClause)
            {
                // Check condition is boolean
                var condType = CheckExpression(ifClause.Condition);
                if (!condType.IsAssignableTo(SemanticType.Bool))
                {
                    AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                        ifClause.LineStart, ifClause.ColumnStart);
                }
            }
        }

        // Check element expression
        var elementType = CheckExpression(setComp.Element);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckDictComprehension(DictComprehension dictComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("dict-comprehension");

        // Process clauses in order
        foreach (var clause in dictComp.Clauses)
        {
            if (clause is ForClause forClause)
            {
                // Check iterator type and validate __iter__ protocol
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = _protocolValidator.ValidateIteration(
                    iterType,
                    forClause.Iterator.LineStart,
                    forClause.Iterator.ColumnStart);

                // Define loop variable (single identifier only for now)
                if (forClause.Target is Identifier id)
                {
                    var loopVarSymbol = new VariableSymbol
                    {
                        Name = id.Name,
                        Kind = SymbolKind.Variable,
                        Type = elemType,
                        AccessLevel = AccessLevel.Public,
                        DeclarationLine = id.LineStart,
                        DeclarationColumn = id.ColumnStart
                    };
                    _symbolTable.Define(loopVarSymbol);
                    _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
                    _semanticInfo.SetExpressionType(forClause.Target, elemType);
                }
                else
                {
                    // For tuple unpacking or other complex targets
                    AddError($"Tuple unpacking in comprehensions not yet supported",
                        forClause.LineStart, forClause.ColumnStart);
                }
            }
            else if (clause is IfClause ifClause)
            {
                // Check condition is boolean
                var condType = CheckExpression(ifClause.Condition);
                if (!condType.IsAssignableTo(SemanticType.Bool))
                {
                    AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                        ifClause.LineStart, ifClause.ColumnStart);
                }
            }
        }

        // Check key and value expressions
        var keyType = CheckExpression(dictComp.Key);
        var valueType = CheckExpression(dictComp.Value);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { keyType, valueType }
        };
    }

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        CheckExpression(cond.Test);
        var thenType = CheckExpression(cond.ThenValue);
        var elseType = CheckExpression(cond.ElseValue);

        // Return common type
        if (thenType.IsAssignableTo(elseType))
            return elseType;
        if (elseType.IsAssignableTo(thenType))
            return thenType;

        return SemanticType.Unknown;
    }

    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        var paramTypes = lambda.Parameters
            .Select(p => _typeResolver.ResolveTypeAnnotation(p.Type))
            .ToList();

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        foreach (var param in lambda.Parameters)
        {
            var paramType = _typeResolver.ResolveTypeAnnotation(param.Type);
            var paramSymbol = new VariableSymbol
            {
                Name = param.Name,
                Kind = SymbolKind.Parameter,
                Type = paramType,
                IsParameter = true
            };
            _symbolTable.Define(paramSymbol);
        }

        var bodyType = CheckExpression(lambda.Body);

        _symbolTable.ExitScope();

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = bodyType
        };
    }

    private SemanticType CheckTypeCast(TypeCast cast)
    {
        CheckExpression(cast.Value);
        return _typeResolver.ResolveTypeAnnotation(cast.TargetType);
    }

    private SemanticType CheckTypeCheck(TypeCheck typeCheck)
    {
        CheckExpression(typeCheck.Value);
        _typeResolver.ResolveTypeAnnotation(typeCheck.CheckType);
        return SemanticType.Bool;
    }

    /// <summary>
    /// Check if a type is numeric (int, long, float, double, etc.).
    /// Delegates to PrimitiveCatalog for exhaustive primitive type checking.
    /// Note: Also allows Unknown types to avoid cascading errors.
    /// </summary>
    private static bool IsNumericType(SemanticType type)
        => type is UnknownType || PrimitiveCatalog.IsNumeric(type);

    /// <summary>
    /// Extract narrowed types from a conditional expression
    /// </summary>
    private Dictionary<string, SemanticType> ExtractNarrowedTypes(Expression condition, bool isPositiveBranch)
    {
        var narrowedTypes = new Dictionary<string, SemanticType>();

        // Handle 'A and B' pattern - combine narrowings from both sides
        if (condition is BinaryOp { Operator: BinaryOperator.And } andOp && isPositiveBranch)
        {
            // In the positive branch, both conditions must be true, so we combine narrowings
            var leftNarrowed = ExtractNarrowedTypes(andOp.Left, true);
            var rightNarrowed = ExtractNarrowedTypes(andOp.Right, true);

            // Merge the dictionaries, with right side taking precedence if there's overlap
            foreach (var kvp in leftNarrowed)
            {
                narrowedTypes[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in rightNarrowed)
            {
                // If we have a narrowing for this variable from both sides,
                // use the more specific one (from the right side)
                narrowedTypes[kvp.Key] = kvp.Value;
            }

            return narrowedTypes;
        }

        // Handle 'x is not None' pattern
        if (condition is BinaryOp { Operator: BinaryOperator.IsNot } binOp)
        {
            if (binOp.Left is Identifier id && binOp.Right is NoneLiteral)
            {
                if (isPositiveBranch)
                {
                    // In the positive branch (x is not None), narrow nullable to non-nullable
                    var symbol = _symbolTable.Lookup(id.Name);
                    if (symbol is VariableSymbol varSymbol && varSymbol.Type is NullableType nullable)
                    {
                        narrowedTypes[id.Name] = nullable.UnderlyingType;
                    }
                }
            }
        }
        // Handle 'x is None' pattern
        else if (condition is BinaryOp { Operator: BinaryOperator.Is } isOp)
        {
            if (isOp.Left is Identifier id && isOp.Right is NoneLiteral)
            {
                if (!isPositiveBranch)
                {
                    // In the negative branch (else after 'x is None'), narrow to non-nullable
                    var symbol = _symbolTable.Lookup(id.Name);
                    if (symbol is VariableSymbol varSymbol && varSymbol.Type is NullableType nullable)
                    {
                        narrowedTypes[id.Name] = nullable.UnderlyingType;
                    }
                }
            }
        }
        // Handle 'isinstance(x, Type)' pattern
        else if (condition is FunctionCall { Function: Identifier { Name: "isinstance" } } call)
        {
            if (call.Arguments.Count >= 2)
            {
                if (isPositiveBranch)
                {
                    // Extract the narrowing key from the first argument
                    string? narrowingKey = ExtractNarrowingKey(call.Arguments[0]);

                    if (narrowingKey != null && call.Arguments[1] is Identifier typeId)
                    {
                        // For isinstance, the second argument is an identifier referring to a type
                        // We need to look it up in the symbol table
                        var typeSymbol = _symbolTable.Lookup(typeId.Name) as TypeSymbol;
                        if (typeSymbol != null)
                        {
                            narrowedTypes[narrowingKey] = new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                        }
                    }
                }
            }
        }

        return narrowedTypes;
    }

    /// <summary>
    /// Extract a key to use for type narrowing from an expression.
    /// For simple identifiers, returns the name. For subscript expressions like arr[i], returns "arr[i]".
    /// </summary>
    private string? ExtractNarrowingKey(Expression expr)
    {
        return expr switch
        {
            Identifier id => id.Name,
            IndexAccess indexAccess => $"{ExtractNarrowingKey(indexAccess.Object)}[{ExtractNarrowingKey(indexAccess.Index)}]",
            _ => null
        };
    }

    /// <summary>
    /// Check if a source type can be assigned to a target type.
    /// This extends the basic IsAssignableTo to handle nullable types and generic variance.
    /// </summary>
    private bool IsAssignable(SemanticType source, SemanticType target)
    {
        // Allow assignment to UnknownType to avoid cascading errors
        // (e.g., when a parameter has no type annotation)
        if (target is UnknownType)
            return true;

        // First check the standard assignability
        if (source.IsAssignableTo(target))
            return true;

        // Non-nullable type can be assigned to nullable version of the same type
        if (target is NullableType nullable)
        {
            return source.IsAssignableTo(nullable.UnderlyingType);
        }

        // Handle covariance for generic collection types (list, set)
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            if (sourceGeneric.Name == targetGeneric.Name &&
                sourceGeneric.TypeArguments.Count == targetGeneric.TypeArguments.Count)
            {
                // For list and set, allow covariant assignment (e.g., list[Dog] to list[Animal])
                if (sourceGeneric.Name == "list" || sourceGeneric.Name == "set")
                {
                    // Check if element type is assignable
                    return IsAssignable(sourceGeneric.TypeArguments[0], targetGeneric.TypeArguments[0]);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extract element type from an iterable type
    /// </summary>
    private SemanticType ExtractElementType(SemanticType iterType)
    {
        // Handle generic iterable types
        if (iterType is GenericType generic)
        {
            // list[T], set[T] -> T
            if ((generic.Name == "list" || generic.Name == "set") && generic.TypeArguments.Count > 0)
            {
                return generic.TypeArguments[0];
            }
            // dict[K, V] -> K (when iterating, we get keys by default)
            if (generic.Name == "dict" && generic.TypeArguments.Count > 0)
            {
                return generic.TypeArguments[0];
            }
        }
        // Handle tuple types
        else if (iterType is TupleType tuple && tuple.ElementTypes.Count > 0)
        {
            // For simplicity, return the first element type
            // In a more sophisticated implementation, we'd handle heterogeneous tuples
            return tuple.ElementTypes[0];
        }
        // Handle string iteration -> str
        else if (iterType == SemanticType.Str)
        {
            return SemanticType.Str;
        }

        // Unknown iterable type
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Validates that no two __init__ methods have the same parameter signature.
    /// Unlike Python (which only allows one __init__), Sharpy supports constructor overloading
    /// by mapping multiple __init__ methods to C# constructor overloads.
    /// </summary>
    private void ValidateConstructorOverloads(TypeSymbol type)
    {
        var constructors = type.Constructors;
        if (constructors.Count <= 1)
            return;  // No overload conflict possible

        _logger.LogDebug($"Validating {constructors.Count} constructor overloads for '{type.Name}'");

        var signatures = new HashSet<string>();
        foreach (var ctor in constructors)
        {
            // Build signature string from parameter types (excluding self)
            var paramTypes = ctor.Parameters
                .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Type.GetDisplayName())
                .ToList();
            var signature = string.Join(",", paramTypes);

            if (!signatures.Add(signature))
            {
                AddError(
                    $"Duplicate constructor signature in '{type.Name}': __init__({signature})",
                    ctor.DeclarationLine,
                    ctor.DeclarationColumn);
            }
        }
    }

    /// <summary>
    /// Validate struct-specific rules
    /// </summary>
    private void ValidateStructRules(TypeSymbol structSymbol, StructDef structDef)
    {
        _logger.LogDebug($"Validating struct-specific rules for '{structSymbol.Name}'");

        // Validate that if a struct has a constructor, it initializes ALL fields
        if (structSymbol.Constructors.Count > 0)
        {
            foreach (var constructor in structSymbol.Constructors)
            {
                ValidateStructConstructorInitializesAllFields(structSymbol, constructor, structDef);
            }
        }
    }

    /// <summary>
    /// Validate that a struct constructor initializes all fields
    /// </summary>
    private void ValidateStructConstructorInitializesAllFields(
        TypeSymbol structSymbol,
        FunctionSymbol constructor,
        StructDef structDef)
    {
        // Find the constructor function definition in the struct body
        var constructorDef = structDef.Body
            .OfType<FunctionDef>()
            .FirstOrDefault(f => f.Name == "__init__" && f.LineStart == constructor.DeclarationLine);

        if (constructorDef == null)
        {
            return; // Constructor not found in AST (shouldn't happen)
        }

        // Track which fields are initialized
        var initializedFields = new HashSet<string>();

        // Analyze the constructor body to find field assignments (self.field = ...)
        AnalyzeConstructorForFieldInitialization(constructorDef.Body, initializedFields);

        // Check if all fields are initialized
        var uninitializedFields = structSymbol.Fields
            .Where(f => !initializedFields.Contains(f.Name))
            .ToList();

        if (uninitializedFields.Count > 0)
        {
            var fieldNames = string.Join(", ", uninitializedFields.Select(f => $"'{f.Name}'"));
            AddError(
                $"Struct '{structSymbol.Name}' constructor must initialize all fields. " +
                $"Missing initialization for: {fieldNames}",
                constructorDef.LineStart,
                constructorDef.ColumnStart);
        }
    }

    /// <summary>
    /// Recursively analyze statements to find field initializations (self.field = ...)
    /// </summary>
    private void AnalyzeConstructorForFieldInitialization(
        IReadOnlyList<Statement> statements,
        HashSet<string> initializedFields)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case Assignment assignment:
                    // Check if this is a self.field assignment
                    if (assignment.Target is MemberAccess memberAccess &&
                        memberAccess.Object is Identifier id &&
                        id.Name == "self")
                    {
                        initializedFields.Add(memberAccess.Member);
                    }
                    break;

                case IfStatement ifStmt:
                    // Don't track fields initialized inside conditionals
                    // They must be initialized unconditionally
                    break;

                case WhileStatement whileStmt:
                    // Don't track fields initialized inside loops
                    break;

                case ForStatement forStmt:
                    // Don't track fields initialized inside loops
                    break;

                case TryStatement tryStmt:
                    // Don't track fields initialized inside try/except
                    break;

                    // For other statement types, we don't need special handling
            }
        }
    }

    /// <summary>
    /// Validate enum-specific rules
    /// </summary>
    private void ValidateEnumRules(EnumDef enumDef)
    {
        _logger.LogDebug($"Validating enum-specific rules for '{enumDef.Name}'");

        // Track the type of enum values to ensure consistency
        SemanticType? enumValueType = null;

        // Rule 1: All enum values must be explicit
        // Rule 2: All values must be of the same type (int or str)
        foreach (var member in enumDef.Members)
        {
            // Rule 1: Check if value is explicit
            if (member.Value == null)
            {
                AddError(
                    $"Enum member '{member.Name}' requires an explicit value. All enum members must have explicit constant values.",
                    member.LineStart,
                    member.ColumnStart);
                continue;
            }

            // Check the type of the value
            var valueType = CheckExpression(member.Value);

            // Rule 2: Ensure value is int or str
            if (!IsIntType(valueType) && !IsStrType(valueType))
            {
                AddError(
                    $"Enum member '{member.Name}' has invalid value type '{valueType.GetDisplayName()}'. Enum values must be int or str.",
                    member.LineStart,
                    member.ColumnStart);
                continue;
            }

            // Rule 2: Ensure all values are the same type
            if (enumValueType == null)
            {
                enumValueType = valueType;
            }
            else if (!valueType.Equals(enumValueType))
            {
                AddError(
                    $"Enum member '{member.Name}' has type '{valueType.GetDisplayName()}' but previous members have type '{enumValueType.GetDisplayName()}'. All enum values must be the same type.",
                    member.LineStart,
                    member.ColumnStart);
            }
        }
    }

    /// <summary>
    /// Check if a method name is a dunder method (starts and ends with __ and has content in between)
    /// </summary>
    private static bool IsDunderMethod(string name) =>
        name.StartsWith("__") && name.EndsWith("__") && name.Length > 4;

    /// <summary>
    /// Validate standalone super() expression (which is always invalid - must be followed by method call)
    /// </summary>
    private SemanticType CheckSuperExpression(SuperExpression superExpr)
    {
        // Standalone super() is not valid - must be used as super().method()
        // The parser allows it, but semantically it's invalid
        AddError("super() must be followed by a method call (e.g., super().__init__())",
            superExpr.LineStart, superExpr.ColumnStart);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Validate super().method() member access and return the method's type
    /// </summary>
    private SemanticType ValidateSuperMemberAccess(MemberAccess memberAccess, SuperExpression superExpr)
    {
        var memberName = memberAccess.Member;

        // Check 1: Must be inside a class
        if (_currentClass == null)
        {
            AddError("super() cannot be used outside of a class",
                superExpr.LineStart, superExpr.ColumnStart);
            return SemanticType.Unknown;
        }

        // Check 2: Class must have a parent
        if (_currentClass.BaseType == null)
        {
            AddError($"super() cannot be used in class '{_currentClass.Name}' which has no parent class",
                superExpr.LineStart, superExpr.ColumnStart);
            return SemanticType.Unknown;
        }

        // Check 3: Cannot access fields via super()
        var parentField = _currentClass.BaseType.Fields.FirstOrDefault(f => f.Name == memberName);
        if (parentField != null)
        {
            AddError("Cannot access parent fields via super(); only methods are allowed",
                memberAccess.LineStart, memberAccess.ColumnStart);
            return SemanticType.Unknown;
        }

        // Check 4: Validate based on method context
        ValidateSuperContextRules(memberName, superExpr, memberAccess);

        // Look up the method in the parent class and return its type
        var parentMethod = _currentClass.BaseType.Methods.FirstOrDefault(m => m.Name == memberName);
        if (parentMethod == null && memberName == "__init__")
        {
            // __init__ might be in Constructors list
            var parentCtor = _currentClass.BaseType.Constructors.FirstOrDefault();
            if (parentCtor != null)
            {
                var paramTypes = parentCtor.Parameters.Skip(1).Select(p => p.Type).ToList();
                return new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = SemanticType.Void
                };
            }
        }

        if (parentMethod != null)
        {
            var paramTypes = parentMethod.Parameters.Skip(1).Select(p => p.Type).ToList();
            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = parentMethod.ReturnType
            };
        }

        AddError($"Parent class '{_currentClass.BaseType.Name}' has no method '{memberName}'",
            memberAccess.LineStart, memberAccess.ColumnStart);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Validate super() context rules based on current method type
    /// </summary>
    private void ValidateSuperContextRules(string calledMethodName, SuperExpression superExpr, MemberAccess memberAccess)
    {
        if (_currentMethodName == null)
        {
            AddError("super() cannot be used outside of a method",
                superExpr.LineStart, superExpr.ColumnStart);
            return;
        }

        // Case 1: Inside __init__
        if (_currentMethodName == "__init__")
        {
            if (calledMethodName != "__init__")
            {
                AddError("super() in __init__ can only call super().__init__(...)",
                    memberAccess.LineStart, memberAccess.ColumnStart);
            }
            else if (_controlFlowDepth > 0)
            {
                AddError("super().__init__() must be the first statement in the constructor, not inside control flow",
                    superExpr.LineStart, superExpr.ColumnStart);
            }
            else if (_superInitCalled)
            {
                AddError("super().__init__() can only be called once",
                    superExpr.LineStart, superExpr.ColumnStart);
            }
            return;
        }

        // Case 2: Inside @override method
        if (_currentMethodIsOverride)
        {
            // In @override methods, can call same method name
            // OR if it's a dunder override, can call other dunders (cross-dunder)
            if (calledMethodName != _currentMethodName)
            {
                if (!(_currentMethodIsDunder && IsDunderMethod(calledMethodName)))
                {
                    AddError($"super() in @override method must call super().{_currentMethodName}(...)",
                        memberAccess.LineStart, memberAccess.ColumnStart);
                }
            }
            return;
        }

        // Case 3: Inside dunder method (not __init__, not @override)
        if (_currentMethodIsDunder)
        {
            // Dunder methods can call any dunder via super()
            if (!IsDunderMethod(calledMethodName))
            {
                AddError("super() in dunder method must call a dunder method (e.g., super().__eq__(...))",
                    memberAccess.LineStart, memberAccess.ColumnStart);
            }
            return;
        }

        // Case 4: Regular method - super() not allowed
        AddError("super() cannot be used in regular methods; only in __init__, @override, or dunder methods",
            superExpr.LineStart, superExpr.ColumnStart);
    }

    /// <summary>
    /// Check if a type is an int type
    /// </summary>
    private static bool IsIntType(SemanticType type)
    {
        return type == SemanticType.Int || type == SemanticType.Long;
    }

    /// <summary>
    /// Check if a type is a str type
    /// </summary>
    private static bool IsStrType(SemanticType type)
    {
        return type == SemanticType.Str;
    }

    private void AddError(string message, int? line = null, int? column = null)
    {
        if (_errors.Count >= MaxErrors)
        {
            if (_errors.Count == MaxErrors)
            {
                _logger.LogError("Maximum error count reached, stopping type checking", 0, 0);
            }
            if (!ContinueAfterError)
            {
                throw new SemanticAnalysisException("Type checking failed with too many errors");
            }
            return;
        }

        var error = new SemanticError(message, line, column);
        _errors.Add(error);
        _logger.LogError(error.Message, line ?? 0, column ?? 0);
    }
}

public class SemanticAnalysisException : Exception
{
    public SemanticAnalysisException(string message) : base(message) { }
}
