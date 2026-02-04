using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Class member generation (constructors, methods, fields, interface members)
/// </summary>
internal partial class RoslynEmitter
{
    #region Class Member Generation

    private List<MemberDeclarationSyntax> GenerateClassMembers(IReadOnlyList<Statement> body, string className)
    {
        var members = new List<MemberDeclarationSyntax>();

        // First pass: generate fields and build mappings for use in constructor
        var fieldMapping = new Dictionary<string, string>();
        var fieldTypeMapping = new Dictionary<string, TypeAnnotation>();
        var fieldMembers = new List<MemberDeclarationSyntax>();

        foreach (var stmt in body.Where(s => s is VariableDeclaration))
        {
            var varDecl = (VariableDeclaration)stmt;
            // Generate the field and capture the mangled name
            var fieldDecl = GenerateField(varDecl);
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

        // Add field members first
        members.AddRange(fieldMembers);

        // Second pass: generate methods, constructors, and operator overloads
        // Collect all __init__ methods for constructor generation (supports overloading)
        var initMethods = new List<FunctionDef>();

        // Track which dunder methods are present for complementary operator generation
        var dunders = new HashSet<string>();
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef fd && NameMangler.IsDunderMethod(fd.Name))
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
                    // Check if this is a dunder method that needs operator synthesis
                    else if (NameMangler.IsDunderMethod(funcDef.Name))
                    {
                        // Dunder methods that map to C# overrides should use the override name
                        // Other dunder methods should preserve their dunder name (e.g., __add__ -> __Add__)
                        // to avoid conflicts with user-defined methods
                        members.Add(GenerateClassMethod(funcDef));

                        // Then try to generate operator overload
                        var operatorMember = TryGenerateOperatorOverload(funcDef, className);
                        if (operatorMember != null)
                        {
                            members.Add(operatorMember);
                        }
                    }
                    else
                    {
                        members.Add(GenerateClassMethod(funcDef));
                    }
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

        // Generate all constructors (supports overloading)
        foreach (var initMethod in initMethods)
        {
            members.Add(GenerateConstructor(initMethod, className, fieldMapping, fieldTypeMapping));
        }

        // Generate complementary operators for C# requirements
        // If __eq__ is defined but not __ne__, generate operator !=
        if (dunders.Contains(DunderNames.Eq) && !dunders.Contains(DunderNames.Ne))
        {
            members.Add(GenerateComplementaryNotEqualsOperator(className));
        }
        // If __ne__ is defined but not __eq__, generate operator ==
        if (dunders.Contains(DunderNames.Ne) && !dunders.Contains(DunderNames.Eq))
        {
            members.Add(GenerateComplementaryEqualsOperator(className));
        }

        return members;
    }

