using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Type checking utilities and validation
/// </summary>
public partial class TypeChecker
{
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
    /// Substitutes type parameters with their corresponding type arguments in a type.
    /// For example, given return type T and type argument int, returns int.
    /// </summary>
    private SemanticType SubstituteTypeParameters(
        SemanticType type,
        List<TypeParameterDef> typeParams,
        List<SemanticType> typeArgs)
    {
        if (typeParams.Count != typeArgs.Count)
            return type;

        // Create a mapping from type parameter name to type argument
        var substitutions = new Dictionary<string, SemanticType>();
        for (int i = 0; i < typeParams.Count; i++)
        {
            substitutions[typeParams[i].Name] = typeArgs[i];
        }

        return SubstituteTypeParametersInType(type, substitutions);
    }

    private SemanticType SubstituteTypeParametersInType(
        SemanticType type,
        Dictionary<string, SemanticType> substitutions)
    {
        return type switch
        {
            TypeParameterType tpt when substitutions.TryGetValue(tpt.Name, out var subst) => subst,
            GenericType gt => new GenericType
            {
                Name = gt.Name,
                TypeArguments = gt.TypeArguments.Select(t => SubstituteTypeParametersInType(t, substitutions)).ToList(),
                GenericDefinition = gt.GenericDefinition
            },
            NullableType nt => new NullableType
            {
                UnderlyingType = SubstituteTypeParametersInType(nt.UnderlyingType, substitutions)
            },
            FunctionType ft => new FunctionType
            {
                ParameterTypes = ft.ParameterTypes.Select(t => SubstituteTypeParametersInType(t, substitutions)).ToList(),
                ReturnType = SubstituteTypeParametersInType(ft.ReturnType, substitutions)
            },
            TupleType tt => new TupleType
            {
                ElementTypes = tt.ElementTypes.Select(t => SubstituteTypeParametersInType(t, substitutions)).ToList()
            },
            _ => type // For types that don't contain type parameters, return as-is
        };
    }

    /// <summary>
    /// Checks if an expression is a valid assignment target.
    /// Valid targets: Identifier, MemberAccess (attribute), IndexAccess, TupleLiteral (for unpacking)
    /// Invalid targets: FunctionCall, Literal, BinaryExpression, etc.
    /// </summary>
    private bool IsValidAssignmentTarget(Expression target)
    {
        return target switch
        {
            Identifier => true,
            MemberAccess => true,
            IndexAccess => true,
            TupleLiteral tuple => tuple.Elements.All(IsValidAssignmentTarget),
            _ => false
        };
    }

    /// <summary>
    /// Gets a human-readable description of an invalid assignment target for error messages.
    /// </summary>
    private string GetAssignmentTargetDescription(Expression target)
    {
        return target switch
        {
            FunctionCall call => call.Function is Identifier id ? $"function call '{id.Name}()'" : "function call result",
            IntegerLiteral => "integer literal",
            FloatLiteral => "float literal",
            StringLiteral => "string literal",
            BooleanLiteral => "boolean literal",
            NoneLiteral => "'None'",
            ListLiteral => "list literal",
            DictLiteral => "dictionary literal",
            SetLiteral => "set literal",
            BinaryOp => "expression result",
            UnaryOp => "expression result",
            ConditionalExpression => "conditional expression result",
            ComparisonChain => "comparison result",
            _ => "expression"
        };
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
