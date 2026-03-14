using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Module class generation
/// </summary>
internal partial class RoslynEmitter
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
    /// Generates the module class with all members nested inside it.
    /// Types (classes, structs, interfaces, enums) are nested inside the module class,
    /// enabling single 'using static' imports for C# consumers.
    /// </summary>
    private ClassDeclarationSyntax GenerateModuleMembers(
        List<Statement> statements, List<FromImportStatement>? reExportImports = null)
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

        // Pre-scan for union declarations and register them in the codegen symbol table.
        // This creates a minimal TypeSymbol shell (TypeKind.Union, IsAbstract) for type-kind
        // discrimination only — UnionCases is intentionally not populated here because case
        // name validation is already performed by the semantic phase (TypeChecker.Expressions.Access).
        // The codegen phase only needs to know that a name refers to a union type so it can emit
        // the correct ObjectCreationExpression for union case construction.
        foreach (var stmt in statements)
        {
            if (stmt is UnionDef unionDef)
            {
                var unionSymbol = new TypeSymbol
                {
                    Name = unionDef.Name,
                    ClrType = null,
                    TypeKind = Semantic.TypeKind.Union,
                    IsAbstract = true
                };
                // Only add if not already present
                if (_context.LookupSymbol(unionDef.Name) == null)
                {
                    _context.SymbolTable.Define(unionSymbol);
                }
            }
        }

        // All declarations go into the module class (types are nested, not namespace siblings)
        var moduleDeclarations = new List<MemberDeclarationSyntax>();
        var executableStatements = new List<Statement>();

        // First pass: check if there's a user-defined main function
        // This affects how we handle module-level variable declarations with execution order issues
        bool hasMainFunction = statements.Any(s => s is FunctionDef f && f.Name == "main");

        // Module-level variables with execution order issues should be generated as static fields when:
        // - There's a user-defined main() (entry points always have one now)
        // - The module is not an entry point (non-entry-point modules always use static fields)
        _forceModuleLevelFields = hasMainFunction || !_context.IsEntryPoint;

        foreach (var stmt in statements)
        {
            var member = GenerateStatement(stmt);

            // After generating class/struct/function declarations, clear local scope tracking
            // so that parameter names from their methods don't leak into module-level code
            if (stmt is ClassDef or StructDef or FunctionDef or InterfaceDef or EnumDef or UnionDef or DelegateDef or EventDef)
            {
                _declaredVariables.Clear();
                _variableVersions.Clear();
                _constVariables.Clear();
                _narrowing.Reset();
            }

            if (member is MemberDeclarationSyntax memberDecl)
            {
                // Everything goes into the module class (types are nested)
                moduleDeclarations.Add(memberDecl);
            }
            else if (member == null && stmt is VariableDeclaration varRedefinition)
            {
                // This is a variable redefinition (GenerateModuleLevelField returned null)
                // For const variables, we skip the duplicate entirely (consts can't be redeclared at runtime)
                // For regular variables, add to executable statements so it becomes a local in Main
                if (!varRedefinition.IsConst && !NameFormDetector.IsConstantCaseName(varRedefinition.Name))
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

        // main() is required for entry points — no synthesized Main() needed.
        // If there's a main function with bare executable statements, report an error.
        if (hasMainFunction && executableStatements.Count > 0)
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

        // Entry points must have a user-defined main() function (enforced by ModuleLevelValidator).
        // The user's main() → Main() (via NameMangler) is the C# entry point.
        bool willHaveMainMethod = hasMainFunction;

        // Collect all function names to check for class name collisions
        var functionNames = statements
            .OfType<FunctionDef>()
            .Select(f => NameMangler.Transform(f.Name, NameContext.Method))
            .ToHashSet();

        // Generate re-export delegating members for from-import statements
        // This enables patterns like: from .helpers import utility_func
        // which makes utility_func accessible from this module's class
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

        // Name collision detection: check if any user-defined type's PascalCase name
        // matches the module class name (e.g., animal.spy defining class Animal)
        ClassDeclarationSyntax? collidingTypeDecl = null;
        {
            // Find colliding type declarations and their generated syntax
            foreach (var stmt in statements)
            {
                string? typeName = stmt switch
                {
                    ClassDef cd => SimpleToPascalCase(cd.Name),
                    StructDef sd => SimpleToPascalCase(sd.Name),
                    InterfaceDef id => SimpleToPascalCase(id.Name),
                    EnumDef ed => SimpleToPascalCase(ed.Name),
                    DelegateDef dd => SimpleToPascalCase(dd.Name),
                    _ => null
                };

                if (typeName != null && typeName == moduleClassName && stmt is ClassDef)
                {
                    // Find the generated ClassDeclarationSyntax for this type
                    collidingTypeDecl = moduleDeclarations
                        .OfType<ClassDeclarationSyntax>()
                        .FirstOrDefault(c => c.Identifier.Text == moduleClassName);
                    break;
                }
                else if (typeName != null && typeName == moduleClassName)
                {
                    // Collision with struct/interface/enum — error (can't merge)
                    var srcName = stmt switch { StructDef sd => sd.Name, InterfaceDef id => id.Name, EnumDef ed => ed.Name, _ => "?" };
                    _context.AddError(
                        $"Type '{srcName}' conflicts with module class name '{moduleClassName}'. " +
                        $"Rename the type or the source file to avoid this collision.",
                        code: DiagnosticCodes.CodeGen.NameCollision);
                }
            }
        }

        // Handle collision: when a class name matches the module name, the class
        // absorbs module-level static members (functions, constants) and becomes
        // the module representative. This enables the common Python pattern of
        // animal.spy containing class Animal.
        if (collidingTypeDecl != null)
        {
            // Separate the colliding type from other declarations
            var otherDeclarations = moduleDeclarations
                .Where(m => m != collidingTypeDecl)
                .ToList();

            // Inject non-type module declarations as static members into the colliding type
            var augmentedType = collidingTypeDecl.WithMembers(
                collidingTypeDecl.Members.AddRange(otherDeclarations));

            return augmentedType;
        }

        // Normal case: build a static module class containing all declarations
        var moduleClassDecl = ClassDeclaration(moduleClassName);

        // Add [SharpyModule] attribute to non-entry-point module classes
        if (!_context.IsEntryPoint)
        {
            var sharpyModuleName = GetSharpyModuleName();
            moduleClassDecl = moduleClassDecl
                .WithAttributeLists(SingletonList(
                    AttributeList(SingletonSeparatedList(
                        Attribute(MakeGlobalQualifiedName("Sharpy", "SharpyModule"),
                            AttributeArgumentList(SingletonSeparatedList(
                                AttributeArgument(LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(sharpyModuleName))))))))));
        }

        // Module class is always partial (allows merging with wrapper declarations
        // from other files in the same package directory)
        var moduleClass = moduleClassDecl
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.PartialKeyword)))
            .WithMembers(List(moduleDeclarations));

        return moduleClass;
    }

    private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
    {
        // Module class name is derived from the source file name
        if (!string.IsNullOrEmpty(_context.SourceFilePath))
        {
            var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);
            if (fileName == DunderNames.Init)
            {
                // __init__.spy → use directory name as class name
                var dirName = Path.GetFileName(Path.GetDirectoryName(_context.SourceFilePath));
                return SimpleToPascalCase(dirName ?? "Module");
            }

            // Entry point: main.spy → "Program" (avoids CS0542: Main.Main() conflict),
            // other files → PascalCase of filename
            if (willGenerateMainMethod && fileName.Equals("main", StringComparison.OrdinalIgnoreCase))
            {
                return "Program";
            }

            return SimpleToPascalCase(fileName);
        }

        return "Module"; // Fallback
    }

    /// <summary>
    /// Computes the Sharpy module name for the [SharpyModule] attribute.
    /// Returns the Python-style dotted module path (e.g., "mypackage.helpers").
    /// </summary>
    private string GetSharpyModuleName()
    {
        if (string.IsNullOrEmpty(_context.SourceFilePath))
            return "module";

        var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);

        if (!string.IsNullOrEmpty(_context.ProjectRootPath))
        {
            var relativePath = Path.GetRelativePath(_context.ProjectRootPath, _context.SourceFilePath);
            var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
            {
                parts.AddRange(relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries));
            }

            // For __init__.spy, the module name is the directory path (without __init__)
            // For regular files, append the file name
            if (fileName != DunderNames.Init)
            {
                parts.Add(fileName);
            }

            if (parts.Count > 0)
                return string.Join(".", parts);
        }

        // Single-file: just use the file name
        if (fileName == DunderNames.Init)
            return "module";

        return fileName;
    }

    /// <summary>
    /// Generate delegating members for re-exported symbols from a from-import statement.
    /// For example: "from .helpers import utility_func" generates a method that delegates to helpers.UtilityFunc()
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateReExportMembers(FromImportStatement fromImport)
    {
        var reExportedSymbols = GetReExportedSymbols(fromImport);
        var resolvedModulePath = GetResolvedModulePath(fromImport);

        if (reExportedSymbols == null || resolvedModulePath == null)
            yield break;

        // Convert the resolved module path to a nested class path
        // e.g., "mypackage.helpers" -> "ProjectNamespace.Mypackage.Helpers"
        var sourceModuleNamespace = ConvertModuleNameToNamespace(resolvedModulePath);
        var sourceClassName = !string.IsNullOrEmpty(_context.ProjectNamespace)
            ? $"{_context.ProjectNamespace}.{sourceModuleNamespace}"
            : sourceModuleNamespace;

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

                case TypeSymbol:
                    // Type re-exports cannot be delegated (no wrapper possible).
                    // The consumer's import will resolve the type via its defining module's
                    // using static directive. Skip silently.
                    break;
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

        // Build the delegate call: SourceClass.Method(args)
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
        var propertyName = NameFormDetector.IsConstantCaseName(localName)
            ? NameMangler.ToConstantCase(localName)
            : NameMangler.ToPascalCase(localName);

        var sourcePropertyName = NameFormDetector.IsConstantCaseName(varSymbol.Name)
            ? NameMangler.ToConstantCase(varSymbol.Name)
            : NameMangler.ToPascalCase(varSymbol.Name);

        // Map the type
        var propertyType = _typeMapper.MapSemanticType(GetVariableType(varSymbol));

        // Build the delegate access: SourceClass.Property
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
        _cancellationToken.ThrowIfCancellationRequested();

        return stmt switch
        {
            FunctionDef funcDef => GenerateFunctionDeclaration(funcDef),
            ClassDef classDef => GenerateClassDeclaration(classDef),
            StructDef structDef => GenerateStructDeclaration(structDef),
            InterfaceDef interfaceDef => GenerateInterfaceDeclaration(interfaceDef),
            EnumDef enumDef => GenerateEnumDeclaration(enumDef),
            UnionDef unionDef => GenerateUnionDeclaration(unionDef),
            DelegateDef delegateDef => GenerateDelegateDeclaration(delegateDef),
            EventDef => null,  // Events are class members only; invalid at module level (caught by semantic analysis)
            VariableDeclaration varDecl => GenerateModuleLevelField(varDecl),
            TypeAlias => null,  // Type aliases are compile-time only, no C# output
            ReturnStatement ret => GenerateReturn(ret),
            Assignment assign => GenerateAssignment(assign),
            ImportStatement => null,       // Imports are resolved at semantic level, no C# output
            FromImportStatement => null,   // Imports are resolved at semantic level, no C# output
            _ => EmitUnrecognizedStatementDiagnostic(stmt)
        };
    }

}
