using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Member access, index access, type resolution, tagged unions
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

        // Resolve type-name member access (int.parse(), Color.RED, MyClass.FIELD, Shape.Circle)
        // regardless of what CheckIdentifier returned for the type name. This handles cases
        // where primitive TypeSymbols return FunctionType instead of Unknown (#432).
        if (memberAccess.Object is Identifier typeId
            && _semanticInfo.GetIdentifierSymbol(typeId) is TypeSymbol typeSym)
        {
            var resolved = TryResolveTypeMemberAccess(memberAccess, typeId, typeSym);
            if (resolved != null)
                return resolved;
        }

        if (objectType is UnknownType)
        {
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
            var memberName = memberAccess.Member;

            // For .NET modules, try PascalCase conversion if the exact name isn't found
            // (e.g., system.console -> System.Console)
            if (!moduleSymbol.Exports.ContainsKey(memberName) && moduleSymbol.IsNetModule)
            {
                var pascalName = NameMangler.ToPascalCase(memberName);
                if (moduleSymbol.Exports.ContainsKey(pascalName))
                    memberName = pascalName;
            }

            if (moduleSymbol.Exports.TryGetValue(memberName, out var exportedSymbol))
            {
                var exportedType = exportedSymbol switch
                {
                    VariableSymbol varSymbol => GetVariableType(varSymbol),
                    FunctionSymbol funcSymbol => new FunctionType
                    {
                        ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                        ReturnType = funcSymbol.ReturnType,
                        VariadicParameterIndex = GetVariadicIndex(funcSymbol.Parameters)
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
            // CLR-backed TypeSymbols may have Methods/Properties populated by CachedModuleDiscovery
            // (with snake_case names via ReverseNameMangler). Attempt member resolution here;
            // fall back to codegen only if the type has no discovered members at all.
            if (udt.Symbol.ClrType != null)
            {
                var clrMemberName = memberAccess.Member;

                // Check properties (is_completed, result, etc.)
                var clrProp = udt.Symbol.Properties.FirstOrDefault(p => p.Name == clrMemberName);
                if (clrProp != null)
                    return clrProp.Type;

                // Check methods
                var clrMethod = udt.Symbol.Methods.FirstOrDefault(m => m.Name == clrMemberName);
                if (clrMethod != null)
                {
                    var clrParameters = clrMethod.Parameters
                        .Where(p => p.Name != "self")
                        .ToList();
                    var paramTypes = clrParameters
                        .Select(p => p.Type ?? SemanticType.Unknown)
                        .ToList();
                    return new FunctionType
                    {
                        ParameterTypes = paramTypes,
                        ReturnType = clrMethod.ReturnType ?? SemanticType.Unknown,
                        VariadicParameterIndex = GetVariadicIndex(clrParameters)
                    };
                }

                // Check fields
                var clrField = udt.Symbol.Fields.FirstOrDefault(f => f.Name == clrMemberName);
                if (clrField != null)
                    return GetVariableType(clrField);

                // Member not found on CLR type — fall back to codegen rather than
                // emitting an error, since the TypeSymbol may be a CLR shadow of a
                // user-defined type with different members.
                MarkExpressionAsErrorRecovery(memberAccess);
                return SemanticType.Unknown;
            }

            var effectiveMember = memberAccess.Member;

            // Handle enum .name and .value properties
            if (udt.Symbol.TypeKind == TypeKind.Enum)
            {
                if (effectiveMember == "name")
                    return SemanticType.Str;
                if (effectiveMember == "value")
                    return SemanticType.Int;
            }

            // Look for field or property (including inherited fields)
            var (field, fieldOwner) = FindFieldInHierarchy(udt.Symbol, effectiveMember);
            if (field != null && fieldOwner != null)
            {
                // Access level validation is handled by AccessValidator in the validation pipeline

                var fieldType = GetVariableType(field);

                // Warn and record resolution when accessing a static field via instance.
                // C# disallows instance access to static members (CS0176), so codegen
                // must rewrite `obj.field` → `ClassName.Field` for any instance access.
                if (field.IsStatic)
                {
                    var ownerName = fieldOwner.Name;
                    _diagnostics.AddWarning(
                        $"Accessing static field '{memberAccess.Member}' via instance. " +
                        $"Prefer '{ownerName}.{memberAccess.Member}'.",
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        _currentFilePath,
                        code: DiagnosticCodes.Validation.StaticFieldViaInstance,
                        phase: CompilerPhase.TypeChecking);
                    _semanticInfo.SetMemberAccessResolution(memberAccess, fieldOwner, field);
                }

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
            var (prop, propOwner) = FindPropertyInHierarchy(udt.Symbol, effectiveMember);
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

            // Look for event (including inherited events)
            var eventSymbol = FindEventInHierarchy(udt.Symbol, effectiveMember);
            if (eventSymbol != null)
            {
                _semanticInfo.MarkAsEventAccess(memberAccess);
                var eventType = eventSymbol.Type;

                // Events are nullable (may have no subscribers)
                if (eventType is not UnknownType and not NullableType)
                {
                    eventType = new NullableType { UnderlyingType = eventType };
                }

                // Wrap result in optional/nullable for null conditional access
                if (memberAccess.IsNullConditional && eventType is NullableType nullableEventType)
                {
                    // ?. unwraps the nullable, returning the underlying type
                    eventType = nullableEventType.UnderlyingType;
                }

                // Check raise restriction: events can only be invoked from within the declaring class
                if (_currentClass == null || !TypeHasEvent(_currentClass, memberAccess.Member))
                {
                    // We're outside the declaring class — the event access is allowed for +=/-=
                    // (which is handled in CheckAssignment), but direct member access for invocation
                    // will be caught when the invoke() call is attempted.
                    // For now, we mark the access and let the codegen/caller handle the restriction.
                }

                return eventType;
            }

            // Resolve .invoke() on delegate types — Sharpy's `invoke` maps to C#'s `Invoke`.
            // This enables the event raise pattern: self.on_change?.invoke(self, args)
            if (effectiveMember == "invoke" && udt.Symbol.TypeKind == TypeKind.Delegate)
            {
                var invokeMethod = TryGetDelegateInvokeMethod(memberLookupType);
                if (invokeMethod != null)
                {
                    // Build a FunctionType from the delegate's Invoke signature
                    var paramTypes = invokeMethod.Parameters.Select(p => p.Type).ToList();
                    return new FunctionType
                    {
                        ParameterTypes = paramTypes,
                        ReturnType = invokeMethod.ReturnType,
                        VariadicParameterIndex = GetVariadicIndex(invokeMethod.Parameters)
                    };
                }
            }

            // Look for method (including inherited methods)
            var (method, methodOwner) = FindMethodInHierarchy(udt.Symbol, effectiveMember);
            if (method != null && methodOwner != null)
            {
                // Access level validation is handled by AccessValidator in the validation pipeline

                // When accessing a method via member access (obj.method), the object is implicitly
                // bound as the first parameter (self), so we skip it when creating the FunctionType.
                // CLR-discovered methods (e.g., inherited from a .NET base class like JSONEncoder)
                // have no synthetic 'self' parameter — their Parameters list contains only the
                // declared method parameters. Mirror the GetSelfOffset logic in ResolveOverloadCore.
                var selfOffset = method.Parameters.Count > 0
                    && method.Parameters[0].Name == PythonNames.Self
                    ? 1 : 0;
                var methodParameters = method.Parameters.Skip(selfOffset).ToList();
                var paramTypes = methodParameters.Select(p => p.Type).ToList();

                var methodFunctionType = new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = method.ReturnType,
                    VariadicParameterIndex = GetVariadicIndex(methodParameters)
                };

                // For null conditional method access, we don't wrap the FunctionType itself,
                // but the eventual call result should be nullable (handled in CheckFunctionCall)
                return methodFunctionType;
            }

            var typeMemberMessage = $"Type '{memberLookupType.GetDisplayName()}' has no member '{effectiveMember}'";
            if (udt.Symbol != null)
            {
                var typeMemberSuggestion = FindMemberSuggestion(effectiveMember, udt.Symbol);
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

        // Resolve .invoke() on generic delegate types (e.g., EventHandler[T]?.invoke(...))
        if (memberAccess.Member == "invoke" && memberLookupType is GenericType delegateGenericType
            && delegateGenericType.GenericDefinition is { TypeKind: TypeKind.Delegate })
        {
            var invokeMethod = TryGetDelegateInvokeMethod(memberLookupType);
            if (invokeMethod != null)
            {
                var paramTypes = invokeMethod.Parameters.Select(p => p.Type).ToList();
                return new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = invokeMethod.ReturnType,
                    VariadicParameterIndex = GetVariadicIndex(invokeMethod.Parameters)
                };
            }
        }

        // Resolve TaskType member access: task.result, task.is_completed, etc.
        // TaskType wraps System.Threading.Tasks.Task<T>, so resolve known properties directly.
        // Accept both snake_case (Sharpy) and PascalCase (.NET) for common properties.
        if (memberLookupType is TaskType taskType)
        {
            return memberAccess.Member switch
            {
                "result" or "Result" => taskType.ResultType ?? SemanticType.Unknown,
                "is_completed" or "IsCompleted" => SemanticType.Bool,
                "is_faulted" or "IsFaulted" => SemanticType.Bool,
                "is_canceled" or "IsCanceled" => SemanticType.Bool,
                "is_completed_successfully" or "IsCompletedSuccessfully" => SemanticType.Bool,
                _ => SemanticType.Unknown
            };
        }

        // Resolve builtin type member access via BuiltinRegistry TypeSymbol metadata.
        // Handles: list.append(), dict.items(), result.unwrap(), optional.unwrap(),
        // str.upper(), int methods, etc.
        {
            var (builtinTypeSymbol, builtinTypeArgs) = ResolveBuiltinTypeInfo(memberLookupType);
            if (builtinTypeSymbol != null)
            {
                var methodSymbol = builtinTypeSymbol.Methods
                    .FirstOrDefault(m => m.Name == memberAccess.Member);
                if (methodSymbol != null)
                {
                    var resolvedReturnType = builtinTypeArgs != null
                        ? SubstituteTypeParameters(methodSymbol.ReturnType, builtinTypeSymbol.TypeParameters, builtinTypeArgs)
                        : methodSymbol.ReturnType;

                    // Skip methods whose return type resolved to 'object' — this indicates
                    // the discovery layer couldn't represent the real type (e.g., generic method
                    // return types like Result<U, E> from map()). Let the codegen fallback handle these.
                    if (resolvedReturnType is not UserDefinedType { Name: "object" })
                    {
                        var resolvedParams = methodSymbol.Parameters
                            .Select(p => builtinTypeArgs != null
                                ? SubstituteTypeParameters(p.Type, builtinTypeSymbol.TypeParameters, builtinTypeArgs)
                                : p.Type)
                            .ToList();
                        return new FunctionType
                        {
                            ParameterTypes = resolvedParams,
                            ReturnType = resolvedReturnType,
                            VariadicParameterIndex = GetVariadicIndex(methodSymbol.Parameters)
                        };
                    }
                }

                // Check properties (is_ok, is_err, is_some, is_none, etc.)
                var property = builtinTypeSymbol.Properties
                    .FirstOrDefault(p => p.Name == memberAccess.Member);
                if (property != null)
                {
                    return builtinTypeArgs != null
                        ? SubstituteTypeParameters(property.Type, builtinTypeSymbol.TypeParameters, builtinTypeArgs)
                        : property.Type;
                }
            }
        }

        // Intentional Unknown without error for non-UserDefinedType member access:
        // GenericType (list[T].append), BuiltinType (str.upper), TupleType, etc.
        // are resolved by the codegen layer through CLR member discovery, not the
        // type checker. Mark as error recovery to suppress SPY0907 false positives.
        MarkExpressionAsErrorRecovery(memberAccess);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Maps a SemanticType to its BuiltinRegistry TypeSymbol and type arguments.
    /// Returns (null, null) if the type is not a registered builtin.
    /// Used by CheckMemberAccess and ResolveUserMethodOverload for uniform
    /// property/method resolution across GenericType, ResultType, OptionalType, and BuiltinType.
    /// </summary>
    private (TypeSymbol? TypeSymbol, List<SemanticType>? TypeArgs) ResolveBuiltinTypeInfo(SemanticType type)
    {
        return type switch
        {
            GenericType gt => (_symbolTable.BuiltinRegistry.GetType(gt.Name), gt.TypeArguments),
            ResultType rt => (_symbolTable.BuiltinRegistry.GetType(BuiltinNames.Result),
                              new List<SemanticType> { rt.OkType, rt.ErrorType }),
            OptionalType ot => (_symbolTable.BuiltinRegistry.GetType(BuiltinNames.Optional),
                                new List<SemanticType> { ot.UnderlyingType }),
            BuiltinType bt => (_symbolTable.BuiltinRegistry.GetType(bt.Name), null),
            _ => (null, null)
        };
    }

    /// <summary>
    /// Finds a field by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (VariableSymbol? Field, TypeSymbol? Owner) FindFieldInHierarchy(TypeSymbol type, string fieldName)
        => TypeHierarchyService.FindField(type, fieldName, SemanticBinding);

    /// <summary>
    /// Finds a property by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (PropertySymbol? Property, TypeSymbol? Owner) FindPropertyInHierarchy(TypeSymbol type, string propertyName)
        => TypeHierarchyService.FindProperty(type, propertyName, SemanticBinding);

    /// <summary>
    /// Finds a method by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (FunctionSymbol? Method, TypeSymbol? Owner) FindMethodInHierarchy(TypeSymbol type, string methodName)
        => TypeHierarchyService.FindMethod(type, methodName, SemanticBinding);

    /// <summary>
    /// Tries to resolve member access on a TypeSymbol (enum values, union cases,
    /// static fields/methods). Returns null if no member was found.
    /// </summary>
    private SemanticType? TryResolveTypeMemberAccess(
        MemberAccess memberAccess, Identifier typeId, TypeSymbol typeSym)
    {
        if (typeSym.TypeKind == TypeKind.Enum)
        {
            var enumType = new UserDefinedType { Name = typeSym.Name, Symbol = typeSym };
            var enumMember = typeSym.Fields.FirstOrDefault(f => f.Name == memberAccess.Member);
            _semanticInfo.SetExpressionType(memberAccess, enumType);
            if (enumMember != null)
                _semanticInfo.SetMemberAccessResolution(memberAccess, typeSym, enumMember);
            return enumType;
        }

        if (typeSym.TypeKind == TypeKind.Union)
        {
            var caseSymbol = typeSym.UnionCases.FirstOrDefault(c => c.Name == memberAccess.Member);
            if (caseSymbol != null)
            {
                var caseType = new UserDefinedType { Name = caseSymbol.Name, Symbol = caseSymbol };
                _semanticInfo.SetExpressionType(memberAccess, caseType);
                _semanticInfo.SetMemberAccessResolution(memberAccess, typeSym, caseSymbol);
                return caseType;
            }

            AddError(
                $"Union '{typeSym.Name}' has no case '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
            return SemanticType.Unknown;
        }

        if (typeSym.TypeKind is TypeKind.Class or TypeKind.Struct)
        {
            var field = typeSym.Fields.FirstOrDefault(f => f.Name == memberAccess.Member);
            if (field != null && (field.IsConstant || field.IsStatic))
            {
                var fieldType = GetVariableType(field);
                _semanticInfo.SetExpressionType(memberAccess, fieldType);
                _semanticInfo.SetMemberAccessResolution(memberAccess, typeSym, field);
                return fieldType;
            }

            // Instance field via type name — error
            if (field != null)
            {
                AddError(
                    $"Cannot access instance field '{memberAccess.Member}' via type name '{typeId.Name}'. " +
                    "Mark it as @static or use an instance.",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.InstanceFieldViaTypeName,
                    span: memberAccess.Span);
                return SemanticType.Unknown;
            }

            // Check for static method access
            var method = typeSym.Methods.FirstOrDefault(m =>
                m.Name == memberAccess.Member && m.IsStatic);
            if (method != null)
            {
                _semanticInfo.SetMemberAccessResolution(memberAccess, typeSym, method);
                var paramTypes = method.Parameters.Select(p => p.Type).ToList();
                var funcType = new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = method.ReturnType,
                    VariadicParameterIndex = GetVariadicIndex(method.Parameters)
                };
                _semanticInfo.SetExpressionType(memberAccess, funcType);
                return funcType;
            }
        }

        return null;
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
            // Handle array type reference: array[int] -> GenericType("array", [int])
            if (typeId.Name == BuiltinNames.Array)
            {
                var arrayTypeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (arrayTypeArgs != null && arrayTypeArgs.Count == 1)
                {
                    var arrayType = new GenericType
                    {
                        Name = BuiltinNames.Array,
                        TypeArguments = arrayTypeArgs
                    };
                    _semanticInfo.SetExpressionType(indexAccess, arrayType);
                    return arrayType;
                }
            }

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

        // Handle generic function reference on module members (e.g., json.loads[int])
        if (indexAccess.Object is MemberAccess memberAccessObj)
        {
            var ownerType = CheckExpression(memberAccessObj.Object);
            if (ownerType is ModuleType modType)
            {
                var memName = memberAccessObj.Member;
                if (!modType.Symbol.Exports.ContainsKey(memName) && modType.Symbol.IsNetModule)
                {
                    var pascalName = NameMangler.ToPascalCase(memName);
                    if (modType.Symbol.Exports.ContainsKey(pascalName))
                        memName = pascalName;
                }

                if (modType.Symbol.Exports.TryGetValue(memName, out var exportedSym)
                    && exportedSym is FunctionSymbol modFuncSymbol
                    && modFuncSymbol.IsGeneric)
                {
                    var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                    if (typeArgs != null)
                    {
                        _semanticInfo.SetExpressionType(indexAccess, new GenericFunctionType
                        {
                            FunctionSymbol = modFuncSymbol,
                            TypeArguments = typeArgs
                        });
                        return _semanticInfo.GetExpressionType(indexAccess)!;
                    }
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

}
