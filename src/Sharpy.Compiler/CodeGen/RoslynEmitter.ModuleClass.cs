using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    private ClassDeclarationSyntax GenerateModuleClass(List<Statement> statements, List<FromImportStatement>? reExportImports = null)
    {
        // Pre-scan for module-level variable declarations to track them across all scopes
        // This ensures functions can reference variables with correct casing
        _moduleConstVariables.Clear();
        _moduleVariables.Clear();
        _moduleFieldNames.Clear();

        // Track variables that need special handling due to execution order issues
        // (e.g., variables assigned before they are declared, or variables with multiple declarations,
        // or variables whose initializers reference other local variables)
        _variablesWithExecutionOrderIssues = new HashSet<string>();
        var variableFirstSeen = new Dictionary<string, int>();
        var variableFirstDeclaration = new Dictionary<string, int>();

        // Collect class, struct, and function names - these are valid to reference from module-level fields
        var typeAndFunctionNames = new HashSet<string>();
        foreach (var stmt in statements)
        {
            if (stmt is ClassDef classDef)
                typeAndFunctionNames.Add(classDef.Name);
            else if (stmt is StructDef structDef)
                typeAndFunctionNames.Add(structDef.Name);
            else if (stmt is FunctionDef funcDef)
                typeAndFunctionNames.Add(funcDef.Name);
            else if (stmt is EnumDef enumDef)
                typeAndFunctionNames.Add(enumDef.Name);
        }

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

        // First pass: collect const variables and detect basic execution order issues
        var constVariables = new HashSet<string>();
        for (int i = 0; i < statements.Count; i++)
        {
            var stmt = statements[i];
            if (stmt is VariableDeclaration varDecl)
            {
                var varName = varDecl.Name;

                // Track const variables
                if (varDecl.IsConst || IsConstantCaseName(varDecl.Name))
                {
                    constVariables.Add(varName);
                    continue;
                }

                if (variableFirstDeclaration.ContainsKey(varName))
                {
                    // Multiple declarations - needs special handling
                    _variablesWithExecutionOrderIssues.Add(varName);
                }
                else
                {
                    variableFirstDeclaration[varName] = i;
                    // Check if there was an assignment before this declaration
                    if (variableFirstSeen.TryGetValue(varName, out var firstSeenIndex) && firstSeenIndex < i)
                    {
                        _variablesWithExecutionOrderIssues.Add(varName);
                    }
                }
            }
            else if (stmt is Assignment assign && assign.Target is Identifier assignId)
            {
                var varName = assignId.Name;
                if (!variableFirstSeen.ContainsKey(varName))
                {
                    variableFirstSeen[varName] = i;
                }
            }
        }

        // Second pass: detect variables whose initializers reference local variables
        // A variable must be local if its initializer references any identifier that:
        // 1. Is in _variablesWithExecutionOrderIssues (already known to be local), OR
        // 2. Is a variable created by an Assignment statement (these go to Main), OR
        // 3. Is a variable (not const, not type/function) that will be local
        // We iterate until no new variables are added (transitive closure)
        var variableDeclarations = new Dictionary<string, VariableDeclaration>();
        var variableDeclarationOrder = new Dictionary<string, int>();

        // Track variables created by Assignment statements (without type annotations)
        // These always go to Main() as executable statements, so any VariableDeclaration
        // that references them must also be local
        var assignmentVariables = new HashSet<string>();
        for (int i = 0; i < statements.Count; i++)
        {
            if (statements[i] is VariableDeclaration vd && !constVariables.Contains(vd.Name))
            {
                variableDeclarations[vd.Name] = vd;
                if (!variableDeclarationOrder.ContainsKey(vd.Name))
                    variableDeclarationOrder[vd.Name] = i;
            }
            else if (statements[i] is Assignment assign && assign.Target is Identifier targetId)
            {
                // This is an assignment without type annotation - it goes to Main
                // and any VariableDeclaration referencing it must also go to Main
                assignmentVariables.Add(targetId.Name);
            }
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var kvp in variableDeclarations)
            {
                var varName = kvp.Key;
                var varDecl = kvp.Value;

                if (_variablesWithExecutionOrderIssues.Contains(varName))
                    continue; // Already marked

                if (varDecl.InitialValue == null)
                    continue; // No initializer to check

                // Collect all identifiers referenced in the initializer
                var referencedIds = new HashSet<string>();
                CollectReferencedIdentifiers(varDecl.InitialValue, referencedIds);

                // Check if any referenced identifier requires this to be local
                foreach (var refId in referencedIds)
                {
                    // Skip if it's a type/function name (these are always available)
                    if (typeAndFunctionNames.Contains(refId))
                        continue;

                    // Skip if it's a const variable (always available at module level)
                    if (constVariables.Contains(refId))
                        continue;

                    // Skip if it's a builtin or known symbol
                    var symbol = _context.LookupSymbol(refId);
                    if (symbol is FunctionSymbol or TypeSymbol)
                        continue;

                    // If referenced variable is already marked as local, this one must be too
                    if (_variablesWithExecutionOrderIssues.Contains(refId))
                    {
                        _variablesWithExecutionOrderIssues.Add(varName);
                        changed = true;
                        break;
                    }

                    // If referenced variable is created by an Assignment (no type annotation),
                    // it will be a local variable in Main, so this one must be too
                    if (assignmentVariables.Contains(refId))
                    {
                        _variablesWithExecutionOrderIssues.Add(varName);
                        changed = true;
                        break;
                    }

                    // If referenced variable is a module variable declared AFTER this one,
                    // the order of static field initialization is undefined, so make this local
                    if (variableDeclarationOrder.TryGetValue(refId, out var refOrder) &&
                        variableDeclarationOrder.TryGetValue(varName, out var thisOrder) &&
                        refOrder < thisOrder)
                    {
                        // Referenced variable is declared before this one, but if it has
                        // execution order issues, it will be local, so check that
                        // (This case is already handled above by the _variablesWithExecutionOrderIssues check)
                    }
                    else if (variableDeclarations.ContainsKey(refId) && !constVariables.Contains(refId))
                    {
                        // Referenced variable is NOT const and is a module-level variable
                        // If it doesn't have explicit execution order issues yet, we need to check
                        // if it references this variable (mutual dependency) or will be local
                        // For safety, if a variable references another non-const variable that
                        // is declared later (or at all, to be safe), make it local
                        // This ensures proper execution order semantics
                        _variablesWithExecutionOrderIssues.Add(varName);
                        changed = true;
                        break;
                    }
                }
            }
        }

        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclaration varDecl)
            {
                // Treat as constant if explicitly const OR if name is ALL_CAPS (Python-style constant)
                if (varDecl.IsConst || IsConstantCaseName(varDecl.Name))
                {
                    _moduleConstVariables.Add(varDecl.Name);
                }
                else if (!_variablesWithExecutionOrderIssues.Contains(varDecl.Name))
                {
                    // Only track as module variable if no execution order issues
                    _moduleVariables.Add(varDecl.Name);
                }
                // Variables with execution order issues are NOT added to _moduleVariables
                // They will be handled as local variables in Main()
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
                declarations.Add(memberDecl);
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
            // This is an error - when main() is defined, it will be automatically invoked
            // Users should not have executable statements alongside a main function definition
            _context.AddError("Cannot have module-level executable statements when a 'main' function is defined. The main function is automatically invoked as the entry point.");
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
                declarations.AddRange(reExportMembers);
            }
        }

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
        if (fromImport.ReExportedSymbols == null || fromImport.ResolvedModulePath == null)
            yield break;

        // Convert the resolved module path to a namespace path
        // e.g., "mypackage.helpers" -> "Mypackage.Helpers.Exports"
        var sourceModuleNamespace = ConvertModuleNameToNamespace(fromImport.ResolvedModulePath);
        var sourceClassName = $"{sourceModuleNamespace}.Exports";

        foreach (var (localName, symbol) in fromImport.ReExportedSymbols)
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
        var propertyType = _typeMapper.MapSemanticType(varSymbol.Type);

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
