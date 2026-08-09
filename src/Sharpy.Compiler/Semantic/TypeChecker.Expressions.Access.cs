extern alias SharpyRT;

using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Member access, index access, type resolution, tagged unions
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        // Type-test operands (`x.f is (not) None`, isinstance subjects) read the raw member value —
        // the node's own narrowing must not apply (its receiver still narrows normally).
        var narrowingKey = ReferenceEquals(memberAccess, _typeTestOperand)
            ? null
            : ExtractNarrowingKey(memberAccess);

        // Expression-level narrowing (the `and` right-hand side, match arms) is pre-resolved in
        // _narrowingContext and short-circuits — preserving the prior behaviour of returning the
        // narrowed type without re-resolving the member. Copy any read lowering the context entry
        // carries so codegen applies the accessor (#1081).
        if (narrowingKey != null
            && _narrowingContext.TryGetNarrowing(narrowingKey, out var contextNarrowed, out var contextLowering))
        {
            _semanticInfo.SetNarrowedType(memberAccess, contextNarrowed!);
            if (contextLowering != null)
                _semanticInfo.SetNarrowedReadLowering(memberAccess, contextLowering);
            return contextNarrowed!;
        }

        var memberType = CheckMemberAccessCore(memberAccess);

        // Statement-level control-flow narrowing (#1042): resolve the CFG facts against the member's
        // computed (non-narrowed) type — e.g. isinstance(self.animal, Dog) narrows "self.animal" to
        // Dog, `if self.value is not None:` strips the Optional from "self.value". The lowering tells
        // codegen which accessor to emit (#1081).
        if (narrowingKey != null && ResolveNarrowedTypeFromFacts(narrowingKey, memberType) is { } factRead)
        {
            _semanticInfo.SetNarrowedType(memberAccess, factRead.Type);
            _semanticInfo.SetNarrowedReadLowering(memberAccess, factRead.Lowering);
            return factRead.Type;
        }

        return memberType;
    }

    private SemanticType CheckMemberAccessCore(MemberAccess memberAccess)
    {
        // Check for super() usage - the parser directly produces SuperExpression for super()
        if (memberAccess.Object is SuperExpression superExpr)
        {
            return ValidateSuperMemberAccess(memberAccess, superExpr);
        }

        // Mark the qualifier so the CheckExpression choke point accepts a generic TYPE reference here:
        // `Box[int].of(42)` names the type a static member is reached through (#1192).
        SemanticType objectType;
        using (ScopedValue.Push(ref _currentMemberAccessQualifier, memberAccess.Object))
            objectType = CheckExpression(memberAccess.Object);

        // Materialize the original CLR method name for CLR-backed receivers so codegen preserves
        // acronym casing (is_os_platform -> IsOSPlatform) without reflecting (#974).
        RecordResolvedClrMemberName(memberAccess, objectType);

        // Materialize static-extension dispatch for str methods so codegen emits an explicit
        // Sharpy.StringExtensions call rather than an instance call a shadowing BCL method captures (#1071, #1072, #1085).
        RecordStaticExtensionDispatch(memberAccess, objectType);

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

        // When the qualifier resolved to a UserDefinedType (e.g. from a nested-type arm above),
        // try resolving the member as a nested type of that type (handles Outer.Middle.Inner chains).
        // Generic nested types are left for the generic reference resolver.
        if (objectType is UserDefinedType qualifierUdt
            && memberAccess.Object is not Identifier)
        {
            var qualifierSym = qualifierUdt.Symbol ?? _symbolTable.LookupType(qualifierUdt.Name);
            if (qualifierSym?.TypeKind is TypeKind.Class or TypeKind.Struct)
            {
                var nestedType = qualifierSym.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member);
                if (nestedType != null && !nestedType.IsGeneric
                    && !ReferenceEquals(memberAccess, _currentMemberAccessQualifier))
                {
                    var nestedUdt = new UserDefinedType { Name = nestedType.Name, Symbol = nestedType };
                    _semanticInfo.SetExpressionType(memberAccess, nestedUdt);
                    _semanticInfo.MarkTypeReference(memberAccess);
                    return nestedUdt;
                }
            }
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

        // Strict Optional (T?): direct member access is only allowed for Optional's own API
        // (unwrap, unwrap_or, unwrap_or_else, map, is_some, is_none, ...). Accessing a member of
        // the underlying type requires narrowing (if x is not None:), '?.', or unwrapping. Without
        // this guard the access silently type-checks and emits broken C# (Optional<T>.Member).
        // Narrowed receivers never reach here — CheckExpression already returns the narrowed T.
        if (objectType is OptionalType && !memberAccess.IsNullConditional
            && !IsOptionalApiMember(objectType, memberAccess.Member))
        {
            AddError(
                $"Type '{objectType.GetDisplayName()}' has no member '{memberAccess.Member}'. " +
                "Narrow the optional first (if x is not None:), use '?.', or unwrap it (x.unwrap()).",
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.NullabilityViolation, span: memberAccess.Span);
            return SemanticType.Unknown;
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

            // The identity rule at the module-export seam (#1328, the cross-module half of #1325's
            // rule): a BARE spelling never binds an escape-DECLARED export, so `lib.zed` cannot
            // answer `` class `zed` `` — it falls through to the "no member" report below. The
            // converse direction is quoting and stands: an escaped spelling binding a bare-declared
            // export is how #713's CLR-namespace imports are written.
            if (moduleSymbol.Exports.TryGetValue(memberName, out var exportedSymbol)
                && !(exportedSymbol.IsNameBacktickEscaped && !memberAccess.IsMemberBacktickEscaped))
            {
                var exportedType = exportedSymbol switch
                {
                    VariableSymbol varSymbol => GetVariableType(varSymbol),
                    FunctionSymbol funcSymbol => FunctionType.FromParameters(funcSymbol.Parameters, funcSymbol.ReturnType),
                    TypeSymbol typeSymbol => new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol },
                    ModuleSymbol nestedModule => new ModuleType { Symbol = nestedModule },
                    _ => SemanticType.Unknown
                };
                // Mark error recovery for unhandled symbol types in module exports
                // (e.g., TypeAliasSymbol) — these are resolved elsewhere, not a compiler bug.
                if (exportedType is UnknownType)
                    MarkExpressionAsErrorRecovery(memberAccess);
                // A module-qualified reference to an exported type denotes the type itself,
                // not a value. Record this so argument validation can accept it for
                // parameters backed by CLR System.Type (e.g., assert_raises(mod.SomeError)).
                else if (exportedSymbol is TypeSymbol)
                    _semanticInfo.MarkTypeReference(memberAccess);
                return exportedType;
            }

            // For .NET modules, try lazy resolution of sub-namespaces and CLR types
            // (e.g., `System`.Console resolves Console as a CLR type on the System namespace).
            if (moduleSymbol.IsNetModule && ModuleRegistry != null)
            {
                var subResult = TryResolveClrSubNamespaceOrType(moduleSymbol, memberAccess);
                if (subResult != null)
                    return subResult;
            }

            // Suppress cascading "has no member" errors on modules that failed to load: the
            // import failure (SPY0300) was already reported, so flagging every member access
            // on the stub is noise. Mirrors the identifier-level recovery in CheckIdentifier
            // so both compilation pipelines agree — the unified project pipeline (#1038)
            // registers the missing module as a (non-recovery) ModuleSymbol and reaches this
            // branch, where the legacy path short-circuited at the identifier. The import
            // root-cause set (threaded in via ImportRootCauses) covers that case.
            if (moduleSymbol.IsErrorRecovery || _diagnostics.IsRootCause(moduleSymbol.Name))
            {
                MarkExpressionAsErrorRecovery(memberAccess);
                return SemanticType.Unknown;
            }

            var moduleMemberMessage = $"Module '{moduleSymbol.Name}' has no member '{memberAccess.Member}'";
            var moduleMemberSuggestion = FindModuleMemberSuggestion(memberAccess.Member, moduleSymbol);
            if (moduleMemberSuggestion != null)
                moduleMemberMessage += $". Did you mean '{moduleMemberSuggestion}'?";
            AddError(moduleMemberMessage,
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span,
                data: SuggestionData(moduleMemberSuggestion));
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
                    var selfOffset = clrMethod.Parameters.Count > 0
                        && clrMethod.Parameters[0].Name == PythonNames.Self
                        ? 1 : 0;
                    return FunctionType.FromParameters(
                        clrMethod.Parameters, clrMethod.ReturnType, skipLeading: selfOffset);
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
                    return udt.Symbol.IsStringEnum ? SemanticType.Str : SemanticType.Int;
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
                    return FunctionType.FromParameters(invokeMethod.Parameters, invokeMethod.ReturnType);
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

                var methodFunctionType = FunctionType.FromParameters(
                    method.Parameters, method.ReturnType, skipLeading: selfOffset);

                // For null conditional method access, we don't wrap the FunctionType itself,
                // but the eventual call result should be nullable (handled in CheckFunctionCall)
                return methodFunctionType;
            }

            var typeMemberMessage = $"Type '{memberLookupType.GetDisplayName()}' has no member '{effectiveMember}'";
            string? typeMemberSuggestion = null;
            if (udt.Symbol != null)
            {
                typeMemberSuggestion = FindMemberSuggestion(effectiveMember, udt.Symbol);
                if (typeMemberSuggestion != null)
                    typeMemberMessage += $". Did you mean '{typeMemberSuggestion}'?";
            }
            AddError(typeMemberMessage,
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span,
                data: SuggestionData(typeMemberSuggestion));
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
                return FunctionType.FromParameters(invokeMethod.Parameters, invokeMethod.ReturnType);
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
                        var substitutedParams = builtinTypeArgs != null
                            ? methodSymbol.Parameters.Select(p => p with
                            {
                                Type = SubstituteTypeParameters(p.Type, builtinTypeSymbol.TypeParameters, builtinTypeArgs)
                            }).ToList()
                            : methodSymbol.Parameters;
                        return FunctionType.FromParameters(substitutedParams, resolvedReturnType);
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

        // Discovered generic module classes (e.g., difflib.SequenceMatcher[str]) are represented
        // as GenericType carrying their definition's TypeSymbol with discovered methods/properties.
        // The builtin path above doesn't cover them (they aren't registered builtins), so resolve
        // members on the definition with the receiver's type-argument substitution applied. Without
        // this, every method call returned Unknown — so e.g. get_opcodes() lost its list[tuple]
        // type and tuple indexing on the loop variable couldn't lower to .ItemN (#892).
        if (memberLookupType is GenericType { GenericDefinition: { ClrType: not null } genDef } genInstance)
        {
            var genericMember = ResolveGenericModuleClassMember(memberAccess, genDef, genInstance.TypeArguments);
            if (genericMember != null)
                return genericMember;
        }

        // User-defined generic types (e.g. Box[Foo]) are represented as GenericType whose
        // GenericDefinition is the user's TypeSymbol with no backing CLR type. The builtin and
        // discovered-CLR paths above don't cover them, so resolve members directly on the
        // definition's fields/properties/methods, substituting the receiver's type arguments for
        // the definition's type parameters (e.g. unwrap() -> T resolves to Foo). Without this,
        // member access on the result fell through to Unknown and emitted SPY0907 (#912).
        if (memberLookupType is GenericType { GenericDefinition: { ClrType: null } userGenDef } userGenInstance
            && userGenDef.TypeParameters.Count > 0)
        {
            var userGenericMember = ResolveUserDefinedGenericMember(
                memberAccess, userGenDef, userGenInstance.TypeArguments);
            if (userGenericMember != null)
                return userGenericMember;
        }

        // Intentional Unknown without error for non-UserDefinedType member access:
        // A RAW BCL member on a builtin receiver is typed from its reflected signature (#1291).
        // `s.to_upper()` is not part of Sharpy's str API — it is System.String.ToUpper reached by
        // reverse-mangling — so it never resolves above, fell through to Unknown, and Unknown is
        // assignable to anything: `b: bool = s.to_upper()` was accepted in silence. (The Sharpy
        // spelling `s.upper()` resolves earlier and has always been typed; that contrast is what
        // makes this a hole rather than a policy.)
        if (BclMemberTypeOnBuiltinReceiver(memberAccess, memberLookupType) is { } bclType)
        {
            return bclType;
        }

        // GenericType (list[T].append), BuiltinType (str.upper), TupleType, etc.
        // are resolved by the codegen layer through CLR member discovery, not the
        // type checker. Mark as error recovery to suppress SPY0907 false positives.
        MarkExpressionAsErrorRecovery(memberAccess);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// The semantic type of a raw BCL member reached on a builtin receiver, or <c>null</c> when the
    /// receiver is not a builtin with a CLR type or the member does not reflect (#1291).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately narrow. Only a <see cref="BuiltinType"/> receiver is considered: a
    /// <see cref="GenericType"/> receiver (<c>list[T].append</c>) is resolved by codegen through a
    /// different discovery path, and typing it here would be a guess about machinery this method
    /// does not own.
    /// </para>
    /// <para>
    /// Only the PRESENT case is answered. A member that reflects nothing keeps today's Unknown
    /// rather than drawing a refusal — a bogus member is still an ICE at emission (CS1061 behind
    /// SPY0908), which is worth fixing, but refusing here would also refuse anything codegen can
    /// resolve by a route this method does not know about, and a false refusal is worse than the
    /// ICE it replaces.
    /// </para>
    /// </remarks>
    private SemanticType? BclMemberTypeOnBuiltinReceiver(MemberAccess memberAccess, SemanticType receiverType)
    {
        if (receiverType is not BuiltinType { ClrType: { } clrType })
        {
            return null;
        }

        var methodName = Discovery.ClrTypeHelper.ResolveClrMethodName(clrType, memberAccess.Member);
        if (methodName != null)
        {
            var methods = clrType.GetMethods(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == methodName && !m.IsGenericMethodDefinition)
                .ToList();

            // A METHOD member is a callable, so its type is a FunctionType — returning the return
            // type directly would make `s.to_upper()` report "'str' is not callable". Only an
            // unambiguous signature is described: with several overloads, which one applies is a
            // call-site decision this method does not make.
            if (methods.Count != 1)
            {
                return null;
            }

            var returnType = _bclGenericMethodBridge.MapClrTypeToSemanticType(methods[0].ReturnType);
            if (returnType is UnknownType)
            {
                return null;
            }

            var parameterTypes = new List<SemanticType>();
            foreach (var parameter in methods[0].GetParameters())
            {
                var mapped = _bclGenericMethodBridge.MapClrTypeToSemanticType(parameter.ParameterType);
                if (mapped is UnknownType)
                {
                    return null;
                }

                parameterTypes.Add(mapped);
            }

            return new FunctionType { ParameterTypes = parameterTypes, ReturnType = returnType };
        }

        var propertyName = Discovery.ClrTypeHelper.ResolveClrPropertyName(clrType, memberAccess.Member);
        if (propertyName != null
            && clrType.GetProperty(propertyName) is { } property)
        {
            var propertyType = _bclGenericMethodBridge.MapClrTypeToSemanticType(property.PropertyType);
            return propertyType is UnknownType ? null : propertyType;
        }

        return null;
    }

    /// <summary>
    /// Returns true when <paramref name="memberName"/> is a member of the Optional API itself
    /// (e.g. unwrap, unwrap_or, unwrap_or_else, map, is_some, is_none) — the only members that may
    /// be accessed directly on a <c>T?</c> receiver. Resolved from the registered Optional
    /// TypeSymbol so the set stays in sync with Sharpy.Core's Optional definition.
    /// </summary>
    private bool IsOptionalApiMember(SemanticType optionalType, string memberName)
    {
        if (_optionalApiMembers == null)
        {
            var (optionalSymbol, _) = ResolveBuiltinTypeInfo(optionalType);
            if (optionalSymbol == null)
            {
                return false;
            }

            _optionalApiMembers = new HashSet<string>(optionalSymbol.Methods.Select(m => m.Name));
            _optionalApiMembers.UnionWith(optionalSymbol.Properties.Select(p => p.Name));
        }

        return _optionalApiMembers.Contains(memberName);
    }

    /// <summary>
    /// Member names of the Optional API, built lazily from the registered Optional TypeSymbol
    /// on first <c>T?</c> member access (the set is identical for every Optional instantiation).
    /// </summary>
    private HashSet<string>? _optionalApiMembers;

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
    /// Resolves a property/method/field access on a discovered generic module class (e.g.
    /// <c>SequenceMatcher[str]</c>), applying the receiver's type-argument substitution
    /// (definition type parameters → <paramref name="typeArgs"/>) to the result type. Returns null
    /// when the member is not found on the definition, leaving the caller's existing fallback intact.
    /// </summary>
    private SemanticType? ResolveGenericModuleClassMember(
        MemberAccess memberAccess, TypeSymbol genDef, List<SemanticType> typeArgs)
    {
        SemanticType Substitute(SemanticType t) =>
            genDef.TypeParameters.Count > 0 && genDef.TypeParameters.Count == typeArgs.Count
                ? SubstituteTypeParameters(t, genDef.TypeParameters, typeArgs)
                : t;

        var member = memberAccess.Member;

        // Resolve methods first, then properties. Only resolve methods when there is exactly
        // one overload: multiple overloads (default-parameter expansions like Counter.most_common)
        // need full overload resolution against the call's arguments, and a self-referential
        // generic return that collapsed to object (e.g. Counter.copy() -> Counter[T]) is left
        // to codegen. Caution: bailing here means the member's type stays unrecorded, so a
        // tuple-returning method that gains a second overload would silently reintroduce the
        // #892 symptom (CS0021 on .ItemN lowering) — extend this to full overload resolution
        // if that ever occurs.
        var methods = genDef.Methods.Where(m => m.Name == member).ToList();
        if (methods.Count == 1)
        {
            var method = methods[0];
            var returnType = Substitute(method.ReturnType);
            if (!IsObjectType(returnType))
            {
                var selfOffset = method.Parameters.Count > 0 && method.Parameters[0].Name == PythonNames.Self
                    ? 1 : 0;
                var substitutedParams = method.Parameters
                    .Select(p => p with { Type = Substitute(p.Type) })
                    .ToList();
                return FunctionType.FromParameters(substitutedParams, returnType, skipLeading: selfOffset);
            }
        }

        // Properties on generic discovered receivers (#1294 sub-case B): dict subclasses like
        // Counter expose CLR properties (Keys, Values) whose types carry provenance after the
        // ConvertTypeSignature stamp. Only resolve when the member access is NOT a call callee
        // — resolving a property type for c.keys() would emit SPY0201 because list[str] is not
        // callable (#555 hazard). The emitter's zero-arg-call-onto-property collapse handles
        // c.keys() from the codegen side; the TypeChecker must leave that path as Unknown.
        if (!ReferenceEquals(memberAccess, _currentCallCallee))
        {
            var prop = genDef.Properties.FirstOrDefault(p => p.Name == member);
            if (prop != null)
            {
                var propType = Substitute(prop.Type);
                if (!IsObjectType(propType))
                    return propType;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a field/property/method access on a user-defined generic type (e.g.
    /// <c>Box[Foo].unwrap()</c> or <c>box.value</c>), substituting the receiver's type arguments
    /// (definition type parameters → <paramref name="typeArgs"/>) into the resolved member type.
    /// Returns null when the member is not found, leaving the caller's existing fallback intact.
    /// Unlike <see cref="ResolveGenericModuleClassMember"/>, this handles definitions with no
    /// backing CLR type (genuinely user-authored generic classes), so it resolves fields and
    /// properties in addition to methods.
    /// </summary>
    private SemanticType? ResolveUserDefinedGenericMember(
        MemberAccess memberAccess, TypeSymbol genDef, List<SemanticType> typeArgs)
    {
        // substituteNamedUserTypes: imported generic definitions may materialize a type-parameter
        // reference in a member signature (e.g. the return type T of an imported Box[T].get()) as a
        // bare UserDefinedType named after the parameter rather than a TypeParameterType. Within the
        // owning generic that name denotes the type parameter, so substitute it too (#912).
        SemanticType Substitute(SemanticType t) =>
            genDef.TypeParameters.Count > 0 && genDef.TypeParameters.Count == typeArgs.Count
                ? SubstituteTypeParameters(t, genDef.TypeParameters, typeArgs, substituteNamedUserTypes: true)
                : t;

        var member = memberAccess.Member;

        var (field, fieldOwner) = FindFieldInHierarchy(genDef, member);
        if (field != null && fieldOwner != null)
        {
            if (field.IsStatic)
                _semanticInfo.SetMemberAccessResolution(memberAccess, fieldOwner, field);
            return Substitute(GetVariableType(field));
        }

        var (prop, propOwner) = FindPropertyInHierarchy(genDef, member);
        if (prop != null && propOwner != null)
            return Substitute(prop.Type);

        var (method, methodOwner) = FindMethodInHierarchy(genDef, member);
        if (method != null && methodOwner != null)
        {
            var selfOffset = method.Parameters.Count > 0
                && method.Parameters[0].Name == PythonNames.Self
                ? 1 : 0;
            var substitutedParams = method.Parameters
                .Select(p => p with { Type = Substitute(p.Type) })
                .ToList();
            return FunctionType.FromParameters(
                substitutedParams, Substitute(method.ReturnType), skipLeading: selfOffset);
        }

        return null;
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
    /// Resolves <paramref name="memberName"/> to a generic instance method on the receiver's
    /// declared type (<paramref name="ownerType"/>), for the explicit-type-argument reference
    /// <c>recv.method[T]</c> (#1133). Handles user-defined classes/structs, user-defined generic
    /// types, and discovered CLR types (via the builtin-registry TypeSymbol). Returns null when
    /// the member is absent or is not a generic method — the caller then falls through to the
    /// ordinary index-access path. A generic overload sitting behind a non-generic sibling is
    /// preferred (the first-by-name <see cref="FindMethodInHierarchy"/> result may be the
    /// non-generic one).
    ///
    /// <para>
    /// When the eagerly-populated <c>Methods</c> scans miss and the owning type is a raw BCL type
    /// (a <see cref="TypeSymbol"/> from <c>ModuleRegistry.CreateTypeSymbolFromClrType</c> with a
    /// <see cref="TypeSymbol.ClrType"/> but no <c>Methods</c>), the generic method is resolved by
    /// reflecting the owning CLR type (#1136). <paramref name="typeArgCount"/>, when known, selects
    /// the arity-matching overload; the arm re-validates the count afterwards. The absence of this
    /// fallback is exactly what made <c>lst.convert_all[str](...)</c> on an imported
    /// <c>List[int]</c> fall through to value-indexing and silently mis-emit.
    /// </para>
    /// </summary>
    private FunctionSymbol? TryResolveGenericInstanceMethod(
        SemanticType ownerType, string memberName, int? typeArgCount = null)
    {
        var ownerSymbol = ResolveInstanceMemberOwnerSymbol(ownerType);
        if (ownerSymbol == null)
            return null;

        var (found, _) = FindMethodInHierarchy(ownerSymbol, memberName);
        if (found is { IsGeneric: true })
            return found;

        // The first-by-name method was non-generic (or absent); scan the type and its
        // base/interface chain for a generic overload of the same name.
        foreach (var candidate in EnumerateTypeAndHierarchy(ownerSymbol))
        {
            var generic = candidate.Methods.FirstOrDefault(m => m.Name == memberName && m.IsGeneric);
            if (generic != null)
                return generic;
        }

        // Raw BCL fallback (#1136): the discovered TypeSymbol has a ClrType but empty Methods, so
        // reflect the owning CLR type for a matching generic method definition.
        if (ownerSymbol.ClrType != null)
            return ResolveBclGenericInstanceMethod(ownerSymbol, ownerType, memberName, typeArgCount);

        return null;
    }

    /// <summary>
    /// CLR-reflection fallback for <see cref="TryResolveGenericInstanceMethod"/> (#1136): materializes
    /// a <see cref="FunctionSymbol"/> for a generic instance method of a raw BCL type whose members were
    /// never eagerly discovered. Memoized per-compilation on <c>(ownerSymbol, memberName)</c> including
    /// negative results; <see cref="TypeSymbol.Methods"/> is never mutated. Reflects on the constructed
    /// receiver when available so class-level type parameters are already closed.
    /// </summary>
    private FunctionSymbol? ResolveBclGenericInstanceMethod(
        TypeSymbol ownerSymbol, SemanticType ownerType, string memberName, int? typeArgCount)
    {
        var memoKey = (ownerSymbol, memberName);
        if (_bclGenericMethodMemo.TryGetValue(memoKey, out var memoized))
            return memoized;

        // Prefer the constructed generic (e.g. List<int>) so class-level params are closed; fall back
        // to the definition's ClrType (e.g. the open List<>) when construction is unavailable.
        var reflectionType = TryGetClrType(ownerType) ?? ownerSymbol.ClrType!;
        var candidates = Discovery.ClrTypeHelper.ResolveGenericInstanceMethods(reflectionType, memberName);

        FunctionSymbol? resolved = null;
        if (candidates.Length > 0)
        {
            // Prefer the overload whose generic arity matches the supplied type-arg count; otherwise
            // take the first. Full parameter-based overload resolution is deliberately not attempted —
            // the emitted C# carries explicit type args + the verbatim CLR name and Roslyn binds it.
            var picked = typeArgCount is { } n
                ? candidates.FirstOrDefault(m => m.GetGenericArguments().Length == n) ?? candidates[0]
                : candidates[0];
            resolved = BuildBclGenericMethodSymbol(picked, memberName);
        }

        _bclGenericMethodMemo[memoKey] = resolved;
        return resolved;
    }

    /// <summary>
    /// Builds a <see cref="FunctionSymbol"/> from a reflected generic-method definition
    /// <paramref name="method"/> (#1136). The CLR name is carried verbatim in
    /// <see cref="FunctionSymbol.ClrMethodName"/> (Axiom 1 — ground truth, not a re-mangle), the
    /// generic parameters supply the arity the arm validates, and parameter/return CLR types map
    /// through <see cref="Discovery.ClrTypeBridge"/> with an <c>object</c> fallback (conservative:
    /// arguments are not re-validated against these imperfectly-mapped signatures). Type-parameter
    /// constraints are intentionally not reconstructed — Roslyn enforces them on the emitted call.
    /// </summary>
    private FunctionSymbol BuildBclGenericMethodSymbol(System.Reflection.MethodInfo method, string memberName)
    {
        // MapClrTypeToSemanticType already collapses unrecognized generics (e.g. Converter<T,TOutput>)
        // to SemanticType.Object, which is the conservative fallback we want.
        SemanticType Map(Type t) => _bclGenericMethodBridge.MapClrTypeToSemanticType(t);

        var typeParameters = method.GetGenericArguments()
            .Select(t => new Parser.Ast.TypeParameterDef { Name = t.Name })
            .ToList();

        var parameters = method.GetParameters()
            .Select(p => new ParameterSymbol { Name = p.Name ?? string.Empty, Type = Map(p.ParameterType) })
            .ToList();

        return new FunctionSymbol
        {
            Name = memberName,
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            ClrMethodName = method.Name,
            ClrMethod = method,
            TypeParameters = typeParameters,
            Parameters = parameters,
            ReturnType = Map(method.ReturnType),
            IsStatic = false
        };
    }

    /// <summary>
    /// The <see cref="TypeSymbol"/> whose members an instance-qualified reference on
    /// <paramref name="ownerType"/> resolves against (user-defined class/struct, generic definition, or
    /// the registered builtin/discovered symbol). Shared by the generic-method resolution above and the
    /// member-absence proof below so both ask the same question of the same symbol.
    /// </summary>
    private TypeSymbol? ResolveInstanceMemberOwnerSymbol(SemanticType ownerType) => ownerType switch
    {
        UserDefinedType { Symbol: { } udtSym } => udtSym,
        GenericType { GenericDefinition: { } genDef } => genDef,
        _ => ResolveBuiltinTypeInfo(ownerType).TypeSymbol
    };

    /// <summary>
    /// Whether reflection can affirmatively prove that <paramref name="memberName"/> does not exist on
    /// <paramref name="ownerType"/> (#1141). True only when the receiver has a <see cref="TypeSymbol.ClrType"/>,
    /// no discovered symbol member carries the name, no CLR member matches it under any mangling candidate
    /// (verbatim, reverse-mangled, forward-mangled), and no extension method reachable from the emitted
    /// compilation declares it. Anything short of that proof — no CLR type, inconclusive reflection, a
    /// same-named non-generic member, an extension-method name match — returns false and leaves the
    /// name-only interop channel exactly as permissive as before (Anton's rule). Memoized alongside the
    /// #1136 negative-result memo, including the "did you mean" candidate.
    /// </summary>
    private bool ClrReflectionProvesMemberAbsent(
        SemanticType ownerType, string memberName, out string? suggestion)
    {
        suggestion = null;

        var ownerSymbol = ResolveInstanceMemberOwnerSymbol(ownerType);
        if (ownerSymbol?.ClrType == null)
            return false;

        var memoKey = (ownerSymbol, memberName);
        if (_bclMemberAbsenceMemo.TryGetValue(memoKey, out var memoized))
        {
            suggestion = memoized.Suggestion;
            return memoized.Absent;
        }

        var absent = ProveClrMemberAbsent(ownerSymbol, ownerType, memberName, out suggestion);
        _bclMemberAbsenceMemo[memoKey] = (absent, suggestion);
        return absent;
    }

    private bool ProveClrMemberAbsent(
        TypeSymbol ownerSymbol, SemanticType ownerType, string memberName, out string? suggestion)
    {
        suggestion = null;

        if (ClrInstanceMemberMayExist(ownerSymbol, ownerType, memberName, out var reflectionType, out var clrNames))
            return false;

        var pascalName = NameMangler.ToPascalCase(memberName);
        foreach (var assembly in EnumerateExtensionMethodAssemblies(reflectionType!))
        {
            var extensionNames = Discovery.ClrTypeHelper.GetExtensionMethodNames(assembly);
            if (extensionNames.Contains(memberName) || extensionNames.Contains(pascalName))
                return false;
        }

        suggestion = EditDistance.FindClosestMatch(memberName, clrNames!);
        return true;
    }

    /// <summary>
    /// Whether an INSTANCE member named <paramref name="memberName"/> may exist on
    /// <paramref name="ownerType"/>: a discovered member on the symbol carries the name, the reflected
    /// CLR surface carries it under some mangling candidate, or reflection could not answer. False only
    /// when reflection SUCCEEDED and found nothing — the affirmative half of the #1141 absence proof,
    /// minus its extension-method clause.
    ///
    /// <para>
    /// Shared by <see cref="ProveClrMemberAbsent"/> and by the staged extension-call gate (#1206), which
    /// needs the instance-member half WITHOUT the extension clause: that clause exists to keep the
    /// permissive channel open when an extension method might bind, and binding an extension method is
    /// exactly what the staged path is about to attempt — with System.Linq enumerated, every name on the
    /// acceptance surface would be "found" and the fix could never run.
    /// </para>
    ///
    /// <para>
    /// <b>The reflected name-surface check below is load-bearing and unredundant.</b> Measured on the
    /// receivers this matters for — <c>List</c>, <c>Dictionary</c>, <c>HashSet</c>, <c>StringBuilder</c>
    /// imported from a .NET namespace — the discovered <see cref="TypeSymbol"/> carries ZERO methods,
    /// properties and fields, so the first check cannot fire for them at all and every member name
    /// reaches this one. It alone is what keeps <c>lst.reverse()</c>, <c>lst.count()</c>,
    /// <c>lst.contains(x)</c> and <c>lst.to_array()</c> bound to their instance members instead of being
    /// re-typed as <c>Enumerable</c> extension calls. Weakening it has no second line of defence.
    /// </para>
    /// </summary>
    private bool ClrInstanceMemberMayExist(
        TypeSymbol ownerSymbol, SemanticType ownerType, string memberName,
        out Type? reflectionType, out IReadOnlyCollection<string>? clrNames)
    {
        reflectionType = null;
        clrNames = null;

        // Discovered members on the symbol itself settle the question without reflecting: a name that
        // was discovered exists, whatever the CLR surface says about mangling.
        if (ownerSymbol.Methods.Any(m => m.Name == memberName)
            || ownerSymbol.Properties.Any(p => p.Name == memberName)
            || ownerSymbol.Fields.Any(f => f.Name == memberName))
            return true;

        // Reflect on the constructed receiver when available (List<int> rather than the open List<>),
        // mirroring the #1136 fallback so both see the same member surface.
        reflectionType = TryGetClrType(ownerType) ?? ownerSymbol.ClrType!;
        clrNames = Discovery.ClrTypeHelper.GetMemberNameSurface(reflectionType);
        if (clrNames == null)
            return true; // reflection inconclusive — cannot rule an instance member out

        return clrNames.Contains(memberName)
            || clrNames.Contains(NameMangler.ToPascalCase(memberName));
    }

    /// <summary>
    /// Gate 4 of the staged extension-call seam (#1206): reflection affirmatively shows that NO instance
    /// member of that name could bind on the receiver, so an extension method is the only thing the name
    /// can mean. Anything short of that proof — no CLR type on the owner, inconclusive reflection, a
    /// same-named member of any kind — returns false and leaves the call on the permissive channel,
    /// which is the safe direction (D2).
    /// </summary>
    private bool NoClrInstanceMemberCouldBind(SemanticType receiverType, string memberName)
    {
        var ownerSymbol = ResolveInstanceMemberOwnerSymbol(receiverType);
        if (ownerSymbol?.ClrType == null)
            return false;

        return !ClrInstanceMemberMayExist(ownerSymbol, receiverType, memberName, out _, out _);
    }

    /// <summary>
    /// The assemblies whose extension methods could bind to a member reference on
    /// <paramref name="receiverClrType"/> in the emitted C#: the receiver's own assembly, Sharpy.Core
    /// and System.Linq (whose namespaces every generated file imports), the core library, and every
    /// .NET module the compilation imported. Consulted by the absence proof so a reachable extension
    /// method keeps the reference permissive.
    /// </summary>
    private IEnumerable<Assembly> EnumerateExtensionMethodAssemblies(Type receiverClrType)
    {
        yield return receiverClrType.Assembly;
        yield return typeof(SharpyRT::Sharpy.Builtins).Assembly;
        yield return typeof(System.Linq.Enumerable).Assembly;
        yield return typeof(object).Assembly;

        if (ModuleRegistry != null)
        {
            foreach (var assembly in ModuleRegistry.LoadedAssemblies)
                yield return assembly;
        }
    }

    /// <summary>
    /// Yields <paramref name="type"/> followed by its base classes and interfaces, for member
    /// scans that need to consider every declaring type (not just the first-by-name match).
    /// </summary>
    private IEnumerable<TypeSymbol> EnumerateTypeAndHierarchy(TypeSymbol type)
    {
        yield return type;
        foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(type, SemanticBinding))
            yield return baseType;
        foreach (var iface in TypeHierarchyService.GetAllInterfaces(type, SemanticBinding))
            yield return iface;
    }

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
                var funcType = FunctionType.FromParameters(method.Parameters, method.ReturnType);
                _semanticInfo.SetExpressionType(memberAccess, funcType);
                return funcType;
            }

            // Check for non-generic nested type access (Outer.Inner). Generic nested types
            // (Outer.Inner[int]) are left for the generic reference resolver which handles
            // type argument filling and inference.
            var nestedType = typeSym.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member);
            if (nestedType != null && !nestedType.IsGeneric
                && !ReferenceEquals(memberAccess, _currentMemberAccessQualifier))
            {
                var nestedUdt = new UserDefinedType { Name = nestedType.Name, Symbol = nestedType };
                _semanticInfo.SetExpressionType(memberAccess, nestedUdt);
                _semanticInfo.MarkTypeReference(memberAccess);
                return nestedUdt;
            }
        }

        return null;
    }

    private SemanticType CheckIndexAccess(IndexAccess indexAccess)
    {
        // Type-test operands (`xs[0] is (not) None`, isinstance subjects) read the raw element —
        // the node's own narrowing must not apply (its container access still lowers normally).
        var narrowingKey = ReferenceEquals(indexAccess, _typeTestOperand)
            ? null
            : ExtractNarrowingKey(indexAccess);

        // Expression-level narrowing (and-RHS, match arms) is pre-resolved and short-circuits. Still
        // type-check (and thereby record) the object and index expressions so codegen can resolve
        // container-specific lowering for the narrowed access — e.g. a narrowed tuple element t[1]
        // must still lower to .Item2, which requires the object's TupleType in SemanticInfo.
        // CheckExpression is cached, so this is cheap and idempotent.
        if (narrowingKey != null
            && _narrowingContext.TryGetNarrowing(narrowingKey, out var contextNarrowed, out var contextLowering))
        {
            var narrowedObjectType = CheckExpression(indexAccess.Object);
            CheckExpression(indexAccess.Index);
            RecordIndexAccessLowering(indexAccess, narrowedObjectType);
            // The container access strategy (RecordIndexAccessLowering above) and the narrowed-read
            // accessor are distinct facts on the same node; codegen composes them (e.g. xs[0].Unwrap()).
            if (contextLowering != null)
                _semanticInfo.SetNarrowedReadLowering(indexAccess, contextLowering);
            return contextNarrowed!;
        }

        var elementType = CheckIndexAccessCore(indexAccess);

        // Statement-level control-flow narrowing (#1042): CheckIndexAccessCore already recorded the
        // index-access container lowering on the normal path, so only the element type needs swapping.
        // The narrowed element read still needs an accessor (#1081) — the read lowering is recorded per
        // index node here; it composes with the container lowering already on the node. Matches the
        // prior index-access narrowing path, which did not record a narrowed *type* in SemanticInfo.
        if (narrowingKey != null && ResolveNarrowedTypeFromFacts(narrowingKey, elementType) is { } factRead)
        {
            _semanticInfo.SetNarrowedReadLowering(indexAccess, factRead.Lowering);
            return factRead.Type;
        }

        return elementType;
    }

    private SemanticType CheckIndexAccessCore(IndexAccess indexAccess)
    {
        // A generic reference (callee[T, ...]) resolves, arity-checks, and lowers uniformly
        // through the GenericReferenceResolver regardless of callee kind — bare or module- or
        // instance-qualified array/type/function (#1143). It records the GenericFunctionType/
        // GenericType expression type and the node-keyed GenericReference fact, and declines
        // gracefully for ordinary value indexing so the value-indexing path below runs unchanged.
        if (TryResolveGenericReference(indexAccess, out var genericReferenceType))
            return genericReferenceType;

        var objectType = CheckExpression(indexAccess.Object);
        SemanticType indexType;
        using (ScopedValue.Push(ref _currentIndexArguments, IndexArgumentSetOf(indexAccess.Index)))
            indexType = CheckExpression(indexAccess.Index);

        // Materialize the codegen lowering strategy for this access so the emitter switches on the
        // tag alone (and never reflects over CLR indexers or re-inspects operand types).
        RecordIndexAccessLowering(indexAccess, objectType);

        // Tuple positional indexing: the index must be a compile-time constant because tuples
        // are heterogeneous (each position can have a different type, and a C# ValueTuple has no
        // runtime indexer — codegen lowers t[k] to .ItemN). Validate constant indices here and
        // reject non-constant ones with a dedicated diagnostic rather than letting them reach CS0021.
        if (objectType is TupleType tupleType)
        {
            if (!TryGetConstantIntIndex(indexAccess.Index, out var constIndex))
            {
                AddError(
                    "Tuple indexing requires a constant integer index because tuples are " +
                    "heterogeneous (each position may have a different type, and the element type " +
                    "must be known at compile time). Use a literal index (e.g. t[0]), unpack the " +
                    "tuple (a, b = t), or iterate it — or use a list[T] if you need dynamic indexing.",
                    indexAccess.Index.LineStart,
                    indexAccess.Index.ColumnStart,
                    DiagnosticCodes.Semantic.TupleNonConstantIndex,
                    indexAccess.Index.Span);
                return SemanticType.Unknown;
            }

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

        // Discovery-backed CLR types with a custom parameterized indexer (e.g. collections.Counter,
        // OrderedDict, ChainMap). Their __getitem__ is recorded only as a typeless protocol stub, so
        // the generic InferIndexAccessType fallback would mis-infer the element type as
        // TypeArguments[0] (the key, while for Counter the value is int). Resolve the real indexer
        // return type from the closed CLR type instead.
        //
        // Excluded:
        //  - Builtin containers (list/dict/set/array/tuple): InferIndexAccessType already infers
        //    these precisely from their type arguments; routing them through the closed CLR type is
        //    redundant and would erase type parameters (see below).
        //  - Types whose type arguments contain unresolved type parameters (e.g. `list[T]` inside a
        //    generic function): TryGetClrType maps `T` to `object`, so the indexer would resolve to
        //    `object` instead of `T`. Keep the precise generic path for those.
        if (objectType is not GenericType
            {
                Name: BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set
                    or BuiltinNames.Array or BuiltinNames.Tuple
            }
            && (objectType is GenericType { GenericDefinition.ClrType: not null }
                or UserDefinedType { Symbol.ClrType: not null })
            && !(objectType is GenericType containerGeneric
                && containerGeneric.TypeArguments.Any(ta => ta is TypeParameterType)))
        {
            var closedClrType = TryGetClrType(objectType);
            if (closedClrType != null)
            {
                var clrIndexerType = _typeInference.InferClrIndexerReturnType(closedClrType);
                if (clrIndexerType != null)
                    return clrIndexerType;
            }
        }

        // Use TypeInferenceService for type inference (errors reported by validator in pipeline)
        var resultType = _typeInference.InferIndexAccessType(objectType, indexType);

        // TypeInferenceService covers all supported operations - return Unknown for unsupported
        return resultType ?? SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves and records the original CLR method name for a member access on a CLR-backed
    /// receiver (static type-name or instance), mirroring the emitter's former reflection fallback.
    /// The reflection lives in <see cref="Discovery.ClrTypeHelper"/> so codegen performs none (#974).
    /// </summary>
    private void RecordResolvedClrMemberName(MemberAccess memberAccess, SemanticType objectType)
    {
        // Static receiver: a bare type-name identifier (e.g. RuntimeInformation.is_os_platform()).
        var clrType = memberAccess.Object is Identifier staticId
            && _semanticInfo.GetIdentifierSymbol(staticId) is TypeSymbol staticTs
                ? staticTs.ClrType
                // Instance receiver: resolve via the receiver's semantic type.
                : objectType switch
                {
                    UserDefinedType udt when (udt.Symbol as TypeSymbol)?.ClrType is { } ct => ct,
                    BuiltinType bt => bt.ClrType,
                    _ => null
                };

        if (clrType == null)
            return;

        // Resolve a method name first (preserves the #705/#974 acronym-casing behavior), falling
        // back to a property name so a verbatim CLR property (e.g. socket's lowercase `type`) is
        // emitted unmangled instead of forward-mangled to `Type` (CS1061, #1093).
        var clrName = Discovery.ClrTypeHelper.ResolveClrMethodName(clrType, memberAccess.Member)
            ?? Discovery.ClrTypeHelper.ResolveClrPropertyName(clrType, memberAccess.Member);
        if (clrName != null)
            _semanticInfo.SetResolvedClrMemberName(memberAccess, clrName);
    }

    /// <summary>
    /// Records that a <c>str</c> method call must be emitted as a static
    /// <c>Sharpy.StringExtensions</c> call rather than an instance call. Every Python string method
    /// lives as an extension method in <c>Sharpy.StringExtensions</c> (registered as the sole source
    /// of <c>str</c>'s instance methods in <see cref="Registry.BuiltinRegistry"/>), so an
    /// instance-style emission <c>s.Method(args)</c> lets a same-named <c>System.String</c> BCL method
    /// shadow the extension — silently wrong for <c>Split</c> (returns <c>string[]</c> not
    /// <c>list[str]</c>) and <c>Replace</c> (the <c>count == 0</c> literal binds the
    /// <c>StringComparison</c> overload and replaces all). Tagging every resolved <c>str</c> method
    /// closes the whole class of shadowing, not just the two known cases (#1071, #1072, #1085).
    /// </summary>
    private void RecordStaticExtensionDispatch(MemberAccess memberAccess, SemanticType objectType)
    {
        if (objectType != SemanticType.Str)
            return;

        // str's builtin TypeSymbol.Methods are populated exclusively from Sharpy.StringExtensions
        // (discovery finds no CLR "Str" type), so a resolved member here is always an extension method.
        var (strTypeSymbol, _) = ResolveBuiltinTypeInfo(SemanticType.Str);
        if (strTypeSymbol == null || strTypeSymbol.Methods.All(m => m.Name != memberAccess.Member))
            return;

        var methodName = NameCasing.ResolveMethod(memberAccess.Member, memberAccess.IsMemberBacktickEscaped);
        _semanticInfo.SetStaticExtensionDispatch(
            memberAccess,
            new StaticExtensionDispatch(CSharpTypeNames.SharpyStringExtensions, methodName));
    }

    /// <summary>
    /// Records the codegen lowering strategy for an index access in SemanticInfo (only when it is not
    /// the default native element access), so the emitter switches on the tag alone.
    /// </summary>
    private void RecordIndexAccessLowering(IndexAccess indexAccess, SemanticType objectType)
    {
        var lowering = ComputeIndexAccessLowering(objectType, indexAccess.Object, indexAccess.Index);
        if (lowering != IndexAccessLowering.Native)
            _semanticInfo.SetIndexAccessLowering(indexAccess, lowering);
    }

    /// <summary>
    /// Computes how <c>obj[index]</c> must be lowered by codegen from the object's type and the index
    /// expression. The precedence mirrors the emitter's former inline logic exactly: tuple positional
    /// access (constant int index) &gt; params-indexer spread (tuple-literal index) &gt; string helper
    /// &gt; array helper &gt; native element access.
    /// </summary>
    private IndexAccessLowering ComputeIndexAccessLowering(
        SemanticType objectType, Expression objectExpr, Expression index)
    {
        if (objectType is TupleType && TryGetConstantIntIndex(index, out _))
            return IndexAccessLowering.TupleItem;

        if (index is TupleLiteral && Discovery.ClrTypeHelper.HasParamsIndexer(objectType))
            return IndexAccessLowering.ParamsSpread;

        if (objectType == SemanticType.Str)
            return IndexAccessLowering.String;

        if (objectType is GenericType { Name: BuiltinNames.Array })
            return IndexAccessLowering.Array;

        // Non-negative access into a genuine Sharpy.List<T> skips the ordinary indexer's negative-index
        // Normalize (#1052). Both conditions are required: only a SharpyList backing exposes the
        // GetItemUnchecked accessor; a ClrArray (*args) or narrowed IList backing lacks it, so tagging
        // those would not compile — they stay on the ordinary indexer (#1089).
        if (objectType is GenericType { Name: BuiltinNames.List }
            && IsProvablyNonNegativeIndex(index)
            && ClassifyListBacking(objectExpr) == ListBackingKind.SharpyList)
        {
            return IndexAccessLowering.NativeUnchecked;
        }

        return IndexAccessLowering.Native;
    }

    /// <summary>
    /// Whether <paramref name="index"/> is provably &gt;= 0 under the two sanctioned proofs (#1052):
    /// (a) a non-negative integer literal, or (b) a <c>range(...)</c>-loop induction variable that was
    /// proven non-negative and not reassigned in its loop body (tracked in
    /// <c>_nonNegativeInductionVars</c>, keyed by the loop variable's symbol identity).
    /// </summary>
    private bool IsProvablyNonNegativeIndex(Expression index)
    {
        if (TryGetConstantIntIndex(index, out var literal))
            return literal >= 0;

        return index is Identifier id
            && _semanticInfo.GetIdentifierSymbol(id) is { } symbol
            && _nonNegativeInductionVars.Contains(symbol);
    }

    /// <summary>
    /// Classifies how the <c>list[T]</c> value produced by <paramref name="objectExpr"/> is backed in
    /// the emitted C#, which decides #1052 fast-path eligibility (only <see cref="ListBackingKind.SharpyList"/>
    /// exposes <c>GetItemUnchecked</c>). The verdicts:
    /// <list type="bullet">
    /// <item>list literal / comprehension → <see cref="ListBackingKind.SharpyList"/> (always a concrete
    /// <c>Sharpy.List&lt;T&gt;</c>);</item>
    /// <item>identifier → the tracked <c>_listBackingKinds</c> entry for its symbol, or
    /// <see cref="ListBackingKind.Unknown"/> when untracked;</item>
    /// <item>a call whose recorded return contract is a genuine <c>Sharpy.List&lt;T&gt;</c> →
    /// <see cref="ListBackingKind.SharpyList"/>. Recognized via the <c>StaticExtensionDispatch</c> already
    /// recorded on the callee (the #1085 <c>str.split</c> family, whose <c>SharpyStringExtensions</c>
    /// methods return <c>Sharpy.List&lt;string&gt;</c>), so a direct <c>"a,b".split(",")[i]</c> receiver is
    /// eligible for the first time. Keyed on the recorded contract, not a method-name allowlist, so any
    /// future genuine-<c>Sharpy.List</c> static-extension return joins automatically;</item>
    /// <item>everything else — interop returns, member/index accesses, narrowed values →
    /// <see cref="ListBackingKind.Unknown"/>.</item>
    /// </list>
    /// This replaces the former syntactic allowlist with a tracked backing-kind notion (#1089): the
    /// distinction is now a first-class fact rather than a growing set of AST shapes.
    /// </summary>
    private ListBackingKind ClassifyListBacking(Expression objectExpr)
    {
        switch (objectExpr)
        {
            case ListLiteral:
            case ListComprehension:
                return ListBackingKind.SharpyList;

            case Identifier id:
                return _semanticInfo.GetIdentifierSymbol(id) is { } symbol
                    && _listBackingKinds.TryGetValue(symbol, out var kind)
                    ? kind
                    : ListBackingKind.Unknown;

            // A call routed through a static extension (only str's SharpyStringExtensions today) whose
            // result is used as a list[T] receiver returns a genuine Sharpy.List<T> — the split family
            // is the only such str extension. The dispatch record is the return contract; no method-name
            // matching (#1089).
            case FunctionCall { Function: MemberAccess callee }
                when _semanticInfo.GetStaticExtensionDispatchForIr(callee) is not null:
                return ListBackingKind.SharpyList;

            default:
                return ListBackingKind.Unknown;
        }
    }

    /// <summary>
    /// Selects the builtin overload whose type-parameter count matches <paramref name="typeArgCount"/>.
    /// Multi-arity generic builtins (e.g. map's <c>MapIterator`2/`3/`4</c>) collide on their bare name
    /// in the builtin registry, so <c>SymbolTable.Lookup</c> returns an arbitrary-first overload. When an
    /// explicit-generics call such as <c>map[int, int, int](…)</c> supplies more type args than that
    /// overload has type parameters, substitution would bail and leak an open <c>TOut</c> (#999). This
    /// picks the registry overload with the matching arity so the return type closes. Falls back to
    /// <paramref name="fallback"/> when arities already match or no better overload exists (e.g.
    /// user-defined generics absent from the builtin registry), preserving prior behaviour.
    /// <para>
    /// Substitution is gated on builtin identity: it runs only when <paramref name="fallback"/> is
    /// reference-equal to one of the registry overloads. Builtins reach the global scope from the very
    /// <see cref="FunctionSymbol"/> instances the registry yields (<c>SymbolTable.PopulateBuiltins</c> →
    /// <c>BuiltinRegistry.GetAllFunctions</c>, the same instances <c>GetFunctionOverloads</c> returns),
    /// so a genuine builtin's resolved symbol is reference-equal to a list element. A user-defined
    /// generic that shadows a builtin in a narrower scope is not in that list, so it keeps its own
    /// symbol and is never silently replaced by a same-name builtin of a different arity (#1002).
    /// </para>
    /// </summary>
    private FunctionSymbol SelectArityMatchingOverload(
        string name, int typeArgCount, FunctionSymbol fallback)
    {
        if (fallback.TypeParameters.Count == typeArgCount)
            return fallback;

        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(name);
        if (overloads == null)
            return fallback;

        // Only swap among genuine builtin overloads. If the resolved symbol is not one of the
        // registry's own instances, it is a user-defined generic shadowing the builtin name —
        // keep it rather than type-checking the call against the wrong (builtin) function (#1002).
        bool fallbackIsBuiltin = false;
        foreach (var overload in overloads)
        {
            if (ReferenceEquals(overload, fallback))
            {
                fallbackIsBuiltin = true;
                break;
            }
        }
        if (!fallbackIsBuiltin)
            return fallback;

        foreach (var overload in overloads)
        {
            if (overload.TypeParameters.Count == typeArgCount)
                return overload;
        }

        return fallback;
    }

    /// <summary>
    /// Which spellings an expression-as-type read accepts, and how it reads them. The
    /// <b>generic-argument skeleton</b> — single argument vs <see cref="TupleLiteral"/>, recursion,
    /// <see cref="GenericType"/> assembly — is shared by every caller; these flags select only the
    /// per-site policy that genuinely differs, so a capability added to the skeleton reaches both the
    /// construction path and the type-test classifier at once (#1257).
    /// </summary>
    [Flags]
    private enum TypeOperandShapes
    {
        /// <summary>
        /// The generic-construction spelling (<c>Box[int](42)</c>): a bare name resolves through the
        /// full type resolver — aliases, builtins and nullable spellings included — and a type-argument
        /// list is taken as written.
        /// </summary>
        Construction = 0,

        /// <summary>Grouping parentheses, around the operand and around each type argument, are
        /// transparent (#1170, #1197).</summary>
        UnwrapGrouping = 1 << 0,

        /// <summary>
        /// A qualified name (<c>mod.Type</c>, <c>Outer.Inner</c>) denotes the type the checker already
        /// recorded for that expression (#903).
        /// </summary>
        QualifiedName = 1 << 1,

        /// <summary>
        /// A bare name resolves through the primitive table and then a <b>non-generic</b> symbol
        /// lookup, instead of through the full type resolver. A bare <i>generic</i> name deliberately
        /// denotes nothing under this flag: at a type test it is the open-generic shape the classifier
        /// must fill from the subject or refuse (SPY0345), never a type in its own right.
        /// </summary>
        BareNameIsPrimitiveOrNonGeneric = 1 << 2,

        /// <summary>
        /// A type-argument list whose length disagrees with the definition's arity denotes nothing,
        /// rather than assembling a wrong-arity <see cref="GenericType"/> for a later phase to reject.
        /// </summary>
        RequireMatchingArity = 1 << 3,

        /// <summary>The shape set a type-test operand is read with.</summary>
        TypeTestOperand =
            UnwrapGrouping | QualifiedName | BareNameIsPrimitiveOrNonGeneric | RequireMatchingArity
    }

    /// <summary>
    /// Tries to resolve an expression as a type. Used both for generic type instantiation, where
    /// <c>Box[int](42)</c> parses the type argument as an expression, and for reading an
    /// <c>isinstance</c> type operand that already names its arguments (or has none) — one resolver so
    /// the two cannot drift (#1257). Returns null when the expression does not denote a type, which
    /// leaves each caller on the path it took before.
    /// </summary>
    private SemanticType? TryResolveExpressionAsType(
        Expression expr, TypeOperandShapes shapes = TypeOperandShapes.Construction)
    {
        if (shapes.HasFlag(TypeOperandShapes.UnwrapGrouping))
            expr = UnwrapParenthesized(expr);

        // Handle simple identifier as type name (e.g., "int", "str", "MyClass")
        if (expr is Identifier typeId)
        {
            if (shapes.HasFlag(TypeOperandShapes.BareNameIsPrimitiveOrNonGeneric))
            {
                return ResolveBuiltinPrimitiveTypeName(typeId.Name)
                    ?? (_symbolTable.Lookup(typeId.Name) is TypeSymbol { IsGeneric: false } nonGeneric
                        ? new UserDefinedType { Symbol = nonGeneric, Name = nonGeneric.Name }
                        : null);
            }

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

        // A qualified type reads the type the checker already recorded for the expression.
        if (shapes.HasFlag(TypeOperandShapes.QualifiedName) && expr is MemberAccess memberAccess)
        {
            return _semanticInfo.GetExpressionType(memberAccess) as UserDefinedType;
        }

        // Handle nested generic types (e.g., Box[int] in Container[Box[int]])
        if (expr is IndexAccess indexAccess &&
            indexAccess.Object is Identifier nestedTypeId &&
            _symbolTable.Lookup(nestedTypeId.Name) is TypeSymbol nestedGenericType &&
            nestedGenericType.IsGeneric)
        {
            var nestedTypeArgs = TryResolveTypeArguments(indexAccess.Index, shapes);
            if (nestedTypeArgs != null
                && (!shapes.HasFlag(TypeOperandShapes.RequireMatchingArity)
                    || nestedTypeArgs.Count == nestedGenericType.TypeParameters.Count))
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
    private List<SemanticType>? TryResolveTypeArguments(
        Expression indexExpr, TypeOperandShapes shapes = TypeOperandShapes.Construction)
    {
        var typeArgs = new List<SemanticType>();

        // Handle multiple type arguments: Pair[int, str] parses as TupleLiteral
        if ((shapes.HasFlag(TypeOperandShapes.UnwrapGrouping)
                ? UnwrapParenthesized(indexExpr)
                : indexExpr) is TupleLiteral tuple)
        {
            foreach (var element in tuple.Elements)
            {
                var typeArg = TryResolveExpressionAsType(element, shapes);
                if (typeArg == null)
                    return null;
                typeArgs.Add(typeArg);
            }
            return typeArgs;
        }

        // Handle single type argument
        var singleTypeArg = TryResolveExpressionAsType(indexExpr, shapes);
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
                if (call.Arguments[0] is NoneLiteral)
                {
                    AddError("Some() cannot wrap None — use None directly for the empty Optional case",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidSomeConstructor,
                        span: call.Span);
                    return _expectedType;
                }
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

    /// <summary>
    /// Resolves a member access on a .NET namespace module to either a nested namespace
    /// (e.g., <c>System.Collections</c>) or a CLR type (e.g., <c>System.Console</c>).
    /// Returns null if neither resolution succeeds.
    /// </summary>
    private SemanticType? TryResolveClrSubNamespaceOrType(ModuleSymbol moduleSymbol, MemberAccess memberAccess)
    {
        if (moduleSymbol.NetNamespaceName == null && moduleSymbol.CanonicalModuleName == null)
            return null;

        // For parent modules (e.g., "numpy"), check if there's a sub-module (e.g., "numpy.fft")
        // registered in ModuleRegistry. This enables lazy resolution of nested modules.
        if (moduleSymbol.CanonicalModuleName != null)
        {
            var subModuleName = moduleSymbol.CanonicalModuleName + "." + memberAccess.Member;
            var subModuleFunctions = ModuleRegistry!.GetModuleFunctions(subModuleName);
            var subModuleTypes = ModuleRegistry.GetModuleTypes(subModuleName);
            var subModuleFields = ModuleRegistry.GetModuleFields(subModuleName);

            if (subModuleFunctions.Count > 0 || subModuleTypes.Count > 0 || subModuleFields.Count > 0)
            {
                var subModule = new ModuleSymbol
                {
                    Name = memberAccess.Member,
                    Kind = SymbolKind.Module,
                    FilePath = subModuleName,
                    IsNetModule = true,
                    CanonicalModuleName = subModuleName,
                    CSharpNamespace = ModuleRegistry.GetModuleCSharpNamespace(subModuleName),
                    CSharpClassName = ModuleRegistry.GetModuleCSharpClassName(subModuleName),
                    IsNameBacktickEscaped = memberAccess.IsMemberBacktickEscaped
                };

                // Add functions
                foreach (var func in subModuleFunctions)
                    subModule.Exports.Add(func.Name, func);

                // Add types
                foreach (var type in subModuleTypes)
                    subModule.Exports.Add(type.Name, type);

                // Add fields
                foreach (var (fieldName, _, _) in subModuleFields)
                    if (ModuleRegistry.TryResolveNetType(subModuleName, fieldName) is { } fieldType)
                    {
                        var fieldSymbol = ModuleRegistry.CreateTypeSymbolFromClrType(fieldType);
                        if (fieldSymbol != null)
                            subModule.Exports.Add(fieldName, fieldSymbol);
                    }

                moduleSymbol.Exports.Add(memberAccess.Member, subModule);
                return new ModuleType { Symbol = subModule };
            }
        }

        if (moduleSymbol.NetNamespaceName != null)
        {
            var fullName = moduleSymbol.NetNamespaceName + "." + memberAccess.Member;

            // Check if the qualified name is itself a known sub-namespace (e.g., System.Collections).
            if (ModuleRegistry!.IsNetNamespace(fullName))
            {
                var netNamespace = ModuleRegistry.GetNetNamespace(fullName);
                if (netNamespace != null)
                {
                    var subModule = new ModuleSymbol
                    {
                        Name = memberAccess.Member,
                        Kind = SymbolKind.Module,
                        FilePath = $".net:{fullName}",
                        IsNetModule = true,
                        NetNamespaceName = netNamespace,
                        IsNameBacktickEscaped = memberAccess.IsMemberBacktickEscaped
                    };

                    foreach (var typeSymbol in ModuleRegistry.GetNamespaceTypes(fullName))
                        subModule.Exports.Add(typeSymbol.Name, typeSymbol);

                    moduleSymbol.Exports.Add(memberAccess.Member, subModule);
                    return new ModuleType { Symbol = subModule };
                }
            }

            // Otherwise, attempt to resolve the member as a CLR type within the current namespace.
            // Pass the full namespace path (e.g., "System.Collections.Generic") so MapModuleToNamespace
            // can resolve nested namespaces correctly; it lowercases the input internally.
            var clrType = ModuleRegistry.TryResolveNetType(moduleSymbol.NetNamespaceName, memberAccess.Member);
            if (clrType != null)
            {
                var typeSymbol = ModuleRegistry.CreateTypeSymbolFromClrType(clrType);
                if (typeSymbol != null)
                {
                    moduleSymbol.Exports.Add(memberAccess.Member, typeSymbol);
                    return new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol };
                }
            }
        }

        return null;
    }

}
