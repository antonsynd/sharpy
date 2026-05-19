using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Compilation unit, namespace and import generation
/// </summary>
internal partial class RoslynEmitter
{
    public CompilationUnitSyntax GenerateCompilationUnit(Module module)
    {
        _context.Logger.LogInfo("Starting code generation");

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
        // Only generate re-export members for package __init__.spy files
        // Regular library modules should not re-export to avoid CS0229 ambiguity
        // when main imports from both module A and module B (where B imports from A)
        List<FromImportStatement>? fromImports = null;
        if (!_context.IsEntryPoint && _context.IsPackageInit)
        {
            fromImports = module.Body
                .OfType<FromImportStatement>()
                .Where(f => HasReExportedSymbols(f))
                .ToList();
        }

        // Generate module class with all members nested inside
        var moduleClass = GenerateModuleMembers(nonImportStatements, fromImports);

        // Compute directory wrapper classes and wrap the module class
        var wrapperNames = ComputeWrapperClasses();
        MemberDeclarationSyntax current = moduleClass;

        // Build wrapper classes from inside out
        for (int i = wrapperNames.Count - 1; i >= 0; i--)
        {
            current = ClassDeclaration(wrapperNames[i])
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.PartialKeyword)))
                .WithMembers(SingletonList(current));
        }

        // Build compilation unit: use namespace wrapper for multi-file projects,
        // global namespace (no wrapper) for single-file compilation
        CompilationUnitSyntax compilationUnit;
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            // Multi-file project: wrap in namespace
            var namespaceName = GenerateNamespaceName();
            var namespaceDecl = NamespaceDeclaration(namespaceName)
                .WithMembers(SingletonList(current));

            compilationUnit = CompilationUnit()
                .WithUsings(List(usingDirectives))
                .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDecl))
                .NormalizeWhitespace();
        }
        else
        {
            // Single-file: emit class directly (global namespace)
            compilationUnit = CompilationUnit()
                .WithUsings(List(usingDirectives))
                .WithMembers(SingletonList(current))
                .NormalizeWhitespace();
        }

        // Add #nullable enable directive to enable C# nullable reference types
        // This aligns with Sharpy's "null-safe by default" principle (Axiom 3)
        // Must be added AFTER NormalizeWhitespace to preserve leading position
        var nullablePragma = ParseLeadingTrivia("#nullable enable\n\n");

        _context.Logger.LogInfo($"Completed code generation ({nonImportStatements.Count} statements emitted)");
        return compilationUnit.WithLeadingTrivia(nullablePragma);
    }

    /// <summary>
    /// Returns only the project-level namespace. Directory and file hierarchy
    /// is expressed via nested static classes, not namespace components.
    /// Only called for multi-file projects (single-file uses global namespace).
    /// </summary>
    private NameSyntax GenerateNamespaceName()
    {
        // With project namespace, use it directly
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
        {
            return ParseName(_context.ProjectNamespace);
        }

        return ParseName("SharpyGenerated");
    }

    /// <summary>
    /// Computes the list of wrapper class names from the directory path.
    /// For regular files: all directory parts are wrappers.
    /// For __init__.spy: all directory parts EXCEPT the last are wrappers
    /// (the last directory is the module class itself).
    /// </summary>
    private List<string> ComputeWrapperClasses()
    {
        if (string.IsNullOrEmpty(_context.ProjectRootPath) ||
            string.IsNullOrEmpty(_context.SourceFilePath))
        {
            return new List<string>();
        }

        var relativePath = Path.GetRelativePath(_context.ProjectRootPath, _context.SourceFilePath);
        var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);

        if (string.IsNullOrEmpty(relativeDir) || relativeDir == ".")
        {
            // Root-level file: no wrappers needed
            return new List<string>();
        }

        var dirParts = relativeDir
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => NameMangler.ToNamespacePart(p))
            .ToList();

        if (fileName == DunderNames.Init)
        {
            // __init__.spy: last directory is the module class, not a wrapper
            // e.g., pkg/sub/__init__.spy → wrappers = [Pkg], module = Sub
            if (dirParts.Count > 0)
                dirParts.RemoveAt(dirParts.Count - 1);
        }

        return dirParts;
    }

    private List<UsingDirectiveSyntax> GenerateUsingDirectives(Module module)
    {
        var usings = new List<UsingDirectiveSyntax>();

        // Add default System usings
        usings.Add(UsingDirective(ParseName("System")));
        usings.Add(UsingDirective(ParseName("System.Collections.Generic")));
        usings.Add(UsingDirective(ParseName("System.Linq")));
        usings.Add(UsingDirective(ParseName("System.Threading.Tasks")));

        // Add Sharpy runtime usings (use global:: to avoid conflicts when output namespace contains "Sharpy")
        usings.Add(UsingDirective(MakeGlobalQualifiedName("Sharpy")));

        // Add project namespace using to make nested module classes accessible without
        // full qualification (e.g., 'using TestProject;' lets code reference 'Utils.Helper()').
        // Skip if the project namespace is "Sharpy" since global::Sharpy already covers it
        // (adding both causes CS0105 duplicate using warning).
        if (!string.IsNullOrEmpty(_context.ProjectNamespace) &&
            _context.ProjectNamespace != "Sharpy")
        {
            usings.Add(UsingDirective(ParseName(_context.ProjectNamespace)));
        }

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

        // Add `using Xunit;` for files containing any @test-decorated functions or methods.
        if (HasTestDecoratedMembers(module))
        {
            usings.Add(UsingDirective(ParseName("Xunit")));
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
            // Synthetic modules (asyncio) have no C# namespace — skip using directives
            if (IsSyntheticModule(alias.Name))
                continue;

            // Convert Python module name to C# namespace/class path
            var namespaceName = ConvertModuleNameToNamespace(alias.Name);
            var isNetFramework = IsNetFrameworkNamespace(alias.Name);

            if (alias.AsName != null)
            {
                if (isNetFramework)
                {
                    // import system.io as io -> using io = System.IO;
                    yield return UsingDirective(
                        NameEquals(alias.AsName),
                        ParseName(namespaceName));
                }
                else if (IsStdlibModule(alias.Name))
                {
                    // import math as m -> using m = global::Sharpy.Math;
                    var fullModuleClass = ConvertStdlibModuleToFullyQualified(alias.Name);
                    yield return UsingDirective(
                        NameEquals(alias.AsName),
                        ParseName(fullModuleClass));
                }
                else
                {
                    // import module as alias -> using alias = ProjectNamespace.Module;
                    string fullModuleClass;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}";
                    }
                    else
                    {
                        fullModuleClass = namespaceName;
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
                    // import system.io -> using System.IO;
                    yield return UsingDirective(ParseName(namespaceName));
                }
                else if (IsStdlibModule(alias.Name))
                {
                    // import math -> using math = global::Sharpy.Math;
                    var sanitizedAlias = EscapeCSharpKeyword(alias.Name.Replace(".", "_", StringComparison.Ordinal));
                    var fullModuleClass = ConvertStdlibModuleToFullyQualified(alias.Name);
                    yield return UsingDirective(
                        NameEquals(sanitizedAlias),
                        ParseName(fullModuleClass));
                }
                else
                {
                    // import module -> using module_alias = ProjectNamespace.Module;
                    var sanitizedAlias = EscapeCSharpKeyword(alias.Name.Replace(".", "_", StringComparison.Ordinal));

                    // e.g., "config" → "TestProject.Config"
                    // e.g., "lib.math.operations" → "TestProject.Lib.Math.Operations"
                    string fullModuleClass;
                    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                    {
                        fullModuleClass = $"{_context.ProjectNamespace}.{namespaceName}";
                    }
                    else
                    {
                        fullModuleClass = namespaceName;
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
        // Synthetic modules (asyncio) have no C# namespace — skip using directives
        if (IsSyntheticModule(fromImport.Module))
            yield break;

        var isNetFramework = IsNetFrameworkNamespace(fromImport.Module);

        if (isNetFramework)
        {
            var namespaceName = ConvertModuleNameToNamespace(fromImport.Module);

            if (fromImport.ImportAll || fromImport.Names.Length == 0)
            {
                // from system.io import * -> using System.IO; (import everything)
                yield return UsingDirective(ParseName(namespaceName));
            }
            else
            {
                // from system import Math -> using Math = global::System.Math;
                // Emit per-name type aliases with global:: prefix to avoid ambiguity
                // when the emitted code's namespace (e.g. Sharpy.Math) shadows .NET types.
                // Skip generic types — C# cannot alias open generic types (e.g., IEquatable<T>),
                // and the base class list already uses fully-qualified names.
                foreach (var importedName in fromImport.Names)
                {
                    // Skip generic CLR types — using alias can't reference open generics
                    var symbol = _context.LookupSymbol(importedName.Name);
                    if (symbol is TypeSymbol { IsGeneric: true })
                        continue;

                    var csharpName = importedName.AsName ?? NameMangler.ToNamespacePart(importedName.Name);
                    var qualifiedName = $"global::{namespaceName}.{NameMangler.ToNamespacePart(importedName.Name)}";
                    yield return UsingDirective(
                        NameEquals(csharpName),
                        ParseName(qualifiedName));
                }
            }
        }
        else if (IsStdlibModule(fromImport.Module))
        {
            // For type-only imports (e.g., "from argparse import ArgumentParser"),
            // skip the using static — the types live in the Sharpy namespace and are
            // already accessible via "using global::Sharpy;".
            var allImportsAreTypes = !fromImport.ImportAll && fromImport.Names.Length > 0 &&
                fromImport.Names.All(n => _context.LookupSymbol(n.Name) is TypeSymbol);
            if (!allImportsAreTypes)
            {
                // Generate using static for Sharpy stdlib module class
                // e.g., "from math import sqrt" → "using static global::Sharpy.Math;"
                // e.g., "from os.path import join" → "using static global::Sharpy.OsPath;"
                var fullModuleClass = ConvertStdlibModuleToFullyQualified(fromImport.Module);
                yield return UsingDirective(ParseName(fullModuleClass))
                    .WithStaticKeyword(Token(SyntaxKind.StaticKeyword));
            }
        }
        else
        {
            // Generate using static for the module class
            // e.g., "from config import MAX_SIZE" → "using static TestProject.Config;"
            // e.g., "from lib.math.operations import add" → "using static TestProject.Lib.Math.Operations;"

            var moduleName = GetResolvedModulePath(fromImport) ?? fromImport.Module;
            var moduleNamespacePath = ConvertModuleNameToNamespace(moduleName);

            // Module class path = ProjectNamespace.ModulePath
            string fullModuleClass;
            if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            {
                fullModuleClass = $"{_context.ProjectNamespace}.{moduleNamespacePath}";
            }
            else
            {
                fullModuleClass = moduleNamespacePath;
            }

            yield return UsingDirective(ParseName(fullModuleClass))
                .WithStaticKeyword(Token(SyntaxKind.StaticKeyword));

            // For imported types that are re-exported from a different module,
            // generate additional using static directives for their defining modules.
            // This handles the pattern: from mypackage import SomeClass
            // where SomeClass is defined in mypackage.submodule but re-exported via __init__.spy.
            if (!fromImport.ImportAll && fromImport.Names.Length > 0)
            {
                foreach (var importedName in fromImport.Names)
                {
                    var symbol = _context.LookupSymbol(importedName.Name);
                    if (symbol is TypeSymbol typeSymbol &&
                        !string.IsNullOrEmpty(typeSymbol.DefiningModule) &&
                        typeSymbol.DefiningModule != moduleName)
                    {
                        var definingModulePath = ConvertModuleNameToNamespace(typeSymbol.DefiningModule);
                        string fullDefiningModuleClass;
                        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
                        {
                            fullDefiningModuleClass = $"{_context.ProjectNamespace}.{definingModulePath}";
                        }
                        else
                        {
                            fullDefiningModuleClass = definingModulePath;
                        }
                        yield return UsingDirective(ParseName(fullDefiningModuleClass))
                            .WithStaticKeyword(Token(SyntaxKind.StaticKeyword));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines if a module name refers to a synthetic (compiler-provided) module.
    /// Synthetic modules (e.g., asyncio) map to special codegen patterns, not real namespaces.
    /// No using directive should be emitted for them.
    /// </summary>
    private static bool IsSyntheticModule(string moduleName)
    {
        return moduleName == Shared.SyntheticModuleNames.Asyncio;
    }

    /// <summary>
    /// Determines if a module name refers to a .NET framework namespace.
    /// .NET framework namespaces use different import handling (no module class wrapper).
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
    /// Check if a module name corresponds to a Sharpy stdlib (.NET) module.
    /// Uses the SemanticBinding registry populated during import resolution.
    /// </summary>
    private bool IsStdlibModule(string moduleName)
        => _context.SemanticBinding.IsNetModule(moduleName);

    /// <summary>
    /// Mapping for stdlib modules whose C# class name differs from the default
    /// PascalCase convention (e.g., because the bare name conflicts with an
    /// existing type or a member name matching the enclosing type).
    /// </summary>
    private static readonly Dictionary<string, string> StdlibClassNameOverrides = new(StringComparer.Ordinal)
    {
        { "argparse", "ArgparseModule" },
        { "bisect", "BisectModule" },
        { "pathlib", "PathlibModule" },
        { "string", "StringModule" },
        { "time", "TimeModule" },
        { "copy", "CopyModule" },
        { "glob", "GlobModule" },
    };

    /// <summary>
    /// Convert a Sharpy stdlib module name to a fully qualified C# class name.
    /// e.g., "math" -> "global::Sharpy.Math", "os.path" -> "global::Sharpy.OsPath"
    /// Stdlib module classes live in the Sharpy namespace with PascalCase names
    /// where dotted parts are concatenated (no dots in the class name).
    /// </summary>
    private static string ConvertStdlibModuleToFullyQualified(string moduleName)
    {
        if (StdlibClassNameOverrides.TryGetValue(moduleName, out var overrideName))
            return $"global::Sharpy.{overrideName}";

        var parts = moduleName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        // Stdlib classes use simple PascalCase (e.g., Json, Os, Re) — not the
        // NameMangler.ToNamespacePart acronym logic used for user module names (which would produce JSON).
        var className = string.Concat(parts.Select(StdlibToPascalCase));
        return $"global::Sharpy.{className}";
    }

    private static string StdlibToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }


    private static string EscapeCSharpKeyword(string name)
    {
        return Shared.CSharpKeywords.EscapeIfNeeded(name);
    }

    /// <summary>
    /// Returns true if the module contains any function or class method decorated with @test.
    /// Used to determine whether `using Xunit;` should be added.
    /// </summary>
    private static bool HasTestDecoratedMembers(Module module)
    {
        static bool IsTest(Decorator d) => !d.IsBracketAttribute && d.Name == DecoratorNames.Test;

        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef fn && fn.Decorators.Any(IsTest))
                return true;

            if (stmt is ClassDef cls)
            {
                foreach (var member in cls.Body)
                {
                    if (member is FunctionDef m && m.Decorators.Any(IsTest))
                        return true;
                }
            }
        }
        return false;
    }
}
