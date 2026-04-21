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
/// RoslynEmitter partial class: Property, field, and indexer generation
/// </summary>
internal partial class RoslynEmitter
{
    private FieldDeclarationSyntax GenerateField(VariableDeclaration varDecl, string? mangledName = null)
    {
        // Use PascalCase for public fields (C# property-like convention)
        var fieldName = mangledName ?? NameMangler.ToPascalCase(varDecl.Name);

        // Get field type from annotation, or infer from initializer for consts
        TypeSyntax fieldType;
        if (varDecl.Type != null)
        {
            fieldType = _typeMapper.MapType(varDecl.Type);
        }
        else if (varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            fieldType = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
        }
        else
        {
            fieldType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var variable = VariableDeclarator(Identifier(fieldName));

        // Add initializer if present
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            // e.g., books: list[Book] = [] needs the element type from the annotation
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var initExpr = GenerateExpression(varDecl.InitialValue);
                variable = variable.WithInitializer(EqualsValueClause(initExpr));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        var declaration = VariableDeclaration(fieldType)
            .WithVariables(SingletonSeparatedList(variable));

        // Determine access modifier from decorators, defaulting based on name convention
        var accessToken = Token(GetAccessModifierFromNameConvention(varDecl.Name));
        foreach (var decorator in varDecl.Decorators)
        {
            switch (decorator.Name)
            {
                case DecoratorNames.Private:
                    accessToken = Token(SyntaxKind.PrivateKeyword);
                    break;
                case DecoratorNames.Protected:
                    accessToken = Token(SyntaxKind.ProtectedKeyword);
                    break;
                case DecoratorNames.Internal:
                    accessToken = Token(SyntaxKind.InternalKeyword);
                    break;
                case DecoratorNames.Public:
                    accessToken = Token(SyntaxKind.PublicKeyword);
                    break;
            }
        }

        var modifiers = TokenList(accessToken);

        if (varDecl.IsConst && IsConstEligibleType(fieldType))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.ConstKeyword));
        }
        else if (varDecl.IsConst)
        {
            // Non-const-eligible types use static readonly
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
            modifiers = modifiers.Add(Token(SyntaxKind.ReadOnlyKeyword));
        }
        else if (varDecl.Decorators.Any(d => d.Name == DecoratorNames.Static))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        return FieldDeclaration(declaration)
            .WithModifiers(modifiers);
    }

    /// <summary>
    /// Generates a C# indexer (this[K key] { get; set; }) from __getitem__ and/or __setitem__.
    /// If both are present, they are combined into a single indexer with both accessors.
    /// </summary>
    private IndexerDeclarationSyntax GenerateIndexer(FunctionDef? getItemFunc, FunctionDef? setItemFunc)
    {
        // Use __getitem__ for determining the indexer parameter type and return type,
        // fall back to __setitem__ if __getitem__ is not present
        var primaryFunc = getItemFunc ?? setItemFunc!;

        ResetMethodScope();
        CollectSourceVariableNames(primaryFunc.Body);

        // Determine modifiers from the primary function's decorators
        var modifiers = GenerateMethodModifiers(primaryFunc.Name, primaryFunc.Decorators);

        // Check if abstract
        bool hasAbstractDecorator = primaryFunc.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = primaryFunc.Body.Length == 1
            && primaryFunc.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
        bool isAbstract = hasAbstractDecorator || (_isInAbstractClass && hasEllipsisBody);

        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

        modifiers = ResolveModifierConflicts(modifiers);

        // Get the indexer parameter (first non-self parameter of __getitem__ or __setitem__)
        var indexParam = primaryFunc.Parameters
            .FirstOrDefault(p =>
                !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase));

        var indexParamSyntax = indexParam != null
            ? GenerateParameter(indexParam)
            : Parameter(Identifier("key"))
                .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)));

        // Determine return type from __getitem__'s return type, or __setitem__'s value parameter type
        TypeSyntax returnType;
        if (getItemFunc?.ReturnType != null)
        {
            returnType = _typeMapper.MapType(getItemFunc.ReturnType);
        }
        else if (setItemFunc != null)
        {
            // Use the type of the value parameter (last non-self parameter) from __setitem__
            var valueParam = setItemFunc.Parameters
                .Where(p =>
                    !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
                .Skip(1) // Skip the key parameter
                .FirstOrDefault();
            returnType = valueParam?.Type != null
                ? _typeMapper.MapType(valueParam.Type)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }
        else
        {
            returnType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Build accessors
        var accessors = new List<AccessorDeclarationSyntax>();

        if (getItemFunc != null)
        {
            ResetMethodScope();
            CollectSourceVariableNames(getItemFunc.Body);

            // Track the index parameter as a declared variable
            if (indexParam != null)
            {
                var paramName = NameMangler.Transform(indexParam.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(indexParam.Name);
                _variableVersions[baseName] = 0;
            }

            if (isAbstract)
            {
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
            else
            {
                var bodyStatements = getItemFunc.Body.SelectMany(GenerateBodyStatements);
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(Block(bodyStatements)));
            }
        }

        if (setItemFunc != null)
        {
            ResetMethodScope();
            CollectSourceVariableNames(setItemFunc.Body);

            // Track all non-self parameters as declared variables
            foreach (var param in setItemFunc.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(param.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            if (isAbstract)
            {
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
            else
            {
                var bodyStatements = setItemFunc.Body.SelectMany(GenerateBodyStatements);
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithBody(Block(bodyStatements)));
            }
        }

        var indexer = IndexerDeclaration(returnType)
            .WithModifiers(modifiers)
            .WithParameterList(BracketedParameterList(SingletonSeparatedList(indexParamSyntax)))
            .WithAccessorList(AccessorList(List(accessors)));

        if (!string.IsNullOrEmpty(primaryFunc.DocString))
        {
            indexer = indexer.WithLeadingTrivia(GenerateXmlDocComment(primaryFunc.DocString));
        }

        return indexer;
    }

    private PropertyDeclarationSyntax GenerateInterfaceProperty(VariableDeclaration varDecl)
    {
        // Use PascalCase for property names
        var propertyName = NameMangler.ToPascalCase(varDecl.Name);

        // Get property type from annotation
        // Interface properties must have type annotations in Sharpy
        if (varDecl.Type == null)
        {
            throw new InvalidOperationException(
                $"Interface property '{varDecl.Name}' must have a type annotation at {varDecl.LineStart}:{varDecl.ColumnStart}");
        }

        var propertyType = _typeMapper.MapType(varDecl.Type);

        // Interface properties have get and set accessors with no body
        var accessors = new[]
        {
            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
        };

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));

        return property;
    }

    /// <summary>
    /// Generates C# members from a group of PropertyDef AST nodes.
    /// A single PropertyDef produces a single-accessor property.
    /// Multiple PropertyDefs with the same name (e.g., getter + setter) combine
    /// into a single C# property with multiple accessors.
    /// Mixed auto+custom groups produce a backing field + property with custom accessors.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateGroupedProperty(List<PropertyDef> propGroup)
    {
        if (propGroup.Count == 1)
        {
            var prop = propGroup[0];
            if (prop.IsFunctionStyle)
            {
                yield return GenerateFunctionStyleProperty(prop);
                yield break;
            }
            yield return GenerateAutoProperty(prop);
            yield break;
        }

        // Check if this is a mixed auto+custom group
        var autoProp = propGroup.FirstOrDefault(p => !p.IsFunctionStyle);
        if (autoProp != null)
        {
            var customDefs = propGroup.Where(p => p.IsFunctionStyle).ToList();
            if (customDefs.Count > 0)
            {
                foreach (var member in GenerateMixedAutoCustomProperty(autoProp, customDefs))
                {
                    yield return member;
                }
                yield break;
            }
        }

        // Multiple function-style PropertyDef nodes with the same name: combine into one C# property
        yield return GenerateCombinedFunctionStyleProperty(propGroup);
    }

    /// <summary>
    /// Generates a backing field and property from mixed auto+custom PropertyDef nodes.
    /// The auto-property defines the backing field type and default value.
    /// Custom function-style PropertyDefs replace the auto-generated get/set accessors.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateMixedAutoCustomProperty(
        PropertyDef autoProp, List<PropertyDef> customDefs)
    {
        var propertyName = NameMangler.ToPascalCase(autoProp.Name);
        var backingFieldName = NameMangler.ToPascalCase("_" + autoProp.Name);

        // Determine property type from auto-property's type annotation
        TypeSyntax propertyType;
        if (autoProp.Type != null)
        {
            propertyType = _typeMapper.MapType(autoProp.Type);
        }
        else
        {
            propertyType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Generate the backing field: private T _name;
        var fieldVariable = VariableDeclarator(Identifier(backingFieldName));
        if (autoProp.DefaultValue != null)
        {
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = autoProp.Type;
            try
            {
                var initExpr = GenerateExpression(autoProp.DefaultValue);
                fieldVariable = fieldVariable.WithInitializer(EqualsValueClause(initExpr));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        var fieldDeclaration = FieldDeclaration(
            VariableDeclaration(propertyType)
                .WithVariables(SingletonSeparatedList(fieldVariable)))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));

        yield return fieldDeclaration;

        // Determine which custom accessors are provided
        var customGetter = customDefs.FirstOrDefault(p => p.Accessor == PropertyAccessor.Get);
        var customSetter = customDefs.FirstOrDefault(p => p.Accessor == PropertyAccessor.Set);

        // Build accessors
        var accessors = new List<AccessorDeclarationSyntax>();

        // Getter: custom body or auto-generated from backing field
        if (customGetter != null)
        {
            ResetMethodScope();
            CollectSourceVariableNames(customGetter.Body);

            foreach (var param in customGetter.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            var bodyStatements = customGetter.Body.SelectMany(GenerateBodyStatements);
            accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(bodyStatements)));
        }
        else
        {
            // Auto-getter: return _name;
            accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(ReturnStatement(IdentifierName(backingFieldName)))));
        }

        // Setter: custom body or auto-generated from backing field
        if (customSetter != null)
        {
            ResetMethodScope();
            CollectSourceVariableNames(customSetter.Body);

            foreach (var param in customSetter.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            // C# setter uses implicit 'value' parameter, so track it
            _declaredVariables.Add("value");
            _variableVersions["value"] = 0;

            var bodyStatements = customSetter.Body.SelectMany(GenerateBodyStatements);
            accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithBody(Block(bodyStatements)));
        }
        else
        {
            // Auto-setter: _name = value;
            accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithBody(Block(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(backingFieldName),
                        IdentifierName("value"))))));
        }

        // Apply modifiers from decorators on the auto-property
        var modifiers = GenerateMethodModifiers(autoProp.Name, autoProp.Decorators);

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(List(accessors)));

        // Add C# attributes from unknown decorators
        var propAttributes = GenerateAttributeListsFromDecorators(autoProp.Decorators);
        if (propAttributes.Count > 0)
        {
            property = property.WithAttributeLists(propAttributes);
        }

        yield return property;
    }

    /// <summary>
    /// Generates a single C# property from multiple PropertyDef nodes (e.g., getter + setter).
    /// Each PropertyDef contributes one accessor. Mixed access modifiers are supported
    /// (e.g., public get, private set) by applying accessor-level modifiers.
    /// </summary>
    private PropertyDeclarationSyntax GenerateCombinedFunctionStyleProperty(List<PropertyDef> propGroup)
    {
        var first = propGroup[0];
        var propertyName = NameMangler.ToPascalCase(first.Name);

        // Determine property type from getter's return type or setter's parameter type
        TypeSyntax propertyType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        var getterProp = propGroup.FirstOrDefault(p => p.Accessor == PropertyAccessor.Get);
        var setterProp = propGroup.FirstOrDefault(p => p.Accessor == PropertyAccessor.Set || p.Accessor == PropertyAccessor.Init);

        if (getterProp?.ReturnType != null)
        {
            propertyType = _typeMapper.MapType(getterProp.ReturnType);
        }
        else if (setterProp != null)
        {
            // Infer type from setter's non-self parameter type
            var valueParam = setterProp.Parameters
                .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
            if (valueParam?.Type != null)
            {
                propertyType = _typeMapper.MapType(valueParam.Type);
            }
        }
        else if (first.ReturnType != null)
        {
            propertyType = _typeMapper.MapType(first.ReturnType);
        }

        // Determine property-level modifiers from the getter (or first property)
        var modifierSource = getterProp ?? first;
        var modifiers = GenerateMethodModifiers(modifierSource.Name, modifierSource.Decorators);

        // Handle static: if any accessor has self, property is not static
        bool hasSelfParameter = propGroup.Any(p => p.Parameters.Any(param =>
            string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase)));
        if (hasSelfParameter && modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.StaticKeyword)));
        }
        if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        // Determine the property-level access modifier (widest access)
        var propertyAccess = GetWidestAccessModifier(modifiers);

        var accessors = new List<AccessorDeclarationSyntax>();

        foreach (var prop in propGroup)
        {
            // Clear method scope tracking for each accessor
            ResetMethodScope();
            CollectSourceVariableNames(prop.Body);

            foreach (var param in prop.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            SyntaxKind accessorKind;
            switch (prop.Accessor)
            {
                case PropertyAccessor.Set:
                    accessorKind = SyntaxKind.SetAccessorDeclaration;
                    break;
                case PropertyAccessor.Init:
                    accessorKind = SyntaxKind.InitAccessorDeclaration;
                    break;
                default:
                    accessorKind = SyntaxKind.GetAccessorDeclaration;
                    break;
            }

            var accessor = AccessorDeclaration(accessorKind);

            bool hasEllipsisBody = prop.Body.Length == 1
                && prop.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
            bool isAbstract = prop.Decorators.Any(d => d.Name == DecoratorNames.Abstract)
                || (_isInAbstractClass && hasEllipsisBody);

            if (isAbstract)
            {
                accessor = accessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                var bodyStatements = prop.Body.SelectMany(GenerateBodyStatements);
                accessor = accessor.WithBody(Block(bodyStatements));
            }

            // Apply accessor-level access modifier if it differs from property-level
            var accessorModifiers = GenerateMethodModifiers(prop.Name, prop.Decorators);
            var accessorAccess = GetAccessModifier(accessorModifiers);

            if (accessorAccess != null && accessorAccess != propertyAccess)
            {
                accessor = accessor.WithModifiers(TokenList(Token(accessorAccess.Value)));
            }

            accessors.Add(accessor);
        }

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));

        // Explicit interface properties: add specifier and omit access modifiers
        // (C# rule: explicit interface members cannot have access modifiers)
        if (first.ExplicitInterface != null)
        {
            var interfaceName = NameMangler.ToPascalCase(first.ExplicitInterface);
            property = property
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(IdentifierName(interfaceName)));
        }
        else
        {
            property = property.WithModifiers(modifiers);
        }

        // Add C# attributes from unknown decorators on the getter/first property
        var propAttributes = GenerateAttributeListsFromDecorators(modifierSource.Decorators);
        if (propAttributes.Count > 0)
        {
            property = property.WithAttributeLists(propAttributes);
        }

        return property;
    }

    /// <summary>
    /// Gets the widest access modifier from a token list.
    /// public > protected > private
    /// </summary>
    private static SyntaxKind? GetWidestAccessModifier(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            return SyntaxKind.PublicKeyword;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return SyntaxKind.ProtectedKeyword;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
            return SyntaxKind.InternalKeyword;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
            return SyntaxKind.PrivateKeyword;
        return SyntaxKind.PublicKeyword; // Default
    }

    /// <summary>
    /// Gets the access modifier from a token list, or null if none.
    /// </summary>
    private static SyntaxKind? GetAccessModifier(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            return SyntaxKind.PublicKeyword;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)))
            return SyntaxKind.ProtectedKeyword;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
            return SyntaxKind.InternalKeyword;
        if (modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
            return SyntaxKind.PrivateKeyword;
        return SyntaxKind.PublicKeyword; // Default
    }

    /// <summary>
    /// Generates a C# auto-property from a PropertyDef AST node.
    /// Maps accessor type to C# accessor list:
    ///   None -> { get; set; }
    ///   Get  -> { get; }
    ///   Set  -> { set; }
    ///   Init -> { get; init; }
    /// </summary>
    private PropertyDeclarationSyntax GenerateAutoProperty(PropertyDef propDef)
    {
        var propertyName = NameMangler.ToPascalCase(propDef.Name);

        TypeSyntax propertyType;
        if (propDef.Type != null)
        {
            propertyType = _typeMapper.MapType(propDef.Type);
        }
        else
        {
            propertyType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Build accessor list based on accessor type
        var accessors = new List<AccessorDeclarationSyntax>();
        switch (propDef.Accessor)
        {
            case PropertyAccessor.None:
                // { get; set; }
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                break;

            case PropertyAccessor.Get:
                // { get; }
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                break;

            case PropertyAccessor.Set:
                // { set; }
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                break;

            case PropertyAccessor.Init:
                // { get; init; }
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                accessors.Add(AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                break;
        }

        // Apply modifiers from decorators
        var modifiers = GenerateMethodModifiers(propDef.Name, propDef.Decorators);

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));

        // Explicit interface properties: add specifier and omit access modifiers
        // (C# rule: explicit interface members cannot have access modifiers)
        if (propDef.ExplicitInterface != null)
        {
            var interfaceName = NameMangler.ToPascalCase(propDef.ExplicitInterface);
            property = property
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(IdentifierName(interfaceName)));
        }
        else
        {
            property = property.WithModifiers(modifiers);
        }

        // Add C# attributes from unknown decorators
        var autoPropAttributes = GenerateAttributeListsFromDecorators(propDef.Decorators);
        if (autoPropAttributes.Count > 0)
        {
            property = property.WithAttributeLists(autoPropAttributes);
        }

        // Add initializer if default value is present
        if (propDef.DefaultValue != null)
        {
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = propDef.Type;
            try
            {
                var initExpr = GenerateExpression(propDef.DefaultValue);
                property = property.WithInitializer(EqualsValueClause(initExpr))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        return property;
    }

    /// <summary>
    /// Generates a C# property with a function-style body (custom getter/setter).
    /// </summary>
    private PropertyDeclarationSyntax GenerateFunctionStyleProperty(PropertyDef propDef)
    {
        var propertyName = NameMangler.ToPascalCase(propDef.Name);

        TypeSyntax propertyType;
        if (propDef.ReturnType != null)
        {
            propertyType = _typeMapper.MapType(propDef.ReturnType);
        }
        else if (propDef.Type != null)
        {
            propertyType = _typeMapper.MapType(propDef.Type);
        }
        else
        {
            propertyType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // Clear method scope tracking
        ResetMethodScope();
        CollectSourceVariableNames(propDef.Body);

        // Track parameters (skip self)
        foreach (var param in propDef.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Check if this is an abstract property (body is single ellipsis)
        bool hasAbstractDecorator = propDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = propDef.Body.Length == 1
            && propDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
        bool isAbstract = hasAbstractDecorator || (_isInAbstractClass && hasEllipsisBody);

        // Apply modifiers from decorators
        var modifiers = GenerateMethodModifiers(propDef.Name, propDef.Decorators);

        // Remove static if it has 'self' parameter (Pythonic convention)
        bool hasSelfParameter = propDef.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        if (hasSelfParameter && modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = TokenList(modifiers.Where(m => !m.IsKind(SyntaxKind.StaticKeyword)));
        }
        if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

        // Build the accessor
        AccessorDeclarationSyntax accessor;
        SyntaxKind accessorKind;
        switch (propDef.Accessor)
        {
            case PropertyAccessor.Set:
                accessorKind = SyntaxKind.SetAccessorDeclaration;
                break;
            case PropertyAccessor.Init:
                accessorKind = SyntaxKind.InitAccessorDeclaration;
                break;
            default:
                accessorKind = SyntaxKind.GetAccessorDeclaration;
                break;
        }

        accessor = AccessorDeclaration(accessorKind);

        if (isAbstract)
        {
            accessor = accessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            // For setter, track the 'value' parameter
            if (accessorKind == SyntaxKind.SetAccessorDeclaration || accessorKind == SyntaxKind.InitAccessorDeclaration)
            {
                var valueParam = propDef.Parameters
                    .FirstOrDefault(p =>
                        !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
                if (valueParam != null)
                {
                    var paramName = NameMangler.Transform(valueParam.Name, NameContext.Parameter);
                    // C# setter uses implicit 'value' parameter, so remap
                    _declaredVariables.Add("value");
                    _variableVersions["value"] = 0;
                }
            }

            var bodyStatements = propDef.Body.SelectMany(GenerateBodyStatements);
            accessor = accessor.WithBody(Block(bodyStatements));
        }

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(SingletonList(accessor)));

        // Explicit interface properties: add specifier and omit access modifiers
        // (C# rule: explicit interface members cannot have access modifiers)
        if (propDef.ExplicitInterface != null)
        {
            var interfaceName = NameMangler.ToPascalCase(propDef.ExplicitInterface);
            property = property
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(IdentifierName(interfaceName)));
        }
        else
        {
            property = property.WithModifiers(modifiers);
        }

        // Add C# attributes from unknown decorators
        var funcPropAttributes = GenerateAttributeListsFromDecorators(propDef.Decorators);
        if (funcPropAttributes.Count > 0)
        {
            property = property.WithAttributeLists(funcPropAttributes);
        }

        return property;
    }

    /// <summary>
    /// Generates a C# interface property from a PropertyDef AST node.
    /// Interface properties have abstract accessors (semicolon-only).
    /// </summary>
    private PropertyDeclarationSyntax GenerateInterfacePropertyFromDef(PropertyDef propDef)
    {
        var propertyName = NameMangler.ToPascalCase(propDef.Name);

        TypeSyntax propertyType;
        if (propDef.Type != null)
        {
            propertyType = _typeMapper.MapType(propDef.Type);
        }
        else if (propDef.ReturnType != null)
        {
            propertyType = _typeMapper.MapType(propDef.ReturnType);
        }
        else
        {
            propertyType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var accessors = new List<AccessorDeclarationSyntax>();

        if (propDef.IsFunctionStyle)
        {
            // Function-style interface property: single accessor based on kind
            bool isAbstract = propDef.Body.Length == 1 &&
                (propDef.Body[0] is PassStatement ||
                 (propDef.Body[0] is ExpressionStatement es && es.Expression is EllipsisLiteral));

            SyntaxKind accessorKind;
            switch (propDef.Accessor)
            {
                case PropertyAccessor.Set:
                    accessorKind = SyntaxKind.SetAccessorDeclaration;
                    break;
                case PropertyAccessor.Init:
                    accessorKind = SyntaxKind.InitAccessorDeclaration;
                    break;
                default:
                    accessorKind = SyntaxKind.GetAccessorDeclaration;
                    break;
            }

            if (isAbstract)
            {
                accessors.Add(AccessorDeclaration(accessorKind)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
            else
            {
                // Default interface property implementation
                ResetMethodScope();
                CollectSourceVariableNames(propDef.Body);

                foreach (var param in propDef.Parameters)
                {
                    if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                    _declaredVariables.Add(paramName);
                    var baseName = NameMangler.ToCamelCase(param.Name);
                    _variableVersions[baseName] = 0;
                }

                var bodyStatements = propDef.Body.SelectMany(GenerateBodyStatements);
                accessors.Add(AccessorDeclaration(accessorKind)
                    .WithBody(Block(bodyStatements)));
            }
        }
        else
        {
            // Auto-property style: determine accessors from Accessor type
            switch (propDef.Accessor)
            {
                case PropertyAccessor.None:
                    accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    break;
                case PropertyAccessor.Get:
                    accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    break;
                case PropertyAccessor.Set:
                    accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    break;
                case PropertyAccessor.Init:
                    accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    accessors.Add(AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                    break;
            }
        }

        return PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));
    }

}
