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
        var convertedParts = parts.Select(part => NameMangler.ToNamespacePart(part));
        return string.Join(".", convertedParts);
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

        // Clear extracted-type tracking. In library mode, top-level type declarations are
        // pulled out of the module class and emitted as namespace siblings (see below).
        _extractedTypes.Clear();

        // Maps a generated top-level type declaration back to its original Sharpy name so the
        // emitted [SharpyModuleType("module", "PythonName")] attribute can carry the source name.
        var extractableTypeNames = new Dictionary<MemberDeclarationSyntax, string>(ReferenceEqualityComparer.Instance);

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

        // First pre-scan: register @test.fixture functions so that test methods declared later
        // in the same module — or earlier — can resolve their fixture parameters consistently
        // regardless of file order.
        foreach (var stmt in statements)
        {
            if (stmt is FunctionDef fixtureFunc && IsTestFixtureFunction(fixtureFunc))
            {
                RegisterFixture(fixtureFunc);
            }
        }

        foreach (var stmt in statements)
        {
            // Module-level @test.fixture functions are emitted as standalone sibling classes,
            // not as static methods on the module class. They were pre-registered above; here
            // we just queue them for emission.
            if (stmt is FunctionDef fixtureFunc && IsTestFixtureFunction(fixtureFunc))
            {
                _pendingFixtures.Add(fixtureFunc);
                _declaredVariables.Clear();
                _variableVersions.Clear();
                _constVariables.Clear();
                _narrowing.Reset();
                continue;
            }

            // Module-level @test functions are collected for a separate sibling test class
            // (so they don't end up as static methods on the module class — xUnit needs them
            // as instance methods on a public class).
            if (stmt is FunctionDef testFunc
                && testFunc.Decorators.Any(IsTestDecorator))
            {
                _pendingTestFunctions.Add(testFunc);
                _declaredVariables.Clear();
                _variableVersions.Clear();
                _constVariables.Clear();
                _narrowing.Reset();
                continue;
            }

            // Module-level functions decorated with @lru_cache/@cache expand into a cache
            // field plus a private/public method pair, so they need to bypass the single-
            // member GenerateStatement dispatcher and emit several members at once.
            if (stmt is FunctionDef cachedFunc && IsLruCacheDecorated(cachedFunc))
            {
                moduleDeclarations.AddRange(GenerateLruCacheWrappedFunction(cachedFunc, isModuleLevel: true));
                _declaredVariables.Clear();
                _variableVersions.Clear();
                _constVariables.Clear();
                _narrowing.Reset();
                continue;
            }

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
                // Everything goes into the module class for now (collision detection below
                // relies on type declarations being present). In library mode, top-level types
                // are partitioned out into _extractedTypes after collision handling.
                moduleDeclarations.Add(memberDecl);

                // Record the original Sharpy name for top-level type declarations so they can
                // be extracted as namespace siblings (library mode only).
                var sourceTypeName = stmt switch
                {
                    ClassDef cd => cd.Name,
                    StructDef sd => sd.Name,
                    InterfaceDef id => id.Name,
                    EnumDef ed => ed.Name,
                    UnionDef ud => ud.Name,
                    _ => null
                };
                if (sourceTypeName != null)
                {
                    extractableTypeNames[memberDecl] = sourceTypeName;
                }
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
                    ClassDef cd => NameMangler.ToNamespacePart(cd.Name),
                    StructDef sd => NameMangler.ToNamespacePart(sd.Name),
                    InterfaceDef id => NameMangler.ToNamespacePart(id.Name),
                    EnumDef ed => NameMangler.ToNamespacePart(ed.Name),
                    DelegateDef dd => NameMangler.ToNamespacePart(dd.Name),
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

        // Single-file library mode: extract top-level type declarations
        // (class/struct/interface/enum/union) out of the module class and emit them as namespace
        // siblings annotated with [SharpyModuleType]. Module-level functions, fields, and
        // re-exports stay on the module class. Entry-point files keep their types nested.
        //
        // Extraction is intentionally limited to single-file library compilation (no
        // ProjectNamespace). Multi-file projects keep types nested inside their module class so
        // that same-named types in sibling modules stay isolated (avoiding CS0101 duplicate-type
        // errors at namespace level) and cross-module references continue to resolve via the
        // Namespace.ModuleClass.Type path. The collision case above returns early, so a type whose
        // name matches the module class is never extracted.
        if (!_context.IsEntryPoint
            && string.IsNullOrEmpty(_context.ProjectNamespace)
            && extractableTypeNames.Count > 0)
        {
            var sharpyModuleName = GetSharpyModuleName();
            var retainedDeclarations = new List<MemberDeclarationSyntax>(moduleDeclarations.Count);
            foreach (var decl in moduleDeclarations)
            {
                if (extractableTypeNames.TryGetValue(decl, out var pythonName))
                {
                    _extractedTypes.Add(DecorateExtractedType(decl, sharpyModuleName, pythonName));
                }
                else
                {
                    retainedDeclarations.Add(decl);
                }
            }
            moduleDeclarations = retainedDeclarations;
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

    /// <summary>
    /// Annotates an extracted top-level type declaration with
    /// <c>[global::Sharpy.SharpyModuleType("moduleName", "pythonName")]</c> so the compiler can
    /// rediscover it as belonging to the module when the assembly is imported. The attribute is
    /// prepended ahead of any existing attribute lists (e.g., dataclass-derived attributes).
    /// </summary>
    private static MemberDeclarationSyntax DecorateExtractedType(
        MemberDeclarationSyntax typeDecl, string moduleName, string pythonName)
    {
        var attribute = Attribute(MakeGlobalQualifiedName("Sharpy", "SharpyModuleType"))
            .WithArgumentList(AttributeArgumentList(SeparatedList(new[]
            {
                AttributeArgument(LiteralExpression(
                    SyntaxKind.StringLiteralExpression, Literal(moduleName))),
                AttributeArgument(LiteralExpression(
                    SyntaxKind.StringLiteralExpression, Literal(pythonName)))
            })));

        var attributeList = AttributeList(SingletonSeparatedList(attribute));

        // Prepend so [SharpyModuleType] appears first, ahead of any existing attribute lists.
        var existing = typeDecl.AttributeLists;
        return typeDecl.WithAttributeLists(existing.Insert(0, attributeList));
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
                return NameMangler.ToNamespacePart(dirName ?? "Module");
            }

            // Entry point: main.spy → "Program" (avoids CS0542: Main.Main() conflict),
            // other files → PascalCase of filename
            if (willGenerateMainMethod && fileName.Equals("main", StringComparison.OrdinalIgnoreCase))
            {
                return "Program";
            }

            return NameMangler.ToNamespacePart(fileName);
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
            : NameCasing.ResolveField(localName, false);

        var sourcePropertyName = NameFormDetector.IsConstantCaseName(varSymbol.Name)
            ? NameMangler.ToConstantCase(varSymbol.Name)
            : NameCasing.ResolveField(varSymbol.Name, false);

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

    /// <summary>
    /// Generates a sibling test class containing all module-level @test functions.
    /// xUnit requires test methods to be public instance methods of a public class.
    ///
    /// Test methods whose parameters match a registered @test.fixture function name are
    /// rewired: the parameter is stripped from the method signature, the test class
    /// implements Xunit.IClassFixture&lt;XFixture&gt;, and a constructor receives the fixture
    /// instance via DI. Each consumed fixture becomes a private field; the test body is
    /// prefixed with `var name = _nameFixture.Value;`.
    /// </summary>
    private ClassDeclarationSyntax GenerateModuleTestClass(IReadOnlyList<FunctionDef> testFunctions)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Collect every unique fixture consumed by any test method in this class so we can
        // emit IClassFixture<T> on the class itself and a constructor that captures them.
        var consumedFixtures = new Dictionary<string, FixtureInfo>();
        foreach (var func in testFunctions)
        {
            foreach (var (_, fixture) in GetConsumedFixtures(func))
            {
                consumedFixtures[fixture.SharpyName] = fixture;
            }
        }

        var savedIsInTestFunction = _isInTestFunction;

        foreach (var func in testFunctions)
        {
            _isInTestFunction = true;

            ResetMethodScope(func);
            CollectSourceVariableNames(func.Body);

            using var _gen = SetGeneratorScope(_context.SemanticInfo?.IsGenerator(func) == true);
            using var _async = SetAsyncScope(func.IsAsync);

            var mangledName = NameMangler.Transform(func.Name, NameContext.Method);

            TypeSyntax returnType = func.ReturnType != null
                ? _typeMapper.MapType(func.ReturnType)
                : PredefinedType(Token(SyntaxKind.VoidKeyword));

            // Wrap return types for async / generator test methods, matching the
            // logic used for regular function declarations.
            bool isAsync = func.IsAsync;
            if (_isCurrentMethodGenerator)
            {
                returnType = isAsync ? WrapInIAsyncEnumerable(returnType) : WrapInIEnumerable(returnType);
            }
            else if (isAsync)
            {
                returnType = func.ReturnType != null ? WrapInTask(returnType) : TaskType();
            }

            // Determine which parameters are fixture-injected and exclude them from the
            // emitted parameter list (xUnit's [Fact] takes no params; for [Theory], only
            // the parametrize columns remain).
            var consumedForFunc = GetConsumedFixtures(func);
            var fixtureParamNames = new HashSet<string>(
                consumedForFunc.Select(c => c.Parameter.Name),
                System.StringComparer.Ordinal);

            var paramsExcludingFixtures = func.Parameters
                .Where(p => !fixtureParamNames.Contains(p.Name))
                .ToArray();
            var orderedParams = ReorderParametersForCSharp(paramsExcludingFixtures);
            var parameters = orderedParams.Select(GenerateParameter).ToArray();

            foreach (var param in paramsExcludingFixtures)
            {
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                if (param.IsLateBound)
                {
                    _declaredVariables.Add(paramName + LateBoundSuffix);
                    _declaredVariables.Add(paramName);
                }
                else
                {
                    _declaredVariables.Add(paramName);
                }
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            // Track fixture-injected names as declared variables to avoid versioning collisions.
            foreach (var (parameter, _) in consumedForFunc)
            {
                var localName = NameMangler.ToCamelCase(parameter.Name);
                _declaredVariables.Add(localName);
                _variableVersions[localName] = 0;
            }

            var preamble = GenerateLateBoundPreamble(paramsExcludingFixtures);
            var fixturePrelude = GenerateFixturePrelude(consumedForFunc);
            var body = Block(preamble
                .Concat(fixturePrelude)
                .Concat(func.Body.SelectMany(GenerateBodyStatements)));

            // Build modifiers: always public, never static (xUnit requires instance methods).
            var modifierTokens = new List<SyntaxToken> { Token(SyntaxKind.PublicKeyword) };
            if (isAsync)
            {
                modifierTokens.Add(Token(SyntaxKind.AsyncKeyword));
            }

            var method = MethodDeclaration(returnType, mangledName)
                .WithModifiers(TokenList(modifierTokens))
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithBody(body);

            // Add type parameters if generic
            if (func.TypeParameters.Length > 0)
            {
                var typeParams = func.TypeParameters
                    .Select(GenerateTypeParameterSyntax)
                    .ToArray();
                method = method
                    .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                    .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
            }

            // Add [Fact] attribute (and any other decorator-derived attributes).
            var attributes = GenerateAttributeListsFromDecorators(func.Decorators);
            if (attributes.Count > 0)
            {
                method = method.WithAttributeLists(attributes);
            }

            if (!string.IsNullOrEmpty(func.DocString))
            {
                method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
            }

            members.Add(method);

            _declaredVariables.Clear();
            _variableVersions.Clear();
            _constVariables.Clear();
            _narrowing.Reset();
        }

        _isInTestFunction = savedIsInTestFunction;

        // Test class name: <ModuleClass>Tests. Always public; not static (xUnit
        // instantiates the class per test method).
        var moduleClassName = GetModuleClassName(willGenerateMainMethod: false, functionNames: new HashSet<string>());
        var testClassName = moduleClassName + "Tests";

        // If any tests consume fixtures, prepend fixture fields and a constructor that
        // captures the injected instances. Build the base list with one Xunit.IClassFixture<T>
        // per unique consumed fixture.
        var baseTypes = new List<BaseTypeSyntax>();
        if (consumedFixtures.Count > 0)
        {
            var ctorParams = new List<ParameterSyntax>();
            var ctorStmts = new List<StatementSyntax>();
            var fieldMembers = new List<MemberDeclarationSyntax>();

            // Sort by fixture name for deterministic output.
            foreach (var fixture in consumedFixtures.Values
                .OrderBy(f => f.SharpyName, System.StringComparer.Ordinal))
            {
                baseTypes.Add(SimpleBaseType(
                    GenericName(Identifier("Xunit.IClassFixture"))
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                            IdentifierName(fixture.ClassName))))));

                // private readonly XFixture _xFixture;
                var fieldDecl = FieldDeclaration(
                        VariableDeclaration(IdentifierName(fixture.ClassName))
                            .WithVariables(SingletonSeparatedList(VariableDeclarator(fixture.FieldName))))
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword)));
                fieldMembers.Add(fieldDecl);

                // ctor parameter: XFixture xFixture
                var paramName = fixture.FieldName.TrimStart('_');
                ctorParams.Add(Parameter(Identifier(paramName))
                    .WithType(IdentifierName(fixture.ClassName)));

                // _xFixture = xFixture;
                ctorStmts.Add(ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(fixture.FieldName),
                    IdentifierName(paramName))));
            }

            // Build ctor and prepend fields/ctor to members.
            var ctor = ConstructorDeclaration(testClassName)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList(ctorParams)))
                .WithBody(Block(ctorStmts));

            // Prepend in deterministic order: fields then ctor then existing test methods.
            var newMembers = new List<MemberDeclarationSyntax>();
            newMembers.AddRange(fieldMembers);
            newMembers.Add(ctor);
            newMembers.AddRange(members);
            members = newMembers;
        }

        var testClass = ClassDeclaration(testClassName)
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.PartialKeyword)))
            .WithMembers(List(members));

        if (baseTypes.Count > 0)
        {
            testClass = testClass.WithBaseList(BaseList(SeparatedList(baseTypes)));
        }

        return testClass;
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
