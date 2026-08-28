using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Method generation
/// </summary>
internal partial class RoslynEmitter
{
    private MethodDeclarationSyntax GenerateClassMethod(FunctionDef func)
    {
        // Clear declared variables and version tracking for new method scope
        ResetMethodScope(func);

        // Check if this method is a generator and/or async
        using var _gen = SetGeneratorScope(_context.Ir.IsGenerator(func));
        using var _async = SetAsyncScope(func.IsAsync);

        // Track @test context so assert statements in the body emit xUnit assertions
        // (instead of System.Diagnostics.Debug.Assert). Matches @test and its
        // sub-decorators (@test.parametrize, @test.skip, @test.skip_if).
        bool hasTestDecorator = func.Decorators.Any(IsTestDecorator);
        bool savedIsInTestFunction = _isInTestFunction;
        if (hasTestDecorator)
        {
            _isInTestFunction = true;
        }

        // Pre-scan the method body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.

        // For class methods, use DunderMapping for dunders, NameCasing for regular names
        var mangledName = DunderMapping.ResolveCSharpName(func.Name)
            ?? NameCasing.ResolveMethod(func.Name, func.IsNameBacktickEscaped);

        // Determine return type from annotation or infer void
        // Default to void if no return type specified
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Use ProtocolRegistry to determine return types for protocol dunders.
        // For protocols with an alternate signature (e.g., __exit__ accepts 1 or 4 params),
        // select the return type matching the form actually declared.
        var protocol = ProtocolRegistry.GetProtocol(func.Name);
        if (protocol != null)
        {
            var effectiveReturnType = protocol.ExpectedReturnType;
            if (protocol.AlternateParamCount.HasValue
                && func.Parameters.Length == protocol.AlternateParamCount.Value
                && protocol.AlternateReturnType != null)
            {
                effectiveReturnType = protocol.AlternateReturnType;
            }

            if (effectiveReturnType != null)
            {
                returnType = effectiveReturnType switch
                {
                    BuiltinNames.Str or "string" => PredefinedType(Token(SyntaxKind.StringKeyword)),
                    BuiltinNames.Int => PredefinedType(Token(SyntaxKind.IntKeyword)),
                    BuiltinNames.Bool => PredefinedType(Token(SyntaxKind.BoolKeyword)),
                    BuiltinNames.None or "void" => PredefinedType(Token(SyntaxKind.VoidKeyword)),
                    _ => func.ReturnType != null ? _typeMapper.MapType(func.ReturnType) : returnType
                };
            }
        }

        // For non-dunder generator methods, wrap return type T in IEnumerable<T> or IAsyncEnumerable<T>
        bool isAsync = func.IsAsync;
        if (_isCurrentMethodGenerator && !DunderMapping.IsDunderMethod(func.Name))
        {
            returnType = isAsync ? WrapInIAsyncEnumerable(returnType) : WrapInIEnumerable(returnType);
        }
        else if (isAsync)
        {
            // For non-generator async methods, wrap return type in Task<T> or Task.
            // An explicit `-> None` annotation maps to `void`, which must become bare
            // `Task` (not `Task<void>` — that is invalid C#).
            if (func.ReturnType != null && !IsVoidType(returnType))
            {
                returnType = WrapInTask(returnType);
            }
            else
            {
                returnType = TaskType();
            }
        }

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiers(func.Name, func.Decorators);

        // @test methods must be public (xUnit requirement). Strip any private/protected/internal
        // modifiers and add public if not present.
        if (hasTestDecorator)
        {
            modifiers = TokenList(modifiers.Where(m =>
                !m.IsKind(SyntaxKind.PrivateKeyword)
                && !m.IsKind(SyntaxKind.ProtectedKeyword)
                && !m.IsKind(SyntaxKind.InternalKeyword)));
            if (!modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            {
                modifiers = TokenList(new[] { Token(SyntaxKind.PublicKeyword) }.Concat(modifiers));
            }
        }

        // In TestCase subclasses, setup/teardown are helper methods invoked by the
        // synthesized constructor/Dispose. They should be private (xUnit only invokes
        // the constructor/Dispose pair, never setup/teardown directly).
        if (_isInTestCaseClass && (func.Name == "setup" || func.Name == "teardown"))
        {
            modifiers = TokenList(modifiers.Where(m =>
                !m.IsKind(SyntaxKind.PublicKeyword)
                && !m.IsKind(SyntaxKind.ProtectedKeyword)
                && !m.IsKind(SyntaxKind.InternalKeyword)));
            if (!modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
            {
                modifiers = TokenList(new[] { Token(SyntaxKind.PrivateKeyword) }.Concat(modifiers));
            }
        }

        // Add override keyword for methods that override Object methods
        // Uses the protocol variable already fetched above, plus special handling for operator dunders
        var shouldAddOverride = protocol?.ClrMethodName is "ToString" or "GetHashCode"
            // __eq__ only generates override when parameter type is object
            || (func.Name == DunderNames.Eq && IsEqualsObjectOverload(func));

        if (shouldAddOverride && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.OverrideKeyword));
            // virtual+override conflict is resolved by ResolveModifierConflicts() below
        }

        // Resolve the method symbol for hierarchy-fact reads — shared by the override,
        // strip-override, and implements-interface blocks below.
        var methodSymbol = _currentTypeSymbol?.Methods.FirstOrDefault(m =>
            m.Name == func.Name && m.DeclarationLine == func.LineStart);

        // #1122: add 'override' for a method that overrides an abstract/virtual member of a
        // CLR-backed base type.
        if (!modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && methodSymbol?.CodeGenInfo?.OverridesClrBaseMember == true)
        {
            modifiers = modifiers.Add(Token(SyntaxKind.OverrideKeyword));
        }

        // In C#, you cannot use 'override' for interface methods (default or abstract).
        // If @override targets an interface method (not a base class), remove the override keyword.
        // The decision is made in semantic analysis and frozen onto CodeGenInfo (#1519).
        if (modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && methodSymbol?.CodeGenInfo?.StripsOverrideKeyword == true)
        {
            modifiers = TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.OverrideKeyword)));
        }

        // Add virtual keyword for non-object __eq__ in class context (for IEquatable<T> dispatch)
        // Structs can't have virtual methods, so skip for struct types
        if (func.Name == DunderNames.Eq && !IsEqualsObjectOverload(func)
            && _currentTypeSymbol?.TypeKind == Semantic.TypeKind.Class
            && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.VirtualKeyword));
        }

        // Add virtual keyword for methods that implement an interface method in a non-sealed class.
        // Without virtual, subclasses cannot use @override on these methods.
        // The implements-interface fact is frozen onto CodeGenInfo (#1519).
        if (_currentTypeSymbol != null
            && _currentTypeSymbol.TypeKind == Semantic.TypeKind.Class
            && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword))
            && !func.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Final)
            && methodSymbol?.CodeGenInfo?.ImplementsInterfaceMethod == true)
        {
            modifiers = modifiers.Add(Token(SyntaxKind.VirtualKeyword));
        }

        // Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
        // @static decorator is valid but OPTIONAL/redundant
        bool hasSelfParameter = func.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

        if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        // Generate parameters with type annotations, skipping 'self' and 'cls' parameters
        // Reorder for C# compliance (required before optional, params last)
        var filteredMethodParams = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase));
        var orderedMethodParams = ReorderParametersForCSharp(filteredMethodParams);
        var parameters = orderedMethodParams
            .Select(GenerateParameter)
            .ToArray();

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(param.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = ParameterCSharpName(param);
            if (param.IsLateBound)
            {
                // The C# parameter is named `y__lb`; the preamble local is named `y`
            }
            else
            {
            }
            // Also track in version map so assignments to parameters work correctly
            var baseName = ParameterCSharpName(param);
        }

        var methodSymbolByName = methodSymbol
            ?? _currentTypeSymbol?.Methods.FirstOrDefault(m => m.Name == func.Name);
        bool isAbstract = methodSymbolByName?.IsAbstract ?? false;

        // If method is abstract, ensure it has the abstract modifier in the token list
        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

        // Final modifier conflict resolution — ensure no illegal C# combinations
        modifiers = ResolveModifierConflicts(modifiers);

        // Generate method declaration
        var method = MethodDeclaration(returnType, EscapedIdentifier(mangledName))
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)));

        // Add C# attributes from unknown decorators
        var methodAttributes = GenerateAttributeListsFromDecorators(func.Decorators);
        if (methodAttributes.Count > 0)
        {
            method = method.WithAttributeLists(methodAttributes);
        }

        if (isAsync)
        {
            method = method.AddModifiers(Token(SyntaxKind.AsyncKeyword));
        }

        // Abstract methods must not have a body in C#
        if (isAbstract)
        {
            method = method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            // Generate method body for concrete methods
            var preamble = GenerateLateBoundPreamble(func.Parameters);
            var userStatements = GenerateSuite(func.Body);

            // For __eq__ implementing IEquatable<T> on classes, prepend null guard:
            //   if (other is null) return false;
            // This satisfies the IEquatable<T> contract (Equals(null) must return false, not throw).
            // Structs don't need this because value type parameters can't be null.
            if (func.Name == DunderNames.Eq && !IsEqualsObjectOverload(func)
                && _currentTypeSymbol?.TypeKind == Semantic.TypeKind.Class)
            {
                var otherParam = func.Parameters
                    .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

                if (otherParam != null)
                {
                    var paramName = ParameterCSharpName(otherParam);
                    var nullGuard = IfStatement(
                        IsPatternExpression(
                            IdentifierName(paramName),
                            ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression))),
                        ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));

                    var body = AttachLineDirectiveToBlock(
                        Block(preamble.Concat(new StatementSyntax[] { nullGuard }).Concat(userStatements)), func.LineStart);
                    method = method.WithBody(body);
                }
                else
                {
                    var body = AttachLineDirectiveToBlock(
                        Block(preamble.Concat(userStatements)), func.LineStart);
                    method = method.WithBody(body);
                }
            }
            else
            {
                var body = AttachLineDirectiveToBlock(
                    Block(preamble.Concat(userStatements)), func.LineStart);
                method = method.WithBody(body);
            }
        }

        // Generic methods: emit the <T...> type-parameter list and any constraint clauses.
        // Mirrors GenerateFunctionDeclaration; helpers live in RoslynEmitter.TypeDeclarations.cs.
        if (func.TypeParameters.Length > 0)
        {
            var typeParams = func.TypeParameters
                .Select(GenerateMethodTypeParameterSyntax)
                .ToArray();
            method = method
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        _isInTestFunction = savedIsInTestFunction;

        return method;
    }

    /// <summary>
    /// Ensures that synthesized protocol properties (e.g., IsTrue, Count) have the virtual
    /// modifier in non-sealed classes so subclasses can use @override. Skips adding virtual
    /// when abstract, override, sealed, or @final is already present, since abstract and
    /// virtual are mutually exclusive in C#.
    /// </summary>
    private SyntaxTokenList EnsureVirtualForProtocolProperty(
        SyntaxTokenList modifiers, bool isAbstract, FunctionDef func)
    {
        if (!isAbstract
            && _currentTypeSymbol != null
            && _currentTypeSymbol.TypeKind == Semantic.TypeKind.Class
            && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword))
            && !func.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Final))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.VirtualKeyword));
        }

        return modifiers;
    }

    /// <summary>
    /// Generates a read-only IsTrue property for __bool__ to satisfy IBoolConvertible.
    /// The user's __bool__ body becomes the getter body.
    /// </summary>
    private PropertyDeclarationSyntax GenerateBoolProperty(FunctionDef func)
    {
        ResetMethodScope();

        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var boolPropSymbol = _currentTypeSymbol?.Methods.FirstOrDefault(m => m.Name == func.Name);
        bool isAbstract = boolPropSymbol?.IsAbstract ?? false;

        // Apply modifiers from decorators (handles public/virtual/override/abstract)
        var modifiers = GenerateMethodModifiers(func.Name, func.Decorators);

        // Ensure abstract modifier is present for abstract properties
        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

        modifiers = EnsureVirtualForProtocolProperty(modifiers, isAbstract, func);

        // Build getter: abstract properties use semicolon, concrete use body
        AccessorDeclarationSyntax getter;
        if (isAbstract)
        {
            getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            var bodyStatements = GenerateSuite(func.Body);
            getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(bodyStatements));
        }

        var property = PropertyDeclaration(returnType, ProtocolConstants.IsTrue)
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(SingletonList(getter)));

        if (!string.IsNullOrEmpty(func.DocString))
        {
            property = property.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return property;
    }

    /// <summary>
    /// Generates a read-only Count property for __len__ to satisfy ISized.
    /// The user's __len__ body becomes the getter body.
    /// </summary>
    private PropertyDeclarationSyntax GenerateLenProperty(FunctionDef func)
    {
        // Clear declared variables for new scope
        ResetMethodScope();


        var returnType = PredefinedType(Token(SyntaxKind.IntKeyword));

        var lenPropSymbol = _currentTypeSymbol?.Methods.FirstOrDefault(m => m.Name == func.Name);
        bool isAbstract = lenPropSymbol?.IsAbstract ?? false;

        // Apply modifiers from decorators (handles public/virtual/override/abstract)
        var modifiers = GenerateMethodModifiers(func.Name, func.Decorators);

        // Ensure abstract modifier is present for abstract properties
        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

        modifiers = EnsureVirtualForProtocolProperty(modifiers, isAbstract, func);

        // Build getter: abstract properties use semicolon, concrete use body
        AccessorDeclarationSyntax getter;
        if (isAbstract)
        {
            getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            // Generate getter body from __len__ body
            var bodyStatements = GenerateSuite(func.Body);
            getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(bodyStatements));
        }

        var property = PropertyDeclaration(returnType, ProtocolConstants.Count)
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(SingletonList(getter)));

        if (!string.IsNullOrEmpty(func.DocString))
        {
            property = property.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return property;
    }

    private SyntaxTokenList GenerateMethodModifiers(string memberName, IReadOnlyList<Decorator> decorators)
    {
        var tokens = new List<SyntaxToken>();

        // Check for access modifiers
        bool hasAccessModifier = false;
        foreach (var decorator in decorators)
        {
            if (decorator.IsBracketAttribute)
                continue;
            switch (decorator.Name)
            {
                case DecoratorNames.Private:
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    hasAccessModifier = true;
                    break;
                case DecoratorNames.Protected:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    hasAccessModifier = true;
                    break;
                case DecoratorNames.Internal:
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    hasAccessModifier = true;
                    break;
                case DecoratorNames.Public:
                    tokens.Add(Token(SyntaxKind.PublicKeyword));
                    hasAccessModifier = true;
                    break;
            }
        }

        // Default access modifier based on name convention when no explicit decorator
        if (!hasAccessModifier)
        {
            tokens.Add(Token(GetAccessModifierFromNameConvention(memberName)));
        }

        // Check for other modifiers
        foreach (var decorator in decorators)
        {
            if (decorator.IsBracketAttribute)
                continue;
            switch (decorator.Name)
            {
                case DecoratorNames.Static:
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case DecoratorNames.Abstract:
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case DecoratorNames.Virtual:
                    tokens.Add(Token(SyntaxKind.VirtualKeyword));
                    break;
                case DecoratorNames.Override:
                    tokens.Add(Token(SyntaxKind.OverrideKeyword));
                    break;
                case DecoratorNames.Final:
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                    break;
            }
        }

        return TokenList(tokens);
    }

    /// <summary>
    /// Resolves illegal C# modifier combinations that can arise when user decorators
    /// interact with auto-generated modifiers (e.g., @virtual on __str__ which auto-adds override).
    /// </summary>
    private static SyntaxTokenList ResolveModifierConflicts(SyntaxTokenList modifiers)
    {
        bool hasVirtual = modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword));
        bool hasOverride = modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword));
        bool hasAbstract = modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword));
        bool hasStatic = modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

        // virtual + override → strip virtual (override implies virtual in C#)
        // virtual + abstract → strip virtual (abstract implies virtual in C#)
        if (hasVirtual && (hasOverride || hasAbstract))
        {
            modifiers = TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.VirtualKeyword)));
        }

        // static + virtual/override/abstract → strip virtual/override/abstract
        if (hasStatic && (hasVirtual || hasOverride || hasAbstract))
        {
            modifiers = TokenList(modifiers.Where(m =>
                !m.IsKind(SyntaxKind.VirtualKeyword)
                && !m.IsKind(SyntaxKind.OverrideKeyword)
                && !m.IsKind(SyntaxKind.AbstractKeyword)));
        }

        return modifiers;
    }


    private MethodDeclarationSyntax GenerateInterfaceMethod(FunctionDef func)
    {
        var mangledName = DunderMapping.ResolveCSharpName(func.Name)
            ?? NameCasing.ResolveMethod(func.Name, func.IsNameBacktickEscaped);

        // Determine return type from annotation or infer void
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Interface methods skip 'self' parameter
        // Reorder for C# compliance (required before optional, params last)
        var filteredInterfaceParams = func.Parameters
            .Where(p => p.Name != PythonNames.Self);
        var orderedInterfaceParams = ReorderParametersForCSharp(filteredInterfaceParams);
        var parameters = orderedInterfaceParams
            .Select(GenerateParameter)
            .ToArray();

        var method = MethodDeclaration(returnType, EscapedIdentifier(mangledName))
            .WithParameterList(ParameterList(SeparatedList(parameters)));

        // Check if this is an abstract method (body is single ellipsis or pass)
        bool isAbstract = AstHelper.IsAbstractStubBody(func.Body);

        if (isAbstract)
        {
            // Abstract interface method: no body, just semicolon
            method = method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            // Default interface method: emit the full body
            ResetMethodScope();

            // Track parameters as declared variables (skip self)
            foreach (var param in func.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = ParameterCSharpName(param);
                var baseName = ParameterCSharpName(param);
            }

            var bodyStatements = GenerateSuite(func.Body);
            method = method.WithBody(AttachLineDirectiveToBlock(
                Block(bodyStatements), func.LineStart));
        }

        // Generic interface methods: emit the <T...> type-parameter list and constraints.
        if (func.TypeParameters.Length > 0)
        {
            var typeParams = func.TypeParameters
                .Select(GenerateMethodTypeParameterSyntax)
                .ToArray();
            method = method
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
    }

}
