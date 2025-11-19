using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Generates C# code using Roslyn syntax trees
/// </summary>
public class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    private readonly HashSet<string> _declaredVariables = new();
    private int _tempVarCounter = 0;

    // Common .NET namespace acronyms that should be all uppercase
    private static readonly HashSet<string> UpperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
        "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
    };

    public RoslynEmitter(CodeGenContext context)
    {
        _context = context;
        _typeMapper = new TypeMapper(context);
    }

    public CompilationUnitSyntax GenerateCompilationUnit(Module module)
    {
        // Collect all using directives from import statements
        var usingDirectives = GenerateUsingDirectives(module);

        // Separate imports from other statements
        var nonImportStatements = module.Body
            .Where(s => s is not ImportStatement && s is not FromImportStatement)
            .ToList();

        // Generate module class wrapper with non-import statements
        var moduleClass = GenerateModuleClass(nonImportStatements);

        // Generate namespace from source file path (if available)
        var namespaceName = GenerateNamespaceName();
        var namespaceDecl = FileScopedNamespaceDeclaration(namespaceName)
            .WithMembers(SingletonList<MemberDeclarationSyntax>(moduleClass));

        return CompilationUnit()
            .WithUsings(List(usingDirectives))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDecl))
            .NormalizeWhitespace();
    }

    private NameSyntax GenerateNamespaceName()
    {
        // Get namespace from context source file path
        // Default to "SharpyGenerated" if no source file specified
        if (string.IsNullOrEmpty(_context.SourceFilePath))
        {
            return ParseName("SharpyGenerated");
        }

        // Convert file path to namespace
        // e.g., "src/myapp/utils.spy" -> "Myapp.Utils"
        var path = Path.GetDirectoryName(_context.SourceFilePath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);

        // Split path and filter out common directory names
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != "src" && p != "lib" && p != ".")
            .Select(p => SimpleToPascalCase(p))
            .ToList();

        // Add file name as final namespace component
        if (!string.IsNullOrEmpty(fileName))
        {
            parts.Add(SimpleToPascalCase(fileName));
        }

        // If no parts, use default
        if (parts.Count == 0)
        {
            return ParseName("SharpyGenerated");
        }

        // Build namespace name
        return ParseName(string.Join(".", parts));
    }

    private List<UsingDirectiveSyntax> GenerateUsingDirectives(Module module)
    {
        var usings = new List<UsingDirectiveSyntax>();

        // Add default System usings
        usings.Add(UsingDirective(ParseName("System")));
        usings.Add(UsingDirective(ParseName("System.Collections.Generic")));
        usings.Add(UsingDirective(ParseName("System.Linq")));

        // Add Sharpy runtime usings
        usings.Add(UsingDirective(ParseName("Sharpy.Core")));

        // Process import statements
        foreach (var stmt in module.Body)
        {
            if (stmt is ImportStatement importStmt)
            {
                usings.AddRange(GenerateImportUsings(importStmt));
            }
            else if (stmt is FromImportStatement fromImportStmt)
            {
                usings.AddRange(GenerateFromImportUsings(fromImportStmt));
            }
        }

        // Deduplicate using directives by their normalized string representation
        var seen = new HashSet<string>();
        var dedupedUsings = new List<UsingDirectiveSyntax>();
        foreach (var u in usings)
        {
            var key = u.NormalizeWhitespace().ToFullString();
            if (seen.Add(key))
            {
                dedupedUsings.Add(u);
            }
        }
        return dedupedUsings;
    }

    private IEnumerable<UsingDirectiveSyntax> GenerateImportUsings(ImportStatement import)
    {
        foreach (var alias in import.Names)
        {
            // Convert Python module name to C# namespace
            // e.g., "system.io" -> "System.IO"
            var namespaceName = ConvertModuleNameToNamespace(alias.Name);

            if (alias.AsName != null)
            {
                // import module as alias -> using alias = Module;
                yield return UsingDirective(
                    NameEquals(alias.AsName),
                    ParseName(namespaceName));
            }
            else
            {
                // import module -> using Module;
                yield return UsingDirective(ParseName(namespaceName));
            }
        }
    }

    private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
    {
        // Convert module name to namespace
        var namespaceName = ConvertModuleNameToNamespace(fromImport.Module);

        // from module import * -> using Module;
        // from module import Name -> using Module;
        // Note: C# doesn't have direct equivalent to Python's selective imports
        // All types from the namespace become available
        yield return UsingDirective(ParseName(namespaceName));
    }

    private string ConvertModuleNameToNamespace(string moduleName)
    {
        // Convert Python module naming to C# namespace naming
        // e.g., "system.io" -> "System.IO"
        // e.g., "my_module.sub_module" -> "MyModule.SubModule"

        // Note: We don't use NameMangler.Transform here because:
        // 1. It tracks unique names which causes "system" to become System, System1, System2, etc.
        // 2. Namespaces should use simple PascalCase without uniqueness tracking

        var parts = moduleName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var convertedParts = parts.Select(part => SimpleToPascalCase(part));
        return string.Join(".", convertedParts);
    }

    private static string SimpleToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped)
        if (name.StartsWith("`") && name.EndsWith("`"))
        {
            if (name.Length <= 2)
                return name;
            return name[1..^1];
        }

        // Check if this is a known acronym that should be all uppercase
        if (UpperCaseAcronyms.Contains(name))
        {
            return name.ToUpperInvariant();
        }

        // Split by underscore and capitalize each part
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);

        // Handle edge case where name is only underscores (e.g., "___")
        if (parts.Length == 0)
            return name;

        var result = string.Join("", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")
        ));

        return result;
    }

    private ClassDeclarationSyntax GenerateModuleClass(List<Statement> statements)
    {
        // Separate declarations (class members) from executable statements
        var declarations = new List<MemberDeclarationSyntax>();
        var executableStatements = new List<Statement>();
        bool hasMainFunction = false;

        foreach (var stmt in statements)
        {
            // Check if this is a main function
            if (stmt is FunctionDef funcDef && funcDef.Name == "main")
            {
                hasMainFunction = true;
            }

            var member = GenerateStatement(stmt);
            if (member is MemberDeclarationSyntax memberDecl)
            {
                declarations.Add(memberDecl);
            }
            else
            {
                // This is an executable statement (expression, assignment, etc.)
                executableStatements.Add(stmt);
            }
        }

        // If there are executable statements, we need to handle them
        if (executableStatements.Count > 0)
        {
            if (!hasMainFunction)
            {
                // No main function - create a Main method for executable statements
                // Clear declared variables for Main method scope
                _declaredVariables.Clear();

                var mainBody = Block(executableStatements
                    .Select(GenerateBodyStatement)
                    .OfType<StatementSyntax>());

                var mainMethod = MethodDeclaration(
                        PredefinedType(Token(SyntaxKind.VoidKeyword)),
                        "Main")
                    .WithModifiers(TokenList(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword)))
                    .WithBody(mainBody);

                declarations.Add(mainMethod);
            }
            else
            {
                // There's a main function - put executable statements in module initializer
                // For now, just ignore them or add to Main after the user's main is called
                // This is a corner case we'll handle later
                Console.WriteLine($"Warning: {executableStatements.Count} module-level statement(s) ignored because a 'main' function is defined");
            }
        }

        return ClassDeclaration("__Module__")
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithMembers(List(declarations));
    }

    private SyntaxNode? GenerateStatement(Statement stmt)
    {
        return stmt switch
        {
            FunctionDef funcDef => GenerateFunctionDeclaration(funcDef),
            ClassDef classDef => GenerateClassDeclaration(classDef),
            StructDef structDef => GenerateStructDeclaration(structDef),
            InterfaceDef interfaceDef => GenerateInterfaceDeclaration(interfaceDef),
            EnumDef enumDef => GenerateEnumDeclaration(enumDef),
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            // Add more statement types...
            _ => null
        };
    }

    private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
    {
        // Clear declared variables for new function scope
        _declaredVariables.Clear();

        // Transform name using NameMangler
        var mangledName = NameMangler.Transform(func.Name, NameContext.Method);

        // Determine return type from annotation or infer void
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Process decorators to determine modifiers
        var modifiers = GenerateModifiersFromDecorators(func.Decorators);

        // Generate parameters with type annotations
        var parameters = func.Parameters
            .Select(GenerateParameter)
            .ToArray();

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
        }

        // Generate method body
        var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        var method = MethodDeclaration(returnType, mangledName)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
    }

    private ParameterSyntax GenerateParameter(Parameter param)
    {
        var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);

        // Get parameter type from annotation or default to object
        TypeSyntax paramType = param.Type != null
            ? _typeMapper.MapType(param.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var parameter = Parameter(Identifier(paramName))
            .WithType(paramType);

        // Add default value if present
        if (param.DefaultValue != null)
        {
            var defaultExpr = GenerateExpression(param.DefaultValue);
            parameter = parameter.WithDefault(EqualsValueClause(defaultExpr));
        }

        return parameter;
    }

    private SyntaxTokenList GenerateModifiersFromDecorators(List<Decorator> decorators)
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
                case "staticmethod":
                case "static":
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case "abstractmethod":
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

        // For module-level functions, add static modifier if not already present
        // and if it's not a method (we'll handle this differently in classes)
        if (!tokens.Any(t => t.IsKind(SyntaxKind.StaticKeyword) ||
                            t.IsKind(SyntaxKind.AbstractKeyword) ||
                            t.IsKind(SyntaxKind.VirtualKeyword) ||
                            t.IsKind(SyntaxKind.OverrideKeyword)))
        {
            tokens.Add(Token(SyntaxKind.StaticKeyword));
        }

        return TokenList(tokens);
    }

    private SyntaxTriviaList GenerateXmlDocComment(string docString)
    {
        // Convert Python docstring to C# XML documentation
        var lines = docString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        var triviaList = new List<SyntaxTrivia>
        {
            Comment("/// <summary>"),
            EndOfLine("\n")
        };

        triviaList.AddRange(lines
            .Select(line => line.Trim())
            .Where(trimmedLine => !string.IsNullOrEmpty(trimmedLine))
            .SelectMany(trimmedLine => new[]
            {
                Comment($"/// {trimmedLine}"),
                EndOfLine("\n")
            }));

        triviaList.Add(Comment("/// </summary>"));
        triviaList.Add(EndOfLine("\n"));

        return TriviaList(triviaList);
    }

    #region Class, Struct, Interface, and Enum Generation

    private ClassDeclarationSyntax GenerateClassDeclaration(ClassDef classDef)
    {
        // Transform class name
        var className = NameMangler.Transform(classDef.Name, NameContext.Type);

        // Process decorators to determine modifiers
        var modifiers = GenerateTypeModifiersFromDecorators(classDef.Decorators);

        // Create class declaration
        var classDecl = ClassDeclaration(className)
            .WithModifiers(modifiers);

        // Add type parameters if generic
        if (classDef.TypeParameters.Count > 0)
        {
            var typeParams = classDef.TypeParameters
                .Select(tp => TypeParameter(tp))
                .ToArray();
            classDecl = classDecl.WithTypeParameterList(
                TypeParameterList(SeparatedList(typeParams)));
        }

        // Add base class and interfaces
        if (classDef.BaseClasses.Count > 0)
        {
            var baseTypes = classDef.BaseClasses
                .Select(bc => SimpleBaseType(_typeMapper.MapType(bc)))
                .ToArray();
            classDecl = classDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
        }

        // Generate class members from body
        var members = GenerateClassMembers(classDef.Body, className);
        classDecl = classDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(classDef.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(classDef.DocString));
        }

        return classDecl;
    }

    private StructDeclarationSyntax GenerateStructDeclaration(StructDef structDef)
    {
        // Transform struct name
        var structName = NameMangler.Transform(structDef.Name, NameContext.Type);

        // Process decorators to determine modifiers
        var modifiers = GenerateTypeModifiersFromDecorators(structDef.Decorators);

        // Create struct declaration
        var structDecl = StructDeclaration(structName)
            .WithModifiers(modifiers);

        // Add type parameters if generic
        if (structDef.TypeParameters.Count > 0)
        {
            var typeParams = structDef.TypeParameters
                .Select(tp => TypeParameter(tp))
                .ToArray();
            structDecl = structDecl.WithTypeParameterList(
                TypeParameterList(SeparatedList(typeParams)));
        }

        // Add interfaces (structs can only implement interfaces, not inherit)
        if (structDef.BaseClasses.Count > 0)
        {
            var baseTypes = structDef.BaseClasses
                .Select(bc => SimpleBaseType(_typeMapper.MapType(bc)))
                .ToArray();
            structDecl = structDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
        }

        // Generate struct members from body
        var members = GenerateClassMembers(structDef.Body, structName);
        structDecl = structDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(structDef.DocString))
        {
            structDecl = structDecl.WithLeadingTrivia(GenerateXmlDocComment(structDef.DocString));
        }

        return structDecl;
    }

    private InterfaceDeclarationSyntax GenerateInterfaceDeclaration(InterfaceDef interfaceDef)
    {
        // Transform interface name using Interface context to preserve I prefix pattern
        var interfaceName = NameMangler.Transform(interfaceDef.Name, NameContext.Interface);

        // Interfaces are always public by default
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        // Create interface declaration
        var interfaceDecl = InterfaceDeclaration(interfaceName)
            .WithModifiers(modifiers);

        // Add type parameters if generic
        if (interfaceDef.TypeParameters.Count > 0)
        {
            var typeParams = interfaceDef.TypeParameters
                .Select(tp => TypeParameter(tp))
                .ToArray();
            interfaceDecl = interfaceDecl.WithTypeParameterList(
                TypeParameterList(SeparatedList(typeParams)));
        }

        // Add base interfaces
        if (interfaceDef.BaseInterfaces.Count > 0)
        {
            var baseTypes = interfaceDef.BaseInterfaces
                .Select(bi => SimpleBaseType(_typeMapper.MapType(bi)))
                .ToArray();
            interfaceDecl = interfaceDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
        }

        // Generate interface members (methods only, no implementation)
        var members = GenerateInterfaceMembers(interfaceDef.Body);
        interfaceDecl = interfaceDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(interfaceDef.DocString))
        {
            interfaceDecl = interfaceDecl.WithLeadingTrivia(GenerateXmlDocComment(interfaceDef.DocString));
        }

        return interfaceDecl;
    }

    private EnumDeclarationSyntax GenerateEnumDeclaration(EnumDef enumDef)
    {
        // Transform enum name
        var enumName = NameMangler.Transform(enumDef.Name, NameContext.Type);

        // Enums are always public by default
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        // Generate enum members
        var members = enumDef.Members
            .Select(GenerateEnumMember)
            .ToArray();

        var enumDecl = EnumDeclaration(enumName)
            .WithModifiers(modifiers)
            .WithMembers(SeparatedList(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(enumDef.DocString))
        {
            enumDecl = enumDecl.WithLeadingTrivia(GenerateXmlDocComment(enumDef.DocString));
        }

        return enumDecl;
    }

    private EnumMemberDeclarationSyntax GenerateEnumMember(EnumMember member)
    {
        // Use constant case transformation for enum members
        var memberName = NameMangler.Transform(member.Name, NameContext.Constant);

        var enumMember = EnumMemberDeclaration(Identifier(memberName));

        // Add explicit value if present
        if (member.Value != null)
        {
            var valueExpr = GenerateExpression(member.Value);
            enumMember = enumMember.WithEqualsValue(EqualsValueClause(valueExpr));
        }

        return enumMember;
    }

    private SyntaxTokenList GenerateTypeModifiersFromDecorators(List<Decorator> decorators)
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
                case "abstract":
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case "sealed":
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                    break;
                case "static":
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
            }
        }

        return TokenList(tokens);
    }

    private List<MemberDeclarationSyntax> GenerateClassMembers(List<Statement> body, string className)
    {
        var members = new List<MemberDeclarationSyntax>();

        // First pass: generate fields and build a mapping for use in constructor
        var fieldMapping = new Dictionary<string, string>();
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
        }

        // Add field members first
        members.AddRange(fieldMembers);

        // Second pass: generate methods, constructors, and operator overloads
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
                    if (funcDef.Name == "__init__")
                    {
                        // Generate constructor with field mapping
                        members.Add(GenerateConstructor(funcDef, className, fieldMapping));
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
                    // Ignore other statements for now
                    break;
            }
        }

        // Generate complementary operators for C# requirements
        // If __eq__ is defined but not __ne__, generate operator !=
        if (dunders.Contains("__eq__") && !dunders.Contains("__ne__"))
        {
            members.Add(GenerateComplementaryNotEqualsOperator(className));
        }
        // If __ne__ is defined but not __eq__, generate operator ==
        if (dunders.Contains("__ne__") && !dunders.Contains("__eq__"))
        {
            members.Add(GenerateComplementaryEqualsOperator(className));
        }

        return members;
    }

    private ConstructorDeclarationSyntax GenerateConstructor(FunctionDef func, string className, Dictionary<string, string> fieldMapping)
    {
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

        // Generate constructor body
        // In Python __init__, assignments like self.name = name set instance fields
        // In C#, these become this.Name = name in the constructor body
        var bodyStatements = new List<StatementSyntax>();

        foreach (var stmt in func.Body)
        {
            // Convert self.field = value to this.Field = value (capitalized)
            if (stmt is Assignment assign)
            {
                // Check if this is a self.field assignment
                if (assign.Target is MemberAccess memberAccess &&
                    memberAccess.Object is Identifier id &&
                    string.Equals(id.Name, "self", StringComparison.OrdinalIgnoreCase))
                {
                    // Look up the field name from the field mapping to ensure consistency
                    string fieldName = fieldMapping.TryGetValue(memberAccess.Member, out var mappedFieldName)
                        ? mappedFieldName
                        : NameMangler.Transform(memberAccess.Member, NameContext.Type);

                    // Generate: this.Field = value;
                    var thisAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(fieldName));

                    // For the right-hand side, check if it's an identifier that matches a parameter
                    var assignValue = (assign.Value is Identifier valueId && parameterMapping.TryGetValue(valueId.Name, out var mappedName))
                        ? IdentifierName(mappedName)
                        : GenerateExpression(assign.Value);

                    bodyStatements.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            thisAccess,
                            assignValue)));
                }
                else
                {
                    // Other assignments, generate normally
                    bodyStatements.Add(GenerateBodyStatement(stmt));
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

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            constructor = constructor.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return constructor;
    }

    private MethodDeclarationSyntax GenerateClassMethod(FunctionDef func)
    {
        // For class methods, use the same logic as module functions but handle special cases
        // Transform name using NameMangler (handles dunder methods automatically)
        var mangledName = NameMangler.Transform(func.Name, NameContext.Method);

        // Determine return type from annotation or infer void
        // Default to void if no return type specified
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Special handling for known override methods
        if (func.Name == "__str__" || func.Name == "__repr__")
        {
            // ToString() should return string
            returnType = PredefinedType(Token(SyntaxKind.StringKeyword));
        }
        else if (func.Name == "__eq__")
        {
            // Equals() should return bool and take object parameter
            returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));
        }
        else if (func.Name == "__hash__")
        {
            // GetHashCode() should return int
            returnType = PredefinedType(Token(SyntaxKind.IntKeyword));
        }

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Add override keyword for methods that override Object methods
        // Add override keyword for methods that override Object methods, if not already present
        if ((func.Name == "__str__" || func.Name == "__repr__" ||
            func.Name == "__eq__" || func.Name == "__hash__") &&
            !modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            modifiers = modifiers.Add(Token(SyntaxKind.OverrideKeyword));
        }

        // Generate parameters with type annotations, skipping 'self' and 'cls' parameters
        var parameters = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, "cls", StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        // Special handling for Equals() - parameter should be object type
        if (func.Name == "__eq__" && parameters.Length > 0)
        {
            var objParam = Parameter(Identifier(parameters[0].Identifier.Text))
                .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)));
            parameters = new[] { objParam };
        }

        // Generate method body
        var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        var method = MethodDeclaration(returnType, mangledName)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return method;
    }

    private SyntaxTokenList GenerateMethodModifiersFromDecorators(List<Decorator> decorators)
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
                case "staticmethod":
                case "static":
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case "abstractmethod":
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
        var fieldName = NameMangler.Transform(varDecl.Name, NameContext.Type);

        // Get field type from annotation or default to object
        TypeSyntax fieldType = varDecl.Type != null
            ? _typeMapper.MapType(varDecl.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var variable = VariableDeclarator(Identifier(fieldName));

        // Add initializer if present
        if (varDecl.InitialValue != null)
        {
            var initExpr = GenerateExpression(varDecl.InitialValue);
            variable = variable.WithInitializer(EqualsValueClause(initExpr));
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

    private List<MemberDeclarationSyntax> GenerateInterfaceMembers(List<Statement> body)
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

                case PassStatement:
                    // Ignore pass in interface body
                    break;

                case ExpressionStatement exprStmt when exprStmt.Expression is EllipsisLiteral:
                    // Ignore ellipsis in interface body
                    break;

                default:
                    // Ignore other statements
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

    #endregion


    private StatementSyntax? GenerateBodyStatement(Statement stmt)
    {
        return stmt switch
        {
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            VariableDeclaration varDecl => GenerateVariableDeclaration(varDecl),
            ExpressionStatement exprStmt => ExpressionStatement(GenerateExpression(exprStmt.Expression)),
            PassStatement => EmptyStatement(),
            Sharpy.Compiler.Parser.Ast.BreakStatement => SyntaxFactory.BreakStatement(),
            Sharpy.Compiler.Parser.Ast.ContinueStatement => SyntaxFactory.ContinueStatement(),
            AssertStatement assert => GenerateAssert(assert),
            RaiseStatement raise => GenerateRaise(raise),
            IfStatement ifStmt => GenerateIf(ifStmt),
            WhileStatement whileStmt => GenerateWhile(whileStmt),
            ForStatement forStmt => GenerateFor(forStmt),
            TryStatement tryStmt => GenerateTry(tryStmt),
            _ => null
        };
    }

    private ReturnStatementSyntax GenerateReturn(ReturnStatement ret)
    {
        if (ret.Value != null)
        {
            return ReturnStatement(GenerateExpression(ret.Value));
        }
        return ReturnStatement();
    }

    private StatementSyntax GenerateAssignment(Assignment assign)
    {
        var value = GenerateExpression(assign.Value);

        // Handle simple identifier assignment
        if (assign.Target is Identifier name)
        {
            var varName = NameMangler.ToCamelCase(name.Name);

            // Check if this is a simple assignment or augmented assignment
            if (assign.Operator == AssignmentOperator.Assign)
            {
                // Simple assignment: x = value
                // Check if this is the first time we're seeing this variable
                if (!_declaredVariables.Contains(varName))
                {
                    // First assignment - treat as declaration
                    _declaredVariables.Add(varName);
                    var declaration = VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))
                                .WithInitializer(EqualsValueClause(value))));

                    return LocalDeclarationStatement(declaration);
                }
                else
                {
                    // Reassignment to existing variable
                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(varName),
                            value));
                }
            }
            else
            {
                // Augmented assignment: x += value
                var left = IdentifierName(varName);
                var binaryOp = GetAugmentedAssignmentOperator(assign.Operator);
                var augmentedValue = BinaryExpression(binaryOp, left, value);

                return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        left,
                        augmentedValue));
            }
        }

        // Handle index assignment: arr[0] = value
        if (assign.Target is IndexAccess indexAccess)
        {
            var obj = GenerateExpression(indexAccess.Object);
            var index = GenerateExpression(indexAccess.Index);

            var elementAccess = ElementAccessExpression(obj)
                .WithArgumentList(BracketedArgumentList(
                    SingletonSeparatedList(Argument(index))));

            var assignmentValue = assign.Operator == AssignmentOperator.Assign
                ? value
                : BinaryExpression(GetAugmentedAssignmentOperator(assign.Operator), elementAccess, value);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    elementAccess,
                    assignmentValue));
        }

        // Handle member assignment: obj.field = value
        if (assign.Target is MemberAccess memberAccess)
        {
            var target = GenerateMemberAccess(memberAccess);

            var assignmentValue = assign.Operator == AssignmentOperator.Assign
                ? value
                : BinaryExpression(GetAugmentedAssignmentOperator(assign.Operator), target, value);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    target,
                    assignmentValue));
        }

        // Handle tuple unpacking: x, y = 1, 2
        if (assign.Target is TupleLiteral tuple)
        {
            // Generate C# tuple deconstruction
            // C#: var (x, y) = (1, 2) or (x, y) = (1, 2) for existing variables

            // Check if all elements are identifiers
            bool allIdentifiers = tuple.Elements.All(e => e is Identifier);

            if (allIdentifiers)
            {
                var identifiers = tuple.Elements.Cast<Identifier>().ToList();

                // Check if all are new variables (not yet declared)
                bool allNew = identifiers.All(id => !_declaredVariables.Contains(NameMangler.ToCamelCase(id.Name)));

                if (allNew)
                {
                    // Use: var (x, y) = expr
                    var variables = identifiers
                        .Select(id =>
                        {
                            var varName = NameMangler.ToCamelCase(id.Name);
                            _declaredVariables.Add(varName);
                            return SingleVariableDesignation(Identifier(varName));
                        })
                        .ToList();

                    var tuplePattern = ParenthesizedVariableDesignation(
                        SeparatedList<VariableDesignationSyntax>(variables));

                    // Create a declaration expression
                    var declExpr = DeclarationExpression(
                        IdentifierName("var"),
                        tuplePattern);

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            declExpr,
                            value));
                }
                else
                {
                    // Use: (x, y) = expr for existing variables
                    var tupleElements = identifiers
                        .Select(id =>
                        {
                            var varName = NameMangler.ToCamelCase(id.Name);
                            if (!_declaredVariables.Contains(varName))
                            {
                                _declaredVariables.Add(varName);
                            }
                            return Argument(IdentifierName(varName));
                        });

                    var tupleExpr = TupleExpression(SeparatedList(tupleElements));

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            tupleExpr,
                            value));
                }
            }

            throw new NotImplementedException("Complex tuple unpacking (non-identifier targets) not yet supported");
        }

        throw new NotImplementedException($"Assignment target type not supported: {assign.Target.GetType().Name}");
    }

    private SyntaxKind GetAugmentedAssignmentOperator(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => SyntaxKind.AddExpression,
            AssignmentOperator.MinusAssign => SyntaxKind.SubtractExpression,
            AssignmentOperator.StarAssign => SyntaxKind.MultiplyExpression,
            AssignmentOperator.SlashAssign => SyntaxKind.DivideExpression,
            AssignmentOperator.PercentAssign => SyntaxKind.ModuloExpression,
            AssignmentOperator.AndAssign => SyntaxKind.BitwiseAndExpression,
            AssignmentOperator.OrAssign => SyntaxKind.BitwiseOrExpression,
            AssignmentOperator.XorAssign => SyntaxKind.ExclusiveOrExpression,
            AssignmentOperator.LeftShiftAssign => SyntaxKind.LeftShiftExpression,
            AssignmentOperator.RightShiftAssign => SyntaxKind.RightShiftExpression,
            // Special cases for floor division and power
            AssignmentOperator.DoubleSlashAssign => SyntaxKind.DivideExpression, // Will need cast to int
            AssignmentOperator.PowerAssign => SyntaxKind.None, // Will need Math.Pow
            _ => throw new NotImplementedException($"Augmented assignment operator not supported: {op}")
        };
    }

    private StatementSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        var varName = varDecl.IsConst
            ? NameMangler.ToConstantCase(varDecl.Name)
            : NameMangler.ToCamelCase(varDecl.Name);
        var typeSyntax = _typeMapper.MapType(varDecl.Type);

        // Track this variable as declared
        _declaredVariables.Add(varName);

        VariableDeclaratorSyntax declarator;
        if (varDecl.InitialValue != null)
        {
            var value = GenerateExpression(varDecl.InitialValue);
            declarator = VariableDeclarator(Identifier(varName))
                .WithInitializer(EqualsValueClause(value));
        }
        else
        {
            declarator = VariableDeclarator(Identifier(varName));
        }

        var declaration = VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(declarator));

        var modifiers = varDecl.IsConst
            ? TokenList(Token(SyntaxKind.ConstKeyword))
            : TokenList();

        return LocalDeclarationStatement(declaration)
            .WithModifiers(modifiers);
    }

    private StatementSyntax GenerateAssert(AssertStatement assert)
    {
        // assert condition, message → Debug.Assert(condition, message)
        var condition = GenerateExpression(assert.Test);

        InvocationExpressionSyntax invocation;
        if (assert.Message != null)
        {
            var message = GenerateExpression(assert.Message);
            invocation = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System.Diagnostics.Debug"),
                    IdentifierName("Assert")))
                .AddArgumentListArguments(
                    Argument(condition),
                    Argument(message));
        }
        else
        {
            invocation = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System.Diagnostics.Debug"),
                    IdentifierName("Assert")))
                .AddArgumentListArguments(Argument(condition));
        }

        return ExpressionStatement(invocation);
    }

    private StatementSyntax GenerateRaise(RaiseStatement raise)
    {
        if (raise.Exception != null)
        {
            var exception = GenerateExpression(raise.Exception);
            return ThrowStatement(exception);
        }

        // Re-throw the current exception
        return ThrowStatement();
    }

    private StatementSyntax GenerateIf(IfStatement ifStmt)
    {
        var condition = GenerateExpression(ifStmt.Test);
        var thenBlock = Block(ifStmt.ThenBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        ElseClauseSyntax? elseClause = null;

        // Process elif clauses from last to first to build nested if-else structure
        if (ifStmt.ElifClauses.Count > 0 || ifStmt.ElseBody.Count > 0)
        {
            StatementSyntax? currentElse = null;

            // Start with the final else block if it exists
            if (ifStmt.ElseBody.Count > 0)
            {
                currentElse = Block(ifStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            }

            // Process elif clauses in reverse order
            for (int i = ifStmt.ElifClauses.Count - 1; i >= 0; i--)
            {
                var elif = ifStmt.ElifClauses[i];
                var elifCondition = GenerateExpression(elif.Test);
                var elifBody = Block(elif.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

                var elifElseClause = currentElse != null ? ElseClause(currentElse) : null;
                var elifStatement = IfStatement(elifCondition, elifBody, elifElseClause);

                currentElse = elifStatement;
            }

            if (currentElse != null)
            {
                elseClause = ElseClause(currentElse);
            }
        }

        return IfStatement(condition, thenBlock, elseClause);
    }

    private StatementSyntax GenerateWhile(WhileStatement whileStmt)
    {
        var condition = GenerateExpression(whileStmt.Test);
        var body = Block(whileStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        return WhileStatement(condition, body);
    }

    private StatementSyntax GenerateFor(ForStatement forStmt)
    {
        // For-in loop: for item in items: → foreach (var item in items)
        var iterator = GenerateExpression(forStmt.Iterator);
        var body = Block(forStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        if (forStmt.Target is Identifier varName)
        {
            var loopVar = NameMangler.ToCamelCase(varName.Name);
            return ForEachStatement(
                IdentifierName("var"),
                Identifier(loopVar),
                iterator,
                body);
        }

        // Handle tuple unpacking in for loops: for x, y in items
        if (forStmt.Target is TupleLiteral tuple)
        {
            // Check if all elements are identifiers
            bool allIdentifiers = tuple.Elements.All(e => e is Identifier);

            if (allIdentifiers)
            {
                var identifiers = tuple.Elements.Cast<Identifier>().ToList();

                // Generate: foreach (var (x, y) in items)
                var variables = identifiers
                    .Select(id =>
                    {
                        var varName = NameMangler.ToCamelCase(id.Name);
                        return SingleVariableDesignation(Identifier(varName));
                    })
                    .ToList();

                var tuplePattern = ParenthesizedVariableDesignation(
                    SeparatedList<VariableDesignationSyntax>(variables));

                var declExpr = DeclarationExpression(
                    IdentifierName("var"),
                    tuplePattern);

                return ForEachVariableStatement(
                    declExpr,
                    iterator,
                    body);
            }

            throw new NotImplementedException("Complex for loop tuple unpacking (non-identifier targets) not yet supported");
        }

        throw new NotImplementedException($"For loop target type not supported: {forStmt.Target.GetType().Name}");
    }

    private StatementSyntax GenerateTry(TryStatement tryStmt)
    {
        var tryBlock = Block(tryStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        // Generate catch clauses
        var catchClauses = tryStmt.Handlers.Select(handler =>
        {
            var catchBlock = Block(handler.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

            if (handler.ExceptionType != null)
            {
                var exceptionType = _typeMapper.MapType(handler.ExceptionType);

                if (handler.Name != null)
                {
                    var exceptionVar = NameMangler.ToCamelCase(handler.Name);
                    var declaration = CatchDeclaration(exceptionType, Identifier(exceptionVar));
                    return CatchClause(declaration, null, catchBlock);
                }
                else
                {
                    var declaration = CatchDeclaration(exceptionType);
                    return CatchClause(declaration, null, catchBlock);
                }
            }
            else
            {
                // Catch all exceptions
                return CatchClause()
                    .WithBlock(catchBlock);
            }
        }).ToList();

        // Generate finally block if present
        FinallyClauseSyntax? finallyClause = null;
        if (tryStmt.FinallyBody.Count > 0)
        {
            var finallyBlock = Block(tryStmt.FinallyBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            finallyClause = FinallyClause(finallyBlock);
        }

        return TryStatement(tryBlock, List(catchClauses), finallyClause);
    }

    private ExpressionSyntax GenerateExpression(Sharpy.Compiler.Parser.Ast.Expression expr)
    {
        return expr switch
        {
            // Literals
            IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
            FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
            StringLiteral strLit => GenerateStringLiteral(strLit),
            BooleanLiteral boolLit => LiteralExpression(boolLit.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            NoneLiteral => LiteralExpression(SyntaxKind.NullLiteralExpression),
            EllipsisLiteral => GenerateEllipsisLiteral(),

            // Collections
            ListLiteral listLit => GenerateListLiteral(listLit),
            DictLiteral dictLit => GenerateDictLiteral(dictLit),
            SetLiteral setLit => GenerateSetLiteral(setLit),
            TupleLiteral tupleLit => GenerateTupleLiteral(tupleLit),

            // Comprehensions
            ListComprehension listComp => GenerateListComprehension(listComp),
            SetComprehension setComp => GenerateSetComprehension(setComp),
            DictComprehension dictComp => GenerateDictComprehension(dictComp),

            // Primary expressions
            Identifier name => IdentifierName(NameMangler.ToCamelCase(name.Name)),
            MemberAccess memberAccess => GenerateMemberAccess(memberAccess),
            IndexAccess indexAccess => GenerateIndexAccess(indexAccess),
            SliceAccess sliceAccess => GenerateSliceAccess(sliceAccess),
            FunctionCall call => GenerateCall(call),

            // Operators
            UnaryOp unaryOp => GenerateUnaryOp(unaryOp),
            BinaryOp binOp => GenerateBinaryOp(binOp),
            ComparisonChain chain => GenerateComparisonChain(chain),

            // Advanced expressions
            ConditionalExpression cond => GenerateConditionalExpression(cond),
            LambdaExpression lambda => GenerateLambdaExpression(lambda),
            TypeCast cast => GenerateTypeCast(cast),
            TypeCheck check => GenerateTypeCheck(check),
            Parenthesized paren => GenerateExpression(paren.Expression),

            // F-strings
            FStringLiteral fstring => GenerateFString(fstring),

            _ => throw new NotImplementedException($"Expression type not implemented: {expr.GetType().Name}")
        };
    }

    private ExpressionSyntax GenerateIntegerLiteral(IntegerLiteral literal)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(int.Parse(literal.Value)));
    }

    private ExpressionSyntax GenerateFloatLiteral(FloatLiteral literal)
    {
        return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(double.Parse(literal.Value)));
    }

    private ExpressionSyntax GenerateStringLiteral(StringLiteral literal)
    {
        return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal.Value));
    }

    private ExpressionSyntax GenerateCall(FunctionCall call)
    {
        if (call.Function is Identifier funcName)
        {
            var name = _context.IsBuiltinFunction(funcName.Name)
                ? $"Sharpy.Core.Exports.{NameMangler.ToPascalCase(funcName.Name)}"
                : NameMangler.ToPascalCase(funcName.Name);

            var args = call.Arguments.Select(GenerateExpression).ToArray();

            return InvocationExpression(ParseName(name))
                .WithArgumentList(ArgumentList(SeparatedList(args.Select(Argument))));
        }

        throw new NotImplementedException("Complex function expressions not yet supported");
    }

    private ExpressionSyntax GenerateBinaryOp(BinaryOp binOp)
    {
        var left = GenerateExpression(binOp.Left);
        var right = GenerateExpression(binOp.Right);

        // Special cases that need method calls or casts
        switch (binOp.Operator)
        {
            case BinaryOperator.Power:
                // x ** y → Math.Pow(x, y)
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Math"),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

            case BinaryOperator.FloorDivide:
                // x // y → (int)(x / y) for integers
                // For now, cast to int (TODO: handle different numeric types)
                return CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    BinaryExpression(SyntaxKind.DivideExpression, left, right));

            case BinaryOperator.In:
                // x in y → y.__Contains__(x)
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        right,
                        IdentifierName("__Contains__")))
                    .AddArgumentListArguments(Argument(left));

            case BinaryOperator.NotIn:
                // x not in y → !y.__Contains__(x)
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            right,
                            IdentifierName("__Contains__")))
                        .AddArgumentListArguments(Argument(left)));

            case BinaryOperator.Is:
                // x is y → object.ReferenceEquals(x, y)
                // Special optimization for None: x is None → x == null
                if (binOp.Right is NoneLiteral)
                {
                    return BinaryExpression(SyntaxKind.EqualsExpression,
                        left,
                        LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        IdentifierName("ReferenceEquals")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

            case BinaryOperator.IsNot:
                // x is not y → !object.ReferenceEquals(x, y)
                // Special optimization for None: x is not None → x != null
                if (binOp.Right is NoneLiteral)
                {
                    return BinaryExpression(SyntaxKind.NotEqualsExpression,
                        left,
                        LiteralExpression(SyntaxKind.NullLiteralExpression));
                }
                return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                            IdentifierName("ReferenceEquals")))
                        .AddArgumentListArguments(
                            Argument(left),
                            Argument(right)));
        }

        // Standard binary operators
        var kind = binOp.Operator switch
        {
            // Arithmetic
            BinaryOperator.Add => SyntaxKind.AddExpression,
            BinaryOperator.Subtract => SyntaxKind.SubtractExpression,
            BinaryOperator.Multiply => SyntaxKind.MultiplyExpression,
            BinaryOperator.Divide => SyntaxKind.DivideExpression,
            BinaryOperator.Modulo => SyntaxKind.ModuloExpression,

            // Comparison
            BinaryOperator.Equal => SyntaxKind.EqualsExpression,
            BinaryOperator.NotEqual => SyntaxKind.NotEqualsExpression,
            BinaryOperator.LessThan => SyntaxKind.LessThanExpression,
            BinaryOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
            BinaryOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
            BinaryOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,

            // Logical (with short-circuit)
            BinaryOperator.And => SyntaxKind.LogicalAndExpression,
            BinaryOperator.Or => SyntaxKind.LogicalOrExpression,

            // Bitwise
            BinaryOperator.BitwiseAnd => SyntaxKind.BitwiseAndExpression,
            BinaryOperator.BitwiseOr => SyntaxKind.BitwiseOrExpression,
            BinaryOperator.BitwiseXor => SyntaxKind.ExclusiveOrExpression,
            BinaryOperator.LeftShift => SyntaxKind.LeftShiftExpression,
            BinaryOperator.RightShift => SyntaxKind.RightShiftExpression,

            // Null coalescing
            BinaryOperator.NullCoalesce => SyntaxKind.CoalesceExpression,

            _ => throw new NotImplementedException($"Binary operator not implemented: {binOp.Operator}")
        };

        return BinaryExpression(kind, left, right);
    }

    private ExpressionSyntax GenerateUnaryOp(UnaryOp unaryOp)
    {
        var operand = GenerateExpression(unaryOp.Operand);

        var kind = unaryOp.Operator switch
        {
            UnaryOperator.Plus => SyntaxKind.UnaryPlusExpression,
            UnaryOperator.Minus => SyntaxKind.UnaryMinusExpression,
            UnaryOperator.Not => SyntaxKind.LogicalNotExpression,
            UnaryOperator.BitwiseNot => SyntaxKind.BitwiseNotExpression,
            _ => throw new NotImplementedException($"Unary operator not implemented: {unaryOp.Operator}")
        };

        return PrefixUnaryExpression(kind, operand);
    }

    private ExpressionSyntax GenerateEllipsisLiteral()
    {
        // Ellipsis in v0.5 is used as a placeholder, similar to pass
        // We'll generate a comment or throw NotImplementedException
        // For now, generate: throw new NotImplementedException()
        return ThrowExpression(
            ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(ArgumentList()));
    }

    private ExpressionSyntax GenerateListLiteral(ListLiteral list)
    {
        // new Sharpy.Core.List<T> { elem1, elem2, elem3 }
        var elementType = _typeMapper.InferElementType(list.Elements);
        var elements = list.Elements.Select(GenerateExpression);

        var listType = GenericName("Sharpy.Core.List")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new Sharpy.Core.Dict<K,V> { { key1, value1 }, { key2, value2 } }
        var keyType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Key));
        var valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key),
                    GenerateExpression(entry.Value)
                })));

        var dictType = GenericName("Sharpy.Core.Dict")
            .AddTypeArgumentListArguments(keyType, valueType);

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new Sharpy.Core.Set<T> { elem1, elem2, elem3 }
        var elementType = _typeMapper.InferElementType(set.Elements);
        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("Sharpy.Core.Set")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateTupleLiteral(TupleLiteral tuple)
    {
        // (elem1, elem2, ...)
        var elements = tuple.Elements.Select(GenerateExpression);

        return TupleExpression(SeparatedList(
            elements.Select(e => Argument(e))));
    }

    // TODO: For nested or complex comprehensions, consider switching to imperative code generation
    // (using foreach loops and temporary lists) to improve readability and handle edge cases.
    // A complexity heuristic could be: multiple for clauses, or deeply nested comprehensions.

    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
    {
        // Generate LINQ method chain: iterator.Where(...).Select(...).ToList()
        // Example: [x * 2 for x in items if x > 0]
        // becomes: items.Where(x => x > 0).Select(x => x * 2).ToList()

        if (listComp.Clauses.Count == 0 || listComp.Clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException("List comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            throw new NotImplementedException("Tuple unpacking in comprehensions not yet supported");
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax current = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in listComp.Clauses.Skip(1))
        {
            if (clause is IfClause ifClause)
            {
                var condition = GenerateExpression(ifClause.Condition);
                var lambda = SimpleLambdaExpression(param)
                    .WithExpressionBody(condition);

                current = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        current,
                        IdentifierName("Where")))
                    .AddArgumentListArguments(Argument(lambda));
            }
            else if (clause is ForClause)
            {
                // Multiple for clauses (nested iteration) - requires more complex LINQ
                // For now, throw NotImplementedException
                throw new NotImplementedException("Nested comprehensions (multiple for clauses) not yet supported");
            }
        }

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(listComp.Element);
        var selectLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(elementExpr);

        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Apply .ToList()
        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("ToList")))
            .WithArgumentList(ArgumentList());

        return current;
    }

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
    {
        // Generate LINQ method chain: iterator.Where(...).Select(...).ToHashSet()
        // Example: {x * 2 for x in items if x > 0}
        // becomes: items.Where(x => x > 0).Select(x => x * 2).ToHashSet()

        if (setComp.Clauses.Count == 0 || setComp.Clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException("Set comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            throw new NotImplementedException("Tuple unpacking in comprehensions not yet supported");
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax current = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in setComp.Clauses.Skip(1))
        {
            if (clause is IfClause ifClause)
            {
                var condition = GenerateExpression(ifClause.Condition);
                var lambda = SimpleLambdaExpression(param)
                    .WithExpressionBody(condition);

                current = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        current,
                        IdentifierName("Where")))
                    .AddArgumentListArguments(Argument(lambda));
            }
            else if (clause is ForClause)
            {
                throw new NotImplementedException("Nested comprehensions (multiple for clauses) not yet supported");
            }
        }

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(setComp.Element);
        var selectLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(elementExpr);

        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Apply .ToHashSet()
        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("ToHashSet")))
            .WithArgumentList(ArgumentList());

        return current;
    }

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
    {
        // Generate LINQ method chain: iterator.Where(...).ToDictionary(x => key, x => value)
        // Example: {k: v for k, v in pairs if v > 0}
        // For now, only support single variable (not tuple unpacking)
        // becomes: pairs.Where(p => p.v > 0).ToDictionary(p => p.k, p => p.v)

        if (dictComp.Clauses.Count == 0 || dictComp.Clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException("Dict comprehension must start with a for clause");
        }

        // Get the loop variable name (single identifier only)
        if (firstFor.Target is not Identifier loopVar)
        {
            throw new NotImplementedException("Tuple unpacking in comprehensions not yet supported");
        }

        var varName = NameMangler.ToCamelCase(loopVar.Name);
        var param = Parameter(Identifier(varName));

        // Start with the iterator expression
        ExpressionSyntax current = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in dictComp.Clauses.Skip(1))
        {
            if (clause is IfClause ifClause)
            {
                var condition = GenerateExpression(ifClause.Condition);
                var lambda = SimpleLambdaExpression(param)
                    .WithExpressionBody(condition);

                current = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        current,
                        IdentifierName("Where")))
                    .AddArgumentListArguments(Argument(lambda));
            }
            else if (clause is ForClause)
            {
                throw new NotImplementedException("Nested comprehensions (multiple for clauses) not yet supported");
            }
        }

        // Generate key and value selector lambdas
        var keyExpr = GenerateExpression(dictComp.Key);
        var valueExpr = GenerateExpression(dictComp.Value);

        var keyLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(keyExpr);
        var valueLambda = SimpleLambdaExpression(param)
            .WithExpressionBody(valueExpr);

        // Apply .ToDictionary(x => key, x => value)
        current = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName("ToDictionary")))
            .AddArgumentListArguments(
                Argument(keyLambda),
                Argument(valueLambda));

        return current;
    }

    private ExpressionSyntax GenerateMemberAccess(MemberAccess memberAccess)
    {
        var obj = GenerateExpression(memberAccess.Object);
        var member = IdentifierName(memberAccess.Member);

        if (memberAccess.IsNullConditional)
        {
            // obj?.member
            return ConditionalAccessExpression(obj,
                MemberBindingExpression(member));
        }
        else
        {
            // obj.member
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                member);
        }
    }

    private ExpressionSyntax GenerateIndexAccess(IndexAccess indexAccess)
    {
        var obj = GenerateExpression(indexAccess.Object);
        var index = GenerateExpression(indexAccess.Index);

        return ElementAccessExpression(obj)
            .AddArgumentListArguments(Argument(index));
    }

    private ExpressionSyntax GenerateSliceAccess(SliceAccess sliceAccess)
    {
        // arr[start:stop:step]
        // Translates to: Sharpy.Core.Slice(arr, start, stop, step)
        var obj = GenerateExpression(sliceAccess.Object);
        var start = sliceAccess.Start != null
            ? GenerateExpression(sliceAccess.Start)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var stop = sliceAccess.Stop != null
            ? GenerateExpression(sliceAccess.Stop)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var step = sliceAccess.Step != null
            ? GenerateExpression(sliceAccess.Step)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Sharpy"),
                    IdentifierName("Core")),
                IdentifierName("Slice")))
            .AddArgumentListArguments(
                Argument(obj),
                Argument(start),
                Argument(stop),
                Argument(step));
    }

    private ExpressionSyntax GenerateComparisonChain(ComparisonChain chain)
    {
        // a < b < c → a < b && b < c (with b evaluated once)
        // For simplicity in v0.5, we'll allow re-evaluation
        // TODO: Store intermediate values in temp variables

        if (chain.Operands.Count < 2 || chain.Operators.Count != chain.Operands.Count - 1)
        {
            throw new InvalidOperationException("Invalid comparison chain");
        }

        ExpressionSyntax? result = null;

        for (int i = 0; i < chain.Operators.Count; i++)
        {
            var left = GenerateExpression(chain.Operands[i]);
            var right = GenerateExpression(chain.Operands[i + 1]);
            var op = chain.Operators[i];

            var kind = op switch
            {
                ComparisonOperator.Equal => SyntaxKind.EqualsExpression,
                ComparisonOperator.NotEqual => SyntaxKind.NotEqualsExpression,
                ComparisonOperator.LessThan => SyntaxKind.LessThanExpression,
                ComparisonOperator.LessThanOrEqual => SyntaxKind.LessThanOrEqualExpression,
                ComparisonOperator.GreaterThan => SyntaxKind.GreaterThanExpression,
                ComparisonOperator.GreaterThanOrEqual => SyntaxKind.GreaterThanOrEqualExpression,
                _ => throw new NotImplementedException($"Comparison operator {op} not supported in chains")
            };

            var comparison = BinaryExpression(kind, left, right);

            result = result == null
                ? comparison
                : BinaryExpression(SyntaxKind.LogicalAndExpression, result, comparison);
        }

        return result ?? throw new InvalidOperationException("Empty comparison chain");
    }

    private ExpressionSyntax GenerateConditionalExpression(ConditionalExpression cond)
    {
        // value if test else other → test ? value : other
        var test = GenerateExpression(cond.Test);
        var whenTrue = GenerateExpression(cond.ThenValue);
        var whenFalse = GenerateExpression(cond.ElseValue);

        return ConditionalExpression(test, whenTrue, whenFalse);
    }

    private ExpressionSyntax GenerateLambdaExpression(LambdaExpression lambda)
    {
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

    private ExpressionSyntax GenerateTypeCast(TypeCast cast)
    {
        // value as Type → (Type)value
        var value = GenerateExpression(cast.Value);
        var targetType = _typeMapper.MapType(cast.TargetType);

        return CastExpression(targetType, value);
    }

    private ExpressionSyntax GenerateTypeCheck(TypeCheck check)
    {
        // value is Type → value is Type
        var value = GenerateExpression(check.Value);
        var checkType = _typeMapper.MapType(check.CheckType);

        return BinaryExpression(
            SyntaxKind.IsExpression,
            value,
            checkType);
    }

    private ExpressionSyntax GenerateFString(FStringLiteral fstring)
    {
        // f"Hello {name}" → $"Hello {name}"
        var parts = new List<InterpolatedStringContentSyntax>();

        foreach (var part in fstring.Parts)
        {
            if (part.Text != null)
            {
                parts.Add(InterpolatedStringText()
                    .WithTextToken(Token(
                        TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        part.Text,
                        part.Text,
                        TriviaList())));
            }
            else if (part.Expression != null)
            {
                parts.Add(Interpolation(GenerateExpression(part.Expression)));
            }
        }

        return InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(parts));
    }

    /// <summary>
    /// Determines if a dunder method should generate a C# method (for overrides or special methods)
    /// Most dunder methods should NOT generate methods to avoid conflicts with user-defined methods
    /// </summary>
    private bool ShouldGenerateDunderMethod(string dunderName)
    {
        // Only generate methods for dunder methods that map to C# overrides or special constructs
        return dunderName switch
        {
            "__str__" => true,     // ToString() override
            "__repr__" => true,    // ToString() override
            "__eq__" => true,      // Equals() override
            "__hash__" => true,    // GetHashCode() override
            "__bool__" => true,    // ToBoolean() method (no operator equivalent)
            "__len__" => true,     // Length property/method (no operator equivalent)
            "__contains__" => true, // Contains() method (no operator equivalent)
            "__getitem__" => true, // Indexer get (no operator equivalent)
            "__setitem__" => true, // Indexer set (no operator equivalent)
            "__iter__" => true,    // GetEnumerator() (no operator equivalent)
            // Arithmetic and comparison operators should NOT generate methods
            // They only generate operators that inline the dunder method body
            _ => false
        };
    }

    /// <summary>
    /// Try to generate an operator overload from a dunder method
    /// </summary>
    private MemberDeclarationSyntax? TryGenerateOperatorOverload(FunctionDef funcDef, string className)
    {
        return funcDef.Name switch
        {
            // Arithmetic operators (binary)
            "__add__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.PlusToken),
            "__sub__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.MinusToken),
            "__mul__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.AsteriskToken),
            "__div__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.SlashToken),
            "__mod__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.PercentToken),

            // Bitwise operators (binary)
            "__and__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.AmpersandToken),
            "__or__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.BarToken),
            "__xor__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.CaretToken),
            "__lshift__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.LessThanLessThanToken),
            "__rshift__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.GreaterThanGreaterThanToken),

            // Comparison operators (binary)
            "__eq__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.EqualsEqualsToken),
            "__ne__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.ExclamationEqualsToken),
            "__lt__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.LessThanToken),
            "__le__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.LessThanEqualsToken),
            "__gt__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.GreaterThanToken),
            "__ge__" => GenerateComparisonOperator(funcDef, className, SyntaxKind.GreaterThanEqualsToken),

            // Unary operators
            "__neg__" => GenerateUnaryOperator(funcDef, className, SyntaxKind.MinusToken),
            "__pos__" => GenerateUnaryOperator(funcDef, className, SyntaxKind.PlusToken),
            "__invert__" => GenerateUnaryOperator(funcDef, className, SyntaxKind.TildeToken),

            // Not supported as operators (handled as methods)
            "__pow__" => null,     // No ** operator in C#, use Math.Pow
            "__getitem__" => null, // Requires indexer syntax, not operator
            "__setitem__" => null, // Requires indexer syntax, not operator

            _ => null
        };
    }

    /// <summary>
    /// Generate a binary operator overload (e.g., operator +, operator -, etc.)
    /// </summary>
    private OperatorDeclarationSyntax GenerateBinaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Binary operators should have 2 parameters: self and other
        // We skip 'self' and use the other parameter
        var otherParam = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (otherParam == null)
        {
            throw new InvalidOperationException($"Binary operator {funcDef.Name} must have at least 2 parameters");
        }

        // Determine return type - default to class type if not specified
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : IdentifierName(className);

        // Generate parameter for the operator
        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));

        var param2Type = otherParam.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : IdentifierName(className);

        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        // Generate body - call the actual dunder method on left operand
        // Use the transformed dunder name (e.g., __add__ -> Add)
        var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
        var invocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("left"),
                IdentifierName(methodName)))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));

        var body = Block(ReturnStatement(invocation));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate a comparison operator overload (==, !=, <, >, <=, >=)
    /// </summary>
    private OperatorDeclarationSyntax GenerateComparisonOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Similar to binary operators but always returns bool
        var otherParam = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (otherParam == null)
        {
            throw new InvalidOperationException($"Comparison operator {funcDef.Name} must have at least 2 parameters");
        }

        // Comparison operators always return bool
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        // Generate parameters
        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));

        var param2Type = otherParam.Type != null
            ? _typeMapper.MapType(otherParam.Type)
            : IdentifierName(className);

        var param2 = Parameter(Identifier("right"))
            .WithType(param2Type);

        // Generate body - call the actual dunder method on left operand
        var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
        var invocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("left"),
                IdentifierName(methodName)))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("right")))));

        var body = Block(ReturnStatement(invocation));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate a unary operator overload (-, +, ~)
    /// </summary>
    private OperatorDeclarationSyntax GenerateUnaryOperator(FunctionDef funcDef, string className, SyntaxKind operatorToken)
    {
        // Unary operators should have only 1 parameter: self

        // Determine return type - default to class type if not specified
        var returnType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : IdentifierName(className);

        // Generate parameter for the operator
        var param = Parameter(Identifier("value"))
            .WithType(IdentifierName(className));

        // Generate body - call the actual dunder method on the operand
        var methodName = NameMangler.Transform(funcDef.Name, NameContext.Method);
        var invocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("value"),
                IdentifierName(methodName)))
            .WithArgumentList(ArgumentList());

        var body = Block(ReturnStatement(invocation));

        return OperatorDeclaration(returnType, Token(operatorToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBody(body);
    }

    /// <summary>
    /// Generate complementary operator == when only __ne__ is defined
    /// </summary>
    private OperatorDeclarationSyntax GenerateComplementaryEqualsOperator(string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));
        var param2 = Parameter(Identifier("right"))
            .WithType(IdentifierName(className));

        // operator == returns !(left != right)
        var body = Block(ReturnStatement(
            PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                BinaryExpression(
                    SyntaxKind.NotEqualsExpression,
                    IdentifierName("left"),
                    IdentifierName("right")))));

        return OperatorDeclaration(returnType, Token(SyntaxKind.EqualsEqualsToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }

    /// <summary>
    /// Generate complementary operator != when only __eq__ is defined
    /// </summary>
    private OperatorDeclarationSyntax GenerateComplementaryNotEqualsOperator(string className)
    {
        var returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));

        var param1 = Parameter(Identifier("left"))
            .WithType(IdentifierName(className));
        var param2 = Parameter(Identifier("right"))
            .WithType(IdentifierName(className));

        // operator != returns !(left == right)
        var body = Block(ReturnStatement(
            PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    IdentifierName("left"),
                    IdentifierName("right")))));

        return OperatorDeclaration(returnType, Token(SyntaxKind.ExclamationEqualsToken))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[] { param1, param2 })))
            .WithBody(body);
    }
}
