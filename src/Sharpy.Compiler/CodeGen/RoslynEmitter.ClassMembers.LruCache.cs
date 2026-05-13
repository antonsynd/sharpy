using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Memoization codegen for <c>@functools.lru_cache</c> and <c>@functools.cache</c>.
///
/// A decorated function <c>def fn(args) -> R</c> emits three members in place of one:
/// <list type="bullet">
///   <item>A private (static or instance) field
///         <c>__FnCache</c> of type <c>global::Sharpy.LruCache&lt;TKey, R&gt;</c>.</item>
///   <item>A private method <c>__Fn</c> holding the original body.</item>
///   <item>A public wrapper method <c>Fn</c> that delegates to
///         <c>__FnCache.GetOrAdd(key, _key =&gt; __Fn(...))</c>.</item>
/// </list>
/// The key type is the single parameter type for arity-1 functions, a
/// <c>ValueTuple</c> for arity-N, and <c>int</c> (with key <c>0</c>) for arity-0.
/// Two additional public delegate methods (<c>FnCacheInfo</c>, <c>FnCacheClear</c>)
/// expose the underlying <see cref="Sharpy.LruCache{TKey, TResult}.CacheInfo"/> and
/// <see cref="Sharpy.LruCache{TKey, TResult}.CacheClear"/>.
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Returns true when the function carries a <c>@lru_cache</c> or <c>@cache</c>
    /// decorator. CodeGen detects this directly rather than via FunctionSymbol so the
    /// decision is independent of whether semantic analysis materialized the symbol.
    /// </summary>
    private static bool IsLruCacheDecorated(FunctionDef func)
    {
        for (int i = 0; i < func.Decorators.Length; i++)
        {
            var name = func.Decorators[i].Name;
            if (name == DecoratorNames.LruCache || name == DecoratorNames.Cache)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Extracts the bounded maxsize from <c>@lru_cache(maxsize=N)</c> / positional form,
    /// or <c>null</c> for the unbounded <c>@cache</c> and <c>@lru_cache(maxsize=None)</c>
    /// forms. Mirrors <c>TypeChecker.ExtractCacheConfig</c>; the validator has already
    /// rejected malformed shapes, so the fall-through default is safe.
    /// </summary>
    private static int? GetLruCacheMaxSize(FunctionDef func)
    {
        if (func.Decorators.Any(d => d.Name == DecoratorNames.Cache))
            return null;

        var lru = func.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.LruCache);
        if (lru == null)
            return 128;

        if (lru.Arguments.Length == 0 && lru.KeywordArguments.Length == 0)
            return 128;

        Expression? value =
            lru.KeywordArguments.FirstOrDefault(kw => kw.Name == "maxsize")?.Value
            ?? (lru.Arguments.Length == 1 ? lru.Arguments[0] : null);

        return value switch
        {
            NoneLiteral => null,
            IntegerLiteral intLit when int.TryParse(
                intLit.Value.Replace("_", "", System.StringComparison.Ordinal),
                out var n) => n,
            _ => 128,
        };
    }

    /// <summary>
    /// Generates the cache field, the renamed original method, the public wrapper, and
    /// the <c>*CacheInfo</c>/<c>*CacheClear</c> accessors for a memoized function.
    /// </summary>
    /// <param name="func">The decorated function definition. Must already pass
    /// <see cref="IsLruCacheDecorated"/>.</param>
    /// <param name="isModuleLevel">True for module-level (top-level) functions; false for
    /// class/struct members. Module-level functions always emit static caches; class
    /// members emit static when the method has no <c>self</c> parameter and instance
    /// caches otherwise.</param>
    private List<MemberDeclarationSyntax> GenerateLruCacheWrappedFunction(
        FunctionDef func,
        bool isModuleLevel)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Cache-related codegen is incompatible with several method shapes. Report once and
        // fall through to the standard emission path to keep diagnostics actionable.
        if (func.IsAsync)
        {
            _context.AddError(
                $"@lru_cache cannot be combined with 'async' on '{func.Name}'. Memoization wrappers do not yet support async functions.",
                DiagnosticCodes.CodeGen.EmitError,
                func.LineStart,
                func.ColumnStart);
            members.Add(isModuleLevel
                ? GenerateFunctionDeclaration(func)
                : GenerateClassMethod(func));
            return members;
        }

        bool isGenerator = _context.SemanticInfo?.IsGenerator(func) == true;
        if (isGenerator)
        {
            _context.AddError(
                $"@lru_cache cannot be combined with generators on '{func.Name}'. Memoization requires a single materialized return value.",
                DiagnosticCodes.CodeGen.EmitError,
                func.LineStart,
                func.ColumnStart);
            members.Add(isModuleLevel
                ? GenerateFunctionDeclaration(func)
                : GenerateClassMethod(func));
            return members;
        }

        // Decide static vs instance and figure out the cached parameters (excluding 'self').
        bool hasSelf = !isModuleLevel && func.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, System.StringComparison.OrdinalIgnoreCase));
        bool isStatic = isModuleLevel || !hasSelf;

        // Skip 'self' from the cache key parameters; the cache is per-instance so 'self'
        // identity is implicit in the field.
        var cachedParams = func.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Compute names. The wrapper keeps the user-facing PascalCase name; the renamed
        // body uses a double-underscore prefix to prevent accidental external dispatch.
        var publicName = NameMangler.Transform(func.Name, NameContext.Method);
        var privateName = "__" + publicName;
        var cacheFieldName = "__" + publicName + "Cache";

        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Build the key type. ValueTuple<T1, ..., Tn> for arity-N, the single param type
        // for arity-1, and 'int' as a degenerate key for arity-0 functions.
        TypeSyntax keyType;
        if (cachedParams.Count == 0)
        {
            keyType = PredefinedType(Token(SyntaxKind.IntKeyword));
        }
        else if (cachedParams.Count == 1)
        {
            keyType = cachedParams[0].Type != null
                ? _typeMapper.MapType(cachedParams[0].Type)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }
        else
        {
            var elementTypes = cachedParams.Select(p => p.Type != null
                ? _typeMapper.MapType(p.Type)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword))).ToArray();
            keyType = TupleType(SeparatedList(
                elementTypes.Select(t => TupleElement(t))));
        }

        // global::Sharpy.LruCache<TKey, TResult>
        var lruCacheType = QualifiedName(
            MakeGlobalQualifiedName("Sharpy"),
            GenericName(Identifier("LruCache"))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { keyType, returnType }))));

        var maxSize = GetLruCacheMaxSize(func);

        members.Add(GenerateLruCacheField(cacheFieldName, lruCacheType, maxSize, isStatic));
        members.Add(GenerateRenamedOriginalMethod(func, privateName, isStatic));
        members.Add(GenerateLruCacheWrapper(func, publicName, privateName, cacheFieldName, returnType,
            cachedParams, isStatic));
        members.AddRange(GenerateLruCacheAccessors(publicName, cacheFieldName, isStatic));

        return members;
    }

    /// <summary>
    /// Emits a private (static or instance) field
    /// <c>(static) readonly global::Sharpy.LruCache&lt;TKey, R&gt; __NameCache = new(maxSize);</c>
    /// </summary>
    private static FieldDeclarationSyntax GenerateLruCacheField(
        string fieldName,
        TypeSyntax cacheType,
        int? maxSize,
        bool isStatic)
    {
        ExpressionSyntax maxSizeExpr = maxSize.HasValue
            ? LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(maxSize.Value))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        var creation = ObjectCreationExpression(cacheType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(maxSizeExpr))));

        var declarator = VariableDeclarator(Identifier(fieldName))
            .WithInitializer(EqualsValueClause(creation));

        var modifiers = new List<SyntaxToken> { Token(SyntaxKind.PrivateKeyword) };
        if (isStatic)
            modifiers.Add(Token(SyntaxKind.StaticKeyword));
        modifiers.Add(Token(SyntaxKind.ReadOnlyKeyword));

        return FieldDeclaration(
                VariableDeclaration(cacheType, SingletonSeparatedList(declarator)))
            .WithModifiers(TokenList(modifiers));
    }

    /// <summary>
    /// Emits the original function body as a private method with a mangled name. For
    /// class members, the static-ness is set explicitly here to override the public
    /// wrapper's inferred modifiers (e.g., when the user wrote a static method).
    /// </summary>
    private MethodDeclarationSyntax GenerateRenamedOriginalMethod(
        FunctionDef func,
        string privateName,
        bool isStatic)
    {
        // Create a copy of the FunctionDef whose Decorators stripped of @lru_cache/@cache
        // and whose Name is the private mangled form. We mutate via 'with' for the record
        // because the existing generation pipeline reads decorators and names directly.
        var filteredDecorators = func.Decorators
            .Where(d => d.Name != DecoratorNames.LruCache && d.Name != DecoratorNames.Cache)
            .ToImmutableArray();

        // Add an explicit private access decorator so GenerateMethodModifiers emits 'private'.
        // The Decorator.Name property is computed from QualifiedParts, so the parts must
        // contain the access modifier name.
        var privateDecorator = new Decorator
        {
            QualifiedParts = ImmutableArray.Create(DecoratorNames.Private),
        };
        var newDecorators = filteredDecorators.Insert(0, privateDecorator);

        // The renamed private method needs a NameContext.Method-style identifier exactly.
        // We pass the already-PascalCased private name through by stuffing it into Name and
        // letting GenerateClassMethod/GenerateFunctionDeclaration mangle it; PascalCase
        // names are idempotent under NameMangler.Transform(.Method).
        var renamed = func with
        {
            Name = privateName,
            Decorators = newDecorators,
        };

        MethodDeclarationSyntax method;
        if (_currentTypeSymbol != null)
        {
            method = GenerateClassMethod(renamed);
        }
        else
        {
            method = GenerateFunctionDeclaration(renamed);
        }

        // Strip any accidental public/internal/protected access we may have inherited
        // from the default name-based access modifier, and ensure static when expected.
        var tokens = method.Modifiers
            .Where(t => !t.IsKind(SyntaxKind.PublicKeyword)
                     && !t.IsKind(SyntaxKind.InternalKeyword)
                     && !t.IsKind(SyntaxKind.ProtectedKeyword))
            .ToList();
        if (!tokens.Any(t => t.IsKind(SyntaxKind.PrivateKeyword)))
            tokens.Insert(0, Token(SyntaxKind.PrivateKeyword));
        if (isStatic && !tokens.Any(t => t.IsKind(SyntaxKind.StaticKeyword)))
            tokens.Add(Token(SyntaxKind.StaticKeyword));
        if (!isStatic)
            tokens = tokens.Where(t => !t.IsKind(SyntaxKind.StaticKeyword)).ToList();

        return method.WithModifiers(TokenList(tokens));
    }

    /// <summary>
    /// Emits the user-facing public wrapper:
    /// <c>public (static)? R Name(params) =&gt; __NameCache.GetOrAdd(key, _key =&gt; __Name(args));</c>
    /// </summary>
    private MethodDeclarationSyntax GenerateLruCacheWrapper(
        FunctionDef func,
        string publicName,
        string privateName,
        string cacheFieldName,
        TypeSyntax returnType,
        List<Parameter> cachedParams,
        bool isStatic)
    {
        // Generate the public wrapper's parameter list (reuses the existing parameter
        // generation so default values, ref/out modifiers, and variadic markers carry over).
        var orderedParams = ReorderParametersForCSharp(cachedParams);
        var parameters = orderedParams.Select(GenerateParameter).ToArray();

        // Build the cache key expression.
        const string keyParamName = "__key";
        ExpressionSyntax keyExpr;
        if (cachedParams.Count == 0)
        {
            keyExpr = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
        }
        else if (cachedParams.Count == 1)
        {
            keyExpr = IdentifierName(NameMangler.Transform(cachedParams[0].Name, NameContext.Parameter));
        }
        else
        {
            keyExpr = TupleExpression(SeparatedList(
                cachedParams.Select(p => Argument(
                    IdentifierName(NameMangler.Transform(p.Name, NameContext.Parameter))))));
        }

        // Build the factory body: _key => __Name(args).
        // For arity-1 we pass the lambda parameter directly; for arity-N we destructure via .ItemK.
        var factoryArgs = new List<ArgumentSyntax>();
        if (cachedParams.Count == 0)
        {
            // Zero-arg: call with no arguments and ignore the key.
        }
        else if (cachedParams.Count == 1)
        {
            factoryArgs.Add(Argument(IdentifierName(keyParamName)));
        }
        else
        {
            for (int i = 0; i < cachedParams.Count; i++)
            {
                factoryArgs.Add(Argument(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(keyParamName),
                    IdentifierName("Item" + (i + 1)))));
            }
        }

        // Receiver for the private method: 'this' is implicit, but for static methods
        // we can call by name directly.
        var privateCall = InvocationExpression(IdentifierName(privateName))
            .WithArgumentList(ArgumentList(SeparatedList(factoryArgs)));

        var factory = SimpleLambdaExpression(
            Parameter(Identifier(keyParamName)),
            privateCall);

        // Cache field access: static methods use 'ClassName.__NameCache' implicitly via
        // an unqualified IdentifierName (works for both static and instance contexts since
        // the field is declared in the same type).
        ExpressionSyntax cacheAccess = IdentifierName(cacheFieldName);

        var getOrAdd = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                cacheAccess,
                IdentifierName("GetOrAdd")))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(keyExpr),
                Argument(factory),
            })));

        // Modifiers: public + (static when isStatic). Mirror the original method's
        // virtual/override/abstract intent — but @lru_cache on virtual/override methods
        // is unusual and not supported; emit a plain public wrapper.
        var modifiers = new List<SyntaxToken> { Token(SyntaxKind.PublicKeyword) };
        if (isStatic)
            modifiers.Add(Token(SyntaxKind.StaticKeyword));

        var method = MethodDeclaration(returnType, publicName)
            .WithModifiers(TokenList(modifiers))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithExpressionBody(ArrowExpressionClause(getOrAdd))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        // Add type parameters if generic (preserve original signature on the wrapper).
        if (func.TypeParameters.Length > 0)
        {
            var typeParams = func.TypeParameters.Select(GenerateTypeParameterSyntax).ToArray();
            method = method
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
        }

        // Preserve the docstring on the user-visible wrapper.
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
    }

    /// <summary>
    /// Emits delegating accessors that match Python's <c>fn.cache_info()</c> and
    /// <c>fn.cache_clear()</c>. Since Sharpy functions are not first-class objects
    /// with attached attributes, the accessors are exposed as sibling methods
    /// (<c>NameCacheInfo</c> / <c>NameCacheClear</c>) on the same class.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateLruCacheAccessors(
        string publicName,
        string cacheFieldName,
        bool isStatic)
    {
        // global::Sharpy.CacheInfo return type
        var cacheInfoType = QualifiedName(
            MakeGlobalQualifiedName("Sharpy"),
            IdentifierName("CacheInfo"));

        var modifiers = new List<SyntaxToken> { Token(SyntaxKind.PublicKeyword) };
        if (isStatic)
            modifiers.Add(Token(SyntaxKind.StaticKeyword));

        // public (static)? CacheInfo NameCacheInfo() => __NameCache.CacheInfo();
        var cacheInfoMethod = MethodDeclaration(cacheInfoType, publicName + "CacheInfo")
            .WithModifiers(TokenList(modifiers))
            .WithParameterList(ParameterList())
            .WithExpressionBody(ArrowExpressionClause(InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(cacheFieldName),
                    IdentifierName("CacheInfo")))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        // public (static)? void NameCacheClear() => __NameCache.CacheClear();
        var cacheClearMethod = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                publicName + "CacheClear")
            .WithModifiers(TokenList(modifiers))
            .WithParameterList(ParameterList())
            .WithExpressionBody(ArrowExpressionClause(InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(cacheFieldName),
                    IdentifierName("CacheClear")))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        yield return cacheInfoMethod;
        yield return cacheClearMethod;
    }
}
