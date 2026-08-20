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
                RecordNarrowedReadLowering(memberAccess, contextLowering);
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
            RecordNarrowedReadLowering(memberAccess, factRead.Lowering);
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

            // builtins.None is a literal, not a module member (CPython: SyntaxError).
            if (IsBuiltinsModule(moduleSymbol) && memberName == BuiltinNames.None
                && !memberAccess.IsMemberBacktickEscaped)
            {
                AddError("'None' is a literal, not a member of builtins — use the bare 'None' literal",
                    memberAccess.LineStart, memberAccess.ColumnStart,
                    code: DiagnosticCodes.Semantic.BuiltinsNoneLiteral, span: memberAccess.Span);
                MarkExpressionAsErrorRecovery(memberAccess,
                    ErrorRecoveryReason.AlreadyReported("builtins.None is a literal (SPY0214)"));
                return SemanticType.Unknown;
            }

            // The builtins module's TYPE surface is the registry, consulted BEFORE the discovered
            // exports so that `builtins.X()` denotes what a bare `X()` denotes. See
            // TryResolveBuiltinsQualifiedType for why the two disagreed in both directions (#1322).
            //
            if (TryResolveBuiltinsQualifiedType(moduleSymbol, memberName, memberAccess.IsMemberBacktickEscaped,
                    isCalleePosition: IsCurrentCallCallee(memberAccess))
                is { } builtinsQualifiedType)
            {
                // CALLEE position: the spelling denotes the type, and the call path resolves it.
                if (IsCurrentCallCallee(memberAccess))
                {
                    _semanticInfo.MarkTypeReference(memberAccess);
                    return new UserDefinedType { Name = builtinsQualifiedType.Name, Symbol = builtinsQualifiedType };
                }

                // VALUE position: the constructor-reference rule owns this spelling exactly as it owns
                // the bare one (#1382). Returning Unknown here is a DEFERRAL, not an answer — the same
                // node reaches CheckValuePositionReference immediately after (TypeChecker.Expressions.cs
                // routes `Identifier or MemberAccess` there when it is not a callee), which pins it
                // against an expected function type or refuses with SPY0342 naming `builtins.dict`.
                // Both sides resolve through TryResolveBuiltinsQualifiedType, so reaching here
                // guarantees the classifier resolves too and the deferral cannot fall through silently.
                //
                // Deliberately NOT MarkTypeReference: IsConstructorReferenceValueUse excludes a node
                // recorded as naming a type, so marking it would suppress the very refusal this defers
                // to. And deliberately not the UserDefinedType above — that would hand codegen a member
                // access with no C# form, turning the old honest "no member" into an SPY0908.
                return SemanticType.Unknown;
            }

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
                {
                    MarkExpressionAsErrorRecovery(memberAccess,
                        ErrorRecoveryReason.DeliberatelyPermissive(
                            "a module export of an unhandled symbol kind (type alias) is resolved elsewhere"));
                }
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
                MarkExpressionAsErrorRecovery(memberAccess,
                    ErrorRecoveryReason.AlreadyReported("the module failed to load (SPY0300)"));
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

                // Reflection can sometimes PROVE the member does not exist, and when it can, falling
                // back to codegen only defers the failure into an internal error: `sb.no_such_xyz()`
                // reached Roslyn and came back as CS1061 behind SPY0908, a compiler-bug report for a
                // typo (#1290). The proof (#1141) is deliberately hard to satisfy — a discovered
                // symbol member, any mangling candidate on the reflected surface, a reachable
                // extension method, or reflection failing to answer all keep the permissive channel
                // — so it fires only where codegen was certain to fail too.
                if (ClrReflectionProvesMemberAbsent(memberLookupType, memberAccess.Member, out var clrSuggestion))
                {
                    var absentMessage =
                        $"Type '{udt.Symbol.Name}' has no member '{memberAccess.Member}'";
                    if (clrSuggestion != null)
                        absentMessage += $". Did you mean '{clrSuggestion}'?";

                    AddError(absentMessage,
                        memberAccess.LineStart, memberAccess.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedMember,
                        span: memberAccess.Span,
                        data: SuggestionData(clrSuggestion));
                    return SemanticType.Unknown;
                }

                // Member not found on CLR type — fall back to codegen rather than
                // emitting an error, since the TypeSymbol may be a CLR shadow of a
                // user-defined type with different members.
                MarkExpressionAsErrorRecovery(memberAccess,
                    ErrorRecoveryReason.DeliberatelyPermissive(
                        "a CLR shadow of a user type may declare members discovery did not see"));
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

                // A field inherited from a source-declared generic base is typed at the base's OPEN
                // parameter unless substituted at the arguments the derived class's base clause pinned
                // (#1449). `class Direct(Root[int])` reads `Direct().value` as `int`, not `T`.
                var fieldType = SubstituteMemberForReceiver(
                    udt.Symbol, Array.Empty<SemanticType>(), fieldOwner, GetVariableType(field));

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
                if (prop.Type is UnknownType && prop.HasGetter)
                {
                    // Property type not yet resolved; fallback to unknown
                    return prop.Type;
                }

                // Substitute the base clause's pinned arguments for a property inherited from a
                // source-declared generic base (#1449).
                var propType = SubstituteMemberForReceiver(
                    udt.Symbol, Array.Empty<SemanticType>(), propOwner, prop.Type);

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
            var (eventSymbol, eventOwner) = FindEventInHierarchyWithOwner(udt.Symbol, effectiveMember);
            if (eventSymbol != null && eventOwner != null)
            {
                _semanticInfo.MarkAsEventAccess(memberAccess);
                // Substitute the base clause's pinned arguments for an event inherited from a
                // source-declared generic base (#1449).
                var eventType = SubstituteMemberForReceiver(
                    udt.Symbol, Array.Empty<SemanticType>(), eventOwner, eventSymbol.Type);

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

                // A method inherited from a source-declared generic base is typed at the base's OPEN
                // parameters unless substituted at the arguments the derived class's base clause
                // pinned (#1449): `class Direct(Root[int])` reads `Direct().get` as `() -> int`.
                // Substituting the built FunctionType re-types both parameters and the return.
                var substitutedMethodType = SubstituteMemberForReceiver(
                    udt.Symbol, Array.Empty<SemanticType>(), methodOwner, methodFunctionType);

                // For null conditional method access, we don't wrap the FunctionType itself,
                // but the eventual call result should be nullable (handled in CheckFunctionCall)
                return substitutedMethodType;
            }

            // A receiver whose OWN symbol is not CLR-backed can still inherit a CLR member through its
            // base clause: `class IntList(List[int])` reaches System.Collections.Generic.List<int>
            // (#1409). The discovered-members block above is gated on the RECEIVER's ClrType, and the
            // four hierarchy walks just above read TypeSymbol.Fields/Properties/Methods, which a raw
            // BCL TypeSymbol leaves empty — so `m.add(7)` was refused outright while the SAME member
            // on a direct `List[int]` receiver takes the permissive channel and runs. This resolves the
            // ancestor's surface at the base clause's written arguments, and falls back to the same
            // #1141 absence proof a direct receiver of that ancestor would face.
            if (TryResolveInheritedClrMember(memberAccess, udt.Symbol, Array.Empty<SemanticType>())
                is { } inheritedClrMember)
            {
                return inheritedClrMember;
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
                    if (resolvedReturnType is not (UserDefinedType { Name: "object" } or UnmappedClrType))
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

        // The member did not reflect, and on a BUILTIN receiver reflection can prove that is because it
        // does not exist: `"s".nonexistent_member_xyz()` reached Roslyn and came back as CS1061 behind
        // SPY0908, a compiler-bug report for a typo (#1291). The same #1141 proof the UserDefinedType
        // arm above draws on, asked of the same symbol — it is deliberately hard to satisfy, so it fires
        // only where codegen was certain to fail too.
        //
        // A builtin CONTAINER receiver (`lst`, `d`, `st`) joined this arm in #1344, and only after the
        // proof was taught the two spellings codegen reaches a container member by that it did not
        // model — the #1069 word-boundary table and the dunder map, see CodeGenMemberNameCandidates.
        // Measured before that teaching, `d.setdefault("b", 2)` was refused: a working program
        // rejected, which is worse than the ICE it replaces (#1243, #1260), and it is why the gate was
        // BuiltinType-only until the routes were enumerated.
        //
        // The gate is deliberately not "any GenericType": an imported CLR generic and a TupleType keep
        // the permissive channel, and so do the registry's `typeof(object)` placeholders (the dict
        // views, the iterator types), which would otherwise be measured against System.Object's member
        // surface and prove every name absent.
        //
        // An `object` receiver joined in #1389 for the same reason and by the same route: once
        // ResolveBuiltinTypeInfo routes it to the registry's `object` entry, the proof measures a
        // reference against System.Object's REAL surface, which is the right question for that
        // receiver. `o.to_string()` and `o.get_hash_code()` are on it and keep working; `o.bit_length()`
        // is not, and drew CS1061 behind SPY0908 with `emit diagnostics` entirely clean.
        if ((memberLookupType is BuiltinType
                || IsBuiltinContainerReceiver(memberLookupType)
                || IsObjectReceiver(memberLookupType)
                || IsConstructedClrGenericReceiver(memberLookupType))
            && ClrReflectionProvesMemberAbsent(memberLookupType, memberAccess.Member, out var builtinSuggestion))
        {
            var absentOnBuiltin =
                $"Type '{memberLookupType.GetDisplayName()}' has no member '{memberAccess.Member}'";
            if (builtinSuggestion != null)
                absentOnBuiltin += $". Did you mean '{builtinSuggestion}'?";

            // `object` is the one receiver whose remedy is not a spelling fix: the member usually
            // exists on the value's real type and the reference needs the compiler to be told what
            // that is. Narrowing is what Sharpy offers for it, and it works on this receiver today
            // (`if isinstance(o, int):` gives the branch an `int`).
            if (IsObjectReceiver(memberLookupType))
                absentOnBuiltin += ". Narrow the receiver first (if isinstance(x, SomeType):)";

            AddError(absentOnBuiltin,
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span,
                data: SuggestionData(builtinSuggestion));
            return SemanticType.Unknown;
        }

        // GenericType (list[T].append), BuiltinType (str.upper), TupleType, etc.
        // are resolved by the codegen layer through CLR member discovery, not the
        // type checker. Mark as error recovery to suppress SPY0907 false positives.
        MarkExpressionAsErrorRecovery(memberAccess,
            ErrorRecoveryReason.DeliberatelyPermissive(
                "codegen resolves this receiver's members through CLR discovery"));
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves a member against the nearest CLR-backed ANCESTOR of a receiver whose own symbol has no
    /// <see cref="TypeSymbol.ClrType"/>, with the base clause's written type arguments substituted in
    /// (#1409). Returns null when the receiver has no CLR ancestor, leaving the caller's own
    /// "no member" report intact.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The substitution is what distinguishes this from a plain "look on the base too": the ancestor's
    /// discovered members are built ONCE from the open definition and shared by every instantiation, so
    /// <c>List[T].Add(T)</c> is the same <see cref="FunctionSymbol"/> for <c>IntList</c> and for
    /// <c>StrList</c>. Typing the member at the receiver's inherited arguments means composing the base
    /// chain's maps, which <see cref="GenericInstantiationWalker.FindClrAncestor"/> does; the composition
    /// is why <c>class A(B[int])</c> over <c>class B[T](C[T])</c> reaches <c>C[int]</c> and not
    /// <c>C[T]</c>.
    /// </para>
    /// <para>
    /// Falling through to the permissive channel rather than to the caller's refusal is deliberate and
    /// is parity, not laxity: a DIRECT <c>List[int]</c> receiver whose member misses the discovered
    /// surface already lands there (a raw BCL <see cref="TypeSymbol"/> has empty <c>Methods</c>, so
    /// nearly every member does), and codegen resolves it through CLR discovery. Refusing the derived
    /// spelling of a reference the direct spelling accepts is the defect #1409 reported.
    /// </para>
    /// </remarks>
    private SemanticType? TryResolveInheritedClrMember(
        MemberAccess memberAccess, TypeSymbol receiver, IReadOnlyList<SemanticType> receiverTypeArguments)
    {
        // The receiver's own CLR surface is the caller's business (the gate this method sits behind).
        if (receiver.ClrType != null)
            return null;

        var ancestor = GenericInstantiationWalker.FindClrAncestor(
            receiver, receiverTypeArguments, SemanticBinding, _typeResolver);
        if (ancestor == null)
            return null;

        var definition = ancestor.Definition;
        var typeArgs = ancestor.TypeArguments.ToList();
        SemanticType Substitute(SemanticType t) =>
            definition.TypeParameters.Count > 0 && definition.TypeParameters.Count == typeArgs.Count
                ? SubstituteTypeParameters(t, definition.TypeParameters, typeArgs, substituteNamedUserTypes: true)
                : t;

        var member = memberAccess.Member;

        var inheritedProperty = definition.Properties.FirstOrDefault(p => p.Name == member);
        if (inheritedProperty != null)
            return Substitute(inheritedProperty.Type);

        var inheritedMethod = definition.Methods.FirstOrDefault(m => m.Name == member);
        if (inheritedMethod != null)
        {
            var selfOffset = inheritedMethod.Parameters.Count > 0
                && inheritedMethod.Parameters[0].Name == PythonNames.Self
                ? 1 : 0;
            var substitutedParams = inheritedMethod.Parameters
                .Select(p => p with { Type = Substitute(p.Type) })
                .ToList();
            return FunctionType.FromParameters(
                substitutedParams, Substitute(inheritedMethod.ReturnType), skipLeading: selfOffset);
        }

        var inheritedField = definition.Fields.FirstOrDefault(f => f.Name == member);
        if (inheritedField != null)
            return Substitute(GetVariableType(inheritedField));

        // Nothing discovered on the ancestor. The #1141 absence proof decides between a refusal and the
        // permissive channel, measured against the ancestor instantiated at the written arguments — the
        // same question, and so the same answer, a direct receiver of that ancestor would get.
        if (ClrReflectionProvesMemberAbsent(InstantiatedTypeOf(ancestor), member, out var suggestion))
        {
            var absentMessage = $"Type '{receiver.Name}' has no member '{member}'";
            if (suggestion != null)
                absentMessage += $". Did you mean '{suggestion}'?";

            AddError(absentMessage,
                memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span,
                data: SuggestionData(suggestion));
            return SemanticType.Unknown;
        }

        MarkExpressionAsErrorRecovery(memberAccess,
            ErrorRecoveryReason.DeliberatelyPermissive(
                "codegen resolves an inherited CLR member through discovery on the base type"));
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Re-types a member found on <paramref name="owner"/> under the composed substitution the
    /// receiver pins for that owner (#1449). A member is declared in its OWNER's type-parameter
    /// namespace, so the only correct substitution is <c>owner.TypeParameters →</c> the owner's
    /// concrete arguments in THIS receiver's instantiation:
    /// <list type="bullet">
    /// <item>owner IS the receiver's own definition — the receiver's own arguments (#912);</item>
    /// <item>owner is a source-declared generic ANCESTOR — the arguments the base clause pinned,
    /// which <see cref="GenericInstantiationWalker.EnumerateBaseChain"/> has already composed down
    /// the chain (e.g. <c>class Direct(Root[int])</c> pins <c>Root[T].get</c> at <c>int</c>, and
    /// <c>class Middle[T](Pair[T, str])</c> pins <c>Pair</c>'s <c>U</c> at <c>str</c>).</item>
    /// </list>
    /// Returns <paramref name="memberType"/> unchanged for a non-generic owner or an owner not
    /// reached through the base chain (an interface owner, whose generic members this arm does not
    /// substitute — status quo). No new reader of <see cref="BaseTypeReference"/>: the walker is the
    /// one substitution authority (#1287/#1408/#1409 doc contract).
    /// </summary>
    private SemanticType SubstituteMemberForReceiver(
        TypeSymbol receiverDefinition,
        IReadOnlyList<SemanticType> receiverTypeArguments,
        TypeSymbol owner,
        SemanticType memberType)
    {
        if (owner.TypeParameters.Count == 0)
            return memberType;

        // Member declared on the receiver's own definition → substitute the receiver's own arguments.
        if (ReferenceEquals(owner, receiverDefinition))
        {
            return owner.TypeParameters.Count == receiverTypeArguments.Count
                ? SubstituteTypeParameters(
                    memberType, owner.TypeParameters, receiverTypeArguments.ToList(),
                    substituteNamedUserTypes: true)
                : memberType;
        }

        // Member inherited from a generic ancestor → substitute the ancestor's arguments, which
        // EnumerateBaseChain has already composed the receiver's own arguments into.
        foreach (var ancestor in GenericInstantiationWalker.EnumerateBaseChain(
            receiverDefinition, receiverTypeArguments, SemanticBinding, _typeResolver))
        {
            if (!ReferenceEquals(ancestor.Definition, owner))
                continue;

            // Arity mismatch: leave the member open rather than answer at wrong arguments.
            if (ancestor.TypeArguments.Count != owner.TypeParameters.Count)
                return memberType;

            return SubstituteTypeParameters(
                memberType, owner.TypeParameters, ancestor.TypeArguments.ToList(),
                substituteNamedUserTypes: true);
        }

        return memberType;
    }

    /// <summary>
    /// The <see cref="SemanticType"/> form of a walked supertype: a <see cref="GenericType"/> when the
    /// chain produced type arguments, a <see cref="UserDefinedType"/> otherwise.
    /// </summary>
    private static SemanticType InstantiatedTypeOf(
        GenericInstantiationWalker.InstantiatedSupertype ancestor)
        => ancestor.TypeArguments.Count > 0
            ? new GenericType
            {
                Name = ancestor.Definition.Name,
                TypeArguments = ancestor.TypeArguments.ToList(),
                GenericDefinition = ancestor.Definition
            }
            : new UserDefinedType { Name = ancestor.Definition.Name, Symbol = ancestor.Definition };

    /// <summary>
    /// Whether <paramref name="type"/> is an <c>object</c> the USER wrote — the receiver whose unknown
    /// members #1389 refuses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference identity against <see cref="SemanticType.Object"/>, and it has to be, because two
    /// different facts wore that type. <c>object</c> WITH a Symbol is the ClrTypeBridge
    /// generic-collapse placeholder (#955), and a Symbol-less <c>object</c> was BOTH the written
    /// annotation and discovery's "could not map this CLR type" fallback — so refusing an unknown
    /// member on the first also refused <c>g.key</c> on a LINQ <c>group_by</c> result, which runs
    /// (<c>generics/bcl_extension_three_round_1206</c> caught it). The fallback now answers a
    /// distinct <see cref="UnmappedClrType"/> (#1534) — structurally a different record, no longer
    /// merely reference-distinct — so this predicate stays OFF unmapped receivers by type, not by
    /// the fragile identity trick this remark used to describe.
    /// </para>
    /// <para>
    /// A written <c>object</c> reaches here as the singleton itself: <c>TypeResolver</c> returns it
    /// verbatim for the annotation and the checker caches types by reference. Any route that rebuilds
    /// the type loses the identity and takes the permissive channel, which is the safe direction.
    /// </para>
    /// </remarks>
    private static bool IsObjectReceiver(SemanticType type)
        => ReferenceEquals(type, SemanticType.Object);

    /// <summary>
    /// Whether <paramref name="type"/> is one of the builtin CONTAINER receivers the #1141 absence
    /// proof may be asked about (#1344): a <c>list</c>, <c>dict</c>, <c>set</c>, <c>frozenset</c> or
    /// <c>frozendict</c> whose own constructed CLR type reflection can build, so the member surface
    /// the proof measures is the receiver's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named rather than structural, and that is the scope #1344 sanctions. An imported CLR generic
    /// (<c>system.collections.generic.List[int]</c>) reaches its members through routes this proof
    /// still does not enumerate, and <c>BclMemberAbsenceTests.UnknownMember_ValueIndexed_StaysPermissive</c>
    /// pins that it keeps the permissive channel; the residue is #1146's, not this arm's.
    /// </para>
    /// <para>
    /// The <see cref="TryGetClrType"/> requirement is a correctness guard, not a formality: the
    /// registry records <c>typeof(object)</c> as the CLR type of several generic builtins whose real
    /// types codegen resolves through <c>CSharpTypeNames</c> (the dict views, the iterator types).
    /// Measuring a member reference against <c>System.Object</c>'s surface would prove every name
    /// absent, which is the false-refusal shape #1243/#1260 named.
    /// </para>
    /// </remarks>
    private bool IsBuiltinContainerReceiver(SemanticType type)
    {
        if (type is not GenericType container)
            return false;

        if (container.Name is not (BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set
            or BuiltinNames.FrozenSet or BuiltinNames.FrozenDict))
            return false;

        return TryGetClrType(type) is { } clrType
            && clrType != typeof(object)
            && !clrType.IsGenericTypeDefinition;
    }

    /// <summary>
    /// Whether the receiver is a constructed CLR generic with a GenericDefinition carrying a live
    /// CLR type — e.g. <c>HashSet[int]</c> or <c>List[str]</c> from an import. The proof can be
    /// asked about it because <see cref="TryGetClrType"/> yields a closed constructed type whose
    /// member surface reflection can enumerate (#1533).
    /// </summary>
    private bool IsConstructedClrGenericReceiver(SemanticType type)
    {
        if (type is not GenericType { GenericDefinition.ClrType: { } defClr })
            return false;

        return defClr.IsGenericTypeDefinition
            && TryGetClrType(type) is { } clrType
            && clrType != typeof(object)
            && !clrType.IsGenericTypeDefinition;
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

            // A char-returning method is declined HERE so the call seam types it instead (#1291). The
            // conversion to str belongs on the CALL node — the value that carries the char — and this
            // seam describes the method group, which has no value to convert. Declining costs nothing:
            // the call seam re-selects the same single overload by arity.
            if (ClrCharProjection(returnType) != null)
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
            if (propertyType is UnknownType)
            {
                return null;
            }

            // A property read IS the value, so its conversion is recorded on the member node itself.
            return ProjectClrChar(memberAccess, propertyType);
        }

        return null;
    }

    /// <summary>
    /// The Sharpy type a raw BCL call on a builtin receiver produces — <c>"abc".to_char_array()</c>,
    /// <c>s.to_upper()</c> — or <c>null</c> when reflection does not decide it (#1291).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs only for what every resolution above declined, i.e. calls that were on the name-only
    /// interop channel: the emitter wrote them verbatim and the checker had typed them Unknown, which
    /// is assignable to anything. That is what let <c>x: str = "abc".to_char_array()[0]</c> through in
    /// silence and hand Roslyn a <c>char</c> for a <c>string</c> slot (SPY0908/CS0029) — the compiler
    /// reporting its own bug for what is, once typed, an ordinary conversion.
    /// </para>
    /// <para>
    /// Selection is #1243's rule verbatim, the same one <see cref="BclMemberTypeOnBuiltinReceiver"/>
    /// uses for a declared member and #1290 uses for a CLR instance call: exactly one candidate of the
    /// call's arity means the user's intent is not in doubt. Two of the same arity keep silence,
    /// because choosing between them is CLR overload resolution and this seam does not own it — and so
    /// does a keyword or spread argument, whose arity this seam cannot count.
    /// </para>
    /// </remarks>
    private SemanticType? BclCallTypeOnBuiltinReceiver(
        FunctionCall call, MemberAccess memberAccess, List<SemanticType> argTypes)
    {
        if (call.KeywordArguments.Length > 0 || call.Arguments.Any(a => a is SpreadElement))
        {
            return null;
        }

        if (_semanticInfo.GetExpressionType(memberAccess.Object) is not BuiltinType { ClrType: { } clrType })
        {
            return null;
        }

        var methodName = Discovery.ClrTypeHelper.ResolveClrMethodName(clrType, memberAccess.Member);
        if (methodName == null)
        {
            return null;
        }

        var candidates = clrType.GetMethods(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == methodName
                && !m.IsGenericMethodDefinition
                && m.GetParameters().Length == argTypes.Count)
            .ToList();

        if (candidates.Count != 1)
        {
            return null;
        }

        var returnType = _bclGenericMethodBridge.MapClrTypeToSemanticType(candidates[0].ReturnType);
        if (returnType is UnknownType)
        {
            return null;
        }

        return ProjectClrChar(call, returnType);
    }

    /// <summary>
    /// Rewrites a reflected type that is char-based into the Sharpy <c>str</c> it means, recording on
    /// <paramref name="producer"/> the conversion codegen must apply to the value (#1291). Returns
    /// <paramref name="mapped"/> unchanged, and records nothing, for every type with no char in it.
    /// </summary>
    private SemanticType ProjectClrChar(Expression producer, SemanticType mapped)
    {
        var projection = ClrCharProjection(mapped);
        if (projection == null)
        {
            return mapped;
        }

        _semanticInfo.SetCharMaterialization(producer, projection.Value.Kind);
        return projection.Value.Projected;
    }

    /// <summary>
    /// The str type a char-based reflected type projects to and the conversion that realizes it, or
    /// <c>null</c> when the type has no char in it. Sharpy has no char type: a CLR <c>char</c> is a
    /// one-character <c>str</c> at the surface, which is what Python's <c>s[0]</c> is too.
    /// </summary>
    /// <remarks>
    /// Only the two shapes reflection actually hands back on a builtin receiver are described — a
    /// scalar and an array. A <c>List&lt;char&gt;</c> or <c>IEnumerable&lt;char&gt;</c> arrives through
    /// the extension-method seam, which this method does not own, and is left to type as it does today
    /// rather than guessed at from here.
    /// </remarks>
    private static (SemanticType Projected, CharMaterializationKind Kind)? ClrCharProjection(SemanticType mapped)
    {
        if (mapped is BuiltinType { ClrType: { } scalarClr } && scalarClr == typeof(char))
        {
            return (SemanticType.Str, CharMaterializationKind.Scalar);
        }

        if (mapped is GenericType { Name: BuiltinNames.Array, TypeArguments: { Count: 1 } elements }
            && elements[0] is BuiltinType { ClrType: { } elementClr }
            && elementClr == typeof(char))
        {
            return (new GenericType
            {
                Name = BuiltinNames.Array,
                TypeArguments = new List<SemanticType> { SemanticType.Str }
            }, CharMaterializationKind.Array);
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
    /// <remarks>
    /// <c>object</c> is a <see cref="UserDefinedType"/> with no Symbol (<c>SemanticType.Object</c>),
    /// so it reached none of the arms below and had no member surface at all: every member access on
    /// an <c>object</c> receiver typed Unknown in silence and failed, if at all, as CS1061 behind
    /// SPY0908 (#1389). The registry has held its entry since it was written —
    /// <c>RegisterType("object", typeof(object), TypeKind.Class)</c> — and nothing routed the
    /// receiver to it. The Symbol-less shape is the whole match condition: <c>object</c> WITH a
    /// Symbol is the ClrTypeBridge generic-collapse placeholder (#955), a different type entirely,
    /// and the shared <c>SemanticType.Object</c> singleton must never be given one.
    /// </remarks>
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
            UserDefinedType { Symbol: null, Name: BuiltinNames.Object } =>
                (_symbolTable.BuiltinRegistry.GetType(BuiltinNames.Object), null),
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
        // SubstituteMemberForReceiver keys the substitution on the OWNER of the found member:
        //   - member on genDef itself → the receiver's own arguments (#912). substituteNamedUserTypes
        //     handles imported definitions that materialize a type-parameter reference (e.g. the
        //     return type T of an imported Box[T].get()) as a bare UserDefinedType named after the
        //     parameter rather than a TypeParameterType — within the owning generic that name denotes
        //     the parameter, so it is substituted too.
        //   - member pinned by a generic BASE (e.g. `class Middle[T](Pair[T, str])` inherits Pair's
        //     `second: U`) → the base-chain arguments the walker composed, so U resolves to str
        //     rather than staying open (#1449).
        var member = memberAccess.Member;

        var (field, fieldOwner) = FindFieldInHierarchy(genDef, member);
        if (field != null && fieldOwner != null)
        {
            if (field.IsStatic)
                _semanticInfo.SetMemberAccessResolution(memberAccess, fieldOwner, field);
            return SubstituteMemberForReceiver(genDef, typeArgs, fieldOwner, GetVariableType(field));
        }

        var (prop, propOwner) = FindPropertyInHierarchy(genDef, member);
        if (prop != null && propOwner != null)
            return SubstituteMemberForReceiver(genDef, typeArgs, propOwner, prop.Type);

        var (method, methodOwner) = FindMethodInHierarchy(genDef, member);
        if (method != null && methodOwner != null)
        {
            var selfOffset = method.Parameters.Count > 0
                && method.Parameters[0].Name == PythonNames.Self
                ? 1 : 0;
            var methodFunctionType = FunctionType.FromParameters(
                method.Parameters, method.ReturnType, skipLeading: selfOffset);
            return SubstituteMemberForReceiver(genDef, typeArgs, methodOwner, methodFunctionType);
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
        // was discovered exists, whatever the CLR surface says about mangling. Operator and protocol
        // dunders are discovered into their own dictionaries rather than Methods, and the emitter
        // reaches them without a member of that name at all (`lst.__add__(x)` lowers to `lst + x`),
        // so a name either dictionary carries is never absent (#1344).
        if (ownerSymbol.Methods.Any(m => m.Name == memberName)
            || ownerSymbol.Properties.Any(p => p.Name == memberName)
            || ownerSymbol.Fields.Any(f => f.Name == memberName)
            || ownerSymbol.OperatorMethods.ContainsKey(memberName)
            || ownerSymbol.ProtocolMethods.ContainsKey(memberName))
            return true;

        // Reflect on the constructed receiver when available (List<int> rather than the open List<>),
        // mirroring the #1136 fallback so both see the same member surface.
        reflectionType = TryGetClrType(ownerType) ?? ownerSymbol.ClrType!;
        clrNames = Discovery.ClrTypeHelper.GetMemberNameSurface(reflectionType);
        if (clrNames == null)
            return true; // reflection inconclusive — cannot rule an instance member out

        return CodeGenMemberNameCandidates(memberName).Any(clrNames.Contains);
    }

    /// <summary>
    /// Every CLR spelling code generation can reach a Sharpy member reference by. The absence proof
    /// must ask exactly this question, or it claims absence for a name that emits and runs — which is
    /// the false refusal that kept container receivers on the permissive channel until #1344.
    /// </summary>
    /// <remarks>
    /// Two routes live outside <see cref="NameMangler.ToPascalCase"/> and are the ones the corpus
    /// caught: the #1069 word-boundary table (<c>setdefault</c> → <c>SetDefault</c>, which
    /// ToPascalCase spells <c>Setdefault</c>) and the dunder map (<c>__len__</c> → <c>Count</c>).
    /// Every entry here only ADDS a candidate, so teaching a route can only make the proof more
    /// permissive — never more refusing.
    /// </remarks>
    private static IEnumerable<string> CodeGenMemberNameCandidates(string memberName)
    {
        yield return memberName;
        yield return NameMangler.ToPascalCase(memberName);

        if (NameMangler.GetCollectionMethodMapping(memberName) is { } collectionMapped)
            yield return collectionMapped;

        if (DunderNameMapping.ResolveCSharpName(memberName) is { } dunderMapped)
            yield return dunderMapped;
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
                RecordNarrowedReadLowering(indexAccess, contextLowering);
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
            RecordNarrowedReadLowering(indexAccess, factRead.Lowering);
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
                    // A receiver that INHERITS its CLR surface reaches the same members and needs the
                    // same verbatim spelling (#1409). Without this the member resolves (the lookup arm
                    // above sees it) but arrives at codegen with no entry in _resolvedClrMemberNames,
                    // and is emitted through NameMangler instead — which survives `add` -> `Add` by
                    // luck and corrupts every member whose CLR casing mangling cannot reproduce
                    // (socket's lowercase `type`, IsOSPlatform's acronym; #1093, #974).
                    UserDefinedType { Symbol: TypeSymbol receiver } =>
                        InheritedClrTypeOf(receiver, Array.Empty<SemanticType>()),
                    GenericType { GenericDefinition: { ClrType: null } definition } gt =>
                        InheritedClrTypeOf(definition, gt.TypeArguments),
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
    /// The CLR type a receiver inherits its member surface from — the nearest CLR-backed ancestor of
    /// <paramref name="receiver"/> — or null when there is none (#1409). Name resolution is
    /// arity-independent, so the ancestor's own (open) <see cref="TypeSymbol.ClrType"/> is the right
    /// reflection target even when the chain closed it at concrete arguments.
    /// </summary>
    private Type? InheritedClrTypeOf(TypeSymbol receiver, IReadOnlyList<SemanticType> receiverTypeArguments)
        => receiver.ClrType
           ?? GenericInstantiationWalker
               .FindClrAncestor(receiver, receiverTypeArguments, SemanticBinding, _typeResolver)
               ?.Definition.ClrType;

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
            // A type-operand position, so a bare generic name is fillable from the subject and the
            // classifier owns its refusal; the annotation arity error (#1331) must not fire here.
            var resolved = _typeResolver.ResolveTypeAnnotation(
                typeAnnotation, bareGenericFillsFromContext: true);
            return resolved != SemanticType.Unknown ? resolved : null;
        }

        // A qualified type reads the type the checker already recorded for the expression.
        if (shapes.HasFlag(TypeOperandShapes.QualifiedName) && expr is MemberAccess memberAccess)
        {
            return _semanticInfo.GetExpressionType(memberAccess) as UserDefinedType;
        }

        // Handle nested generic types (e.g., Box[int] in Container[Box[int]], lib.Box[int] at a
        // type test). The definition is read through one helper so a QUALIFIED spelling reaches the
        // same arm a bare one does: it did not, and the CLOSED qualified generic fell out of this
        // method as "not a type" — no lowering was recorded, the emitter re-derived the name from
        // the open definition, and `isinstance(box, lib.Box[int])` reached Roslyn as `Lib.Box<T>`,
        // CS0305 behind SPY0908. #1411 fixed the OPEN qualified form; this is its closed sibling
        // (#1447).
        if (expr is IndexAccess indexAccess &&
            TryReadGenericDefinition(indexAccess.Object, shapes) is { IsGeneric: true } nestedGenericType)
        {
            var nestedTypeId = indexAccess.Object as Identifier;
            var nestedTypeArgs = TryResolveTypeArguments(indexAccess.Index, shapes);

            // `tuple[...]` is a TupleType, never GenericType("tuple", …), wherever it is written.
            // This is the #1200 rule, and it has to be applied HERE as well as at the top-level
            // generic-reference classifier (GenericReferenceResolver.cs, the TupleTypeRef arm),
            // because a tuple written as a TYPE ARGUMENT — `iter[tuple[int, int]](…)`,
            // `f[tuple[int, int]](…)` — reaches this resolver instead of that one. Producing the
            // GenericType shape here made the resolved argument unequal to the identical TupleType
            // the annotation position produces, so the substituted parameter matched nothing
            // (SPY0354) and the return check reported the two spellings as different types while
            // RENDERING THEM IDENTICALLY: "Cannot return type 'Iterator[tuple[int, int]]' from
            // function expecting 'Iterator[tuple[int, int]]'" (#1470, the nested sibling of #1200).
            if (nestedTypeId != null
                && TryBuildTupleTypeReference(nestedTypeId, nestedTypeArgs) is { } nestedTuple)
            {
                return nestedTuple;
            }

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
    /// The declaration a type-argument list is being applied to: <c>Box</c> in <c>Box[int]</c>,
    /// <c>lib.Box</c> in <c>lib.Box[int]</c>. One helper for both spellings, so a qualifier changes
    /// which lookup answers and nothing else (#1447; the sweep's contract in miniature).
    /// </summary>
    /// <remarks>
    /// The qualified arm reads the type the checker already recorded for the member access — the
    /// same fact the <see cref="TypeOperandShapes.QualifiedName"/> arm above reads for a
    /// non-generic qualified name — and is gated on that flag, so a construction-position caller
    /// keeps its previous, identifier-only behaviour.
    /// </remarks>
    private TypeSymbol? TryReadGenericDefinition(Expression target, TypeOperandShapes shapes)
    {
        if (shapes.HasFlag(TypeOperandShapes.UnwrapGrouping))
            target = UnwrapParenthesized(target);

        if (target is Identifier bareName)
            return _symbolTable.Lookup(bareName.Name) as TypeSymbol;

        if (shapes.HasFlag(TypeOperandShapes.QualifiedName) && target is MemberAccess qualified)
        {
            // The recorded expression type first, then the resolver's own qualified lookup. The
            // recorded fact is absent here in practice — a member access that is the OBJECT of a
            // type-argument list is never checked as a value — so falling through to
            // LookupModuleQualifiedType is what actually answers, and it is the same machinery the
            // annotation position uses for the identical spelling (#1447, Design Decision 4).
            return (_semanticInfo.GetExpressionType(qualified) as UserDefinedType)?.Symbol
                ?? (TryFlattenDottedName(qualified) is { } dottedName
                    ? _typeResolver.LookupModuleQualifiedType(dottedName)
                    : null);
        }

        return null;
    }

    /// <summary>
    /// The dotted spelling of a member-access chain whose every segment is a plain name
    /// (<c>lib.Box</c>, <c>pkg.mod.Box</c>), or null when any segment is something else — a call, an
    /// index, a literal — because such a chain is not a type name and must not be handed to a
    /// name-keyed lookup.
    /// </summary>
    private static string? TryFlattenDottedName(MemberAccess memberAccess)
        => memberAccess.Object switch
        {
            Identifier root => $"{root.Name}.{memberAccess.Member}",
            MemberAccess nested => TryFlattenDottedName(nested) is { } prefix
                ? $"{prefix}.{memberAccess.Member}"
                : null,
            _ => null
        };

    /// <summary>
    /// The one place that decides a written <c>tuple[...]</c> denotes a <see cref="TupleType"/>.
    ///
    /// <para>A tuple's arity is part of its type, so the written vector is the ELEMENT LIST rather
    /// than arguments to a fixed-arity declaration — which is why it cannot go through the ordinary
    /// generic-reference construction, and why it must produce the same <see cref="TupleType"/> that
    /// <c>TypeResolver</c> produces for the identical annotation. Two shapes for one spelling render
    /// identically and compare unequal, which is the self-contradictory diagnostic of #1200 (the
    /// top-level spelling) and #1470 (the same spelling as a type argument).</para>
    ///
    /// <para>Shared by both resolvers so the two entry points cannot drift again: fixing one and not
    /// the other is exactly how #1470 outlived #1200.</para>
    /// </summary>
    private TupleType? TryBuildTupleTypeReference(Identifier typeId, List<SemanticType>? elementTypes)
        => typeId.Name == BuiltinNames.Tuple
            && !typeId.IsNameBacktickEscaped
            && _symbolTable.Lookup(typeId.Name) is TypeSymbol
            && elementTypes is { Count: > 0 }
                ? new TupleType { ElementTypes = elementTypes }
                : null;

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
