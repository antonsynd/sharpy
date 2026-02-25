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
/// RoslynEmitter partial class: Class member generation (constructors, methods, fields, interface members)
/// </summary>
internal partial class RoslynEmitter
{
    #region Class Member Generation

    private List<MemberDeclarationSyntax> GenerateClassMembers(
        IReadOnlyList<Statement> body, string className, string originalTypeName)
    {
        var members = new List<MemberDeclarationSyntax>();

        // First pass: generate fields and build mappings for use in constructor
        var fieldMapping = new Dictionary<string, string>();
        var fieldTypeMapping = new Dictionary<string, TypeAnnotation>();
        var fieldMembers = new List<MemberDeclarationSyntax>();

        var typeSymbol = _context.LookupSymbol(originalTypeName) as TypeSymbol;
        var previousTypeSymbol = _currentTypeSymbol;
        _currentTypeSymbol = typeSymbol;

        foreach (var stmt in body.Where(s => s is VariableDeclaration))
        {
            var varDecl = (VariableDeclaration)stmt;
            // Generate the field and capture the mangled name
            var fieldSymbol = typeSymbol?.Fields.FirstOrDefault(f => f.Name == varDecl.Name);
            var codeGenInfo = fieldSymbol != null ? GetCodeGenInfo(fieldSymbol) : null;
            var fieldDecl = GenerateField(varDecl, codeGenInfo?.CSharpName);
            fieldMembers.Add(fieldDecl);

            // Extract the field name from the generated declaration
            // The field name is in the VariableDeclarator
            var variable = ((FieldDeclarationSyntax)fieldDecl).Declaration.Variables.First();
            var fieldName = variable.Identifier.Text;
            fieldMapping[varDecl.Name] = fieldName;

            // Also track the field's declared type for contextual type inference
            if (varDecl.Type != null)
            {
                fieldTypeMapping[varDecl.Name] = varDecl.Type;
            }
        }

        // Also register auto-property names in fieldMapping so self.name = value
        // in constructors resolves to this.Name = value
        foreach (var stmt in body.Where(s => s is PropertyDef pd && !pd.IsFunctionStyle))
        {
            var propDef = (PropertyDef)stmt;
            var propName = NameMangler.ToPascalCase(propDef.Name);
            fieldMapping[propDef.Name] = propName;
            if (propDef.Type != null)
            {
                fieldTypeMapping[propDef.Name] = propDef.Type;
            }
        }

        // Add field members first
        members.AddRange(fieldMembers);

        // Second pass: generate methods, constructors, and operator overloads
        // Collect all __init__ methods for constructor generation (supports overloading)
        var initMethods = new List<FunctionDef>();

        // Collect all PropertyDef nodes, grouped by name for combining getter/setter
        var propertyGroups = new Dictionary<string, List<PropertyDef>>();

        // Track which dunder methods are present for complementary operator generation
        var dunders = new HashSet<string>();
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef fd && DunderMapping.IsDunderMethod(fd.Name))
            {
                dunders.Add(fd.Name);
            }
        }

        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef:
                    // Check if this is a constructor (__init__)
                    if (funcDef.Name == DunderNames.Init)
                    {
                        // Collect for later generation (supports multiple overloads)
                        initMethods.Add(funcDef);
                    }
                    // __len__ generates a Count property (not a method) to satisfy ISized
                    else if (funcDef.Name == DunderNames.Len)
                    {
                        members.Add(GenerateLenProperty(funcDef));
                    }
                    // __bool__ generates an IsTrue property + operator true/false
                    else if (funcDef.Name == DunderNames.Bool)
                    {
                        members.Add(GenerateBoolProperty(funcDef));
                        members.Add(GenerateBoolOperatorTrue(className));
                        // operator false is generated in the complementary section below
                    }
                    // __next__ generates IEnumerator<T> protocol members (MoveNext, Current, etc.)
                    else if (funcDef.Name == DunderNames.Next)
                    {
                        members.AddRange(GenerateIteratorProtocolMembers(funcDef));
                    }
                    // __iter__ generates IEnumerable<T> protocol members (GetEnumerator, etc.)
                    else if (funcDef.Name == DunderNames.Iter)
                    {
                        if (dunders.Contains(DunderNames.Next))
                        {
                            // Self-iterating class: __iter__ returns self → GetEnumerator() => this
                            // Get element type from __next__'s return type
                            var nextFunc = body.OfType<FunctionDef>()
                                .FirstOrDefault(f => f.Name == DunderNames.Next);
                            TypeSyntax elemType = nextFunc?.ReturnType != null
                                ? _typeMapper.MapType(nextFunc.ReturnType)
                                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
                            members.AddRange(GenerateEnumerableBridgeMembers(elemType));
                        }
                        else if (_context.SemanticInfo?.IsGenerator(funcDef) == true)
                        {
                            // Generator __iter__: body contains yield → emit
                            // IEnumerator<T> GetEnumerator() with the user's body
                            // plus the non-generic IEnumerable.GetEnumerator() bridge
                            members.AddRange(GenerateGeneratorIterMethod(funcDef));
                        }
                        else
                        {
                            // Iterable-only: just generate GetEnumerator() with user body
                            // C# foreach uses duck-typing (GetEnumerator pattern), so no
                            // IEnumerable<T> synthesis needed for this case
                            members.Add(GenerateClassMethod(funcDef));
                        }
                    }
                    // __reversed__ generates GetReverseEnumerator() with IEnumerator<T> return type
                    else if (funcDef.Name == DunderNames.Reversed)
                    {
                        using var _gen = SetGeneratorScope(_context.SemanticInfo?.IsGenerator(funcDef) == true);
                        members.Add(GenerateReverseEnumeratorMethod(funcDef));
                    }
                    // Check if this is a dunder method that needs operator synthesis
                    else if (DunderMapping.IsDunderMethod(funcDef.Name))
                    {
                        if (funcDef.Name == DunderNames.Eq || funcDef.Name == DunderNames.Ne)
                        {
                            // __eq__/__ne__ keep their current path: Equals()/method + operator ==/ !=
                            // because operator == routes through Equals() for null-safety and IEquatable<T>
                            members.Add(GenerateClassMethod(funcDef));
                            var eqOp = TryGenerateOperatorOverload(funcDef, className);
                            if (eqOp != null)
                                members.Add(eqOp);
                        }
                        else
                        {
                            // Try the inlined path: operator body is inlined, no instance method
                            var inlined = TryGenerateInlinedOperatorOverload(funcDef, className);
                            if (inlined != null)
                            {
                                members.AddRange(inlined);
                            }
                            else
                            {
                                // Fallback for dunders that don't map to operators
                                // (e.g., __getitem__, __setitem__, __str__, __hash__)
                                members.Add(GenerateClassMethod(funcDef));
                            }
                        }
                    }
                    else
                    {
                        members.Add(GenerateClassMethod(funcDef));
                    }
                    break;

                case PropertyDef propDef:
                    // Collect for grouped generation (getter+setter combine into one C# property)
                    if (!propertyGroups.TryGetValue(propDef.Name, out var group))
                    {
                        group = new List<PropertyDef>();
                        propertyGroups[propDef.Name] = group;
                    }
                    group.Add(propDef);
                    break;

                case VariableDeclaration _:
                    // Already processed in first pass
                    break;

                case PassStatement:
                    // Ignore pass in class body
                    break;

                case ExpressionStatement exprStmt when exprStmt.Expression is EllipsisLiteral:
                    // Ignore ellipsis in class body
                    break;

                default:
                    _context.AddError(
                        $"Internal: unrecognized statement type '{stmt.GetType().Name}' in class body was not emitted. This is a compiler bug — please report it.",
                        DiagnosticCodes.CodeGen.UnrecognizedStatementType,
                        stmt.LineStart,
                        stmt.ColumnStart);
                    break;
            }
        }

        // Generate all properties (grouped by name to combine getter/setter)
        foreach (var (propName, propGroup) in propertyGroups)
        {
            members.Add(GenerateGroupedProperty(propGroup));
        }

        // Generate all constructors (supports overloading)
        foreach (var initMethod in initMethods)
        {
            members.Add(GenerateConstructor(initMethod, className, fieldMapping, fieldTypeMapping));
        }

        // Generate complementary operators for C# requirements
        // If __bool__ is defined, operator true was generated above — also generate operator false
        if (dunders.Contains(DunderNames.Bool))
        {
            members.Add(GenerateBoolOperatorFalse(className));
        }

        // If __eq__ is defined but not __ne__, generate operator != for each __eq__ overload
        if (dunders.Contains(DunderNames.Eq) && !dunders.Contains(DunderNames.Ne))
        {
            var eqMethods = body.OfType<FunctionDef>().Where(f => f.Name == DunderNames.Eq);
            foreach (var eqMethod in eqMethods)
            {
                members.Add(GenerateComplementaryNotEqualsOperator(eqMethod, className));
            }
        }
        // If __ne__ is defined but not __eq__, generate operator ==
        if (dunders.Contains(DunderNames.Ne) && !dunders.Contains(DunderNames.Eq))
        {
            members.Add(GenerateComplementaryEqualsOperator(className));
        }

        _currentTypeSymbol = previousTypeSymbol;
        return members;
    }

    private ConstructorDeclarationSyntax GenerateConstructor(
        FunctionDef func,
        string className,
        Dictionary<string, string> fieldMapping,
        Dictionary<string, TypeAnnotation> fieldTypeMapping)
    {
        // Clear declared variables and version tracking for new method scope
        ResetMethodScope();

        // Pre-scan the constructor body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Generate parameters with type annotations, skipping 'self' parameter
        var parameters = func.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        // Create a mapping of parameter names (original) to their mangled names
        var parameterMapping = func.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                p => p.Name,
                p => NameMangler.Transform(p.Name, NameContext.Parameter));

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            // Also track in version map so assignments to parameters work correctly
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Find super().__init__() or self.__init__() anywhere in the body and convert to
        // constructor initializer (: base(...) or : this(...))
        ConstructorInitializerSyntax? constructorInitializer = null;
        int initializerCallIndex = -1;

        for (int i = 0; i < func.Body.Length; i++)
        {
            if (func.Body[i] is ExpressionStatement es &&
                es.Expression is FunctionCall initCall &&
                initCall.Function is MemberAccess ma &&
                ma.Member == DunderNames.Init)
            {
                if (ma.Object is SuperExpression)
                {
                    initializerCallIndex = i;
                    var baseArgs = initCall.Arguments.Select(arg => Argument(GenerateExpression(arg))).ToArray();
                    constructorInitializer = ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        ArgumentList(SeparatedList(baseArgs)));
                    break;
                }

                if (ma.Object is Identifier { Name: PythonNames.Self })
                {
                    initializerCallIndex = i;
                    var thisArgs = initCall.Arguments.Select(arg => Argument(GenerateExpression(arg))).ToArray();
                    constructorInitializer = ConstructorInitializer(
                        SyntaxKind.ThisConstructorInitializer,
                        ArgumentList(SeparatedList(thisArgs)));
                    break;
                }
            }
        }

        var bodyStartIndex = 0;

        if (initializerCallIndex >= 0)
        {
            if (initializerCallIndex == 0)
            {
                // Simple case: initializer call is the first statement, skip it
                bodyStartIndex = 1;
            }
            // else: initializer call is not the first statement.
            // We still emit the initializer and skip the call during body generation.
            // Statements before the call are emitted as regular constructor body.
        }

        // Generate constructor body
        // In Python __init__, assignments like self.name = name set instance fields
        // In C#, these become this.Name = name in the constructor body
        var bodyStatements = new List<StatementSyntax>();

        for (int i = bodyStartIndex; i < func.Body.Length; i++)
        {
            // Skip the initializer call — it was already converted to : base(...) or : this(...)
            if (i == initializerCallIndex)
                continue;

            var stmt = func.Body[i];

            // Convert self.field = value to this.Field = value (capitalized)
            if (stmt is Assignment assign)
            {
                // Check if this is a self.field assignment
                if (assign.Target is MemberAccess memberAccess &&
                    memberAccess.Object is Identifier id &&
                    string.Equals(id.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                {
                    // Look up the field name from the field mapping to ensure consistency
                    // For fields not in mapping (inherited fields), use PascalCase to match
                    // the convention used by GenerateField
                    string fieldName = fieldMapping.TryGetValue(memberAccess.Member, out var mappedFieldName)
                        ? mappedFieldName
                        : NameMangler.ToPascalCase(memberAccess.Member);

                    // Generate: this.Field = value;
                    var thisAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(fieldName));

                    // For the right-hand side, check if it's an identifier that matches a parameter
                    ExpressionSyntax assignValue;
                    if (assign.Value is Identifier valueId && parameterMapping.TryGetValue(valueId.Name, out var mappedName))
                    {
                        assignValue = IdentifierName(mappedName);
                    }
                    else
                    {
                        // Set target type context for collection literal inference (e.g., self.items = [])
                        var previousTargetType = _targetTypeContext;
                        if (fieldTypeMapping.TryGetValue(memberAccess.Member, out var fieldType))
                        {
                            _targetTypeContext = fieldType;
                        }
                        try
                        {
                            assignValue = GenerateExpression(assign.Value);
                        }
                        finally
                        {
                            _targetTypeContext = previousTargetType;
                        }
                    }

                    var selfAssign = ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            thisAccess,
                            assignValue));
                    bodyStatements.Add(AttachLineDirective(selfAssign, stmt));
                }
                else
                {
                    // Other assignments, generate normally
                    bodyStatements.AddRange(GenerateBodyStatements(stmt));
                }
            }
            else
            {
                // Other statements, generate normally
                bodyStatements.AddRange(GenerateBodyStatements(stmt));
            }
        }

        var body = Block(bodyStatements);

        var constructor = ConstructorDeclaration(className)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add constructor initializer if present (: base(...) or : this(...))
        if (constructorInitializer != null)
        {
            constructor = constructor.WithInitializer(constructorInitializer);
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            constructor = constructor.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return constructor;
    }

    private MethodDeclarationSyntax GenerateClassMethod(FunctionDef func)
    {
        // Clear declared variables and version tracking for new method scope
        ResetMethodScope();

        // Check if this method is a generator
        using var _gen = SetGeneratorScope(_context.SemanticInfo?.IsGenerator(func) == true);

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

        // For non-dunder generator methods, wrap return type T in IEnumerable<T>
        if (_isCurrentMethodGenerator && !DunderMapping.IsDunderMethod(func.Name))
        {
            returnType = WrapInIEnumerable(returnType);
        }

        // For async methods, wrap return type in Task<T> or Task
        bool isAsync = func.IsAsync;
        if (isAsync)
        {
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
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Add override keyword for methods that override Object methods
        // Uses the protocol variable already fetched above, plus special handling for operator dunders
        var shouldAddOverride = protocol?.ClrMethodName is "ToString" or "GetHashCode"
            // __eq__ only generates override when parameter type is object
            || (func.Name == DunderNames.Eq && IsEqualsObjectOverload(func));

        if (shouldAddOverride && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.OverrideKeyword));
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

        // Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
        // @static decorator is valid but OPTIONAL/redundant
        bool hasSelfParameter = func.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));

        if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        // Generate parameters with type annotations, skipping 'self' and 'cls' parameters
        var parameters = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, PythonNames.Cls, StringComparison.OrdinalIgnoreCase))
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

        // Generate method declaration
        var method = MethodDeclaration(returnType, mangledName)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)));

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
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Ensure abstract modifier is present for abstract properties
        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

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

        var property = PropertyDeclaration(returnType, "IsTrue")
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
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Ensure abstract modifier is present for abstract properties
        if (isAbstract && !modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.AbstractKeyword));
        }

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

        var property = PropertyDeclaration(returnType, "Count")
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(SingletonList(getter)));

        if (!string.IsNullOrEmpty(func.DocString))
        {
            property = property.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return property;
    }

    /// <summary>
    /// Generates iterator protocol members for a class defining __next__.
    /// Produces: private _current field, private NextImpl() method,
    /// public MoveNext(), Current property, Reset(), Dispose(),
    /// and explicit IEnumerator.Current.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateIteratorProtocolMembers(FunctionDef funcDef)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Determine element type T from __next__ return type
        TypeSyntax elementType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // 1. Private _current field of type T
        members.Add(FieldDeclaration(
            VariableDeclaration(elementType)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier("_current")))))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

        // 2. Private NextImpl() method containing the user's __next__ body
        {
            ResetMethodScope();
            CollectSourceVariableNames(funcDef.Body);

            // Track parameters (skip self)
            foreach (var param in funcDef.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            var body = Block(funcDef.Body.SelectMany(GenerateBodyStatements));

            var nextImpl = MethodDeclaration(elementType, "NextImpl")
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                .WithBody(body);

            members.Add(nextImpl);
        }

        // 3. public bool MoveNext() — wraps NextImpl in try/catch(StopIteration)
        {
            // try { _current = NextImpl(); return true; }
            var tryStatements = new StatementSyntax[]
            {
                ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("_current"),
                        InvocationExpression(IdentifierName("NextImpl")))),
                ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression))
            };

            // catch (Sharpy.StopIteration) { return false; }
            var catchClause = CatchClause()
                .WithDeclaration(CatchDeclaration(
                    QualifiedName(IdentifierName("Sharpy"), IdentifierName("StopIteration"))))
                .WithBlock(Block(ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))));

            var tryStatement = TryStatement()
                .WithBlock(Block(tryStatements))
                .WithCatches(SingletonList(catchClause));

            var moveNext = MethodDeclaration(
                    PredefinedType(Token(SyntaxKind.BoolKeyword)), "MoveNext")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(tryStatement));

            members.Add(moveNext);
        }

        // 4. public T Current => _current; (needed for foreach duck-typing)
        members.Add(PropertyDeclaration(elementType, Identifier("Current"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithExpressionBody(ArrowExpressionClause(IdentifierName("_current")))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // 5. object System.Collections.IEnumerator.Current => _current;
        members.Add(PropertyDeclaration(
                PredefinedType(Token(SyntaxKind.ObjectKeyword)), "Current")
            .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(
                QualifiedName(
                    QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                    IdentifierName("IEnumerator"))))
            .WithExpressionBody(ArrowExpressionClause(IdentifierName("_current")))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // 6. public void Reset() => throw new System.NotSupportedException();
        members.Add(MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)), "Reset")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithExpressionBody(ArrowExpressionClause(
                ThrowExpression(
                    ObjectCreationExpression(ParseTypeName("System.NotSupportedException"))
                        .WithArgumentList(ArgumentList()))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // 7. public void Dispose() { }
        members.Add(MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)), "Dispose")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block()));

        return members;
    }

    /// <summary>
    /// Generates GetReverseEnumerator() for __reversed__ with correct
    /// IEnumerator&lt;T&gt; return type to satisfy IReverseEnumerable&lt;T&gt;.
    /// </summary>
    private MethodDeclarationSyntax GenerateReverseEnumeratorMethod(FunctionDef funcDef)
    {
        // Element type T from __reversed__ return type annotation (defaults to object if absent)
        TypeSyntax elementType = _typeMapper.MapType(funcDef.ReturnType);

        var returnType = WrapInIEnumerator(elementType);

        // Set up method scope — same pattern as GenerateClassMethod
        ResetMethodScope();
        CollectSourceVariableNames(funcDef.Body);

        // Track parameters (skip self) — same as GenerateClassMethod
        foreach (var param in funcDef.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Generate body from user's __reversed__ implementation
        var body = Block(funcDef.Body.SelectMany(GenerateBodyStatements));

        // Build parameter list — skip self
        var parameters = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        var method = MethodDeclaration(returnType, "GetReverseEnumerator")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(funcDef.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(funcDef.DocString));
        }

        return method;
    }

    /// <summary>
    /// Generates IEnumerator&lt;T&gt; GetEnumerator() with the user's generator body
    /// plus the non-generic IEnumerable.GetEnumerator() bridge for generator __iter__.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateGeneratorIterMethod(FunctionDef funcDef)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Element type from __iter__'s return type annotation (defaults to object if absent)
        TypeSyntax elementType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var returnType = WrapInIEnumerator(elementType);

        // Set up method scope
        ResetMethodScope();
        CollectSourceVariableNames(funcDef.Body);

        // Track parameters (skip self)
        foreach (var param in funcDef.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Set generator flag so yield statements and bare returns emit correctly
        using var _gen = SetGeneratorScope(true);

        var body = Block(funcDef.Body.SelectMany(GenerateBodyStatements));

        var parameters = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        var method = MethodDeclaration(returnType, "GetEnumerator")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        if (!string.IsNullOrEmpty(funcDef.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(funcDef.DocString));
        }

        members.Add(method);

        // Non-generic IEnumerable.GetEnumerator() bridge
        members.Add(GenerateNonGenericGetEnumeratorBridge());

        return members;
    }

    /// <summary>
    /// Generates IEnumerable bridge members for a self-iterating class
    /// (one that defines both __iter__ and __next__).
    /// Produces: GetEnumerator() => this, IEnumerable.GetEnumerator() bridge.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateEnumerableBridgeMembers(TypeSyntax elementType)
    {
        var members = new List<MemberDeclarationSyntax>();

        // public IEnumerator<T> GetEnumerator() => this;
        var returnType = QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                IdentifierName("Generic")),
            GenericName("IEnumerator")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));

        members.Add(MethodDeclaration(returnType, "GetEnumerator")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithExpressionBody(ArrowExpressionClause(ThisExpression()))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        members.Add(GenerateNonGenericGetEnumeratorBridge());

        return members;
    }

    /// <summary>
    /// Generates the non-generic IEnumerable.GetEnumerator() explicit interface bridge.
    /// </summary>
    private MethodDeclarationSyntax GenerateNonGenericGetEnumeratorBridge()
    {
        return MethodDeclaration(
                QualifiedName(
                    QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                    IdentifierName("IEnumerator")),
                "GetEnumerator")
            .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(
                QualifiedName(
                    QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                    IdentifierName("IEnumerable"))))
            .WithExpressionBody(ArrowExpressionClause(
                InvocationExpression(IdentifierName("GetEnumerator"))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private SyntaxTokenList GenerateMethodModifiersFromDecorators(IReadOnlyList<Decorator> decorators)
    {
        var tokens = new List<SyntaxToken>();

        // Check for access modifiers
        bool hasAccessModifier = false;
        foreach (var decorator in decorators)
        {
            switch (decorator.Name)
            {
                case "private":
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    hasAccessModifier = true;
                    break;
                case "protected":
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    hasAccessModifier = true;
                    break;
                case "internal":
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    hasAccessModifier = true;
                    break;
                case "public":
                    tokens.Add(Token(SyntaxKind.PublicKeyword));
                    hasAccessModifier = true;
                    break;
            }
        }

        // Default to public if no access modifier specified
        if (!hasAccessModifier)
        {
            tokens.Add(Token(SyntaxKind.PublicKeyword));
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
        var baseType = _context.SemanticBinding.GetBaseType(_currentTypeSymbol) ?? _currentTypeSymbol.BaseType;
        var current = baseType;
        while (current != null)
        {
            if (current.Methods.Any(m => m.Name == methodName && (m.IsVirtual || m.IsAbstract || m.IsOverride)))
                return false; // Found in base class, keep override
            current = _context.SemanticBinding.GetBaseType(current) ?? current.BaseType;
        }

        // No base class method found — check ALL interfaces (abstract or default)
        var interfaceRefs = _context.SemanticBinding.GetInterfaces(_currentTypeSymbol)
            ?? (IReadOnlyList<Semantic.InterfaceReference>)_currentTypeSymbol.Interfaces;
        var interfaces = interfaceRefs.Select(r => r.Definition).ToList();
        foreach (var iface in interfaces)
        {
            if (iface.Methods.Any(m => m.Name == methodName))
                return true;
        }

        return false;
    }

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

        // Fields are public by default (can be changed with decorators later)
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        if (varDecl.IsConst)
        {
            modifiers = modifiers.Add(Token(SyntaxKind.ConstKeyword));
        }

        return FieldDeclaration(declaration)
            .WithModifiers(modifiers);
    }

    private List<MemberDeclarationSyntax> GenerateInterfaceMembers(IReadOnlyList<Statement> body)
    {
        var members = new List<MemberDeclarationSyntax>();

        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef:
                    // Interface methods have no body
                    members.Add(GenerateInterfaceMethod(funcDef));
                    break;

                case PropertyDef propDef:
                    members.Add(GenerateInterfacePropertyFromDef(propDef));
                    break;

                case VariableDeclaration varDecl:
                    // Interface properties (get/set accessors)
                    members.Add(GenerateInterfaceProperty(varDecl));
                    break;

                case PassStatement:
                    // Ignore pass in interface body
                    break;

                case ExpressionStatement exprStmt when exprStmt.Expression is EllipsisLiteral:
                    // Ignore ellipsis in interface body
                    break;

                default:
                    _context.AddError(
                        $"Internal: unrecognized statement type '{stmt.GetType().Name}' in interface body was not emitted. This is a compiler bug — please report it.",
                        DiagnosticCodes.CodeGen.UnrecognizedStatementType,
                        stmt.LineStart,
                        stmt.ColumnStart);
                    break;
            }
        }

        return members;
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
        var parameters = func.Parameters
            .Where(p => p.Name != PythonNames.Self)
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

            var bodyStatements = func.Body
                .SelectMany(GenerateBodyStatements);
            method = method.WithBody(Block(bodyStatements));
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
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
    /// Generates a C# property from a group of PropertyDef AST nodes.
    /// A single PropertyDef produces a single-accessor property.
    /// Multiple PropertyDefs with the same name (e.g., getter + setter) combine
    /// into a single C# property with multiple accessors.
    /// </summary>
    private MemberDeclarationSyntax GenerateGroupedProperty(List<PropertyDef> propGroup)
    {
        if (propGroup.Count == 1)
        {
            var prop = propGroup[0];
            if (prop.IsFunctionStyle)
            {
                return GenerateFunctionStyleProperty(prop);
            }
            return GenerateAutoProperty(prop);
        }

        // Multiple PropertyDef nodes with the same name: combine into one C# property
        return GenerateCombinedFunctionStyleProperty(propGroup);
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
        var modifiers = GenerateMethodModifiersFromDecorators(modifierSource.Decorators);

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
            var accessorModifiers = GenerateMethodModifiersFromDecorators(prop.Decorators);
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
        var modifiers = GenerateMethodModifiersFromDecorators(propDef.Decorators);

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
        var modifiers = GenerateMethodModifiersFromDecorators(propDef.Decorators);

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

    #endregion

    /// <summary>
    /// Check if an __eq__ FunctionDef has parameter type 'object', meaning it
    /// should generate 'override bool Equals(object)' instead of a new overload.
    /// </summary>
    private static bool IsEqualsObjectOverload(FunctionDef func)
    {
        var otherParam = func.Parameters
            .FirstOrDefault(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        return otherParam?.Type is TypeAnnotation { Name: "object" };
    }

}
