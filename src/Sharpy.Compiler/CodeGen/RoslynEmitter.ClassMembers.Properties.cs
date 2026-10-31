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
    /// <param name="constructorOwnsDefault">
    /// True when a synthesized constructor definitely assigns this field on every path and owns the
    /// evaluation of its default (a struct auto-constructor's per-instance field default, #1684 R-A
    /// / #1901). A field initializer runs on EVERY construction, so leaving it in place evaluated
    /// the default a second time — once eagerly and once in the constructor — which is observable
    /// the moment the default is impure (`[side(1), side(2)]`). See
    /// <see cref="GeneratePerInstanceDefaultAssignment"/>.
    /// </param>
    private FieldDeclarationSyntax GenerateField(
        VariableDeclaration varDecl, string? mangledName = null, bool constructorOwnsDefault = false)
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

        // Add initializer if present. A comprehension/generator/lambda/walrus in the initializer
        // hoists under its own scope sink so it has somewhere to land (#1685) — see
        // GenerateInitializerExpression.
        if (varDecl.InitialValue != null && !constructorOwnsDefault)
        {
            var initExpr = GenerateInitializerExpression(varDecl.InitialValue, fieldType);
            variable = variable.WithInitializer(EqualsValueClause(initExpr));
        }

        var declaration = VariableDeclaration(fieldType)
            .WithVariables(SingletonSeparatedList(variable));

        // Access is the field symbol's classified level, host axis included (#1937).
        var modifiers = TokenList(Token(MemberAccessKeyword(varDecl)));

        // Read the compile-time constant fact from CodeGenInfo (#1791).
        var fieldSymbol = _currentTypeSymbol?.Fields.FirstOrDefault(f => f.Name == varDecl.Name);
        var fieldCodeGenInfo = fieldSymbol != null ? GetCodeGenInfo(fieldSymbol) : null;
        if (varDecl.IsConst && fieldCodeGenInfo?.IsCompileTimeConstant == true)
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

        // Determine modifiers from the primary function's decorators
        var modifiers = GenerateMethodModifiers(MemberAccessKeyword(primaryFunc), primaryFunc.Decorators);

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

            // Track the index parameter as a declared variable
            if (indexParam != null)
            {
                var paramName = ParameterCSharpName(indexParam);
                var baseName = ParameterCSharpName(indexParam);
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

            // A C# indexer's set accessor takes its written value through the implicit `value`, so
            // `__setitem__`'s LAST parameter is mapped onto that name and never declared — exactly
            // as a property's custom setter does (#1405, `GenerateMixedAutoCustomProperty`). Without
            // the mapping the body referenced the Sharpy spelling, which no C# declaration
            // introduces: `def __setitem__(self, i: int, v: int)` emitted `v` and came back as
            // CS0103 behind SPY0908, while the same method spelled `value` compiled by accident
            // (#1529). The INDEX parameter is a real declared parameter and is registered normally.
            var valueParamName = IndexerValueParamName(setItemFunc.Parameters);

            foreach (var param in setItemFunc.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(param.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = ParameterCSharpName(param);
                var baseName = ParameterCSharpName(param);
            }


            if (isAbstract)
            {
                accessors.Add(AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
            }
            else
            {
                using var valueRewrite = AccessorParamRewrite(valueParamName, "value");
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
            foreach (var member in GenerateAutoProperty(prop))
                yield return member;
            yield break;
        }

        // Every half is auto (`property get a: int` + `property set a: int`): one auto property
        if (propGroup.All(p => !p.IsFunctionStyle))
        {
            yield return GenerateSplitAutoProperty(propGroup);
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
    /// Generates ONE C# auto property from a property's split auto halves
    /// (<c>property get a: int</c> + <c>@private property set a: int</c>, properties_function_style.md
    /// "With Auto-Properties"): each half contributes its own accessors
    /// (<see cref="AutoHalfAccessorKinds"/>) at its own access, the property takes the widest, and the
    /// one half carrying a default supplies the initializer — two defaulted halves are SPY0407
    /// (<c>PropertyValidator.ValidateMixedPropertyStyle</c>), so there is at most one. Without this arm
    /// the group fell to the function-style combiner: empty accessor bodies, CS0161 (#2382).
    /// </summary>
    private PropertyDeclarationSyntax GenerateSplitAutoProperty(List<PropertyDef> propGroup)
    {
        var first = propGroup[0];
        var propertyName = NameCasing.ResolveMethod(first.Name, first.IsNameBacktickEscaped);
        TypeSyntax propertyType = propGroup.DeclaredTypeAnnotation() is { } declaredAnnotation
            ? _typeMapper.MapType(declaredAnnotation)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // The property-level access is the widest half's; static/virtual/… come from the getter half
        // (or the first), as in the combined function-style arm.
        var modifierSource = propGroup.FirstOrDefault(p => p.Accessor == PropertyAccessor.Get) ?? first;
        var propertyAccess = GetWidestAccessModifier(TokenList(propGroup.Select(p => Token(MemberAccessKeyword(p)))));
        var modifiers = GenerateMethodModifiers(propertyAccess ?? SyntaxKind.PublicKeyword, modifierSource.Decorators);

        var accessors = propGroup
            .SelectMany(half => AutoHalfAccessorKinds(half.Accessor).Select(kind => WithAccessorAccess(
                AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                half, propertyAccess)))
            .ToList();

        var property = PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));

        // Explicit interface properties: add specifier and omit access modifiers
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

        var propAttributes = GenerateAttributeListsFromDecorators(modifierSource.Decorators);
        if (propAttributes.Count > 0)
        {
            property = property.WithAttributeLists(propAttributes);
        }

        if (propGroup.FirstOrDefault(p => p.DefaultValue != null) is { DefaultValue: { } defaultValue })
        {
            property = property.WithInitializer(EqualsValueClause(GenerateExpression(defaultValue)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        return property;
    }

    /// <summary>
    /// The private field an auto half's storage lives in when its property is expanded — beside a
    /// function-style accessor (<see cref="GenerateMixedAutoCustomProperty"/>) or with observers
    /// (<see cref="GenerateObserverProperty"/>). The ONE spelling: a struct's synthesized constructor
    /// writes this field for a mixed property (<c>StructConstructorRoster</c>, #2382), so it can never
    /// name a field the property arm did not declare.
    /// </summary>
    private static string PropertyBackingFieldName(PropertyDef autoHalf)
        => "_" + NameCasing.ResolveField(autoHalf.Name, autoHalf.IsNameBacktickEscaped);

    /// <summary>
    /// Generates a backing field and property from mixed auto+custom PropertyDef nodes.
    /// The auto-property defines the backing field type and default value.
    /// Custom function-style PropertyDefs replace the auto-generated get/set accessors.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateMixedAutoCustomProperty(
        PropertyDef autoProp, List<PropertyDef> customDefs)
    {
        var propertyName = NameCasing.ResolveMethod(autoProp.Name, autoProp.IsNameBacktickEscaped);
        var backingFieldName = PropertyBackingFieldName(autoProp);

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
            var initExpr = GenerateExpression(autoProp.DefaultValue);
            fieldVariable = fieldVariable.WithInitializer(EqualsValueClause(initExpr));
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
        var modifiers = GenerateMethodModifiers(MemberAccessKeyword(autoProp), autoProp.Decorators);
        var propertyAccess = GetAccessModifier(modifiers);

        // The auto half's declared accessor decides which halves exist; a custom accessor always
        // contributes its own. `property get x: T` beside a custom GETTER is get-only and used to
        // gain a silent auto-setter, writing a backing field the author never asked to expose
        // (#1307). `property init x: T` contributes an init accessor, not a set.
        var autoKinds = AutoHalfAccessorKinds(autoProp.Accessor);
        bool emitGetter = customGetter != null || autoKinds.Contains(SyntaxKind.GetAccessorDeclaration);
        bool emitSetter = customSetter != null || autoKinds.Any(IsWritingAccessor);
        var autoSetterKind = autoKinds.Contains(SyntaxKind.InitAccessorDeclaration)
            ? SyntaxKind.InitAccessorDeclaration
            : SyntaxKind.SetAccessorDeclaration;

        // Build accessors
        var accessors = new List<AccessorDeclarationSyntax>();

        // Getter: custom body or auto-generated from backing field
        if (customGetter != null)
        {
            ResetMethodScope();

            foreach (var param in customGetter.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = ParameterCSharpName(param);
                var baseName = ParameterCSharpName(param);
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

            // C# setter uses implicit 'value' parameter, so track it

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

        // The property's type is its declaring annotation across the accessors — the getter's
        // first, then a setter's value parameter — the same syntax fact the checker typed it from;
        // a setter's `-> None` is never it (#2370, R-JD).
        var getterProp = propGroup.FirstOrDefault(p => p.Accessor == PropertyAccessor.Get);
        var declaredAnnotation = propGroup.DeclaredTypeAnnotation();
        TypeSyntax propertyType = declaredAnnotation != null
            ? _typeMapper.MapType(declaredAnnotation)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // Determine property-level modifiers from the getter (or first property)
        var modifierSource = getterProp ?? first;
        var modifiers = GenerateMethodModifiers(MemberAccessKeyword(modifierSource), modifierSource.Decorators);

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
                }
            }

            var accessor = AccessorDeclaration(accessorKind);

            var propSymbol = _currentTypeSymbol?.FindProperty(first.Name, first.ExplicitInterface);
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
            var accessorModifiers = GenerateMethodModifiers(MemberAccessKeyword(prop), prop.Decorators);
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
        var accessorAccess = GetAccessModifier(GenerateMethodModifiers(MemberAccessKeyword(accessorDef), accessorDef.Decorators));
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
    ///   Set  -> private T _Name; T Name { set => _Name = value; }  (abstract: abstract T Name { set; })
    ///   Init -> { get; init; }
    /// C# has no set-only auto property (CS8051), so a lone auto set half — the spec's write-only
    /// <c>property set name: T</c> — stores to a synthesized backing field its set accessor writes,
    /// named by <see cref="PropertyBackingFieldName"/> like the mixed and observer arms' fields, and
    /// its default initializes that field (R-JS (a), #2395). The shape is the AST's: no semantic fact
    /// decides it.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateAutoProperty(PropertyDef propDef)
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

        // Apply modifiers from decorators
        var modifiers = GenerateMethodModifiers(MemberAccessKeyword(propDef), propDef.Decorators);

        // Build accessor list based on accessor type. An abstract lone set half declares no storage —
        // `abstract T Name { set; }` is legal C#, and an abstract accessor has no body to write a field.
        bool isReadonly = propDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Readonly);
        var backingFieldName = propDef.Accessor == PropertyAccessor.Set
            && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword))
                ? PropertyBackingFieldName(propDef)
                : null;
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
                // { set => _Name = value; } — or, abstract, { set; }
                var setAccessor = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration);
                if (backingFieldName != null)
                {
                    setAccessor = setAccessor.WithExpressionBody(ArrowExpressionClause(AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(backingFieldName),
                        IdentifierName("value"))));
                }
                accessors.Add(setAccessor.WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                break;

            case PropertyAccessor.Init:
                // { get; init; }
                accessors.Add(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                accessors.Add(AccessorDeclaration(SyntaxKind.InitAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                break;
        }

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

        // The default initializes the property, or the lone set half's backing field
        var defaultValue = propDef.DefaultValue != null ? GenerateExpression(propDef.DefaultValue) : null;

        if (backingFieldName != null)
        {
            // private [static] T _Name [= default];
            var fieldVariable = VariableDeclarator(EscapedIdentifier(backingFieldName));
            if (defaultValue != null)
            {
                fieldVariable = fieldVariable.WithInitializer(EqualsValueClause(defaultValue));
            }

            var fieldModifiers = modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                ? TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword))
                : TokenList(Token(SyntaxKind.PrivateKeyword));
            yield return FieldDeclaration(
                    VariableDeclaration(propertyType).WithVariables(SingletonSeparatedList(fieldVariable)))
                .WithModifiers(fieldModifiers);
            yield return property;
            yield break;
        }

        // Add initializer if default value is present
        if (defaultValue != null)
        {
            property = property.WithInitializer(EqualsValueClause(defaultValue))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        yield return property;
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
        var backingFieldName = PropertyBackingFieldName(propDef);

        TypeSyntax propertyType = propDef.Type != null
            ? _typeMapper.MapType(propDef.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // Backing field: private T _name [= default];
        var fieldVariable = VariableDeclarator(EscapedIdentifier(backingFieldName));
        if (propDef.DefaultValue != null)
        {
            var initExpr = GenerateExpression(propDef.DefaultValue);
            fieldVariable = fieldVariable.WithInitializer(EqualsValueClause(initExpr));
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

        var modifiers = GenerateMethodModifiers(MemberAccessKeyword(propDef), propDef.Decorators);

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
    /// An indexer set accessor's named incoming value: <c>__setitem__</c>'s LAST non-<c>self</c>
    /// parameter, or null when it declares none.
    ///
    /// <para>Separate from <see cref="AccessorValueParamName"/>, which takes the FIRST — and that
    /// difference is the whole point. A property setter's only non-<c>self</c> parameter IS the
    /// value; <c>__setitem__(self, index, value)</c> declares the index first, so taking the first
    /// would map the INDEX onto <c>value</c> and leave the value undeclared: the same CS0103 with
    /// the operands swapped (#1529).</para>
    /// </summary>
    private static string? IndexerValueParamName(IReadOnlyList<Parameter> parameters)
        => parameters.LastOrDefault(p =>
            !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))?.Name;

    /// <summary>
    /// The C# type of a single-accessor function-style property, read from the annotation the user
    /// WROTE that declares it: a getter's return type, a setter's value-parameter type — never a
    /// setter's return annotation, which made <c>property set p(self, v: int) -&gt; None</c> a C#
    /// <c>void</c> property (CS0547; #2370, R-JD).
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
        => propDef.DeclaredTypeAnnotation() is { } declaredAnnotation
            ? _typeMapper.MapType(declaredAnnotation)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

    /// <summary>
    /// Generates a C# property with a function-style body (custom getter/setter).
    /// </summary>
    private PropertyDeclarationSyntax GenerateFunctionStyleProperty(PropertyDef propDef)
    {
        var propertyName = NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped);

        var propertyType = FunctionStylePropertyType(propDef);

        // Clear method scope tracking
        ResetMethodScope();

        bool writesValue = propDef.Accessor is PropertyAccessor.Set or PropertyAccessor.Init;

        // A getter's parameters have no C# name to map onto; see GenerateCombinedFunctionStyleProperty.
        if (!writesValue)
        {
            foreach (var param in propDef.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
        }

        var funcStylePropSymbol = _currentTypeSymbol?.FindProperty(propDef.Name, propDef.ExplicitInterface);
        bool isAbstract = funcStylePropSymbol?.IsAbstract
            ?? propDef.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract);

        // Apply modifiers from decorators
        var modifiers = GenerateMethodModifiers(MemberAccessKeyword(propDef), propDef.Decorators);

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
    /// The accessors an AUTO property half declares, by its accessor keyword: <c>property x: T</c> is
    /// get + set, <c>property get x: T</c> get, <c>property set x: T</c> set, <c>property init x: T</c>
    /// get + init. The one rule the class arm's auto-beside-custom merge and both interface arms read.
    /// </summary>
    private static SyntaxKind[] AutoHalfAccessorKinds(PropertyAccessor accessor) => accessor switch
    {
        PropertyAccessor.Get => new[] { SyntaxKind.GetAccessorDeclaration },
        PropertyAccessor.Set => new[] { SyntaxKind.SetAccessorDeclaration },
        PropertyAccessor.Init => new[] { SyntaxKind.GetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration },
        _ => new[] { SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration },
    };

    private static bool IsWritingAccessor(SyntaxKind accessorKind)
        => accessorKind is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration;

    /// <summary>
    /// Generates ONE C# interface property from a property's halves — the <see cref="PropertyDef"/>s
    /// of one name in an interface body (<see cref="GroupPropertiesByName"/>). A lone half keeps its
    /// own shape (<see cref="GenerateInterfacePropertyFromDef"/>). Several halves combine as the class
    /// arm combines them: each function-style accessor contributes its own accessor (abstract when its
    /// body is a stub, a default implementation otherwise), and an auto half contributes the abstract
    /// accessors no function-style half supplies (<c>properties_function_style.md</c>: the auto half
    /// supplies the complementary half). The type is the group's declaring annotation — the one rule
    /// the checker typed the property from.
    /// </summary>
    private PropertyDeclarationSyntax GenerateInterfaceGroupedProperty(List<PropertyDef> propGroup)
    {
        if (propGroup.Count == 1)
            return GenerateInterfacePropertyFromDef(propGroup[0]);

        var first = propGroup[0];
        var propertyName = NameCasing.ResolveMethod(first.Name, first.IsNameBacktickEscaped);
        TypeSyntax propertyType = propGroup.DeclaredTypeAnnotation() is { } declaredAnnotation
            ? _typeMapper.MapType(declaredAnnotation)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var customKinds = propGroup.Where(p => p.IsFunctionStyle)
            .Select(p => InterfaceAccessorKind(p.Accessor))
            .ToList();

        var accessors = new List<AccessorDeclarationSyntax>();
        foreach (var prop in propGroup)
        {
            if (prop.IsFunctionStyle)
            {
                accessors.Add(GenerateInterfaceFunctionStyleAccessor(prop));
                continue;
            }

            // A custom getter replaces the auto getter; a custom setter replaces the auto set/init
            foreach (var kind in AutoHalfAccessorKinds(prop.Accessor))
            {
                bool supplied = IsWritingAccessor(kind)
                    ? customKinds.Any(IsWritingAccessor)
                    : customKinds.Contains(kind);
                if (!supplied)
                {
                    accessors.Add(AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
                }
            }
        }

        return PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));
    }

    private static SyntaxKind InterfaceAccessorKind(PropertyAccessor accessor) => accessor switch
    {
        PropertyAccessor.Set => SyntaxKind.SetAccessorDeclaration,
        PropertyAccessor.Init => SyntaxKind.InitAccessorDeclaration,
        _ => SyntaxKind.GetAccessorDeclaration,
    };

    /// <summary>
    /// A function-style interface accessor: abstract (semicolon-only) when its body is a stub, a
    /// default implementation otherwise.
    /// </summary>
    private AccessorDeclarationSyntax GenerateInterfaceFunctionStyleAccessor(PropertyDef propDef)
    {
        var accessorKind = InterfaceAccessorKind(propDef.Accessor);

        if (AstHelper.IsAbstractStubBody(propDef.Body))
        {
            return AccessorDeclaration(accessorKind)
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        // Default interface property implementation
        ResetMethodScope();

        // A setter's declared parameter maps onto C#'s implicit `value` (#1405) — this arm registered
        // no `value` at all before #1405, the one accessor shape where neither the Sharpy name nor the
        // C# one was ever introduced.
        bool writesValue = propDef.Accessor is PropertyAccessor.Set or PropertyAccessor.Init;
        using var valueRewrite = AccessorParamRewrite(
            writesValue ? AccessorValueParamName(propDef.Parameters) : null, "value");

        var bodyStatements = GenerateSuite(propDef.Body);
        return AccessorDeclaration(accessorKind).WithBody(Block(bodyStatements));
    }

    /// <summary>
    /// Generates a C# interface property from ONE PropertyDef AST node (a property with no other
    /// half in the body). Auto-property accessors are abstract (semicolon-only); a function-style
    /// accessor is abstract when its body is a stub, a default implementation otherwise.
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

        var accessors = propDef.IsFunctionStyle
            ? new List<AccessorDeclarationSyntax> { GenerateInterfaceFunctionStyleAccessor(propDef) }
            : AutoHalfAccessorKinds(propDef.Accessor)
                .Select(kind => AccessorDeclaration(kind).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))
                .ToList();

        return PropertyDeclaration(propertyType, propertyName)
            .WithAccessorList(AccessorList(List(accessors)));
    }

}
