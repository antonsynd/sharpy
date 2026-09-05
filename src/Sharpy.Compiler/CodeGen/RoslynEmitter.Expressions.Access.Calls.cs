using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Sharpy.Compiler.CodeGen.EmittedTreePrecedence;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Lambdas, type resolution helpers, tagged union constructors,
/// single-evaluation helpers, asyncio calls, call-site argument reordering
/// </summary>
internal partial class RoslynEmitter
{
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

        // Arrow lambdas have explicit type annotations → emit typed parameters
        // (x: int) -> x + 1 → (int x) => x + 1
        if (lambda.IsArrowSyntax)
        {
            return GenerateTypedLambdaExpression(lambda);
        }

        // Python-style lambdas can also carry per-parameter type annotations
        // (lambda a: int, b: int: a - b). C# requires explicitly-typed lambda
        // parameter lists to be all-or-nothing, so only emit typed parameters when
        // every parameter is annotated; otherwise fall through to the implicit path.
        if (lambda.Parameters.Length > 0
            && lambda.Parameters.All(p => p.Type != null))
        {
            return GenerateTypedLambdaExpression(lambda);
        }

        // lambda x, y: x + y → (x, y) => x + y
        var parameters = lambda.Parameters
            .Select(p => Parameter(EscapedIdentifier(ParameterCSharpName(p))))
            .ToArray();

        if (_context.SemanticInfo?.GetLambdaBodyLowering(lambda.Body) != null)
        {
            if (parameters.Length == 0)
                return ParenthesizedLambdaExpression().WithBlock(Block());
            if (parameters.Length == 1)
                return SimpleLambdaExpression(parameters[0]).WithBlock(Block());
            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithBlock(Block());
        }

        // A lambda parameter re-binds its name: an accessor-parameter rewrite in force outside
        // must not reach into the body (#1500).
        ExpressionSyntax body;
        using (SuspendAccessorParamRewriteIfShadowed(lambda.Parameters.Select(p => p.Name)))
        {
            body = GenerateExpression(lambda.Body);
        }

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

    private ExpressionSyntax GenerateTypedLambdaExpression(LambdaExpression lambda)
    {
        var lambdaType = GetExpressionSemanticType(lambda) as Semantic.FunctionType;

        var parameters = new List<ParameterSyntax>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            var paramName = ParameterCSharpName(param);
            var paramType = ResolveParameterTypeSyntax(lambda, lambdaType, i);

            parameters.Add(Parameter(EscapedIdentifier(paramName)).WithType(paramType));
        }

        if (_context.SemanticInfo?.GetLambdaBodyLowering(lambda.Body) != null)
        {
            if (parameters.Count == 0)
                return ParenthesizedLambdaExpression().WithBlock(Block());
            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithBlock(Block());
        }

        // See GenerateLambdaExpression: the parameter list re-binds these names (#1500).
        ExpressionSyntax body;
        using (SuspendAccessorParamRewriteIfShadowed(lambda.Parameters.Select(p => p.Name)))
        {
            body = GenerateExpression(lambda.Body);
        }

        if (parameters.Count == 0)
        {
            return ParenthesizedLambdaExpression()
                .WithExpressionBody(body);
        }

