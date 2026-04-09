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
        ResetMethodScope();

        // Check if this method is a generator and/or async
        using var _gen = SetGeneratorScope(_context.SemanticInfo?.IsGenerator(func) == true);
        using var _async = SetAsyncScope(func.IsAsync);

        // Pre-scan the method body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // For class methods, use DunderMapping for dunders, NameMangler for regular names
        var mangledName = DunderMapping.ResolveCSharpName(func.Name)
            ?? NameMangler.Transform(func.Name, NameContext.Method);

        // Determine return type from annotation or infer void
        // Default to void if no return type specified
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Use ProtocolRegistry to determine return types for protocol dunders
        var protocol = ProtocolRegistry.GetProtocol(func.Name);
        if (protocol != null && protocol.ExpectedReturnType != null)
        {
            returnType = protocol.ExpectedReturnType switch
            {
                BuiltinNames.Str or "string" => PredefinedType(Token(SyntaxKind.StringKeyword)),
                BuiltinNames.Int => PredefinedType(Token(SyntaxKind.IntKeyword)),
                BuiltinNames.Bool => PredefinedType(Token(SyntaxKind.BoolKeyword)),
                BuiltinNames.None or "void" => PredefinedType(Token(SyntaxKind.VoidKeyword)),
                _ => func.ReturnType != null ? _typeMapper.MapType(func.ReturnType) : returnType
            };
        }

        // For non-dunder generator methods, wrap return type T in IEnumerable<T> or IAsyncEnumerable<T>
        bool isAsync = func.IsAsync;
        if (_isCurrentMethodGenerator && !DunderMapping.IsDunderMethod(func.Name))
        {
            returnType = isAsync ? WrapInIAsyncEnumerable(returnType) : WrapInIEnumerable(returnType);
        }
        else if (isAsync)
        {
            // For non-generator async methods, wrap return type in Task<T> or Task
            if (func.ReturnType != null)
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

        // In C#, you cannot use 'override' for interface methods (default or abstract).
        // If @override targets an interface method (not a base class), remove the override keyword.
        if (modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && _currentTypeSymbol != null
            && ShouldStripOverrideKeyword(func.Name))
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
        if (_currentTypeSymbol != null
            && _currentTypeSymbol.TypeKind == Semantic.TypeKind.Class
            && !modifiers.Any(m => m.IsKind(SyntaxKind.VirtualKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))
            && !modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword))
            && !func.Decorators.Any(d => d.Name == DecoratorNames.Final)
            && ImplementsInterfaceMethod(func.Name))
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
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            // Also track in version map so assignments to parameters work correctly
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Check if this is an abstract method:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an abstract class AND has ellipsis body (implicit abstract)
        bool hasAbstractDecorator = func.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = func.Body.Length == 1
            && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

        bool isAbstract = hasAbstractDecorator || (_isInAbstractClass && hasEllipsisBody);

        // If method is abstract, ensure it has the abstract modifier in the token list
        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

        // Final modifier conflict resolution — ensure no illegal C# combinations
        modifiers = ResolveModifierConflicts(modifiers);

        // Generate method declaration
        var method = MethodDeclaration(returnType, mangledName)
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
            var userStatements = func.Body.SelectMany(GenerateBodyStatements);

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
                    var paramName = NameMangler.Transform(otherParam.Name, NameContext.Parameter);
                    var nullGuard = IfStatement(
                        IsPatternExpression(
                            IdentifierName(paramName),
                            ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression))),
                        ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));

                    var body = Block(new StatementSyntax[] { nullGuard }.Concat(userStatements));
                    method = method.WithBody(body);
                }
                else
                {
                    var body = Block(userStatements);
                    method = method.WithBody(body);
                }
            }
            else
            {
                var body = Block(userStatements);
                method = method.WithBody(body);
            }
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

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
            && !func.Decorators.Any(d => d.Name == DecoratorNames.Final))
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
        CollectSourceVariableNames(func.Body);

        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        // Check if this is an abstract property:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an abstract class AND has ellipsis body (implicit abstract)
        bool hasAbstractDecorator = func.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = func.Body.Length == 1
            && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
        bool isAbstract = hasAbstractDecorator || (_isInAbstractClass && hasEllipsisBody);

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
            var bodyStatements = func.Body
                .SelectMany(GenerateBodyStatements);
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

        CollectSourceVariableNames(func.Body);

        var returnType = PredefinedType(Token(SyntaxKind.IntKeyword));

        // Check if this is an abstract property:
        // 1. Has @abstract decorator explicitly, OR
        // 2. Is in an abstract class AND has ellipsis body (implicit abstract)
        bool hasAbstractDecorator = func.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = func.Body.Length == 1
            && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
        bool isAbstract = hasAbstractDecorator || (_isInAbstractClass && hasEllipsisBody);

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
            var bodyStatements = func.Body
                .SelectMany(GenerateBodyStatements);
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

    /// <summary>
    /// Determines whether the 'override' keyword should be stripped from a method.
    /// In C#, interface methods (both default and abstract) cannot be overridden with
    /// the 'override' keyword — the class simply provides its own implementation.
    /// However, if a base class has the method, 'override' must be kept.
    /// </summary>
    private bool ShouldStripOverrideKeyword(string methodName)
    {
        if (_currentTypeSymbol == null)
            return false;

        // Walk the base class chain — if any base has this method as virtual/abstract/override, keep override
        var baseTypes = TypeHierarchyService.GetAllBaseTypes(_currentTypeSymbol, _context.SemanticBinding);
        foreach (var baseType in baseTypes)
        {
            if (baseType.Methods.Any(m => m.Name == methodName && (m.IsVirtual || m.IsAbstract || m.IsOverride)))
                return false; // Found in base class, keep override
        }

        // No base class method found — check only DIRECT interfaces (not inherited ones,
        // since base classes synthesize abstract methods for inherited interface methods)
        var interfaceRefs = _context.SemanticBinding.GetInterfaces(_currentTypeSymbol)
            ?? (IReadOnlyList<Semantic.InterfaceReference>)_currentTypeSymbol.Interfaces;
        foreach (var ifaceRef in interfaceRefs)
        {
            if (ifaceRef.Definition.Methods.Any(m => m.Name == methodName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the given method name matches a method declared in any interface
    /// implemented by the current type. Used to add 'virtual' to interface implementations
    /// so that subclasses can override them.
    /// </summary>
    private bool ImplementsInterfaceMethod(string methodName)
    {
        if (_currentTypeSymbol == null)
            return false;

        var interfaces = Semantic.TypeHierarchyService.GetAllInterfaces(_currentTypeSymbol, _context.SemanticBinding);
        foreach (var iface in interfaces)
        {
            if (iface.Methods.Any(m => m.Name == methodName))
                return true;
        }

        return false;
    }

    private MethodDeclarationSyntax GenerateInterfaceMethod(FunctionDef func)
    {
        var mangledName = DunderMapping.ResolveCSharpName(func.Name)
            ?? NameMangler.Transform(func.Name, NameContext.Method);

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

        var method = MethodDeclaration(returnType, mangledName)
            .WithParameterList(ParameterList(SeparatedList(parameters)));

        // Check if this is an abstract method (body is single ellipsis or pass)
        bool isAbstract = func.Body.Length == 1 &&
            (func.Body[0] is PassStatement ||
             (func.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral));

        if (isAbstract)
        {
            // Abstract interface method: no body, just semicolon
            method = method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            // Default interface method: emit the full body
            ResetMethodScope();
            CollectSourceVariableNames(func.Body);

            // Track parameters as declared variables (skip self)
            foreach (var param in func.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            var bodyStatements = func.Body.SelectMany(GenerateBodyStatements);
            method = method.WithBody(Block(bodyStatements));
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
    }

}
