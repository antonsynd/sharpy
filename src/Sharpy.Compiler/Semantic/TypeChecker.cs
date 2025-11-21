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
    }

    public IReadOnlyList<SemanticError> Errors
    {
        get
        {
            // Combine errors from type checker, control flow validator, and access validator
            var allErrors = new List<SemanticError>(_errors);
            allErrors.AddRange(_controlFlowValidator.Errors);
            allErrors.AddRange(_accessValidator.Errors);
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
                // Enums don't need type checking
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
        var functionSymbol = _symbolTable.LookupFunction(functionDef.Name);

        // Resolve return type
        var returnType = _typeResolver.ResolveTypeAnnotation(functionDef.ReturnType);

        // Special case: __init__ always returns None/void
        if (functionDef.Name == "__init__")
        {
            // Validate that __init__ has no return type or -> None
            if (functionDef.ReturnType != null && returnType != SemanticType.Void)
            {
                AddError($"Constructor '__init__' cannot have return type '{returnType.GetDisplayName()}'. " +
                         "Constructors must have no return type annotation or '-> None'.",
                    functionDef.LineStart, functionDef.ColumnStart);
            }
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

        // Validate self parameter for instance methods
        if (_currentClass != null && functionDef.Parameters.Count > 0)
        {
            // Instance method should have 'self' as first parameter
            if (functionDef.Parameters[0].Name != "self")
            {
                AddError($"Instance method '{functionDef.Name}' must have 'self' as the first parameter",
                    functionDef.LineStart, functionDef.ColumnStart);
            }
        }
        else if (_currentClass != null && functionDef.Parameters.Count == 0)
        {
            // Instance method with no parameters at all
            AddError($"Instance method '{functionDef.Name}' is missing required 'self' parameter",
                functionDef.LineStart, functionDef.ColumnStart);
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

        // Restore previous class
        _currentClass = previousClass;
        _accessValidator.ExitClass();

        _symbolTable.ExitScope();
    }

    private void CheckStruct(StructDef structDef)
    {
        _logger.LogDebug($"Type checking struct: {structDef.Name}");

        // Enter struct scope
        _symbolTable.EnterScope($"struct:{structDef.Name}");

        foreach (var statement in structDef.Body)
        {
            CheckStatement(statement);
        }

        _symbolTable.ExitScope();
    }

    private void CheckAssignment(Assignment assignment)
    {
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
                    var existingSymbol = _symbolTable.Lookup(tupleTargetId.Name, searchParents: true);

                    // If the identifier doesn't exist, create it with inferred type
                    if (existingSymbol == null)
                    {
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
                        // Check type compatibility for existing variable
                        var targetElemType = CheckExpression(targetElem);
                        if (!IsAssignable(valueElemType, targetElemType))
                        {
                            AddError($"Cannot assign type '{valueElemType.GetDisplayName()}' to '{targetElemType.GetDisplayName()}' in tuple unpacking",
                                targetElem.LineStart, targetElem.ColumnStart);
                        }
                    }
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

        // Check if this is a simple assignment to an undefined identifier (type inference case)
        if (assignment.Operator == AssignmentOperator.Assign && assignment.Target is Identifier targetId)
        {
            var existingSymbol = _symbolTable.Lookup(targetId.Name, searchParents: true);

            // If the identifier doesn't exist, this is an implicit variable declaration with type inference
            if (existingSymbol == null)
            {
                var inferredType = CheckExpression(assignment.Value);

                // Create a new variable symbol with the inferred type
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
            // Check if trying to reassign a constant
            else if (existingSymbol is VariableSymbol varSymbol && varSymbol.IsConstant)
            {
                // Assignment without type annotation to a const is always an error
                // If the user wants to shadow the const, they must use a type annotation
                AddError($"Cannot reassign constant variable '{targetId.Name}'. Use a type annotation to shadow it instead.",
                    assignment.LineStart, assignment.ColumnStart);
                return;
            }
        }

        // Otherwise, check as a regular assignment
        var targetType = CheckExpression(assignment.Target);
        var valueType = CheckExpression(assignment.Value);

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

        if (existingSymbol is VariableSymbol varSymbol)
        {
            // Symbol already exists in the current scope
            // Per language spec: Variable declarations with type annotations (including 'auto')
            // are allowed to shadow/redefine variables in the same scope
            // This is intentional redefinition - const symbols are pre-defined by NameResolver,
            // and non-const variables can be redefined with a type annotation
            // Just skip the define step since the symbol is already there
        }
        else
        {
            // Create new variable symbol (normal case for non-const variables)
            var newSymbol = new VariableSymbol
            {
                Name = varDecl.Name,
                Kind = SymbolKind.Variable,
                Type = declaredType,
                IsConstant = varDecl.IsConst,
                DeclarationLine = varDecl.LineStart,
                DeclarationColumn = varDecl.ColumnStart
            };
            _symbolTable.Define(newSymbol);
        }
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
        foreach (var stmt in ifStmt.ThenBody)
            CheckStatement(stmt);
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
            foreach (var stmt in elif.Body)
                CheckStatement(stmt);
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
            foreach (var stmt in ifStmt.ElseBody)
                CheckStatement(stmt);
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
        foreach (var stmt in whileStmt.Body)
            CheckStatement(stmt);
        _symbolTable.ExitScope();

        // Restore original narrowed types
        _narrowedTypes = savedNarrowedTypes;
    }

    private void CheckFor(ForStatement forStmt)
    {
        var iterType = CheckExpression(forStmt.Iterator);

        // Extract element type from iterable
        var elementType = ExtractElementType(iterType);

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
        foreach (var stmt in forStmt.Body)
            CheckStatement(stmt);

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
        foreach (var stmt in tryStmt.Body)
            CheckStatement(stmt);
        _symbolTable.ExitScope();

        // Each exception handler has its own scope
        foreach (var handler in tryStmt.Handlers)
        {
            _symbolTable.EnterScope("except");
            _inExceptBlock = true;
            foreach (var stmt in handler.Body)
                CheckStatement(stmt);
            _inExceptBlock = false;
            _symbolTable.ExitScope();
        }

        // Finally block has its own scope
        if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
        {
            _symbolTable.EnterScope("finally");
            foreach (var stmt in tryStmt.FinallyBody)
                CheckStatement(stmt);
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
            TypeSymbol => SemanticType.Unknown, // Type names used as values need special handling
            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);
        var rightType = CheckExpression(binOp.Right);

        return binOp.Operator switch
        {
            // Special handling for Add - supports both arithmetic and string concatenation
            BinaryOperator.Add => InferAdditionType(leftType, rightType),

            BinaryOperator.Subtract or
            BinaryOperator.Multiply or BinaryOperator.Divide or
            BinaryOperator.FloorDivide or BinaryOperator.Modulo or
            BinaryOperator.Power => InferArithmeticType(leftType, rightType),

            BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or
            BinaryOperator.BitwiseXor or BinaryOperator.LeftShift or
            BinaryOperator.RightShift => SemanticType.Int,

            BinaryOperator.And or BinaryOperator.Or => SemanticType.Bool,

            BinaryOperator.Equal or BinaryOperator.NotEqual or
            BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or
            BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual => SemanticType.Bool,

            BinaryOperator.In or BinaryOperator.NotIn or
            BinaryOperator.Is or BinaryOperator.IsNot => SemanticType.Bool,

            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        var operandType = CheckExpression(unOp.Operand);

        return unOp.Operator switch
        {
            UnaryOperator.Not => SemanticType.Bool,
            UnaryOperator.Minus or UnaryOperator.Plus => operandType,
            UnaryOperator.BitwiseNot => SemanticType.Int,
            _ => SemanticType.Unknown
        };
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // All comparison chains return bool
        foreach (var operand in chain.Operands)
        {
            CheckExpression(operand);
        }
        return SemanticType.Bool;
    }

    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        var objectType = CheckExpression(memberAccess.Object);

        if (objectType is UserDefinedType udt && udt.Symbol != null)
        {
            // Look for field or property
            var field = udt.Symbol.Fields.FirstOrDefault(f => f.Name == memberAccess.Member);
            if (field != null)
            {
                // Validate access level
                _accessValidator.ValidateFieldAccess(field, udt.Symbol, memberAccess.LineStart, memberAccess.ColumnStart);
                return field.Type;
            }

            // Look for method
            var method = udt.Symbol.Methods.FirstOrDefault(m => m.Name == memberAccess.Member);
            if (method != null)
            {
                // Validate access level
                _accessValidator.ValidateMethodAccess(method, udt.Symbol, memberAccess.LineStart, memberAccess.ColumnStart);

                return new FunctionType
                {
                    ParameterTypes = method.Parameters.Select(p => p.Type).ToList(),
                    ReturnType = method.ReturnType
                };
            }

            AddError($"Type '{objectType.GetDisplayName()}' has no member '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
        }

        return SemanticType.Unknown;
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

        if (objectType is GenericType generic)
        {
            if (generic.Name == "list" && generic.TypeArguments.Count > 0)
                return generic.TypeArguments[0];
            if (generic.Name == "dict" && generic.TypeArguments.Count > 1)
                return generic.TypeArguments[1];
        }

        return SemanticType.Unknown;
    }

    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Check the called expression type first
        var calleeType = CheckExpression(call.Function);

        // Check arguments and collect their types
        var argTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            argTypes.Add(CheckExpression(arg));
        }

        foreach (var kwarg in call.KeywordArguments)
        {
            CheckExpression(kwarg.Value);
        }

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;
        if (call.Function is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
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
                    return argTypes.Count >= requiredParams && argTypes.Count <= totalParams;
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
                    AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {argTypes.Count}",
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

            // Validate argument count considering defaults
            if (argTypes.Count < requiredParamCount || argTypes.Count > totalParamCount)
            {
                if (requiredParamCount == totalParamCount)
                {
                    AddError($"Function expects {totalParamCount} arguments but got {argTypes.Count}",
                        call.LineStart, call.ColumnStart);
                }
                else
                {
                    AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {argTypes.Count}",
                        call.LineStart, call.ColumnStart);
                }
            }
            else
            {
                // Validate argument types for the provided arguments
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], funcSymbol.Parameters[i].Type))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{funcSymbol.Parameters[i].Type.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart);
                    }
                }
            }

            return funcSymbol.ReturnType;
        }

        // Fallback to FunctionType validation (no default parameter support)
        var funcType = CheckExpression(call.Function);

        if (funcType is FunctionType ft)
        {
            // Validate argument count (ignoring keyword arguments for now)
            if (argTypes.Count != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {argTypes.Count}",
                    call.LineStart, call.ColumnStart);
            }
            else
            {
                // Validate argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], ft.ParameterTypes[i]))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{ft.ParameterTypes[i].GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart);
                    }
                }
            }

            return ft.ReturnType;
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
                // Check iterator type
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = ExtractElementType(iterType);

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
                // Check iterator type
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = ExtractElementType(iterType);

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
                // Check iterator type
                var iterType = CheckExpression(forClause.Iterator);
                var elemType = ExtractElementType(iterType);

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

    private SemanticType InferArithmeticType(SemanticType left, SemanticType right)
    {
        // Simplified: promote to widest type
        if (left == SemanticType.Double || right == SemanticType.Double)
            return SemanticType.Double;
        if (left == SemanticType.Float || right == SemanticType.Float)
            return SemanticType.Float;
        if (left == SemanticType.Long || right == SemanticType.Long)
            return SemanticType.Long;
        return SemanticType.Int;
    }

    private SemanticType InferAdditionType(SemanticType left, SemanticType right)
    {
        // String concatenation
        if (left == SemanticType.Str && right == SemanticType.Str)
            return SemanticType.Str;

        // Otherwise, use arithmetic type inference
        return InferArithmeticType(left, right);
    }

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
