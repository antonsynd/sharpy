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
        usings.Add(UsingDirective(ParseName("Sharpy")));
        usings.Add(UsingDirective(ParseName("Sharpy.Runtime")));

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
        var members = statements
            .Select(GenerateStatement)
            .OfType<MemberDeclarationSyntax>()
            .ToArray();

        return ClassDeclaration("__Module__")
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithMembers(List(members));
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

        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case FunctionDef funcDef:
                    // Check if this is a constructor (__init__)
                    if (funcDef.Name == "__init__")
                    {
                        // Generate constructor
                        members.Add(GenerateConstructor(funcDef, className));
                    }
                    else
                    {
                        members.Add(GenerateClassMethod(funcDef));
                    }
                    break;

                case VariableDeclaration varDecl:
                    // Generate field declaration
                    members.Add(GenerateField(varDecl));
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

        return members;
    }

    private ConstructorDeclarationSyntax GenerateConstructor(FunctionDef func, string className)
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
                    // Transform field name to PascalCase for C# property/field access
                    // Use direct case transformation without uniqueness tracking
                    string fieldName;
                    if (memberAccess.Member.Contains('_'))
                    {
                        // Handle snake_case to PascalCase
                        fieldName = string.Concat(memberAccess.Member.Split('_')
                            .Where(part => part.Length > 0)
                            .Select(part => char.ToUpper(part[0]) + part.Substring(1)));
                    }
                    else if (memberAccess.Member.Length > 0)
                    {
                        // Simple PascalCase
                        fieldName = char.ToUpper(memberAccess.Member[0]) + memberAccess.Member.Substring(1);
                    }
                    else
                    {
                        // Handle empty field name gracefully
                        fieldName = memberAccess.Member;
                    }
                    
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
                        assignValue = GenerateExpression(assign.Value);
                    }
                    
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

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

        // Generate parameters with type annotations, skipping 'self' and 'cls' parameters
        var parameters = func.Parameters
            .Where(p =>
                !string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(p.Name, "cls", StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

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
                // For now, treat as variable declaration (TODO: track if variable exists)
                var declaration = VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(varName))
                            .WithInitializer(EqualsValueClause(value))));

                return LocalDeclarationStatement(declaration);
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
            // For tuple unpacking, we need to generate multiple assignment statements
            // For now, we'll use deconstruction syntax
            throw new NotImplementedException("Tuple unpacking assignment not yet supported");
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

        // TODO: Handle tuple unpacking in for loops (for x, y in items:)
        throw new NotImplementedException("Complex for loop targets not yet supported");
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
                ? $"Sharpy.Exports.{NameMangler.ToPascalCase(funcName.Name)}"
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
        // new Sharpy.List<T> { elem1, elem2, elem3 }
        var elementType = _typeMapper.InferElementType(list.Elements);
        var elements = list.Elements.Select(GenerateExpression);

        var listType = GenericName("Sharpy.List")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new Sharpy.Dict<K,V> { { key1, value1 }, { key2, value2 } }
        var keyType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Key));
        var valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key),
                    GenerateExpression(entry.Value)
                })));

        var dictType = GenericName("Sharpy.Dict")
            .AddTypeArgumentListArguments(keyType, valueType);

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new Sharpy.Set<T> { elem1, elem2, elem3 }
        var elementType = _typeMapper.InferElementType(set.Elements);
        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("Sharpy.Set")
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
        // Translates to: Sharpy.Runtime.Slice(arr, start, stop, step)
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
                IdentifierName("Sharpy.Runtime"),
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
}
