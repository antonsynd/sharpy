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
/// RoslynEmitter partial class: Module class generation
/// </summary>
public partial class RoslynEmitter
{
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

    /// <summary>
    /// Generates the module class and namespace-level type declarations.
    /// Types (classes, structs, interfaces, enums) are placed at namespace level as siblings to the module class,
    /// matching C# conventions for top-level type declarations.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - The module class (static class with fields, functions, Main)
    /// - List of type declarations to place at namespace level
    /// </returns>
    private (ClassDeclarationSyntax moduleClass, List<MemberDeclarationSyntax> namespaceTypes)
        GenerateModuleMembers(List<Statement> statements, List<FromImportStatement>? reExportImports = null)
    {
        // Clear tracking field for module field names (still needed to prevent duplicate field declarations)
        _moduleFieldNames.Clear();

        // Note: Module variable tracking is now handled by CodeGenInfo during semantic analysis.
        // The CodeGenInfoComputer.ComputeForModule method sets CodeGenInfo.IsModuleLevel,
        // CodeGenInfo.HasExecutionOrderIssues, etc. for proper symbol name resolution.

        // Collect interface definitions for abstract class stub generation
        // This allows abstract classes to generate stubs for unimplemented interface methods
        _interfaceDefinitions.Clear();
        foreach (var stmt in statements)
        {
            if (stmt is InterfaceDef interfaceDef)
            {
                _interfaceDefinitions[interfaceDef.Name] = interfaceDef;
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

        // Separate declarations into:
        // - moduleDeclarations: fields, methods, constants (go in module class)
        // - namespaceTypes: classes, structs, interfaces, enums (go at namespace level)
        // - executableStatements: bare statements (wrapped in Main)
        var moduleDeclarations = new List<MemberDeclarationSyntax>();
        var namespaceTypes = new List<MemberDeclarationSyntax>();
        var executableStatements = new List<Statement>();

        // First pass: check if there's a user-defined main function
        // This affects how we handle module-level variable declarations with execution order issues
        bool hasMainFunction = statements.Any(s => s is FunctionDef f && f.Name == "main");

        // When there's a user-defined main(), module-level variables with execution order issues
        // should still be generated as static fields (the user is responsible for execution order)
        _forceModuleLevelFields = hasMainFunction;

        foreach (var stmt in statements)
        {
            var member = GenerateStatement(stmt);

            // After generating class/struct/function declarations, clear local scope tracking
            // so that parameter names from their methods don't leak into module-level code
            if (stmt is ClassDef or StructDef or FunctionDef or InterfaceDef or EnumDef)
            {
                _declaredVariables.Clear();
                _variableVersions.Clear();
                _constVariables.Clear();
            }

            if (member is MemberDeclarationSyntax memberDecl)
            {
                // Route type declarations to namespace level
                if (stmt is ClassDef or StructDef or InterfaceDef or EnumDef)
                {
                    namespaceTypes.Add(memberDecl);
                }
                else
                {
                    // Functions, fields, constants stay in module class
                    moduleDeclarations.Add(memberDecl);
                }
            }
            else if (member == null && stmt is VariableDeclaration varRedefinition)
            {
                // This is a variable redefinition (GenerateModuleLevelField returned null)
                // For const variables, we skip the duplicate entirely (consts can't be redeclared at runtime)
                // For regular variables, add to executable statements so it becomes a local in Main
                if (!varRedefinition.IsConst && !IsConstantCaseName(varRedefinition.Name))
                {
                    executableStatements.Add(stmt);
                }
                // else: skip const redefinitions - first declaration wins
            }
            else if (member == null && stmt is TypeAlias)
            {
                // Type aliases are compile-time only, they don't generate any C# output
                // and should not be treated as executable statements
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

            moduleDeclarations.Add(mainMethod);
        }
        else if (hasMainFunction && executableStatements.Count > 0)
        {
            // There's a main function and also module-level statements
            // Filter to only truly executable statements (not variable declarations with type annotations)
            // VariableDeclaration nodes are typed declarations, not executable statements
            // Note: Use Parser.Ast.VariableDeclaration to avoid conflict with SyntaxFactory.VariableDeclaration
            var trulyExecutableStatements = executableStatements
                .Where(s => s is not Parser.Ast.VariableDeclaration)
                .ToList();

            if (trulyExecutableStatements.Count > 0)
            {
                // This is an error - when main() is defined, it will be automatically invoked
                // Users should not have executable statements alongside a main function definition
                _context.AddError("Cannot have module-level executable statements when a 'main' function is defined. The main function is automatically invoked as the entry point.", code: DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);
            }
            // else: Only VariableDeclaration statements remain, which are legitimate typed declarations
            // These will be handled by generating them as local variables in a synthesized static constructor or similar
        }
        else if (!_context.IsEntryPoint && executableStatements.Count > 0)
        {
            // Non-entry-point files with executable statements: ignore them
            // Module-level executable code should only run in the entry point
            _context.Logger.LogWarning($"{executableStatements.Count} module-level executable statement(s) in non-entry-point file ignored", 0, 0);
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

        // Generate re-export delegating members for from-import statements
        // This enables patterns like: from .helpers import utility_func
        // which makes utility_func accessible from this module's Exports class
        if (reExportImports != null)
        {
            foreach (var fromImport in reExportImports)
            {
                var reExportMembers = GenerateReExportMembers(fromImport);
                moduleDeclarations.AddRange(reExportMembers);
            }
        }

        // Generate module class name from source file name
        var moduleClassName = GetModuleClassName(willHaveMainMethod, functionNames);

        var moduleClass = ClassDeclaration(moduleClassName)
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithMembers(List(moduleDeclarations));

        return (moduleClass, namespaceTypes);
    }

    /// <summary>
    /// Generates only the module class (legacy method for backward compatibility).
    /// For new code, prefer GenerateModuleMembers which also returns namespace-level types.
    /// </summary>
    private ClassDeclarationSyntax GenerateModuleClass(List<Statement> statements, List<FromImportStatement>? reExportImports = null)
    {
        var (moduleClass, _) = GenerateModuleMembers(statements, reExportImports);
        return moduleClass;
    }

    private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
    {
        // All modules use "Exports" as the class name
        // This matches the spec and import system expectation:
        // "using config = ProjectNamespace.Config.Exports;"
        // Exception: entry point modules that generate a Main method use "Program"
        if (willGenerateMainMethod)
        {
            return "Program";
        }

        return "Exports";
    }

    /// <summary>
    /// Generate delegating members for re-exported symbols from a from-import statement.
    /// For example: "from .helpers import utility_func" generates a method that delegates to helpers.Exports.UtilityFunc()
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateReExportMembers(FromImportStatement fromImport)
    {
        var reExportedSymbols = GetReExportedSymbols(fromImport);
        var resolvedModulePath = GetResolvedModulePath(fromImport);

        if (reExportedSymbols == null || resolvedModulePath == null)
            yield break;

        // Convert the resolved module path to a namespace path
        // e.g., "mypackage.helpers" -> "Mypackage.Helpers.Exports"
        var sourceModuleNamespace = ConvertModuleNameToNamespace(resolvedModulePath);
        var sourceClassName = $"{sourceModuleNamespace}.Exports";

        foreach (var (localName, symbol) in reExportedSymbols)
        {
            switch (symbol)
            {
                case FunctionSymbol funcSymbol:
                    yield return GenerateReExportMethod(localName, funcSymbol, sourceClassName);
                    break;

                case VariableSymbol varSymbol:
                    yield return GenerateReExportProperty(localName, varSymbol, sourceClassName);
                    break;

                    // TypeSymbol (classes, structs, enums) are handled differently - they use type aliases
                    // which are already supported via the import system, so we don't need to re-export them here
            }
        }
    }

    /// <summary>
    /// Generate a delegating method for a re-exported function.
    /// </summary>
    private MemberDeclarationSyntax GenerateReExportMethod(string localName, FunctionSymbol funcSymbol, string sourceClassName)
    {
        var methodName = NameMangler.Transform(localName, NameContext.Method);
        var sourceMethodName = NameMangler.Transform(funcSymbol.Name, NameContext.Method);

        // Generate parameter list
        var parameters = funcSymbol.Parameters
            .Select(p =>
            {
                var paramName = NameMangler.Transform(p.Name, NameContext.Parameter);
                var paramType = _typeMapper.MapSemanticType(p.Type);
                return Parameter(Identifier(paramName)).WithType(paramType);
            })
            .ToArray();

        // Generate arguments to pass to the delegate call
        var arguments = funcSymbol.Parameters
            .Select(p => Argument(IdentifierName(NameMangler.Transform(p.Name, NameContext.Parameter))))
            .ToArray();

        // Map return type
        var returnType = _typeMapper.MapSemanticType(funcSymbol.ReturnType);

        // Build the delegate call: SourceModule.Exports.Method(args)
        var delegateCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParseExpression(sourceClassName),
                IdentifierName(sourceMethodName)))
            .WithArgumentList(ArgumentList(SeparatedList(arguments)));

        // If return type is void, generate expression statement; otherwise, generate return statement
        StatementSyntax body;
        if (funcSymbol.ReturnType is VoidType || funcSymbol.ReturnType == SemanticType.Void)
        {
            body = ExpressionStatement(delegateCall);
        }
        else
        {
            body = ReturnStatement(delegateCall);
        }

        return MethodDeclaration(returnType, methodName)
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(Block(body));
    }

    /// <summary>
    /// Generate a delegating property for a re-exported variable/constant.
    /// </summary>
    private MemberDeclarationSyntax GenerateReExportProperty(string localName, VariableSymbol varSymbol, string sourceClassName)
    {
        // For constants/variables with ALL_CAPS names, preserve the case
        var propertyName = IsConstantCaseName(localName)
            ? NameMangler.ToConstantCase(localName)
            : NameMangler.ToPascalCase(localName);

        var sourcePropertyName = IsConstantCaseName(varSymbol.Name)
            ? NameMangler.ToConstantCase(varSymbol.Name)
            : NameMangler.ToPascalCase(varSymbol.Name);

        // Map the type
        var propertyType = _typeMapper.MapSemanticType(GetVariableType(varSymbol));

        // Build the delegate access: SourceModule.Exports.Property
        var delegateAccess = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            ParseExpression(sourceClassName),
            IdentifierName(sourcePropertyName));

        // Generate a read-only property with expression body
        return PropertyDeclaration(propertyType, propertyName)
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword)))
            .WithExpressionBody(ArrowExpressionClause(delegateAccess))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
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

}
