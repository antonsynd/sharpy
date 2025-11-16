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
                if (!defaultType.IsAssignableTo(paramType))
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
        }
        
        // Otherwise, check as a regular assignment
        var targetType = CheckExpression(assignment.Target);
        var valueType = CheckExpression(assignment.Value);

        if (!valueType.IsAssignableTo(targetType))
        {
            AddError($"Cannot assign type '{valueType.GetDisplayName()}' to '{targetType.GetDisplayName()}'",
                assignment.LineStart, assignment.ColumnStart);
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
            else if (!initType.IsAssignableTo(declaredType))
            {
                AddError($"Cannot assign type '{initType.GetDisplayName()}' to variable of type '{declaredType.GetDisplayName()}'",
                    varDecl.LineStart, varDecl.ColumnStart);
            }
        }
        else if (declaredType is UnknownType)
        {
            AddError($"Variable '{varDecl.Name}' declared with 'auto' must have an initializer",
                varDecl.LineStart, varDecl.ColumnStart);
        }

        // Define or update symbol with resolved type
        var existingSymbol = _symbolTable.Lookup(varDecl.Name, searchParents: false);
        if (existingSymbol is VariableSymbol varSymbol)
        {
            // Update existing symbol with resolved type
            var updatedSymbol = varSymbol with { Type = declaredType };
            _symbolTable.Define(updatedSymbol);
        }
        else
        {
            // Create new variable symbol
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
            if (!returnType.IsAssignableTo(_currentFunctionReturnType))
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

        foreach (var stmt in ifStmt.ThenBody)
            CheckStatement(stmt);

        foreach (var stmt in ifStmt.ElseBody)
            CheckStatement(stmt);
    }

    private void CheckWhile(WhileStatement whileStmt)
    {
        var condType = CheckExpression(whileStmt.Test);
        if (condType != SemanticType.Bool && !(condType is UnknownType))
        {
            AddError($"While condition must be boolean, got '{condType.GetDisplayName()}'",
                whileStmt.LineStart, whileStmt.ColumnStart);
        }

        foreach (var stmt in whileStmt.Body)
            CheckStatement(stmt);
    }

    private void CheckFor(ForStatement forStmt)
    {
        var iterType = CheckExpression(forStmt.Iterator);
        // TODO: Check that iterType is iterable

        // Add loop variable to scope
        // The target is typically an Identifier or TupleExpression
        if (forStmt.Target is Identifier id)
        {
            // Infer the type of the loop variable from the iterator
            // For now, use Unknown - proper iteration type inference would extract element type
            var loopVarSymbol = new VariableSymbol
            {
                Name = id.Name,
                Kind = SymbolKind.Variable,
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

            // TODO: Infer element type from iterable
            _semanticInfo.SetExpressionType(forStmt.Target, SemanticType.Unknown);
        }

        foreach (var stmt in forStmt.Body)
            CheckStatement(stmt);
    }

    private void CheckRaise(RaiseStatement raiseStmt)
    {
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
            foreach (var stmt in handler.Body)
                CheckStatement(stmt);
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

        if (objectType is UserDefinedType udt)
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
            funcSymbol = symbol as FunctionSymbol;

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
                        if (!argTypes[i].IsAssignableTo(overload.Parameters[i].Type))
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
                    if (!argTypes[i].IsAssignableTo(funcSymbol.Parameters[i].Type))
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
                    if (!argTypes[i].IsAssignableTo(ft.ParameterTypes[i]))
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
            if (!elemType.IsAssignableTo(commonType))
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
