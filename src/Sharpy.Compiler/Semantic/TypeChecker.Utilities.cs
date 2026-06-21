extern alias SharpyRT;
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
    /// Returns the index of the first variadic parameter in the sequence, or null if none.
    /// Used when materializing <see cref="FunctionType"/> from a parameter list so callers
    /// see `params T` semantics.
    /// </summary>
    internal static int? GetVariadicIndex(IEnumerable<ParameterSymbol> parameters)
    {
        int i = 0;
        foreach (var p in parameters)
        {
            if (p.IsVariadic)
                return i;
            i++;
        }
        return null;
    }

    /// <summary>
    /// Returns true if the type can be used in a boolean context (if, while conditions).
    /// A type is truth-testable if it is bool, UnknownType, or a user-defined type with __bool__.
    /// </summary>
    private bool IsTruthTestable(SemanticType type)
    {
        if (type == SemanticType.Bool || type is UnknownType)
            return true;

        // Strings are truth-testable: empty string is falsy, non-empty is truthy
        if (type == SemanticType.Str)
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

        // Unwrap parenthesized expressions
        if (condition is Parenthesized paren)
        {
            return ExtractNarrowedTypes(paren.Expression, isPositiveBranch);
        }

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

        // Handle 'A or B' pattern in the else-branch - De Morgan dual of 'and' narrowing:
        // else of (A or B) is equivalent to then of (not A and not B), so both sides narrow.
        if (condition is BinaryOp { Operator: BinaryOperator.Or } orOp && !isPositiveBranch)
        {
            var (leftNarrowed, leftDecision) = ExtractNarrowedTypes(orOp.Left, false);
            var (rightNarrowed, rightDecision) = ExtractNarrowedTypes(orOp.Right, false);

            foreach (var kvp in leftNarrowed)
            {
                narrowedTypes[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in rightNarrowed)
            {
                narrowedTypes[kvp.Key] = kvp.Value;
            }

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
                        if (nullable.IsValueType)
                            optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, nullable.UnderlyingType, IsValueTypeNullable: true, NarrowInThenBranch: true));
                        else
                            optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, nullable.UnderlyingType, IsValueTypeNullable: false, NarrowInThenBranch: true, IsReferenceTypeNullable: true));
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
                        if (nullable.IsValueType)
                            optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, nullable.UnderlyingType, IsValueTypeNullable: true, NarrowInThenBranch: false));
                        else
                            optionalNarrowings.Add(new OptionalNarrowing(narrowingKey, nullable.UnderlyingType, IsValueTypeNullable: false, NarrowInThenBranch: false, IsReferenceTypeNullable: true));
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
                                var narrowedType = BuildIsInstanceNarrowedType(typeSymbol);
                                narrowedTypes[narrowingKey] = narrowedType;
                                isInstanceNarrowings.Add(new IsInstanceNarrowing(narrowingKey, narrowedType, NarrowInThenBranch: true));
                            }
                        }
                    }
                    // Module-qualified type argument: isinstance(err, email.MessageError) (#903).
                    // The condition was already type-checked, so the MemberAccess type argument's
                    // type is recorded as the referenced UserDefinedType; narrow to it.
                    else if (narrowingKey != null && call.Arguments[1] is MemberAccess typeMemberAccess
                        && _semanticInfo.GetExpressionType(typeMemberAccess) is UserDefinedType qualifiedNarrowedType)
                    {
                        narrowedTypes[narrowingKey] = qualifiedNarrowedType;
                        isInstanceNarrowings.Add(new IsInstanceNarrowing(narrowingKey, qualifiedNarrowedType, NarrowInThenBranch: true));
                    }
                }
            }
        }

        return (narrowedTypes, new NarrowingDecision(optionalNarrowings, isInstanceNarrowings));
    }

    /// <summary>
    /// Builds the narrowed type for an <c>isinstance(x, T)</c> check against a user/builtin
    /// TypeSymbol. Generic builtin collections (list, set, dict) narrow to a parameterized
    /// <see cref="GenericType"/> with default <c>object</c> type arguments, so downstream member
    /// access on the narrowed value (indexing, <c>.items()</c>, etc.) resolves at the semantic
    /// level (#912). Without this they would narrow to a bare <see cref="UserDefinedType"/> with
    /// no type arguments, and e.g. <c>d[k]</c> on a narrowed <c>dict</c> would fail to lower.
    /// Mirrors the unparameterized-collection handling in
    /// <see cref="CheckTypePattern"/>.
    /// </summary>
    private SemanticType BuildIsInstanceNarrowedType(TypeSymbol typeSymbol)
    {
        var arity = typeSymbol.Name switch
        {
            BuiltinNames.List => 1,
            BuiltinNames.Set => 1,
            BuiltinNames.Dict => 2,
            _ => 0
        };

        if (arity > 0 && typeSymbol.TypeParameters.Count == arity)
        {
            var defaultArgs = new List<SemanticType>(arity);
            for (var i = 0; i < arity; i++)
            {
                defaultArgs.Add(SemanticType.Object);
            }

            return new GenericType
            {
                Name = typeSymbol.Name,
                TypeArguments = defaultArgs,
                GenericDefinition = typeSymbol
            };
        }

        return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
    }

    /// <summary>
    /// Extract a key to use for type narrowing from an expression.
    /// Delegates to <see cref="AstHelper.ExtractNarrowingKey"/>.
    /// </summary>
    private string? ExtractNarrowingKey(Expression expr) => AstHelper.ExtractNarrowingKey(expr);

    /// <summary>
    /// Returns true if a statement body unconditionally transfers control out of the
    /// enclosing block (ends with return/raise/break/continue, possibly via an if
    /// statement whose branches all exit). Used to propagate inverse type narrowing
    /// past early-exit guards like <c>if x is None: return</c> (#817).
    /// </summary>
    private static bool BodyExitsUnconditionally(System.Collections.Immutable.ImmutableArray<Statement> body)
        => body.Length > 0 && StatementExitsUnconditionally(body[^1]);

    /// <summary>
    /// Returns true if executing the statement always transfers control out of the
    /// enclosing block. Conservative: only recognizes direct exit statements and
    /// if statements whose then/elif/else branches all exit.
    /// </summary>
    private static bool StatementExitsUnconditionally(Statement statement) => statement switch
    {
        ReturnStatement or RaiseStatement or BreakStatement or ContinueStatement => true,
        IfStatement ifStmt => ifStmt.ElseBody.Length > 0
            && BodyExitsUnconditionally(ifStmt.ThenBody)
            && ifStmt.ElifClauses.All(elif => BodyExitsUnconditionally(elif.Body))
            && BodyExitsUnconditionally(ifStmt.ElseBody),
        _ => false
    };

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

        // Non-nullable type can be assigned to nullable version of the same type.
        // Recurse through IsAssignable (not just IsAssignableTo) so the underlying-type check
        // also benefits from the CLR-metadata fallback below — this is what lets a builtin
        // `bytes` argument bind to a `Bytes?` (Nullable<Bytes>) parameter (#890).
        if (target is NullableType nullable)
        {
            return IsAssignable(source, nullable.UnderlyingType);
        }

        // Non-optional type can be assigned to optional version of the same type
        if (target is OptionalType optional)
        {
            return IsAssignable(source, optional.UnderlyingType);
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

        // Generic variance (#827): same-name generics check per-type-parameter variance
        // from the definition's TypeParameterDefs; different-name generics check
        // assignability through implemented interfaces and base classes
        // (e.g., list[int] → IEnumerable[int], MyList[int] → list[int]).
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            var varianceResult = IsGenericAssignableWithVariance(sourceGeneric, targetGeneric);
            if (varianceResult == true)
                return true;
            if (varianceResult == false)
                return false;
            // null → no opinion, continue to CLR fallback
        }

        // CLR fallback: when both types have CLR metadata (e.g., module-discovered types like
        // StringIO and TextWriter that may be different SemanticType subtypes), use reflection
        // to check inheritance. This covers cross-subtype assignability that the standard
        // IsAssignableTo checks miss, including Sharpy collection types (list/dict/set) being
        // passed to CLR parameters expecting IEnumerable, ICollection<T>, etc.
        var sourceClr = TryGetClrType(source);
        var targetClr = TryGetClrType(target);
        if (sourceClr != null && targetClr != null && targetClr.IsAssignableFrom(sourceClr))
            return true;

        return false;
    }

    /// <summary>
    /// Argument-binding assignability: standard <see cref="IsAssignable"/> plus the
    /// list[T] → array[T] coercion for CLR T[] parameters (#959). This is scoped to the
    /// CLR-interop boundary (function/keyword argument binding) on purpose — general
    /// assignment keeps list[T] ↛ array[T] (Decision #2, #944), so e.g.
    /// <c>arr: array[int] = lst</c> stays an error. The matching codegen .ToArray()
    /// bridge lives in <c>RoslynEmitter.ApplyArrayBridge</c>; the two must agree on which
    /// arguments are coercible.
    /// </summary>
    private bool IsArgumentAssignable(SemanticType source, SemanticType target)
    {
        if (IsAssignable(source, target))
            return true;

        // list[T] → array[T]: element types must match exactly (UnknownType acts as a
        // wildcard for empty list literals) so codegen's .ToArray() produces an array of
        // the parameter's element type. Using IsAssignable on the element would wrongly
        // permit list[int] → array[float] (numeric widening), whose int[] cannot bind to
        // a C# double[].
        if (source is GenericType { Name: "list" } listType
            && target is GenericType { Name: "array" } arrayType
            && listType.TypeArguments.Count == 1
            && arrayType.TypeArguments.Count == 1
            && (listType.TypeArguments[0] is UnknownType
                || arrayType.TypeArguments[0] is UnknownType
                || listType.TypeArguments[0].Equals(arrayType.TypeArguments[0])))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks generic-to-generic assignability using per-type-parameter variance (#827).
    /// Same-name generics compare each argument position under the definition's declared
    /// variance; different-name generics walk the source's instantiated supertypes
    /// (interfaces and base classes) to find one matching the target, then apply the
    /// supertype definition's variance.
    /// Returns <c>true</c> when assignable (variance satisfied), <c>false</c> when
    /// authoritatively rejected (a matching definition was found but variance is
    /// violated), and <c>null</c> when no opinion (no matching definition/supertype
    /// found; the CLR reflection fallback is appropriate) (#829).
    /// </summary>
    private bool? IsGenericAssignableWithVariance(GenericType source, GenericType target)
    {
        if (source.Name == target.Name)
        {
            if (source.TypeArguments.Count != target.TypeArguments.Count)
                return false;

            var definition = GenericInstantiationWalker.ResolveDefinition(source, _symbolTable);
            if (definition == null || definition.TypeParameters.Count != source.TypeArguments.Count)
                return null;

            return TypeArgumentsSatisfyVariance(
                definition.TypeParameters, source.TypeArguments, target.TypeArguments);
        }

        // Interface or base-class assignment: find an instantiated supertype of the
        // source matching the target's name and arity.
        var rejected = false;
        foreach (var supertype in GenericInstantiationWalker.EnumerateSupertypes(
                     source, _symbolTable, SemanticBinding, _typeResolver))
        {
            if (supertype.Definition.Name != target.Name
                || supertype.TypeArguments.Count != target.TypeArguments.Count)
            {
                continue;
            }

            if (TypeArgumentsSatisfyVariance(
                    supertype.Definition.TypeParameters, supertype.TypeArguments, target.TypeArguments))
            {
                return true;
            }

            rejected = true;
        }

        return rejected ? false : (bool?)null;
    }

    /// <summary>
    /// Checks each type-argument position under the corresponding type parameter's
    /// declared variance: covariant (out) positions require source → target
    /// assignability, contravariant (in) positions require target → source, and
    /// invariant positions require equivalent types.
    /// </summary>
    private bool TypeArgumentsSatisfyVariance(
        IReadOnlyList<TypeParameterDef> typeParameters,
        IReadOnlyList<SemanticType> sourceArguments,
        IReadOnlyList<SemanticType> targetArguments)
    {
        for (int i = 0; i < sourceArguments.Count; i++)
        {
            var variance = i < typeParameters.Count
                ? typeParameters[i].Variance
                : TypeParameterVariance.None;
            var sourceArg = sourceArguments[i];
            var targetArg = targetArguments[i];

            // UnknownType acts as a wildcard — allows empty collection literals
            // (list[<?>], dict[<?>,<?>]) to satisfy any argument position.
            if (sourceArg is UnknownType || targetArg is UnknownType)
                continue;

            var satisfied = variance switch
            {
                TypeParameterVariance.Covariant => IsAssignable(sourceArg, targetArg),
                TypeParameterVariance.Contravariant => IsAssignable(targetArg, sourceArg),
                _ => sourceArg.Equals(targetArg)
                     || (sourceArg.IsAssignableTo(targetArg) && targetArg.IsAssignableTo(sourceArg)),
            };

            if (!satisfied)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Attempts to resolve a CLR <see cref="Type"/> for a <see cref="SemanticType"/>, including
    /// constructing concrete generic types for Sharpy collection generics (list/dict/set). This
    /// enables CLR assignability checks (e.g., passing a <c>list[int]</c> to a method parameter
    /// typed as <c>IEnumerable</c>).
    /// </summary>
    private Type? TryGetClrType(SemanticType type)
    {
        switch (type)
        {
            case BuiltinType bt:
                return bt.ClrType;
            case UserDefinedType udt:
                return udt.Symbol?.ClrType;
            case NullableType nt:
                return TryGetClrType(nt.UnderlyingType);
            case OptionalType ot:
                return TryGetClrType(ot.UnderlyingType);
            case GenericType gt:
                {
                    if (gt.Name == BuiltinNames.Array && gt.TypeArguments.Count == 1)
                    {
                        var elemClr = TryGetClrType(gt.TypeArguments[0]);
                        return elemClr?.MakeArrayType();
                    }

                    Type? openType = gt.Name switch
                    {
                        "list" => typeof(SharpyRT::Sharpy.List<>),
                        "dict" => typeof(SharpyRT::Sharpy.Dict<,>),
                        "set" => typeof(SharpyRT::Sharpy.Set<>),
                        _ => null,
                    };
                    if (openType == null)
                    {
                        var candidateClr = gt.GenericDefinition?.ClrType
                            ?? _symbolTable.LookupType(gt.Name)?.ClrType;
                        if (candidateClr != null && candidateClr.IsGenericTypeDefinition
                            && candidateClr.GetGenericArguments().Length == gt.TypeArguments.Count)
                        {
                            openType = candidateClr;
                        }
                        else if (candidateClr != null && !candidateClr.IsGenericTypeDefinition)
                        {
                            // ClrType is non-generic (e.g., IEnumerable instead of IEnumerable<T>).
                            // Search loaded assemblies for the generic version with matching arity.
                            var arity = gt.TypeArguments.Count;
                            var genericName = candidateClr.Name.Contains('`', StringComparison.Ordinal)
                                ? candidateClr.Name
                                : candidateClr.Name + "`" + arity;
                            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                            {
                                try
                                {
                                    var match = asm.GetType(candidateClr.Namespace + "." + genericName)
                                        ?? asm.GetTypes().FirstOrDefault(t =>
                                            t.IsGenericTypeDefinition
                                            && Shared.ClrNameHelper.StripArity(t.Name) == gt.Name
                                            && t.GetGenericArguments().Length == arity);
                                    if (match != null)
                                    {
                                        openType = match;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    if (openType == null || !openType.IsGenericTypeDefinition)
                        return null;
                    var clrTypeArgs = gt.TypeArguments.Select(ta => TryGetClrType(ta) ?? typeof(object)).ToArray();
                    if (clrTypeArgs.Length != openType.GetGenericArguments().Length)
                        return null;
                    try
                    {
                        return openType.MakeGenericType(clrTypeArgs);
                    }
                    catch
                    {
                        return null;
                    }
                }
            default:
                return type.ClrType ?? type.DeclaringSymbol?.ClrType;
        }
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
                Type = TypeSubstitution.Apply(p.Type, substitutions)
            }).ToList();
            var substitutedReturn = TypeSubstitution.Apply(invoke.ReturnType, substitutions);

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
        List<SemanticType> typeArgs,
        bool substituteNamedUserTypes = false)
    {
        if (typeParams.Count != typeArgs.Count)
            return type;

        var substitutions = new Dictionary<string, SemanticType>();
        for (int i = 0; i < typeParams.Count; i++)
        {
            substitutions[typeParams[i].Name] = typeArgs[i];
        }

        return TypeSubstitution.Apply(type, substitutions, substituteNamedUserTypes);
    }

    /// <summary>
    /// Recursively checks whether <paramref name="type"/> references a type parameter with the
    /// given <paramref name="name"/> (used to determine where a specific generic parameter appears).
    /// </summary>
    private static bool ReferencesTypeParameterNamed(SemanticType type, string name)
    {
        return type switch
        {
            TypeParameterType tp => tp.Name == name,
            ResultType rt => ReferencesTypeParameterNamed(rt.OkType, name) || ReferencesTypeParameterNamed(rt.ErrorType, name),
            OptionalType ot => ReferencesTypeParameterNamed(ot.UnderlyingType, name),
            NullableType nt => ReferencesTypeParameterNamed(nt.UnderlyingType, name),
            GenericType gt => gt.TypeArguments.Any(t => ReferencesTypeParameterNamed(t, name)),
            FunctionType ft => ft.ParameterTypes.Any(t => ReferencesTypeParameterNamed(t, name)) || ReferencesTypeParameterNamed(ft.ReturnType, name),
            TupleType tt => tt.ElementTypes.Any(t => ReferencesTypeParameterNamed(t, name)),
            _ => false
        };
    }

    /// <summary>
    /// Checks if a type contains any unresolved TypeParameterType instances.
    /// Used to detect method-level generic type parameters that need inference.
    /// </summary>
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

                // When the parent has multiple constructor overloads, defer argument
                // validation to the C# compiler — mirroring how direct constructor calls
                // (CheckConstructorCall) skip strict validation for overloaded __init__.
                if (currentType.Constructors.Count > 1)
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
                    return FunctionType.FromParameters(
                        parentCtor.Parameters, SemanticType.Void, skipLeading: 1);
                }
                currentType = GetBaseType(currentType);
            }
        }

        if (parentMethod != null)
        {
            // __init__ is stored in both Methods and Constructors. When the owning type
            // has multiple constructor overloads, defer argument validation to the C#
            // compiler — mirroring how direct constructor calls (CheckConstructorCall)
            // skip strict validation for overloaded __init__.
            if (memberName == DunderNames.Init && methodOwner != null && methodOwner.Constructors.Count > 1)
            {
                return new FunctionType
                {
                    ParameterTypes = new List<SemanticType>(),
                    ReturnType = SemanticType.Void,
                    SkipArgumentValidation = true
                };
            }

            return FunctionType.FromParameters(
                parentMethod.Parameters, parentMethod.ReturnType, skipLeading: 1);
        }

        // Also check properties in the parent hierarchy (e.g. super().age in
        // an @override property getter). Properties generate C# base.Property
        // so they resolve at runtime; we just need the property type here.
        var (parentProperty, _) = FindPropertyInHierarchy(classBaseType, memberName);
        if (parentProperty != null)
            return parentProperty.Type;

        AddError($"No method '{memberName}' found in parent class hierarchy of '{_currentClass.Name}'",
            memberAccess.LineStart, memberAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.UndefinedMember,
            span: memberAccess.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves the constructor (<c>__init__</c>) overload candidates that an initializer call —
    /// <c>super().__init__(...)</c> or <c>self.__init__(...)</c> — could bind to. Walks
    /// <paramref name="targetType"/> and its base hierarchy (via <see cref="GetBaseType"/>) and
    /// returns <em>all</em> overloads of the first type that declares any constructors, mirroring
    /// the hierarchy walk in <see cref="ValidateSuperMemberAccess"/> (first type yielding
    /// constructors wins). For <c>super</c>, callers pass the base type; for <c>self</c>, callers
    /// pass the current class.
    /// <para>
    /// Unlike <see cref="ValidateSuperMemberAccess"/> — which defers argument validation entirely
    /// when a base has more than one overload — this returns the complete overload set so callers
    /// can validate keyword-argument names against the <em>union</em> of overloads.
    /// </para>
    /// <para>
    /// A CLR-backed type (<see cref="TypeSymbol.ClrType"/> != null) whose constructors are not
    /// enumerated in metadata yields an empty list, signalling callers to skip validation rather
    /// than reject otherwise-valid keyword arguments.
    /// </para>
    /// </summary>
    private IReadOnlyList<FunctionSymbol> ResolveInitializerConstructorCandidates(TypeSymbol targetType)
    {
        var currentType = targetType;
        while (currentType != null)
        {
            // First type yielding constructors wins — return the full overload set.
            if (currentType.Constructors.Count > 0)
                return currentType.Constructors;

            // CLR-backed type with no enumerated constructors: no metadata to validate
            // against, so signal callers to skip rather than reject valid kwargs.
            if (currentType.ClrType != null)
                return Array.Empty<FunctionSymbol>();

            currentType = GetBaseType(currentType);
        }

        return Array.Empty<FunctionSymbol>();
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
    /// Records an informational note from the type-checking phase.
    /// Used for non-error suggestions (e.g., recommending idiomatic forms).
    /// </summary>
    private void AddInfo(string message, int? line = null, int? column = null, string? code = null)
    {
        _diagnostics.AddInfo(message, line, column, _currentFilePath, code, CompilerPhase.TypeChecking);
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

    /// <summary>
    /// Registers a scoped type alias in the current symbol table scope.
    /// Used to re-register class-scoped aliases (which are first registered during Pass 1
    /// in a scope that no longer exists) and to register function-scoped aliases.
    /// </summary>
    private void RegisterScopedTypeAlias(TypeAlias typeAlias)
    {
        // Skip if already defined in current scope. This guard is needed because class/struct
        // bodies pre-register aliases before field type resolution (TypeChecker.Definitions.cs),
        // then CheckStatement processes the same TypeAlias node again during the full body pass.
        if (_symbolTable.Lookup(typeAlias.Name, searchParents: false) is TypeAliasSymbol)
            return;

        // Validate that exactly one of Type or FunctionType is set
        if (typeAlias.Type == null && typeAlias.FunctionType == null)
        {
            AddError($"Type alias '{typeAlias.Name}' must have a type",
                typeAlias.LineStart, typeAlias.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        if (typeAlias.Type != null && typeAlias.FunctionType != null)
        {
            AddError($"Type alias '{typeAlias.Name}' cannot have both Type and FunctionType",
                typeAlias.LineStart, typeAlias.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidTypeAlias, span: typeAlias.Span);
            return;
        }

        // Check for redefinition by a non-alias symbol
        var existing = _symbolTable.Lookup(typeAlias.Name, searchParents: false);
        if (existing != null)
        {
            AddError($"Type alias '{typeAlias.Name}' is already defined",
                typeAlias.LineStart, typeAlias.ColumnStart,
                code: DiagnosticCodes.Semantic.DuplicateDefinition, span: typeAlias.Span);
            return;
        }

        _symbolTable.Define(TypeAliasSymbol.CreateFrom(typeAlias));
    }
}