    private ConstructorDeclarationSyntax GenerateConstructor(
        FunctionDef func,
        string className,
        Dictionary<string, string> fieldMapping,
        Dictionary<string, TypeAnnotation> fieldTypeMapping)
    {
        // Clear declared variables and version tracking for new method scope
        _declaredVariables.Clear();
        _variableVersions.Clear();
        _constVariables.Clear();
        _sourceVariableNames.Clear();

        // Pre-scan the constructor body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Generate parameters with type annotations, skipping 'self' parameter
        var parameters = func.Parameters
            .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        // Create a mapping of parameter names (original) to their mangled names
        var parameterMapping = func.Parameters
            .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                p => p.Name,
                p => NameMangler.Transform(p.Name, NameContext.Parameter));

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            if (string.Equals(param.Name, "self", StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            // Also track in version map so assignments to parameters work correctly
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Check if the first statement is a super().__init__() call
        // This needs to be converted to a constructor initializer (: base(...))
        ConstructorInitializerSyntax? baseInitializer = null;
        var bodyStartIndex = 0;

        if (func.Body.Length > 0 && func.Body[0] is ExpressionStatement exprStmt)
        {
            // Check if it's super().__init__(...)
            if (exprStmt.Expression is FunctionCall call &&
                call.Function is MemberAccess memberAccess &&
                memberAccess.Object is SuperExpression &&
                memberAccess.Member == DunderNames.Init)
            {
                // Generate the base constructor arguments
                var baseArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg))).ToArray();

                // Create the base initializer: : base(...)
                baseInitializer = ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SeparatedList(baseArgs)));

                // Skip this statement in the body (it becomes the initializer)
                bodyStartIndex = 1;
            }
        }

        // Generate constructor body
        // In Python __init__, assignments like self.name = name set instance fields
        // In C#, these become this.Name = name in the constructor body
        var bodyStatements = new List<StatementSyntax>();

        for (int i = bodyStartIndex; i < func.Body.Length; i++)
        {
            var stmt = func.Body[i];

            // Convert self.field = value to this.Field = value (capitalized)
            if (stmt is Assignment assign)
            {
                // Check if this is a self.field assignment
                if (assign.Target is MemberAccess memberAccess &&
                    memberAccess.Object is Identifier id &&
                    string.Equals(id.Name, "self", StringComparison.OrdinalIgnoreCase))
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
                    var genStmt = GenerateBodyStatement(stmt);
                    if (genStmt != null)
                    {
                        bodyStatements.Add(genStmt);
                    }
                }
            }
            else
            {
                // Other statements, generate normally
                var genStmt = GenerateBodyStatement(stmt);
                if (genStmt != null)
                {
                    bodyStatements.Add(genStmt);
                }
            }
        }

        var body = Block(bodyStatements);

        var constructor = ConstructorDeclaration(className)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add base initializer if present
        if (baseInitializer != null)
        {
            constructor = constructor.WithInitializer(baseInitializer);
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
        _declaredVariables.Clear();
        _variableVersions.Clear();
        _constVariables.Clear();
        _sourceVariableNames.Clear();

        // Pre-scan the method body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // For class methods, use the same logic as module functions but handle special cases
        // Transform name using NameMangler (handles dunder methods automatically)
        var mangledName = NameMangler.Transform(func.Name, NameContext.Method);

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
                "str" or "string" => PredefinedType(Token(SyntaxKind.StringKeyword)),
                "int" => PredefinedType(Token(SyntaxKind.IntKeyword)),
                "bool" => PredefinedType(Token(SyntaxKind.BoolKeyword)),
                "None" or "void" => PredefinedType(Token(SyntaxKind.VoidKeyword)),
                _ => func.ReturnType != null ? _typeMapper.MapType(func.ReturnType) : returnType
            };
        }

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Add override keyword for methods that override Object methods
        // Uses the protocol variable already fetched above, plus special handling for operator dunders
        var shouldAddOverride = protocol?.ClrMethodName is "ToString" or "GetHashCode"
            // __repr__ maps to ToString but has ClrMethodName: null, so check explicitly
            || func.Name == DunderNames.Repr
            // __eq__ is an operator dunder (not in ProtocolRegistry) but maps to Equals() override
            || func.Name == DunderNames.Eq;

        if (shouldAddOverride && !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.OverrideKeyword));
        }

        // Primary mechanism: Method is static if it doesn't have 'self' parameter (Pythonic)
        // @static decorator is valid but OPTIONAL/redundant
        bool hasSelfParameter = func.Parameters.Any(p =>
            string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));

        if (!hasSelfParameter && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
        }

        // Generate parameters with type annotations, skipping 'self' and 'cls' parameters
        var parameters = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, "cls", StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        // Special handling for Equals() - parameter should be object type
        if (func.Name == DunderNames.Eq && parameters.Length > 0)
        {
            var objParam = Parameter(Identifier(parameters[0].Identifier.Text))
                .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)));
            parameters = new[] { objParam };
        }

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            if (string.Equals(param.Name, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(param.Name, "cls", StringComparison.OrdinalIgnoreCase))
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
        bool hasAbstractDecorator = func.Decorators.Any(d => d.Name == "abstract");
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

        // Abstract methods must not have a body in C#
        if (isAbstract)
        {
            method = method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }
        else
        {
            // Generate method body for concrete methods
            var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            method = method.WithBody(body);
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
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
                case "static":
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case "abstract":
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case "virtual":
                    tokens.Add(Token(SyntaxKind.VirtualKeyword));
                    break;
                case "override":
                    tokens.Add(Token(SyntaxKind.OverrideKeyword));
                    break;
            }
        }

        return TokenList(tokens);
    }

    private FieldDeclarationSyntax GenerateField(VariableDeclaration varDecl)
    {
        // Use PascalCase for public fields (C# property-like convention)
        var fieldName = NameMangler.ToPascalCase(varDecl.Name);

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
        var mangledName = NameMangler.Transform(func.Name, NameContext.Method);

        // Determine return type from annotation or infer void
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Interface methods have no modifiers and no body
        var parameters = func.Parameters
            .Where(p => p.Name != "self")
            .Select(GenerateParameter)
            .ToArray();

        var method = MethodDeclaration(returnType, mangledName)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

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

    #endregion


}