        return ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithExpressionBody(body);
    }

    /// <summary>
    /// Resolves the C# type syntax for a lambda parameter using a three-way fallback:
    /// (1) semantic FunctionType parameter type if available and not UnknownType,
    /// (2) AST type annotation on the parameter,
    /// (3) <c>object</c> as a last resort.
    /// </summary>
    private TypeSyntax ResolveParameterTypeSyntax(
        LambdaExpression lambda, Semantic.FunctionType? lambdaType, int index)
    {
        if (lambdaType != null && index < lambdaType.ParameterTypes.Count
            && lambdaType.ParameterTypes[index] is not UnknownType)
        {
            return _typeMapper.MapSemanticType(lambdaType.ParameterTypes[index]);
        }

        var param = lambda.Parameters[index];
        if (param.Type != null)
        {
            return _typeMapper.MapType(param.Type);
        }

        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
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
            var paramName = ParameterCSharpName(param);
            var paramType = ResolveParameterTypeSyntax(lambda, lambdaType, i);

            var paramSyntax = Parameter(EscapedIdentifier(paramName)).WithType(paramType);

            // Handle default value
            if (param.DefaultValue != null)
            {
                paramSyntax = paramSyntax.WithDefault(GenerateParameterDefault(
                    param.DefaultValue,
                    param.Type is { IsOptional: true } or { IsCSharpNullable: true }));
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

        if (_context.SemanticInfo?.GetLambdaBodyLowering(lambda.Body) != null)
        {
            return LocalFunctionStatement(returnType, Identifier(functionName))
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithBody(Block());
        }

        // Generate body expression. The parameter list re-binds these names, so an accessor
        // rewrite in force outside stops at the boundary (#1500). Default-value expressions above
        // are deliberately outside the scope: they are evaluated against the ENCLOSING binding.
        ExpressionSyntax body;
        using (SuspendAccessorParamRewriteIfShadowed(lambda.Parameters.Select(p => p.Name)))
        {
            body = GenerateExpression(lambda.Body);
        }

        var localFunc = LocalFunctionStatement(returnType, Identifier(functionName))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithExpressionBody(ArrowExpressionClause(body))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        return localFunc;
    }

    /// <summary>
    /// Gets the fully qualified C# type name for a type named by a resolved symbol (construction,
    /// isinstance, except clauses, module-qualified base names), handling cross-file references.
    /// Types are nested inside the module class, so cross-file references use
    /// Namespace.ModuleClass.TypeName.
    /// <para>
    /// Delegates to <see cref="TypeSyntaxMapper.GetTypeNameForConstruction"/>: the mapper owns type-name
    /// qualification for every emission position. This used to be a second, textually independent copy
    /// of that algorithm, and #1139's raw-BCL rule had to be added to both by hand — exactly the
    /// hand-mirrored seam contract #1146 exists to eliminate.
    /// </para>
    /// </summary>
    private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
        => _typeMapper.GetTypeNameForConstruction(typeSymbol, sharpyTypeName);

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
        var capture = IsPattern(
            captureTarget,
            VarPattern(SingleVariableDesignation(EscapedIdentifier(tempName))));
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
    /// When the target type is an Optional wrapping a function type and the source
    /// expression is a method group (identifier resolving to a function) or a lambda,
    /// C# cannot perform the two-step implicit conversion
    /// (method group/lambda → delegate → Optional&lt;delegate&gt;).
    /// Wraps the generated expression in an explicit delegate cast so the implicit
    /// Optional&lt;T&gt; conversion can apply: Printer → (Action&lt;string&gt;)Printer.
    /// Returns the expression unchanged when no conversion is needed.
    /// </summary>
    private ExpressionSyntax ApplyOptionalDelegateConversion(
        Expression sourceExpr, ExpressionSyntax generated, Semantic.SemanticType? targetType)
    {
        if (targetType is not OptionalType { UnderlyingType: Semantic.FunctionType ft }
            || ft.HasUnresolvedTypes())
        {
            return generated;
        }

        if (!IsMethodGroupOrLambda(sourceExpr))
            return generated;

        var delegateType = _typeMapper.MapSemanticType(ft);
        return ParenthesizedExpression(
            CastExpression(delegateType, ParenthesizedExpression(generated)));
    }

    /// <summary>
    /// When the target parameter is a delegate type (FunctionType) from a CLR-discovered
    /// method and the source expression is a method group, the generated C# may produce
    /// CS8622 warnings due to nullability annotation mismatches between the Sharpy-emitted
    /// method signature and the CLR delegate type. Sharpy's type system does not track
    /// .NET Nullable Reference Type annotations, so the emitted method lacks the nullable
    /// annotations the CLR delegate expects.
    ///
    /// Appends the null-forgiving operator (!) to the method group expression to suppress
    /// these warnings: <c>AddType</c> → <c>AddType!</c>.
    /// Returns the expression unchanged when no adaptation is needed.
    /// </summary>
    private ExpressionSyntax ApplyNullabilityDelegateAdaptation(
        Expression sourceExpr, ExpressionSyntax generated,
        Semantic.SemanticType? targetType, FunctionSymbol? callee)
    {
        if (callee == null)
            return generated;

        // Unwrap Optional/Nullable to find the underlying FunctionType.
        var underlying = targetType;
        if (underlying is OptionalType opt)
            underlying = opt.UnderlyingType;
        if (underlying is NullableType nullable)
            underlying = nullable.UnderlyingType;

        if (underlying is not Semantic.FunctionType)
            return generated;

        // Only method groups need adaptation — lambdas already infer
        // parameter types from the delegate context.
        if (!IsMethodGroup(sourceExpr))
            return generated;

        return Postfix(
            SyntaxKind.SuppressNullableWarningExpression, generated);
    }

    /// <summary>
    /// Whether an identifier names a function — a method group when passed as an argument.
    /// The recorded identifier symbol (<see cref="SemanticInfo.GetIdentifierSymbol"/>) is the
    /// authority: it is the only route by which a nested <c>def</c> is visible here, because
    /// the emitter's scope lookup runs at the enclosing declaration's level and the former
    /// emitter-side local-function table was deleted with #1560 (the null-forgiving <c>!</c>
    /// on <c>objectHook: InjectHook</c> in the regenerated json spy tests went missing and
    /// CS8622 came back — plan-c6ae1b verification @ 3bc6bc2a7). The name lookup remains for
    /// top-level functions in AST-only unit tests, where no identifier symbol was recorded.
    /// </summary>
    private bool IsFunctionReference(Identifier id)
        => _context.SemanticInfo?.GetIdentifierSymbol(id) is FunctionSymbol
            || _context.LookupSymbol(id.Name) is FunctionSymbol;

    /// <summary>
    /// Returns true if the expression is a method group (an identifier or member access
    /// resolving to a function symbol rather than a delegate-typed variable) or a lambda.
    /// These require an explicit delegate cast before user-defined implicit conversions
    /// (e.g., to Optional&lt;T&gt;) can apply.
    /// </summary>
    private bool IsMethodGroupOrLambda(Expression expr)
    {
        while (expr is Parenthesized paren)
            expr = paren.Expression;

        return expr switch
        {
            LambdaExpression => true,
            Identifier id => IsFunctionReference(id),
            MemberAccess ma =>
                _context.SemanticInfo?.GetMemberAccessResolution(ma)?.Member is FunctionSymbol,
            _ => false,
        };
    }

    /// <summary>
    /// Returns true if the expression is a method group (an identifier or member access
    /// resolving to a function symbol). Unlike <see cref="IsMethodGroupOrLambda"/>,
    /// excludes lambda expressions — used for nullability adaptation where lambdas
    /// already infer parameter types from the delegate context.
    /// </summary>
    private bool IsMethodGroup(Expression expr)
    {
        while (expr is Parenthesized paren)
            expr = paren.Expression;

        return expr switch
        {
            Identifier id => IsFunctionReference(id),
            MemberAccess ma =>
                _context.SemanticInfo?.GetMemberAccessResolution(ma)?.Member is FunctionSymbol,
            _ => false,
        };
    }

    // When a bytes or list argument is passed to an array (T[]) parameter, emit
    // .ToArray() to bridge Sharpy.Bytes → byte[] (#941) or Sharpy.List<T> → T[]
    // (#959). Sharpy.List<T> implements IEnumerable<T>, so .ToArray() resolves to
    // System.Linq.Enumerable.ToArray (System.Linq is always imported in generated
    // code); Sharpy.Bytes has an instance ToArray() that takes precedence.
    private ExpressionSyntax ApplyArrayBridge(
        Expression sourceExpr, ExpressionSyntax generated, Semantic.SemanticType? targetType)
    {
        if (targetType is not GenericType { Name: "array" } arrayType
            || arrayType.TypeArguments.Count != 1)
            return generated;

        var argType = GetExpressionSemanticType(sourceExpr);

        // bytes → array[byte] (#941). CLR byte maps to Sharpy uint8.
        var bytesToByteArray =
            arrayType.TypeArguments[0] is BuiltinType bt
            && (bt.Name == "uint8" || bt.Name == "byte")
            && argType is UserDefinedType { Name: "bytes" };

        // list[T] → array[T] (#959). Element type must match exactly (UnknownType is
        // a wildcard for empty list literals), mirroring GenericType.IsAssignableTo so
        // the emitted .ToArray() yields an array of the parameter's element type.
        var listToArray =
            argType is GenericType { Name: "list" } listType
            && listType.TypeArguments.Count == 1
            && (listType.TypeArguments[0] is UnknownType
                || arrayType.TypeArguments[0] is UnknownType
                || listType.TypeArguments[0].Equals(arrayType.TypeArguments[0]));

        if (!bytesToByteArray && !listToArray)
            return generated;

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                ParenthesizedExpression(generated),
                IdentifierName("ToArray")));
    }

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
            ("bytes", "fromhex") => "global::Sharpy.BytesFromhex.Fromhex",
            _ => null
        };
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
    /// Lowers the variadic value form of <c>min()</c>/<c>max()</c> with a <c>key=</c> keyword
    /// argument (<c>min(a, b, …, key=f)</c> with ≥2 positional args) to the iterable+key overload
    /// <c>Min&lt;T,TKey&gt;(IEnumerable&lt;T&gt;, Func&lt;T,TKey&gt;)</c>, emitting
    /// <c>Min(new[]{ a, b, … }, f)</c>. The C# value-form overload
    /// <c>Min&lt;T&gt;(T, T, params T[])</c> has no key slot, so a named <c>key:</c> argument
    /// collides with a positional and produces CS1744 (#1012). Routing through an implicit array
    /// (which is <c>IEnumerable&lt;T&gt;</c>) avoids that and lets C# infer the element type —
    /// including #1014's mixed-numeric promotion (<c>double[]</c>).
    /// </summary>
    /// <remarks>
    /// Detects min/max by builtin name; the caller only invokes this when the call is an
    /// unshadowed builtin (<c>isBuiltinFunc</c>). Returns <see langword="null"/> for anything but
    /// the value-form-with-key shape, leaving the single-positional iterable form
    /// (<c>min(it, key=f)</c>, which already binds to the key overload) and the no-key value form
    /// (<c>min(a, b, c)</c>) on the normal emission path.
    /// </remarks>
    private ExpressionSyntax? TryGenerateMinMaxValueFormWithKey(FunctionCall call, Identifier funcName)
    {
        if (funcName.Name is not (BuiltinNames.Min or BuiltinNames.Max))
            return null;
        // Value-form-with-key shape: a single key= kwarg AND >= 2 positional args.
        if (call.Arguments.Length < 2 || call.KeywordArguments.Length != 1)
            return null;
        var keyArg = call.KeywordArguments[0];
        if (keyArg.Name != "key")
            return null;

        // new[] { a, b, … } — implicit array so C# infers the (possibly promoted) element type.
        var elements = call.Arguments.Select(GenerateExpression).ToArray();
        var arrayExpr = ImplicitArrayCreationExpression(
            InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(elements)));

        var keyValue = GenerateExpression(keyArg.Value);

        var target = MakeGlobalQualifiedName("Sharpy", "Builtins",
            NameCasing.ResolveMethod(funcName.Name, funcName.IsNameBacktickEscaped));
        return InvocationExpression(target)
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(arrayExpr),
                Argument(keyValue)
            })));
    }

    // Resolve the C# parameter name for a kwarg. For CLR-discovered methods
    // (ClrMethodName != null), parameter names from reflection are the actual C# identifiers
    // (e.g., "exist_ok"), so we look up the declared param and use its name verbatim.
    // For Sharpy-defined functions, parameters are stored in snake_case and the C# name
    // is obtained via camelCase mangling. Both paths escape C# keywords.
    //
    // Regression guard (#942): CLR parameter names are stored UNMANGLED (unlike methods and
    // properties, which are reverse-mangled during discovery). The verbatim match below must
    // stay verbatim — routing it through NameMangler would re-break #942.
    private static string GetCSharpParameterName(string sharpyName, FunctionSymbol? funcSymbol)
    {
        if (funcSymbol?.ClrMethodName != null)
        {
            var camelName = NameMangler.ToCamelCase(sharpyName);
            var match = funcSymbol.Parameters.FirstOrDefault(p => p.Name == sharpyName)
                ?? funcSymbol.Parameters.FirstOrDefault(p => p.Name == camelName);
            if (match != null)
                return CSharpKeywords.EscapeIfNeeded(match.Name);
        }
        return NameMangler.ToCamelCase(sharpyName);
    }

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
            var positionalArgs = GeneratePositionalArguments(call.Arguments, funcSymbol);
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
            {
                var csharpName = GetCSharpParameterName(kwarg.Name, funcSymbol);
                var kwargValue = GenerateExpression(kwarg.Value);
                if (funcSymbol != null)
                {
                    var targetParam = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name)
                        ?? funcSymbol.Parameters.FirstOrDefault(p => p.Name == csharpName);
                    if (targetParam is { IsLateBound: true })
                        csharpName += LateBoundSuffix;
                    if (targetParam != null)
                    {
                        kwargValue = ApplyOptionalDelegateConversion(kwarg.Value, kwargValue, targetParam.Type);
                        kwargValue = ApplyNullabilityDelegateAdaptation(kwarg.Value, kwargValue, targetParam.Type, funcSymbol);
                        kwargValue = ApplyArrayBridge(kwarg.Value, kwargValue, targetParam.Type);
                    }
                }
                else if (IsMethodGroup(kwarg.Value))
                {
                    // When the callee FunctionSymbol is unavailable (e.g., CLR constructors
                    // without resolved parameter metadata), apply ! to method group keyword
                    // args to suppress potential CS8622 NRT nullability mismatch warnings.
                    kwargValue = Postfix(
                        SyntaxKind.SuppressNullableWarningExpression, kwargValue);
                }
                return Argument(kwargValue)
                    .WithNameColon(NameColon(EscapedIdentifierName(csharpName)));
            });
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
            string csharpName = GetCSharpParameterName(firstParam.Name, funcSymbol);
            argByParam[firstParam.Name] = prependedArgument
                .WithNameColon(NameColon(EscapedIdentifierName(csharpName)));
            paramStartIndex = 1;
        }

        for (int pi = paramStartIndex; pi < paramList.Count; pi++)
        {
            var param = paramList[pi];
            if (param.IsVariadic)
                continue;

            string csharpParamName = GetCSharpParameterName(param.Name, funcSymbol);
            if (param.IsLateBound)
                csharpParamName += LateBoundSuffix;

            if (keywordArgsByName.TryGetValue(param.Name, out var kwarg))
            {
                var kwargGenerated = GenerateExpression(kwarg.Value);
                kwargGenerated = ApplyOptionalDelegateConversion(
                    kwarg.Value, kwargGenerated, param.Type);
                kwargGenerated = ApplyNullabilityDelegateAdaptation(
                    kwarg.Value, kwargGenerated, param.Type, funcSymbol);
                kwargGenerated = ApplyArrayBridge(
                    kwarg.Value, kwargGenerated, param.Type);
                argByParam[param.Name] = Argument(kwargGenerated)
                    .WithNameColon(NameColon(EscapedIdentifierName(csharpParamName)));
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
                        var remainingCsharpName = GetCSharpParameterName(remaining.Name, funcSymbol);
                        var remainingParam = funcSymbol!.Parameters.FirstOrDefault(p => p.Name == remaining.Name);
                        if (remainingParam is { IsLateBound: true })
                            remainingCsharpName += LateBoundSuffix;
                        result.Add(Argument(GenerateExpression(remaining.Value))
                            .WithNameColon(NameColon(EscapedIdentifierName(remainingCsharpName))));
                    }
                    return result.ToArray();
                }
                var posGenerated = GenerateExpression(argExpr);
                posGenerated = ApplyOptionalDelegateConversion(
                    argExpr, posGenerated, param.Type);
                posGenerated = ApplyNullabilityDelegateAdaptation(
                    argExpr, posGenerated, param.Type, funcSymbol);
                posGenerated = ApplyArrayBridge(
                    argExpr, posGenerated, param.Type);
                argByParam[param.Name] = Argument(posGenerated)
                    .WithNameColon(NameColon(EscapedIdentifierName(csharpParamName)));
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
            var remainingCsharpName = GetCSharpParameterName(remaining.Name, funcSymbol);
            var remainingParam = funcSymbol!.Parameters.FirstOrDefault(p => p.Name == remaining.Name);
            if (remainingParam is { IsLateBound: true })
                remainingCsharpName += LateBoundSuffix;
            orderedResult.Add(Argument(GenerateExpression(remaining.Value))
                .WithNameColon(NameColon(EscapedIdentifierName(remainingCsharpName))));
        }

        // Phase 3: Variadic trailing args (remaining positional, unnamed)
        var variadicParam = paramList.FirstOrDefault(p => p.IsVariadic);
        var remainingArgs = call.Arguments.Skip(positionalIndex).ToList();
        bool needsCombinedArray = variadicParam != null
            && remainingArgs.Any(a => a is SpreadElement)
            && remainingArgs.Count > 1;

        if (needsCombinedArray)
        {
            // Mixed positional + spread targeting params T[]: combine into a single
            // array so C# sees one T[] argument instead of loose T + T[].
            // total(1, 2, *[3, 4]) → total(new int[] { 1, 2 }.Concat([3, 4]).ToArray())
            orderedResult.Add(Argument(
                BuildCombinedVariadicArray(remainingArgs, variadicParam!)));
        }
        else
        {
            foreach (var argExpr in remainingArgs)
            {
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
            }
        }

        return orderedResult.ToArray();
    }

    private ExpressionSyntax BuildCombinedVariadicArray(
        List<Expression> args, ParameterSymbol variadicParam)
    {
        var elementType = _typeMapper.MapSemanticType(variadicParam.Type);
        var positionalBuffer = new List<ExpressionSyntax>();
        ExpressionSyntax? combined = null;

        void FlushPositional()
        {
            if (positionalBuffer.Count == 0)
                return;
            var array = ArrayCreationExpression(
                    ArrayType(elementType)
                        .WithRankSpecifiers(SingletonList(
                            ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                                OmittedArraySizeExpression())))))
                .WithInitializer(InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SeparatedList(positionalBuffer)));
            combined = ConcatOnto(combined, array);
            positionalBuffer.Clear();
        }

        foreach (var arg in args)
        {
            if (arg is SpreadElement spread)
            {
                FlushPositional();
                combined = ConcatOnto(combined, GenerateExpression(spread.Value));
            }
            else
            {
                positionalBuffer.Add(GenerateExpression(arg));
            }
        }

        FlushPositional();

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                combined!, IdentifierName("ToArray")))
            .WithArgumentList(ArgumentList());
    }

    private static ExpressionSyntax ConcatOnto(
        ExpressionSyntax? existing, ExpressionSyntax next)
    {
        if (existing == null)
            return next;
        return InvocationExpression(Member(existing, "Concat"))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(next))));
    }

    /// <summary>
    /// Generates positional arguments for a function call, handling SpreadElement arguments.
    /// For spread of a tuple type → expands to .Item1, .Item2, ... individual arguments.
    /// For spread of an iterable type → generates .ToArray() and passes as a single argument.
    /// </summary>
    /// <summary>
    /// Applies the iterable projection the TypeChecker recorded for a builtin-call argument (#1154,
    /// #1198): <see cref="IterableProjectionKind.DictKeys"/> becomes <c>arg.Keys()</c>
    /// (<c>DictKeyView&lt;K,V&gt;</c>, matching Python's key iteration),
    /// <see cref="IterableProjectionKind.TupleToArray"/> becomes a typed array of the tuple's
    /// members, and <see cref="IterableProjectionKind.Direct"/> passes through (the source already
    /// implements <c>IEnumerable&lt;element&gt;</c>). Absent mark ⇒ unchanged. The emitter is a pure
    /// applier here — it switches on the recorded tag and never inspects types (repo rule 2; the
    /// NarrowedReadLowering precedent).
    /// </summary>
    private ExpressionSyntax ApplyIterableProjection(Expression argNode, ExpressionSyntax generated)
    {
        if (_context.SemanticInfo?.GetIterableProjection(argNode) is not { } projection)
            return generated;

        switch (projection.Kind)
        {
            case IterableProjectionKind.DictKeys:
                return InvocationExpression(Member(generated, "Keys"));

            case IterableProjectionKind.TupleToArray:
                return GenerateTupleElementArray(projection, generated);

            case IterableProjectionKind.StrToList:
                // Builtins.ListFromStr(s) -> List<string> of one-character strings. The operand is
                // spliced once, so `sorted(make_str())` calls make_str once.
                return InvocationExpression(
                    MakeGlobalQualifiedName("Sharpy", "Builtins", "ListFromStr"))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(generated))));

            default:
                return generated;
        }
    }

    /// <summary>
    /// Spreads a tuple argument into <c>new element[] { t.Item1, …, t.ItemN }</c> so it can bind an
    /// <c>IEnumerable&lt;element&gt;</c> parameter — <c>System.ValueTuple</c> implements none (#1198).
    /// The element type and arity come from the recorded mark; the emitter chooses nothing.
    ///
    /// <para>The operand is evaluated exactly ONCE: anything but a plain identifier is hoisted into a
    /// temp local first, because splicing <c>.Item1…ItemN</c> off the raw expression would evaluate
    /// <c>sum(make_tuple())</c>'s call N times (the same "no operand spliced twice" rule
    /// <see cref="GenerateFloorModulo"/> documents).</para>
    /// </summary>
    private ExpressionSyntax GenerateTupleElementArray(
        IterableArgumentProjection projection, ExpressionSyntax generated)
    {
        var operand = generated;
        if (operand is not IdentifierNameSyntax)
        {
            var tempName = GenerateTempVarName("tuple_iter");
            _hoistedStatements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(tempName))
                            .WithInitializer(EqualsValueClause(generated))))));
            operand = IdentifierName(tempName);
        }

        var members = new List<ExpressionSyntax>();
        for (int i = 1; i <= projection.TupleArity; i++)
        {
            members.Add(MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression, operand, IdentifierName($"Item{i}")));
        }

        return ArrayCreationExpression(
                ArrayType(_typeMapper.MapSemanticType(projection.ElementType))
                    .WithRankSpecifiers(SingletonList(
                        ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                            OmittedArraySizeExpression())))))
            .WithInitializer(InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList(members)));
    }

    private IEnumerable<ArgumentSyntax> GeneratePositionalArguments(
        System.Collections.Immutable.ImmutableArray<Expression> arguments,
        FunctionSymbol? funcSymbol = null)
    {
        // Positional parameter list (excluding self/cls) for target-typed argument
        // conversions (e.g., method group → Optional<delegate> needs an explicit cast).
        var positionalParams = funcSymbol?.Parameters
            .Where(p => p.Name != PythonNames.Self && p.Name != PythonNames.Cls)
            .ToList();

        int argIndex = -1;
        bool sawSpread = false;
        foreach (var arg in arguments)
        {
            argIndex++;
            if (arg is SpreadElement spread)
            {
                // Spreads expand to a variable number of arguments, so positional
                // index → parameter mapping is no longer reliable past this point.
                sawSpread = true;
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
            else if (arg is Parser.Ast.ModifiedArgument modArg)
            {
                // Inline out declaration: out name: type → out type name
                if (modArg.InlineName != null)
                {
                    // Map the type: "auto" → var, otherwise use TypeSyntaxMapper
                    TypeSyntax typeSyntax;
                    if (modArg.InlineType!.Name == "auto")
                    {
                        typeSyntax = IdentifierName("var");
                    }
                    else
                    {
                        typeSyntax = _typeMapper.MapType(modArg.InlineType);
                    }

                    // The binding is a recorded fact (#1560 R3): a rebind of an already-bound name
                    // passes the existing C# local (`out v`), a fresh name declares it
                    // (`out int v`). The name-based arm is for an argument the checker never saw
                    // (AST-only unit tests).
                    var outInfo = _context.SemanticInfo;
                    var outSymbol = outInfo?.GetInlineOutSymbol(modArg);
                    string mangledName;
                    var rebinds = false;
                    if (outSymbol != null)
                    {
                        var outBinding = outInfo!.GetTargetBinding(modArg)
                            ?? throw new InvalidOperationException(
                                $"No TargetBinding recorded for inline out '{modArg.InlineName}' at {modArg.LineStart}:{modArg.ColumnStart}");
                        mangledName = GetCSharpNameForSymbol(outSymbol);
                        rebinds = outBinding.Kind == TargetBindingKind.Rebinds;
                    }
                    else
                    {
                        mangledName = GetMangledVariableName(modArg.InlineName,
                            isNewDeclaration: true, modArg.IsNameBacktickEscaped);
                    }

                    if (rebinds)
                    {
                        yield return Argument(EscapedIdentifierName(mangledName))
                            .WithRefKindKeyword(Token(SyntaxKind.OutKeyword));
                    }
                    else
                    {
                        yield return Argument(
                            DeclarationExpression(
                                typeSyntax,
                                SingleVariableDesignation(EscapedIdentifier(mangledName))))
                            .WithRefKindKeyword(Token(SyntaxKind.OutKeyword));
                    }
                }
                else
                {
                    var refKind = modArg.Modifier switch
                    {
                        Parser.Ast.ParameterModifier.Ref => SyntaxKind.RefKeyword,
                        Parser.Ast.ParameterModifier.Out => SyntaxKind.OutKeyword,
                        Parser.Ast.ParameterModifier.In => SyntaxKind.InKeyword,
                        _ => SyntaxKind.None
                    };
                    var csArg = Argument(GenerateExpression(modArg.Argument));
                    if (refKind != SyntaxKind.None)
                        csArg = csArg.WithRefKindKeyword(Token(refKind));
                    yield return csArg;
                }
            }
            else
            {
                var generated = GenerateExpression(arg);
                generated = ApplyIterableProjection(arg, generated);
                if (positionalParams != null && !sawSpread
                    && argIndex < positionalParams.Count
                    && !positionalParams[argIndex].IsVariadic
                    && !positionalParams[argIndex].IsKeywordOnly)
                {
                    generated = ApplyOptionalDelegateConversion(
                        arg, generated, positionalParams[argIndex].Type);
                    generated = ApplyNullabilityDelegateAdaptation(
                        arg, generated, positionalParams[argIndex].Type, funcSymbol);
                    generated = ApplyArrayBridge(
                        arg, generated, positionalParams[argIndex].Type);
                }
                yield return Argument(generated);
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
        var csharpTypeName = NameCasing.ResolveType(originalName, typeSymbol.IsNameBacktickEscaped);
        var fqn = GetFullyQualifiedTypeName(typeSymbol, originalName);

        if (fqn.Contains('.', StringComparison.Ordinal))
        {
            // A global::-prefixed FQN must build its leftmost segment as a real
            // AliasQualifiedName, not IdentifierName("global::System") — the latter prints
            // correctly but is a single broken identifier token that fails to bind under direct
            // tree handoff (#1095). The member-access spine matches what ParseText produces for a
            // dotted name in expression position, so the printed text is unchanged.
            ExpressionSyntax baseExpr;
            string[] parts;
            if (fqn.StartsWith("global::", StringComparison.Ordinal))
            {
                parts = fqn["global::".Length..].Split('.');
                baseExpr = AliasQualifiedName(
                    IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                    IdentifierName(parts[0]));
            }
            else
            {
                parts = fqn.Split('.');
                baseExpr = IdentifierName(parts[0]);
            }

            return parts.Skip(1).Aggregate(
                baseExpr,
                (left, part) => MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression, left, IdentifierName(part)));
        }

        if (_currentTypeSymbol != null)
        {
            var moduleClassName = GetModuleClassName();
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(moduleClassName),
                EscapedIdentifierName(csharpTypeName));
        }

        return EscapedIdentifierName(csharpTypeName);
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
        var fieldName = codeGenInfo?.CSharpName ?? NameCasing.ResolveField(memberName, isBacktickEscaped: fieldSymbol.IsNameBacktickEscaped);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            typeExpr,
            EscapedIdentifierName(fieldName));
    }

    /// <summary>
    /// Generates code for a <c>functools.partial(f, fixed_args..., kw=val, ...)</c> call.
    /// Desugars to a lambda that captures the fixed arguments and forwards the remaining
    /// parameters to the target function:
    /// <code>functools.partial(add, 5) -> (int x) => add(5, x)</code>
    /// </summary>
    private ExpressionSyntax GenerateFunctoolsPartialCall(FunctionCall call, Semantic.FunctoolsPartialSpec spec)
    {
        // call.Arguments[0] is the target callable; remaining positional args are fixed.
        var targetExpr = call.Arguments[0];
        var targetCSharp = GenerateExpression(targetExpr);

        var resultType = GetExpressionSemanticType(call) as Semantic.FunctionType;
        if (resultType == null)
        {
            return GenerateCall(call);
        }

        // Evaluate fixed positional and keyword args. Side-effect-bearing expressions are
        // hoisted into local temps so they execute exactly once (matching Python semantics
        // where partial captures its arguments at construction time).
        var fixedPositionalArgs = new List<ExpressionSyntax>(call.Arguments.Length - 1);
        for (int i = 1; i < call.Arguments.Length; i++)
        {
            fixedPositionalArgs.Add(CaptureFixedArg(call.Arguments[i]));
        }

        // The lambda's parameters come verbatim from the spec's remaining-parameter list — the
        // TypeChecker already resolved the subset, order, C# names and types (#1520). Only the
        // camelCase spelling of the lambda parameter is applied here.
        var lambdaParams = new List<ParameterSyntax>(spec.RemainingParameters.Count);
        var lambdaParamIdentifiers = new List<string>(spec.RemainingParameters.Count);
        foreach (var (name, _, type) in spec.RemainingParameters)
        {
            var paramTypeSyntax = _typeMapper.MapSemanticType(type);
            var paramName = NameMangler.ToCamelCase(name);
            lambdaParams.Add(Parameter(EscapedIdentifier(paramName)).WithType(paramTypeSyntax));
            lambdaParamIdentifiers.Add(paramName);
        }

        // Build the call inside the lambda body:
        //   target(fixed_positional..., remaining..., fixedKw1: val1, ...)
        // With keyword fixes present, the remaining arguments are bound BY NAME (the spec's
        // resolved C# parameter names): positionally they would walk into the keyword-fixed
        // parameter's slot whenever it precedes a remaining one (CS1744 behind SPY0908).
        var bindRemainingByName = spec.FixedKeywords.Count > 0;
        var bodyArgs = new List<ArgumentSyntax>();
        foreach (var fa in fixedPositionalArgs)
        {
            bodyArgs.Add(Argument(fa));
        }
        for (int i = 0; i < lambdaParamIdentifiers.Count; i++)
        {
            var arg = Argument(IdentifierName(lambdaParamIdentifiers[i]));
            if (bindRemainingByName)
            {
                arg = arg.WithNameColon(
                    NameColon(EscapedIdentifierName(spec.RemainingParameters[i].CSharpName)));
            }
            bodyArgs.Add(arg);
        }
        foreach (var (csharpName, argumentIndex) in spec.FixedKeywords)
        {
            bodyArgs.Add(Argument(CaptureFixedArg(call.KeywordArguments[argumentIndex].Value))
                .WithNameColon(NameColon(EscapedIdentifierName(csharpName))));
        }

        var body = InvocationExpression(targetCSharp)
            .WithArgumentList(ArgumentList(SeparatedList(bodyArgs)));

        if (lambdaParams.Count == 0)
        {
            return ParenthesizedLambdaExpression().WithExpressionBody(body);
        }
        if (lambdaParams.Count == 1)
        {
            return ParenthesizedLambdaExpression()
                .WithParameterList(ParameterList(SeparatedList(lambdaParams)))
                .WithExpressionBody(body);
        }
        return ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(SeparatedList(lambdaParams)))
            .WithExpressionBody(body);
    }

    /// <summary>
    /// Evaluates a fixed argument for <c>functools.partial</c>. Side-effect-free
    /// expressions (literals, identifiers) are inlined; everything else is hoisted
    /// into a local temp so it executes once at the partial-construction site.
    /// </summary>
    private ExpressionSyntax CaptureFixedArg(Expression argExpr)
    {
        var generated = GenerateExpression(argExpr);
        if (IsSideEffectFree(argExpr))
        {
            return generated;
        }

        var tempName = GenerateTempVarName("partialArg");
        _hoistedStatements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(tempName))
                        .WithInitializer(EqualsValueClause(generated))))));
        return IdentifierName(tempName);
    }
}
