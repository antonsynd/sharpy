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
/// RoslynEmitter partial class: Function calls, member access, index/slice access, lambdas,
/// module paths, tagged union constructors, single-evaluation helpers
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateCall(FunctionCall call)
    {
        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericName)
        {
            var result = GenerateGenericInstantiation(indexAccess, genericName, call);
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

                // Use explicit AliasQualifiedName to handle all expression contexts (f-strings, etc.)
                var builtinName = MakeGlobalQualifiedName("Sharpy", "Builtins", NameMangler.ToPascalCase(funcName.Name));
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
                        .Select(t => _typeMapper.MapSemanticType(t));
                    var csharpCollectionName = CSharpTypeNames.FromSharpyName(funcName.Name)
                        ?? NameMangler.ToPascalCase(funcName.Name);
                    var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpCollectionName,
                            typeArgsSyntax.ToArray());
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
                        ?? NameMangler.ToPascalCase(funcName.Name);
                    var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpName,
                            typeArgsSyntax.ToArray());
                    return ObjectCreationExpression(genericTypeSyntax)
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                // For builtin collection types, use the fully-qualified Sharpy.X name
                var collectionName = CSharpTypeNames.FromSharpyName(funcName.Name);
                if (collectionName != null)
                {
                    return ObjectCreationExpression(ParseName(collectionName))
                        .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
                }

                return ObjectCreationExpression(ParseName(name))
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Regular function call — check if this is a local variable/parameter (callable)
            // before falling back to PascalCase for module-level functions
            var codeGenInfo = symbol != null ? GetCodeGenInfo(symbol) : null;
            string funcCSharpName;
            if (codeGenInfo?.CSharpName != null)
            {
                funcCSharpName = codeGenInfo.CSharpName;
            }
            else if (_variableVersions.ContainsKey(_nameResolutionService.GetBaseName(funcName.Name)))
            {
                // Parameter or local variable with callable type — use camelCase resolution
                funcCSharpName = GetMangledVariableName(funcName.Name, isNewDeclaration: false);
            }
            else
            {
                funcCSharpName = NameMangler.ToPascalCase(funcName.Name);
            }
            return InvocationExpression(ParseName(funcCSharpName))
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
                    var caseCSharpName = NameMangler.Transform(memberAccess.Member, NameContext.Type);

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

                    // TODO(#254): GetCallTarget always returns null — reordering is a no-op here.
                    var caseCallTarget = _context.SemanticInfo?.GetCallTarget(call);
                    var caseAllArgs = GenerateReorderedCallArguments(call, caseCallTarget);

                    return ObjectCreationExpression(qualifiedCaseName)
                        .WithArgumentList(ArgumentList(SeparatedList(caseAllArgs)));
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
            var methodName = DunderMapping.ResolveCSharpName(memberAccess.Member)
                ?? NameMangler.GetListMethodMapping(memberAccess.Member)
                ?? NameMangler.ToPascalCase(memberAccess.Member);

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
                // TODO(#231): Type not fully resolved (e.g., bare lambda without type annotations in IIFE).
                // C# requires explicit delegate type for lambda invocation — this will produce
                // a CS0149 error. Requires semantic analysis to infer lambda types from call-site args.
                callTarget = ParenthesizedExpression(innerExprForCheck);
            }
        }

        // TODO(#254): GetCallTarget always returns null — reordering is a no-op here.
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
                ?? NameMangler.ToPascalCase(genericName.Name);
            var genericTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(csharpGenericTypeName, typeArgsSyntax);

            // Generate arguments (reorder for C# compliance if needed)
            var genericTypeCallTarget = ResolveConstructorForCall(genericTypeSymbol, call);
            var allArgs = GenerateReorderedCallArguments(call, genericTypeCallTarget);

            return ObjectCreationExpression(genericTypeSyntax)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
        {
            // Generate: GenericFunction<TypeArgs>(args)
            var genericFuncSyntax = GenericName(NameMangler.ToPascalCase(genericName.Name))
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
        var csharpName = NameMangler.ToPascalCase(name);

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
            if (elemType == null)
            {
                var callType = _context.SemanticInfo?.GetExpressionType(call);
                if (callType is GenericType callGeneric
                    && callGeneric.TypeArguments.Count > 0)
                {
                    elemType = callGeneric.TypeArguments[0];
                }
            }

            if (elemType != null)
                typeArg = _typeMapper.MapSemanticType(elemType);
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
                    var fieldName = NameMangler.Transform(memberAccess.Member, NameContext.Constant);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(fieldName));
                    // String enums: Color.RED already returns the string value from the static field
                    return enumMemberAccess;
                }
                else
                {
                    // Integer enums use PascalCase member names
                    var enumMemberName = NameMangler.ToEnumMemberName(memberAccess.Member);
                    var enumMemberAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        enumType,
                        IdentifierName(enumMemberName));
                    // Return the enum member directly (not cast to int)
                    // The .value property is used to get the underlying int value
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

            // enum_instance.name -> (Sharpy.Str)enum_instance.ToString()
            return CastExpression(
                ParseTypeName("Sharpy.Str"),
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        obj,
                        IdentifierName("ToString"))));
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
                : NameMangler.ToPascalCase(memberAccess.Member));
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

        // Optional<T> narrowing for member access paths (self.field is not None)
        if (dottedPath != null && _narrowing.IsNarrowed(dottedPath))
        {
            if (_narrowing.IsNullableNarrowed(dottedPath))
            {
                result = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, result, IdentifierName("Value"));
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
                if (currentModule.IsNetModule
                    && currentModule.Exports.TryGetValue(memberPart, out var exportSymbol)
                    && exportSymbol is VariableSymbol)
                {
                    mangledMemberName = memberPart;
                }
                else if (NameFormDetector.IsConstantCaseName(memberPart))
                {
                    mangledMemberName = NameMangler.ToConstantCase(memberPart);
                }
                else
                {
                    mangledMemberName = NameMangler.ToPascalCase(memberPart);
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
        ExpressionSyntax currentExpr = IdentifierName(NameMangler.ToPascalCase(modulePath[0]));

        // Chain the rest of the path
        for (int i = 1; i < modulePath.Count; i++)
        {
            // Use CONSTANT_CASE for ALL_CAPS names (Python-style constants)
            var memberPart = modulePath[i];
            var memberName = NameFormDetector.IsConstantCaseName(memberPart)
                ? NameMangler.ToConstantCase(memberPart)
                : NameMangler.ToPascalCase(memberPart);
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
            ? (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(sliceAccess.Start))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var stop = sliceAccess.Stop != null
            ? (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(sliceAccess.Stop))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var step = sliceAccess.Step != null
            ? (ExpressionSyntax)CastExpression(
                NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                GenerateExpression(sliceAccess.Step))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

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

    private ExpressionSyntax GenerateLambdaExpression(LambdaExpression lambda)
    {
        // If the lambda has default parameters and appears in an expression context
        // (not a direct variable assignment — that case is handled by GenerateVariableDeclaration),
        // hoist a local function and return the function name as an identifier.
        // This allows the lambda to be passed as a delegate while preserving default values
        // for direct calls via the hoisted local function.
        if (HasDefaultParameters(lambda))
        {
            var tempName = $"__lambda_{_tempVarCounter++}";
            _hoistedStatements.Add(GenerateLambdaAsLocalFunction(lambda, tempName));
            return IdentifierName(tempName);
        }

        // lambda x, y: x + y → (x, y) => x + y
        var parameters = lambda.Parameters
            .Select(p => Parameter(Identifier(NameMangler.ToCamelCase(p.Name))))
            .ToArray();

        var body = GenerateExpression(lambda.Body);

        if (parameters.Length == 0)
        {
            return ParenthesizedLambdaExpression()
                .WithExpressionBody(body);
        }
        else if (parameters.Length == 1)
        {
            return SimpleLambdaExpression(parameters[0])
                .WithExpressionBody(body);
        }
        else
        {
            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithExpressionBody(body);
        }
    }

    /// <summary>
    /// Generates a C# local function statement from a lambda expression.
    /// Used when a lambda has default parameter values, since C# delegates / Func&lt;&gt; don't
    /// support optional parameters but local functions do.
    /// </summary>
    private LocalFunctionStatementSyntax GenerateLambdaAsLocalFunction(
        LambdaExpression lambda, string functionName)
    {
        // Get the semantic type of the lambda (FunctionType) for parameter and return types
        var lambdaType = GetExpressionSemanticType(lambda) as Semantic.FunctionType;

        // Generate parameters with types and defaults
        var parameters = new List<ParameterSyntax>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            var paramName = NameMangler.ToCamelCase(param.Name);

            // Resolve type from semantic info first, fall back to annotation
            TypeSyntax paramType;
            if (lambdaType != null && i < lambdaType.ParameterTypes.Count
                && lambdaType.ParameterTypes[i] is not UnknownType)
            {
                paramType = _typeMapper.MapSemanticType(lambdaType.ParameterTypes[i]);
            }
            else if (param.Type != null)
            {
                paramType = _typeMapper.MapType(param.Type);
            }
            else
            {
                paramType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
            }

            var paramSyntax = Parameter(Identifier(paramName)).WithType(paramType);

            // Handle default value
            if (param.DefaultValue != null)
            {
                // Sharpy.Str default: use 'default' sentinel (see GenerateParameter)
                if (IsStrTypedParameter(param) && param.DefaultValue is StringLiteral)
                {
                    var actualDefault = GenerateExpression(param.DefaultValue);
                    _pendingStrDefaults.Add((paramName, actualDefault));
                    paramSyntax = paramSyntax.WithDefault(
                        EqualsValueClause(LiteralExpression(SyntaxKind.DefaultLiteralExpression)));
                }
                else
                {
                    paramSyntax = paramSyntax.WithDefault(
                        EqualsValueClause(GenerateExpression(param.DefaultValue)));
                }
            }

            parameters.Add(paramSyntax);
        }

        // Get return type from semantic info
        TypeSyntax returnType;
        if (lambdaType != null && lambdaType.ReturnType is not UnknownType)
        {
            returnType = _typeMapper.MapSemanticType(lambdaType.ReturnType);
        }
        else
        {
            returnType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Generate body expression
        var body = GenerateExpression(lambda.Body);

        var localFunc = LocalFunctionStatement(returnType, Identifier(functionName))
            .WithParameterList(ParameterList(SeparatedList(parameters)));

        // If Str defaults need preamble statements, use block body instead of expression body
        var strDefaults = DrainPendingStrDefaults();
        if (strDefaults.Count > 0)
        {
            strDefaults.Add(ReturnStatement(body));
            localFunc = localFunc.WithBody(Block(strDefaults));
        }
        else
        {
            localFunc = localFunc
                .WithExpressionBody(ArrowExpressionClause(body))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return localFunc;
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type, handling cross-file references.
    /// Types are nested inside the module class, so cross-file references use
    /// Namespace.ModuleClass.TypeName.
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
    {
        // For Sharpy.Core CLR types (with [SharpyModuleType] attribute), use the actual CLR
        // type name rather than deriving from DefiningModule, since the CLR namespace (Sharpy)
        // differs from the module name (e.g., argparse -> Sharpy.ArgumentParser,
        // not Argparse.ArgumentParser)
        if (typeSymbol.ClrType != null && typeSymbol.ClrType.Namespace == "Sharpy")
        {
            return $"global::{typeSymbol.ClrType.FullName}";
        }

        // Check if type is from a different file (cross-file reference)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath) &&
            !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            var moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Check if type is from an external module (imported via DefiningModule)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            var moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
            var typeName = NameMangler.ToPascalCase(sharpyTypeName);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Type is in current file - use simple name
        return NameMangler.ToPascalCase(sharpyTypeName);
    }

    /// <summary>
    /// Builds a fully qualified type name, handling collision cases where the type IS
    /// the module class (e.g., animal.spy defining class Animal).
    /// </summary>
    private string BuildQualifiedTypeName(string moduleNamespace, string typeName)
    {
        // Check for collision: when the module name matches the type name,
        // the type IS the module class, not nested inside it.
        var lastSegment = moduleNamespace.Contains('.', StringComparison.Ordinal)
            ? moduleNamespace.Split('.').Last()
            : moduleNamespace;

        if (string.Equals(lastSegment, typeName, StringComparison.Ordinal))
        {
            // Type IS the module class — module path is the type path
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                return $"{_context.ProjectNamespace}.{moduleNamespace}";
            }
            return moduleNamespace;
        }

        // Type is nested inside the module class
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return $"{_context.ProjectNamespace}.{moduleNamespace}.{typeName}";
        }
        return $"{moduleNamespace}.{typeName}";
    }

    /// <summary>
    /// Derives a module namespace from a file path, computing the full package path
    /// relative to the project root.
    /// E.g., for project root "/temp" and file "/temp/mypackage/submodule.spy" -> "Mypackage.Submodule"
    /// </summary>
    private string GetModuleNameFromFilePath(string filePath)
    {
        // If we have a project root, compute relative path for proper namespace
        if (!string.IsNullOrEmpty(_context.ProjectRootPath))
        {
            var relativePath = Path.GetRelativePath(_context.ProjectRootPath, filePath);
            var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            var namespaceParts = new List<string>();

            // Add directory parts (package hierarchy)
            if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
            {
                var dirParts = relativeDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in dirParts)
                {
                    if (!string.IsNullOrEmpty(part) && part != ".")
                    {
                        namespaceParts.Add(NameMangler.ToPascalCase(part));
                    }
                }
            }

            // Add file name part (skip __init__ as it represents the package itself)
            if (!string.Equals(fileName, DunderNames.Init, StringComparison.OrdinalIgnoreCase))
            {
                namespaceParts.Add(NameMangler.ToPascalCase(fileName));
            }

            if (namespaceParts.Count > 0)
            {
                return string.Join(".", namespaceParts);
            }
        }

        // Fallback: just use file name
        var fallbackFileName = Path.GetFileNameWithoutExtension(filePath);
        return NameMangler.ToPascalCase(fallbackFileName);
    }

    /// <summary>
    /// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
    /// </summary>
    private static string ConvertModuleToNamespace(string modulePath)
    {
        var parts = modulePath.Split('.');
        return string.Join(".", parts.Select(p => NameMangler.ToPascalCase(p)));
    }

    // ============================================================
    // Helper: Single-evaluation capture for complex expressions
    // ============================================================

    /// <summary>
    /// Returns true if the AST expression is side-effect-free (safe to evaluate multiple times).
    /// Simple identifiers, self, and literals are safe; everything else may have side effects.
    /// </summary>
    private static bool IsSideEffectFree(Expression expr)
        => expr is Parser.Ast.Identifier or NoneLiteral or BooleanLiteral or IntegerLiteral
                 or FloatLiteral or StringLiteral or SuperExpression;

    /// <summary>
    /// Ensures an expression is only evaluated once. For simple identifiers, returns the
    /// expression as-is. For complex expressions, captures the value using an inline
    /// <c>is var</c> pattern: <c>expr is var __temp &amp;&amp; __temp.Check ? __temp.Access : default</c>.
    /// Returns the safe-to-reuse expression and an optional capture condition to prepend.
    /// </summary>
    private (ExpressionSyntax SafeExpr, ExpressionSyntax? CaptureCondition) EnsureSingleEvaluation(
        ExpressionSyntax generated, Expression astExpr)
    {
        if (IsSideEffectFree(astExpr))
            return (generated, null);

        var tempName = GenerateTempVarName("opt");
        var tempIdent = IdentifierName(tempName);

        // Parenthesize conditional expressions (ternaries) so that the `is var`
        // pattern captures the entire expression, not just the false branch.
        // Without this, `a ? b : c is var t` parses as `a ? b : (c is var t)`.
        var captureTarget = generated is ConditionalExpressionSyntax
            ? ParenthesizedExpression(generated)
            : generated;
        var capture = IsPatternExpression(
            captureTarget,
            VarPattern(SingleVariableDesignation(Identifier(tempName))));
        return (tempIdent, capture);
    }

    // ============================================================
    // Tagged Union Constructor Generation (Some/Ok/Err)
    // ============================================================

    /// <summary>
    /// Generates code for a tagged union constructor call (Some, Ok, Err).
    /// Some(v) generates Optional&lt;T&gt;.Some(v).
    /// Ok(v)/Err(e) generate Result&lt;T,E&gt;.Ok(v)/Err(e).
    /// </summary>
    private ExpressionSyntax GenerateTaggedUnionConstructor(FunctionCall call)
    {
        var id = (Identifier)call.Function;
        var exprType = GetExpressionSemanticType(call)!;

        return (id.Name, exprType) switch
        {
            ("Some", OptionalType opt) => GenerateSomeExpression(call, opt),
            ("Ok", ResultType res) => GenerateOkExpression(call, res),
            ("Err", ResultType res) => GenerateErrExpression(call, res),
            _ => throw new InvalidOperationException($"Unexpected tagged union constructor: {id.Name}")
        };
    }

    /// <summary>
    /// Generates: Optional&lt;T&gt;.None (static property access)
    /// </summary>
    private ExpressionSyntax GenerateOptionalNone(OptionalType opt)
    {
        var underlyingType = _typeMapper.MapSemanticType(opt.UnderlyingType);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
            IdentifierName("None"));
    }

    /// <summary>
    /// Wraps an already-generated expression in Optional&lt;T&gt;.Some(value).
    /// Used for null-conditional ternary true branches where C# cannot reconcile
    /// the unwrapped result type with Optional&lt;T&gt;.None on the false branch.
    /// </summary>
    private ExpressionSyntax WrapInOptionalSome(ExpressionSyntax value, OptionalType optType)
    {
        var underlyingType = _typeMapper.MapSemanticType(optType.UnderlyingType);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Optional")
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
                IdentifierName("Some")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));
    }

    /// <summary>
    /// Generates: Optional&lt;T&gt;.Some(value)
    /// </summary>
    private ExpressionSyntax GenerateSomeExpression(FunctionCall call, OptionalType opt)
        => WrapInOptionalSome(GenerateExpression(call.Arguments[0]), opt);

    /// <summary>
    /// Generates: Result&lt;T, E&gt;.Ok(value)
    /// </summary>
    private ExpressionSyntax GenerateOkExpression(FunctionCall call, ResultType res)
    {
        var okType = _typeMapper.MapSemanticType(res.OkType);
        var errType = _typeMapper.MapSemanticType(res.ErrorType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Result")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
                IdentifierName("Ok")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Generates: Result&lt;T, E&gt;.Err(error)
    /// </summary>
    private ExpressionSyntax GenerateErrExpression(FunctionCall call, ResultType res)
    {
        var okType = _typeMapper.MapSemanticType(res.OkType);
        var errType = _typeMapper.MapSemanticType(res.ErrorType);
        var arg = GenerateExpression(call.Arguments[0]);

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Result")
                    .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
                IdentifierName("Err")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
    }

    /// <summary>
    /// Checks whether a method call on an object expression targets a default interface method
    /// that the concrete class doesn't override. Returns the mangled C# interface name if so,
    /// or null if the method is defined directly on the class.
    /// </summary>
    private string? TryGetDefaultMethodInterface(Parser.Ast.Expression objectExpr, string methodName)
    {
        var objType = GetExpressionSemanticType(objectExpr);
        if (objType is not UserDefinedType udt || udt.Symbol == null)
            return null;

        var typeSymbol = udt.Symbol;

        // Only applies to class types (not interfaces, enums, structs)
        if (typeSymbol.TypeKind != Semantic.TypeKind.Class)
            return null;

        // Check if the class itself defines this method (including inherited concrete methods)
        if (HasMethodDefined(typeSymbol, methodName))
            return null;

        // Search interfaces for a default method with this name
        foreach (var ifaceRef in typeSymbol.Interfaces)
        {
            if (HasDefaultMethod(ifaceRef.Definition, methodName))
                return NameMangler.Transform(ifaceRef.Definition.Name, NameContext.Interface);
        }

        return null;
    }

    /// <summary>
    /// Maps primitive type static method calls to their Sharpy.Core helper class methods.
    /// Returns the fully qualified C# method name, or null if not a known primitive static call.
    /// </summary>
    private static string? GetPrimitiveStaticCallTarget(string typeName, string methodName)
    {
        return (typeName, methodName) switch
        {
            ("int", "parse") => "global::Sharpy.IntParse.Parse",
            ("float", "parse") => "global::Sharpy.DoubleParse.Parse",
            _ => null
        };
    }

    /// <summary>
    /// Checks whether a type (or its base classes) defines a method with the given name.
    /// Does not search interfaces — only the class hierarchy.
    /// </summary>
    private static bool HasMethodDefined(TypeSymbol typeSymbol, string methodName)
    {
        var (method, _) = TypeHierarchyService.FindMember<FunctionSymbol>(
            typeSymbol, methodName, t => t.Methods, searchInterfaces: false);
        return method != null;
    }

    /// <summary>
    /// Checks whether an interface has a default (non-abstract) method with the given name.
    /// Uses symbol metadata (IsAbstract) rather than inspecting AST body shape.
    /// </summary>
    private static bool HasDefaultMethod(TypeSymbol interfaceSymbol, string methodName)
    {
        return interfaceSymbol.Methods.Any(m => m.Name == methodName && !m.IsAbstract);
    }

    /// <summary>
    /// Emits C# code for asyncio module function calls.
    /// asyncio.gather(t1, t2, ...) → Task.WhenAll(t1, t2, ...)
    /// asyncio.gather(*tasks)      → Task.WhenAll(tasks)
    /// asyncio.sleep(n)            → Task.Delay(TimeSpan.FromSeconds(n))
    /// </summary>
    private ExpressionSyntax GenerateAsyncioCall(string functionName, FunctionCall call)
    {
        // global::System.Threading.Tasks.Task
        var taskTypeName = MakeGlobalQualifiedName("System", "Threading", "Tasks", "Task");

        if (functionName == BuiltinFunctionNames.Gather)
        {
            // Task.WhenAll(...)
            var whenAllAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                taskTypeName,
                IdentifierName("WhenAll"));

            // Handle spread arguments: asyncio.gather(*tasks) → Task.WhenAll(tasks)
            // Handle individual arguments: asyncio.gather(t1, t2) → Task.WhenAll(t1, t2)
            var args = GeneratePositionalArguments(call.Arguments).ToArray();

            return InvocationExpression(whenAllAccess)
                .WithArgumentList(ArgumentList(SeparatedList(args)));
        }

        if (functionName == BuiltinFunctionNames.Sleep)
        {
            // Task.Delay(TimeSpan.FromSeconds(seconds))
            var delayAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                taskTypeName,
                IdentifierName("Delay"));

            // Build TimeSpan.FromSeconds(seconds)
            var timeSpanTypeName = MakeGlobalQualifiedName("System", "TimeSpan");
            var fromSecondsAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                timeSpanTypeName,
                IdentifierName("FromSeconds"));

            var secondsArg = call.Arguments.Length > 0
                ? GenerateExpression(call.Arguments[0])
                : LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0.0));

            var timeSpanExpr = InvocationExpression(fromSecondsAccess)
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(secondsArg))));

            return InvocationExpression(delayAccess)
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(timeSpanExpr))));
        }

        // Unknown asyncio function — fall through to regular member access emission
        return EmitNotImplementedExpression(
            $"asyncio.{functionName} is not supported",
            DiagnosticCodes.CodeGen.UnsupportedFeature, call.LineStart, call.ColumnStart);
    }

    // ============================================================
    // Call-site argument reordering helpers
    //
    // When C# parameter order differs from Sharpy declaration order
    // (due to ReorderParametersForCSharp in Phase 2), positional
    // arguments at call sites can misalign. These helpers detect
    // when reordering was applied and emit named arguments so C#
    // binds by name instead of by position.
    // ============================================================

    /// <summary>
    /// Resolves the constructor FunctionSymbol for a type instantiation call.
    /// Matches by argument count (positional + keyword) to handle overloads.
    /// Returns null if no matching constructor is found.
    /// </summary>
    private static FunctionSymbol? ResolveConstructorForCall(TypeSymbol typeSymbol, FunctionCall call)
    {
        var totalArgs = call.Arguments.Length + call.KeywordArguments.Length;
        foreach (var ctor in typeSymbol.Constructors)
        {
            var nonSelfParams = ctor.Parameters
                .Where(p => p.Name != PythonNames.Self && p.Name != PythonNames.Cls)
                .ToList();

            // Check if argument count is in the valid range:
            // - For variadic ctors: totalArgs >= requiredCount (no upper bound)
            // - For non-variadic ctors: requiredCount <= totalArgs <= totalParamCount
            var requiredCount = nonSelfParams.Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = nonSelfParams.Any(p => p.IsVariadic);
            if (hasVariadic)
            {
                if (totalArgs >= requiredCount)
                    return ctor;
            }
            else
            {
                var totalParamCount = nonSelfParams.Count;
                if (totalArgs >= requiredCount && totalArgs <= totalParamCount)
                    return ctor;
            }
        }
        return typeSymbol.Constructors.Count == 1 ? typeSymbol.Constructors[0] : null;
    }

    /// <summary>
    /// Resolves the FunctionSymbol for a method call on an object.
    /// Uses the receiver's semantic type to look up the method by name.
    /// Returns null if the method cannot be resolved.
    /// </summary>
    private FunctionSymbol? ResolveMethodForCall(Expression receiver, string methodName)
    {
        var receiverType = GetExpressionSemanticType(receiver);
        TypeSymbol? typeSymbol = receiverType switch
        {
            UserDefinedType udt => udt.Symbol as TypeSymbol,
            GenericType gt => _context.LookupSymbol(gt.Name) as TypeSymbol,
            _ => null
        };

        if (typeSymbol == null)
            return null;

        // Search Methods in the type hierarchy (including interfaces)
        var (method, _) = TypeHierarchyService.FindMethod(typeSymbol, methodName);
        if (method != null)
            return method;

        // Fall back to MethodOverloads (walk base class chain for overloaded methods)
        if (typeSymbol.MethodOverloads.TryGetValue(methodName, out var overloads) && overloads.Count > 0)
            return overloads[0];
        foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(typeSymbol))
        {
            if (baseType.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads[0];
        }

        return null;
    }

    /// <summary>
    /// Returns true if the function's C# signature has been reordered relative
    /// to its Sharpy declaration order (i.e., keyword-only or variadic params
    /// required ReorderParametersForCSharp to intervene).
    /// </summary>
    private static bool NeedsParameterReordering(FunctionSymbol? funcSymbol)
    {
        if (funcSymbol == null)
            return false;

        var parameters = funcSymbol.Parameters;
        bool hasVariadic = false;
        bool hasKeywordOnly = false;
        foreach (var p in parameters)
        {
            if (p.Name == PythonNames.Self || p.Name == PythonNames.Cls)
                continue;
            if (p.IsVariadic)
                hasVariadic = true;
            if (p.IsKeywordOnly)
                hasKeywordOnly = true;
        }

        // No variadic and no keyword-only → no reordering was applied
        if (!hasVariadic && !hasKeywordOnly)
            return false;

        return true;
    }

    /// <summary>
    /// Generates call arguments in the correct order for a potentially-reordered C# signature.
    /// Forwards to <see cref="GenerateReorderedCallArgumentsCore"/> with no prepended argument.
    /// </summary>
    private ArgumentSyntax[] GenerateReorderedCallArguments(FunctionCall call, FunctionSymbol? funcSymbol)
        => GenerateReorderedCallArgumentsCore(call, funcSymbol, prependedArgument: null);

    /// <summary>
    /// Generates call arguments with a pre-built argument prepended (used by pipe forward operator).
    /// The <paramref name="prependedArgument"/> is mapped to the first non-self/cls parameter and
    /// emitted as a named argument when reordering is needed.
    /// </summary>
    private ArgumentSyntax[] GenerateReorderedCallArguments(
        FunctionCall call, FunctionSymbol? funcSymbol, ArgumentSyntax prependedArgument)
        => GenerateReorderedCallArgumentsCore(call, funcSymbol, prependedArgument);

    /// <summary>
    /// Core implementation for call-site argument reordering.
    /// When reordering is needed, all non-variadic arguments are emitted as named arguments
    /// so C# binds by name regardless of parameter position. Variadic arguments remain
    /// positional (trailing). When not needed, falls back to positional + keyword concat.
    /// If <paramref name="prependedArgument"/> is non-null it is inserted before call arguments
    /// (pipe forward scenario: <c>x |> f(y)</c> → <c>f(x, y)</c>).
    /// </summary>
    private ArgumentSyntax[] GenerateReorderedCallArgumentsCore(
        FunctionCall call, FunctionSymbol? funcSymbol, ArgumentSyntax? prependedArgument)
    {
        if (!NeedsParameterReordering(funcSymbol))
        {
            // No reordering — use the existing positional + keyword pattern
            var positionalArgs = GeneratePositionalArguments(call.Arguments);
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));
            if (prependedArgument != null)
                return new[] { prependedArgument }.Concat(positionalArgs).Concat(keywordArgs).ToArray();
            return positionalArgs.Concat(keywordArgs).ToArray();
        }

        // Build the non-self/cls parameter list in Sharpy declaration order
        var paramList = funcSymbol!.Parameters
            .Where(p => p.Name != PythonNames.Self && p.Name != PythonNames.Cls)
            .ToList();

        // Phase 1: Match call arguments to parameters by name.
        // Positional args match non-keyword-only params in Sharpy declaration order.
        // Keyword args match by name. Remaining positional args go to variadic.
        var argByParam = new Dictionary<string, ArgumentSyntax>();
        var keywordArgsByName = call.KeywordArguments
            .ToDictionary(k => k.Name, k => k);

        int positionalIndex = 0;
        int paramStartIndex = 0;

        // If there's a prepended argument, assign it to the first parameter
        if (prependedArgument != null && paramList.Count > 0)
        {
            var firstParam = paramList[0];
            string csharpName = NameMangler.ToCamelCase(firstParam.Name);
            argByParam[firstParam.Name] = prependedArgument
                .WithNameColon(NameColon(IdentifierName(csharpName)));
            paramStartIndex = 1;
        }

        for (int pi = paramStartIndex; pi < paramList.Count; pi++)
        {
            var param = paramList[pi];
            if (param.IsVariadic)
                continue;

            string csharpParamName = NameMangler.ToCamelCase(param.Name);

            if (keywordArgsByName.TryGetValue(param.Name, out var kwarg))
            {
                argByParam[param.Name] = Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(csharpParamName)));
                keywordArgsByName.Remove(param.Name);
            }
            else if (!param.IsKeywordOnly && positionalIndex < call.Arguments.Length)
            {
                var argExpr = call.Arguments[positionalIndex];
                if (argExpr is SpreadElement)
                {
                    // Spread elements can't be named — fall back to positional for safety
                    var result = new List<ArgumentSyntax>();
                    if (prependedArgument != null)
                        result.Add(prependedArgument);
                    foreach (var spreadArg in GeneratePositionalArguments(call.Arguments))
                        result.Add(spreadArg);
                    foreach (var remaining in keywordArgsByName.Values)
                    {
                        result.Add(Argument(GenerateExpression(remaining.Value))
                            .WithNameColon(NameColon(IdentifierName(
                                NameMangler.ToCamelCase(remaining.Name)))));
                    }
                    return result.ToArray();
                }
                argByParam[param.Name] = Argument(GenerateExpression(argExpr))
                    .WithNameColon(NameColon(IdentifierName(csharpParamName)));
                positionalIndex++;
            }
            // else: parameter has a default value and was not provided — skip
        }

        // Phase 2: Emit named args in C# reordered parameter order.
        // This ensures named args are in-position, which is required when
        // followed by unnamed variadic trailing args (CS8323).
        var reorderedParams = ReorderParameterSymbolsForCSharp(paramList);
        var orderedResult = new List<ArgumentSyntax>();

        foreach (var param in reorderedParams)
        {
            if (param.IsVariadic)
                continue;
            if (argByParam.TryGetValue(param.Name, out var arg))
                orderedResult.Add(arg);
        }

        // Add any remaining keyword args not matched to declared params
        foreach (var remaining in keywordArgsByName.Values)
        {
            orderedResult.Add(Argument(GenerateExpression(remaining.Value))
                .WithNameColon(NameColon(IdentifierName(
                    NameMangler.ToCamelCase(remaining.Name)))));
        }

        // Phase 3: Variadic trailing args (remaining positional, unnamed)
        while (positionalIndex < call.Arguments.Length)
        {
            var argExpr = call.Arguments[positionalIndex];
            if (argExpr is SpreadElement)
            {
                foreach (var spreadArg in GeneratePositionalArguments(
                    System.Collections.Immutable.ImmutableArray.Create(argExpr)))
                {
                    orderedResult.Add(spreadArg);
                }
            }
            else
            {
                orderedResult.Add(Argument(GenerateExpression(argExpr)));
            }
            positionalIndex++;
        }

        return orderedResult.ToArray();
    }

    /// <summary>
    /// Generates positional arguments for a function call, handling SpreadElement arguments.
    /// For spread of a tuple type → expands to .Item1, .Item2, ... individual arguments.
    /// For spread of an iterable type → generates .ToArray() and passes as a single argument.
    /// </summary>
    private IEnumerable<ArgumentSyntax> GeneratePositionalArguments(
        System.Collections.Immutable.ImmutableArray<Expression> arguments)
    {
        foreach (var arg in arguments)
        {
            if (arg is SpreadElement spread)
            {
                var spreadType = GetExpressionSemanticType(spread.Value);
                var spreadExpr = GenerateExpression(spread.Value);

                if (spreadType is Semantic.TupleType tupleType)
                {
                    // Tuple spread: expand to individual .ItemN arguments
                    // f(*(a, b, c)) → f(tuple.Item1, tuple.Item2, tuple.Item3)
                    // Use a temp var to avoid evaluating spread.Value multiple times
                    var tempName = GenerateTempVarName("spread");
                    _hoistedStatements.Add(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(Identifier(tempName))
                                    .WithInitializer(EqualsValueClause(spreadExpr))))));

                    for (int i = 0; i < tupleType.ElementTypes.Count; i++)
                    {
                        yield return Argument(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(tempName),
                                IdentifierName($"Item{i + 1}")));
                    }
                }
                else
                {
                    // Iterable spread: call .ToArray() and pass as single argument
                    // f(*items) → f(items.ToArray())
                    // This works for params T[] parameters
                    yield return Argument(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                spreadExpr,
                                IdentifierName("ToArray")))
                            .WithArgumentList(ArgumentList()));
                }
            }
            else
            {
                yield return Argument(GenerateExpression(arg));
            }
        }
    }

    /// <summary>
    /// Builds a qualified type access expression from a TypeSymbol.
    /// Handles three cases: cross-module FQN (dot-separated), same-file inside
    /// a class (module class qualification), and top-level (bare identifier).
    /// </summary>
    private ExpressionSyntax BuildQualifiedTypeAccess(
        Semantic.TypeSymbol typeSymbol, string originalName)
    {
        var csharpTypeName = NameMangler.ToPascalCase(originalName);
        var fqn = GetFullyQualifiedTypeName(typeSymbol, originalName);

        if (fqn.Contains('.', StringComparison.Ordinal))
        {
            var parts = fqn.Split('.');
            return parts.Skip(1).Aggregate(
                (ExpressionSyntax)IdentifierName(parts[0]),
                (left, part) => MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, left, IdentifierName(part)));
        }

        if (_currentTypeSymbol != null)
        {
            var moduleClassName = GetModuleClassName();
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(moduleClassName),
                IdentifierName(csharpTypeName));
        }

        return IdentifierName(csharpTypeName);
    }

    /// <summary>
    /// Generates a static/const field access expression: TypeName.FieldName.
    /// Handles cross-module FQN, same-file module class qualification, and simple name.
    /// </summary>
    private ExpressionSyntax GenerateStaticFieldAccess(
        Semantic.TypeSymbol classSymbol, string originalName,
        Semantic.VariableSymbol fieldSymbol, string memberName)
    {
        ExpressionSyntax typeExpr = BuildQualifiedTypeAccess(classSymbol, originalName);

        var codeGenInfo = GetCodeGenInfo(fieldSymbol);
        var fieldName = codeGenInfo?.CSharpName ?? NameMangler.ToPascalCase(memberName);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            typeExpr,
            IdentifierName(fieldName));
    }
}
