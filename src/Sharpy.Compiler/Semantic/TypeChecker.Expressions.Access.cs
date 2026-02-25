using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Member access, index access, function calls, lambdas, generics, tagged unions
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        // Check for super() usage - the parser directly produces SuperExpression for super()
        if (memberAccess.Object is SuperExpression superExpr)
        {
            return ValidateSuperMemberAccess(memberAccess, superExpr);
        }

        var objectType = CheckExpression(memberAccess.Object);

        // Check if this member access path has been narrowed by isinstance()
        // e.g., isinstance(self.animal, Dog) narrows "self.animal" to Dog
        var narrowingKey = ExtractNarrowingKey(memberAccess);
        if (narrowingKey != null)
        {
            var narrowedType = _narrowingContext.GetNarrowedType(narrowingKey);
            if (narrowedType != null)
            {
                _semanticInfo.SetNarrowedType(memberAccess, narrowedType);
                return narrowedType;
            }
        }

        // If object type is Unknown (e.g., from error recovery symbols), check if it's an
        // enum type access (Color.RED) before giving up. TypeSymbol identifiers resolve to
        // Unknown because they're not values, but enum member access IS a valid value expression.
        if (objectType is UnknownType)
        {
            // Check for enum type member access: Color.RED -> UserDefinedType(Color)
            if (memberAccess.Object is Identifier enumId)
            {
                var sym = _symbolTable.Lookup(enumId.Name);
                if (sym is TypeSymbol { TypeKind: TypeKind.Enum } enumTypeSym)
                {
                    return new UserDefinedType { Name = enumTypeSym.Name, Symbol = enumTypeSym };
                }
            }
            return SemanticType.Unknown;
        }

        // Handle null conditional access (?.)
        SemanticType memberLookupType = objectType;
        if (memberAccess.IsNullConditional)
        {
            // Null conditional can only be used on nullable/optional types
            if (objectType is NullableType nullableObjectType)
            {
                memberLookupType = nullableObjectType.UnderlyingType;
            }
            else if (objectType is OptionalType optionalObjectType)
            {
                memberLookupType = optionalObjectType.UnderlyingType;
            }
            else
            {
                AddError(
                    $"Null conditional operator '?.' can only be used on nullable types, but got '{objectType.GetDisplayName()}'",
                    memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.InvalidNullConditional,
                    span: memberAccess.Span);
                return SemanticType.Unknown;
            }
        }

        // Handle module member access (e.g., config.MAX_SIZE, utils.helper())
        if (memberLookupType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                var exportedType = exportedSymbol switch
                {
                    VariableSymbol varSymbol => GetVariableType(varSymbol),
                    FunctionSymbol funcSymbol => new FunctionType
                    {
                        ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                        ReturnType = funcSymbol.ReturnType
                    },
                    TypeSymbol typeSymbol => new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol },
                    ModuleSymbol nestedModule => new ModuleType { Symbol = nestedModule },
                    _ => SemanticType.Unknown
                };
                // Mark error recovery for unhandled symbol types in module exports
                // (e.g., TypeAliasSymbol) — these are resolved elsewhere, not a compiler bug.
                if (exportedType is UnknownType)
                    MarkExpressionAsErrorRecovery(memberAccess);
                return exportedType;
            }

            var moduleMemberMessage = $"Module '{moduleSymbol.Name}' has no member '{memberAccess.Member}'";
            var moduleMemberSuggestion = FindModuleMemberSuggestion(memberAccess.Member, moduleSymbol);
            if (moduleMemberSuggestion != null)
                moduleMemberMessage += $". Did you mean '{moduleMemberSuggestion}'?";
            AddError(moduleMemberMessage,
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
            return SemanticType.Unknown;
        }

        if (memberLookupType is UserDefinedType udt && udt.Symbol != null)
        {
            // Handle enum .name and .value properties
            if (udt.Symbol.TypeKind == TypeKind.Enum)
            {
                if (memberAccess.Member == "name")
                    return SemanticType.Str;
                if (memberAccess.Member == "value")
                    return SemanticType.Int;
            }

            // Look for field or property (including inherited fields)
            var (field, fieldOwner) = FindFieldInHierarchy(udt.Symbol, memberAccess.Member);
            if (field != null && fieldOwner != null)
            {
                // Access level validation is handled by AccessValidator in the validation pipeline

                var fieldType = GetVariableType(field);

                // Wrap result in optional/nullable for null conditional access
                if (memberAccess.IsNullConditional && fieldType is not NullableType and not OptionalType)
                {
                    // Use OptionalType when object is Optional, NullableType for C# nullable
                    if (objectType is OptionalType)
                        return new OptionalType { UnderlyingType = fieldType };
                    return new NullableType { UnderlyingType = fieldType };
                }
                return fieldType;
            }

            // Look for property (including inherited properties)
            var (prop, propOwner) = FindPropertyInHierarchy(udt.Symbol, memberAccess.Member);
            if (prop != null && propOwner != null)
            {
                var propType = prop.Type;
                if (propType is UnknownType && prop.HasGetter)
                {
                    // Property type not yet resolved; fallback to unknown
                    return propType;
                }

                // Wrap result in optional/nullable for null conditional access
                if (memberAccess.IsNullConditional && propType is not NullableType and not OptionalType)
                {
                    if (objectType is OptionalType)
                        return new OptionalType { UnderlyingType = propType };
                    return new NullableType { UnderlyingType = propType };
                }
                return propType;
            }

            // Look for method (including inherited methods)
            var (method, methodOwner) = FindMethodInHierarchy(udt.Symbol, memberAccess.Member);
            if (method != null && methodOwner != null)
            {
                // Access level validation is handled by AccessValidator in the validation pipeline

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

            var typeMemberMessage = $"Type '{memberLookupType.GetDisplayName()}' has no member '{memberAccess.Member}'";
            if (udt.Symbol != null)
            {
                var typeMemberSuggestion = FindMemberSuggestion(memberAccess.Member, udt.Symbol);
                if (typeMemberSuggestion != null)
                    typeMemberMessage += $". Did you mean '{typeMemberSuggestion}'?";
            }
            AddError(typeMemberMessage,
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
        }

        // Handle named tuple element access: pos.x, pos.y
        if (memberLookupType is TupleType tupleType && tupleType.IsNamed)
        {
            var names = tupleType.ElementNames!.Value;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == memberAccess.Member)
                {
                    return tupleType.ElementTypes[i];
                }
            }

            AddError(
                $"Named tuple type '{tupleType.GetDisplayName()}' has no element '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
            return SemanticType.Unknown;
        }

        // Intentional Unknown without error for non-UserDefinedType member access:
        // GenericType (list[T].append), BuiltinType (str.upper), TupleType, etc.
        // are resolved by the codegen layer through CLR member discovery, not the
        // type checker. Mark as error recovery to suppress SPY0907 false positives.
        MarkExpressionAsErrorRecovery(memberAccess);
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
        var current = GetBaseType(type);
        while (current != null)
        {
            field = current.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
                return (field, current);
            current = GetBaseType(current);
        }

        return (null, null);
    }

    /// <summary>
    /// Finds a property by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (PropertySymbol? Property, TypeSymbol? Owner) FindPropertyInHierarchy(TypeSymbol type, string propertyName)
    {
        // First check the type itself
        var prop = type.Properties.FirstOrDefault(p => p.Name == propertyName);
        if (prop != null)
            return (prop, type);

        // Check base class chain
        var current = GetBaseType(type);
        while (current != null)
        {
            prop = current.Properties.FirstOrDefault(p => p.Name == propertyName);
            if (prop != null)
                return (prop, current);
            current = GetBaseType(current);
        }

        // Check interfaces
        foreach (var iface in GetInterfaces(type))
        {
            prop = iface.Properties.FirstOrDefault(p => p.Name == propertyName);
            if (prop != null)
                return (prop, iface);
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
        var current = GetBaseType(type);
        while (current != null)
        {
            method = current.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, current);
            current = GetBaseType(current);
        }

        // Check interfaces (for method contracts)
        foreach (var iface in GetInterfaces(type))
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
        if (narrowingKey != null)
        {
            var narrowedType = _narrowingContext.GetNarrowedType(narrowingKey);
            if (narrowedType != null)
            {
                return narrowedType;
            }
        }

        // Special handling for generic type reference: Box[int] or Pair[int, str]
        // This is parsed as IndexAccess(Object: Box, Index: int or TupleLiteral)
        // When the object is a generic type and the index can be resolved as type(s),
        // this represents a generic type with type arguments, not an index operation
        if (indexAccess.Object is Identifier typeId)
        {
            var symbol = _symbolTable.Lookup(typeId.Name);

            // Handle generic type reference (e.g., Box[int])
            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Return a GenericType representing the instantiated type
                    return new GenericType
                    {
                        Name = genericTypeSymbol.Name,
                        TypeArguments = typeArgs,
                        GenericDefinition = genericTypeSymbol
                    };
                }
            }

            // Handle generic function reference (e.g., identity[int])
            // This creates a special "instantiated generic function" type for use in function calls
            if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Store the type arguments in SemanticInfo for use in CheckFunctionCall
                    _semanticInfo.SetExpressionType(indexAccess, new GenericFunctionType
                    {
                        FunctionSymbol = genericFuncSymbol,
                        TypeArguments = typeArgs
                    });
                    return _semanticInfo.GetExpressionType(indexAccess)!;
                }
            }
        }

        var objectType = CheckExpression(indexAccess.Object);
        var indexType = CheckExpression(indexAccess.Index);

        // Tuple positional indexing: validate constant integer indices at compile time
        if (objectType is TupleType tupleType && TryGetConstantIntIndex(indexAccess.Index, out var constIndex))
        {
            if (constIndex < 0)
            {
                AddError(
                    $"Negative index {constIndex} is not supported for tuple positional access",
                    indexAccess.Index.LineStart,
                    indexAccess.Index.ColumnStart,
                    DiagnosticCodes.Semantic.TupleNegativeIndex,
                    indexAccess.Index.Span);
                return SemanticType.Unknown;
            }

            if (constIndex >= tupleType.ElementTypes.Count)
            {
                AddError(
                    $"Tuple index {constIndex} is out of range for tuple with {tupleType.ElementTypes.Count} elements",
                    indexAccess.Index.LineStart,
                    indexAccess.Index.ColumnStart,
                    DiagnosticCodes.Semantic.TupleIndexOutOfRange,
                    indexAccess.Index.Span);
                return SemanticType.Unknown;
            }

            return tupleType.ElementTypes[constIndex];
        }

        // Use TypeInferenceService for type inference (errors reported by validator in pipeline)
        var resultType = _typeInference.InferIndexAccessType(objectType, indexType);

        // TypeInferenceService covers all supported operations - return Unknown for unsupported
        return resultType ?? SemanticType.Unknown;
    }

    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Handle None() — empty Optional constructor
        if (call.Function is NoneLiteral && call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
        {
            if (_expectedType is OptionalType)
            {
                return _expectedType;
            }
            else if (_expectedType != null)
            {
                AddError($"'None()' can only construct Optional types, not '{_expectedType.GetDisplayName()}'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidNoneConstructor,
                    span: call.Span);
                return SemanticType.Unknown;
            }
            else
            {
                AddError("Cannot infer type for 'None()' without a type annotation. Add a type annotation like 'x: int? = None()'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }

        // Check if this is a tagged union constructor shorthand (Some/Ok/Err)
        if (call.Function is Identifier constructorId && call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
        {
            var constructorResult = TryCheckTaggedUnionConstructor(constructorId, call);
            if (constructorResult != null)
                return constructorResult;
        }

        // Check if this is a null conditional method call (obj?.method())
        bool isNullConditionalCall = call.Function is MemberAccess { IsNullConditional: true };
        bool isOptionalNullConditional = false;

        // Check the called expression type first
        var calleeType = CheckExpression(call.Function);

        // After checking the callee, determine if this is ?. on an Optional object
        if (isNullConditionalCall && call.Function is MemberAccess nullCondMa)
        {
            var objType = _semanticInfo.GetExpressionType(nullCondMa.Object);
            isOptionalNullConditional = objType is OptionalType;
        }

        // Track super().__init__() calls AFTER validation completes
        // (do this after CheckExpression so the validation doesn't see it as already called)
        if (call.Function is MemberAccess ma && ma.Object is SuperExpression && ma.Member == DunderNames.Init)
        {
            _superInitCalled = true;
        }

        // Validate self.__init__() is only called inside a constructor
        if (call.Function is MemberAccess selfInitMa &&
            selfInitMa.Object is Identifier { Name: "self" } &&
            selfInitMa.Member == DunderNames.Init)
        {
            if (_currentMethodName != DunderNames.Init)
            {
                AddError("self.__init__() can only be called inside a constructor (__init__)",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.SelfInitOutsideConstructor,
                    span: call.Span);
            }
            else if (_superInitCalled)
            {
                AddError("Cannot use both super().__init__() and self.__init__() in the same constructor",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.ConflictingConstructorInitializers,
                    span: call.Span);
            }
        }

        // Try to resolve the function symbol early for constructor inference on arguments.
        // For simple identifier calls (foo(Some(42))), we can look up the function before
        // checking arguments, allowing _expectedType to be set per-parameter.
        FunctionSymbol? earlyFuncSymbol = null;
        int earlyParamOffset = 0; // offset to skip 'self' parameter for __init__ methods
        if (call.Function is Identifier earlyId)
        {
            var earlySymbol = _symbolTable.Lookup(earlyId.Name);
            if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)
            {
                // Only use early resolution for non-generic, non-overloaded functions.
                // Generic functions need argument types first for inference.
                // Overloaded builtins need argument types for resolution.
                var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(earlyId.Name);
                if (overloads == null || overloads.Count <= 1 || !overloads.Contains(fs))
                {
                    earlyFuncSymbol = fs;
                }
            }
            else if (earlySymbol is TypeSymbol ts && !ts.IsGeneric)
            {
                // Constructor call: Person(Some(42)) — look up __init__ for parameter types.
                // __init__ includes 'self' at index 0, but call arguments don't, so offset by 1.
                var initMethod = ts.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
                if (initMethod != null && !initMethod.IsGeneric)
                {
                    earlyFuncSymbol = initMethod;
                    earlyParamOffset = 1; // skip 'self' parameter
                }
            }
        }

        // Check arguments and collect their types
        // When we have an early function symbol or callee FunctionType, set _expectedType per-parameter
        // to enable constructor inference (Some/None()/Ok/Err) in function arguments.
        var calleeFunctionType = calleeType as FunctionType;
        var argTypes = new List<SemanticType>();
        for (int argIdx = 0; argIdx < call.Arguments.Length; argIdx++)
        {
            var previousExpectedType = _expectedType;

            // Handle spread arguments: *expr
            if (call.Arguments[argIdx] is SpreadElement spreadArg)
            {
                var spreadValueType = CheckExpression(spreadArg.Value);

                if (spreadValueType is TupleType tupleSpread)
                {
                    // Tuple spread: expand element types as individual arguments
                    argTypes.AddRange(tupleSpread.ElementTypes);
                }
                else
                {
                    // Iterable spread: extract element type for variadic param matching
                    var elemType = _typeInference.InferIterableElementType(spreadValueType);
                    if (elemType != null)
                        argTypes.Add(elemType);
                    else
                        argTypes.Add(SemanticType.Unknown);
                }
                _expectedType = previousExpectedType;
                continue;
            }

            if (earlyFuncSymbol != null && argIdx + earlyParamOffset < earlyFuncSymbol.Parameters.Count)
            {
                var paramType = earlyFuncSymbol.Parameters[argIdx + earlyParamOffset].Type;
                _expectedType = paramType is UnknownType ? null : paramType;
            }
            else if (calleeFunctionType != null && argIdx < calleeFunctionType.ParameterTypes.Count)
            {
                var paramType = calleeFunctionType.ParameterTypes[argIdx];
                _expectedType = paramType is UnknownType ? null : paramType;
            }
            argTypes.Add(CheckExpression(call.Arguments[argIdx]));
            _expectedType = previousExpectedType;
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            var previousExpectedType = _expectedType;
            if (earlyFuncSymbol != null)
            {
                var param = earlyFuncSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param != null)
                {
                    _expectedType = param.Type is UnknownType ? null : param.Type;
                }
            }
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
            _expectedType = previousExpectedType;
        }

        // Total argument count includes both positional and keyword arguments
        var totalArgCount = argTypes.Count + kwargTypes.Count;

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;

        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        var genericResult = CheckGenericInstantiation(call, calleeType);
        if (genericResult != null)
            return genericResult;

        if (call.Function is Identifier id)
        {
            // Special handling for builtin len() - validate that argument supports __len__ protocol
            if (id.Name == BuiltinNames.Len && argTypes.Count == 1)
            {
                // Use TypeInferenceService for type inference (errors reported by validator in pipeline)
                var lenType = _typeInference.InferLenType(argTypes[0]);

                // TypeInferenceService always returns Int for len() - return Unknown only if completely unsupported
                return lenType ?? SemanticType.Unknown;
            }

            // Special handling for builtin hash() - every object supports GetHashCode()
            if (id.Name == BuiltinNames.Hash && argTypes.Count == 1)
            {
                var hashType = _typeInference.InferHashType(argTypes[0]);
                return hashType ?? SemanticType.Unknown;
            }

            // reversed(iterable) -> Iterator<T>
            if (id.Name == BuiltinNames.Reversed && argTypes.Count == 1)
            {
                var elementType = _typeInference.InferReversedElementType(argTypes[0]);
                if (elementType != null)
                    return new GenericType { Name = BuiltinNames.Iterator, TypeArguments = new List<SemanticType> { elementType } };
            }

            // sorted(iterable, ...) -> list<T>
            if (id.Name == BuiltinNames.Sorted && argTypes.Count >= 1)
            {
                var elementType = _typeInference.InferIterableElementType(argTypes[0]);
                if (elementType != null)
                    return new GenericType { Name = BuiltinNames.List, TypeArguments = new List<SemanticType> { elementType } };
            }

            var symbol = _symbolTable.Lookup(id.Name);

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
                // For primitive types (int, float, str, bool, long, etc.), route to builtin function overloads
                // instead of treating as constructor. This matches Python semantics where int(x) calls
                // the int conversion function, not constructs a new int object.
                var primitiveOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                if (primitiveOverloads != null && primitiveOverloads.Count > 0 && PrimitiveCatalog.IsPrimitive(id.Name))
                {
                    // Route to builtin function overload resolution below
                    // (fall through to overload handling)
                }
                else
                {
                    // SPY0357: Check for iterable spread into non-variadic constructor
                    var initMethod = typeSymbol.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
                    if (initMethod != null)
                    {
                        var initParams = initMethod.Parameters.Skip(1).ToList(); // skip 'self'
                        if (CheckSpreadIntoNonVariadic(call, typeSymbol.Name, initParams))
                            return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                    }

                    // Cannot instantiate abstract classes
                    if (typeSymbol.IsAbstract)
                    {
                        AddError($"Cannot instantiate abstract class '{typeSymbol.Name}'",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                            span: call.Span);
                        return SemanticType.Unknown;
                    }

                    // For generic types called without type arguments (e.g., set()),
                    // infer type arguments from the expected type annotation if available,
                    // otherwise emit a diagnostic for empty constructors or fall back to
                    // UnknownType args for wildcard matching.
                    if (typeSymbol.IsGeneric)
                    {
                        List<SemanticType>? typeArgs = null;
                        if (_expectedType is GenericType expectedGeneric
                            && expectedGeneric.Name == typeSymbol.Name
                            && expectedGeneric.TypeArguments.Count == typeSymbol.TypeParameters.Count)
                        {
                            typeArgs = expectedGeneric.TypeArguments;
                        }
                        else if (call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
                        {
                            // Empty generic constructor with no type annotation — cannot infer type args
                            AddError($"Cannot infer type of empty {typeSymbol.Name} constructor; add a type annotation (e.g., x: {typeSymbol.Name}[...] = {typeSymbol.Name}())",
                                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                                span: call.Span);
                            return SemanticType.Unknown;
                        }
                        else if (call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
                        {
                            // Single-argument constructor: try to infer type args from iterable argument type
                            var argType = argTypes.Count > 0 ? argTypes[0] : null;
                            if (argType != null && argType != SemanticType.Unknown)
                            {
                                var elementType = _typeInference.InferIterableElementType(argType);
                                if (elementType != null && elementType != SemanticType.Unknown)
                                {
                                    if (typeSymbol.Name is BuiltinNames.List or BuiltinNames.Set
                                        && typeSymbol.TypeParameters.Count == 1)
                                    {
                                        typeArgs = new List<SemanticType> { elementType };
                                    }
                                    else if (typeSymbol.Name == BuiltinNames.Dict
                                             && typeSymbol.TypeParameters.Count == 2
                                             && elementType is TupleType tt && tt.ElementTypes.Count == 2)
                                    {
                                        typeArgs = new List<SemanticType> { tt.ElementTypes[0], tt.ElementTypes[1] };
                                    }
                                }
                            }

                            // Fall through to Unknown if inference failed
                            typeArgs ??= Enumerable.Range(0, typeSymbol.TypeParameters.Count)
                                .Select(_ => (SemanticType)SemanticType.Unknown)
                                .ToList();
                        }
                        else
                        {
                            // Multiple arguments or keyword arguments: cannot infer type args
                            typeArgs = Enumerable.Range(0, typeSymbol.TypeParameters.Count)
                                .Select(_ => (SemanticType)SemanticType.Unknown)
                                .ToList();
                        }
                        return new GenericType
                        {
                            Name = typeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = typeSymbol
                        };
                    }

                    // Constructor call returns an instance of the type
                    return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                }
            }

            funcSymbol = symbol as FunctionSymbol;

            // If we found a symbol but it's not a function or type, it's not callable
            // UNLESS it's a variable with a FunctionType (e.g., a parameter with type (T) -> U)
            if (symbol != null && funcSymbol == null && symbol is not TypeSymbol)
            {
                // Check if it's an error recovery symbol - suppress cascading errors
                if (symbol.IsErrorRecovery)
                {
                    return SemanticType.Unknown;
                }

                // Check if it's a variable with a FunctionType - those are callable
                if (symbol is VariableSymbol varSym && GetVariableType(varSym) is FunctionType)
                {
                    // Let the FunctionType handling below deal with this
                }
                else
                {
                    AddError($"'{id.Name}' is not callable (type: {calleeType.GetDisplayName()})",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                        span: call.Function.Span);
                    return SemanticType.Unknown;
                }
            }

            // Special handling for builtin functions with overloads
            var overloadResult = ResolveBuiltinOverload(id, argTypes, totalArgCount, call);
            if (overloadResult != null)
                return overloadResult;
        }
        // Handle member access function calls (e.g., module.function() or obj.method())
        // Skip super() calls - they're already validated by ValidateSuperMemberAccess
        else if (call.Function is MemberAccess memberAccessCall && memberAccessCall.Object is not SuperExpression)
        {
            funcSymbol = ResolveFunctionSymbolFromMemberAccess(memberAccessCall);

            // Try user-defined method overloads: either when no symbol was found,
            // or when the found symbol's method has multiple overloads on the owning type
            {
                var overloadResult = ResolveUserMethodOverload(
                    memberAccessCall, argTypes, totalArgCount, call,
                    isNullConditionalCall, isOptionalNullConditional);
                if (overloadResult != null)
                    return overloadResult;
            }
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            return ValidateFunctionSymbolCall(call, funcSymbol, argTypes, kwargTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // Fallback to FunctionType validation (no default parameter support)
        // Use the already-computed calleeType to avoid re-evaluating call.Function
        // (which causes double validation, e.g., super().__init__() being flagged as duplicate)
        if (calleeType is FunctionType ft)
        {
            return CheckLambdaCall(call, ft, argTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // If callee type is Unknown, this is error recovery from a sub-expression
        // (covered by transitive error recovery tracking in CheckExpression).
        // Otherwise, the callee evaluated to a non-callable type — emit an error.
        if (calleeType is not UnknownType)
        {
            AddError($"Expression of type '{calleeType.GetDisplayName()}' is not callable",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: call.Function.Span);
        }
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Handles generic type instantiation (Box[int](42)) and generic function calls (identity[int](42)).
    /// Returns null if the call is not a generic instantiation.
    /// </summary>
    private SemanticType? CheckGenericInstantiation(FunctionCall call, SemanticType calleeType)
    {
        // Special handling for generic type instantiation: Box[int](42) or Pair[int, str](1, "a")
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box, Index: int or TupleLiteral), Arguments: [...])
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericTypeId &&
            _symbolTable.Lookup(genericTypeId.Name) is TypeSymbol genericTypeSymbol &&
            genericTypeSymbol.IsGeneric)
        {
            // The "index" is actually type argument(s) - try to resolve them as types
            var typeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (typeArgs != null)
            {
                // SPY0357: Check for iterable spread into non-variadic generic constructor
                var initMethod = genericTypeSymbol.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
                if (initMethod != null)
                {
                    var initParams = initMethod.Parameters.Skip(1).ToList();
                    if (CheckSpreadIntoNonVariadic(call, genericTypeSymbol.Name, initParams))
                        return new GenericType
                        {
                            Name = genericTypeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = genericTypeSymbol
                        };
                }

                // Cannot instantiate abstract classes
                if (genericTypeSymbol.IsAbstract)
                {
                    AddError($"Cannot instantiate abstract class '{genericTypeSymbol.Name}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                        span: call.Span);
                    return SemanticType.Unknown;
                }

                // Return a GenericType with the type arguments
                return new GenericType
                {
                    Name = genericTypeSymbol.Name,
                    TypeArguments = typeArgs,
                    GenericDefinition = genericTypeSymbol
                };
            }
        }

        // Handle generic function call: identity[int](42)
        // The calleeType will be GenericFunctionType from CheckIndexAccess
        if (calleeType is GenericFunctionType genericFuncType)
        {
            // Substitute type parameters with type arguments in the return type
            var substitutedReturnType = SubstituteTypeParameters(
                genericFuncType.FunctionSymbol.ReturnType,
                genericFuncType.FunctionSymbol.TypeParameters,
                genericFuncType.TypeArguments);
            return substitutedReturnType;
        }

        return null;
    }

    /// <summary>
    /// Resolves builtin function overloads for a call. Returns the resolved return type,
    /// or null if no overload resolution is needed.
    /// </summary>
    private SemanticType? ResolveBuiltinOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        // When there are multiple overloads, we need to perform overload resolution to find the right one.
        // The funcSymbol from Lookup is just the first overload, which may not match the call.
        // Only use builtin overloads if there's no user-defined function shadowing the builtin.
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
        var isBuiltinWithOverloads = overloads != null && overloads.Count > 1;
        var funcSymbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        // If funcSymbol was found in symbol table AND it's one of the builtin overloads, use overload resolution
        var needsOverloadResolution = isBuiltinWithOverloads &&
            (funcSymbol == null || (funcSymbol != null && overloads!.Contains(funcSymbol)));
        if (!needsOverloadResolution)
            return null;

        // First pass: filter by argument count (considering default parameters and variadic parameters)
        var candidateOverloads = overloads!.Where(o =>
        {
            var requiredParams = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count;
            // Variadic functions can accept any number of arguments >= required
            if (hasVariadic)
                return totalArgCount >= requiredParams;
            return totalArgCount >= requiredParams && totalArgCount <= totalParams;
        }).ToList();

        // Second pass: check type compatibility
        FunctionSymbol? matchingOverload = null;
        foreach (var overload in candidateOverloads)
        {
            bool typesMatch = true;
            var variadicParam = overload.Parameters.FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < argTypes.Count; i++)
            {
                SemanticType expectedType;
                if (i < overload.Parameters.Count && !overload.Parameters[i].IsVariadic)
                {
                    // Regular parameter
                    expectedType = overload.Parameters[i].Type;
                }
                else if (variadicParam != null)
                {
                    // Variadic parameter - all remaining args must match the element type
                    expectedType = variadicParam.Type;
                }
                else
                {
                    // Index out of bounds - shouldn't happen with valid candidates
                    typesMatch = false;
                    break;
                }

                if (!IsAssignable(argTypes[i], expectedType))
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

        // No matching overload found
        var expectedCounts = string.Join(" or ", overloads!.Select(o =>
        {
            var required = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var total = o.Parameters.Count;
            var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
            if (hasVariadic)
                return $"{required}+";
            return required == total ? total.ToString() : $"{required}-{total}";
        }).Distinct());
        AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {totalArgCount}",
            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
            span: call.Span);
        return SemanticType.Unknown;
    }

    // TODO(#205): Add language spec for method overloading (docs/language_specification/method_overloading.md)
    // TODO(#207): Add test fixtures for ambiguous overloads and overloads with default parameters
    /// <summary>
    /// Resolves a user-defined method overload from a member access call (e.g., obj.method(args)).
    /// Returns the resolved return type when the method has multiple overloads, null if not applicable.
    /// Handles the complete call validation including argument type checking.
    /// </summary>
    private SemanticType? ResolveUserMethodOverload(
        MemberAccess memberAccess, List<SemanticType> argTypes, int totalArgCount, FunctionCall call,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType is not UserDefinedType { Symbol: { } typeSymbol })
            return null;

        // Walk the hierarchy looking for overloads
        var overloads = FindMethodOverloadsInHierarchy(typeSymbol, memberAccess.Member);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // SPY0357: Check for iterable spread into non-variadic overloaded method.
        // Must run before argument count filtering, since spread collapses N args into 1.
        var anyOverloadVariadic = overloads.Any(o => o.Parameters.Any(p => p.IsVariadic));
        if (!anyOverloadVariadic)
        {
            for (int i = 0; i < call.Arguments.Length; i++)
            {
                if (call.Arguments[i] is SpreadElement spreadElem)
                {
                    var spreadType = _semanticInfo.GetExpressionType(spreadElem.Value);
                    if (spreadType is not null and not UnknownType and not TupleType)
                    {
                        AddError(
                            $"Cannot spread '{spreadType.GetDisplayName()}' into non-variadic function '{memberAccess.Member}'; " +
                            "use a function with *args parameter or pass arguments individually",
                            spreadElem.LineStart, spreadElem.ColumnStart,
                            code: DiagnosticCodes.Semantic.SpreadIntoNonVariadic,
                            span: spreadElem.Span);
                        return SemanticType.Unknown;
                    }
                }
            }
        }

        // First pass: filter by argument count (skip 'self' parameter)
        var candidates = overloads.Where(o =>
        {
            var selfOffset = o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;
            var requiredParams = o.Parameters.Skip(selfOffset).Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Skip(selfOffset).Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count - selfOffset;
            if (hasVariadic)
                return totalArgCount >= requiredParams;
            return totalArgCount >= requiredParams && totalArgCount <= totalParams;
        }).ToList();

        // Second pass: check type compatibility
        FunctionSymbol? matchingOverload = null;
        int matchCount = 0;
        foreach (var overload in candidates)
        {
            var selfOffset = overload.Parameters.Count > 0 && overload.Parameters[0].Name == PythonNames.Self ? 1 : 0;
            bool typesMatch = true;
            var variadicParam = overload.Parameters.Skip(selfOffset).FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < argTypes.Count; i++)
            {
                SemanticType expectedType;
                var paramIdx = i + selfOffset;
                if (paramIdx < overload.Parameters.Count && !overload.Parameters[paramIdx].IsVariadic)
                {
                    expectedType = overload.Parameters[paramIdx].Type;
                }
                else if (variadicParam != null)
                {
                    expectedType = variadicParam.Type;
                }
                else
                {
                    typesMatch = false;
                    break;
                }

                if (expectedType is not UnknownType && argTypes[i] is not UnknownType
                    && !IsAssignable(argTypes[i], expectedType))
                {
                    typesMatch = false;
                    break;
                }
            }
            if (typesMatch)
            {
                matchingOverload = overload;
                matchCount++;
            }
        }

        if (matchCount > 1)
        {
            AddError($"Ambiguous call to overloaded method '{memberAccess.Member}' — multiple overloads match the argument types",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AmbiguousOverload,
                span: call.Span);
            return SemanticType.Unknown;
        }

        if (matchingOverload == null)
        {
            if (candidates.Count == 0)
            {
                AddError($"No matching overload for '{memberAccess.Member}' with {totalArgCount} argument(s)",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                    span: call.Span);
            }
            else
            {
                // Candidates matched by arity but not by type
                AddError($"No matching overload for '{memberAccess.Member}' with the given argument types",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                    span: call.Span);
            }
            return SemanticType.Unknown;
        }

        var returnType = matchingOverload.ReturnType;
        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Finds all overloads for a method name walking the type hierarchy.
    /// Returns null if no overloads are found.
    /// </summary>
    private List<FunctionSymbol>? FindMethodOverloadsInHierarchy(TypeSymbol type, string methodName)
    {
        // Check the type itself
        if (type.MethodOverloads.TryGetValue(methodName, out var overloads) && overloads.Count > 0)
            return overloads;

        // Check base class chain
        var current = GetBaseType(type);
        while (current != null)
        {
            if (current.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads;
            current = GetBaseType(current);
        }

        return null;
    }

    /// <summary>
    /// Resolves a FunctionSymbol from a member access expression (e.g., module.function()).
    /// Returns null if the member does not resolve to a FunctionSymbol.
    /// </summary>
    private FunctionSymbol? ResolveFunctionSymbolFromMemberAccess(MemberAccess memberAccess)
    {
        // Re-evaluate the object to get the module, then lookup the member.
        // This is duplicate work but necessary until we refactor to store symbols in SemanticInfo.
        var objectType = CheckExpression(memberAccess.Object);
        if (objectType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                return exportedSymbol as FunctionSymbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks for iterable spread arguments in a call to a non-variadic target.
    /// Returns true if a violation was found and a diagnostic was emitted.
    /// TupleType spreads are excluded because their size is statically known.
    /// </summary>
    private bool CheckSpreadIntoNonVariadic(
        FunctionCall call, string targetName, IReadOnlyList<ParameterSymbol>? parameters)
    {
        if (parameters == null)
            return false;

        var hasVariadicParam = parameters.Any(p => p.IsVariadic);
        if (hasVariadicParam)
            return false;

        for (int i = 0; i < call.Arguments.Length; i++)
        {
            if (call.Arguments[i] is SpreadElement spreadElem)
            {
                var spreadType = _semanticInfo.GetExpressionType(spreadElem.Value);
                if (spreadType is not null and not UnknownType and not TupleType)
                {
                    AddError(
                        $"Cannot spread '{spreadType.GetDisplayName()}' into non-variadic function '{targetName}'; " +
                        "use a function with *args parameter or pass arguments individually",
                        spreadElem.LineStart, spreadElem.ColumnStart,
                        code: DiagnosticCodes.Semantic.SpreadIntoNonVariadic,
                        span: spreadElem.Span);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Validates a function call against a resolved FunctionSymbol, including generic inference,
    /// argument count, positional/keyword argument type checking.
    /// </summary>
    private SemanticType ValidateFunctionSymbolCall(
        FunctionCall call, FunctionSymbol funcSymbol,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // Check for iterable spread into non-variadic function (SPY0357)
        // Must run before generic inference — generic functions without *args must also reject
        // iterable spread. Tuple spread is excluded because tuple size is statically known.
        if (CheckSpreadIntoNonVariadic(call, funcSymbol.Name, funcSymbol.Parameters))
        {
            var earlyReturn = funcSymbol.ReturnType;
            if (isNullConditionalCall && earlyReturn is not NullableType and not OptionalType)
            {
                if (isOptionalNullConditional)
                    return new OptionalType { UnderlyingType = earlyReturn };
                return new NullableType { UnderlyingType = earlyReturn };
            }
            return earlyReturn;
        }

        // Handle generic function inference: identity(42) -> infer T=int
        // This is triggered when calling a generic function without explicit type arguments
        if (funcSymbol.IsGeneric)
        {
            var inferenceResult = _genericInference.InferTypeArguments(funcSymbol, argTypes);
            if (inferenceResult.Success && inferenceResult.InferredTypes != null)
            {
                // Inference succeeded - substitute type parameters and return the result
                var substitutedReturnType = SubstituteTypeParameters(
                    funcSymbol.ReturnType,
                    funcSymbol.TypeParameters,
                    inferenceResult.InferredTypes);

                // Store the inferred type arguments for codegen
                _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);

                // Wrap result in optional/nullable for null conditional calls
                if (isNullConditionalCall && substitutedReturnType is not NullableType and not OptionalType)
                {
                    if (isOptionalNullConditional)
                        return new OptionalType { UnderlyingType = substitutedReturnType };
                    return new NullableType { UnderlyingType = substitutedReturnType };
                }
                return substitutedReturnType;
            }
            else
            {
                // Inference failed - report error
                AddError(inferenceResult.ErrorMessage ?? "Type arguments cannot be inferred",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferGenericType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }

        // Count required parameters (those without defaults)
        var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
        var totalParamCount = funcSymbol.Parameters.Count;

        // Validate argument count considering defaults (include both positional and keyword args)
        if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
        {
            if (requiredParamCount == totalParamCount)
            {
                AddError($"Function expects {totalParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else
            {
                AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
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
                        call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[i].Span);
                }
            }

            // Validate keyword arguments
            foreach (var kwarg in call.KeywordArguments)
            {
                var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param == null)
                {
                    AddError($"Unknown keyword argument '{kwarg.Name}'",
                        kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                        span: kwarg.Value.Span);
                }
                else
                {
                    // Check if this parameter was already provided positionally
                    var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                    if (paramIndex < argTypes.Count)
                    {
                        AddError($"Argument '{kwarg.Name}' was already provided positionally",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                            span: kwarg.Value.Span);
                    }
                    else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                    {
                        AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: kwarg.Value.Span);
                    }
                }
            }
        }

        var returnType = funcSymbol.ReturnType;

        // Wrap result in optional/nullable for null conditional calls
        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Validates a function call against a FunctionType (lambda/delegate calls without a FunctionSymbol).
    /// </summary>
    private SemanticType CheckLambdaCall(
        FunctionCall call, FunctionType ft, List<SemanticType> argTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // Skip validation for .NET types with multiple constructor overloads
        // (C# compiler will handle overload resolution)
        if (!ft.SkipArgumentValidation)
        {
            // Validate argument count (include both positional and keyword arguments)
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else
            {
                // Validate positional argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], ft.ParameterTypes[i]))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{ft.ParameterTypes[i].GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
                }
            }
        }

        var returnType = ft.ReturnType;

        // Wrap result in optional/nullable for null conditional calls
        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Tries to resolve an expression as a type. This is used for generic type instantiation
    /// where Box[int](42) parses the type argument as an expression.
    /// Returns null if the expression cannot be interpreted as a type.
    /// </summary>
    private SemanticType? TryResolveExpressionAsType(Expression expr)
    {
        // Handle simple identifier as type name (e.g., "int", "str", "MyClass")
        if (expr is Identifier typeId)
        {
            // Create a synthetic type annotation and resolve it
            var typeAnnotation = new Parser.Ast.TypeAnnotation
            {
                Name = typeId.Name,
                LineStart = expr.LineStart,
                ColumnStart = expr.ColumnStart
            };
            var resolved = _typeResolver.ResolveTypeAnnotation(typeAnnotation);
            return resolved != SemanticType.Unknown ? resolved : null;
        }

        // Handle nested generic types (e.g., Box[int] in Container[Box[int]])
        if (expr is IndexAccess indexAccess &&
            indexAccess.Object is Identifier nestedTypeId &&
            _symbolTable.Lookup(nestedTypeId.Name) is TypeSymbol nestedGenericType &&
            nestedGenericType.IsGeneric)
        {
            var nestedTypeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (nestedTypeArgs != null)
            {
                return new GenericType
                {
                    Name = nestedGenericType.Name,
                    TypeArguments = nestedTypeArgs,
                    GenericDefinition = nestedGenericType
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to resolve one or more type arguments from an index expression.
    /// Handles both single type arguments (int) and multiple type arguments (int, str as TupleLiteral).
    /// Returns null if the expressions cannot be interpreted as types.
    /// </summary>
    private List<SemanticType>? TryResolveTypeArguments(Expression indexExpr)
    {
        var typeArgs = new List<SemanticType>();

        // Handle multiple type arguments: Pair[int, str] parses as TupleLiteral
        if (indexExpr is TupleLiteral tuple)
        {
            foreach (var element in tuple.Elements)
            {
                var typeArg = TryResolveExpressionAsType(element);
                if (typeArg == null)
                    return null;
                typeArgs.Add(typeArg);
            }
            return typeArgs;
        }

        // Handle single type argument
        var singleTypeArg = TryResolveExpressionAsType(indexExpr);
        if (singleTypeArg == null)
            return null;
        typeArgs.Add(singleTypeArg);
        return typeArgs;
    }

    /// <summary>
    /// Tries to check a function call as a tagged union constructor (Some/Ok/Err).
    /// Returns the resolved type if successful, or null if this is not a constructor call.
    /// </summary>
    private SemanticType? TryCheckTaggedUnionConstructor(Identifier constructorId, FunctionCall call)
    {
        var name = constructorId.Name;

        if (name == "Some")
        {
            if (_expectedType is OptionalType opt)
            {
                var argType = CheckExpression(call.Arguments[0]);
                if (!IsAssignable(argType, opt.UnderlyingType))
                {
                    AddError($"Argument type '{argType.GetDisplayName()}' is not compatible with Optional underlying type '{opt.UnderlyingType.GetDisplayName()}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[0].Span);
                }
                return _expectedType;
            }
            else if (_expectedType == null && _symbolTable.Lookup("Some") == null)
            {
                // No expected type and no user-defined 'Some' — error
                AddError("Cannot infer type for 'Some()' without a type annotation. Add a type annotation like 'x: int? = Some(value)'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                // Still check the argument to avoid cascading errors
                CheckExpression(call.Arguments[0]);
                return SemanticType.Unknown;
            }
            // Fall through to normal function call if there's a user-defined 'Some' or expectedType is not OptionalType
        }

        if (name == "Ok")
        {
            if (_expectedType is ResultType result)
            {
                var argType = CheckExpression(call.Arguments[0]);
                if (!IsAssignable(argType, result.OkType))
                {
                    AddError($"Argument type '{argType.GetDisplayName()}' is not compatible with Result Ok type '{result.OkType.GetDisplayName()}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[0].Span);
                }
                return _expectedType;
            }
            else if (_expectedType == null && _symbolTable.Lookup("Ok") == null)
            {
                AddError("Cannot infer type for 'Ok()' without a type annotation. Add a type annotation like 'x: int !str = Ok(value)'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                CheckExpression(call.Arguments[0]);
                return SemanticType.Unknown;
            }
        }

        if (name == "Err")
        {
            if (_expectedType is ResultType result)
            {
                var argType = CheckExpression(call.Arguments[0]);
                if (!IsAssignable(argType, result.ErrorType))
                {
                    AddError($"Argument type '{argType.GetDisplayName()}' is not compatible with Result Error type '{result.ErrorType.GetDisplayName()}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[0].Span);
                }
                return _expectedType;
            }
            else if (_expectedType == null && _symbolTable.Lookup("Err") == null)
            {
                AddError("Cannot infer type for 'Err()' without a type annotation. Add a type annotation like 'x: int !str = Err(error)'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                CheckExpression(call.Arguments[0]);
                return SemanticType.Unknown;
            }
        }

        return null; // Not a tagged union constructor — fall through to normal handling
    }

    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        // Use _expectedType for bidirectional type inference: if the context expects
        // a FunctionType, extract parameter types from it to infer lambda parameter types.
        FunctionType? expectedFunc = _expectedType as FunctionType;

        var paramTypes = new List<SemanticType>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            if (param.Type != null)
            {
                // Explicit type annotation — use it
                paramTypes.Add(_typeResolver.ResolveTypeAnnotation(param.Type));
            }
            else if (expectedFunc != null && i < expectedFunc.ParameterTypes.Count)
            {
                // Infer from expected function type context
                paramTypes.Add(expectedFunc.ParameterTypes[i]);
            }
            else
            {
                paramTypes.Add(SemanticType.Unknown);
            }
        }

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        // Enter an isolated narrowing scope for this lambda.
        // Type narrowings from the enclosing scope should NOT be visible inside the lambda,
        // because lambdas can be stored and called later when the narrowing condition no longer holds.
        // This is the same logic as for nested function definitions (task 1.7).
        using var _ = _narrowingContext.EnterIsolatedScope();

        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var paramSymbol = new VariableSymbol
            {
                Name = lambda.Parameters[i].Name,
                Kind = SymbolKind.Parameter,
                Type = paramTypes[i],
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
}
