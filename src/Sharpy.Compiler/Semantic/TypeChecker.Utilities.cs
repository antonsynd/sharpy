using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Collections;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Type checking utilities and validation
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Returns true if the type can be used in a boolean context (if, while conditions).
    /// A type is truth-testable if it is bool, UnknownType, or a user-defined type with __bool__.
    /// </summary>
    private bool IsTruthTestable(SemanticType type)
    {
        if (type == SemanticType.Bool || type is UnknownType)
            return true;

        // User-defined types with __bool__ can be used in boolean contexts
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            return udt.Symbol.Methods.Any(m => m.Name == DunderNames.Bool);
        }

        return false;
    }

    private (Dictionary<string, SemanticType> NarrowedTypes, NarrowingDecision Decision) ExtractNarrowedTypes(Expression condition, bool isPositiveBranch)
    {
        var narrowedTypes = new Dictionary<string, SemanticType>();
        var optionalNarrowings = new List<OptionalNarrowing>();
        var isInstanceNarrowings = new List<IsInstanceNarrowing>();

        // Handle 'not <expr>' pattern - flip the branch polarity and recurse
        if (condition is UnaryOp { Operator: UnaryOperator.Not } notOp)
        {
            return ExtractNarrowedTypes(notOp.Operand, !isPositiveBranch);
        }

        // Handle 'A and B' pattern - combine narrowings from both sides
        if (condition is BinaryOp { Operator: BinaryOperator.And } andOp && isPositiveBranch)
        {
            // In the positive branch, both conditions must be true, so we combine narrowings
            var (leftNarrowed, leftDecision) = ExtractNarrowedTypes(andOp.Left, true);
            var (rightNarrowed, rightDecision) = ExtractNarrowedTypes(andOp.Right, true);

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

            // Merge narrowing decisions from both sides
            optionalNarrowings.AddRange(leftDecision.OptionalNarrowings);
            optionalNarrowings.AddRange(rightDecision.OptionalNarrowings);
            isInstanceNarrowings.AddRange(leftDecision.IsInstanceNarrowings);
            isInstanceNarrowings.AddRange(rightDecision.IsInstanceNarrowings);

            return (narrowedTypes, new NarrowingDecision(optionalNarrowings, isInstanceNarrowings));
        }

        // Handle 'x is not None' pattern (x can be identifier or member access like self.field)
        if (condition is BinaryOp { Operator: BinaryOperator.IsNot } binOp)
        {
            if (binOp.Right is NoneLiteral)
            {
                var narrowingKey = ExtractNarrowingKey(binOp.Left);
                if (narrowingKey != null && isPositiveBranch)
                {
                    // Get the type of the expression being narrowed
                    SemanticType? resolvedType = null;
                    if (binOp.Left is Identifier id)
                    {
                        var symbol = _symbolTable.Lookup(id.Name);
                        if (symbol is VariableSymbol varSymbol)
                            resolvedType = GetVariableType(varSymbol);
                    }
                    else
                    {
                        // For member access (self.field), use the already type-checked expression type
                        resolvedType = _semanticInfo.GetExpressionType(binOp.Left);
                    }

                    if (resolvedType is NullableType nullable)
                    {
                        narrowedTypes[narrowingKey] = nullable.UnderlyingType;
                        optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, nullable.UnderlyingType, IsValueTypeNullable: true, NarrowInThenBranch: true));
                    }
                    else if (resolvedType is OptionalType optional)
                    {
                        narrowedTypes[narrowingKey] = optional.UnderlyingType;
                        optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, optional.UnderlyingType, IsValueTypeNullable: false, NarrowInThenBranch: true));
                    }
                }
            }
        }
        // Handle 'x is None' pattern (x can be identifier or member access like self.field)
        else if (condition is BinaryOp { Operator: BinaryOperator.Is } isOp)
        {
            if (isOp.Right is NoneLiteral)
            {
                var narrowingKey = ExtractNarrowingKey(isOp.Left);
                if (narrowingKey != null && !isPositiveBranch)
                {
                    SemanticType? resolvedType = null;
                    if (isOp.Left is Identifier id)
                    {
                        var symbol = _symbolTable.Lookup(id.Name);
                        if (symbol is VariableSymbol varSymbol)
                            resolvedType = GetVariableType(varSymbol);
                    }
                    else
                    {
                        resolvedType = _semanticInfo.GetExpressionType(isOp.Left);
                    }

                    if (resolvedType is NullableType nullable)
                    {
                        narrowedTypes[narrowingKey] = nullable.UnderlyingType;
                        optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, nullable.UnderlyingType, IsValueTypeNullable: true, NarrowInThenBranch: false));
                    }
                    else if (resolvedType is OptionalType optional)
                    {
                        narrowedTypes[narrowingKey] = optional.UnderlyingType;
                        optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, optional.UnderlyingType, IsValueTypeNullable: false, NarrowInThenBranch: false));
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
                        // Check if the type is a builtin primitive first
                        var builtinType = typeId.Name switch
                        {
                            BuiltinNames.Int => SemanticType.Int,
                            BuiltinNames.Long => SemanticType.Long,
                            BuiltinNames.Float => SemanticType.Float,
                            BuiltinNames.Float32 => SemanticType.Float32,
                            BuiltinNames.Decimal => SemanticType.Decimal,
                            BuiltinNames.Double => SemanticType.Double,
                            BuiltinNames.Bool => SemanticType.Bool,
                            BuiltinNames.Str => SemanticType.Str,
                            _ => (SemanticType?)null
                        };

                        if (builtinType != null)
                        {
                            narrowedTypes[narrowingKey] = builtinType;
                            isInstanceNarrowings.Add(new IsInstanceNarrowing(narrowingKey, builtinType, NarrowInThenBranch: true));
                        }
                        else
                        {
                            var typeSymbol = _symbolTable.Lookup(typeId.Name) as TypeSymbol;
                            if (typeSymbol != null)
                            {
                                var narrowedType = new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                                narrowedTypes[narrowingKey] = narrowedType;
                                isInstanceNarrowings.Add(new IsInstanceNarrowing(narrowingKey, narrowedType, NarrowInThenBranch: true));
                            }
                        }
                    }
                }
            }
        }

        return (narrowedTypes, new NarrowingDecision(optionalNarrowings, isInstanceNarrowings));
    }

    /// <summary>
    /// Extract a key to use for type narrowing from an expression.
    /// Delegates to <see cref="AstHelper.ExtractNarrowingKey"/>.
    /// </summary>
    private string? ExtractNarrowingKey(Expression expr) => AstHelper.ExtractNarrowingKey(expr);

    /// <summary>
    /// Returns true if the given type contains any <see cref="TypeParameterType"/>
    /// (e.g., Iterator&lt;T&gt; contains T). Used during overload resolution to
    /// skip type-matching for generic parameters that C# will infer later.
    /// </summary>
    private static bool ContainsTypeParameter(SemanticType type)
    {
        return type switch
        {
            TypeParameterType => true,
            GenericType gt => gt.TypeArguments.Any(ContainsTypeParameter),
            NullableType nt => ContainsTypeParameter(nt.UnderlyingType),
            OptionalType ot => ContainsTypeParameter(ot.UnderlyingType),
            TupleType tt => tt.ElementTypes.Any(ContainsTypeParameter),
            FunctionType ft => ft.ParameterTypes.Any(ContainsTypeParameter) || ContainsTypeParameter(ft.ReturnType),
            _ => false
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

        // FunctionType is assignable to a delegate type if the signatures are compatible
        if (source is FunctionType sourceFt)
        {
            var delegateInvoke = TryGetDelegateInvokeMethod(target);
            if (delegateInvoke != null)
            {
                // Compare parameter counts
                if (sourceFt.ParameterTypes.Count != delegateInvoke.Parameters.Count)
                    return false;

                // Compare parameter types
                for (int i = 0; i < sourceFt.ParameterTypes.Count; i++)
                {
                    var invokeParamType = delegateInvoke.Parameters[i].Type;
                    if (!invokeParamType.IsAssignableTo(sourceFt.ParameterTypes[i])
                        && !sourceFt.ParameterTypes[i].IsAssignableTo(invokeParamType))
                        return false;
                }

                // Compare return types
                if (delegateInvoke.ReturnType is not VoidType && sourceFt.ReturnType is not VoidType)
                {
                    if (!sourceFt.ReturnType.IsAssignableTo(delegateInvoke.ReturnType)
                        && !IsAssignable(sourceFt.ReturnType, delegateInvoke.ReturnType))
                        return false;
                }

                return true;
            }
        }

        // Handle covariance for generic collection types (list, set)
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            if (sourceGeneric.Name == targetGeneric.Name &&
                sourceGeneric.TypeArguments.Count == targetGeneric.TypeArguments.Count)
            {
                // Check TypeSymbol metadata for covariance
                var sourceTypeSymbol = _symbolTable.BuiltinRegistry.GetType(sourceGeneric.Name);
                if (sourceTypeSymbol?.IsCovariant == true)
                {
                    return IsAssignable(sourceGeneric.TypeArguments[0], targetGeneric.TypeArguments[0]);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the Invoke method from a delegate type, substituting type parameters
    /// for generic delegates. Returns null if the type is not a delegate.
    /// </summary>
    private FunctionSymbol? TryGetDelegateInvokeMethod(SemanticType type)
    {
        TypeSymbol? delegateSymbol = null;
        List<SemanticType>? typeArgs = null;

        if (type is UserDefinedType { Symbol: { TypeKind: TypeKind.Delegate } udt })
        {
            delegateSymbol = udt;
        }
        else if (type is GenericType gt && gt.GenericDefinition is { TypeKind: TypeKind.Delegate })
        {
            delegateSymbol = gt.GenericDefinition;
            typeArgs = gt.TypeArguments;
        }

        if (delegateSymbol == null)
            return null;

        var invoke = delegateSymbol.Methods.FirstOrDefault(m => m.Name == "Invoke");
        if (invoke == null)
            return null;

        // For generic delegates, substitute type parameters in the Invoke signature
        if (typeArgs != null && delegateSymbol.TypeParameters.Count == typeArgs.Count)
        {
            var substitutions = new Dictionary<string, SemanticType>();
            for (int i = 0; i < delegateSymbol.TypeParameters.Count; i++)
            {
                substitutions[delegateSymbol.TypeParameters[i].Name] = typeArgs[i];
            }

            var substitutedParams = invoke.Parameters.Select(p => p with
            {
                Type = SubstituteTypeParametersInType(p.Type, substitutions)
            }).ToList();
            var substitutedReturn = SubstituteTypeParametersInType(invoke.ReturnType, substitutions);

            return invoke with
            {
                Parameters = substitutedParams,
                ReturnType = substitutedReturn
            };
        }

        return invoke;
    }

    /// <summary>
    /// Check if all types in a list are assignable to a target type.
    /// Used by contextual type inference for collection literals.
    /// </summary>
    private bool AllAssignableTo(List<SemanticType> types, SemanticType target)
    {
        return types.All(t => IsAssignable(t, target));
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
    /// Checks if a type contains any unresolved TypeParameterType instances.
    /// Used to detect method-level generic type parameters that need inference.
    /// </summary>
    /// <remarks>
    /// TODO(#414): This helper serves a different purpose than GenericTypeInferenceService.
    /// GenericTypeInferenceService.InferTypeArguments operates on FunctionSymbol (function-level
    /// generics with explicit TypeParameters), while this helper detects unresolved
    /// TypeParameterType placeholders in raw SemanticType trees (e.g., FunctionType from
    /// method resolution on generic classes). Unification into GenericTypeInferenceService
    /// would require adding an overload that accepts FunctionType instead of FunctionSymbol.
    /// </remarks>
    private static bool ContainsTypeParameterType(SemanticType type)
    {
        return type switch
        {
            TypeParameterType => true,
            ResultType rt => ContainsTypeParameterType(rt.OkType) || ContainsTypeParameterType(rt.ErrorType),
            OptionalType ot => ContainsTypeParameterType(ot.UnderlyingType),
            NullableType nt => ContainsTypeParameterType(nt.UnderlyingType),
            GenericType gt => gt.TypeArguments.Any(ContainsTypeParameterType),
            FunctionType ft => ft.ParameterTypes.Any(ContainsTypeParameterType) || ContainsTypeParameterType(ft.ReturnType),
            TupleType tt => tt.ElementTypes.Any(ContainsTypeParameterType),
            _ => false
        };
    }

    /// <summary>
    /// Collects mappings from TypeParameterType names to concrete types by structurally
    /// comparing a parameter type (with TypeParameterType placeholders) against an argument type
    /// (with concrete types). Used for method-level generic type parameter inference.
    /// </summary>
    /// <remarks>
    /// TODO(#414): This performs the same structural unification as
    /// GenericTypeInferenceService.Unify, but operates on raw SemanticType pairs rather than
    /// FunctionSymbol parameters. It is used by CheckLambdaCall where only a FunctionType is
    /// available (no FunctionSymbol). Unifying these two code paths requires adding a
    /// type-level inference API to GenericTypeInferenceService (e.g.,
    /// InferFromTypes(List&lt;SemanticType&gt; formalTypes, List&lt;SemanticType&gt; actualTypes)).
    /// </remarks>
    private static void CollectTypeParameterMappings(
        SemanticType paramType, SemanticType argType, Dictionary<string, SemanticType> map)
    {
        switch (paramType)
        {
            case TypeParameterType tpt:
                if (argType is not TypeParameterType and not UnknownType)
                    map[tpt.Name] = argType;
                break;
            case FunctionType paramFt when argType is FunctionType argFt:
                var minParams = Math.Min(paramFt.ParameterTypes.Count, argFt.ParameterTypes.Count);
                for (int i = 0; i < minParams; i++)
                    CollectTypeParameterMappings(paramFt.ParameterTypes[i], argFt.ParameterTypes[i], map);
                CollectTypeParameterMappings(paramFt.ReturnType, argFt.ReturnType, map);
                break;
            case GenericType paramGt when argType is GenericType argGt && paramGt.TypeArguments.Count == argGt.TypeArguments.Count:
                for (int i = 0; i < paramGt.TypeArguments.Count; i++)
                    CollectTypeParameterMappings(paramGt.TypeArguments[i], argGt.TypeArguments[i], map);
                break;
            case ResultType paramRt when argType is ResultType argRt:
                CollectTypeParameterMappings(paramRt.OkType, argRt.OkType, map);
                CollectTypeParameterMappings(paramRt.ErrorType, argRt.ErrorType, map);
                break;
            case OptionalType paramOt when argType is OptionalType argOt:
                CollectTypeParameterMappings(paramOt.UnderlyingType, argOt.UnderlyingType, map);
                break;
            case TupleType paramTt when argType is TupleType argTt && paramTt.ElementTypes.Count == argTt.ElementTypes.Count:
                for (int i = 0; i < paramTt.ElementTypes.Count; i++)
                    CollectTypeParameterMappings(paramTt.ElementTypes[i], argTt.ElementTypes[i], map);
                break;
        }
    }

    /// <summary>
    /// Applies a type parameter substitution map to a type.
    /// Delegates to SubstituteTypeParametersInType which handles all type forms.
    /// </summary>
    /// <remarks>
    /// TODO(#414): Thin wrapper around SubstituteTypeParametersInType, used only by
    /// CheckLambdaCall. If CollectTypeParameterMappings is unified into
    /// GenericTypeInferenceService, this method can be removed as well.
    /// </remarks>
    private SemanticType ApplyTypeParameterMap(
        SemanticType type, Dictionary<string, SemanticType> map)
    {
        return SubstituteTypeParametersInType(type, map);
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
            StarExpression star => IsValidAssignmentTarget(star.Operand),
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
    /// Extract element type from an iterable type.
    /// Delegates to <see cref="TypeInferenceService.InferIterableElementType"/>.
    /// </summary>
    private SemanticType ExtractElementType(SemanticType iterType)
        => _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

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
            code: DiagnosticCodes.Semantic.InvalidSuperUsage,
            span: superExpr.Span);
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
                code: DiagnosticCodes.Semantic.SuperOutsideClass,
                span: superExpr.Span);
            return SemanticType.Unknown;
        }

        // Check 2: Class must have a parent
        var classBaseType = GetBaseType(_currentClass);
        if (classBaseType == null)
        {
            AddError($"super() cannot be used in class '{_currentClass.Name}' which has no parent class",
                superExpr.LineStart, superExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.SuperNoParent,
                span: superExpr.Span);
            return SemanticType.Unknown;
        }

        // Check 3: Cannot access fields via super()
        // Check the entire inheritance chain for fields
        var currentType = classBaseType;
        while (currentType != null)
        {
            var field = currentType.Fields.FirstOrDefault(f => f.Name == memberName);
            if (field != null)
            {
                AddError("Cannot access parent fields via super(); only methods are allowed",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: memberAccess.Span);
                return SemanticType.Unknown;
            }
            currentType = GetBaseType(currentType);
        }

        // Check 4: Validate based on method context
        ValidateSuperContextRules(memberName, superExpr, memberAccess);

        // Look up the method in the parent class hierarchy and return its type
        // Use FindMethodInHierarchy to traverse the full inheritance chain
        var (parentMethod, methodOwner) = FindMethodInHierarchy(classBaseType, memberName);
        if (parentMethod == null && memberName == DunderNames.Init)
        {
            // __init__ might be in Constructors list - check full hierarchy
            currentType = classBaseType;
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
                currentType = GetBaseType(currentType);
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
            code: DiagnosticCodes.Semantic.UndefinedMember,
            span: memberAccess.Span);
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
                code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                span: superExpr.Span);
            return;
        }

        // Case 1: Inside __init__
        if (_currentMethodName == DunderNames.Init)
        {
            if (calledMethodName != DunderNames.Init)
            {
                AddError("super() in __init__ can only call super().__init__(...)",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: memberAccess.Span);
            }
            else if (_controlFlowDepth > 0)
            {
                AddError("super().__init__() must be the first statement in the constructor, not inside control flow",
                    superExpr.LineStart, superExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: superExpr.Span);
            }
            else if (_superInitCalled)
            {
                AddError("super().__init__() can only be called once",
                    superExpr.LineStart, superExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: superExpr.Span);
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
                        code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                        span: memberAccess.Span);
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
                    code: DiagnosticCodes.Semantic.InvalidSuperUsage,
                    span: memberAccess.Span);
            }
            return;
        }

        // Case 4: Regular method - super() not allowed
        AddError("super() cannot be used in regular methods; only in __init__, @override, or dunder methods",
            superExpr.LineStart, superExpr.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidSuperUsage,
            span: superExpr.Span);
    }

    /// <summary>
    /// Collect all interfaces a type implements, including:
    /// - Directly implemented interfaces
    /// - Base interfaces (interface inheritance)
    /// - Interfaces implemented by base classes
    /// </summary>
    private TypeSymbolSet CollectAllInterfaces(TypeSymbol type)
    {
        var all = TypeHierarchyService.GetAllInterfaces(type, SemanticBinding);
        var result = new TypeSymbolSet();
        foreach (var iface in all)
            result.Add(iface);
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
        => TypeHierarchyService.GetAncestorChain(type, SemanticBinding).ToList();

    /// <summary>
    /// Marks an expression as error recovery in SemanticInfo and increments the recovery counter.
    /// The counter enables transitive propagation: when a sub-expression is marked as error
    /// recovery, parent expressions that return Unknown can detect this and also mark themselves.
    /// Use this instead of calling <c>_semanticInfo.MarkErrorRecovery()</c> directly.
    /// </summary>
    private void MarkExpressionAsErrorRecovery(Expression expr)
    {
        _semanticInfo.MarkErrorRecovery(expr);
        _errorRecoveryMarkCount++;
    }

    /// <summary>
    /// Sets an expression's type to UnknownType and marks it as error recovery in SemanticInfo.
    /// Use this when the Unknown type is expected because a user-facing diagnostic was emitted.
    /// This allows the invariant checker to distinguish intentional error recovery from
    /// silent type inference failures (compiler bugs).
    /// </summary>
    private void SetErrorRecoveryType(Expression expr)
    {
        _semanticInfo.SetExpressionType(expr, SemanticType.Unknown);
        MarkExpressionAsErrorRecovery(expr);
    }

    /// <summary>
    /// Records a type-checking error. When the error relates to a relationship between
    /// two nodes (e.g., "type X is not assignable to type Y"), use the *target* node's
    /// span — that's where the user needs to fix the code.
    /// </summary>
    private void AddError(string message, int? line = null, int? column = null, string? code = null,
        Text.TextSpan? span = null)
    {
        if (_diagnostics.ErrorCount >= MaxErrors)
        {
            if (!_maxErrorsReported)
            {
                _maxErrorsReported = true;
                _diagnostics.AddWarning(
                    $"Too many errors ({MaxErrors}); further errors suppressed. Use '--max-errors' to increase the limit.",
                    line, column, _currentFilePath,
                    code: DiagnosticCodes.Infrastructure.TooManyErrors,
                    phase: CompilerPhase.TypeChecking);
                _logger.LogError("Maximum error count reached, stopping type checking", 0, 0);
            }
            if (!ContinueAfterError)
            {
                throw new SemanticAnalysisException("Type checking failed with too many errors");
            }
            return;
        }

        _diagnostics.AddPhaseError(message, CompilerPhase.TypeChecking,
            span, line, column, _currentFilePath, code, _logger);
    }

    /// <summary>
    /// Finds a "did you mean?" suggestion for an undefined identifier from visible symbols.
    /// </summary>
    private string? FindSuggestion(string name)
    {
        return EditDistance.FindClosestMatch(name, _symbolTable.GetVisibleSymbolNames());
    }

    /// <summary>
    /// Finds a "did you mean?" suggestion for an undefined member from a type's fields and methods,
    /// including inherited members from base classes and interfaces.
    /// </summary>
    private string? FindMemberSuggestion(string memberName, TypeSymbol typeSymbol)
    {
        var memberNames = new HashSet<string>();

        // Collect from the type itself and its base class chain
        var current = typeSymbol;
        while (current != null)
        {
            foreach (var f in current.Fields)
                memberNames.Add(f.Name);
            foreach (var m in current.Methods)
                memberNames.Add(m.Name);
            current = GetBaseType(current);
        }

        // Collect from interfaces
        foreach (var iface in GetInterfaces(typeSymbol))
        {
            foreach (var m in iface.Methods)
                memberNames.Add(m.Name);
        }

        return EditDistance.FindClosestMatch(memberName, memberNames);
    }

    /// <summary>
    /// Finds a "did you mean?" suggestion for an undefined module member.
    /// </summary>
    private string? FindModuleMemberSuggestion(string memberName, ModuleSymbol moduleSymbol)
    {
        return EditDistance.FindClosestMatch(memberName, moduleSymbol.Exports.Keys);
    }

    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Delegates to <see cref="AstHelper.TryGetConstantIntIndex"/>.
    /// </summary>
    private static bool TryGetConstantIntIndex(Expression expr, out int value)
        => AstHelper.TryGetConstantIntIndex(expr, out value);

    /// <summary>
    /// Walks the type hierarchy to find an event with the given name.
    /// </summary>
    private static EventSymbol? FindEventInHierarchy(TypeSymbol type, string eventName)
    {
        var current = type;
        while (current != null)
        {
            var evt = current.Events.FirstOrDefault(e => e.Name == eventName);
            if (evt != null)
                return evt;
            current = current.BaseType;
        }

        // Also check interfaces
        foreach (var ifaceRef in type.Interfaces)
        {
            var iface = ifaceRef.Definition;
            var evt = iface.Events.FirstOrDefault(e => e.Name == eventName);
            if (evt != null)
                return evt;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the given type (or its base types) declares an event with the given name.
    /// </summary>
    private static bool TypeHasEvent(TypeSymbol type, string eventName)
    {
        return FindEventInHierarchy(type, eventName) != null;
    }

    /// <summary>
    /// Resolves the owner type of an event member access expression.
    /// </summary>
    private TypeSymbol? ResolveEventOwner(MemberAccess memberAccess)
    {
        if (memberAccess.Object is Identifier objId)
        {
            if (objId.Name == PythonNames.Self && _currentClass != null)
                return _currentClass;

            var symbol = _symbolTable.Lookup(objId.Name);
            if (symbol is VariableSymbol varSym)
            {
                var varType = GetVariableType(varSym);
                if (varType is UserDefinedType udt)
                    return udt.Symbol;
            }
            else if (symbol is TypeSymbol ts)
            {
                return ts;
            }
        }
        return null;
    }

    /// <summary>
    /// Attempts to resolve a member access expression to an event symbol.
    /// Returns the EventSymbol if the member access refers to an event, null otherwise.
    /// Handles both self.event_name and obj.event_name patterns.
    /// </summary>
    private EventSymbol? TryResolveEventAccess(MemberAccess memberAccess)
    {
        // Resolve the object type to find the owning type
        TypeSymbol? owningType = null;

        if (memberAccess.Object is Identifier objId)
        {
            if (objId.Name == PythonNames.Self && _currentClass != null)
            {
                owningType = _currentClass;
            }
            else
            {
                var symbol = _symbolTable.Lookup(objId.Name);
                if (symbol is VariableSymbol varSym)
                {
                    var varType = GetVariableType(varSym);
                    if (varType is UserDefinedType udt)
                        owningType = udt.Symbol;
                }
                else if (symbol is TypeSymbol ts)
                {
                    owningType = ts;
                }
            }
        }

        if (owningType == null)
            return null;

        return FindEventInHierarchy(owningType, memberAccess.Member);
    }
}
