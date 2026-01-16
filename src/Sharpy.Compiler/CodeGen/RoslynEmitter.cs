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
    private readonly Dictionary<string, int> _variableVersions = new();
    private readonly HashSet<string> _constVariables = new(); // Track const variable names (original Sharpy names)
    private readonly HashSet<string> _moduleConstVariables = new(); // Track module-level const names (preserved across function scopes)
    private readonly HashSet<string> _moduleVariables = new(); // Track module-level variable names (for PascalCase reference)
    private readonly HashSet<string> _classNames = new(); // Track class names defined in the current module
    private readonly HashSet<string> _structNames = new(); // Track struct names defined in the current module
    private int _tempVarCounter = 0;

    // Target type context for collection literal type inference
    // Set before generating expressions that need target type information
    private TypeAnnotation? _targetTypeContext;

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

    /// <summary>
    /// Get the mangled variable name with version suffix if this is a redefinition.
    /// </summary>
    /// <param name="name">The original Sharpy variable name</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition, false if this is a reference</param>
    /// <returns>The C# variable name with version suffix (e.g., "x", "x_1", "x_2")</returns>
    private string GetMangledVariableName(string name, bool isNewDeclaration)
    {
        var baseName = NameMangler.ToCamelCase(name);

        // FIRST: Check if this is a local variable that shadows a module-level one
        // Local variables take precedence over module-level variables
        if (_variableVersions.ContainsKey(baseName))
        {
            // There's a local variable with this name - use local resolution
            if (isNewDeclaration)
            {
                // This is a redefinition of an existing local variable
                var currentVersion = _variableVersions[baseName];
                var newVersion = currentVersion + 1;
                _variableVersions[baseName] = newVersion;
                return $"{baseName}_{newVersion}";
            }
            else
            {
                // This is a reference to the local variable
                var currentVersion = _variableVersions[baseName];
                return currentVersion == 0 ? baseName : $"{baseName}_{currentVersion}";
            }
        }

        // Check if this is a reference to a local const variable - use constant case
        if (_constVariables.Contains(name))
        {
            return NameMangler.ToConstantCase(name);
        }

        // Check if this is a reference to a module-level const - use constant case
        if (_moduleConstVariables.Contains(name))
        {
            return NameMangler.ToConstantCase(name);
        }

        // Check if this is a reference to a module-level variable - use PascalCase
        if (_moduleVariables.Contains(name))
        {
            return NameMangler.ToPascalCase(name);
        }

        // Check if this is a reference to a class or struct name - preserve PascalCase
        if (_classNames.Contains(name) || _structNames.Contains(name))
        {
            return NameMangler.ToPascalCase(name);
        }

        // Check if this is a module symbol - preserve the exact name (with sanitization)
        // This ensures imported module names match their using alias (e.g., math_ops stays math_ops)
        var symbol = _context.LookupSymbol(name);
        if (symbol is ModuleSymbol)
        {
            // Use the same sanitization as in GenerateImportUsings
            return name.Replace(".", "_");
        }

        // If we reach here, this is a new local variable that doesn't shadow any module-level var
        if (isNewDeclaration)
        {
            // First declaration of this local variable
            _variableVersions[baseName] = 0;
            return baseName;
        }
        else
        {
            // Reference to a variable not yet declared (shouldn't happen in valid code)
            // Fall back to just returning the base name
            return baseName;
        }
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
        // Use block-scoped namespace for C# 9.0 compatibility (Unity)
        var namespaceName = GenerateNamespaceName();
        var namespaceDecl = NamespaceDeclaration(namespaceName)
            .WithMembers(SingletonList<MemberDeclarationSyntax>(moduleClass));

        // Build compilation unit first
        var compilationUnit = CompilationUnit()
            .WithUsings(List(usingDirectives))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDecl))
            .NormalizeWhitespace();

        // Add #nullable enable directive to enable C# nullable reference types
        // This aligns with Sharpy's "null-safe by default" principle (Axiom 3)
        // Must be added AFTER NormalizeWhitespace to preserve leading position
        var nullablePragma = ParseLeadingTrivia("#nullable enable\n\n");

        return compilationUnit.WithLeadingTrivia(nullablePragma);
    }

    private NameSyntax GenerateNamespaceName()
    {
        // If project namespace is specified, use project-based namespace generation
        if (!string.IsNullOrEmpty(_context.ProjectNamespace) &&
            !string.IsNullOrEmpty(_context.ProjectRootPath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath))
        {
            return GenerateProjectNamespace();
        }

        // If only project namespace is specified (single-file with explicit namespace),
        // use it directly with the file name
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            var fileName = !string.IsNullOrEmpty(_context.SourceFilePath)
                ? Path.GetFileNameWithoutExtension(_context.SourceFilePath)
                : null;

            if (!string.IsNullOrEmpty(fileName))
            {
                return ParseName($"{_context.ProjectNamespace}.{SimpleToPascalCase(fileName)}");
            }
            return ParseName(_context.ProjectNamespace);
        }

        // Fallback to file-based namespace generation for single-file compilation
        // For single-file compilation without a project, use a simple namespace based
        // on just the file name to avoid problematic paths (numeric directories, etc.)
        if (string.IsNullOrEmpty(_context.SourceFilePath))
        {
            return ParseName("SharpyGenerated");
        }

        var fileNameOnly = Path.GetFileNameWithoutExtension(_context.SourceFilePath);

        // Use simple file-name-based namespace for single-file compilation
        // This avoids issues with paths containing numeric directories, special chars, etc.
        if (!string.IsNullOrEmpty(fileNameOnly))
        {
            var sanitizedName = SimpleToPascalCase(fileNameOnly);
            return ParseName($"Sharpy.{sanitizedName}");
        }

        return ParseName("SharpyGenerated");
    }

    private NameSyntax GenerateProjectNamespace()
    {
        // Start with project root namespace
        var namespaceParts = new List<string> { _context.ProjectNamespace! };

        // Get relative path from project src directory to source file
        var relativePath = Path.GetRelativePath(_context.ProjectRootPath!, _context.SourceFilePath!);

        // Extract directory path (without filename)
        var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);

        // Add directory parts to namespace (if not at root)
        if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
        {
            var dirParts = relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => SimpleToPascalCase(p));
            namespaceParts.AddRange(dirParts);
        }

        // Add file name as final namespace component
        if (!string.IsNullOrEmpty(fileName))
        {
            namespaceParts.Add(SimpleToPascalCase(fileName));
        }

        return ParseName(string.Join(".", namespaceParts));
    }

    private List<UsingDirectiveSyntax> GenerateUsingDirectives(Module module)
    {
        var usings = new List<UsingDirectiveSyntax>();

        // Add default System usings
        usings.Add(UsingDirective(ParseName("System")));
        usings.Add(UsingDirective(ParseName("System.Collections.Generic")));
        usings.Add(UsingDirective(ParseName("System.Linq")));

        // Add Sharpy runtime usings (use global:: to avoid conflicts when output namespace contains "Sharpy")
        usings.Add(UsingDirective(ParseName("global::Sharpy.Core")));

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
            var isNetFramework = IsNetFrameworkNamespace(alias.Name);

            if (alias.AsName != null)
            {
                // import module as alias
                if (isNetFramework)
                {
                    // import system.io as io -> using io = System.IO; (for .NET framework)
                    yield return UsingDirective(
                        NameEquals(alias.AsName),
                        ParseName(namespaceName));
                }
                else
                {
                    // import module as alias -> using alias = ProjectNamespace.Module.Module; (for Sharpy modules)
                    // Extract just the last part for the class name
                    // e.g., "Lib.Math.Operations" → "Operations"
                    var lastDotIndex = namespaceName.LastIndexOf('.');
                    var moduleClassName = lastDotIndex >= 0
                        ? namespaceName.Substring(lastDotIndex + 1)
                        : namespaceName;

                    // Build full path: <ProjectNamespace>.<ModuleNamespace>.<ClassName>
                    string fullModuleClass;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}.{moduleClassName}";
                    }
                    else
                    {
                        // Fallback for single-file compilation without project namespace
                        fullModuleClass = $"{namespaceName}.{moduleClassName}";
                    }

                    yield return UsingDirective(
                        NameEquals(alias.AsName),
                        ParseName(fullModuleClass));
                }
            }
            else
            {
                if (isNetFramework)
                {
                    // import system.io -> using System.IO; (standard .NET import)
                    yield return UsingDirective(ParseName(namespaceName));
                }
                else
                {
                    // import module -> using module_alias = ProjectNamespace.Module.Module; (Sharpy module)
                    // Convert "utils.helpers" to "utils_helpers" for valid C# identifier
                    var sanitizedAlias = alias.Name.Replace(".", "_");

                    // Extract just the last part for the class name
                    // e.g., "Lib.Math.Operations" → "Operations"
                    var lastDotIndex = namespaceName.LastIndexOf('.');
                    var moduleClassName = lastDotIndex >= 0
                        ? namespaceName.Substring(lastDotIndex + 1)
                        : namespaceName;

                    // Build full path: <ProjectNamespace>.<ModuleNamespace>.<ClassName>
                    // For example:
                    //   - "config" → "TestProject.Config.Config"
                    //   - "lib.math.operations" → "TestProject.Lib.Math.Operations.Operations"
                    string fullModuleClass;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}.{moduleClassName}";
                    }
                    else
                    {
                        // Fallback for single-file compilation without project namespace
                        fullModuleClass = $"{namespaceName}.{moduleClassName}";
                    }

                    yield return UsingDirective(
                        NameEquals(sanitizedAlias),
                        ParseName(fullModuleClass));
                }
            }
        }
    }

    private IEnumerable<UsingDirectiveSyntax> GenerateFromImportUsings(FromImportStatement fromImport)
    {
        var isNetFramework = IsNetFrameworkNamespace(fromImport.Module);

        if (isNetFramework)
        {
            // from system.io import File -> using System.IO; (standard .NET import)
            var namespaceName = ConvertModuleNameToNamespace(fromImport.Module);
            yield return UsingDirective(ParseName(namespaceName));
        }
        else
        {
            // Generate using static for the module class
            // This enables direct access to module-level functions and variables
            // e.g., "from config import MAX_SIZE" → "using static TestFromImport.Config.Config;"
            //
            // For nested modules like "lib.math.operations":
            // - Namespace: TestFromImport.Lib.Math.Operations
            // - Class: Operations
            // - Full path: TestFromImport.Lib.Math.Operations.Operations

            var moduleNamespacePath = ConvertModuleNameToNamespace(fromImport.Module);

            // Extract just the last part for the class name
            // e.g., "Lib.Math.Operations" → "Operations"
            var lastDotIndex = moduleNamespacePath.LastIndexOf('.');
            var moduleClassName = lastDotIndex >= 0
                ? moduleNamespacePath.Substring(lastDotIndex + 1)
                : moduleNamespacePath;

            // Build full path: <ProjectNamespace>.<ModuleNamespace>.<ClassName>
            // For example:
            //   - "config" → "TestFromImport.Config.Config"
            //   - "lib.math.operations" → "TestFromImport.Lib.Math.Operations.Operations"
            string fullModuleClass;
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                fullModuleClass = $"{_context.ProjectNamespace}.{moduleNamespacePath}.{moduleClassName}";
            }
            else
            {
                // Fallback for single-file compilation without project namespace
                fullModuleClass = $"{moduleNamespacePath}.{moduleClassName}";
            }

            yield return UsingDirective(ParseName(fullModuleClass))
                .WithStaticKeyword(Token(SyntaxKind.StaticKeyword));
        }
    }

    /// <summary>
    /// Determines if a module name refers to a .NET framework namespace.
    /// .NET framework namespaces don't have an Exports class, so they need different import handling.
    /// </summary>
    private static bool IsNetFrameworkNamespace(string moduleName)
    {
        // Common .NET framework namespace prefixes
        var netPrefixes = new[]
        {
            "system",
            "microsoft",
            "windows",
            "xamarin",
            "mono",
            "netstandard"
        };

        var firstPart = moduleName.Split('.')[0].ToLowerInvariant();
        return netPrefixes.Contains(firstPart);
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

        // Replace invalid identifier characters with underscores, then split
        // Valid C# identifier chars: letters, digits (not at start), underscores
        var sanitized = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }

        // Split by underscore and capitalize each part
        var parts = sanitized.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);

        // Handle edge case where name is only underscores (e.g., "___")
        if (parts.Length == 0)
            return "_";

        var result = string.Join("", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")
        ));

        // If result starts with a digit, prefix with underscore to make it valid
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return string.IsNullOrEmpty(result) ? "_" : result;
    }

    private ClassDeclarationSyntax GenerateModuleClass(List<Statement> statements)
    {
        // Pre-scan for module-level variable declarations to track them across all scopes
        // This ensures functions can reference variables with correct casing
        _moduleConstVariables.Clear();
        _moduleVariables.Clear();
        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                if (varDecl.IsConst)
                {
                    _moduleConstVariables.Add(varDecl.Name);
                }
                else
                {
                    _moduleVariables.Add(varDecl.Name);
                }
            }
        }

        // Pre-scan for enum declarations and register them in the symbol table
        // This ensures enum member access (e.g., Color.RED) works correctly
        foreach (var stmt in statements)
        {
            if (stmt is EnumDef enumDef)
            {
                var enumSymbol = new TypeSymbol
                {
                    Name = enumDef.Name,
                    ClrType = null,
                    TypeKind = Semantic.TypeKind.Enum
                };
                // Only add if not already present
                if (_context.LookupSymbol(enumDef.Name) == null)
                {
                    _context.SymbolTable.Define(enumSymbol);
                }
            }
        }

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

        // Generate a Main method if:
        // 1. There's no user-defined main function, AND
        // 2. This is the entry point file
        // Note: We generate Main even if there are no executable statements, to support
        // entry points that only contain imports or declarations
        if (!hasMainFunction && _context.IsEntryPoint)
        {
            // Create a Main method for executable statements (or empty if none)
            // Clear declared variables and version tracking for Main method scope
            _declaredVariables.Clear();
            _variableVersions.Clear();
            _constVariables.Clear();

            var mainBody = executableStatements.Count > 0
                ? Block(executableStatements
                    .Select(GenerateBodyStatement)
                    .OfType<StatementSyntax>())
                : Block(); // Empty block for entry points with no executable code

            var mainMethod = MethodDeclaration(
                    PredefinedType(Token(SyntaxKind.VoidKeyword)),
                    "Main")
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword)))
                .WithBody(mainBody);

            declarations.Add(mainMethod);
        }
        else if (hasMainFunction && executableStatements.Count > 0)
        {
            // There's a main function and also module-level statements
            // For now, just ignore them or add to Main after the user's main is called
            // This is a corner case we'll handle later
            Console.WriteLine($"Warning: {executableStatements.Count} module-level statement(s) ignored because a 'main' function is defined");
        }
        else if (!_context.IsEntryPoint && executableStatements.Count > 0)
        {
            // Non-entry-point files with executable statements: ignore them
            // Module-level executable code should only run in the entry point
            Console.WriteLine($"Warning: {executableStatements.Count} module-level executable statement(s) in non-entry-point file ignored");
        }

        // Check if we're generating a Main method OR if there's a user-defined main function
        // (both will result in a method named "Main" in the class)
        // Note: Main is generated for ALL entry point files (even empty ones), per line 557
        bool willHaveMainMethod = hasMainFunction || (!hasMainFunction && _context.IsEntryPoint);

        // Collect all function names to check for class name collisions
        var functionNames = statements
            .OfType<FunctionDef>()
            .Select(f => NameMangler.Transform(f.Name, NameContext.Method))
            .ToHashSet();

        // Generate module class name from source file name
        var moduleClassName = GetModuleClassName(willHaveMainMethod, functionNames);

        return ClassDeclaration(moduleClassName)
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithMembers(List(declarations));
    }

    private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
    {
        // Get the file name without extension and convert to PascalCase
        if (!string.IsNullOrEmpty(_context.SourceFilePath))
        {
            var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);
            if (!string.IsNullOrEmpty(fileName))
            {
                var className = SimpleToPascalCase(fileName);

                // Avoid name collision: if the class would be named "Main" and we're generating
                // a Main method, use "Program" instead (following C# convention)
                if (className == "Main" && willGenerateMainMethod)
                {
                    return "Program";
                }

                // Avoid name collision: if the class name matches any function name in the module,
                // append "Module" suffix to the class name (C# doesn't allow method name == enclosing type name)
                if (functionNames != null && functionNames.Contains(className))
                {
                    return $"{className}Module";
                }

                return className;
            }
        }

        // Fallback to "Exports" if no source file path available
        return "Exports";
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
            VariableDeclaration varDecl => GenerateModuleLevelField(varDecl),
            TypeAlias => null,  // Type aliases are compile-time only, no C# output
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            // Add more statement types...
            _ => null
        };
    }

    private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
    {
        // Clear declared variables and version tracking for new function scope
        _declaredVariables.Clear();
        _variableVersions.Clear();
        _constVariables.Clear();

        // Transform name using NameMangler
        // Special case: only convert "main" to "Main" if this is the entry point file
        var mangledName = func.Name == "main" && !_context.IsEntryPoint
            ? "MainFunc"  // Rename to avoid C# entry point conflict in non-entry files
            : NameMangler.Transform(func.Name, NameContext.Method);

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
            // Also track in version map so assignments to parameters work correctly
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Generate method body
        var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        var method = MethodDeclaration(returnType, mangledName)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add type parameters if generic
        if (func.TypeParameters.Count > 0)
        {
            var typeParams = func.TypeParameters
                .Select(tp => TypeParameter(tp.Name))
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

    private ParameterSyntax GenerateParameter(Parameter param)
    {
        var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);

        // Get parameter type from annotation or default to object
        TypeSyntax paramType = param.Type != null
            ? _typeMapper.MapType(param.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // For variadic parameters (*args), wrap the element type in an array
        if (param.IsVariadic)
        {
            paramType = ArrayType(paramType)
                .WithRankSpecifiers(SingletonList(ArrayRankSpecifier()));
        }

        var parameter = Parameter(Identifier(paramName))
            .WithType(paramType);

        // For variadic parameters, add the 'params' modifier
        if (param.IsVariadic)
        {
            parameter = parameter.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
        }

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
        // Track this class name for instantiation detection
        _classNames.Add(classDef.Name);

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
                .Select(tp => TypeParameter(tp.Name))
                .ToArray();
            classDecl = classDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(classDef.TypeParameters));
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
        // Track this struct name for instantiation detection
        _structNames.Add(structDef.Name);

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
                .Select(tp => TypeParameter(tp.Name))
                .ToArray();
            structDecl = structDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(structDef.TypeParameters));
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
                .Select(tp => TypeParameter(tp.Name))
                .ToArray();
            interfaceDecl = interfaceDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(interfaceDef.TypeParameters));
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

    private SyntaxList<TypeParameterConstraintClauseSyntax> GenerateConstraintClauses(
        List<TypeParameterDef> typeParameters)
    {
        var clauses = new List<TypeParameterConstraintClauseSyntax>();

        foreach (var typeParam in typeParameters)
        {
            if (typeParam.Constraints.Count == 0)
                continue;

            var constraintSyntaxes = new List<TypeParameterConstraintSyntax>();

            // Order: class/struct first, then types, then new()
            var ordered = typeParam.Constraints
                .OrderBy(c => c switch
                {
                    ClassConstraint => 0,
                    StructConstraint => 0,
                    Parser.Ast.TypeConstraint => 1,
                    NewConstraint => 2,
                    _ => 3
                });

            foreach (var constraint in ordered)
            {
                constraintSyntaxes.Add(constraint switch
                {
                    ClassConstraint => ClassOrStructConstraint(
                        SyntaxKind.ClassConstraint),
                    StructConstraint => ClassOrStructConstraint(
                        SyntaxKind.StructConstraint),
                    Parser.Ast.TypeConstraint tc => Microsoft.CodeAnalysis.CSharp.SyntaxFactory.TypeConstraint(
                        _typeMapper.MapType(tc.Type)),
                    NewConstraint => ConstructorConstraint(),
                    _ => throw new InvalidOperationException($"Unknown constraint type: {constraint.GetType().Name}")
                });
            }

            clauses.Add(TypeParameterConstraintClause(typeParam.Name)
                .WithConstraints(SeparatedList(constraintSyntaxes)));
        }

        return List(clauses);
    }

    private SyntaxNode GenerateEnumDeclaration(EnumDef enumDef)
    {
        // Determine if this is a string enum or integer enum
        bool isStringEnum = IsStringEnum(enumDef);

        if (isStringEnum)
        {
            return GenerateStringEnumClass(enumDef);
        }
        else
        {
            return GenerateIntegerEnum(enumDef);
        }
    }

    /// <summary>
    /// Determines if an enum is a string enum (has at least one string literal value)
    /// </summary>
    private bool IsStringEnum(EnumDef enumDef)
    {
        // Check if any member has a string value
        foreach (var member in enumDef.Members)
        {
            if (member.Value is StringLiteral)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Generates a C# enum for integer enums
    /// </summary>
    private EnumDeclarationSyntax GenerateIntegerEnum(EnumDef enumDef)
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

    /// <summary>
    /// Generates a sealed class with public static readonly string fields for string enums
    /// </summary>
    private ClassDeclarationSyntax GenerateStringEnumClass(EnumDef enumDef)
    {
        // Transform enum name
        var className = NameMangler.Transform(enumDef.Name, NameContext.Type);

        // Create public sealed class
        var modifiers = TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.SealedKeyword)
        );

        var classDecl = ClassDeclaration(className)
            .WithModifiers(modifiers);

        // Generate public static readonly string fields for each member
        var members = new List<MemberDeclarationSyntax>();

        foreach (var member in enumDef.Members)
        {
            var fieldName = NameMangler.Transform(member.Name, NameContext.Constant);

            // Determine the value - use the explicit value if provided, otherwise use the member name
            ExpressionSyntax valueExpr;
            if (member.Value is StringLiteral strLit)
            {
                valueExpr = GenerateExpression(strLit);
            }
            else
            {
                // Default to the original member name as a string
                valueExpr = LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    Literal(member.Name)
                );
            }

            var field = FieldDeclaration(
                VariableDeclaration(
                    PredefinedType(Token(SyntaxKind.StringKeyword))
                )
                .WithVariables(
                    SingletonSeparatedList(
                        VariableDeclarator(Identifier(fieldName))
                            .WithInitializer(EqualsValueClause(valueExpr))
                    )
                )
            )
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword)
            ));

            members.Add(field);
        }

        classDecl = classDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(enumDef.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(enumDef.DocString));
        }

        return classDecl;
    }

    private EnumMemberDeclarationSyntax GenerateEnumMember(EnumMember member)
    {
        // Enum members use PascalCase in C# (RED -> Red, DARK_BLUE -> DarkBlue)
        // Need custom logic because NameMangler.ToPascalCase preserves all-caps words
        var memberName = TransformEnumMemberName(member.Name);

        var enumMember = EnumMemberDeclaration(Identifier(memberName));

        // Add explicit value if present
        if (member.Value != null)
        {
            var valueExpr = GenerateExpression(member.Value);
            enumMember = enumMember.WithEqualsValue(EqualsValueClause(valueExpr));
        }

        return enumMember;
    }

    private static string TransformEnumMemberName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Handle literal names (backtick-escaped)
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];

        // Split by underscores and capitalize each part
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var capitalizedParts = parts.Select(part =>
            string.IsNullOrEmpty(part) ? part :
            char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant());

        return string.Join("", capitalizedParts);
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
                    if (funcDef.Name == "__init__")
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
                    // Ignore other statements for now
                    break;
            }
        }

        // Generate all constructors (supports overloading)
        foreach (var initMethod in initMethods)
        {
            members.Add(GenerateConstructor(initMethod, className, fieldMapping));
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

        // Check if the first statement is a super().__init__() call
        // This needs to be converted to a constructor initializer (: base(...))
        ConstructorInitializerSyntax? baseInitializer = null;
        var bodyStartIndex = 0;

        if (func.Body.Count > 0 && func.Body[0] is ExpressionStatement exprStmt)
        {
            // Check if it's super().__init__(...)
            if (exprStmt.Expression is FunctionCall call &&
                call.Function is MemberAccess memberAccess &&
                memberAccess.Object is SuperExpression &&
                memberAccess.Member == "__init__")
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

        for (int i = bodyStartIndex; i < func.Body.Count; i++)
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
            || func.Name == "__repr__"
            // __eq__ is an operator dunder (not in ProtocolRegistry) but maps to Equals() override
            || func.Name == "__eq__";

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
        if (func.Name == "__eq__" && parameters.Length > 0)
        {
            var objParam = Parameter(Identifier(parameters[0].Identifier.Text))
                .WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)));
            parameters = new[] { objParam };
        }

        // Check if this is an abstract method
        bool isAbstract = func.Decorators.Any(d =>
            d.Name == "abstractmethod" || d.Name == "abstract");

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


    private StatementSyntax? GenerateBodyStatement(Statement stmt)
    {
        return stmt switch
        {
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            VariableDeclaration varDecl => GenerateVariableDeclaration(varDecl),
            ExpressionStatement exprStmt => GenerateExpressionStatement(exprStmt),
            PassStatement => EmptyStatement(),
            Sharpy.Compiler.Parser.Ast.BreakStatement => SyntaxFactory.BreakStatement(),
            BreakWithFlagStatement breakWithFlag => GenerateBreakWithFlag(breakWithFlag),
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

    /// <summary>
    /// Generates a C# statement from a Sharpy expression statement.
    /// In C#, only certain expressions are valid as statements (invocations, assignments, new, ++/--).
    /// For other expressions (literals, arithmetic, comparison, etc.), we use a discard: _ = expr;
    /// </summary>
    private StatementSyntax GenerateExpressionStatement(ExpressionStatement exprStmt)
    {
        var expr = exprStmt.Expression;

        // None as a statement is a no-op (like Python's None expression)
        // We generate an empty statement since `_ = null;` requires type annotation in C#
        if (expr is NoneLiteral)
        {
            return EmptyStatement();
        }

        var generated = GenerateExpression(expr);

        // Check if the expression is valid as a C# statement
        if (IsValidCSharpStatementExpression(expr))
        {
            return ExpressionStatement(generated);
        }

        // Otherwise, wrap in a discard: _ = expr;
        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("_"),
                generated));
    }

    /// <summary>
    /// Determines if an expression is valid as a standalone C# statement.
    /// Valid statement expressions in C# are:
    /// - Invocation expressions (method calls)
    /// - Object creation expressions (new)
    /// - Assignment expressions
    /// - Increment/decrement expressions (++/--)
    /// - Await expressions
    /// </summary>
    private bool IsValidCSharpStatementExpression(Expression expr)
    {
        return expr switch
        {
            // Method calls are valid statements
            FunctionCall => true,

            // Await expressions are valid (if we had them in AST)
            // AwaitExpression => true,

            // All other expressions need a discard
            _ => false
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
            // Check if this is a simple assignment or augmented assignment
            if (assign.Operator == AssignmentOperator.Assign)
            {
                // Simple assignment: x = value
                // In Sharpy, assignments can be redefinitions with type changes
                // However, inside a function/loop, we should update existing vars
                // Get the base name to check if already declared
                var baseName = NameMangler.ToCamelCase(name.Name);

                // Check if this variable was already declared in current scope
                // _variableVersions tracks all variables by base name
                // Also check if this is a module-level variable which should be assigned, not declared
                if (_variableVersions.ContainsKey(baseName) ||
                    _moduleVariables.Contains(name.Name) ||
                    _moduleConstVariables.Contains(name.Name))
                {
                    // Variable exists - just update it with a regular assignment
                    var currentName = GetMangledVariableName(name.Name, isNewDeclaration: false);
                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(currentName),
                            value));
                }
                else
                {
                    // First declaration of this variable in this scope
                    var varName = GetMangledVariableName(name.Name, isNewDeclaration: true);
                    _declaredVariables.Add(varName);
                    var declaration = VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))
                                .WithInitializer(EqualsValueClause(value))));

                    return LocalDeclarationStatement(declaration);
                }
            }
            else
            {
                // Augmented assignment: x += value
                // This references the current version and modifies it
                var varName = GetMangledVariableName(name.Name, isNewDeclaration: false);
                var left = IdentifierName(varName);
                var augmentedValue = GenerateAugmentedValue(assign.Operator, left, value, assign.Target, assign.Value);

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
                : GenerateAugmentedValue(assign.Operator, elementAccess, value, assign.Target, assign.Value);

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
                : GenerateAugmentedValue(assign.Operator, target, value, assign.Target, assign.Value);

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
            // C#: var (x, y) = (1, 2)

            // Check if all elements are identifiers
            bool allIdentifiers = tuple.Elements.All(e => e is Identifier);

            if (allIdentifiers)
            {
                var identifiers = tuple.Elements.Cast<Identifier>().ToList();

                // In Sharpy, tuple unpacking is always a new declaration/redefinition
                // Use: var (x, y) = expr
                var variables = identifiers
                    .Select(id =>
                    {
                        var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
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
            // Special cases handled by GenerateAugmentedValue
            AssignmentOperator.DoubleSlashAssign => SyntaxKind.None,
            AssignmentOperator.PowerAssign => SyntaxKind.None,
            AssignmentOperator.NullCoalesceAssign => SyntaxKind.None,
            _ => throw new NotImplementedException($"Augmented assignment operator not supported: {op}")
        };
    }

    /// <summary>
    /// Generates the value expression for an augmented assignment.
    /// Handles special cases like //= (floor divide) and **= (power) that require
    /// method calls or casts instead of simple binary expressions.
    /// </summary>
    /// <param name="op">The assignment operator</param>
    /// <param name="left">Generated C# expression for the target</param>
    /// <param name="right">Generated C# expression for the value</param>
    /// <param name="targetAst">Original AST target expression (for type inference)</param>
    /// <param name="valueAst">Original AST value expression (for type inference)</param>
    private ExpressionSyntax GenerateAugmentedValue(AssignmentOperator op, ExpressionSyntax left, ExpressionSyntax right, Expression? targetAst = null, Expression? valueAst = null)
    {
        return op switch
        {
            // x **= y → System.Math.Pow(x, y)
            // Note: We use fully qualified System.Math to avoid conflicts with Sharpy.Math namespace
            AssignmentOperator.PowerAssign =>
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("System"),
                            IdentifierName("Math")),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right)),

            // x //= y → floor division with Python semantics (toward negative infinity)
            // Integer operands: (long)Math.Floor((double)x / y) → result is int64
            // Float operands: Math.Floor(x / y) → result is float type
            AssignmentOperator.DoubleSlashAssign =>
                GenerateFloorDivision(left, right,
                    (targetAst != null && IsFloatExpression(targetAst)) ||
                    (valueAst != null && IsFloatExpression(valueAst))),

            // x ??= y → x ?? y (null coalescing)
            AssignmentOperator.NullCoalesceAssign =>
                BinaryExpression(SyntaxKind.CoalesceExpression, left, right),

            // All other operators use simple binary expressions
            _ => BinaryExpression(GetAugmentedAssignmentOperator(op), left, right)
        };
    }

    /// <summary>
    /// Generate a break statement with flag assignment for loop else support.
    /// Generates: { flagName = false; break; }
    /// </summary>
    private StatementSyntax GenerateBreakWithFlag(BreakWithFlagStatement breakStmt)
    {
        return Block(
            ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(breakStmt.FlagName),
                    LiteralExpression(SyntaxKind.FalseLiteralExpression))),
            SyntaxFactory.BreakStatement());
    }

    private StatementSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        // Track const variables by their original Sharpy name for consistent reference resolution
        if (varDecl.IsConst)
        {
            _constVariables.Add(varDecl.Name);
        }

        var varName = varDecl.IsConst
            ? NameMangler.ToConstantCase(varDecl.Name)
            : GetMangledVariableName(varDecl.Name, isNewDeclaration: true);

        // Handle 'auto' type annotation - use 'var' in C#
        // For const without type annotation, infer type from initializer (C# const can't use 'var')
        TypeSyntax typeSyntax;
        if (varDecl.Type != null && varDecl.Type.Name == "auto")
        {
            typeSyntax = IdentifierName("var");
        }
        else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
        }
        else
        {
            typeSyntax = _typeMapper.MapType(varDecl.Type);
        }

        // Track this variable as declared
        _declaredVariables.Add(varName);

        VariableDeclaratorSyntax declarator;
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            // This allows list/dict/set literals to use the declared type
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var value = GenerateExpression(varDecl.InitialValue);
                declarator = VariableDeclarator(Identifier(varName))
                    .WithInitializer(EqualsValueClause(value));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
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

    private FieldDeclarationSyntax GenerateModuleLevelField(VariableDeclaration varDecl)
    {
        // Track const variables by their original Sharpy name for consistent reference resolution
        if (varDecl.IsConst)
        {
            _constVariables.Add(varDecl.Name);
        }

        // Module-level fields are public static, so use PascalCase
        // (Instance fields in classes use camelCase via NameContext.Field)
        var varName = varDecl.IsConst
            ? NameMangler.ToConstantCase(varDecl.Name)
            : NameMangler.ToPascalCase(varDecl.Name);

        // Handle 'auto' type annotation - for fields, we must resolve to concrete type
        // For const without type annotation, infer type from initializer
        TypeSyntax typeSyntax;
        if (varDecl.Type != null && varDecl.Type.Name == "auto")
        {
            // Infer type from initializer
            if (varDecl.InitialValue != null)
            {
                typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
            }
            else
            {
                // No initializer - default to object
                typeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
            }
        }
        else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
        }
        else
        {
            typeSyntax = _typeMapper.MapType(varDecl.Type);
        }

        VariableDeclaratorSyntax declarator;
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var value = GenerateExpression(varDecl.InitialValue);
                declarator = VariableDeclarator(Identifier(varName))
                    .WithInitializer(EqualsValueClause(value));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }
        else
        {
            declarator = VariableDeclarator(Identifier(varName));
        }

        var declaration = VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(declarator));

        // Module-level fields must be static
        // For const variables, try to use C# const if the initializer is a compile-time literal
        // Otherwise fall back to public static readonly
        // Regular variables become "public static"
        SyntaxTokenList modifiers;
        if (varDecl.IsConst && IsCompileTimeLiteral(varDecl.InitialValue))
        {
            // Use const for compile-time literals
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.ConstKeyword));
        }
        else if (varDecl.IsConst)
        {
            // Use static readonly for non-literal const values
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword));
        }
        else
        {
            // Regular variables become public static
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword));
        }

        return FieldDeclaration(declaration)
            .WithModifiers(modifiers);
    }

    private static bool IsCompileTimeLiteral(Expression? expr)
    {
        // Check if the expression is a compile-time literal that can be used with C# const
        return expr switch
        {
            IntegerLiteral => true,
            FloatLiteral => true,
            StringLiteral => true,
            BooleanLiteral => true,
            NoneLiteral => true,
            _ => false
        };
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

        // If there's no else clause, generate simple while loop
        if (whileStmt.ElseBody.Count == 0)
        {
            var body = Block(whileStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            return WhileStatement(condition, body);
        }

        // Loop with else clause: use boolean flag pattern
        // bool _loopCompleted = true;
        // while (condition) { ... if (break) { _loopCompleted = false; break; } }
        // if (_loopCompleted) { elseBody }
        var flagName = GenerateTempVarName("loopCompleted");
        var statements = new List<StatementSyntax>();

        // bool _loopCompleted = true;
        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

        // Transform the body to set flag to false before break
        var transformedBody = TransformLoopBodyForElse(whileStmt.Body, flagName);
        var bodyBlock = Block(transformedBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        // while (condition) { transformedBody }
        statements.Add(WhileStatement(condition, bodyBlock));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(whileStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

        return Block(statements);
    }

    private StatementSyntax GenerateFor(ForStatement forStmt)
    {
        // For-in loop: for item in items: → foreach (var item in items)
        var iterator = GenerateExpression(forStmt.Iterator);

        // If there's no else clause, generate simple foreach loop
        if (forStmt.ElseBody.Count == 0)
        {
            var body = Block(forStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            return GenerateForEachCore(forStmt.Target, iterator, body);
        }

        // Loop with else clause: use boolean flag pattern
        var flagName = GenerateTempVarName("loopCompleted");
        var statements = new List<StatementSyntax>();

        // bool _loopCompleted = true;
        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

        // Transform the body to set flag to false before break
        var transformedBody = TransformLoopBodyForElse(forStmt.Body, flagName);
        var bodyBlock = Block(transformedBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        // foreach (...) { transformedBody }
        statements.Add(GenerateForEachCore(forStmt.Target, iterator, bodyBlock));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(forStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

        return Block(statements);
    }

    private StatementSyntax GenerateForEachCore(Expression target, ExpressionSyntax iterator, BlockSyntax body)
    {
        if (target is Identifier varName)
        {
            var loopVar = NameMangler.ToCamelCase(varName.Name);
            return ForEachStatement(
                IdentifierName("var"),
                Identifier(loopVar),
                iterator,
                body);
        }

        // Handle tuple unpacking in for loops: for x, y in items
        if (target is TupleLiteral tuple)
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
                        var name = NameMangler.ToCamelCase(id.Name);
                        return SingleVariableDesignation(Identifier(name));
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

        throw new NotImplementedException($"For loop target type not supported: {target.GetType().Name}");
    }

    private StatementSyntax GenerateTry(TryStatement tryStmt)
    {
        // If there's an else clause, we need to use a flag pattern:
        // bool __trySucceeded = false;
        // try { ... __trySucceeded = true; }
        // catch { ... }
        // finally { ... }
        // if (__trySucceeded) { else_body }
        if (tryStmt.ElseBody.Count > 0)
        {
            return GenerateTryWithElse(tryStmt);
        }

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

    private StatementSyntax GenerateTryWithElse(TryStatement tryStmt)
    {
        // Generate: bool __trySucceeded = false;
        var flagName = GenerateTempVarName("trySucceeded");
        var flagDecl = LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression))))));

        // Generate try body with flag set to true at the end
        var tryBodyStatements = tryStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>().ToList();
        tryBodyStatements.Add(ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(flagName),
                LiteralExpression(SyntaxKind.TrueLiteralExpression))));
        var tryBlock = Block(tryBodyStatements);

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

        var tryCatchFinally = TryStatement(tryBlock, List(catchClauses), finallyClause);

        // Generate: if (__trySucceeded) { else_body }
        var elseBlock = Block(tryStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        var elseIf = IfStatement(IdentifierName(flagName), elseBlock);

        // Return a block containing all statements
        return Block(flagDecl, tryCatchFinally, elseIf);
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
            // Handle 'self' -> 'this' conversion for instance methods
            Identifier name when string.Equals(name.Name, "self", StringComparison.OrdinalIgnoreCase) => ThisExpression(),
            Identifier name => IdentifierName(GetMangledVariableName(name.Name, isNewDeclaration: false)),
            SuperExpression => BaseExpression(),  // super() -> base
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
            TypeCoercion coercion => GenerateTypeCoercion(coercion),
            TypeCheck check => GenerateTypeCheck(check),
            Parenthesized paren => ParenthesizedExpression(GenerateExpression(paren.Expression)),

            // F-strings
            FStringLiteral fstring => GenerateFString(fstring),

            // Try/Maybe expressions
            TryExpression tryExpr => GenerateTryExpression(tryExpr),
            MaybeExpression maybeExpr => GenerateMaybeExpression(maybeExpr),

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
            // Check if this is a type instantiation (calling a class or struct constructor)
            // We check both:
            // 1. The _classNames and _structNames sets (populated during type declaration generation)
            // 2. The symbol table (for testing and imported types)
            var symbol = _context.LookupSymbol(funcName.Name);
            var isTypeInstantiation = _classNames.Contains(funcName.Name) ||
                                     _structNames.Contains(funcName.Name) ||
                                     (symbol is TypeSymbol typeSymbol &&
                                      (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
                                       typeSymbol.TypeKind == Semantic.TypeKind.Struct));

            var name = _context.IsBuiltinFunction(funcName.Name)
                ? $"global::Sharpy.Core.Exports.{NameMangler.ToPascalCase(funcName.Name)}"
                : NameMangler.ToPascalCase(funcName.Name);

            // Generate positional arguments
            var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));

            // Generate keyword arguments with named syntax
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(kwarg.Name))));

            // Combine positional and keyword arguments
            var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

            // For type instantiation (class or struct), generate 'new TypeName(args)' instead of 'TypeName(args)'
            if (isTypeInstantiation)
            {
                return ObjectCreationExpression(ParseName(name))
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
            }

            // Regular function call
            return InvocationExpression(ParseName(name))
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Handle method calls on objects: obj.method() or ClassName.static_method()
        if (call.Function is MemberAccess memberAccess)
        {
            var obj = GenerateExpression(memberAccess.Object);

            // Apply name mangling to method name
            var methodName = NameMangler.ToPascalCase(memberAccess.Member);

            // Generate positional arguments
            var positionalArgs = call.Arguments.Select(arg => Argument(GenerateExpression(arg)));

            // Generate keyword arguments with named syntax
            var keywordArgs = call.KeywordArguments.Select(kwarg =>
                Argument(GenerateExpression(kwarg.Value))
                    .WithNameColon(NameColon(IdentifierName(NameMangler.ToCamelCase(kwarg.Name)))));

            // Combine positional and keyword arguments
            var allArgs = positionalArgs.Concat(keywordArgs).ToArray();

            // Handle null conditional method calls: obj?.Method(args)
            if (memberAccess.IsNullConditional)
            {
                // Generate: obj?.Method(args)
                // Uses ConditionalAccessExpression with MemberBindingExpression for the method
                // followed by InvocationExpression for the call
                var memberBinding = MemberBindingExpression(IdentifierName(methodName));
                var invocation = InvocationExpression(memberBinding)
                    .WithArgumentList(ArgumentList(SeparatedList(allArgs)));

                return ConditionalAccessExpression(obj, invocation);
            }

            // Generate: obj.Method(args)
            var methodAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                obj,
                IdentifierName(methodName));

            return InvocationExpression(methodAccess)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
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
                // x ** y → System.Math.Pow(x, y)
                // Note: We use fully qualified System.Math to avoid conflicts with Sharpy.Math namespace
                return InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("System"),
                            IdentifierName("Math")),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right));

            case BinaryOperator.FloorDivide:
                // x // y → floor division with Python semantics (toward negative infinity)
                // Integer operands: (long)Math.Floor((double)x / y) → result is int64
                // Float operands: Math.Floor(x / y) → result is float type
                var hasFloatOperand = IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right);
                return GenerateFloorDivision(left, right, hasFloatOperand);

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

            case BinaryOperator.PipeForward:
                // x |> f → f(x)
                // x |> f(y) → f(x, y) (prepend to argument list)
                return GeneratePipeForward(binOp.Left, binOp.Right);
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

    /// <summary>
    /// Generate code for pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity in parser)
    /// </summary>
    private ExpressionSyntax GeneratePipeForward(Expression leftExpr, Expression rightExpr)
    {
        var left = GenerateExpression(leftExpr);

        // Case 1: Right side is already a function call - prepend left to its arguments
        // x |> f(y, z) → f(x, y, z)
        if (rightExpr is FunctionCall funcCall)
        {
            // Generate the function name with proper name mangling (same as GenerateCall)
            var func = GeneratePipeCallTarget(funcCall.Function);
            var prependedArg = Argument(left);
            var existingArgs = funcCall.Arguments.Select(a => Argument(GenerateExpression(a)));
            var keywordArgs = funcCall.KeywordArguments.Select(k =>
                Argument(GenerateExpression(k.Value))
                    .WithNameColon(NameColon(IdentifierName(k.Name))));

            var allArgs = new[] { prependedArg }.Concat(existingArgs).Concat(keywordArgs);

            return InvocationExpression(func)
                .WithArgumentList(ArgumentList(SeparatedList(allArgs)));
        }

        // Case 2: Right side is an identifier or member access - call it with left as the only argument
        // x |> f → f(x)
        var right = GeneratePipeCallTarget(rightExpr);
        return InvocationExpression(right)
            .AddArgumentListArguments(Argument(left));
    }

    /// <summary>
    /// Generate the call target expression for a pipe operator.
    /// Handles proper name mangling for function names (PascalCase) and builtin functions.
    /// </summary>
    private ExpressionSyntax GeneratePipeCallTarget(Expression expr)
    {
        if (expr is Identifier funcName)
        {
            // Use the same name mangling logic as GenerateCall
            var name = _context.IsBuiltinFunction(funcName.Name)
                ? $"global::Sharpy.Core.Exports.{NameMangler.ToPascalCase(funcName.Name)}"
                : NameMangler.ToPascalCase(funcName.Name);
            return ParseName(name);
        }

        // For member access and other expressions, use standard expression generation
        return GenerateExpression(expr);
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
        // Ellipsis (...) in concrete method bodies generates throw NotImplementedException()
        // Note: For abstract methods/interface methods, the ellipsis is ignored and
        // the method has no body (handled in GenerateClassMethod/GenerateInterfaceMethod)
        return ThrowExpression(
            ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(ArgumentList()));
    }

    private ExpressionSyntax GenerateListLiteral(ListLiteral list)
    {
        // new global::Sharpy.Core.List<T> { elem1, elem2, elem3 }
        // Prefer target type annotation if available (e.g., list[int] = [...])
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "list" &&
            _targetTypeContext.TypeArguments.Count > 0)
        {
            // Use the declared element type from the target type annotation
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            // Fall back to inference from elements
            elementType = _typeMapper.InferElementType(list.Elements);
        }

        var elements = list.Elements.Select(GenerateExpression);

        var listType = GenericName("global::Sharpy.Core.List")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new global::Sharpy.Core.Dict<K,V> { { key1, value1 }, { key2, value2 } }
        // Prefer target type annotation if available (e.g., dict[str, int] = {...})
        TypeSyntax keyType, valueType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "dict" &&
            _targetTypeContext.TypeArguments.Count >= 2)
        {
            keyType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
            valueType = _typeMapper.MapType(_targetTypeContext.TypeArguments[1]);
        }
        else
        {
            keyType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Key));
            valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));
        }

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key),
                    GenerateExpression(entry.Value)
                })));

        var dictType = GenericName("global::Sharpy.Core.Dict")
            .AddTypeArgumentListArguments(keyType, valueType);

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new global::Sharpy.Core.Set<T> { elem1, elem2, elem3 }
        // Prefer target type annotation if available (e.g., set[int] = {...})
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "set" &&
            _targetTypeContext.TypeArguments.Count > 0)
        {
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            elementType = _typeMapper.InferElementType(set.Elements);
        }

        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("global::Sharpy.Core.Set")
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
        // Check for nested module access (e.g., lib.math.add -> Lib.Math.Add)
        // This must be checked before enum handling to ensure module paths take precedence
        if (TryExtractModulePath(memberAccess, out var modulePath))
        {
            return BuildModuleAccessExpression(modulePath);
        }

        // Check for enum member access (e.g., Color.RED -> Color.Red)
        if (memberAccess.Object is Identifier enumTypeIdentifier)
        {
            var symbol = _context.LookupSymbol(enumTypeIdentifier.Name);

            // If this is an enum type, handle member access specially
            if (symbol is TypeSymbol { TypeKind: Semantic.TypeKind.Enum })
            {
                // Enum member access: Color.RED -> Color.Red
                var enumTypeName = NameMangler.ToPascalCase(enumTypeIdentifier.Name);
                var enumMemberName = TransformEnumMemberName(memberAccess.Member);

                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(enumTypeName),
                    IdentifierName(enumMemberName));
            }
        }

        var obj = GenerateExpression(memberAccess.Object);

        // Handle special .value property for enum instances
        // enum_instance.value -> (int)enum_instance
        if (string.Equals(memberAccess.Member, "value", StringComparison.OrdinalIgnoreCase))
        {
            // Only cast to int if the object expression is of an enum type
            if (IsEnumTypeExpression(memberAccess.Object))
            {
                return CastExpression(
                    PredefinedType(Token(SyntaxKind.IntKeyword)),
                    obj);
            }
        }

        // Apply name mangling to member names - fields and methods use PascalCase in generated C#
        var mangledMemberName = NameMangler.ToPascalCase(memberAccess.Member);
        var member = IdentifierName(mangledMemberName);

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

    /// <summary>
    /// Attempts to extract a module path from a member access chain.
    /// For example, lib.math.add becomes ["lib", "math", "add"].
    /// Returns true if the entire chain represents module access, false otherwise.
    /// </summary>
    private bool TryExtractModulePath(MemberAccess memberAccess, out List<string> modulePath)
    {
        modulePath = new List<string>();

        // Build the path by traversing the member access chain
        Expression current = memberAccess;
        while (current is MemberAccess ma)
        {
            // Add the member name to the front of the list
            modulePath.Insert(0, ma.Member);
            current = ma.Object;
        }

        // The base should be an identifier
        if (current is not Identifier identifier)
        {
            modulePath.Clear();
            return false;
        }

        // Add the base identifier to the front
        modulePath.Insert(0, identifier.Name);

        // Now check if this path represents module access
        // We need at least 2 parts (e.g., lib.math)
        if (modulePath.Count < 2)
        {
            modulePath.Clear();
            return false;
        }

        // Check if the base is a module symbol
        var baseSymbol = _context.LookupSymbol(modulePath[0]);
        if (baseSymbol is not ModuleSymbol)
        {
            modulePath.Clear();
            return false;
        }

        // Verify that the path exists in the module hierarchy
        var currentModule = (ModuleSymbol)baseSymbol;  // Safe cast - we already checked it's a ModuleSymbol
        for (int i = 1; i < modulePath.Count; i++)
        {
            var memberName = modulePath[i];

            // Check if this member exists in the current module's exports
            if (!currentModule.Exports.TryGetValue(memberName, out var exportedSymbol))
            {
                modulePath.Clear();
                return false;
            }

            // If this is not the last element, it should be a nested module
            if (i < modulePath.Count - 1)
            {
                if (exportedSymbol is not ModuleSymbol nestedModule)
                {
                    modulePath.Clear();
                    return false;
                }
                currentModule = nestedModule;
            }
            // The last element can be any symbol (function, variable, or module)
        }

        return true;
    }

    /// <summary>
    /// Builds a C# member access expression from a module path.
    /// For example, ["lib", "math", "add"] becomes Lib.Math.Add.
    /// Special handling for imported modules: if the base is an imported module with a using alias,
    /// use the alias directly. For example, ["config", "MAX_SIZE"] with "import config" becomes
    /// "config.MaxSize" (using the alias created by the using directive).
    /// </summary>
    private ExpressionSyntax BuildModuleAccessExpression(List<string> modulePath)
    {
        if (modulePath.Count == 0)
        {
            throw new ArgumentException("Module path cannot be empty", nameof(modulePath));
        }

        // Check if this is a simple two-part access (e.g., config.MAX_SIZE)
        // where the first part is an imported module that has a using alias
        if (modulePath.Count == 2)
        {
            var baseModule = modulePath[0];
            var memberName = modulePath[1];

            // Check if the base is an imported module symbol
            var baseSymbol = _context.LookupSymbol(baseModule);
            if (baseSymbol is ModuleSymbol)
            {
                // For simple imports like "import config", we generate a using alias
                // "using config = TestProject.Config.Config;", so we can use the alias directly
                // Generate: alias.Member (e.g., config.MaxSize)
                var aliasName = baseModule.Replace(".", "_"); // Same sanitization as in GenerateImportUsings
                var mangledMemberName = NameMangler.ToPascalCase(memberName);
                return MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(aliasName),
                    IdentifierName(mangledMemberName));
            }
        }

        // For multi-part module paths (e.g., lib.math.add) or other cases,
        // build the full qualified path (e.g., Lib.Math.Add)
        ExpressionSyntax current = IdentifierName(NameMangler.ToPascalCase(modulePath[0]));

        // Chain the rest of the path
        for (int i = 1; i < modulePath.Count; i++)
        {
            var memberName = NameMangler.ToPascalCase(modulePath[i]);
            current = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                current,
                IdentifierName(memberName));
        }

        return current;
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
        // For simplicity in v0.6, we'll allow re-evaluation
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

    private ExpressionSyntax GenerateTypeCoercion(TypeCoercion coercion)
    {
        // The `to` operator:
        // - value to T → (T)value (throws InvalidCastException on failure)
        // - value to T? → value as T (for reference types, returns null on failure)
        //                 value is T _temp ? (T?)_temp : null (for value types)

        var value = GenerateExpression(coercion.Value);

        if (coercion.TargetType.IsNullable)
        {
            // Safe form: value to T?
            // Create a non-nullable version of the target type for the 'as' expression
            var baseType = new TypeAnnotation
            {
                Name = coercion.TargetType.Name,
                TypeArguments = coercion.TargetType.TypeArguments,
                IsNullable = false
            };
            var baseTypeSyntax = _typeMapper.MapType(baseType);
            var nullableTypeSyntax = _typeMapper.MapType(coercion.TargetType);

            // Check if this is a value type (primitives are value types except string/object)
            var primitiveInfo = PrimitiveCatalog.GetByName(coercion.TargetType.Name);
            bool isValueType = primitiveInfo != null &&
                               primitiveInfo.ClrType != typeof(string) &&
                               primitiveInfo.ClrType != typeof(object) &&
                               primitiveInfo.ClrType != typeof(void);

            if (isValueType)
            {
                // For value types: value is T _temp ? (T?)_temp : null
                // Generate unique temp variable name
                var tempName = $"__coerce_temp_{_tempVarCounter++}";

                // value is T tempName ? (T?)tempName : (T?)null
                return ConditionalExpression(
                    IsPatternExpression(
                        value,
                        DeclarationPattern(
                            baseTypeSyntax,
                            SingleVariableDesignation(Identifier(tempName)))),
                    CastExpression(nullableTypeSyntax, IdentifierName(tempName)),
                    CastExpression(nullableTypeSyntax, LiteralExpression(SyntaxKind.NullLiteralExpression)));
            }
            else
            {
                // For reference types: value as T
                return BinaryExpression(
                    SyntaxKind.AsExpression,
                    value,
                    baseTypeSyntax);
            }
        }
        else
        {
            // Throwing form: value to T → (T)value
            var targetType = _typeMapper.MapType(coercion.TargetType);
            return CastExpression(targetType, value);
        }
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
    private static bool ShouldGenerateDunderMethod(string dunderName)
    {
        // __init__ is explicitly checked here for clarity, even though it IS in ProtocolRegistry.
        // This makes the special constructor handling obvious to readers.
        if (dunderName == "__init__")
            return true;

        // Protocol dunders that map to .NET methods should be generated
        return ProtocolRegistry.IsProtocolDunder(dunderName);
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
            "__truediv__" => GenerateBinaryOperator(funcDef, className, SyntaxKind.SlashToken),
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

    /// <summary>
    /// Generate a try expression: try expr or try[ExceptionType] expr
    /// Wraps the expression in Result[T, E] using a try/catch pattern.
    /// </summary>
    private ExpressionSyntax GenerateTryExpression(TryExpression tryExpr)
    {
        // Generate the operand expression
        var operandExpr = GenerateExpression(tryExpr.Operand);

        // Determine the exception type to catch (default to Exception)
        var exceptionTypeName = tryExpr.ExceptionType != null
            ? _typeMapper.MapType(tryExpr.ExceptionType).ToString()
            : "Exception";

        // Generate: Result.Try(() => operand)
        // or for specific exception type: Result.Try<ExceptionType>(() => operand)
        var lambdaExpr = ParenthesizedLambdaExpression()
            .WithExpressionBody(operandExpr);

        // If exception type is specified and not the default Exception, use generic version
        if (tryExpr.ExceptionType != null && exceptionTypeName != "Exception")
        {
            // Result.Try<ExceptionType>(() => operand)
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("global::Sharpy.Core.Result"),
                    GenericName("Try")
                        .WithTypeArgumentList(TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(IdentifierName(exceptionTypeName))))))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(lambdaExpr))));
        }
        else
        {
            // Result.Try(() => operand)
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("global::Sharpy.Core.Result"),
                    IdentifierName("Try")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(lambdaExpr))));
        }
    }

    /// <summary>
    /// Generate a maybe expression: maybe expr
    /// Wraps the nullable expression in Optional[T].
    /// </summary>
    private ExpressionSyntax GenerateMaybeExpression(MaybeExpression maybeExpr)
    {
        // Generate the operand expression
        var operandExpr = GenerateExpression(maybeExpr.Operand);

        // Generate: Optional.From(operand)
        // This converts a nullable T? to Optional[T]
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("global::Sharpy.Core.Optional"),
                IdentifierName("From")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(operandExpr))));
    }

    /// <summary>
    /// Generate a unique temporary variable name
    /// </summary>
    private string GenerateTempVarName(string prefix)
    {
        return $"__{prefix}_{_tempVarCounter++}";
    }

    /// <summary>
    /// Transform loop body statements for else clause support.
    /// Wraps break statements with flag assignment: { flag = false; break; }
    /// </summary>
    private List<Statement> TransformLoopBodyForElse(List<Statement> body, string flagName)
    {
        var result = new List<Statement>();
        foreach (var stmt in body)
        {
            result.Add(TransformStatementForLoopElse(stmt, flagName));
        }
        return result;
    }

    /// <summary>
    /// Transform a single statement for loop else support.
    /// Recursively handles nested structures.
    /// </summary>
    private Statement TransformStatementForLoopElse(Statement stmt, string flagName)
    {
        return stmt switch
        {
            // Transform break statements to set flag before breaking
            BreakStatement breakStmt => new BreakWithFlagStatement
            {
                FlagName = flagName,
                LineStart = breakStmt.LineStart,
                ColumnStart = breakStmt.ColumnStart,
                LineEnd = breakStmt.LineEnd,
                ColumnEnd = breakStmt.ColumnEnd
            },

            // Recursively transform if statements
            IfStatement ifStmt => ifStmt with
            {
                ThenBody = TransformLoopBodyForElse(ifStmt.ThenBody, flagName),
                ElifClauses = ifStmt.ElifClauses.Select(e => e with
                {
                    Body = TransformLoopBodyForElse(e.Body, flagName)
                }).ToList(),
                ElseBody = TransformLoopBodyForElse(ifStmt.ElseBody, flagName)
            },

            // Don't transform nested loops - their break statements apply to their own loop
            WhileStatement _ => stmt,
            ForStatement _ => stmt,

            // All other statements pass through unchanged
            _ => stmt
        };
    }

    /// <summary>
    /// Checks if an expression evaluates to a floating-point type.
    /// Used to determine floor division semantics.
    /// </summary>
    private bool IsFloatExpression(Expression expr)
    {
        return expr switch
        {
            FloatLiteral => true,
            UnaryOp unary => IsFloatExpression(unary.Operand),
            BinaryOp binOp => binOp.Operator switch
            {
                // Division always produces float
                BinaryOperator.Divide => true,
                // Power produces float (Math.Pow returns double)
                BinaryOperator.Power => true,
                // Floor division depends on operands
                BinaryOperator.FloorDivide => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right),
                // Other operators: float if either operand is float
                _ => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right)
            },
            Parenthesized paren => IsFloatExpression(paren.Expression),
            // For other expressions (variables, function calls, etc.), assume integer
            // A full type system would resolve these properly
            _ => false
        };
    }

    /// <summary>
    /// Generates floor division expression with correct Python semantics.
    /// Floors toward negative infinity (not truncation toward zero).
    /// - Integer operands: (int)Math.Floor((double)a / b) → result is int32 (pragmatic for .NET)
    /// - Float operands: Math.Floor((double)(a / b)) → result is double (cast to avoid CS0121 ambiguity)
    /// Note: Spec says integer floor division should return int64, but we return int32
    /// for .NET compatibility with most use cases (augmented assignment, common variables).
    /// </summary>
    private ExpressionSyntax GenerateFloorDivision(ExpressionSyntax left, ExpressionSyntax right, bool hasFloatOperand)
    {
        // System.Math.Floor((double)(left / right)) for both cases
        // Note: We use fully qualified System.Math to avoid conflicts with Sharpy.Math namespace
        // Note: We always cast to double to avoid CS0121 ambiguity between Math.Floor(double) and Math.Floor(decimal)
        var divisionExpr = BinaryExpression(SyntaxKind.DivideExpression,
            hasFloatOperand ? left : CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
            right);

        // Cast division result to double to resolve Math.Floor overload ambiguity
        var castToDouble = CastExpression(
            PredefinedType(Token(SyntaxKind.DoubleKeyword)),
            ParenthesizedExpression(divisionExpr));

        var floorCall = InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System"),
                    IdentifierName("Math")),
                IdentifierName("Floor")))
            .AddArgumentListArguments(Argument(castToDouble));

        // For integer operands, cast to int (pragmatic .NET-first approach);
        // for float operands, return as-is (double from Math.Floor)
        return hasFloatOperand
            ? floorCall
            : CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), floorCall);
    }

    /// <summary>
    /// Checks if an expression evaluates to an enum type.
    /// Used to determine whether .value access should be translated to an int cast.
    /// </summary>
    private bool IsEnumTypeExpression(Expression expr)
    {
        if (expr is Identifier id)
        {
            var symbol = _context.LookupSymbol(id.Name);
            if (symbol is VariableSymbol varSymbol &&
                varSymbol.Type is Semantic.UserDefinedType udt &&
                udt.Symbol?.TypeKind == Semantic.TypeKind.Enum)
            {
                return true;
            }
        }
        return false;
    }
}
