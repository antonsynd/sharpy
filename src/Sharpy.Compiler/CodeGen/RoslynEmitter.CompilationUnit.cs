using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Compilation unit, namespace and import generation
/// </summary>
public partial class RoslynEmitter
{
    public CompilationUnitSyntax GenerateCompilationUnit(Module module)
    {
        // Note: From-import symbol tracking is now handled by CodeGenInfo during semantic analysis.
        // The CodeGenInfoComputer.ProcessFromImport method sets CodeGenInfo.CSharpName and
        // CodeGenInfo.OriginalImportName for proper symbol name resolution.

        // Collect all using directives from import statements
        var usingDirectives = GenerateUsingDirectives(module);

        // Separate imports from other statements
        var nonImportStatements = module.Body
            .Where(s => s is not ImportStatement && s is not FromImportStatement)
            .ToList();

        // Collect from-import statements with re-exports for generating delegating members
        // Only generate re-export members for non-entry-point files (i.e., library modules/packages)
        // Entry point files should not re-export - they just use the imports
        List<FromImportStatement>? fromImports = null;
        if (!_context.IsEntryPoint)
        {
            fromImports = module.Body
                .OfType<FromImportStatement>()
                .Where(f => HasReExportedSymbols(f))
                .ToList();
        }

        // Generate module members: module class (with fields, methods) + namespace-level types
        // Types (classes, structs, interfaces, enums) are placed at namespace level as siblings,
        // matching C# conventions. This gives cleaner qualified names like MyProject.Geometry.Point
        // instead of MyProject.Geometry.Exports.Point.
        var (moduleClass, namespaceTypes) = GenerateModuleMembers(nonImportStatements, fromImports);

        // Combine module class with namespace-level types
        // Module class comes first, followed by type declarations
        var namespaceMembers = new List<MemberDeclarationSyntax> { moduleClass };
        namespaceMembers.AddRange(namespaceTypes);

        // Generate namespace from source file path (if available)
        // Use block-scoped namespace for C# 9.0 compatibility (Unity)
        var namespaceName = GenerateNamespaceName();
        var namespaceDecl = NamespaceDeclaration(namespaceName)
            .WithMembers(List(namespaceMembers));

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
        // EXCEPT for __init__.spy files, which represent the package itself
        if (!string.IsNullOrEmpty(fileName) && fileName != "__init__")
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
                    // import module as alias -> using alias = ProjectNamespace.Module.Exports; (for Sharpy modules)
                    // Sharpy modules expose their members via a class named "Exports"
                    const string exportsClassName = "Exports";

                    // Build full path: <ProjectNamespace>.<ModuleNamespace>.Exports
                    string fullModuleClass;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}.{exportsClassName}";
                    }
                    else
                    {
                        // Fallback for single-file compilation without project namespace
                        fullModuleClass = $"{namespaceName}.{exportsClassName}";
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
                    // import module -> using module_alias = ProjectNamespace.Module.Exports; (Sharpy module)
                    // Convert "utils.helpers" to "utils_helpers" for valid C# identifier
                    // Also escape C# reserved keywords like "base" -> "@base"
                    var sanitizedAlias = EscapeCSharpKeyword(alias.Name.Replace(".", "_"));

                    // Sharpy modules expose their members via a class named "Exports"
                    const string exportsClassName = "Exports";

                    // Build full path: <ProjectNamespace>.<ModuleNamespace>.Exports
                    // For example:
                    //   - "config" → "TestProject.Config.Exports"
                    //   - "lib.math.operations" → "TestProject.Lib.Math.Operations.Exports"
                    string fullModuleClass;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}.{exportsClassName}";
                    }
                    else
                    {
                        // Fallback for single-file compilation without project namespace
                        fullModuleClass = $"{namespaceName}.{exportsClassName}";
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
            // e.g., "from config import MAX_SIZE" → "using static TestProject.Config.Exports;"
            //
            // The module class is always named "Exports" (see GetModuleClassName),
            // not the module name duplicated. For nested modules like "lib.math.operations":
            // - Namespace: TestProject.Lib.Math.Operations
            // - Class: Exports
            // - Full path: TestProject.Lib.Math.Operations.Exports

            // Use ResolvedModulePath for relative imports (e.g., ".helpers" → "mypackage.helpers")
            // Fall back to Module for non-relative imports or when resolution hasn't been performed
            var moduleName = GetResolvedModulePath(fromImport) ?? fromImport.Module;
            var moduleNamespacePath = ConvertModuleNameToNamespace(moduleName);

            // The module class is always named "Exports" (not the module name)
            const string moduleClassName = "Exports";

            // Build full path: <ProjectNamespace>.<ModuleNamespace>.Exports
            // For example:
            //   - "config" → "TestProject.Config.Exports"
            //   - "lib.math.operations" → "TestProject.Lib.Math.Operations.Exports"
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

            // Also generate using statements for namespaces where re-exported types are actually defined.
            // This handles the case where a package __init__.spy re-exports types from submodules.
            // For example:
            //   - mypackage/__init__.spy re-exports SomeClass from mypackage.submodule
            //   - When we "from mypackage import SomeClass", we need:
            //     - using static TestProject.Mypackage.Exports; (for the import)
            //     - using TestProject.Mypackage.Submodule; (for the actual type namespace)
            foreach (var usingDirective in GenerateReExportedTypeNamespaceUsings(fromImport, moduleName))
            {
                yield return usingDirective;
            }
        }
    }

    /// <summary>
    /// Generates using statements for namespaces where re-exported types are actually defined.
    /// When importing from a package that re-exports types from submodules, we need using
    /// statements for those submodule namespaces to resolve the type references.
    /// </summary>
    private IEnumerable<UsingDirectiveSyntax> GenerateReExportedTypeNamespaceUsings(
        FromImportStatement fromImport,
        string importModuleName)
    {
        var reExportedSymbols = GetReExportedSymbols(fromImport);
        if (reExportedSymbols == null || reExportedSymbols.Count == 0)
        {
            yield break;
        }

        var addedNamespaces = new HashSet<string>();

        foreach (var (_, symbol) in reExportedSymbols)
        {
            if (symbol is TypeSymbol typeSymbol && !string.IsNullOrEmpty(typeSymbol.DefiningModule))
            {
                // If the type's DefiningModule differs from the import module, we need a using statement
                // for the namespace where the type is actually defined.
                if (!string.Equals(typeSymbol.DefiningModule, importModuleName, StringComparison.OrdinalIgnoreCase))
                {
                    var definingNamespace = ConvertModuleNameToNamespace(typeSymbol.DefiningModule);

                    // Build full namespace path
                    string fullNamespace;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullNamespace = $"{_context.ProjectNamespace}.{definingNamespace}";
                    }
                    else
                    {
                        fullNamespace = definingNamespace;
                    }

                    // Only add each namespace once
                    if (addedNamespaces.Add(fullNamespace))
                    {
                        yield return UsingDirective(ParseName(fullNamespace));
                    }
                }
            }
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

    /// <summary>
    /// Checks if a name appears to be in CONSTANT_CASE (ALL_CAPS with optional underscores).
    /// Used to determine proper casing for symbols imported via "from X import Y".
    /// </summary>
    private static bool IsConstantCaseName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // A constant name should contain at least one letter and be all uppercase
        // Allowed characters: uppercase letters, digits, underscores
        bool hasLetter = false;
        foreach (char c in name)
        {
            if (char.IsLetter(c))
            {
                if (!char.IsUpper(c))
                    return false;
                hasLetter = true;
            }
            else if (!char.IsDigit(c) && c != '_')
            {
                return false;
            }
        }
        return hasLetter;
    }

    /// <summary>
    /// C# keywords that need @ prefix when used as identifiers.
    /// </summary>
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object",
        "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while"
    };

    /// <summary>
    /// Escapes C# keywords by prefixing with @ symbol.
    /// </summary>
    private static string EscapeCSharpKeyword(string name)
    {
        return CSharpKeywords.Contains(name) ? "@" + name : name;
    }

}
