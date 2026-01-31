using Sharpy.Compiler.Diagnostics;
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
                    // In the positive branch (x is not None), narrow nullable/optional to non-nullable
                    var symbol = _symbolTable.Lookup(id.Name);
                    if (symbol is VariableSymbol varSymbol)
                    {
                        if (varSymbol.Type is NullableType nullable)
                            narrowedTypes[id.Name] = nullable.UnderlyingType;
                        else if (varSymbol.Type is OptionalType optional)
                            narrowedTypes[id.Name] = optional.UnderlyingType;
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
                    if (symbol is VariableSymbol varSymbol)
                    {
                        if (varSymbol.Type is NullableType nullable)
                            narrowedTypes[id.Name] = nullable.UnderlyingType;
                        else if (varSymbol.Type is OptionalType optional)
                            narrowedTypes[id.Name] = optional.UnderlyingType;
                    }
                }
            }
        }
        // Handle 'isinstance(x, Type)' pattern
        else if (condition is FunctionCall { Function: Identifier { Name: "isinstance" } } call)
        {
            if (call.Arguments.Length >= 2)
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

        // Non-optional type can be assigned to optional version of the same type
        if (target is OptionalType optional)
        {
            return source.IsAssignableTo(optional.UnderlyingType);
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
            OptionalType ot => new OptionalType
            {
                UnderlyingType = SubstituteTypeParametersInType(ot.UnderlyingType, substitutions)
            },
            ResultType rt => new ResultType
            {
                OkType = SubstituteTypeParametersInType(rt.OkType, substitutions),
                ErrorType = SubstituteTypeParametersInType(rt.ErrorType, substitutions)
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
                    ctor.DeclarationColumn,
                    code: DiagnosticCodes.Semantic.DuplicateDefinition);
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
                constructorDef.ColumnStart,
                code: DiagnosticCodes.Semantic.UninitializedStructField);
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
                    member.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidEnumValue);
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
                    member.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidEnumValue);
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
                    member.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidEnumValue);
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
            superExpr.LineStart, superExpr.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidSuperUsage);
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
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.SuperOutsideClass);
            return SemanticType.Unknown;
        }

        // Check 2: Class must have a parent
        if (_currentClass.BaseType == null)
        {
            AddError($"super() cannot be used in class '{_currentClass.Name}' which has no parent class",
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.SuperNoParent);
            return SemanticType.Unknown;
        }

        // Check 3: Cannot access fields via super()
        // Check the entire inheritance chain for fields
        var currentType = _currentClass.BaseType;
        while (currentType != null)
        {
            var field = currentType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field != null)
            {
                AddError("Cannot access parent fields via super(); only methods are allowed",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage);
                return SemanticType.Unknown;
            }
            currentType = currentType.BaseType;
        }

        // Check 4: Validate based on method context
        ValidateSuperContextRules(memberName, superExpr, memberAccess);

        // Look up the method in the parent class hierarchy and return its type
        // Use FindMethodInHierarchy to traverse the full inheritance chain
        var (parentMethod, methodOwner) = FindMethodInHierarchy(_currentClass.BaseType, memberName);
        if (parentMethod == null && memberName == "__init__")
        {
            // __init__ might be in Constructors list - check full hierarchy
            currentType = _currentClass.BaseType;
            while (currentType != null)
            {
                // For .NET types, we can't do proper overload resolution here
                // (we don't have access to the call arguments). Mark the type to skip validation
                // and let C# do the overload resolution at compile time.
                if (currentType.ClrType != null)
                {
                    return new FunctionType
                    {
                        ParameterTypes = new List<SemanticType>(),
                        ReturnType = SemanticType.Void,
                        SkipArgumentValidation = true
                    };
                }

                var parentCtor = currentType.Constructors.FirstOrDefault();
                if (parentCtor != null)
                {
                    var paramTypes = parentCtor.Parameters.Skip(1).Select(p => p.Type).ToList();
                    return new FunctionType
                    {
                        ParameterTypes = paramTypes,
                        ReturnType = SemanticType.Void
                    };
                }
                currentType = currentType.BaseType;
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

        AddError($"No method '{memberName}' found in parent class hierarchy of '{_currentClass.Name}'",
            memberAccess.LineStart, memberAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.UndefinedMember);
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
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidSuperUsage);
            return;
        }

        // Case 1: Inside __init__
        if (_currentMethodName == "__init__")
        {
            if (calledMethodName != "__init__")
            {
                AddError("super() in __init__ can only call super().__init__(...)",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage);
            }
            else if (_controlFlowDepth > 0)
            {
                AddError("super().__init__() must be the first statement in the constructor, not inside control flow",
                    superExpr.LineStart, superExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage);
            }
            else if (_superInitCalled)
            {
                AddError("super().__init__() can only be called once",
                    superExpr.LineStart, superExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage);
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
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.InvalidSuperUsage);
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
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage);
            }
            return;
        }

        // Case 4: Regular method - super() not allowed
        AddError("super() cannot be used in regular methods; only in __init__, @override, or dunder methods",
            superExpr.LineStart, superExpr.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidSuperUsage);
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

    /// <summary>
    /// Validate that a class or struct implements all required interface methods.
    /// This includes methods from directly implemented interfaces and their base interfaces.
    /// </summary>
    private void ValidateInterfaceImplementations(TypeSymbol typeSymbol, int? declarationLine, int? declarationColumn)
    {
        // Collect all interfaces that need to be implemented
        var allInterfaces = CollectAllInterfaces(typeSymbol);
        if (allInterfaces.Count == 0)
            return;

        _logger.LogDebug($"Validating interface implementations for '{typeSymbol.Name}': {allInterfaces.Count} interfaces");

        // Collect all methods implemented by this type and its base classes (by name)
        var implementedMethodsByName = CollectImplementedMethodsByName(typeSymbol);

        // Check each interface method is implemented
        foreach (var iface in allInterfaces)
        {
            foreach (var interfaceMethod in iface.Methods)
            {
                // Check if there's a method with the same name
                if (!implementedMethodsByName.TryGetValue(interfaceMethod.Name, out var classMethod))
                {
                    AddError(
                        $"Class '{typeSymbol.Name}' does not implement interface method '{iface.Name}.{interfaceMethod.Name}'",
                        declarationLine,
                        declarationColumn,
                        code: DiagnosticCodes.Semantic.ProtocolMissingMethod);
                    continue;
                }

                // Verify parameter count matches (excluding 'self')
                var interfaceParams = interfaceMethod.Parameters.Where(p => p.Name != "self").ToList();
                var classParams = classMethod.Parameters.Where(p => p.Name != "self").ToList();

                if (interfaceParams.Count != classParams.Count)
                {
                    AddError(
                        $"Class '{typeSymbol.Name}' method '{interfaceMethod.Name}' has {classParams.Count} parameters but interface '{iface.Name}' requires {interfaceParams.Count}",
                        declarationLine,
                        declarationColumn,
                        code: DiagnosticCodes.Semantic.IncompatibleOverride);
                }
            }
        }
    }

    /// <summary>
    /// Collect all interfaces a type implements, including:
    /// - Directly implemented interfaces
    /// - Base interfaces (interface inheritance)
    /// - Interfaces implemented by base classes
    /// </summary>
    private HashSet<TypeSymbol> CollectAllInterfaces(TypeSymbol type)
    {
        var result = new HashSet<TypeSymbol>();
        var visited = new HashSet<string>();
        var visitedBaseClasses = new HashSet<string>();
        var queue = new Queue<TypeSymbol>();

        // Add directly implemented interfaces
        foreach (var iface in type.Interfaces)
        {
            queue.Enqueue(iface);
        }

        // Add interfaces from base class hierarchy (with cycle detection)
        var baseType = type.BaseType;
        while (baseType != null && visitedBaseClasses.Add(baseType.Name))
        {
            foreach (var iface in baseType.Interfaces)
            {
                queue.Enqueue(iface);
            }
            baseType = baseType.BaseType;
        }

        // BFS through interface inheritance
        while (queue.Count > 0)
        {
            var iface = queue.Dequeue();
            if (!visited.Add(iface.Name))
                continue;

            result.Add(iface);

            // Add base interfaces
            foreach (var baseIface in iface.Interfaces)
            {
                queue.Enqueue(baseIface);
            }
        }

        return result;
    }

    /// <summary>
    /// Collect all methods implemented by a type and its base classes.
    /// Returns a dictionary mapping method name to the FunctionSymbol.
    /// Used for interface implementation validation by name matching.
    /// </summary>
    private Dictionary<string, FunctionSymbol> CollectImplementedMethodsByName(TypeSymbol type)
    {
        var result = new Dictionary<string, FunctionSymbol>();
        var visited = new HashSet<string>();

        var currentType = type;
        while (currentType != null && visited.Add(currentType.Name))
        {
            foreach (var method in currentType.Methods)
            {
                // Only add if not already present (prefer most derived implementation)
                if (!result.ContainsKey(method.Name))
                {
                    result[method.Name] = method;
                }
            }
            currentType = currentType.BaseType;
        }

        return result;
    }

    /// <summary>
    /// Finds the least common ancestor (most specific common base type) of a list of types.
    /// Returns SemanticType.Object if no more specific common ancestor exists.
    /// Returns SemanticType.Unknown only if types list is empty.
    /// </summary>
    private SemanticType FindLeastCommonAncestor(List<SemanticType> types)
    {
        if (types.Count == 0)
            return SemanticType.Unknown;
        if (types.Count == 1)
            return types[0];

        // Get all ancestors of the first type (including itself)
        var ancestorChain = GetTypeAncestorChain(types[0]);
        if (ancestorChain.Count == 0)
            return SemanticType.Object;

        // For each subsequent type, find common ancestors
        foreach (var type in types.Skip(1))
        {
            var typeAncestors = new HashSet<string>(
                GetTypeAncestorChain(type).Select(t => GetTypeKey(t)));

            // Filter ancestor chain to only include common ancestors
            ancestorChain = ancestorChain
                .Where(a => typeAncestors.Contains(GetTypeKey(a)))
                .ToList();

            if (ancestorChain.Count == 0)
                return SemanticType.Object;
        }

        // Return the most specific common ancestor (first in chain)
        return ancestorChain.First();
    }

    /// <summary>
    /// Gets a unique key for a type to use in LCA comparison.
    /// </summary>
    private static string GetTypeKey(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Name,
            BuiltinType bt => bt.Name,
            GenericType gt => $"{gt.Name}<{string.Join(",", gt.TypeArguments.Select(GetTypeKey))}>",
            NullableType nt => $"{GetTypeKey(nt.UnderlyingType)}|None",
            OptionalType ot => $"{GetTypeKey(ot.UnderlyingType)}?",
            ResultType rt => $"{GetTypeKey(rt.OkType)}!{GetTypeKey(rt.ErrorType)}",
            _ => type.GetDisplayName()
        };
    }

    /// <summary>
    /// Gets the inheritance chain for a type, from most specific to least specific.
    /// For UserDefinedType: [Type, BaseType, BaseType.BaseType, ..., object]
    /// For primitives: [PrimitiveType, object]
    /// </summary>
    private List<SemanticType> GetTypeAncestorChain(SemanticType type)
    {
        var chain = new List<SemanticType> { type };

        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            var current = udt.Symbol.BaseType;
            while (current != null)
            {
                chain.Add(new UserDefinedType
                {
                    Name = current.Name,
                    Symbol = current
                });
                current = current.BaseType;
            }
        }

        // Add object as ultimate base (if not already there)
        var lastTypeName = chain.Last().GetDisplayName().ToLowerInvariant();
        if (lastTypeName != "object" && lastTypeName != "system.object")
        {
            chain.Add(SemanticType.Object);
        }

        return chain;
    }

    private void AddError(string message, int? line = null, int? column = null, string? code = null)
    {
        if (_diagnostics.ErrorCount >= MaxErrors)
        {
            if (_diagnostics.ErrorCount == MaxErrors)
            {
                _logger.LogError("Maximum error count reached, stopping type checking", 0, 0);
            }
            if (!ContinueAfterError)
            {
                throw new SemanticAnalysisException("Type checking failed with too many errors");
            }
            return;
        }

        _diagnostics.AddError(message, line, column, code: code, phase: CompilerPhase.TypeChecking);
        _logger.LogError(message, line ?? 0, column ?? 0);
    }
}

public class SemanticAnalysisException : Exception
{
    public SemanticAnalysisException(string message) : base(message) { }
}
