using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Function calls, member access, index/slice access,
/// module paths, generic builtins
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateCall(FunctionCall call)
    {
        // Handle functools.partial(f, ...) — compatibility shim that emits an equivalent lambda.
        // The spec is recorded by the TypeChecker; the emitter reads it instead of
        // re-classifying via FunctoolsPartialHelper (#1520).
        if (_context.SemanticInfo?.GetFunctoolsPartialSpec(call) is { } partialSpec)
        {
            return GenerateFunctoolsPartialCall(call, partialSpec);
        }

        // A parenthesized callee — (foo)(5), (p.method)(5), (identity[int])(5) — wraps the inner
        // expression in Parenthesized. The semantic phase already pinned the call target through the
        // wrapper (CheckExpression recurses through Parenthesized), so unwrap once here and dispatch
        // every callee-shape arm below on this unwrapped `callee`. Without the unwrap a parenthesized
        // plain-identifier or member-access callee misses every proper arm and falls to the delegate
        // fall-through, emitting (Foo)(5) — which C# re-parses as a cast (CS0246 → SPY0908). Purely
        // structural — no type or lowering decision (#1138 for the generic arms, #1147 for the rest).
        var callee = call.Function;
        while (callee is Parenthesized parenCallee)
            callee = parenCallee.Expression;

        // A generic reference — callee[T, ...] — lowers off the single GenericReference fact the
        // semantic phase materialized on the callee node: Box[int](42), identity[int](42),
        // json.loads[int](text), difflib.SequenceMatcher[str](...), recv.convert[int](x),
        // Outer.Inner[int](42). The resolver already decided the callee kind, its target symbol and
        // its type arguments, so this reads the kind and applies the matching emission body — no
        // per-kind try-cascade and no symbol re-inspection (#1143, #1164, Critical Rule 2 pattern
        // (b)). Every generic-reference shape now arrives as a fact; a callee[T, ...] without one is
        // ordinary value indexing and belongs to the arms below. The fact is keyed on the IndexAccess
        // the TypeChecker saw, which is the unwrapped `callee` above (#1147).
        if (callee is IndexAccess genericReferenceAccess
            && _context.SemanticInfo?.GetGenericReference(genericReferenceAccess) is { } genericReference)
        {
            var result = GenerateGenericReferenceCall(genericReferenceAccess, genericReference, call);
            if (result != null)
                return result;
        }

        // `builtins.dict()` is the BARE `dict()`, spelled so no local binding can capture it. The
        // TypeChecker resolved it against the registry and recorded CalleeRouting.Builtin; applying
        // that fact means emitting the bare spelling's emission, so the callee becomes the bare name
        // and the arm below reads the registry instead of the collapsed scope. Without this the
        // qualified syntax survived into C# as `Sharpy.Builtins.Dict()`, which names no method
        // (#1322).
        var isBuiltinsQualified = callee is MemberAccess { IsNullConditional: false }
            && _context.SemanticInfo?.GetCalleeRouting(call) == CalleeRouting.Builtin;
        if (isBuiltinsQualified && callee is MemberAccess builtinsQualified)
        {
            var resolvedName = _context.SemanticInfo?.GetCalleeAliasTargetName(call)
                ?? builtinsQualified.Member;
            callee = new Identifier
            {
                Name = resolvedName,
                LineStart = builtinsQualified.LineStart,
                ColumnStart = builtinsQualified.ColumnStart,
                LineEnd = builtinsQualified.LineEnd,
                ColumnEnd = builtinsQualified.ColumnEnd,
                Span = builtinsQualified.Span
            };
        }
        // Type alias transparency (#1527, #1587): an alias callee routed to Builtin needs
        // its name rewritten to the TARGET's name so the emitter's name-keyed paths find it.
        // Also applies after the builtins-qualified rewrite above: `lib.Handle("42")` where
        // Handle aliases int becomes Identifier("Handle") which then needs rewriting to "int".
        if (_context.SemanticInfo?.GetCalleeRouting(call) == CalleeRouting.Builtin
            && callee is Identifier aliasId)
        {
            var aliasSymbol = _context.LookupSymbol(aliasId.Name) as TypeAliasSymbol;
            if (aliasSymbol?.TypeAnnotation != null)
            {
                callee = new Identifier
                {
                    Name = aliasSymbol.TypeAnnotation.Name,
                    LineStart = aliasId.LineStart,
                    ColumnStart = aliasId.ColumnStart,
                    LineEnd = aliasId.LineEnd,
                    ColumnEnd = aliasId.ColumnEnd,
                    Span = aliasId.Span
                };
            }
        }

        if (callee is Identifier funcName)
        {
            // The TypeChecker records whether this call targets a builtin or a user symbol
            // while scopes are live. At codegen time scopes are collapsed (only the global scope
            // is visible), so the emitter's own lookup would see the global-seeded builtin even
            // when a local variable shadows it. Read the semantic fact first (#1326).
            var calleeRouting = _context.SemanticInfo?.GetCalleeRouting(call);

            bool isBuiltinFunc;
            if (calleeRouting == CalleeRouting.UserSymbol)
            {
                isBuiltinFunc = false;
            }
            else
            {
                isBuiltinFunc = _context.IsBuiltinFunction(funcName.Name)
                                && !funcName.IsNameBacktickEscaped;
            }

            var symbol = _context.LookupSymbol(funcName.Name);
            if (symbol != null && funcName.IsNameBacktickEscaped != symbol.IsNameBacktickEscaped)
                symbol = null;

            // `from builtins import len as blen` binds the alias's SPELLING to the registry's own
            // symbol. Every arm below is keyed by the callee's NAME — the generic-builtin list, the
            // str-len special case, the mangled Builtins.<Name> call — so become the builtin's
            // spelling here rather than teaching each of them about aliases. This is the same
            // rewrite the builtins-qualified arm above performs, for the same reason: the semantic
            // phase already decided what this call targets, and the emitter applies that decision
            // (#1383). The span is untouched, so diagnostics still point at what the user wrote.
            if (symbol?.BuiltinAliasOf is { } aliasedBuiltin)
            {
                funcName = funcName with { Name = aliasedBuiltin.Name };
                isBuiltinFunc = calleeRouting != CalleeRouting.UserSymbol
                                && !funcName.IsNameBacktickEscaped;
            }

            // The one thing the qualified spelling changes is WHERE the symbol comes from: the
            // registry, never the scope. Everything after this is bare's own derivation, unchanged,
            // so the two spellings emit the same C# for the same name — including the split that
            // makes `str(5)` the conversion function while `dict()` constructs. Being immune to
            // shadowing is the whole point of writing `builtins.`, so the shadow adjustments below
            // are skipped for it (#1322).
            if (isBuiltinsQualified)
            {
                symbol = (Symbol?)_context.SymbolTable.BuiltinRegistry.GetType(funcName.Name)
                    ?? _context.SymbolTable.BuiltinRegistry.GetFunction(funcName.Name);
            }
            // When the TypeChecker says a user binding shadows the builtin, the scope-collapsed
            // lookup answer (the global-seeded builtin) is wrong — null it so the emitter does
            // not treat the call as a type instantiation or constructor (#1326).
            if (calleeRouting == CalleeRouting.UserSymbol && symbol != null
                && _context.SymbolTable.BuiltinRegistry.IsBuiltinSymbol(symbol))
                symbol = null;
            if (!isBuiltinsQualified && isBuiltinFunc && symbol != null
                && !_context.SymbolTable.BuiltinRegistry.IsBuiltinSymbol(symbol))
                isBuiltinFunc = false;
            if (!isBuiltinsQualified && isBuiltinFunc && !funcName.IsNameBacktickEscaped
                && _localFunctionNames.ContainsKey(funcName.Name))
                isBuiltinFunc = false;

            // Handle direct calls to asyncio functions (from asyncio import gather, sleep)
            if (symbol is FunctionSymbol { OriginalModule: Shared.SyntheticModuleNames.Asyncio })
            {
                return GenerateAsyncioCall(funcName.Name, call);
            }

            // isinstance(expr, T) → expr is T, against the type the semantic phase decided the
            // operand denotes. Must intercept BEFORE argument evaluation because the second argument
            // names a type, not a value.
            //
            // WHAT THE OPERAND DENOTES IS NOT DECIDED HERE (Critical Rule 2). This arm used to read
            // the operand expression's shape and map it by name — `MapType(new TypeAnnotation { Name
            // = typeId.Name })` — which is how a bare generic became the unspellable `Box<T>`
            // (CS0305 → SPY0908, #1207) and a tuple of type names became a tuple of method groups
            // (CS1503, #1213). The TypeChecker's type-operand classifier now resolves the operand
            // once and rejects at semantic time every shape that has no single closed type, so an
            // un-lowerable operand never arrives here and this arm only applies what it was given.
            // An operand with no recorded lowering is not a type test at all (a shadowed
            // `isinstance`, a `System.Type` value) and falls through to the ordinary call below.
            if (funcName.Name == BuiltinFunctionNames.IsInstance
                && call.Arguments.Length == 2
                && _context.SemanticInfo?.GetTypeTestLowering(call.Arguments[1]) is { } typeTest)
            {
                var value = GenerateExpression(call.Arguments[0]);
                return BinaryExpression(SyntaxKind.IsExpression, value, MapTypeTestTarget(typeTest));
            }

            // Check if this is a type instantiation (calling a class or struct constructor)
            // We use the symbol table which is populated during semantic analysis.
            // This handles both local type definitions and imported types.
            // NOTE: Builtin functions are NOT type instantiations (e.g., int(x) is a conversion function)
            var isTypeInstantiation = !isBuiltinFunc &&
                                      symbol is TypeSymbol typeSymbol &&
                                      (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
                                       typeSymbol.TypeKind == Semantic.TypeKind.Struct);

            // Resolve the callee FunctionSymbol for argument reordering.
            // For type instantiations, look up the constructor from the TypeSymbol.
            // Fall back to the semantic-info call target for CLR types whose constructors
            // are not populated by CachedModuleDiscovery.
            FunctionSymbol? directCallTarget = symbol as FunctionSymbol;
            if (directCallTarget == null && symbol is TypeSymbol callTypeSymbol)
            {
                directCallTarget = ResolveConstructorForCall(callTypeSymbol, call)
                    ?? _context.SemanticInfo?.GetCallTarget(call);
            }
            // Decided BEFORE the generic argument generation: this lowering builds its own
            // argument list from the same AST nodes, so generating them here first produced every
            // argument and the key expression twice and discarded one set. GenerateExpression is
            // not pure — it can push into `_hoistedStatements`, which are flushed unconditionally
            // — so a speculative generation is a duplicated side effect waiting for the right
            // argument (#1228's rule, found live by the re-entry tripwire, #1334).
            if (isBuiltinFunc)
            {
                // Variadic value form of min()/max() with key= → route to the iterable+key
                // overload to avoid CS1744 from the params-overload's missing key slot (#1012).
                var minMaxValueFormWithKey = TryGenerateMinMaxValueFormWithKey(call, funcName);
                if (minMaxValueFormWithKey != null)
                    return minMaxValueFormWithKey;
            }

            var allArgs = GenerateReorderedCallArguments(call, directCallTarget);

            if (isBuiltinFunc)
            {
                // Generic builtins need explicit type arguments
                if (funcName.Name is BuiltinNames.Reversed or BuiltinNames.Sorted)
                {
                    return GenerateGenericBuiltinCall(funcName.Name, call, allArgs);
                }

                // iter(s) on strings → StringHelpers.Iterate(s), which yields one-character STRINGS.
                // Sharpy.Builtins.Iter<T>(IEnumerable<T>) would bind T = char on a CLR string and
                // hand back an Iterator<char>, disagreeing with the Iterator[str] the checker
                // records for this call — the CS0266-behind-SPY0908 shape the StringHelpers.Reversed
                // remarks describe. Mirrors the reversed(s) arm below (#1468).
                if (funcName.Name == BuiltinNames.Iter && call.Arguments.Length == 1
                    && GetExpressionSemanticType(call.Arguments[0]) == SemanticType.Str)
                {
                    return InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                            IdentifierName("Iterate")))
                        .AddArgumentListArguments(Argument(allArgs[0].Expression));
                }

                // len(s) on strings → s.Length (string doesn't implement ISized)
                if (funcName.Name == "len" && call.Arguments.Length == 1
                    && GetExpressionSemanticType(call.Arguments[0]) == SemanticType.Str)
                {
                    return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        allArgs[0].Expression,
                        IdentifierName("Length"));
                }

                // Use explicit AliasQualifiedName to handle all expression contexts (f-strings, etc.)
                var builtinName = MakeGlobalQualifiedName("Sharpy", "Builtins", NameCasing.ResolveMethod(funcName.Name, funcName.IsNameBacktickEscaped));
                return InvocationExpression(builtinName)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            if (isTypeInstantiation && symbol is TypeSymbol typeSymbolForName)
            {
                // For type instantiation, use fully qualified name if type is from another file.
                // For aliased imports (e.g., "from helper import Config as Cfg"), resolve the
                // original type name so we generate "Helper.Config", not "Helper.Cfg".
                var originalTypeName = GetCodeGenInfo(typeSymbolForName)?.OriginalImportName ?? funcName.Name;
                var name = GetFullyQualifiedTypeName(typeSymbolForName, originalTypeName);

                // Generic instantiation resolves the name through CSharpTypeNames (builtin
                // collections: set → Sharpy.Set) with a PascalCase fallback for user types.
                var genericBaseName = ClrTypeBridge.SpecialCases.TryGetWrapperCollectionName(funcName.Name)
                    ?? NameCasing.ResolveType(funcName.Name, funcName.IsNameBacktickEscaped);

                var exprType = _context.SemanticInfo?.GetExpressionType(call);

                // dict(a=1, b=2): the keyword NAMES are the keys, so this is the
                // collection-initializer form of the equivalent dict literal, not a C#
                // named-argument call. Passing them through the argument list is CS1739 (#1220).
                // The dict[str, V] this reads was resolved by the TypeChecker.
                if (call.KeywordArguments.Length > 0 && call.Arguments.Length == 0
                    && exprType is GenericType { Name: BuiltinNames.Dict } keywordDictType
                    && keywordDictType.TypeArguments.Count == 2
                    && keywordDictType.TypeArguments.All(t => t is not UnknownType))
                {
                    return GenerateKeywordDictConstruction(call, keywordDictType);
                }

                var hasResolvedGenericArgs = exprType is GenericType resolvedGeneric
                    && resolvedGeneric.TypeArguments.Count > 0
                    && resolvedGeneric.TypeArguments.All(t => t is not UnknownType);
                if (hasResolvedGenericArgs)
                {
                    // DefaultDict: wrap type-reference arguments in factory lambdas.
                    // DefaultDict(list) → new DefaultDict<string, List<int>>(() => new List<int>())
                    var genericExprType = (GenericType)exprType!;
                    if (string.Equals(funcName.Name, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase)
                        && genericExprType.TypeArguments.Count >= 2
                        && call.Arguments.Length >= 1)
                    {
                        var valueTypeSyntax = _typeMapper.MapSemanticType(genericExprType.TypeArguments[1]);
                        allArgs = WrapDefaultDictFactoryArgs(call, allArgs, valueTypeSyntax);
                    }
                }
                else if (_context.SemanticInfo?.GetInferredTypeArguments(call) is not { Count: > 0 })
                {
                    // For builtin collection types, use the fully-qualified Sharpy.X name.
                    // If we reached here, neither the expression type nor inference supplied
                    // generic args. Try to infer element type from the constructor argument:
                    // list(d.keys()) → new Sharpy.List<string>(d.Keys) when d.keys() is IEnumerable<string>
                    var collectionName = ClrTypeBridge.SpecialCases.TryGetWrapperCollectionName(funcName.Name);
                    if (collectionName != null)
                    {
                        var needsGlobalQualification = !string.IsNullOrEmpty(_context.ProjectNamespace)
                            && collectionName.Contains('.', StringComparison.Ordinal);
                        if (call.Arguments.Length == 1)
                        {
                            var elementType = TryInferElementTypeFromArg(call.Arguments[0]);
                            if (elementType != null)
                            {
                                var elementTypeSyntax = _typeMapper.MapSemanticType(elementType);
                                var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(
                                    collectionName, needsGlobalQualification, elementTypeSyntax);
                                return ObjectCreationExpression(genericTypeSyntax)
                                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                            }
                        }
                        NameSyntax collectionTypeSyntax = needsGlobalQualification
                            ? MakeGlobalQualifiedName(collectionName.Split('.'))
                            : ParseQualifiedName(collectionName);
                        return ObjectCreationExpression(collectionTypeSyntax)
                            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                    }
                }

                return GenerateTypeInstantiation(call, name, allArgs, genericBaseName);
            }

            // Regular function call — check if this is a local variable/parameter (callable)
            // before falling back to PascalCase for module-level functions.
            // The slot must belong to THIS spelling, exactly as the read path in
            // GetMangledVariableName requires: the slot key collapses case, so a call to the module
            // function `Zed` found the slot of a local or parameter `zed` and emitted `zed()` on an
            // int — CS0149, surfacing as SPY0908 (#1276).
            var codeGenInfo = symbol != null ? GetCodeGenInfo(symbol) : null;
            string funcCSharpName;
            if (IsLocalSlotInScope(funcName.Name, funcName.IsNameBacktickEscaped))
            {
                funcCSharpName = GetMangledVariableName(funcName.Name, isNewDeclaration: false, funcName.IsNameBacktickEscaped);
            }
            else if (codeGenInfo?.CSharpName != null)
            {
                funcCSharpName = codeGenInfo.CSharpName;
            }
            else
            {
                funcCSharpName = NameCasing.ResolveMethod(funcName.Name, funcName.IsNameBacktickEscaped, GetClrMethodName(symbol));
            }

            // If the callee is a narrowed Optional delegate (e.g., `if cb is not None: cb(x)`),
            // generate through GenerateExpression so the recorded accessor (.Unwrap()/!) is applied
            // before invocation. A lowering on the callee node is exactly this narrowed-read signal.
            // The narrowing fact is recorded on the inner node (CheckExpression recurses through
            // Parenthesized), so look it up via the unwrapped `callee`, not the wrapped call.Function.
            ExpressionSyntax calleeExpr;
            if (_context.SemanticInfo?.GetNarrowedReadLowering(callee) != null)
                calleeExpr = GenerateExpression(callee);
            else
                calleeExpr = ParseQualifiedName(funcCSharpName);
            return InvocationExpression(calleeExpr)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Handle method calls on objects: obj.method() or ClassName.static_method()
        if (callee is MemberAccess memberAccess)
        {
            // Check for asyncio module calls: asyncio.gather() → Task.WhenAll(), asyncio.sleep() → Task.Delay()
            if (memberAccess.Object is Identifier asyncioId && asyncioId.Name == Shared.SyntheticModuleNames.Asyncio)
            {
                var asyncioSym = _context.LookupSymbol(asyncioId.Name);
                if (asyncioSym is ModuleSymbol)
                {
                    return GenerateAsyncioCall(memberAccess.Member, call);
                }
            }

            // Check for union case construction: Shape.Circle(5.0) → new Shape.Circle(5.0)
            if (memberAccess.Object is Identifier unionId)
            {
                var unionSym = _context.LookupSymbol(unionId.Name);
                if (unionSym is TypeSymbol { TypeKind: Semantic.TypeKind.Union } unionTypeSym)
                {
                    var unionCSharpName = NameMangler.Transform(unionId.Name, NameContext.Type);
                    var caseCSharpName = NameCasing.ResolveType(memberAccess.Member, isBacktickEscaped: memberAccess.IsMemberBacktickEscaped);

                    // For generic unions, include type arguments: Option<int>.Some(42)
                    NameSyntax unionNameSyntax;
                    if (unionTypeSym.IsGeneric)
                    {
                        var exprType = _context.SemanticInfo?.GetExpressionType(call);
                        if (exprType is GenericType resolvedGeneric && resolvedGeneric.TypeArguments.Count > 0
                            && resolvedGeneric.TypeArguments.All(t => t is not UnknownType))
                        {
                            var typeArgsSyntax = resolvedGeneric.TypeArguments
                                .Select(t => _typeMapper.MapSemanticType(t))
                                .ToArray();
                            unionNameSyntax = GenericName(Identifier(unionCSharpName))
                                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));
                        }
                        else
                        {
                            unionNameSyntax = IdentifierName(unionCSharpName);
                        }
                    }
                    else
                    {
                        unionNameSyntax = IdentifierName(unionCSharpName);
                    }

                    var qualifiedCaseName = QualifiedName(unionNameSyntax, IdentifierName(caseCSharpName));

                    var caseCallTarget = _context.SemanticInfo?.GetCallTarget(call);
                    var caseAllArgs = GenerateReorderedCallArguments(call, caseCallTarget);

                    return ObjectCreationExpression(qualifiedCaseName)
                        .WithArgumentList(ArgumentList(SeparatedList(caseAllArgs)));
                }
            }

            // Check for nested type construction: Outer.Inner(42) → new Outer.Inner(42)
            // Also handles multi-level: Outer.Middle.Inner(42)
            // Module-qualified: lib.Registry.Entry(9) → new Lib.Registry.Entry(9) (#1523)
            {
                var nestedSym = ResolveNestedTypeFromAccess(memberAccess);
                if (nestedSym != null && (nestedSym.TypeKind == Semantic.TypeKind.Class ||
                                          nestedSym.TypeKind == Semantic.TypeKind.Struct))
                {
                    var typeArgsSyntax = ResolvedConstructionTypeArguments(call);
                    NameSyntax qualifiedName;

                    // For nested types from other modules, use TypeSyntaxMapper to emit
                    // the full module-qualified declaring chain (#1523, mirrors #1435).
                    if (!string.IsNullOrEmpty(nestedSym.DefiningModule)
                        || (!string.IsNullOrEmpty(nestedSym.DefiningFilePath)
                            && !string.Equals(nestedSym.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var mappedType = _typeMapper.MapSemanticType(
                            new Semantic.UserDefinedType { Name = nestedSym.Name, Symbol = nestedSym });
                        if (typeArgsSyntax is { Length: > 0 } && mappedType is NameSyntax baseName)
                        {
                            qualifiedName = baseName;
                        }
                        else if (mappedType is NameSyntax nameSyntax)
                        {
                            qualifiedName = nameSyntax;
                        }
                        else
                        {
                            qualifiedName = BuildNestedTypeName(nestedSym, typeArgsSyntax);
                        }
                    }
                    else
                    {
                        qualifiedName = BuildNestedTypeName(nestedSym, typeArgsSyntax);
                    }

                    var nestedCallTarget = ResolveConstructorForCall(nestedSym, call);
                    var nestedAllArgs = GenerateReorderedCallArguments(call, nestedCallTarget);

                    return ObjectCreationExpression(qualifiedName)
                        .WithArgumentList(ArgumentList(SeparatedList(nestedAllArgs)));
                }
            }

            // Check for module-qualified constructor call: fractions.Fraction(1, 2) →
            // new global::...Fraction(1, 2). Resolve the member through the module-export
            // machinery; if it is an exported class/struct TypeSymbol, emit object creation
            // (routing through the shared instantiation helper for generic-arg handling).
            {
                var moduleType = TryResolveModuleExportedType(memberAccess);
                if (moduleType is { } mt
                    && (mt.Symbol.TypeKind == Semantic.TypeKind.Class
                        || mt.Symbol.TypeKind == Semantic.TypeKind.Struct))
                {
                    var ctorTarget = ResolveConstructorForCall(mt.Symbol, call)
                        ?? _context.SemanticInfo?.GetCallTarget(call);
                    var ctorArgs = GenerateReorderedCallArguments(call, ctorTarget);
                    var baseName = GetFullyQualifiedTypeName(mt.Symbol, mt.OriginalName);
                    return GenerateTypeInstantiation(call, baseName, ctorArgs);
                }
            }

            // Handle static method calls on primitive types: int.parse(s), float.parse(s),
            // bytes.fromhex(s). The TypeChecker records these via SetMemberAccessResolution;
            // the emitter reads the recorded fact only (Rule 2) — there is deliberately no
            // name-based fallback, because a missing resolution means the call was never
            // semantically checked and must not be silently forwarded to Roslyn (#1347).
            var staticResolution = _context.SemanticInfo?.GetMemberAccessResolution(memberAccess);
            if (staticResolution is { } sr && sr.Member is FunctionSymbol { IsStatic: true } staticMethod)
            {
                var staticCallTarget = GetPrimitiveStaticCallTarget(sr.Owner.Name, staticMethod.Name);
                if (staticCallTarget != null)
                {
                    var staticArgs = call.Arguments.Select(a => Argument(GenerateExpression(a))).ToArray();
                    return InvocationExpression(ParseQualifiedName(staticCallTarget))
                        .WithArgumentList(ArgumentList(SeparatedList(staticArgs)));
                }
            }

            // Handle static method calls on generic CLR types: Comparer[object].create(cmp)
            // IndexAccess(TypeName, TypeArgs) must emit GenericName<TypeArgs> (angle brackets),
            // not ElementAccess[TypeArgs] (square brackets).
            if (memberAccess.Object is IndexAccess genericStaticIndexAccess
                && genericStaticIndexAccess.Object is Identifier genericStaticTypeId)
            {
                var genericStaticSym = _context.LookupSymbol(genericStaticTypeId.Name);
                if (genericStaticSym is TypeSymbol { IsGeneric: true })
                {
                    var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(genericStaticIndexAccess.Index);
                    var csharpTypeName = NameCasing.ResolveType(genericStaticTypeId.Name, genericStaticTypeId.IsNameBacktickEscaped);
                    var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpTypeName, typeArgsSyntax);

                    var genericMethodSym = (Symbol?)_context.SemanticInfo?.GetCallTarget(call)
                        ?? _context.SemanticInfo?.GetMemberAccessResolution(memberAccess)?.Member;
                    var genericClrMethodName = GetClrMethodName(genericMethodSym);
                    var genericMethodName = DunderMapping.ResolveCSharpName(memberAccess.Member)
                        ?? NameCasing.ResolveMethod(memberAccess.Member, memberAccess.IsMemberBacktickEscaped, genericClrMethodName);

                    var genericCallArgs = GenerateReorderedCallArguments(call, genericMethodSym as FunctionSymbol);

                    return InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            genericTypeSyntax,
                            IdentifierName(genericMethodName)))
                        .WithArgumentList(ArgumentList(SeparatedList(genericCallArgs)));
                }
            }

            // Narrowed Optional delegate field invocation: self._cb(msg) after
            // `if self._cb is not None`. The callee is a delegate-typed field, not a
            // method — generate through GenerateMemberAccess so the recorded narrowing accessor
            // (.Unwrap()/!) is applied to the callee, then invoke the resulting delegate.
            if (_context.SemanticInfo?.GetNarrowedReadLowering(memberAccess) != null
                && GetExpressionSemanticType(memberAccess) is Semantic.FunctionType)
            {
                var delegateCallee = GenerateMemberAccess(memberAccess);
                var delegateArgs = GenerateReorderedCallArguments(call, funcSymbol: null);
                return InvocationExpression(ParenthesizedExpression(delegateCallee))
                    .WithArgumentList(ArgumentList(SeparatedList(delegateArgs)));
            }

            var obj = GenerateExpression(memberAccess.Object);

            // Cross-dunder calls: transform operator dunders to C# operator expressions.
            // e.g., self.__lt__(other) → this < other, self.__neg__() → -this
            // This must happen BEFORE regular method name resolution so that operator dunders
            // emit operators instead of method calls. Unknown dunders are now compile errors (SPY0414).
            if (DunderMapping.IsDunderMethod(memberAccess.Member))
            {
                var binaryKind = DunderMapping.TryGetBinaryExpressionKind(memberAccess.Member);
                if (binaryKind != null && call.Arguments.Length == 1)
                {
                    var arg = GenerateExpression(call.Arguments[0]);
                    return BinaryExpression(binaryKind.Value, obj, arg);
                }

                var unaryKind = DunderMapping.TryGetUnaryExpressionKind(memberAccess.Member);
                if (unaryKind != null && call.Arguments.Length == 0)
                {
                    return PrefixUnaryExpression(unaryKind.Value, obj);
                }
            }

            // Apply name mangling to method name
            // First check for dunder methods, then Python list method mappings (append -> Add, etc.)
            // For discovery-loaded CLR methods, prefer the original CLR name (preserves acronym casing).
            var resolvedMethodSymbol = (Symbol?)_context.SemanticInfo?.GetCallTarget(call)
                ?? _context.SemanticInfo?.GetMemberAccessResolution(memberAccess)?.Member;
            var resolvedClrMethodName = GetClrMethodName(resolvedMethodSymbol)
                ?? GetIrResolvedClrMemberName(memberAccess);
            // The word-boundary table (setdefault/popitem, #1069) applies only to the builtin
            // collections it was written for. A discovered type may deliberately spell a method to
            // reverse-mangle cleanly — OrderedDict's one-capital `Popitem` demangles to `popitem`,
            // so `od.popitem()` resolves; forcing the table's `PopItem` there would reference a
            // nonexistent member (CS1061). Gate the table on a builtin-collection receiver.
            var receiverType = GetExpressionSemanticType(memberAccess.Object);
            var isBuiltinCollectionReceiver =
                receiverType is GenericType { Name: BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set };
            var methodName = DunderMapping.ResolveCSharpName(memberAccess.Member)
                ?? (isBuiltinCollectionReceiver ? NameMangler.GetCollectionMethodMapping(memberAccess.Member) : null)
                ?? NameCasing.ResolveMethod(memberAccess.Member, memberAccess.IsMemberBacktickEscaped, resolvedClrMethodName);

            // CLR property access: if the member is a property (not a method) on a
            // discovery-loaded type and the call has no arguments, emit property access
            // without invocation parens. E.g., Python d.keys() → C# d.Keys (not d.Keys()).
            // The decision is recorded in SemanticInfo by the TypeChecker (#1519).
            if (_context.SemanticInfo?.IsClrPropertyCall(call) == true)
            {
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    obj,
                    IdentifierName(methodName));
            }

            // Guard: super().__init__() outside constructor context would produce base.Constructor()
            // which is invalid C#. This should have been handled in GenerateConstructor.
            if (methodName == "Constructor" && memberAccess.Object is SuperExpression)
            {
                return EmitNotImplementedExpression(
                    "super().__init__() must be in __init__ method body to be converted to base constructor call",
                    DiagnosticCodes.CodeGen.UnsupportedFeature, call.LineStart, call.ColumnStart);
            }

            // Generate arguments (reorder for C# compliance if needed).
            // The semantic-info-resolved symbol (from overload resolution during type checking)
            // is the correct overload for this call site. No emitter-side re-resolution (#1519).
            var methodCallTarget = resolvedMethodSymbol as FunctionSymbol;
            var allArgs = GenerateReorderedCallArguments(call, methodCallTarget);

            // Handle null conditional method calls: obj?.Method(args)
            if (memberAccess.IsNullConditional)
            {
                return GenerateNullConditionalMethodCall(obj, memberAccess, methodName, allArgs, call);
            }

            // Interface default method promotion: if the method is a default method
            // on an interface (not overridden by the class), call through an interface cast.
            // In C#, default interface methods can only be called through interface-typed refs.
            // The decision is recorded in SemanticInfo by the TypeChecker (#1519).
            var defaultMethodInterface = _context.SemanticInfo?.GetDefaultInterfaceDispatch(call);
            if (defaultMethodInterface != null)
            {
                var castExpr = ParenthesizedExpression(
                    CastExpression(IdentifierName(defaultMethodInterface), obj));
                var castMethodAccess = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    castExpr,
                    IdentifierName(methodName));
                return InvocationExpression(castMethodAccess)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Static-extension dispatch (#1071, #1072, #1085): the TypeChecker tagged this call to emit
            // as global::Ext.Method(receiver, args...) so an instance-style call can't silently bind a
            // shadowing BCL method (C# prefers instance methods over extensions). The receiver becomes
            // the extension's first argument, ahead of the reordered call arguments.
            var staticDispatch = GetIrStaticExtensionDispatch(memberAccess);
            if (staticDispatch != null)
            {
                var extensionType = MakeGlobalQualifiedName(staticDispatch.ExtensionTypeName.Split('.'));
                var staticMethodAccess = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    extensionType,
                    IdentifierName(staticDispatch.MethodName));
                var staticArgs = new[] { Argument(obj) }.Concat(allArgs);
                return InvocationExpression(staticMethodAccess)
                    .WithArgumentList(ArgumentList(SeparatedList(staticArgs)));
            }

            // Generate: obj.Method(args)
            var methodAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(methodName));

            return InvocationExpression(methodAccess)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Fallback: arbitrary expression as call target — dispatch on the top-level-unwrapped callee.
        // Handles: get_handler()("arg"), callbacks[0]("arg"), (lambda x: x)(42), chained calls, etc.
        var callTarget = GenerateExpression(callee);

        // Lambdas need explicit delegate cast for invocation in C#: ((Func<int, int>)(x => x * 2))(21)
        // The lambda may be bare or wrapped in a Parenthesized AST node → ParenthesizedExpressionSyntax
        var innerExprForCheck = callTarget;
        if (innerExprForCheck is ParenthesizedExpressionSyntax parenSyntax)
            innerExprForCheck = parenSyntax.Expression;

        if (innerExprForCheck is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
        {
            // Get the type of the lambda from semantic info. `callee` is already unwrapped of any
            // Parenthesized wrappers, so the semantic fact keyed on the inner lambda node is reachable.
            var lambdaType = _context.SemanticInfo?.GetExpressionType(callee);
            if (lambdaType is Semantic.FunctionType ft && !ft.HasUnresolvedTypes())
            {
                var delegateType = _typeMapper.MapSemanticType(ft);
                // Parenthesize the lambda before casting to prevent C# parser ambiguity:
                // (Func<int,int>)x => x*2 is parsed as cast-of-x, not cast-of-lambda.
                // ((Func<int,int>)(x => x*2)) is correct.
                callTarget = ParenthesizedExpression(
                    CastExpression(delegateType, ParenthesizedExpression(innerExprForCheck)));
            }
            else
            {
                // Bare lambda without type annotations — C# requires an explicit delegate type
                // for lambda invocation, so this produces CS0149 at the C# level.
                callTarget = ParenthesizedExpression(innerExprForCheck);
            }
        }

        var fallbackCallTarget = _context.SemanticInfo?.GetCallTarget(call);
        var fallbackAllArgs = GenerateReorderedCallArguments(call, fallbackCallTarget);

        return InvocationExpression(callTarget)
            .WithArgumentList(ArgumentList(SeparatedList(fallbackAllArgs)));
    }

    /// <summary>
    /// Lowers a called generic reference — <c>callee[T, ...](args)</c> — by the
    /// <see cref="GenericReferenceKind"/> the semantic phase resolved, dispatching to the emission
    /// body for that kind. Which kind this is and which symbol it targets were decided by the
    /// GenericReferenceResolver and are read here from the materialized
    /// <see cref="GenericReference"/>; the bodies only build syntax, mapping the written type
    /// arguments through the shared type mapper exactly as an annotation position does (#1143).
    /// <para>Returns null when the reference's target does not denote something constructible or
    /// callable in this position (e.g. a module-exported generic type that is neither a class nor a
    /// struct), leaving the caller's remaining callee arms to run.</para>
    /// </summary>
    private ExpressionSyntax? GenerateGenericReferenceCall(
        IndexAccess indexAccess, GenericReference reference, FunctionCall call)
    {
        switch (reference.Kind)
        {
            case GenericReferenceKind.ArrayTypeRef:
                return GenerateArrayConstruction(indexAccess, call);

            case GenericReferenceKind.TupleTypeRef:
                return GenerateTupleConversion(call);

            case GenericReferenceKind.GenericTypeRef:
                return indexAccess.Object is Identifier typeName
                       && reference.TargetSymbol is TypeSymbol genericTypeSymbol
                    ? GenerateGenericTypeInstantiation(indexAccess, reference, typeName, genericTypeSymbol, call)
                    : null;

            case GenericReferenceKind.NestedTypeRef:
                return indexAccess.Object is MemberAccess nestedTypeAccess
                       && reference.TargetSymbol is TypeSymbol nestedTypeSymbol
                    ? GenerateNestedGenericInstantiation(indexAccess, reference, nestedTypeSymbol, call)
                    : null;

            case GenericReferenceKind.Builtin:
            case GenericReferenceKind.UserFunction:
                return indexAccess.Object is Identifier funcName
                       && reference.TargetSymbol is FunctionSymbol genericFuncSymbol
                    ? GenerateGenericFunctionInvocation(
                        indexAccess, reference, funcName, genericFuncSymbol,
                        isBuiltin: reference.Kind == GenericReferenceKind.Builtin, call)
                    : null;

            case GenericReferenceKind.ModuleFunction:
                return indexAccess.Object is MemberAccess moduleFuncAccess
                       && reference.TargetSymbol is FunctionSymbol moduleFuncSymbol
                    ? GenerateModuleGenericFunctionCall(indexAccess, reference, moduleFuncAccess, moduleFuncSymbol, call)
                    : null;

            case GenericReferenceKind.ModuleType:
                return indexAccess.Object is MemberAccess moduleTypeAccess
                       && reference.TargetSymbol is TypeSymbol moduleTypeSymbol
                    ? GenerateModuleGenericTypeInstantiation(indexAccess, reference, moduleTypeAccess, moduleTypeSymbol, call)
                    : null;

            case GenericReferenceKind.InstanceMethod:
            case GenericReferenceKind.BclInstanceMethod:
                return indexAccess.Object is MemberAccess methodAccess
                       && reference.TargetSymbol is FunctionSymbol methodSymbol
                    ? GenerateInstanceGenericMethodCall(indexAccess, reference, methodAccess, methodSymbol, call)
                    : null;

            case GenericReferenceKind.BclExtensionMethod:
                return indexAccess.Object is MemberAccess extensionAccess
                       && reference.ClrMemberName is { } extensionClrName
                       && reference.LoweredTypeArgs is { Count: > 0 } extensionTypeArgs
                    ? GenerateBclExtensionMethodCall(
                        extensionAccess, extensionClrName, extensionTypeArgs, call)
                    : null;

            default:
                return null;
        }
    }

    /// <summary>
    /// The C# type-argument list for a generic reference — type OR function. The written arguments
    /// map through <see cref="TypeSyntaxMapper.MapTypeArgumentsFromExpression"/>, the same authority
    /// an annotation position uses, so a nested <c>list[int]</c> argument resolves identically in
    /// both. Any trailing arguments the resolver filled from PEP-696 type-parameter defaults have no
    /// written form at all (<c>Pair[int]</c> declaring <c>Pair[K, V = str]</c> emits
    /// <c>Pair&lt;int, string&gt;</c>), so they are taken from the materialized
    /// <see cref="GenericReference.TypeArgs"/> — the vector semantic analysis completed (#1192).
    ///
    /// <para>The function-shaped emissions mapped the written index alone until #1219, which is why
    /// the checker's default fill was invisible to them: it completed the vector in place and on the
    /// recorded fact, and the emitter re-derived from the AST instead of applying it. Reading the
    /// materialized vector here is the repo-rule-2 shape — the emitter applies, it does not
    /// re-derive.</para>
    /// </summary>
    private TypeSyntax[] MapTypeReferenceTypeArguments(IndexAccess indexAccess, GenericReference reference)
    {
        var written = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);
        if (reference.TypeArgs.Count <= written.Length)
            return written;

        var completed = new TypeSyntax[reference.TypeArgs.Count];
        Array.Copy(written, completed, written.Length);
        for (int i = written.Length; i < reference.TypeArgs.Count; i++)
            completed[i] = _typeMapper.MapSemanticType(reference.TypeArgs[i]);
        return completed;
    }

    /// <summary>
    /// Array construction: <c>array[T](size)</c> → <c>new T[size]</c>. The array constructor takes
    /// exactly one argument (the size), which semantic analysis enforces; a differently shaped call
    /// falls through to the general call path rather than emitting an array creation.
    /// </summary>
    private ExpressionSyntax? GenerateArrayConstruction(IndexAccess indexAccess, FunctionCall call)
    {
        if (call.Arguments.Length != 1)
            return null;

        var elementType = _typeMapper.MapTypeFromExpression(indexAccess.Index);
        var sizeExpr = GenerateExpression(call.Arguments[0]);
        return ArrayCreationExpression(
            ArrayType(elementType)
                .AddRankSpecifiers(
                    ArrayRankSpecifier(
                        SingletonSeparatedList<ExpressionSyntax>(sizeExpr))));
    }

    /// <summary>
    /// Tuple conversion: <c>tuple[int, str](t)</c> → <c>((ValueTuple&lt;int, string&gt;)(t))</c>
    /// (#1200). A tuple's arity is part of its type, so the semantic phase resolved this to a
    /// conversion whose argument is already assignable to the written tuple type — the value needs no
    /// rebuilding, and emitting a <c>ValueTuple</c> constructor around it produced CS7036 (one tuple
    /// argument where the constructor wants one argument per member). The cast is what makes the C#
    /// type equal the type the checker gave the expression when the two differ element-wise
    /// (<c>tuple[float, str]((1, "a"))</c>), the same way an annotated <c>x: float = 1</c> emits a
    /// real double; C# tuple conversions apply element-wise, so it is a no-op when they match. A
    /// differently shaped call cannot reach here: the checker rejects any arity but one.
    /// </summary>
    private ExpressionSyntax? GenerateTupleConversion(FunctionCall call)
    {
        if (call.Arguments.Length != 1)
            return null;

        var argument = ParenthesizedExpression(GenerateExpression(call.Arguments[0]));
        return GetExpressionSemanticType(call) is Semantic.TupleType tupleTarget
            ? ParenthesizedExpression(CastExpression(_typeMapper.MapSemanticType(tupleTarget), argument))
            : argument;
    }

    /// <summary>
    /// Generic type instantiation through a bare type name: <c>Box[int](42)</c> →
    /// <c>new Box&lt;int&gt;(42)</c>.
    /// </summary>
    private ExpressionSyntax GenerateGenericTypeInstantiation(
        IndexAccess indexAccess, GenericReference reference, Identifier typeName,
        TypeSymbol genericTypeSymbol, FunctionCall call)
    {
        var typeArgsSyntax = MapTypeReferenceTypeArguments(indexAccess, reference);

        // Resolve the C# name through the single naming seam so this construction position agrees
        // with the annotation position: a wrapper collection (list/dict/set) stays Sharpy.List; an
        // imported/cross-file type is fully qualified (an imported raw-BCL List[int]() emits
        // System.Collections.Generic.List<int>, not the bare List<int> that collided with
        // Sharpy.List → CS0104); a current-file user type keeps its short name (#1139).
        var csharpGenericTypeName = _typeMapper.GetTypeNameForReference(typeName.Name, typeName.IsNameBacktickEscaped);
        var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpGenericTypeName, typeArgsSyntax);

        // Generate arguments (reorder for C# compliance if needed)
        var genericTypeCallTarget = ResolveConstructorForCall(genericTypeSymbol, call);
        var allArgs = GenerateReorderedCallArguments(call, genericTypeCallTarget);

        // DefaultDict: wrap type-reference arguments in factory lambdas.
        // defaultdict[str, list[int]](list) → new DefaultDict<string, List<long>>(() => new List<long>())
        // The DefaultDict constructor takes Func<TValue>, not a type reference.
        if (string.Equals(typeName.Name, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase)
            && call.Arguments.Length >= 1
            && typeArgsSyntax.Length >= 2)
        {
            allArgs = WrapDefaultDictFactoryArgs(call, allArgs, typeArgsSyntax[1]);
        }

        return ObjectCreationExpression(genericTypeSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Generic function call through a bare name: <c>identity[int](42)</c> →
    /// <c>Identity&lt;int&gt;(42)</c>, or, for a registry builtin,
    /// <c>global::Sharpy.Builtins.Map&lt;int, int&gt;(...)</c>. Whether the callee is a genuine
    /// builtin or a user function shadowing a builtin name (<c>def map[T]</c>, #1003) is the
    /// resolver's <see cref="GenericReferenceKind"/> decision, passed in as
    /// <paramref name="isBuiltin"/>.
    /// </summary>
    private ExpressionSyntax GenerateGenericFunctionInvocation(
        IndexAccess indexAccess, GenericReference reference, Identifier funcName,
        FunctionSymbol genericFuncSymbol, bool isBuiltin, FunctionCall call)
    {
        var typeArgsSyntax = MapTypeReferenceTypeArguments(indexAccess, reference);
        var genericFuncSyntax = GenericName(
                NameCasing.ResolveMethod(funcName.Name, funcName.IsNameBacktickEscaped, GetClrMethodName(genericFuncSymbol)))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

        // Generate arguments (reorder for C# compliance if needed)
        var allArgs = GenerateReorderedCallArguments(call, genericFuncSymbol);

        if (isBuiltin)
        {
            var qualifiedBase = MakeGlobalQualifiedName("Sharpy", "Builtins");
            return InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, qualifiedBase, genericFuncSyntax))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        return InvocationExpression(genericFuncSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Nested generic type instantiation: <c>Outer.Inner[int](42)</c> →
    /// <c>new Outer.Inner&lt;int&gt;(42)</c>. <paramref name="nestedTypeSymbol"/> is the nested type the
    /// resolver bound (<see cref="GenericReferenceKind.NestedTypeRef"/>, #1164), so no symbol-table
    /// walk happens here: the whole qualified name is spelled from that symbol's declaring-type
    /// chain by <see cref="BuildNestedTypeName"/>, the same builder the no-type-argument spelling
    /// uses (#1217).
    /// <para>Note the innermost segment's source changed with that unification: it is now the
    /// SYMBOL's name rather than <c>memberAccess.Member</c>'s source spelling. They agree for
    /// ordinary names, and for a backtick-escaped one the symbol is the CORRECT source — see
    /// <see cref="BuildNestedTypeName"/>, which explains why the old verbatim spelling was
    /// CS0426.</para>
    /// </summary>
    private ExpressionSyntax GenerateNestedGenericInstantiation(
        IndexAccess indexAccess, GenericReference reference,
        TypeSymbol nestedTypeSymbol, FunctionCall call)
    {
        var typeArgsSyntax = MapTypeReferenceTypeArguments(indexAccess, reference);
        var qualifiedGenericName = BuildNestedTypeName(nestedTypeSymbol, typeArgsSyntax);

        var constructorTarget = ResolveConstructorForCall(nestedTypeSymbol, call);
        var allArgs = GenerateReorderedCallArguments(call, constructorTarget);

        return ObjectCreationExpression(qualifiedGenericName)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Generic function call on an imported module: <c>json.loads[int](text)</c> →
    /// <c>Json.Loads&lt;int&gt;(text)</c>. <paramref name="funcSymbol"/> is the exported function the
    /// resolver bound the member to, so no export table is re-scanned here (#1143).
    /// </summary>
    private ExpressionSyntax GenerateModuleGenericFunctionCall(
        IndexAccess indexAccess, GenericReference reference, MemberAccess memberAccess,
        FunctionSymbol funcSymbol, FunctionCall call)
    {
        var typeArgsSyntax = MapTypeReferenceTypeArguments(indexAccess, reference);
        var genericMethodName = GenericName(
                NameCasing.ResolveMethod(memberAccess.Member, memberAccess.IsMemberBacktickEscaped, GetClrMethodName(funcSymbol)))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

        var moduleExpr = GenerateExpression(memberAccess.Object);
        var qualifiedCall = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression, moduleExpr, genericMethodName);

        var allArgs = GenerateReorderedCallArguments(call, funcSymbol);
        return InvocationExpression(qualifiedCall)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Emits an instance generic-method call with explicit type arguments:
    /// <c>recv.convert[int](x)</c> → <c>recv.Convert&lt;int&gt;(x)</c> (#1133), including the raw-BCL
    /// receivers the reflection path resolves (#1136). <paramref name="methodSymbol"/> is the method
    /// the resolver bound, so this is a pure translator: it threads the receiver through a
    /// <see cref="MemberAccessExpression"/> + <see cref="GenericName"/>.
    /// </summary>
    private ExpressionSyntax GenerateInstanceGenericMethodCall(
        IndexAccess indexAccess, GenericReference reference, MemberAccess memberAccess,
        FunctionSymbol methodSymbol, FunctionCall call)
    {
        var typeArgsSyntax = MapTypeReferenceTypeArguments(indexAccess, reference);
        var genericMethodName = GenericName(
                NameCasing.ResolveMethod(memberAccess.Member, memberAccess.IsMemberBacktickEscaped, GetClrMethodName(methodSymbol)))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

        var receiverExpr = GenerateExpression(memberAccess.Object);
        var qualifiedCall = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression, receiverExpr, genericMethodName);

        var allArgs = GenerateReorderedCallArguments(call, methodSymbol);
        return InvocationExpression(qualifiedCall)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Emits an extension-method call with explicit type arguments in instance-call syntax:
    /// <c>lst.select[str](f)</c> → <c>lst.Select&lt;int, string&gt;(f)</c> (#1163). Both the CLR method
    /// name and the COMPLETE type-argument vector come from the fact — the receiver-inferred arguments
    /// are not written in the source and cannot be mapped from the index expression, which is why this
    /// kind carries <see cref="GenericReference.LoweredTypeArgs"/>. C# binds the call to
    /// <c>System.Linq.Enumerable</c> because <c>using System.Linq;</c> is always emitted, the same way
    /// the no-type-args spelling <c>lst.first()</c> binds.
    /// </summary>
    private ExpressionSyntax GenerateBclExtensionMethodCall(
        MemberAccess memberAccess, string clrMethodName,
        IReadOnlyList<Semantic.SemanticType> loweredTypeArgs, FunctionCall call)
    {
        var typeArgsSyntax = loweredTypeArgs.Select(_typeMapper.MapSemanticType).ToArray();
        var genericMethodName = GenericName(clrMethodName)
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

        var receiverExpr = GenerateExpression(memberAccess.Object);
        var qualifiedCall = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression, receiverExpr, genericMethodName);

        // An extension method has no discovered FunctionSymbol, so there is no parameter list to
        // reorder against; the arguments are emitted in source order.
        var allArgs = GenerateReorderedCallArguments(call, funcSymbol: null);
        return InvocationExpression(qualifiedCall)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Handles generic instantiation of a module-qualified type:
    /// <c>difflib.SequenceMatcher[str](None, a, b)</c> →
    /// <c>new global::...SequenceMatcher&lt;string&gt;(None, a, b)</c>.
    /// <paramref name="typeSymbol"/> is the exported type the resolver bound the member to.
    /// Returns null for an exported generic type that is not constructible (neither class nor
    /// struct), leaving the call on the general path.
    /// </summary>
    private ExpressionSyntax? GenerateModuleGenericTypeInstantiation(
        IndexAccess indexAccess, GenericReference reference, MemberAccess memberAccess,
        TypeSymbol typeSymbol, FunctionCall call)
    {
        if (typeSymbol.TypeKind != Semantic.TypeKind.Class
            && typeSymbol.TypeKind != Semantic.TypeKind.Struct)
        {
            return null;
        }

        var typeArgsSyntax = MapTypeReferenceTypeArguments(indexAccess, reference);
        var baseName = GetFullyQualifiedTypeName(typeSymbol, memberAccess.Member);
        var (dottedName, globalQualified) = NormalizeTypeName(baseName);
        var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(
            dottedName, globalQualified, typeArgsSyntax);

        var ctorTarget = ResolveConstructorForCall(typeSymbol, call);
        var allArgs = GenerateReorderedCallArguments(call, ctorTarget);
        return ObjectCreationExpression(genericTypeSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Emits an <c>ObjectCreationExpression</c> for a constructor call, supplying explicit
    /// generic type arguments when the resolved type is generic (C# has no generic constructor
    /// inference). Shared by the identifier-callee and module-qualified member-access
    /// constructor paths.
    /// <paramref name="baseCSharpName"/> is the C# type name WITHOUT type arguments (possibly
    /// <c>global::</c>-prefixed and/or dotted), as produced by <see cref="GetFullyQualifiedTypeName"/>.
    /// <paramref name="genericBaseName"/>, when supplied, overrides the type name used for
    /// generic instantiation (the identifier path resolves builtin collections through
    /// <see cref="CSharpTypeNames"/>, e.g. <c>set</c> → <c>Sharpy.Set</c>).
    /// </summary>
    private ExpressionSyntax GenerateTypeInstantiation(
        FunctionCall call, string baseCSharpName, ArgumentSyntax[] allArgs, string? genericBaseName = null)
    {
        // Explicit generic type arguments from the resolved expression type
        // (e.g., a generic type called without an explicit subscript: set(), Cell(42)).
        var exprType = _context.SemanticInfo?.GetExpressionType(call);
        if (exprType is GenericType resolvedGeneric && resolvedGeneric.TypeArguments.Count > 0
            && resolvedGeneric.TypeArguments.All(t => t is not UnknownType))
        {
            // NOTE: list("abc") used to be special-cased here (#1067), emitting ListFromStr
            // directly. The iterable-projection ring now records StrToList for every `str` in a
            // builtin iterable position — `list` among them — and the argument arrives already
            // projected, so the ordinary constructor path below emits
            // new Sharpy.List<string>(Builtins.ListFromStr("abc")). Keeping the old arm as well
            // double-projected it into ListFromStr(ListFromStr(s)) (CS1503, #1209).

            var typeArgsSyntax = resolvedGeneric.TypeArguments
                .Select(t => _typeMapper.MapSemanticType(t))
                .ToArray();
            var (genericName, genericGlobal) = NormalizeTypeName(genericBaseName ?? baseCSharpName);
            return ObjectCreationExpression(
                    TypeSyntaxMapper.QualifiedGenericName(genericName, genericGlobal, typeArgsSyntax))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Fallback: inferred type arguments from generic constructor inference.
        // C# does not support generic constructor inference, so we must always emit
        // explicit type arguments: Cell(42) -> new Cell<int>(42)
        if (ResolvedConstructionTypeArguments(call) is { } typeArgsFromInference)
        {
            var (genericName, genericGlobal) = NormalizeTypeName(genericBaseName ?? baseCSharpName);
            return ObjectCreationExpression(
                    TypeSyntaxMapper.QualifiedGenericName(genericName, genericGlobal, typeArgsFromInference))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Non-generic construction.
        var (dottedName, globalQualified) = NormalizeTypeName(baseCSharpName);
        NameSyntax typeSyntax = globalQualified
            ? MakeGlobalQualifiedName(dottedName.Split('.'))
            : ParseQualifiedName(dottedName);
        return ObjectCreationExpression(typeSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// The type-argument vector a constructor call must spell, or null when the construction needs
    /// none. C# has no generic constructor inference, so <c>Box(5)</c> and <c>Outer.Inner(5)</c> alike
    /// have to emit what semantic analysis resolved: the call's own <see cref="GenericType"/>
    /// expression type when it carries a complete one, else the inferred-type-arguments fact (#1193).
    /// </summary>
    private TypeSyntax[]? ResolvedConstructionTypeArguments(FunctionCall call)
    {
        if (_context.SemanticInfo?.GetExpressionType(call) is GenericType resolvedGeneric
            && resolvedGeneric.TypeArguments.Count > 0
            && resolvedGeneric.TypeArguments.All(t => t is not UnknownType))
        {
            return resolvedGeneric.TypeArguments.Select(t => _typeMapper.MapSemanticType(t)).ToArray();
        }

        var inferredTypeArgs = _context.SemanticInfo?.GetInferredTypeArguments(call);
        return inferredTypeArgs is { Count: > 0 }
            ? inferredTypeArgs.Select(t => _typeMapper.MapSemanticType(t)).ToArray()
            : null;
    }

    /// <summary>
    /// Splits a C# type name (as produced by <see cref="GetFullyQualifiedTypeName"/>) into its
    /// dotted form and a flag indicating whether it must be <c>global::</c>-qualified. A name
    /// already carrying a <c>global::</c> prefix is stripped and flagged; otherwise it is flagged
    /// when a project namespace is active and the name is dotted (to avoid namespace prepending).
    /// </summary>
    private (string Dotted, bool GlobalQualified) NormalizeTypeName(string baseCSharpName)
    {
        if (baseCSharpName.StartsWith("global::", StringComparison.Ordinal))
            return (baseCSharpName["global::".Length..], true);

        var globalQualified = !string.IsNullOrEmpty(_context.ProjectNamespace)
            && baseCSharpName.Contains('.', StringComparison.Ordinal);
        return (baseCSharpName, globalQualified);
    }

    /// <summary>
    /// Builds a <see cref="NameSyntax"/> from a fully-qualified C# type name (as produced by
    /// <see cref="GetFullyQualifiedTypeName"/>), preserving <c>global::</c> qualification.
    /// </summary>
    private NameSyntax BuildTypeNameFromFqn(string fqn)
    {
        var (dotted, globalQualified) = NormalizeTypeName(fqn);
        return globalQualified
            ? MakeGlobalQualifiedName(dotted.Split('.'))
            : ParseQualifiedName(dotted);
    }

    /// <summary>
    /// Maps a builtin collection name (<c>list</c>/<c>dict</c>/<c>set</c>) to its non-generic
    /// Sharpy protocol interface (<c>Sharpy.IList</c>/<c>IDict</c>/<c>ISet</c>), returning null for
    /// any other name. These type-erased interfaces are implemented by every closed generic
    /// instantiation via boxing adapters, so they are the correct target for <c>isinstance</c>
    /// type tests and the resulting narrowing cast against an <c>object</c> receiver — a closed
    /// generic like <c>Sharpy.List&lt;object&gt;</c> would only match (and only cast from) that
    /// exact instantiation. Mirrors the <c>case list()</c> pattern lowering in
    /// <see cref="GeneratePattern"/>.
    /// </summary>
    internal static NameSyntax? TryMapBuiltinCollectionToNonGenericInterface(string sharpyTypeName) =>
        sharpyTypeName switch
        {
            BuiltinNames.List => MakeGlobalQualifiedName("Sharpy", "IList"),
            BuiltinNames.Dict => MakeGlobalQualifiedName("Sharpy", "IDict"),
            BuiltinNames.Set => MakeGlobalQualifiedName("Sharpy", "ISet"),
            _ => null
        };

    /// <summary>
    /// Renders a classified <c>isinstance</c> type test as the C# type the <c>is</c> operator tests
    /// against. Pure translation of a decision already made: the kind selects the shape and the
    /// resolved type supplies the name (#1207, #1213).
    /// </summary>
    private TypeSyntax MapTypeTestTarget(TypeTestLowering typeTest)
    {
        if (typeTest.Kind == TypeTestLoweringKind.ErasedBuiltinCollection
            && typeTest.TestType is GenericType erasedCollection
            && TryMapBuiltinCollectionToNonGenericInterface(erasedCollection.Name) is { } protocolInterface)
        {
            return protocolInterface;
        }

        return MapTypeTestTypeName(typeTest.TestType);
    }

    /// <summary>
    /// Renders a decided test type as C# type syntax. A user/CLR type reaches its name through the
    /// construction position — the same position the module-qualified arm used before classification
    /// (#903) and the one <c>TypeSyntaxMapper</c> documents for isinstance and except clauses. Going
    /// through <c>MapSemanticType</c> instead would take the reference position and re-qualify
    /// non-generic CLR types differently (#1139).
    /// </summary>
    private TypeSyntax MapTypeTestTypeName(SemanticType testType)
        => testType is UserDefinedType { Symbol: { } typeSymbol } userDefined
            ? BuildTypeNameFromFqn(GetFullyQualifiedTypeName(typeSymbol, userDefined.Name))
            : _typeMapper.MapSemanticType(testType);

    /// <summary>
    /// Resolves a member access of the form <c>module.TypeName</c> (or nested
    /// <c>module.sub.TypeName</c>) to its exported <see cref="TypeSymbol"/>, applying the
    /// PascalCase fallback for .NET modules. Returns the symbol and the export key used to
    /// resolve it (for fully-qualified name generation), or null when the member access does
    /// not denote a module-exported type.
    /// </summary>
    private (TypeSymbol Symbol, string OriginalName)? TryResolveModuleExportedType(MemberAccess memberAccess)
    {
        var moduleSymbol = ResolveModuleFromExpression(memberAccess.Object);
        if (moduleSymbol == null)
            return null;

        var memberName = memberAccess.Member;
        if (!moduleSymbol.Exports.ContainsKey(memberName) && moduleSymbol.IsNetModule)
        {
            var pascalName = NameCasing.ResolveType(memberName, isBacktickEscaped: memberAccess.IsMemberBacktickEscaped);
            if (moduleSymbol.Exports.ContainsKey(pascalName))
                memberName = pascalName;
        }

        if (moduleSymbol.Exports.TryGetValue(memberName, out var exported)
            && exported is TypeSymbol typeSymbol)
        {
            return (typeSymbol, memberName);
        }

        return null;
    }

    /// <summary>
    /// Resolves an expression to a <see cref="ModuleSymbol"/>: a bare identifier referencing an
    /// imported module, or a nested module member access (e.g., <c>email.message</c>).
    /// Returns null when the expression does not denote a module.
    /// </summary>
    private ModuleSymbol? ResolveModuleFromExpression(Expression expr)
    {
        if (expr is Identifier id)
            return _context.LookupSymbol(id.Name) as ModuleSymbol;

        if (expr is MemberAccess ma)
        {
            var parent = ResolveModuleFromExpression(ma.Object);
            if (parent != null && parent.Exports.TryGetValue(ma.Member, out var sym)
                && sym is ModuleSymbol nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// For DefaultDict construction, wraps type-reference arguments in factory lambdas.
    /// <c>defaultdict[str, list[int]](list)</c> becomes
    /// <c>new DefaultDict&lt;string, List&lt;long&gt;&gt;(() =&gt; new List&lt;long&gt;())</c>
    /// because the DefaultDict constructor takes <c>Func&lt;TValue&gt;</c>, not a type reference.
    /// </summary>
    private ArgumentSyntax[] WrapDefaultDictFactoryArgs(
        FunctionCall call, ArgumentSyntax[] allArgs, TypeSyntax valueTypeSyntax)
    {
        if (allArgs.Length == 0 || call.Arguments.Length == 0)
            return allArgs;

        // Whether the first argument is a type name used as a callable factory — Python's
        // defaultdict(list) convention — was decided during type checking and materialized on the
        // argument node (MarkTypeFactoryArguments): the name may resolve as a TypeSymbol, as a builtin
        // collection function, or only through the wrapper-collection special cases, and choosing
        // among those is a semantic decision, not a translation (#1175, Critical Rule 2).
        var firstArg = call.Arguments[0];
        if (_context.SemanticInfo?.IsTypeFactoryArgument(firstArg) != true)
            return allArgs;

        // Generate factory lambda: () => new ValueType()
        var factoryBody = ObjectCreationExpression(valueTypeSyntax)
            .WithArgumentList(ArgumentList());
        var factoryLambda = ParenthesizedLambdaExpression(
            ParameterList(), factoryBody);

        // Replace the first argument with the factory lambda
        var result = new ArgumentSyntax[allArgs.Length];
        result[0] = Argument(factoryLambda);
        for (int i = 1; i < allArgs.Length; i++)
            result[i] = allArgs[i];

        return result;
    }

    /// <summary>
    /// Handle null conditional method calls: obj?.Method(args).
    /// For Optional&lt;T&gt;, lowers to a ternary since ?. doesn't work on structs.
    /// For nullable reference types, uses ConditionalAccessExpression.
    /// </summary>
    private ExpressionSyntax GenerateNullConditionalMethodCall(
        ExpressionSyntax obj, MemberAccess memberAccess, string methodName,
        ArgumentSyntax[] allArgs, FunctionCall call)
    {
        // For Optional<T>: lower to ternary since ?.  doesn't work on structs
        if (GetExpressionSemanticType(memberAccess.Object) is OptionalType objOptType)
        {
            // Ensure obj is only evaluated once for complex expressions
            var (safeObj, capture) = EnsureSingleEvaluation(obj, memberAccess.Object);
            // safeObj.IsSome ? safeObj.Unwrap().Method(args) : Optional<T>.None
            var methodCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            safeObj, IdentifierName(ProtocolConstants.Unwrap)))
                        .WithArgumentList(ArgumentList()),
                    IdentifierName(methodName)))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

            ExpressionSyntax cond = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                ParenthesizedExpression(safeObj), IdentifierName("IsSome"));
            if (capture != null)
                cond = BinaryExpression(SyntaxKind.LogicalAndExpression, capture, cond);

            // Determine the Optional type and whether to wrap the true branch.
            // Case 1: callType is OptionalType — the method itself returns Optional<T>
            //   (e.g., get_city() -> str?). The true branch already returns Optional<T>,
            //   so we use it as-is and only set the false branch to Optional<T>.None.
            // Case 2: callType is Unknown or non-Optional — the method returns a plain type
            //   (e.g., str.upper() -> str, resolved via CLR discovery). The true branch returns
            //   the raw type, so we wrap it in Optional<T>.Some() using the object's
            //   Optional underlying type. This ensures both branches have the same type.
            var callType = GetExpressionSemanticType(call);
            ExpressionSyntax trueBranch;
            ExpressionSyntax falseExpr;
            if (callType is OptionalType optCallType)
            {
                // Method returns Optional<T> — true branch is already correct
                trueBranch = methodCall;
                falseExpr = GenerateOptionalNone(optCallType);
            }
            else
            {
                // Method returns non-Optional (or Unknown) — wrap both branches
                trueBranch = WrapInOptionalSome(methodCall, objOptType);
                falseExpr = GenerateOptionalNone(objOptType);
            }
            return ConditionalExpression(cond, trueBranch, falseExpr);
        }

        // Generate: obj?.Method(args)
        // Uses ConditionalAccessExpression with MemberBindingExpression for the method
        // followed by InvocationExpression for the call
        var memberBinding = MemberBindingExpression(IdentifierName(methodName));
        var invocation = InvocationExpression(memberBinding)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

        return ConditionalAccessExpression(obj, invocation);
    }

    /// <summary>
    /// Generate a call to a generic builtin function (reversed, sorted) with explicit type arguments.
    /// These builtins exist in Sharpy.Core as generic methods but are filtered out by OverloadIndexBuilder.
    /// </summary>
    private ExpressionSyntax GenerateGenericBuiltinCall(string name, FunctionCall call, ArgumentSyntax[] allArgs)
    {
        var csharpName = NameCasing.ResolveMethod(name, isBacktickEscaped: false);

        // Infer the element type argument (T in Sorted<T>/Reversed<T>). Read the semantic layer's
        // already-resolved answer first: sorted() resolves to list[T] and reversed() to
        // Iterator[T], so the call's resolved return type carries the correct element type at
        // TypeArguments[0]. TypeChecker computed it via InferIterableElementType, which correctly
        // picks the VALUE type for dict views (values()), the (K, V) tuple for items(), the KEY for
        // keys(), etc. The emitter must not re-infer this: reading gt.TypeArguments[0] off the
        // *argument* type picks the KEY for DictValuesView<K, V> (#1068 — an emitter-purity residue).
        TypeSyntax? typeArg = null;
        if (call.Arguments.Length > 0)
        {
            SemanticType? elemType = null;

            var callType = _context.SemanticInfo?.GetExpressionType(call);
            if (callType is GenericType callGeneric
                && callGeneric.TypeArguments.Count > 0
                && callGeneric.TypeArguments[0] is not UnknownType
                && !IsObjectType(callGeneric.TypeArguments[0]))
            {
                elemType = callGeneric.TypeArguments[0];
            }

            // Fallback for error recovery, when the call's return type was not resolved: infer the
            // element type from the argument's AST structure (#555, e.g. sorted(list(d.keys()))).
            if (elemType == null || IsObjectType(elemType))
            {
                var inferred = TryInferElementTypeFromArg(call.Arguments[0]);
                if (inferred != null)
                    elemType = inferred;
            }

            if (elemType != null && !IsObjectType(elemType))
                typeArg = _typeMapper.MapSemanticType(elemType);
        }

        // Final fallback: if typeArg is still null or object, try to extract element type from
        // the already-generated argument syntax. E.g., new Sharpy.List<string>(...) → string.
        if (typeArg == null || typeArg.ToString() == "object")
        {
            if (allArgs.Length > 0
                && allArgs[0].Expression is ObjectCreationExpressionSyntax objCreation
                && objCreation.Type is QualifiedNameSyntax { Right: GenericNameSyntax gns }
                && gns.TypeArgumentList.Arguments.Count > 0)
            {
                typeArg = gns.TypeArgumentList.Arguments[0];
            }
        }

        // For reversed(s) on strings, emit StringHelpers.Reversed(s) to yield single-char strings
        if (name == BuiltinNames.Reversed && call.Arguments.Length > 0
            && GetExpressionSemanticType(call.Arguments[0]) == SemanticType.Str)
        {
            return InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                    IdentifierName("Reversed")))
                .AddArgumentListArguments(Argument(allArgs[0].Expression));
        }

        // For reversed(): if the argument type has __reversed__, cast to IReverseEnumerable<T>
        // to disambiguate C# overload resolution between Reversed<T>(IEnumerable<T>) and
        // Reversed<T>(IReverseEnumerable<T>).
        //
        // This is a fact about the GENERATED code, not about Sharpy's type system. A class with both
        // __iter__ and __reversed__ implements both interfaces, they are unrelated, so neither
        // conversion is better and C# reports CS0121. #1242 predicted the cast would become removable
        // once IReverseEnumerable<T> stopped degrading to `object` in the CLR-to-semantic mapping;
        // that prediction was wrong, and it was checked rather than assumed — deleting this block with
        // the mapping fixed still gives CS0121 (behind SPY0908) on builtins/reversed_user_class_both.
        // No mapping change can affect C# overload resolution, so the cast stays.
        if (name == BuiltinNames.Reversed && typeArg != null && call.Arguments.Length > 0)
        {
            var argType2 = GetExpressionSemanticType(call.Arguments[0]);
            if (argType2 is UserDefinedType udt && udt.Symbol is TypeSymbol argTypeSymbol
                && argTypeSymbol.ProtocolMethods.ContainsKey("__reversed__"))
            {
                // Cast argument to IReverseEnumerable<T> to select the correct overload
                var iReverseType = QualifiedName(
                    AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                        IdentifierName("Sharpy")),
                    GenericName("IReverseEnumerable")
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(typeArg))));
                var castExpr = CastExpression(iReverseType, allArgs[0].Expression);
                allArgs[0] = Argument(castExpr);
            }
        }

        // When sorted() has a key= argument, omit explicit type args so C# infers both T and TKey.
        // sorted(data, reverse=True) without key= should still emit Sorted<T>(...).
        var hasKeyArg = name == BuiltinNames.Sorted
            && allArgs.Any(a => a.NameColon?.Name.Identifier.Text == "key");
        if (hasKeyArg)
            typeArg = null;

        // Build: global::Sharpy.Builtins.Reversed<T>(args)
        var qualifiedBase = MakeGlobalQualifiedName("Sharpy", "Builtins");
        SimpleNameSyntax methodName = typeArg != null
            ? GenericName(csharpName).WithTypeArgumentList(
                TypeArgumentList(SingletonSeparatedList(typeArg)))
            : IdentifierName(csharpName);

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, qualifiedBase, methodName))
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess, bool applyNarrowing = true)
    {
        // A builtins-qualified constructor reference pinned by semantic analysis (`builtins.dict`,
        // #1382). The recorded lowering is the same fact the bare spelling records, keyed on this
        // node — so this applies it verbatim rather than deciding anything, and the two spellings
        // emit identically by construction. Must precede every member-access path below: those emit
        // `<qualifier>.<Member>`, which for the builtins module class is CS0117 ('Builtins' has no
        // 'Dict') — the SPY0908 this arm exists to prevent.
        if (_context.SemanticInfo?.GetConstructorReferenceLowering(memberAccess) is { } qualifiedConstructorReference)
            return GenerateConstructorReference(memberAccess.IsMemberBacktickEscaped, qualifiedConstructorReference);

        // A module-qualified type used as a value/type reference (e.g. the receiver
        // `http.HTTPStatus` of `http.HTTPStatus.OK`). The module alias points at the module
        // CLASS (using http = global::Sharpy.HttpModule), which has no such member, so emit the
        // fully-qualified type name (global::Sharpy.HTTPStatus) instead of `alias.Type` (#897).
        // This must precede TryExtractModulePath, which would otherwise treat the type segment
        // as a module-path element and emit the broken alias-qualified access.
        if (TryResolveModuleExportedType(memberAccess) is { } moduleTypeRef)
        {
            return BuildTypeNameFromFqn(
                GetFullyQualifiedTypeName(moduleTypeRef.Symbol, moduleTypeRef.OriginalName));
        }

        // Check for nested module access (e.g., lib.math.add -> Lib.Math.Add)
        // This must be checked before enum handling to ensure module paths take precedence
        if (TryExtractModulePath(memberAccess, out var modulePath))
        {
            return BuildModuleAccessExpression(modulePath);
        }

        // Check for enum member access (e.g., Color.RED -> Color.Red)
        if (memberAccess.Object is Identifier enumTypeIdentifier)
        {
            var symbol = _context.LookupSymbol(enumTypeIdentifier.Name);

            // If this is an enum type, handle member access specially
            if (symbol is TypeSymbol enumSymbol && enumSymbol.TypeKind == Semantic.TypeKind.Enum)
            {
                // Qualify enum type to avoid method name shadowing (e.g., vehicle_type() -> VehicleType()
                // collides with VehicleType enum). Cross-file types are already qualified by
                // GetFullyQualifiedTypeName; same-file types inside a class need module class qualification.
                ExpressionSyntax enumType = BuildQualifiedTypeAccess(enumSymbol, enumTypeIdentifier.Name);

                // String enum → the singleton field's CONSTANT_CASE name; CLR enum → the .NET name
                // unmangled; source int enum → NameContext.EnumMember. Shared with the pattern path
                // so the two cannot spell the same member differently (#1284).
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    enumType,
                    EnumMemberIdentifier(
                        enumSymbol, memberAccess.Member, memberAccess.IsMemberBacktickEscaped));
            }
        }

        // Check for static/const field access via type name (ClassName.FIELD) or via instance
        // (self.field, obj.field). The TypeChecker stores the resolved symbol in SemanticInfo
        // so the emitter doesn't re-resolve. For static fields accessed via instance, codegen
        // must rewrite to ClassName.Field because C# disallows instance access (CS0176).
        var resolution = _context.SemanticInfo?.GetMemberAccessResolution(memberAccess);
        if (resolution is { } res && res.Member is VariableSymbol resolvedField)
        {
            var classSymbol = res.Owner;
            // Use the owner type's name — not the object identifier, which could be
            // a variable name (e.g., `a.count` → owner is Counter, not `a`)
            return GenerateStaticFieldAccess(classSymbol, classSymbol.Name, resolvedField, memberAccess.Member);
        }

        var obj = GenerateExpression(memberAccess.Object);

        // Handle special .value and .name properties for enum instances.
        if (memberAccess.Member is "value" or "name" && IsEnumInstance(memberAccess.Object))
        {
            // A string-backed enum is a class of singletons carrying both halves as properties,
            // so both reads are plain member accesses — `(int)` would not even compile (#1284).
            if (GetExpressionSemanticType(memberAccess.Object) is Semantic.UserDefinedType
                { Symbol: { } strEnumSymbol } && IsStringEnumSymbol(strEnumSymbol))
            {
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    obj,
                    IdentifierName(memberAccess.Member == "value" ? "Value" : "Name"));
            }

            if (memberAccess.Member == "value")
            {
                // enum_instance.value -> (int)enum_instance
                return CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    obj);
            }

            // enum_instance.name -> enum_instance.ToString()
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    obj,
                    IdentifierName("ToString")));
        }

        // Named tuple element access: keep element names as-is (no PascalCase)
        if (GetExpressionSemanticType(memberAccess.Object) is Semantic.TupleType namedTupleType
            && namedTupleType.IsNamed)
        {
            var names = namedTupleType.ElementNames!.Value;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == memberAccess.Member)
                {
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        obj,
                        IdentifierName(memberAccess.Member));
                }
            }
        }

        // Apply name mangling to member names:
        // - Dunder methods use DunderMapping
        // - ALL_CAPS names (Python-style constants) use CONSTANT_CASE
        // - Other names use PascalCase, unless a resolved CLR property name was materialized
        //   (a verbatim CLR member such as socket's lowercase `type`, #1093), which wins.
        var mangledMemberName = DunderMapping.ResolveCSharpName(memberAccess.Member)
            ?? (NameFormDetector.IsConstantCaseName(memberAccess.Member)
                ? NameMangler.ToConstantCase(memberAccess.Member)
                : NameCasing.ResolveField(
                    memberAccess.Member,
                    isBacktickEscaped: memberAccess.IsMemberBacktickEscaped,
                    GetIrResolvedClrMemberName(memberAccess)));
        var member = EscapedIdentifierName(mangledMemberName);

        ExpressionSyntax result;

        if (memberAccess.IsNullConditional)
        {
            // For Optional<T>: lower to ternary since ?. doesn't work on structs
            if (GetExpressionSemanticType(memberAccess.Object) is OptionalType propObjOptType)
            {
                // Ensure obj is only evaluated once for complex expressions
                var (safeObj, capture) = EnsureSingleEvaluation(obj, memberAccess.Object);
                // safeObj.IsSome ? safeObj.Unwrap().Member : Optional<T>.None
                ExpressionSyntax cond = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(safeObj), IdentifierName("IsSome"));
                if (capture != null)
                    cond = BinaryExpression(SyntaxKind.LogicalAndExpression, capture, cond);

                var trueExpr = (ExpressionSyntax)MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            safeObj, IdentifierName(ProtocolConstants.Unwrap)))
                        .WithArgumentList(ArgumentList()),
                    member);

                // Determine the Optional type and whether to wrap the true branch.
                // Same logic as method calls: if the member type is already Optional,
                // the true branch is correct as-is. Otherwise wrap in Optional<T>.Some().
                var exprType = GetExpressionSemanticType(memberAccess);
                ExpressionSyntax falseExpr;
                ExpressionSyntax wrappedTrue;
                if (exprType is OptionalType optExprType)
                {
                    // Member returns Optional<T> — true branch is already correct
                    wrappedTrue = trueExpr;
                    falseExpr = GenerateOptionalNone(optExprType);
                }
                else
                {
                    // Member returns non-Optional (or Unknown) — wrap both branches
                    wrappedTrue = WrapInOptionalSome(trueExpr, propObjOptType);
                    falseExpr = GenerateOptionalNone(propObjOptType);
                }
                result = ConditionalExpression(cond, wrappedTrue, falseExpr);
            }
            else
            {
                // obj?.member
                result = ConditionalAccessExpression(obj,
                    MemberBindingExpression(member));
            }
        }
        else
        {
            // When the member is only accessible through an explicitly-implemented interface
            // (e.g. IList.IsFixedSize on List<T>), the TypeChecker recorded an InterfaceCastLowering.
            // Wrap the receiver in a cast so codegen emits ((InterfaceType)obj).Member (#1572).
            var interfaceCast = _context.SemanticInfo?.GetInterfaceCastLowering(memberAccess);
            if (interfaceCast != null)
            {
                var interfaceType = MakeGlobalQualifiedName(interfaceCast.InterfaceTypeName.Split('.'));
                obj = ParenthesizedExpression(CastExpression(interfaceType, obj));
            }

            // obj.member (or ((InterfaceType)obj).member when interface-cast lowering is active)
            result = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                member);
        }

        // Apply the narrowed-read accessor the TypeChecker recorded for this member-access node, if
        // any: isinstance → parenthesized cast (((Dog)this.Animal)); self.field is not None →
        // .Unwrap()/.Value/! per the field's declared shape. Suppressed for assignment write targets
        // (applyNarrowing: false) — narrowing applies only to reads, and a narrowed LHS is not an lvalue.
        return applyNarrowing ? ApplyNarrowedReadLowering(memberAccess, result) : result;
    }

    /// <summary>
    /// Attempts to extract a module path from a member access chain.
    /// For example, lib.math.add becomes ["lib", "math", "add"].
    /// Returns true if the entire chain represents module access, false otherwise.
    /// </summary>
    private bool TryExtractModulePath(MemberAccess memberAccess, out List<string> modulePath)
    {
        modulePath = new List<string>();

        // Build the path by traversing the member access chain
        Expression current = memberAccess;
        while (current is MemberAccess ma)
        {
            // Add the member name to the front of the list
            modulePath.Insert(0, ma.Member);
            current = ma.Object;
        }

        // The base should be an identifier
        if (current is not Identifier identifier)
        {
            modulePath.Clear();
            return false;
        }

        // Add the base identifier to the front
        modulePath.Insert(0, identifier.Name);

        // Now check if this path represents module access
        // We need at least 2 parts (e.g., lib.math)
        if (modulePath.Count < 2)
        {
            modulePath.Clear();
            return false;
        }

        // Check if the base is a module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is not ModuleSymbol)
        {
            modulePath.Clear();
            return false;
        }

        // Verify that the path exists in the module hierarchy
        var currentModule = (ModuleSymbol)baseSymbol;  // Safe cast - we already checked it's a ModuleSymbol
        for (int i = 1; i < modulePath.Count; i++)
        {
            var memberName = modulePath[i];

            // Check if this member exists in the current module's exports
            if (!currentModule.Exports.TryGetValue(memberName, out var exportedSymbol))
            {
                modulePath.Clear();
                return false;
            }

            // If this is not the last element, it should be a nested module
            if (i < modulePath.Count - 1)
            {
                if (exportedSymbol is not ModuleSymbol nestedModule)
                {
                    modulePath.Clear();
                    return false;
                }
                currentModule = nestedModule;
            }
            // The last element can be any symbol (function, variable, or module)
        }

        return true;
    }

    /// <summary>
    /// Builds a C# member access expression from a module path.
    /// For example, ["lib", "math", "add"] becomes Lib.Math.Add.
    /// Special handling for imported modules: if the base is an imported module with a using alias,
    /// use the alias directly. For example, ["config", "MAX_SIZE"] with "import config" becomes
    /// "config.MaxSize" (using the alias created by the using directive).
    /// </summary>
    private ExpressionSyntax BuildModuleAccessExpression(List<string> modulePath)
    {
        if (modulePath.Count == 0)
        {
            throw new ArgumentException("Module path cannot be empty", nameof(modulePath));
        }

        // Check if the base is an imported module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is ModuleSymbol)
        {
            // For imported modules, we need to check if we have a using alias
            // For "import parent.child", the alias is "parent_child"
            // For accessing "parent.child.member", we use "parent_child.Member"

            // Find the longest module path prefix that matches an import
            // For example, if we have "import parent.child" and access "parent.child.child_func",
            // we want to find "parent.child" as the import and "child_func" as the member

            ModuleSymbol currentModule = (ModuleSymbol)baseSymbol;
            int modulePartCount = 1;

            // Try to traverse the module hierarchy to find how deep the imported module goes
            for (int i = 1; i < modulePath.Count; i++)
            {
                var memberName = modulePath[i];

                // Check if this is a nested module in the current module's exports
                if (currentModule.Exports.TryGetValue(memberName, out var exportedSymbol)
                    && exportedSymbol is ModuleSymbol nestedModule)
                {
                    currentModule = nestedModule;
                    modulePartCount++;
                }
                else
                {
                    // Not a nested module - this is a member access
                    break;
                }
            }

            // Build the import alias from the module path parts
            // Also escape C# keywords like "base" -> "@base"
            // For .NET namespace modules (e.g., system -> System), use the actual namespace name.
            // For sub-modules with a known C# class (e.g., numpy.random -> NumpyRandom),
            // use the fully qualified class name so np.random.seed(42) emits NumpyRandom.Seed(42).
            var moduleParts = modulePath.Take(modulePartCount);
            ExpressionSyntax moduleExpr;
            if (currentModule.CSharpClassName != null)
            {
                var ns = currentModule.CSharpNamespace ?? "Sharpy";
                moduleExpr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName(ns)),
                    IdentifierName(currentModule.CSharpClassName));
            }
            else if (currentModule.NetNamespaceName != null)
            {
                moduleExpr = IdentifierName(currentModule.NetNamespaceName);
            }
            else
            {
                moduleExpr = EscapedIdentifierName(EscapeCSharpKeyword(string.Join("_", moduleParts)));
            }

            // If the entire path is just the module (no member access), return it
            if (modulePartCount == modulePath.Count)
            {
                return moduleExpr;
            }

            // Build member access: module.Member1.Member2...
            ExpressionSyntax expr = moduleExpr;
            for (int i = modulePartCount; i < modulePath.Count; i++)
            {
                var memberPart = modulePath[i];

                string mangledMemberName;
                currentModule.Exports.TryGetValue(memberPart, out var exportSymbol);
                if (currentModule.IsNetModule && exportSymbol is VariableSymbol vs)
                {
                    mangledMemberName = vs.ClrFieldName ?? memberPart;
                }
                else if (NameFormDetector.IsConstantCaseName(memberPart))
                {
                    mangledMemberName = NameMangler.ToConstantCase(memberPart);
                }
                else
                {
                    mangledMemberName = NameCasing.ResolveMethod(memberPart, isBacktickEscaped: false, GetClrMethodName(exportSymbol));
                }

                expr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expr,
                    EscapedIdentifierName(mangledMemberName));
            }

            return expr;
        }

        // For multi-part module paths (e.g., lib.math.add) or other cases,
        // build the full qualified path (e.g., Lib.Math.Add)
        ExpressionSyntax currentExpr = IdentifierName(NameCasing.ResolveType(modulePath[0], isBacktickEscaped: false));

        // Chain the rest of the path
        for (int i = 1; i < modulePath.Count; i++)
        {
            // Use CONSTANT_CASE for ALL_CAPS names (Python-style constants)
            var memberPart = modulePath[i];
            var memberName = NameFormDetector.IsConstantCaseName(memberPart)
                ? NameMangler.ToConstantCase(memberPart)
                : NameCasing.ResolveMethod(memberPart, isBacktickEscaped: false);
            currentExpr = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                currentExpr,
                EscapedIdentifierName(memberName));
        }

        return currentExpr;
    }

    /// <summary>
    /// Reads the index-access lowering strategy from the lowering IR (E2 #1056, migrates
    /// <c>_indexAccessLowerings</c>). Returns <c>null</c> when the node has no
    /// <see cref="IrIndexAccess"/>, which the caller treats as the default native element access.
    /// </summary>
    private IndexAccessLowering? GetIrIndexAccessLowering(IndexAccess indexAccess)
    {
        return _context.Ir?.Index.TryGetValue(indexAccess, out var node) == true
            && node is IrIndexAccess irIndexAccess
            ? irIndexAccess.Strategy
            : null;
    }

    /// <summary>
    /// Reads the static-extension dispatch decision for a method-call member access from the lowering
    /// IR (E2 #1056, migrates <c>_staticExtensionDispatches</c>). Returns <c>null</c> when the call
    /// should emit as an ordinary instance-method invocation.
    /// </summary>
    private StaticExtensionDispatch? GetIrStaticExtensionDispatch(MemberAccess memberAccess)
    {
        return _context.Ir?.Index.TryGetValue(memberAccess, out var node) == true
            && node is IrMemberAccess irMemberAccess
            ? irMemberAccess.ExtensionDispatch
            : null;
    }

    /// <summary>
    /// Reads the resolved CLR member name for a member access from the lowering IR (E2 #1056,
    /// migrates <c>_resolvedClrMemberNames</c>). Returns <c>null</c> when none was recorded (codegen
    /// then applies normal name mangling).
    /// </summary>
    private string? GetIrResolvedClrMemberName(MemberAccess memberAccess)
    {
        return _context.Ir?.Index.TryGetValue(memberAccess, out var node) == true
            && node is IrMemberAccess irMemberAccess
            ? irMemberAccess.ResolvedClrMemberName
            : null;
    }

    private ExpressionSyntax GenerateIndexAccess(IndexAccess indexAccess)
    {
        // The lowering strategy was materialized during semantic analysis; switch on the tag alone.
        var lowering = GetIrIndexAccessLowering(indexAccess) ?? IndexAccessLowering.Native;

        // Tuple positional indexing: t[0] -> t.Item1, t[1] -> t.Item2, etc.
        // C# ValueTuples don't support [] indexing, so we emit .ItemN member access.
        if (lowering == IndexAccessLowering.TupleItem
            && TryGetConstantIntIndex(indexAccess.Index, out var tupleIndex))
        {
            var obj = GenerateExpression(indexAccess.Object);
            return ApplyNarrowedReadLowering(indexAccess, MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName($"Item{tupleIndex + 1}")));
        }

        // Spread TupleLiteral index into separate arguments for params indexers (#956).
        // a[1, 2] parses as IndexAccess(TupleLiteral) — spread to a[1, 2] in C# for
        // types with params int[] indexers (e.g., NdArray), instead of a[(1, 2)].
        if (lowering == IndexAccessLowering.ParamsSpread && indexAccess.Index is TupleLiteral tuple)
        {
            var objExprSpread = GenerateExpression(indexAccess.Object);
            var args = new List<ArgumentSyntax>();
            foreach (var elem in tuple.Elements)
                args.Add(Argument(GenerateExpression(elem)));
            return ApplyNarrowedReadLowering(indexAccess, ElementAccessExpression(objExprSpread)
                .AddArgumentListArguments(args.ToArray()));
        }

        var objExpr = GenerateExpression(indexAccess.Object);
        var index = GenerateExpression(indexAccess.Index);

        // Compose the container access with any narrowed-read accessor the TypeChecker recorded for
        // this index node (e.g. list[int?] → xs.GetItemUnchecked(0).Unwrap(); list[int | None] →
        // xs[0].Value): container access first, then the accessor (#1081).
        ExpressionSyntax indexResult = lowering switch
        {
            // String indexing: s[i] -> StringHelpers.GetItem(s, i) to return string, not char,
            // and to support negative indexing.
            IndexAccessLowering.String => InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                        IdentifierName("GetItem")))
                .AddArgumentListArguments(Argument(objExpr), Argument(index)),

            // Array indexing: arr[i] -> ArrayHelpers.GetItem(arr, i) to support negative indexing.
            IndexAccessLowering.Array => InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                        IdentifierName("GetItem")))
                .AddArgumentListArguments(Argument(objExpr), Argument(index)),

            // Provably non-negative list access: xs[i] -> xs.GetItemUnchecked(i), skipping the
            // negative-index Normalize the ordinary indexer runs (#1052). The TypeChecker proved
            // the index is >= 0; bounds are still enforced by GetItemUnchecked.
            IndexAccessLowering.NativeUnchecked => InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        objExpr,
                        IdentifierName("GetItemUnchecked")))
                .AddArgumentListArguments(Argument(index)),

            _ => ElementAccessExpression(objExpr)
                .AddArgumentListArguments(Argument(index))
        };

        return ApplyNarrowedReadLowering(indexAccess, indexResult);
    }

    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Delegates to <see cref="AstHelper.TryGetConstantIntIndex"/>.
    /// </summary>
    private static bool TryGetConstantIntIndex(Expression expr, out int value)
        => AstHelper.TryGetConstantIntIndex(expr, out value);

    private ExpressionSyntax GenerateSliceAccess(SliceAccess sliceAccess)
    {
        var lowering = _context.SemanticInfo?.GetSliceLowering(sliceAccess)
            ?? throw new InvalidOperationException(
                "No SliceLowering recorded for slice access — semantic analysis must classify " +
                "every receiver the emitter is asked to generate (#1608)");

        var obj = GenerateExpression(sliceAccess.Object);

        // GenerateExpression applies ApplyNarrowedReadLowering, which casts builtin collections
        // to non-generic protocol interfaces (IList/IDict/ISet). GetSlice<T> needs the concrete
        // generic type for T inference — re-cast to it when the narrowing erased generics (#1608).
        var narrowedLowering = _context.SemanticInfo?.GetNarrowedReadLowering(sliceAccess.Object);
        if (narrowedLowering is { Kind: NarrowedReadKind.Cast, CastTarget: GenericType narrowedGeneric }
            && TryMapBuiltinCollectionToNonGenericInterface(narrowedGeneric.Name) is not null)
        {
            var concreteType = _typeMapper.MapSemanticType(narrowedGeneric);
            obj = ParenthesizedExpression(CastExpression(concreteType, obj));
        }

        var result = lowering.Kind switch
        {
            // NdArray slicing is per-axis: a[1:4] → a.Slice(new SliceSpec((int?)1, (int?)4)).
            // Slice() requires one spec per dimension, so a single-axis slice of a higher-rank
            // array throws IndexError at runtime (unlike numpy, which pads the trailing axes).
            SliceLoweringKind.NdArray => InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        obj,
                        IdentifierName("Slice")))
                .AddArgumentListArguments(Argument(
                    GenerateSliceSpec(sliceAccess.Start, sliceAccess.Stop, sliceAccess.Step))),

            // List/Array/Str/Bytes → Sharpy.Slice.GetSlice(obj, start, end, step)
            SliceLoweringKind.List or SliceLoweringKind.Array
                or SliceLoweringKind.Str or SliceLoweringKind.Bytes =>
                GenerateGetSliceCall(obj, sliceAccess),

            // #1610: user __getitem__(slice) → obj[new Slice(start, stop, step)]
            SliceLoweringKind.UserProtocol =>
                ElementAccessExpression(obj)
                .AddArgumentListArguments(Argument(
                    GenerateNewSlice(sliceAccess.Start, sliceAccess.Stop, sliceAccess.Step))),

            // #1609: tuple constant-bound slicing → ValueTuple.Create(t.Item1, t.Item2, ...)
            SliceLoweringKind.Tuple => GenerateTupleSlice(obj, lowering),

            _ => throw new InvalidOperationException(
                $"Unhandled SliceLoweringKind '{lowering.Kind}' (#1608)"),
        };

        return result;
    }

    private ExpressionSyntax GenerateGetSliceCall(ExpressionSyntax obj, SliceAccess sliceAccess)
    {
        var start = sliceAccess.Start != null
            ? GenerateExpression(sliceAccess.Start)
            : (ExpressionSyntax)LiteralExpression(SyntaxKind.NullLiteralExpression);
        var end = sliceAccess.Stop != null
            ? GenerateExpression(sliceAccess.Stop)
            : (ExpressionSyntax)LiteralExpression(SyntaxKind.NullLiteralExpression);
        var step = sliceAccess.Step != null
            ? GenerateExpression(sliceAccess.Step)
            : (ExpressionSyntax)LiteralExpression(SyntaxKind.NullLiteralExpression);

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MakeGlobalQualifiedName("Sharpy", "Slice"),
                IdentifierName("GetSlice")))
            .AddArgumentListArguments(
                Argument(obj),
                Argument(start),
                Argument(end),
                Argument(step));
    }

    /// <summary>
    /// #1609: tuple constant-bound slicing — <c>t[1:3]</c> on <c>(int, str, float)</c> →
    /// <c>System.ValueTuple.Create(t.Item2, t.Item3)</c>.
    /// </summary>
    private ExpressionSyntax GenerateTupleSlice(ExpressionSyntax obj, SliceLowering lowering)
    {
        var indices = lowering.TupleElementIndices
            ?? throw new InvalidOperationException("Tuple slice lowering has no element indices");

        var args = indices.Select(i =>
            Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                obj, IdentifierName($"Item{i + 1}")))).ToArray();

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                ValueTupleTypeAccess(),
                IdentifierName("Create")))
            .AddArgumentListArguments(args);
    }

    /// <summary>
    /// #1610: <c>new global::Sharpy.Slice(start, stop, step)</c> for user-protocol slicing.
    /// </summary>
    private ExpressionSyntax GenerateNewSlice(Expression? startExpr, Expression? stopExpr, Expression? stepExpr)
    {
        var nullableInt = NullableType(PredefinedType(Token(SyntaxKind.IntKeyword)));
        var start = startExpr != null
            ? (ExpressionSyntax)CastExpression(nullableInt, GenerateExpression(startExpr))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var stop = stopExpr != null
            ? (ExpressionSyntax)CastExpression(nullableInt, GenerateExpression(stopExpr))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var step = stepExpr != null
            ? (ExpressionSyntax)CastExpression(nullableInt, GenerateExpression(stepExpr))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        return ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "Slice"))
            .AddArgumentListArguments(Argument(start), Argument(stop), Argument(step));
    }

    private ExpressionSyntax GenerateMultiAxisAccess(MultiAxisAccess multiAxis)
    {
        var obj = GenerateExpression(multiAxis.Object);

        var hasSlice = false;
        foreach (var dim in multiAxis.Dimensions)
        {
            if (dim.IsSlice)
            {
                hasSlice = true;
                break;
            }
        }

        if (!hasSlice)
        {
            // All indices: a[1, 2] → a[1, 2] (spread into params int[])
            var args = new List<ArgumentSyntax>();
            foreach (var dim in multiAxis.Dimensions)
                args.Add(Argument(GenerateExpression(dim.Index!)));
            return ElementAccessExpression(obj)
                .AddArgumentListArguments(args.ToArray());
        }

        // Any slice: a[1:3, :] → a.Slice(new SliceSpec(1, 3), SliceSpec.All)
        var sliceArgs = new List<ArgumentSyntax>();
        foreach (var dim in multiAxis.Dimensions)
        {
            if (dim.IsSlice)
            {
                sliceArgs.Add(Argument(GenerateSliceSpec(dim)));
            }
            else
            {
                // Index dimension → SliceSpec.Range(i, i + 1)
                // Generate the expression twice: Roslyn SyntaxNodes cannot be
                // shared across two positions in the syntax tree.
                var idxForStart = GenerateExpression(dim.Index!);
                var idxForEnd = GenerateExpression(dim.Index!);
                sliceArgs.Add(Argument(
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            MakeGlobalQualifiedName("Sharpy", "SliceSpec"),
                            IdentifierName("Range")))
                    .AddArgumentListArguments(
                        Argument(idxForStart),
                        Argument(BinaryExpression(SyntaxKind.AddExpression,
                            idxForEnd, LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)))))));
            }
        }

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName("Slice")))
            .AddArgumentListArguments(sliceArgs.ToArray());
    }

    private ExpressionSyntax GenerateSliceSpec(SubscriptDimension dim)
        => GenerateSliceSpec(dim.Start, dim.Stop, dim.Step);

    /// <summary>
    /// Builds the <c>Sharpy.SliceSpec</c> value for one axis: <c>SliceSpec.All</c> when every
    /// bound is absent, otherwise <c>new SliceSpec((int?)start, (int?)stop[, (int?)step])</c>.
    /// Shared by multi-axis dimensions and single-axis ndarray slices (#1608).
    /// </summary>
    private ExpressionSyntax GenerateSliceSpec(Expression? start, Expression? stop, Expression? step)
    {
        if (start == null && stop == null && step == null)
        {
            return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MakeGlobalQualifiedName("Sharpy", "SliceSpec"),
                IdentifierName("All"));
        }

        var args = new List<ArgumentSyntax>();

        args.Add(Argument(start != null
            ? CastExpression(NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(start))
            : LiteralExpression(SyntaxKind.NullLiteralExpression)));

        args.Add(Argument(stop != null
            ? CastExpression(NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(stop))
            : LiteralExpression(SyntaxKind.NullLiteralExpression)));

        if (step != null)
        {
            args.Add(Argument(CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(step))));
        }

        return ObjectCreationExpression(
                MakeGlobalQualifiedName("Sharpy", "SliceSpec"))
            .WithArgumentList(ArgumentList(SeparatedList(args)));
    }

    private TypeSymbol? ResolveNestedTypeFromAccess(MemberAccess memberAccess)
    {
        if (memberAccess.Object is Identifier outerTypeId)
        {
            var outerSym = _context.LookupSymbol(outerTypeId.Name);
            if (outerSym is TypeSymbol outerTypeSym)
            {
                return outerTypeSym.NestedTypes.FirstOrDefault(
                    n => n.Name == memberAccess.Member);
            }
        }

        if (memberAccess.Object is MemberAccess innerAccess)
        {
            var parentSym = ResolveNestedTypeFromAccess(innerAccess);
            if (parentSym != null)
            {
                return parentSym.NestedTypes.FirstOrDefault(
                    n => n.Name == memberAccess.Member);
            }

            // Module-qualified nested type: lib.Registry.Entry — the root is a module,
            // the next segment is a type in its exports, and the member is a nested type.
            if (innerAccess.Object is Identifier moduleId)
            {
                var moduleSym = _context.LookupSymbol(moduleId.Name);
                if (moduleSym is ModuleSymbol mod
                    && mod.Exports.TryGetValue(innerAccess.Member, out var exported)
                    && exported is TypeSymbol exportedType)
                {
                    return exportedType.NestedTypes.FirstOrDefault(
                        n => n.Name == memberAccess.Member);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The single builder for a nested type's qualified C# name: <c>Outer.Inner</c>, with type
    /// arguments on the innermost segment only — <c>Outer.Inner&lt;int&gt;</c>, never
    /// <c>Outer&lt;int&gt;.Inner</c>, since the nested type declares them and its enclosing types do
    /// not. Both construction spellings use it (#1217); the second builder it replaced joined
    /// segments into a string and round-tripped them through <c>ParseQualifiedName</c>.
    ///
    /// <para>Every segment is cased from the SYMBOL, deliberately. The replaced builder passed the
    /// source's <c>IsMemberBacktickEscaped</c> through for the innermost segment only, so a
    /// reference could disagree with its own declaration and fail CS0426. Casing from the symbol is
    /// what makes the reference agree with the declaration (#1217) — and the symbol carries the
    /// escape flag, so <c>class `data`</c> now declares and references <c>data</c> alike. Reading
    /// the flag from the symbol rather than the reference site is what keeps the two sides in
    /// agreement no matter how the reference happens to be spelled.</para>
    ///
    /// <para>The keyword escaping the issue reported as divergent was already identical in both
    /// builders: <c>NameMangler.Transform(x, NameContext.Type)</c> and
    /// <c>NameCasing.ResolveType(x, isBacktickEscaped: false)</c> both PascalCase and then escape,
    /// and PascalCasing a lowercase keyword (<c>class</c> → <c>Class</c>) removes the collision
    /// before escaping is consulted.</para>
    /// </summary>
    private NameSyntax BuildNestedTypeName(TypeSymbol nestedSym, TypeSyntax[]? typeArguments = null)
    {
        var parts = new List<string>();
        var current = nestedSym;
        while (current != null)
        {
            parts.Add(NameCasing.ResolveType(current.Name, current.IsNameBacktickEscaped));
            current = current.DeclaringType;
        }
        parts.Reverse();

        // Only the innermost segment carries the type arguments — Outer.Inner<int>, never
        // Outer<int>.Inner: the nested type declares them, its enclosing types do not.
        SimpleNameSyntax Segment(int index) =>
            index == parts.Count - 1 && typeArguments is { Length: > 0 }
                ? GenericName(EscapedIdentifier(parts[index]))
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArguments)))
                : EscapedIdentifierName(parts[index]);

        NameSyntax result = Segment(0);
        for (int i = 1; i < parts.Count; i++)
        {
            result = QualifiedName(result, (SimpleNameSyntax)Segment(i));
        }
        return result;
    }
}
