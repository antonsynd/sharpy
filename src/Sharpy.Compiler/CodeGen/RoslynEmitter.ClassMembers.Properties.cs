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
        var fieldName = mangledName ?? NameCasing.ResolveField(varDecl.Name, varDecl.IsNameBacktickEscaped);

        // Get field type from annotation, or infer from initializer for consts
        TypeSyntax fieldType;
        if (varDecl.Type != null)
        {
            fieldType = _typeMapper.MapType(varDecl.Type);
        }
        else if (varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            fieldType = ResolveInferredDeclarationType(varDecl.InitialValue);
        }
        else
        {
            fieldType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var variable = VariableDeclarator(EscapedIdentifier(fieldName));

        // Add initializer if present
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            // e.g., books: list[Book] = [] needs the element type from the annotation
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var initExpr = GenerateInitializerValue(varDecl.InitialValue, varDecl.Type);
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
        else if (varDecl.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Static))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        // @final fields emit as C# readonly (assignment restricted to constructors).
        if (varDecl.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Final))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.ReadOnlyKeyword));
        }

        var fieldDecl = FieldDeclaration(declaration).WithModifiers(modifiers);
        var attributes = GenerateAttributeListsFromDecorators(varDecl.Decorators);
        if (attributes.Count > 0)
            fieldDecl = fieldDecl.WithAttributeLists(attributes);
        return fieldDecl;
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

        var indexerSymbol = _currentTypeSymbol?.Methods.FirstOrDefault(m => m.Name == primaryFunc.Name);
        bool isAbstract = indexerSymbol?.IsAbstract ?? false;

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
                var paramName = ParameterCSharpName(indexParam);
                _declaredVariables.Add(paramName);
                var baseName = ParameterCSharpName(indexParam);
                RegisterLocalSlot(baseName, indexParam.Name);
            }

            if (isAbstract)
            {
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
            else
            {
                var bodyStatements = GenerateSuite(getItemFunc.Body);
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
                var paramName = ParameterCSharpName(param);
                _declaredVariables.Add(paramName);
                var baseName = ParameterCSharpName(param);
                RegisterLocalSlot(baseName, param.Name);
            }

            if (isAbstract)
            {
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
            else
            {
                var bodyStatements = GenerateSuite(setItemFunc.Body);
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
        var propertyName = NameCasing.ResolveMethod(varDecl.Name, varDecl.IsNameBacktickEscaped);

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
            if (!prop.Observers.IsEmpty)
            {
                foreach (var member in GenerateObserverProperty(prop))
                    yield return member;
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
        var propertyName = NameCasing.ResolveMethod(autoProp.Name, autoProp.IsNameBacktickEscaped);
        var backingFieldName = "_" + NameCasing.ResolveField(autoProp.Name, autoProp.IsNameBacktickEscaped);

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
        var fieldVariable = VariableDeclarator(EscapedIdentifier(backingFieldName));
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

        // Apply modifiers from decorators on the auto-property (needed before the accessors so an
        // accessor-level modifier can be compared against the property's own).
        var modifiers = GenerateMethodModifiers(autoProp.Name, autoProp.Decorators);
        var propertyAccess = GetAccessModifier(modifiers);

        // The auto half's declared accessor decides which halves exist; a custom accessor always
        // contributes its own. `property get x: T` beside a custom GETTER is get-only and used to
        // gain a silent auto-setter, writing a backing field the author never asked to expose
        // (#1307). `property init x: T` contributes an init accessor, not a set.
        bool emitGetter = customGetter != null
            || autoProp.Accessor is PropertyAccessor.None or PropertyAccessor.Get or PropertyAccessor.Init;
        bool emitSetter = customSetter != null
            || autoProp.Accessor is PropertyAccessor.None or PropertyAccessor.Set or PropertyAccessor.Init;
        var autoSetterKind = autoProp.Accessor == PropertyAccessor.Init
            ? SyntaxKind.InitAccessorDeclaration
            : SyntaxKind.SetAccessorDeclaration;

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
                var paramName = ParameterCSharpName(param);
                _declaredVariables.Add(paramName);
                var baseName = ParameterCSharpName(param);
                RegisterLocalSlot(baseName, param.Name);
            }

            var bodyStatements = GenerateSuite(customGetter.Body);
            accessors.Add(WithAccessorAccess(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithBody(Block(bodyStatements)),
                customGetter, propertyAccess));
        }
        else if (emitGetter)
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

            // C# setter uses implicit 'value' parameter, so track it
            _declaredVariables.Add("value");
            RegisterLocalSlot("value", "value");

            // The setter's declared parameter is MAPPED onto that implicit `value`, never declared
            // (#1405). Registering it as a local was the defect: it made the slot lookup answer the
            // Sharpy spelling with a name no C# declaration introduces.
            using var setterRewrite =
                AccessorParamRewrite(AccessorValueParamName(customSetter.Parameters), "value");

            var bodyStatements = GenerateSuite(customSetter.Body);
            accessors.Add(WithAccessorAccess(
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithBody(Block(bodyStatements)),
                customSetter, propertyAccess));
        }
        else if (emitSetter)
        {
            // Auto-setter: _name = value;
            accessors.Add(AccessorDeclaration(autoSetterKind)
                .WithBody(Block(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(backingFieldName),
                        IdentifierName("value"))))));
        }

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
        var propertyName = NameCasing.ResolveMethod(first.Name, first.IsNameBacktickEscaped);

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

            SyntaxKind accessorKind;
            bool writesValue;
            switch (prop.Accessor)
            {
                case PropertyAccessor.Set:
                    accessorKind = SyntaxKind.SetAccessorDeclaration;
                    writesValue = true;
                    break;
                case PropertyAccessor.Init:
                    accessorKind = SyntaxKind.InitAccessorDeclaration;
                    writesValue = true;
                    break;
                default:
                    accessorKind = SyntaxKind.GetAccessorDeclaration;
                    writesValue = false;
                    break;
            }

            if (writesValue)
            {
                _declaredVariables.Add("value");
                RegisterLocalSlot("value", "value");
            }
            else
            {
                // A getter has no incoming value and so no C# name to map its parameters onto. They
                // are registered here only to keep today's behavior; a non-`self` parameter on a
                // getter is a shape C# cannot express and is refused in #1406, not mapped here.
                foreach (var param in prop.Parameters)
                {
                    if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                        continue;
                    _declaredVariables.Add(ParameterCSharpName(param));
                    RegisterLocalSlot(ParameterCSharpName(param), param.Name);
                }
            }

            var accessor = AccessorDeclaration(accessorKind);

            var propSymbol = _currentTypeSymbol?.Properties.FirstOrDefault(p => p.Name == first.Name);
            bool isAbstract = propSymbol?.IsAbstract
                ?? prop.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract);

            if (isAbstract)
            {
                accessor = accessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
            }
            else
            {
                // The setter's declared parameter maps onto C#'s implicit `value` (#1405).
                using var valueRewrite = AccessorParamRewrite(
                    writesValue ? AccessorValueParamName(prop.Parameters) : null, "value");
                var bodyStatements = GenerateSuite(prop.Body);
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
            var interfaceName = NameCasing.ResolveType(first.ExplicitInterface, false);
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
    /// <summary>
    /// Carries a custom accessor's own access modifier onto the accessor when it is narrower than
    /// the property's — the mixed auto/function-style path used to read decorators from the auto
    /// half only, silently dropping a `@private` on the setter (#1307). Mirrors the combined
    /// function-style path.
    /// </summary>
    private AccessorDeclarationSyntax WithAccessorAccess(
        AccessorDeclarationSyntax accessor, PropertyDef accessorDef, SyntaxKind? propertyAccess)
    {
        var accessorAccess = GetAccessModifier(GenerateMethodModifiers(accessorDef.Name, accessorDef.Decorators));
        return accessorAccess != null && accessorAccess != propertyAccess
            ? accessor.WithModifiers(TokenList(Token(accessorAccess.Value)))
            : accessor;
    }

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
        var propertyName = NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped);

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
        bool isReadonly = propDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Readonly);
        var accessors = new List<AccessorDeclarationSyntax>();
        switch (propDef.Accessor)
        {
            case PropertyAccessor.None:
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                if (!isReadonly)
                {
                    // { get; set; } — default; @readonly omits the setter → { get; }
                    accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                }
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
            var interfaceName = NameCasing.ResolveType(propDef.ExplicitInterface, false);
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
    /// Generates a backing field and an expanded-setter property for an auto-property carrying
    /// <c>before_set</c>/<c>after_set</c> observers (#416). The getter returns the backing field;
    /// the setter runs the observers around the store:
    /// <code>
    /// private T _name = &lt;default&gt;;
    /// public T Name
    /// {
    ///     get { return _name; }
    ///     set
    ///     {
    ///         var __old = _name;        // only when after_set exists
    ///         { &lt;before_set body, param → value&gt; }
    ///         _name = value;
    ///         { &lt;after_set body, param → __old&gt; }
    ///     }
    /// }
    /// </code>
    /// Every store to the property — including constructor assignments, which target the property
    /// rather than the field — therefore runs the observers (Design Decision 9).
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateObserverProperty(PropertyDef propDef)
    {
        var propertyName = NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped);
        var backingFieldName = "_" + NameCasing.ResolveField(propDef.Name, propDef.IsNameBacktickEscaped);

        TypeSyntax propertyType = propDef.Type != null
            ? _typeMapper.MapType(propDef.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // Backing field: private T _name [= default];
        var fieldVariable = VariableDeclarator(EscapedIdentifier(backingFieldName));
        if (propDef.DefaultValue != null)
        {
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = propDef.Type;
            try
            {
                var initExpr = GenerateInitializerValue(propDef.DefaultValue, propDef.Type);
                fieldVariable = fieldVariable.WithInitializer(EqualsValueClause(initExpr));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        yield return FieldDeclaration(
                VariableDeclaration(propertyType).WithVariables(SingletonSeparatedList(fieldVariable)))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));

        var beforeSet = propDef.Observers.FirstOrDefault(o => o.Kind == ObserverKind.BeforeSet);
        var afterSet = propDef.Observers.FirstOrDefault(o => o.Kind == ObserverKind.AfterSet);
        const string oldValueLocal = "__old";

        // Getter: { return _name; }
        var getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithBody(Block(ReturnStatement(IdentifierName(backingFieldName))));

        // Setter: observers around the store.
        var setterStatements = new List<StatementSyntax>();

        if (afterSet != null)
        {
            // var __old = _name;  (captured before the store, read inside after_set)
            setterStatements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(oldValueLocal))
                            .WithInitializer(EqualsValueClause(IdentifierName(backingFieldName)))))));
        }

        if (beforeSet != null)
        {
            setterStatements.Add(GenerateObserverBody(beforeSet, targetName: "value"));
        }

        // _name = value;
        setterStatements.Add(ExpressionStatement(AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            IdentifierName(backingFieldName),
            IdentifierName("value"))));

        if (afterSet != null)
        {
            setterStatements.Add(GenerateObserverBody(afterSet, targetName: oldValueLocal));
        }

        var setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
            .WithBody(Block(setterStatements));

        var modifiers = GenerateMethodModifiers(propDef.Name, propDef.Decorators);

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(List(new[] { getter, setter })));

        var propAttributes = GenerateAttributeListsFromDecorators(propDef.Decorators);
        if (propAttributes.Count > 0)
            property = property.WithAttributeLists(propAttributes);

        yield return property;
    }

    /// <summary>
    /// Emits a single observer suite as a braced block, rewriting the observer's source parameter
    /// to <paramref name="targetName"/> (the setter's implicit <c>value</c> for
    /// <c>before_set</c>, or the captured old-value local for <c>after_set</c>).
    /// </summary>
    private BlockSyntax GenerateObserverBody(PropertyObserver observer, string targetName)
    {
        ResetMethodScope();
        CollectSourceVariableNames(observer.Body);

        using var rewrite = AccessorParamRewrite(observer.ParamName, targetName);
        return GenerateSuiteBlock(observer.Body);
    }

    /// <summary>
    /// The accessor's named incoming value — its first non-<c>self</c> parameter — or null when it
    /// declares none. C# hands that value to the accessor as the implicit <c>value</c>, so the
    /// Sharpy name is MAPPED onto it rather than declared; see <c>AccessorParamRewrite</c>.
    /// </summary>
    private static string? AccessorValueParamName(IEnumerable<Parameter> parameters)
        => parameters.FirstOrDefault(p =>
            !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))?.Name;

    /// <summary>
    /// The C# type of a single-accessor function-style property, read from the annotation the user
    /// WROTE: a getter's return type, else a setter's value-parameter type.
    ///
    /// <para>The setter arm is what was missing (#1405): consulting only <c>ReturnType</c> then
    /// <c>Type</c> — both null for a setter — fell through to <c>object</c>, so
    /// <c>property set doubled(self, v: int)</c> emitted <c>public object Doubled</c>. Derived from
    /// the AST rather than from <c>PropertySymbol.Type</c> because a MODULE-LEVEL property has no
    /// <c>TypeSymbol</c> and therefore no <c>PropertySymbol</c> to read — the symbol route cannot
    /// reach that shape at all, while this one serves every shape and already resolves aliases and
    /// type parameters (it is what the combined-accessor generator has always used).</para>
    /// </summary>
    private TypeSyntax FunctionStylePropertyType(PropertyDef propDef)
    {
        if (propDef.ReturnType != null)
            return _typeMapper.MapType(propDef.ReturnType);
        if (propDef.Type != null)
            return _typeMapper.MapType(propDef.Type);

        var valueParam = propDef.Parameters.FirstOrDefault(p =>
            !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        if (valueParam?.Type != null)
            return _typeMapper.MapType(valueParam.Type);

        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
    }

    /// <summary>
    /// Generates a C# property with a function-style body (custom getter/setter).
    /// </summary>
    private PropertyDeclarationSyntax GenerateFunctionStyleProperty(PropertyDef propDef)
    {
        var propertyName = NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped);

        var propertyType = FunctionStylePropertyType(propDef);

        // Clear method scope tracking
        ResetMethodScope();
        CollectSourceVariableNames(propDef.Body);

        bool writesValue = propDef.Accessor is PropertyAccessor.Set or PropertyAccessor.Init;

        // A getter's parameters have no C# name to map onto; see GenerateCombinedFunctionStyleProperty.
        if (!writesValue)
        {
            foreach (var param in propDef.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                _declaredVariables.Add(ParameterCSharpName(param));
                RegisterLocalSlot(ParameterCSharpName(param), param.Name);
            }
        }

        var funcStylePropSymbol = _currentTypeSymbol?.Properties.FirstOrDefault(p => p.Name == propDef.Name);
        bool isAbstract = funcStylePropSymbol?.IsAbstract
            ?? propDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract);

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
            if (writesValue)
            {
                _declaredVariables.Add("value");
                RegisterLocalSlot("value", "value");
            }

            // The setter's declared parameter maps onto that implicit `value` (#1405).
            using var valueRewrite = AccessorParamRewrite(
                writesValue ? AccessorValueParamName(propDef.Parameters) : null, "value");

            var bodyStatements = GenerateSuite(propDef.Body);
            accessor = accessor.WithBody(Block(bodyStatements));
        }

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(SingletonList(accessor)));

        // Explicit interface properties: add specifier and omit access modifiers
        // (C# rule: explicit interface members cannot have access modifiers)
        if (propDef.ExplicitInterface != null)
        {
            var interfaceName = NameCasing.ResolveType(propDef.ExplicitInterface, false);
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
        var propertyName = NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped);

        // Same derivation as the class-level single-accessor generator, including the setter arm a
        // lone `property set` needs (#1405); Type is preferred first here because an interface
        // auto-property carries its annotation there.
        var propertyType = propDef.Type != null
            ? _typeMapper.MapType(propDef.Type)
            : FunctionStylePropertyType(propDef);

        var accessors = new List<AccessorDeclarationSyntax>();

        if (propDef.IsFunctionStyle)
        {
            // Function-style interface property: single accessor based on kind
            bool isAbstract = AstHelper.IsAbstractStubBody(propDef.Body);

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

                bool writesValue = propDef.Accessor is PropertyAccessor.Set or PropertyAccessor.Init;
                if (writesValue)
                {
                    // This arm registered no `value` at all before #1405 — it is the one accessor
                    // shape where neither the Sharpy name nor the C# one was ever introduced.
                    _declaredVariables.Add("value");
                    RegisterLocalSlot("value", "value");
                }
                else
                {
                    foreach (var param in propDef.Parameters)
                    {
                        if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                            continue;
                        _declaredVariables.Add(ParameterCSharpName(param));
                        RegisterLocalSlot(ParameterCSharpName(param), param.Name);
                    }
                }

                using var valueRewrite = AccessorParamRewrite(
                    writesValue ? AccessorValueParamName(propDef.Parameters) : null, "value");

                var bodyStatements = GenerateSuite(propDef.Body);
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
