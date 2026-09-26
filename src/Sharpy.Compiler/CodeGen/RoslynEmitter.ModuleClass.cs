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
    /// <summary>
    /// Generates the module's members class <c>&lt;X&gt;</c> (its functions, variables and
    /// constants) and collects its top-level types into <c>_siblingTypes</c>: every module is a
    /// namespace, and its types are declared beside <c>&lt;X&gt;</c> in it (#2039).
    /// </summary>
    private ClassDeclarationSyntax GenerateModuleMembers(
        List<Statement> statements, List<FromImportStatement>? reExportImports = null)
    {
        // Clear tracking field for module field names (still needed to prevent duplicate field declarations)
        _moduleFieldNames.Clear();

        _siblingTypes.Clear();

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

        // Whether the module declares the entry-point main() — ModuleShape's answer, computed once by
        // the one predicate every reader shares (#2013). This affects how we handle module-level
        // variable declarations with execution order issues.
        System.Diagnostics.Debug.Assert(
            _moduleShape != null, "ComputeModuleShape must run before GenerateModuleMembers");
        bool hasMainFunction = _moduleShape!.DeclaresEntryMain;

        // Module-level variables with execution order issues should be generated as static fields when:
        // - There's a user-defined main() (entry points always have one now)
        // - The module is not an entry point (non-entry-point modules always use static fields)
        _forceModuleLevelFields = hasMainFunction || !_context.IsEntryPoint;

        // The module class name and the rest of the module shape were resolved up front by
        // ComputeModuleShape (called first in GenerateCompilationUnit) so [MemberData] attributes
        // generated for @test.parametrize(VARIABLE) decorators (emitted both during the statement
        // loop for class-based tests and after this method for module-level tests) can reference the
        // module class via MemberType = typeof(...). The shape is computed before any body emission.
        _memberDataVariables.Clear();

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

        // Pre-group module-level properties by name so split get/set declarations
        // merge into a single C# property (same rules as class-level split accessors).
        var modulePropertyGroups = new Dictionary<string, List<PropertyDef>>();
        foreach (var stmt in statements)
        {
            if (stmt is PropertyDef propDef)
            {
                if (!modulePropertyGroups.TryGetValue(propDef.Name, out var group))
                {
                    group = new List<PropertyDef>();
                    modulePropertyGroups[propDef.Name] = group;
                }
                group.Add(propDef);
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
                continue;
            }

            // Module-level @test functions are collected for a separate sibling test class ONLY
            // when compiling for a test host (#1495, #1532): xUnit discovers test methods as
            // instance methods on a public class, so under a test host the class IS the point.
            // Outside one there is no runner and no discovery, so a @test function has no reason
            // not to be an ordinary module-level function — which is also what a Python reader
            // expects of a test file. Diverting it into the sibling test class unconditionally
            // left a module-level caller referencing a name that is not in scope (`passing_assert()`
            // → CS0103 behind SPY0908), making a @test program uncompilable under `sharpyc run`.
            // The test-host path is unchanged, so `unittest/*.expected.cs` stays byte-identical.
            if (_context.TargetsTestHost
                && stmt is FunctionDef testFunc
                && testFunc.Decorators.Any(IsTestDecorator))
            {
                _pendingTestFunctions.Add(testFunc);
                continue;
            }

            // Module-level functions decorated with @lru_cache/@cache expand into a cache
            // field plus a private/public method pair, so they need to bypass the single-
            // member GenerateStatement dispatcher and emit several members at once.
            if (stmt is FunctionDef cachedFunc && IsLruCacheDecorated(cachedFunc))
            {
                moduleDeclarations.AddRange(GenerateLruCacheWrappedFunction(cachedFunc, isModuleLevel: true));
                continue;
            }

            // Module-level properties are emitted as static members on the module class.
            // The whole name-group is emitted at its first occurrence (split get/set
            // declarations merge into one C# property; mixed auto+custom groups emit a
            // backing field plus a property), so later occurrences are skipped. Like
            // @lru_cache functions above, groups can expand into multiple members, so
            // they bypass the single-member GenerateStatement dispatcher.
            if (stmt is PropertyDef moduleProp)
            {
                var propGroup = modulePropertyGroups[moduleProp.Name];
                if (ReferenceEquals(propGroup[0], moduleProp))
                {
                    moduleDeclarations.AddRange(GenerateModuleLevelProperty(propGroup));
                }
                continue;
            }

            var member = GenerateStatement(stmt);

            // After generating class/struct/function declarations, clear local scope tracking
            // so that parameter names from their methods don't leak into module-level code
            if (stmt is ClassDef or StructDef or FunctionDef or InterfaceDef or EnumDef or UnionDef or DelegateDef or EventDef)
            {
            }

            if (member is MemberDeclarationSyntax memberDecl)
            {
                // Declarations are collected in source order; the types among them are partitioned
                // out to _siblingTypes once every member is generated.
                moduleDeclarations.Add(memberDecl);

                // Record the original Sharpy name of every top-level type declaration: it is a
                // namespace sibling of the members class, stamped with its python name.
                var sourceTypeName = stmt switch
                {
                    ClassDef cd => cd.Name,
                    StructDef sd => sd.Name,
                    InterfaceDef id => id.Name,
                    EnumDef ed => ed.Name,
                    UnionDef ud => ud.Name,
                    DelegateDef dd => dd.Name,
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

        // Emit MemberData wrapper properties for module-level variables referenced by
        // @test.parametrize(VARIABLE). xUnit's [MemberData] requires a public static member
        // returning IEnumerable<object[]>, so each referenced list variable gets a companion
        // property projecting its rows into object arrays.
        moduleDeclarations.AddRange(GenerateParametrizeMemberDataProperties(statements));

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
                _context.ReportAt(trulyExecutableStatements[0],
                    "Cannot have module-level executable statements when a 'main' function is defined. The main function is automatically invoked as the entry point.",
                    DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);
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

        // Every module is a namespace (#2039, Decision 28 (a)/(c)/(f)): its functions, variables and
        // constants are members of <X>, and every top-level type is declared BESIDE <X> in the module
        // namespace — never nested in it, never merged into it, in every mode. A non-entry module
        // stamps each type [SharpyModuleType(module, pythonName)] so discovery finds it by attribute
        // (rung 3); the entry module's types are stamped `__main__` and its members class carries no
        // [SharpyModule].
        var shape = _moduleShape!;
        var sharpyModuleName = GetSharpyModuleName();
        var memberDeclarations = new List<MemberDeclarationSyntax>(moduleDeclarations.Count);
        foreach (var decl in moduleDeclarations)
        {
            if (extractableTypeNames.TryGetValue(decl, out var pythonName))
            {
                // The entry module's types are stamped too, with python's name for it, `__main__`:
                // once a type is no longer nested in its module class, the stamp is the only thing
                // that marks it Sharpy-declared for the python-name channel (<__main__.D object>,
                // #2006 R-CF). The entry members class itself stays unstamped (no [SharpyModule]).
                _siblingTypes.Add(DecorateSiblingType(
                    decl, _context.IsEntryPoint ? "__main__" : sharpyModuleName, pythonName));
            }
            else
            {
                memberDeclarations.Add(decl);
            }
        }

        var membersClassDecl = ClassDeclaration(shape.MembersClassName);

        // [SharpyModule] on the members class of every non-entry module — including one that holds
        // only types (uniform layout; discovery also finds such a module through its types'
        // [SharpyModuleType] stamps).
        if (!_context.IsEntryPoint)
        {
            membersClassDecl = PrependAttributeList(membersClassDecl, SharpyModuleAttributeList(sharpyModuleName));
        }

        // The members class is partial (a hand-written partial of a spy-sourced stdlib module
        // completes it).
        return membersClassDecl
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.PartialKeyword)))
            .WithMembers(List(memberDeclarations));
    }

    /// <summary><c>[global::Sharpy.SharpyModule("&lt;dotted module name&gt;")]</c> for a module class.</summary>
    private static AttributeListSyntax SharpyModuleAttributeList(string sharpyModuleName)
        => AttributeList(SingletonSeparatedList(
            Attribute(MakeGlobalQualifiedName("Sharpy", "SharpyModule"),
                AttributeArgumentList(SingletonSeparatedList(
                    AttributeArgument(LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal(sharpyModuleName))))))));

    /// <summary>
    /// Annotates a top-level type declaration (a sibling of the members class) with
    /// <c>[global::Sharpy.SharpyModuleType("moduleName", "pythonName")]</c> so the compiler can
    /// rediscover it as belonging to the module when the assembly is imported (#2039). The attribute is
    /// prepended ahead of any existing attribute lists (e.g., dataclass-derived attributes).
    /// </summary>
    private static MemberDeclarationSyntax DecorateSiblingType(
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

        // Prepend so [SharpyModuleType] appears first, ahead of any existing attribute lists.
        return PrependAttributeList(typeDecl, AttributeList(SingletonSeparatedList(attribute)));
    }

    /// <summary>
    /// Prepends <paramref name="attributeList"/> to a declaration, moving the declaration's leading
    /// trivia — its <c>///</c> doc comment — onto the new first list. The one way the emitter adds an
    /// attribute list to a declaration that may already carry that trivia: prepended in front of it,
    /// the doc comment would follow the attribute and document nothing (CS1587, an error in a
    /// TreatWarningsAsErrors documentation build such as Sharpy.Stdlib; #2039).
    /// </summary>
    private static TDeclaration PrependAttributeList<TDeclaration>(
        TDeclaration declaration, AttributeListSyntax attributeList)
        where TDeclaration : MemberDeclarationSyntax
    {
        var bare = declaration.WithoutLeadingTrivia();
        return (TDeclaration)bare.WithAttributeLists(bare.AttributeLists.Insert(
            0, attributeList.WithLeadingTrivia(declaration.GetLeadingTrivia())));
    }

    /// <summary>
    /// The once-computed module shape read by every layout consumer (#1802, #2039): the module
    /// namespace (<see cref="NamespaceParts"/> — the project namespace followed by the recorded
    /// <see cref="ModuleLayout.NamespaceSegments"/>), the members class <c>&lt;X&gt;</c>
    /// (<see cref="MembersClassName"/>) its functions, variables and constants live in, the emitted
    /// C# names of its top-level types (siblings of <c>&lt;X&gt;</c> in that namespace), the
    /// recorded layout itself, and whether the module declares the entry-point <c>main()</c>
    /// (<see cref="ModuleIdentifiers.DeclaresEntryMain"/>, #2013).
    /// </summary>
    internal sealed record ModuleShape(
        bool DeclaresEntryMain,
        string MembersClassName,
        IReadOnlyList<string> NamespaceParts,
        IReadOnlySet<string> OwnTypeNames,
        ModuleLayout Layout)
    {
        /// <summary>The <c>global::</c>-rootable path of the members class: namespace + <c>&lt;X&gt;</c>.</summary>
        public string[] MembersClassPath => NamespaceParts.Append(MembersClassName).ToArray();

        /// <summary>The <c>global::</c>-rootable path of a top-level type of this module.</summary>
        public string[] SiblingTypePath(string csharpTypeName) => NamespaceParts.Append(csharpTypeName).ToArray();
    }

    /// <summary>
    /// Computes the <see cref="ModuleShape"/> ONCE, before any declaration is emitted, from the
    /// module layout semantic analysis recorded on the <see cref="Module"/> root (#2039, Decision 28
    /// (e)). No emission — the recorded fact, read into <c>_moduleShape</c>.
    /// </summary>
    private ModuleShape ComputeModuleShape(Module? module, List<Statement> statements)
    {
        bool declaresEntryMain = ModuleIdentifiers.DeclaresEntryMain(statements);

        // The AST-only unit-test path drives the emitter without semantic analysis recording a
        // layout; it reads the same path authority the recorder does, so the two cannot disagree.
        var layout = (module != null ? _context.SemanticInfo?.GetModuleLayout(module) : null)
            ?? (string.IsNullOrEmpty(_context.SourceFilePath)
                ? new ModuleLayout(Array.Empty<string>(), "Module")
                : new ModuleLayout(
                    ModuleIdentifiers.LayoutNamespaceSegments(_context.ProjectRootPath, _context.SourceFilePath),
                    ModuleIdentifiers.LayoutMembersClassName(_context.SourceFilePath)));
        var namespaceParts = layout.NamespaceParts(_context.ProjectNamespace);

        // The emitted C# names of every top-level TYPE this module declares. A same-file type
        // reference is qualified through the module shape ONLY for these names, so a builtin or a
        // Sharpy-runtime type that resolves through the same code path (ValueError, Bytes,
        // RangeIterator) is not mis-qualified as `ModuleClass.ValueError` (#1683).
        var ownTypeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stmt in statements)
        {
            var name = stmt switch
            {
                ClassDef cd => NameCasing.ResolveType(cd.Name, cd.IsNameBacktickEscaped),
                StructDef sd => NameCasing.ResolveType(sd.Name, sd.IsNameBacktickEscaped),
                InterfaceDef id => NameCasing.ResolveInterface(id.Name, id.IsNameBacktickEscaped),
                EnumDef ed => NameCasing.ResolveType(ed.Name, ed.IsNameBacktickEscaped),
                UnionDef ud => NameCasing.ResolveType(ud.Name, ud.IsNameBacktickEscaped),
                DelegateDef dd => NameCasing.ResolveType(dd.Name, dd.IsNameBacktickEscaped),
                _ => (string?)null
            };
            if (name != null)
                ownTypeNames.Add(name);
        }

        return new ModuleShape(declaresEntryMain, layout.MembersClassName, namespaceParts, ownTypeNames, layout);
    }

    /// <summary>
    /// The shape of the module being emitted. <see cref="GenerateCompilationUnit"/> computes it before
    /// any member is generated; the AST-only unit-test paths that emit a single declaration without a
    /// compilation unit get the shape of a nameless module (no namespace, members class
    /// <c>Module</c>).
    /// </summary>
    private ModuleShape CurrentModuleShape
        => _moduleShape ??= ComputeModuleShape(null, new List<Statement>());

    /// <summary>
    /// The Sharpy module name for the [SharpyModule] attribute — the python dotted module path
    /// (e.g., "mypackage.helpers"), read from the one path authority
    /// <see cref="ModuleIdentifiers.SharpyModuleName"/> (#1948).
    /// </summary>
    private string GetSharpyModuleName()
        => ModuleIdentifiers.SharpyModuleName(_context.ProjectRootPath, _context.SourceFilePath);

    /// <summary>
    /// Generates one MemberData wrapper property per module-level variable referenced by a
    /// @test.parametrize(VARIABLE) decorator. The property adapts the variable (a list of
    /// tuples, or a flat list for single-parameter tests) to xUnit's MemberData contract:
    /// <c>public static IEnumerable&lt;object[]&gt;</c>. Scans module-level test functions and
    /// methods of module-level classes; emission follows first-reference source order.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateParametrizeMemberDataProperties(
        List<Statement> statements)
    {
        // Ordered, de-duplicated collection of referenced variable names.
        var referencedNames = new List<string>();
        var seen = new HashSet<string>();

        void Collect(IEnumerable<Decorator> decorators)
        {
            foreach (var decorator in decorators)
            {
                if (decorator.Name == DecoratorNames.TestParametrize
                    && decorator.Arguments.Length == 1
                    && decorator.Arguments[0] is Identifier id
                    && seen.Add(id.Name))
                {
                    referencedNames.Add(id.Name);
                }
            }
        }

        foreach (var stmt in statements)
        {
            switch (stmt)
            {
                case FunctionDef func:
                    Collect(func.Decorators);
                    break;
                case ClassDef classDef:
                    foreach (var member in classDef.Body.OfType<FunctionDef>())
                    {
                        Collect(member.Decorators);
                    }
                    break;
            }
        }

        foreach (var name in referencedNames)
        {
            _memberDataVariables.Add(name);
            var property = GenerateMemberDataProperty(name, statements);
            if (property != null)
            {
                yield return property;
            }
        }
    }

    /// <summary>
    /// Builds the MemberData wrapper property for one parametrize data variable:
    /// <c>public static IEnumerable&lt;object[]&gt; VarMemberData =&gt;
    /// System.Linq.Enumerable.Select(VAR, row =&gt; new object[] { row.Item1, ..., row.ItemN });</c>
    /// For non-tuple element types (single-parameter tests), each element is wrapped directly:
    /// <c>row =&gt; new object[] { row }</c>. Returns null when the variable is not declared in
    /// this module (no wrapper can be generated here).
    /// </summary>
    private PropertyDeclarationSyntax? GenerateMemberDataProperty(
        string variableName, List<Statement> statements)
    {
        // Mirror the module-level field naming rules from GenerateModuleLevelField.
        var varDecl = statements
            .OfType<Parser.Ast.VariableDeclaration>()
            .FirstOrDefault(v => v.Name == variableName);
        if (varDecl == null)
        {
            return null;
        }

        string fieldName;
        if (varDecl.IsConst || NameFormDetector.IsConstantCaseName(variableName))
        {
            fieldName = NameCasing.ResolveConstant(variableName, varDecl.IsNameBacktickEscaped);
        }
        else
        {
            fieldName = NameCasing.ResolveField(variableName, varDecl.IsNameBacktickEscaped);
        }

        // Row arity from the variable's semantic type: list[tuple[...]] → tuple arity
        // (row.Item1..ItemN); any other element type → single-parameter rows (row itself).
        int arity = 1;
        if (_context.LookupSymbol(variableName) is VariableSymbol
            {
                Type: GenericType { TypeArguments.Count: 1 } listType
            }
            && listType.TypeArguments[0] is Semantic.TupleType tupleType)
        {
            arity = tupleType.ElementTypes.Count;
        }

        var rowElements = arity >= 2
            ? Enumerable.Range(1, arity)
                .Select(i => (ExpressionSyntax)MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("row"),
                    IdentifierName($"Item{i}")))
                .ToArray()
            : new ExpressionSyntax[] { IdentifierName("row") };

        // new object[] { row.Item1, ..., row.ItemN }
        var rowArray = ArrayCreationExpression(
            ArrayType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
                .WithRankSpecifiers(SingletonList(ArrayRankSpecifier(
                    SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))))
            .WithInitializer(InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList(rowElements)));

        // global::System.Linq.Enumerable.Select(VAR, row => new object[] { ... })
        var selectCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                ParseQualifiedName("global::System.Linq.Enumerable"),
                IdentifierName("Select")),
            ArgumentList(SeparatedList(new[]
            {
                Argument(EscapedIdentifierName(fieldName)),
                Argument(SimpleLambdaExpression(Parameter(EscapedIdentifier("row")))
                    .WithExpressionBody(rowArray)),
            })));

        return PropertyDeclaration(
                ParseQualifiedTypeName("global::System.Collections.Generic.IEnumerable<object[]>"),
                Identifier(GetMemberDataPropertyName(variableName)))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithExpressionBody(ArrowExpressionClause(selectCall))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
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

        // The source module's members class, global::-rooted (#2039, F13): "mypackage.helpers" ->
        // "global::ProjectNamespace.Mypackage.Helpers.HelpersModule", a subpackage's __init__ ->
        // "global::ProjectNamespace.Mypackage.Sub.SubModule" (#1948).
        var sourceSegments = new List<string>();
        if (!string.IsNullOrEmpty(_context.ProjectNamespace))
            sourceSegments.AddRange(_context.ProjectNamespace!.Split('.'));
        sourceSegments.AddRange(FromImportMembersClassPath(fromImport));
        var sourceClass = MakeGlobalQualifiedName(sourceSegments.ToArray());

        foreach (var (localName, symbol) in reExportedSymbols)
        {
            switch (symbol)
            {
                case FunctionSymbol funcSymbol:
                    yield return GenerateReExportMethod(localName, funcSymbol, sourceClass);
                    break;

                case VariableSymbol varSymbol:
                    yield return GenerateReExportProperty(localName, varSymbol, sourceClass);
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
    private MemberDeclarationSyntax GenerateReExportMethod(string localName, FunctionSymbol funcSymbol, NameSyntax sourceClass)
    {
        var methodName = NameMangler.Transform(localName, NameContext.Method);
        var sourceMethodName = NameMangler.Transform(funcSymbol.Name, NameContext.Method);

        // Generate parameter list
        var parameters = funcSymbol.Parameters
            .Select(p =>
            {
                var paramName = ParameterCSharpName(p);
                var paramType = _typeMapper.MapSemanticType(p.Type);
                return Parameter(EscapedIdentifier(paramName)).WithType(paramType);
            })
            .ToArray();

        // Generate arguments to pass to the delegate call
        var arguments = funcSymbol.Parameters
            .Select(p => Argument(IdentifierName(ParameterCSharpName(p))))
            .ToArray();

        // Map return type
        var returnType = _typeMapper.MapSemanticType(funcSymbol.ReturnType);

        // Build the delegate call: SourceClass.Method(args)
        var delegateCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                sourceClass,
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
    private MemberDeclarationSyntax GenerateReExportProperty(string localName, VariableSymbol varSymbol, NameSyntax sourceClass)
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
            sourceClass,
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

            using var _gen = SetGeneratorScope(_context.Ir.IsGenerator(func));
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
                // An explicit `-> None` annotation maps to `void`, which must become
                // bare `Task` (not `Task<void>` — that is invalid C#).
                returnType = (func.ReturnType != null && !IsVoidType(returnType))
                    ? WrapInTask(returnType) : TaskType();
            }

            // Determine which parameters are fixture-injected and exclude them from the
            // emitted parameter list (xUnit's [Fact] takes no params; for [Theory], only
            // the parametrize columns remain). User fixtures (IClassFixture-injected) and
            // built-in per-test fixtures (e.g. tmp_path) are handled the same way per-method
            // (stripped + prelude); they differ only in class-level wiring.
            var consumedForFunc = GetConsumedFixtures(func)
                .Concat(GetConsumedBuiltinFixtures(func))
                .ToList();
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
                var paramName = ParameterCSharpName(param);
                if (param.IsLateBound)
                {
                }
                else
                {
                }
                var baseName = ParameterCSharpName(param);
            }

            // Track fixture-injected names as declared variables to avoid versioning collisions.
            foreach (var (parameter, _) in consumedForFunc)
            {
                var localName = ParameterCSharpName(parameter);
            }

            var preamble = GenerateLateBoundPreamble(paramsExcludingFixtures);
            var fixturePrelude = GenerateFixturePrelude(consumedForFunc);
            var body = Block(preamble
                .Concat(fixturePrelude)
                .Concat(GenerateSuite(func.Body)));

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
                    .Select(GenerateMethodTypeParameterSyntax)
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

        }

        _isInTestFunction = savedIsInTestFunction;

        // Test class name: the recorded <X>Tests (#2039, F14), a sibling of <X> in the module
        // namespace. Always public; not static (xUnit instantiates the class per test method).
        var shape = CurrentModuleShape;
        var testClassName = shape.Layout.TestClassName ?? shape.MembersClassName + "Tests";

        // Compose class-level fixture wiring. Two mechanisms can coexist:
        //   - User fixtures: Xunit.IClassFixture<T> base types + ctor injection + readonly fields
        //     (shared once per test class).
        //   - Built-in tmp_path: a per-test TmpPathFixture instance field + System.IDisposable
        //     (fresh per test method, disposed after each — pytest's per-test lifecycle).
        // Member order is deterministic: fields (user, then tmp_path), ctor, test methods, Dispose.
        var baseTypes = new List<BaseTypeSyntax>();
        var fieldMembers = new List<MemberDeclarationSyntax>();
        ConstructorDeclarationSyntax? ctor = null;
        var suffixMembers = new List<MemberDeclarationSyntax>();

        if (consumedFixtures.Count > 0)
        {
            var ctorParams = new List<ParameterSyntax>();
            var ctorStmts = new List<StatementSyntax>();

            // Sort by fixture name for deterministic output.
            foreach (var fixture in consumedFixtures.Values
                .OrderBy(f => f.SharpyName, System.StringComparer.Ordinal))
            {
                baseTypes.Add(SimpleBaseType(
                    TypeSyntaxMapper.QualifiedGenericName(
                        "Xunit.IClassFixture", IdentifierName(fixture.ClassName))));

                // private readonly XFixture _xFixture;
                var fieldDecl = FieldDeclaration(
                        VariableDeclaration(IdentifierName(fixture.ClassName))
                            .WithVariables(SingletonSeparatedList(VariableDeclarator(fixture.FieldName))))
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword)));
                fieldMembers.Add(fieldDecl);

                // ctor parameter: XFixture xFixture
                var paramName = fixture.FieldName.TrimStart('_');
                ctorParams.Add(Parameter(EscapedIdentifier(paramName))
                    .WithType(IdentifierName(fixture.ClassName)));

                // _xFixture = xFixture;
                ctorStmts.Add(ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(fixture.FieldName),
                    IdentifierName(paramName))));
            }

            ctor = ConstructorDeclaration(EscapedIdentifier(testClassName))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList(ctorParams)))
                .WithBody(Block(ctorStmts));
        }

        // Built-in tmp_path: per-test instance field + System.IDisposable + Dispose().
        bool usesTmpPath = testFunctions.Any(f => GetConsumedBuiltinFixtures(f).Count > 0);
        if (usesTmpPath)
        {
            var tmpPathType = MakeGlobalQualifiedName("Sharpy", "TmpPathFixture");

            // private readonly global::Sharpy.TmpPathFixture _tmpPathFixture = new global::Sharpy.TmpPathFixture();
            fieldMembers.Add(FieldDeclaration(
                    VariableDeclaration(tmpPathType)
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(BuiltinTmpPathFixture.FieldName)
                                .WithInitializer(EqualsValueClause(
                                    ObjectCreationExpression(tmpPathType).WithArgumentList(ArgumentList()))))))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword))));

            baseTypes.Add(SimpleBaseType(MakeGlobalQualifiedName("System", "IDisposable")));

            // public void Dispose() { _tmpPathFixture.Dispose(); }
            suffixMembers.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Dispose")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(ExpressionStatement(InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(BuiltinTmpPathFixture.FieldName),
                        IdentifierName("Dispose")))))));
        }

        if (fieldMembers.Count > 0 || ctor != null || suffixMembers.Count > 0)
        {
            var newMembers = new List<MemberDeclarationSyntax>();
            newMembers.AddRange(fieldMembers);
            if (ctor != null)
                newMembers.Add(ctor);
            newMembers.AddRange(members);
            newMembers.AddRange(suffixMembers);
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
            // Note: PropertyDef is intercepted in GenerateModuleClass before this dispatcher
            // (property groups can expand into multiple members; see GenerateModuleLevelProperty).
            _ => EmitUnrecognizedStatementDiagnostic(stmt)
        };
    }

    /// <summary>
    /// Generates static C# members from a group of module-level PropertyDef nodes
    /// sharing the same name. Reuses the class-level grouped property generation
    /// (split get/set merging, mixed auto+custom backing fields, auto-properties)
    /// and forces the static modifier: module-level properties have no instance,
    /// so they are always static members of the module class (#844).
    /// Function-style accessors are already emitted static (no self parameter);
    /// this additionally covers auto-properties and generated backing fields.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateModuleLevelProperty(List<PropertyDef> propGroup)
    {
        foreach (var member in GenerateGroupedProperty(propGroup))
        {
            yield return member switch
            {
                PropertyDeclarationSyntax prop when !prop.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                    => prop.AddModifiers(Token(SyntaxKind.StaticKeyword)),
                FieldDeclarationSyntax field when !field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                    => field.AddModifiers(Token(SyntaxKind.StaticKeyword)),
                _ => member
            };
        }
    }
}
