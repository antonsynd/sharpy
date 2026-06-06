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

    private ExpressionSyntax GenerateTypedLambdaExpression(LambdaExpression lambda)
    {
        var lambdaType = GetExpressionSemanticType(lambda) as Semantic.FunctionType;

        var parameters = new List<ParameterSyntax>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            var paramName = NameMangler.ToCamelCase(param.Name);
            var paramType = ResolveParameterTypeSyntax(lambda, lambdaType, i);

            parameters.Add(Parameter(Identifier(paramName)).WithType(paramType));
        }

        var body = GenerateExpression(lambda.Body);

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
            var paramName = NameMangler.ToCamelCase(param.Name);
            var paramType = ResolveParameterTypeSyntax(lambda, lambdaType, i);

            var paramSyntax = Parameter(Identifier(paramName)).WithType(paramType);

            // Handle default value
            if (param.DefaultValue != null)
            {
                paramSyntax = paramSyntax.WithDefault(
                    EqualsValueClause(GenerateExpression(param.DefaultValue)));
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
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithExpressionBody(ArrowExpressionClause(body))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

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
        // differs from the module name (e.g., argparse -> Sharpy.ArgumentParser)
        if (typeSymbol.ClrType != null && typeSymbol.ClrType.Namespace == "Sharpy")
        {
            var fullName = ClrNameHelper.StripArity(typeSymbol.ClrType.FullName!);
            return $"global::{fullName}";
        }

        // All CLR types need global:: when inside a user namespace to avoid
        // namespace prepending (e.g., Sharpy.System.Text.StringBuilder)
        if (typeSymbol.ClrType != null && !string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            var fullName = ClrNameHelper.StripArity(typeSymbol.ClrType.FullName!);
            return $"global::{fullName}";
        }

        // Check if type is from a different file (cross-file reference)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath) &&
            !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            var moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
            var typeName = NameCasing.ResolveType(sharpyTypeName, isBacktickEscaped: false);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Check if type is from an external module (imported via DefiningModule)
        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            var moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
            var typeName = NameCasing.ResolveType(sharpyTypeName, isBacktickEscaped: false);

            return BuildQualifiedTypeName(moduleNamespace, typeName);
        }

        // Type is in current file - use simple name
        return NameCasing.ResolveType(sharpyTypeName, isBacktickEscaped: false);
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
                        namespaceParts.Add(NameCasing.ResolveType(part, isBacktickEscaped: false));
                    }
                }
            }

            // Add file name part (skip __init__ as it represents the package itself)
            if (!string.Equals(fileName, DunderNames.Init, StringComparison.OrdinalIgnoreCase))
            {
                namespaceParts.Add(NameCasing.ResolveType(fileName, isBacktickEscaped: false));
            }

            if (namespaceParts.Count > 0)
            {
                return string.Join(".", namespaceParts);
            }
        }

        // Fallback: just use file name
        var fallbackFileName = Path.GetFileNameWithoutExtension(filePath);
        return NameCasing.ResolveType(fallbackFileName, isBacktickEscaped: false);
    }

    /// <summary>
    /// Converts a module path (e.g., "animal" or "lib.animal") to a C# namespace segment.
    /// </summary>
    private static string ConvertModuleToNamespace(string modulePath)
    {
        var parts = modulePath.Split('.');
        return string.Join(".", parts.Select(NameMangler.ToNamespacePart));
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
            Identifier id => _context.LookupSymbol(id.Name) is FunctionSymbol,
            MemberAccess ma =>
                _context.SemanticInfo?.GetMemberAccessResolution(ma)?.Member is FunctionSymbol,
            _ => false,
        };
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
            var positionalArgs = GeneratePositionalArguments(call.Arguments, funcSymbol);
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
            {
                var csharpName = NameMangler.ToCamelCase(kwarg.Name);
                var kwargValue = GenerateExpression(kwarg.Value);
                if (funcSymbol != null)
                {
                    var targetParam = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (targetParam is { IsLateBound: true })
                        csharpName += LateBoundSuffix;
                    if (targetParam != null)
                        kwargValue = ApplyOptionalDelegateConversion(kwarg.Value, kwargValue, targetParam.Type);
                }
                return Argument(kwargValue)
                    .WithNameColon(NameColon(IdentifierName(csharpName)));
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
            if (param.IsLateBound)
                csharpParamName += LateBoundSuffix;

            if (keywordArgsByName.TryGetValue(param.Name, out var kwarg))
            {
                argByParam[param.Name] = Argument(
                    ApplyOptionalDelegateConversion(
                        kwarg.Value, GenerateExpression(kwarg.Value), param.Type))
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
                        var remainingCsharpName = NameMangler.ToCamelCase(remaining.Name);
                        var remainingParam = funcSymbol!.Parameters.FirstOrDefault(p => p.Name == remaining.Name);
                        if (remainingParam is { IsLateBound: true })
                            remainingCsharpName += LateBoundSuffix;
                        result.Add(Argument(GenerateExpression(remaining.Value))
                            .WithNameColon(NameColon(IdentifierName(remainingCsharpName))));
                    }
                    return result.ToArray();
                }
                argByParam[param.Name] = Argument(
                    ApplyOptionalDelegateConversion(
                        argExpr, GenerateExpression(argExpr), param.Type))
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
            var remainingCsharpName = NameMangler.ToCamelCase(remaining.Name);
            var remainingParam = funcSymbol!.Parameters.FirstOrDefault(p => p.Name == remaining.Name);
            if (remainingParam is { IsLateBound: true })
                remainingCsharpName += LateBoundSuffix;
            orderedResult.Add(Argument(GenerateExpression(remaining.Value))
                .WithNameColon(NameColon(IdentifierName(remainingCsharpName))));
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

                    // Register the variable for subsequent references
                    var mangledName = GetMangledVariableName(modArg.InlineName, isNewDeclaration: true);

                    yield return Argument(
                        DeclarationExpression(
                            typeSyntax,
                            SingleVariableDesignation(Identifier(mangledName))))
                        .WithRefKindKeyword(Token(SyntaxKind.OutKeyword));
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
                if (positionalParams != null && !sawSpread
                    && argIndex < positionalParams.Count
                    && !positionalParams[argIndex].IsVariadic
                    && !positionalParams[argIndex].IsKeywordOnly)
                {
                    generated = ApplyOptionalDelegateConversion(
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
        var csharpTypeName = NameCasing.ResolveType(originalName, isBacktickEscaped: false);
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
        var fieldName = codeGenInfo?.CSharpName ?? NameCasing.ResolveField(memberName, isBacktickEscaped: fieldSymbol.IsNameBacktickEscaped);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            typeExpr,
            IdentifierName(fieldName));
    }

    /// <summary>
    /// Returns true if <paramref name="memberName"/> (Python name) resolves to a CLR
    /// property on the receiver's type, AND no method with the same PascalCase name exists.
    /// Used to emit property access (no parens) instead of method invocation (#555).
    /// </summary>
    private bool IsClrPropertyAccess(Expression receiver, string memberName)
    {
        var receiverType = GetExpressionSemanticType(receiver);
        TypeSymbol? typeSymbol = receiverType switch
        {
            UserDefinedType udt => udt.Symbol as TypeSymbol,
            GenericType gt => _context.LookupSymbol(gt.Name) as TypeSymbol
                              ?? gt.GenericDefinition,
            _ => null
        };

        if (typeSymbol == null)
            return false;

        // Check the type hierarchy for a property with this Sharpy name
        foreach (var ts in GetTypeAndBases(typeSymbol))
        {
            foreach (var prop in ts.Properties)
            {
                if (string.Equals(prop.Name, memberName, StringComparison.OrdinalIgnoreCase))
                {
                    // Confirm no method with the same name exists (method takes priority)
                    var (method, _) = TypeHierarchyService.FindMethod(typeSymbol, memberName);
                    return method == null;
                }
            }
        }

        return false;
    }

    private static IEnumerable<TypeSymbol> GetTypeAndBases(TypeSymbol typeSymbol)
    {
        yield return typeSymbol;
        foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(typeSymbol))
            yield return baseType;
    }

    // Caches reflection lookups of (CLR type, Sharpy member name) -> original CLR method name.
    private readonly Dictionary<(System.Type, string), string?> _clrReflectionMethodNames = new();

    /// <summary>
    /// Resolves the original CLR method name for a member access whose receiver is a
    /// CLR-backed type, when no discovered <see cref="FunctionSymbol"/> is available
    /// (e.g., directly-imported .NET types whose methods are not eagerly discovered).
    /// Matches the written Sharpy member name against the reverse-mangled name of each
    /// CLR method so acronym casing survives (is_os_platform → IsOSPlatform). Returns
    /// null when no unambiguous match exists, leaving normal mangling in place.
    /// </summary>
    private string? ResolveClrMethodNameByReflection(Expression receiver, string memberName)
    {
        var clrType = GetReceiverClrType(receiver);
        if (clrType == null)
            return null;

        if (_clrReflectionMethodNames.TryGetValue((clrType, memberName), out var cached))
            return cached;

        string? resolved = null;
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance;
        foreach (var method in clrType.GetMethods(flags))
        {
            // A CLR name already written verbatim (PascalCase) should be left untouched;
            // only match when the Sharpy (reverse-mangled) form equals the written name.
            if (Discovery.ReverseNameMangler.ToSharpyName(method.Name, Discovery.ReverseNameContext.Method) == memberName)
            {
                if (resolved != null && resolved != method.Name)
                {
                    // Ambiguous (multiple distinct CLR names map to this Sharpy name) — bail out.
                    resolved = null;
                    break;
                }
                resolved = method.Name;
            }
        }

        _clrReflectionMethodNames[(clrType, memberName)] = resolved;
        return resolved;
    }

    /// <summary>
    /// Gets the CLR <see cref="System.Type"/> of a member-access receiver, for both static
    /// (type-name) receivers and instance receivers. Returns null when the receiver is not
    /// backed by a CLR type.
    /// </summary>
    private System.Type? GetReceiverClrType(Expression receiver)
    {
        // Static receiver: a bare type-name identifier (e.g., RuntimeInformation.is_os_platform()).
        if (receiver is Identifier id && _context.LookupSymbol(id.Name) is TypeSymbol staticTs)
            return staticTs.ClrType;

        // Instance receiver: resolve via the receiver's semantic type.
        return GetExpressionSemanticType(receiver) switch
        {
            UserDefinedType udt when (udt.Symbol as TypeSymbol)?.ClrType is { } ct => ct,
            BuiltinType bt => bt.ClrType,
            _ => null
        };
    }

    /// <summary>
    /// Generates code for a <c>functools.partial(f, fixed_args..., kw=val, ...)</c> call.
    /// Desugars to a lambda that captures the fixed arguments and forwards the remaining
    /// parameters to the target function:
    /// <code>functools.partial(add, 5) -> (int x) => add(5, x)</code>
    /// </summary>
    private ExpressionSyntax GenerateFunctoolsPartialCall(FunctionCall call)
    {
        // call.Arguments[0] is the target callable; remaining positional args are fixed.
        var targetExpr = call.Arguments[0];
        var targetCSharp = GenerateExpression(targetExpr);

        var resultType = GetExpressionSemanticType(call) as Semantic.FunctionType;
        if (resultType == null)
        {
            // Defensive fallback: semantic analysis failed to compute a FunctionType
            // (should not happen when IsFunctoolsPartialCall returns true and type checking succeeded).
            return GenerateCall(call);
        }

        // Resolve target FunctionSymbol so we can name the remaining lambda parameters
        // after the original function's parameter names (preserves keyword-fix support).
        FunctionSymbol? targetSymbol = null;
        if (targetExpr is Parser.Ast.Identifier targetId)
        {
            targetSymbol = _context.LookupSymbol(targetId.Name) as FunctionSymbol;
        }

        // Evaluate fixed positional and keyword args. Side-effect-bearing expressions are
        // hoisted into local temps so they execute exactly once (matching Python semantics
        // where partial captures its arguments at construction time).
        var fixedPositionalArgs = new List<ExpressionSyntax>(call.Arguments.Length - 1);
        for (int i = 1; i < call.Arguments.Length; i++)
        {
            fixedPositionalArgs.Add(CaptureFixedArg(call.Arguments[i]));
        }

        var fixedKwargNames = new HashSet<string>(StringComparer.Ordinal);
        var fixedKwargs = new List<(string Name, ExpressionSyntax Value)>(call.KeywordArguments.Length);
        foreach (var kwarg in call.KeywordArguments)
        {
            fixedKwargs.Add((kwarg.Name, CaptureFixedArg(kwarg.Value)));
            fixedKwargNames.Add(kwarg.Name);
        }

        // Determine names for the lambda's remaining parameters. Prefer the original
        // function's parameter names (when a FunctionSymbol target is available); fall
        // back to synthetic names otherwise.
        var remainingParamNames = new List<string>(resultType.ParameterTypes.Count);
        if (targetSymbol != null)
        {
            int fixedPosCount = fixedPositionalArgs.Count;
            for (int i = fixedPosCount; i < targetSymbol.Parameters.Count; i++)
            {
                var p = targetSymbol.Parameters[i];
                if (fixedKwargNames.Contains(p.Name))
                    continue;
                remainingParamNames.Add(p.Name);
            }
        }

        while (remainingParamNames.Count < resultType.ParameterTypes.Count)
        {
            remainingParamNames.Add($"__partial_arg{remainingParamNames.Count}");
        }

        var lambdaParams = new List<ParameterSyntax>(resultType.ParameterTypes.Count);
        var lambdaParamIdentifiers = new List<string>(resultType.ParameterTypes.Count);
        for (int i = 0; i < resultType.ParameterTypes.Count; i++)
        {
            var paramTypeSyntax = _typeMapper.MapSemanticType(resultType.ParameterTypes[i]);
            var paramName = NameMangler.ToCamelCase(remainingParamNames[i]);
            lambdaParams.Add(Parameter(Identifier(paramName)).WithType(paramTypeSyntax));
            lambdaParamIdentifiers.Add(paramName);
        }

        // Build the call inside the lambda body:
        //   target(fixed_positional..., remaining_positional..., fixedKw1: val1, ...)
        var bodyArgs = new List<ArgumentSyntax>();
        foreach (var fa in fixedPositionalArgs)
        {
            bodyArgs.Add(Argument(fa));
        }
        foreach (var lpn in lambdaParamIdentifiers)
        {
            bodyArgs.Add(Argument(IdentifierName(lpn)));
        }
        foreach (var (kwName, kwValue) in fixedKwargs)
        {
            var csharpName = NameMangler.ToCamelCase(kwName);
            bodyArgs.Add(Argument(kwValue)
                .WithNameColon(NameColon(IdentifierName(csharpName))));
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
