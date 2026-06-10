using System.IO;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
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
        // Handle functools.partial(f, ...) — compatibility shim that emits an equivalent lambda
        if (Semantic.FunctoolsPartialHelper.IsFunctoolsPartialCall(call, _context.SymbolTable))
        {
            return GenerateFunctoolsPartialCall(call);
        }

        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericName)
        {
            var result = GenerateGenericInstantiation(indexAccess, genericName, call);
            if (result != null)
                return result;
        }

        // Handle generic nested type instantiation: Outer.Inner[int](42)
        // and generic module function calls: json.loads[int](text)
        if (call.Function is IndexAccess nestedIndexAccess &&
            nestedIndexAccess.Object is MemberAccess nestedMemberAccess)
        {
            var result = GenerateNestedGenericInstantiation(nestedIndexAccess, nestedMemberAccess, call);
            if (result != null)
                return result;

            result = GenerateModuleGenericFunctionCall(nestedIndexAccess, nestedMemberAccess, call);
            if (result != null)
                return result;

            // Generic module-qualified type instantiation: difflib.SequenceMatcher[str](None, a, b)
            result = GenerateModuleGenericTypeInstantiation(nestedIndexAccess, nestedMemberAccess, call);
            if (result != null)
                return result;
        }

        if (call.Function is Identifier funcName)
        {
            // Check if this is a builtin function call (e.g., int(), str(), print(), len(), etc.)
            var isBuiltinFunc = _context.IsBuiltinFunction(funcName.Name);

            // User-defined functions shadow builtins (Python scoping rules):
            // A FunctionSymbol with CodeGenInfo was processed by semantic analysis (user-defined),
            // while builtin functions from the registry won't have CodeGenInfo set.
            var symbol = _context.LookupSymbol(funcName.Name);
            if (isBuiltinFunc && symbol is FunctionSymbol { CodeGenInfo: not null })
                isBuiltinFunc = false;

            // Handle direct calls to asyncio functions (from asyncio import gather, sleep)
            if (symbol is FunctionSymbol { OriginalModule: Shared.SyntheticModuleNames.Asyncio })
            {
                return GenerateAsyncioCall(funcName.Name, call);
            }

            // isinstance(expr, Type) → expr is Type
            // Must intercept BEFORE argument evaluation because the second argument
            // is a type identifier, not a value expression.
            if (funcName.Name == BuiltinFunctionNames.IsInstance
                && call.Arguments.Length == 2
                && call.Arguments[1] is Identifier typeId)
            {
                var value = GenerateExpression(call.Arguments[0]);
                // Use TypeSyntaxMapper to correctly resolve builtin types (str→string, int→int)
                // and user-defined types (dog→Dog) via MapType.
                var typeAnnotation = new TypeAnnotation { Name = typeId.Name };
                var checkType = _typeMapper.MapType(typeAnnotation);
                return BinaryExpression(SyntaxKind.IsExpression, value, checkType);
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
            FunctionSymbol? directCallTarget = symbol as FunctionSymbol;
            if (directCallTarget == null && symbol is TypeSymbol callTypeSymbol)
            {
                directCallTarget = ResolveConstructorForCall(callTypeSymbol, call);
            }
            var allArgs = GenerateReorderedCallArguments(call, directCallTarget);

            if (isBuiltinFunc)
            {
                // Generic builtins need explicit type arguments
                if (funcName.Name is BuiltinNames.Reversed or BuiltinNames.Sorted)
                {
                    return GenerateGenericBuiltinCall(funcName.Name, call, allArgs);
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

                // For generic types called without explicit type arguments (e.g., set(), Cell(42)),
                // use the resolved expression type to supply type arguments.
                var exprType = _context.SemanticInfo?.GetExpressionType(call);
                if (exprType is GenericType resolvedGeneric && resolvedGeneric.TypeArguments.Count > 0
                    && resolvedGeneric.TypeArguments.All(t => t is not UnknownType))
                {
                    var typeArgsSyntax = resolvedGeneric.TypeArguments
                        .Select(t => _typeMapper.MapSemanticType(t))
                        .ToArray();
                    var csharpCollectionName = CSharpTypeNames.FromSharpyName(funcName.Name)
                        ?? NameCasing.ResolveType(funcName.Name, funcName.IsNameBacktickEscaped);
                    var needsGlobalQualification = !string.IsNullOrEmpty(_context.ProjectNamespace)
                        && csharpCollectionName.Contains('.', StringComparison.Ordinal);
                    var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpCollectionName,
                            needsGlobalQualification, typeArgsSyntax);

                    // DefaultDict: wrap type-reference arguments in factory lambdas.
                    // DefaultDict(list) → new DefaultDict<string, List<int>>(() => new List<int>())
                    if (string.Equals(funcName.Name, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase)
                        && typeArgsSyntax.Length >= 2
                        && call.Arguments.Length >= 1)
                    {
                        allArgs = WrapDefaultDictFactoryArgs(call, allArgs, typeArgsSyntax[1]);
                    }

                    return ObjectCreationExpression(genericTypeSyntax)
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                // Fallback: if the expression type has UnknownType type args, check for
                // inferred type arguments from semantic analysis (generic constructor inference).
                // C# does not support generic constructor inference, so we must always emit
                // explicit type arguments: Cell(42) -> new Cell<int>(42)
                var inferredTypeArgs = _context.SemanticInfo?.GetInferredTypeArguments(call);
                if (inferredTypeArgs is { Count: > 0 })
                {
                    var typeArgsSyntax = inferredTypeArgs
                        .Select(t => _typeMapper.MapSemanticType(t));
                    var csharpName = CSharpTypeNames.FromSharpyName(funcName.Name)
                        ?? NameCasing.ResolveType(funcName.Name, funcName.IsNameBacktickEscaped);
                    var needsGlobalQualification = !string.IsNullOrEmpty(_context.ProjectNamespace)
                        && csharpName.Contains('.', StringComparison.Ordinal);
                    var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpName,
                            needsGlobalQualification, typeArgsSyntax.ToArray());
                    return ObjectCreationExpression(genericTypeSyntax)
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                // For builtin collection types, use the fully-qualified Sharpy.X name.
                // If we reached here, the expression type didn't have valid generic args.
                // Try to infer element type from the constructor argument's semantic type:
                // list(d.keys()) → new Sharpy.List<string>(d.Keys) when d.keys() is IEnumerable<string>
                var collectionName = CSharpTypeNames.FromSharpyName(funcName.Name);
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
                        : ParseName(collectionName);
                    return ObjectCreationExpression(collectionTypeSyntax)
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                NameSyntax typeSyntax = name.StartsWith("global::", StringComparison.Ordinal)
                    ? MakeGlobalQualifiedName(name.Substring("global::".Length).Split('.'))
                    : ParseName(name);
                return ObjectCreationExpression(typeSyntax)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Regular function call — check if this is a local variable/parameter (callable)
            // before falling back to PascalCase for module-level functions
            var codeGenInfo = symbol != null ? GetCodeGenInfo(symbol) : null;
            string funcCSharpName;
            if (_variableVersions.ContainsKey(_nameResolutionService.GetBaseName(funcName.Name)))
            {
                funcCSharpName = GetMangledVariableName(funcName.Name, isNewDeclaration: false);
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
            // generate through GenerateExpression so .Unwrap() is applied before invocation.
            ExpressionSyntax calleeExpr;
            if (_narrowing.IsNarrowed(funcName.Name))
                calleeExpr = GenerateExpression(call.Function);
            else
                calleeExpr = ParseName(funcCSharpName);
            return InvocationExpression(calleeExpr)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Handle method calls on objects: obj.method() or ClassName.static_method()
        if (call.Function is MemberAccess memberAccess)
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
            {
                var nestedSym = ResolveNestedTypeFromAccess(memberAccess);
                if (nestedSym != null && (nestedSym.TypeKind == Semantic.TypeKind.Class ||
                                          nestedSym.TypeKind == Semantic.TypeKind.Struct))
                {
                    var qualifiedName = BuildNestedTypeName(nestedSym);
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
                    var ctorTarget = ResolveConstructorForCall(mt.Symbol, call);
                    var ctorArgs = GenerateReorderedCallArguments(call, ctorTarget);
                    var baseName = GetFullyQualifiedTypeName(mt.Symbol, mt.OriginalName);
                    return GenerateTypeInstantiation(call, baseName, ctorArgs);
                }
            }

            // Handle static method calls on primitive types: int.parse(s), float.parse(s)
            // The TypeChecker records these via SetMemberAccessResolution. We intercept here
            // to emit the correct Sharpy.Core helper class call instead of trying to generate
            // an expression for the type name (which would produce invalid C#).
            var staticResolution = _context.SemanticInfo?.GetMemberAccessResolution(memberAccess);
            if (staticResolution is { } sr && sr.Member is FunctionSymbol { IsStatic: true } staticMethod)
            {
                var staticCallTarget = GetPrimitiveStaticCallTarget(sr.Owner.Name, staticMethod.Name);
                if (staticCallTarget != null)
                {
                    var staticArgs = call.Arguments.Select(a => Argument(GenerateExpression(a))).ToArray();
                    return InvocationExpression(ParseName(staticCallTarget))
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
            // method — generate through GenerateMemberAccess so the Optional narrowing
            // (.Unwrap()) is applied to the callee, then invoke the resulting delegate.
            var calleeDottedPath = TryBuildDottedPath(memberAccess);
            if (calleeDottedPath != null && _narrowing.IsNarrowed(calleeDottedPath)
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
                ?? _context.SemanticInfo?.GetMemberAccessResolution(memberAccess)?.Member
                ?? ResolveMethodForCall(memberAccess.Object, memberAccess.Member);
            var resolvedClrMethodName = GetClrMethodName(resolvedMethodSymbol)
                ?? ResolveClrMethodNameByReflection(memberAccess.Object, memberAccess.Member);
            var methodName = DunderMapping.ResolveCSharpName(memberAccess.Member)
                ?? NameMangler.GetListMethodMapping(memberAccess.Member)
                ?? NameCasing.ResolveMethod(memberAccess.Member, memberAccess.IsMemberBacktickEscaped, resolvedClrMethodName);

            // CLR property access: if the member is a property (not a method) on a
            // discovery-loaded type and the call has no arguments, emit property access
            // without invocation parens. E.g., Python d.keys() → C# d.Keys (not d.Keys()).
            if (call.Arguments.Length == 0 && IsClrPropertyAccess(memberAccess.Object, memberAccess.Member))
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

            // Generate arguments (reorder for C# compliance if needed)
            var methodCallTarget = ResolveMethodForCall(memberAccess.Object, memberAccess.Member);
            var allArgs = GenerateReorderedCallArguments(call, methodCallTarget);

            // Handle null conditional method calls: obj?.Method(args)
            if (memberAccess.IsNullConditional)
            {
                return GenerateNullConditionalMethodCall(obj, memberAccess, methodName, allArgs, call);
            }

            // Interface default method promotion: if the method is a default method
            // on an interface (not overridden by the class), call through an interface cast.
            // In C#, default interface methods can only be called through interface-typed refs.
            var defaultMethodInterface = TryGetDefaultMethodInterface(memberAccess.Object, memberAccess.Member);
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

            // Generate: obj.Method(args)
            var methodAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(methodName));

            return InvocationExpression(methodAccess)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Fallback: arbitrary expression as call target
        // Handles: get_handler()("arg"), callbacks[0]("arg"), (lambda x: x)(42), chained calls, etc.
        var callTarget = GenerateExpression(call.Function);

        // Lambdas need explicit delegate cast for invocation in C#: ((Func<int, int>)(x => x * 2))(21)
        // The lambda may be bare or wrapped in a Parenthesized AST node → ParenthesizedExpressionSyntax
        var innerExprForCheck = callTarget;
        if (innerExprForCheck is ParenthesizedExpressionSyntax parenSyntax)
            innerExprForCheck = parenSyntax.Expression;

        if (innerExprForCheck is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
        {
            // Get the type of the lambda from semantic info (unwrap Parenthesized AST nodes)
            var funcAstNode = call.Function;
            while (funcAstNode is Parenthesized p)
                funcAstNode = p.Expression;

            var lambdaType = _context.SemanticInfo?.GetExpressionType(funcAstNode);
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
    /// Handle generic type/function instantiation: Box[int](42) or identity[int](42).
    /// Parsed as FunctionCall(Function: IndexAccess(Object: Box/identity, Index: int), Arguments: [42]).
    /// Returns null if the symbol is neither a generic type nor a generic function.
    /// </summary>
    private ExpressionSyntax? GenerateGenericInstantiation(
        IndexAccess indexAccess, Identifier genericName, FunctionCall call)
    {
        // Handle array construction: array[T](size) -> new T[size]
        if (genericName.Name == BuiltinNames.Array && call.Arguments.Length == 1)
        {
            var elementType = _typeMapper.MapTypeFromExpression(indexAccess.Index);
            var sizeExpr = GenerateExpression(call.Arguments[0]);
            return ArrayCreationExpression(
                ArrayType(elementType)
                    .AddRankSpecifiers(
                        ArrayRankSpecifier(
                            SingletonSeparatedList<ExpressionSyntax>(sizeExpr))));
        }

        var symbol = _context.LookupSymbol(genericName.Name);

        // Map the type argument(s)
        var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);

        if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
        {
            // Generate: new GenericType<TypeArgs>(args)
            var csharpGenericTypeName = CSharpTypeNames.FromSharpyName(genericName.Name)
                ?? NameCasing.ResolveType(genericName.Name, genericName.IsNameBacktickEscaped);
            var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpGenericTypeName, typeArgsSyntax);

            // Generate arguments (reorder for C# compliance if needed)
            var genericTypeCallTarget = ResolveConstructorForCall(genericTypeSymbol, call);
            var allArgs = GenerateReorderedCallArguments(call, genericTypeCallTarget);

            // DefaultDict: wrap type-reference arguments in factory lambdas.
            // defaultdict[str, list[int]](list) → new DefaultDict<string, List<long>>(() => new List<long>())
            // The DefaultDict constructor takes Func<TValue>, not a type reference.
            if (string.Equals(genericName.Name, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase)
                && call.Arguments.Length >= 1
                && typeArgsSyntax.Length >= 2)
            {
                allArgs = WrapDefaultDictFactoryArgs(call, allArgs, typeArgsSyntax[1]);
            }

            return ObjectCreationExpression(genericTypeSyntax)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
        {
            // Generate: GenericFunction<TypeArgs>(args)
            var genericFuncSyntax = GenericName(NameCasing.ResolveMethod(genericName.Name, genericName.IsNameBacktickEscaped, GetClrMethodName(genericFuncSymbol)))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

            // Generate arguments (reorder for C# compliance if needed)
            var allArgs = GenerateReorderedCallArguments(call, genericFuncSymbol);

            // Builtin generic functions need qualification: global::Sharpy.Builtins.Map<T>(...)
            if (_context.IsBuiltinFunction(genericName.Name))
            {
                var qualifiedBase = MakeGlobalQualifiedName("Sharpy", "Builtins");
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, qualifiedBase, genericFuncSyntax))
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            return InvocationExpression(genericFuncSyntax)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        return null;
    }

    private ExpressionSyntax? GenerateNestedGenericInstantiation(
        IndexAccess indexAccess, MemberAccess memberAccess, FunctionCall call)
    {
        var nestedTypeSymbol = LookupNestedTypeFromMemberAccess(memberAccess);
        if (nestedTypeSymbol == null || !nestedTypeSymbol.IsGeneric)
            return null;

        var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);
        var csharpName = NameCasing.ResolveType(memberAccess.Member, isBacktickEscaped: memberAccess.IsMemberBacktickEscaped);
        var outerName = GetNestedTypeOuterPrefix(nestedTypeSymbol);
        var qualifiedGenericName = QualifiedName(
            ParseName(outerName),
            GenericName(csharpName)
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax))));

        var constructorTarget = ResolveConstructorForCall(nestedTypeSymbol, call);
        var allArgs = GenerateReorderedCallArguments(call, constructorTarget);

        return ObjectCreationExpression(qualifiedGenericName)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    private TypeSymbol? LookupNestedTypeFromMemberAccess(MemberAccess memberAccess)
    {
        if (memberAccess.Object is Identifier outerName)
        {
            var outerSymbol = _context.LookupSymbol(outerName.Name) as TypeSymbol;
            return outerSymbol?.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member);
        }
        if (memberAccess.Object is MemberAccess innerAccess)
        {
            var parentType = LookupNestedTypeFromMemberAccess(innerAccess);
            return parentType?.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member);
        }
        return null;
    }

    private static string GetNestedTypeOuterPrefix(TypeSymbol nestedType)
    {
        var parts = new List<string>();
        var declaring = nestedType.DeclaringType;
        while (declaring != null)
        {
            parts.Add(NameCasing.ResolveType(declaring.Name, isBacktickEscaped: false));
            declaring = declaring.DeclaringType;
        }
        parts.Reverse();
        return string.Join(".", parts);
    }

    private ExpressionSyntax? GenerateModuleGenericFunctionCall(
        IndexAccess indexAccess, MemberAccess memberAccess, FunctionCall call)
    {
        if (memberAccess.Object is not Identifier moduleId)
            return null;

        var moduleSymbol = _context.LookupSymbol(moduleId.Name) as ModuleSymbol;
        if (moduleSymbol == null)
            return null;

        var memberName = memberAccess.Member;
        if (!moduleSymbol.Exports.ContainsKey(memberName) && moduleSymbol.IsNetModule)
        {
            var pascalName = NameCasing.ResolveMethod(memberName, isBacktickEscaped: memberAccess.IsMemberBacktickEscaped);
            if (moduleSymbol.Exports.ContainsKey(pascalName))
                memberName = pascalName;
        }

        if (!moduleSymbol.Exports.TryGetValue(memberName, out var exportedSymbol)
            || exportedSymbol is not FunctionSymbol funcSymbol
            || !funcSymbol.IsGeneric)
        {
            return null;
        }

        var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);
        var genericMethodName = GenericName(NameCasing.ResolveMethod(memberName, memberAccess.IsMemberBacktickEscaped, GetClrMethodName(funcSymbol)))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgsSyntax)));

        var moduleExpr = GenerateExpression(memberAccess.Object);
        var qualifiedCall = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression, moduleExpr, genericMethodName);

        var allArgs = GenerateReorderedCallArguments(call, funcSymbol);
        return InvocationExpression(qualifiedCall)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Handles generic instantiation of a module-qualified type:
    /// <c>difflib.SequenceMatcher[str](None, a, b)</c> →
    /// <c>new global::...SequenceMatcher&lt;string&gt;(None, a, b)</c>.
    /// Parsed as FunctionCall(Function: IndexAccess(Object: MemberAccess(module, Type), Index: T), Arguments).
    /// Returns null if the member access does not denote a generic module-exported type.
    /// </summary>
    private ExpressionSyntax? GenerateModuleGenericTypeInstantiation(
        IndexAccess indexAccess, MemberAccess memberAccess, FunctionCall call)
    {
        var moduleType = TryResolveModuleExportedType(memberAccess);
        if (moduleType is not { } mt || !mt.Symbol.IsGeneric
            || (mt.Symbol.TypeKind != Semantic.TypeKind.Class
                && mt.Symbol.TypeKind != Semantic.TypeKind.Struct))
        {
            return null;
        }

        var typeArgsSyntax = _typeMapper.MapTypeArgumentsFromExpression(indexAccess.Index);
        var baseName = GetFullyQualifiedTypeName(mt.Symbol, mt.OriginalName);
        var (dottedName, globalQualified) = NormalizeTypeName(baseName);
        var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(
            dottedName, globalQualified, typeArgsSyntax);

        var ctorTarget = ResolveConstructorForCall(mt.Symbol, call);
        var allArgs = GenerateReorderedCallArguments(call, ctorTarget);
        return ObjectCreationExpression(genericTypeSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
    }

    /// <summary>
    /// Emits an <c>ObjectCreationExpression</c> for a constructor call, supplying explicit
    /// generic type arguments when the resolved type is generic (C# has no generic constructor
    /// inference). Shared by the module-qualified member-access constructor path.
    /// <paramref name="baseCSharpName"/> is the C# type name WITHOUT type arguments (possibly
    /// <c>global::</c>-prefixed and/or dotted), as produced by <see cref="GetFullyQualifiedTypeName"/>.
    /// </summary>
    private ExpressionSyntax GenerateTypeInstantiation(
        FunctionCall call, string baseCSharpName, ArgumentSyntax[] allArgs)
    {
        var (dottedName, globalQualified) = NormalizeTypeName(baseCSharpName);

        // Explicit generic type arguments from the resolved expression type
        // (e.g., a generic module type called without an explicit subscript).
        var exprType = _context.SemanticInfo?.GetExpressionType(call);
        if (exprType is GenericType resolvedGeneric && resolvedGeneric.TypeArguments.Count > 0
            && resolvedGeneric.TypeArguments.All(t => t is not UnknownType))
        {
            var typeArgsSyntax = resolvedGeneric.TypeArguments
                .Select(t => _typeMapper.MapSemanticType(t))
                .ToArray();
            return ObjectCreationExpression(
                    TypeSyntaxMapper.QualifiedGenericName(dottedName, globalQualified, typeArgsSyntax))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Fallback: inferred type arguments from generic constructor inference.
        var inferredTypeArgs = _context.SemanticInfo?.GetInferredTypeArguments(call);
        if (inferredTypeArgs is { Count: > 0 })
        {
            var typeArgsSyntax = inferredTypeArgs
                .Select(t => _typeMapper.MapSemanticType(t))
                .ToArray();
            return ObjectCreationExpression(
                    TypeSyntaxMapper.QualifiedGenericName(dottedName, globalQualified, typeArgsSyntax))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Non-generic construction.
        NameSyntax typeSyntax = globalQualified
            ? MakeGlobalQualifiedName(dottedName.Split('.'))
            : ParseName(dottedName);
        return ObjectCreationExpression(typeSyntax)
            .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
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
            return (baseCSharpName.Substring("global::".Length), true);

        var globalQualified = !string.IsNullOrEmpty(_context.ProjectNamespace)
            && baseCSharpName.Contains('.', StringComparison.Ordinal);
        return (baseCSharpName, globalQualified);
    }

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

        // Check if the first argument is a type reference used as a callable factory.
        // In Python, defaultdict(list) passes the type `list` as a factory callable.
        // We need to detect this and wrap it in a lambda: () => new ValueType()
        var firstArg = call.Arguments[0];
        if (firstArg is not Identifier argId)
            return allArgs;

        // The DefaultDict constructor takes Func<TValue>. When the user writes
        // defaultdict(list), defaultdict(int), etc., the argument is a type name used
        // as a factory callable. Detect this by checking if the argument name is a known
        // type constructor (builtin type or collection type). We check multiple resolution
        // paths because 'list' can resolve as FunctionSymbol (builtin function), TypeSymbol,
        // or both depending on context.
        var argSymbol = _context.LookupSymbol(argId.Name);
        var resolvedSymbol = _context.SemanticInfo?.GetIdentifierSymbol(argId);
        var isTypeFactory = argSymbol is TypeSymbol
            || resolvedSymbol is TypeSymbol
            || CSharpTypeNames.FromSharpyName(argId.Name) != null
            || _context.IsBuiltinFunction(argId.Name);

        if (!isTypeFactory)
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

        // Infer element type from first argument's semantic type
        TypeSyntax? typeArg = null;
        if (call.Arguments.Length > 0)
        {
            var argType = GetExpressionSemanticType(call.Arguments[0]);
            var elemType = argType switch
            {
                GenericType gt when gt.TypeArguments.Count > 0 => gt.TypeArguments[0],
                _ when argType == SemanticType.Str => SemanticType.Str,
                _ => null
            };

            // Fallback: for user-defined types (e.g., with __reversed__), extract element type
            // from the call's resolved return type (Iterator<T> -> T).
            if (elemType == null || IsObjectType(elemType))
            {
                var callType = _context.SemanticInfo?.GetExpressionType(call);
                if (callType is GenericType callGeneric
                    && callGeneric.TypeArguments.Count > 0
                    && callGeneric.TypeArguments[0] is not UnknownType
                    && !IsObjectType(callGeneric.TypeArguments[0]))
                {
                    elemType = callGeneric.TypeArguments[0];
                }
            }

            // Fallback: try to infer element type from the argument's AST structure (#555).
            // Handles sorted(list(d.keys())) where d is a generic dict type.
            if ((elemType == null || IsObjectType(elemType)) && call.Arguments.Length > 0)
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

    private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
    {
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

                // Check if this is a string enum (string enums are generated as classes, not C# enums)
                if (IsStringEnumSymbol(enumSymbol))
                {
                    // String enums use CONSTANT_CASE field names (same as NameContext.Constant)
                    var fieldName = NameCasing.ResolveConstant(memberAccess.Member, isBacktickEscaped: memberAccess.IsMemberBacktickEscaped);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(fieldName));
                    // String enums: Color.RED already returns the string value from the static field
                    return enumMemberAccess;
                }
                else
                {
                    // CLR enums already have correct PascalCase member names — skip mangling
                    var enumMemberName = enumSymbol.ClrType != null
                        ? memberAccess.Member
                        : NameMangler.ToEnumMemberName(memberAccess.Member);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(enumMemberName));
                    return enumMemberAccess;
                }
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
        // - Other names use PascalCase
        var mangledMemberName = DunderMapping.ResolveCSharpName(memberAccess.Member)
            ?? (NameFormDetector.IsConstantCaseName(memberAccess.Member)
                ? NameMangler.ToConstantCase(memberAccess.Member)
                : NameCasing.ResolveField(memberAccess.Member, isBacktickEscaped: memberAccess.IsMemberBacktickEscaped));
        var member = IdentifierName(mangledMemberName);

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
            // obj.member
            result = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                member);
        }

        // If this member access path has been narrowed by isinstance(),
        // wrap with cast: ((Dog)this.Animal) so further member access works
        var dottedPath = TryBuildDottedPath(memberAccess);
        if (dottedPath != null && _narrowing.IsInstanceNarrowed(dottedPath))
        {
            var narrowedType = _narrowing.GetIsInstanceNarrowedType(dottedPath)!;
            result = ParenthesizedExpression(
                CastExpression(
                    ParseTypeName(narrowedType),
                    result));
        }

        // Optional<T>/Nullable<T> narrowing for member access paths (self.field is not None)
        if (dottedPath != null && _narrowing.IsNarrowed(dottedPath))
        {
            if (_narrowing.IsNullableNarrowed(dottedPath))
            {
                result = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, result, IdentifierName("Value"));
            }
            else if (_narrowing.IsReferenceNullableNarrowed(dottedPath))
            {
                // Reference-type nullable field → ! (null-forgiving operator)
                result = PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, result);
            }
            else
            {
                result = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression, result, IdentifierName(ProtocolConstants.Unwrap)))
                    .WithArgumentList(ArgumentList());
            }
        }

        return result;
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
            // For .NET namespace modules (e.g., system -> System), use the actual namespace name
            var moduleParts = modulePath.Take(modulePartCount);
            var aliasName = currentModule.NetNamespaceName != null
                ? currentModule.NetNamespaceName
                : EscapeCSharpKeyword(string.Join("_", moduleParts));

            // If the entire path is just the module (no member access), return the alias
            if (modulePartCount == modulePath.Count)
            {
                return IdentifierName(aliasName);
            }

            // Build member access: alias.Member1.Member2...
            ExpressionSyntax expr = IdentifierName(aliasName);
            for (int i = modulePartCount; i < modulePath.Count; i++)
            {
                var memberPart = modulePath[i];

                // For .NET module fields (e.g., string.digits), the CLR field name
                // may differ from PascalCase convention (Sharpy.Core uses Python-style
                // snake_case names for string module constants). Use the CLR name directly
                // when the export is a VariableSymbol.
                string mangledMemberName;
                currentModule.Exports.TryGetValue(memberPart, out var exportSymbol);
                if (currentModule.IsNetModule && exportSymbol is VariableSymbol)
                {
                    mangledMemberName = memberPart;
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
                    IdentifierName(mangledMemberName));
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
                IdentifierName(memberName));
        }

        return currentExpr;
    }

    private ExpressionSyntax GenerateIndexAccess(IndexAccess indexAccess)
    {
        // Tuple positional indexing: t[0] -> t.Item1, t[1] -> t.Item2, etc.
        // C# ValueTuples don't support [] indexing, so we emit .ItemN member access.
        if (GetExpressionSemanticType(indexAccess.Object) is Semantic.TupleType
            && TryGetConstantIntIndex(indexAccess.Index, out var tupleIndex))
        {
            var obj = GenerateExpression(indexAccess.Object);
            var itemName = $"Item{tupleIndex + 1}";
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(itemName));
        }

        var objExpr = GenerateExpression(indexAccess.Object);
        var index = GenerateExpression(indexAccess.Index);

        var objectType = GetExpressionSemanticType(indexAccess.Object);

        // String indexing: s[i] -> StringHelpers.GetItem(s, i) to return string, not char,
        // and to support negative indexing
        if (objectType == SemanticType.Str)
        {
            return InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                    IdentifierName("GetItem")))
                .AddArgumentListArguments(
                    Argument(objExpr),
                    Argument(index));
        }

        // Array indexing: arr[i] -> ArrayHelpers.GetItem(arr, i) to support negative indexing
        if (objectType is Semantic.GenericType { Name: BuiltinNames.Array })
        {
            return InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                    IdentifierName("GetItem")))
                .AddArgumentListArguments(
                    Argument(objExpr),
                    Argument(index));
        }

        var elementAccess = ElementAccessExpression(objExpr)
            .AddArgumentListArguments(Argument(index));

        return elementAccess;
    }

    /// <summary>
    /// Tries to extract a constant integer value from an expression.
    /// Delegates to <see cref="AstHelper.TryGetConstantIntIndex"/>.
    /// </summary>
    private static bool TryGetConstantIntIndex(Expression expr, out int value)
        => AstHelper.TryGetConstantIntIndex(expr, out value);

    private ExpressionSyntax GenerateSliceAccess(SliceAccess sliceAccess)
    {
        // arr[start:stop:step]
        // Translates to: global::Sharpy.Slice.GetSlice(obj, start, stop, step)
        // where omitted bounds pass null (matching the nullable int? parameters)
        var obj = GenerateExpression(sliceAccess.Object);
        var start = sliceAccess.Start != null
            ? GenerateExpression(sliceAccess.Start)
            : (ExpressionSyntax)LiteralExpression(SyntaxKind.NullLiteralExpression);
        var stop = sliceAccess.Stop != null
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
                Argument(stop),
                Argument(step));
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
        }

        return null;
    }

    private NameSyntax BuildNestedTypeName(TypeSymbol nestedSym)
    {
        var parts = new List<string>();
        var current = nestedSym;
        while (current != null)
        {
            parts.Add(NameMangler.Transform(current.Name, NameContext.Type));
            current = current.DeclaringType;
        }
        parts.Reverse();

        NameSyntax result = IdentifierName(parts[0]);
        for (int i = 1; i < parts.Count; i++)
        {
            result = QualifiedName(result, IdentifierName(parts[i]));
        }
        return result;
    }
}
