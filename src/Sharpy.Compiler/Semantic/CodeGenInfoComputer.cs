using System.Collections.Immutable;
using System.Collections.Frozen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Computes CodeGenInfo for all symbols in a module.
/// This class runs after type checking to populate CodeGenInfo on symbols.
///
/// The computation mirrors what RoslynEmitter currently does at emission time,
/// but does it once during semantic analysis instead of dynamically during emission.
/// </summary>
internal class CodeGenInfoComputer
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticBinding _semanticBinding;
    private readonly SemanticInfo? _semanticInfo;
    private readonly DiagnosticBag _diagnostics;
    private readonly HashSet<string> _processedModuleLevelVars = new();
    private HashSet<string> _variablesWithExecutionOrderIssues = new();
    private string? _sourceFilePath;
    private string? _sourceRootPath;
    private SemanticBinding? _importFacts;
    private bool _isEntryPoint;

    // Module-level const analysis is now in ConstEligibility (#1791); this class reads the fact
    // from SemanticBinding.GetCompileTimeConstant in ProcessModuleLevelConstant.

    public CodeGenInfoComputer(SymbolTable symbolTable, SemanticBinding? semanticBinding = null,
        DiagnosticBag? diagnostics = null, SemanticInfo? semanticInfo = null)
    {
        _symbolTable = symbolTable;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
        _diagnostics = diagnostics ?? new DiagnosticBag();
        _semanticInfo = semanticInfo;
    }

    private TypeResolver? _synthesizedArgumentResolver;

    /// <summary>
    /// The resolver <see cref="SynthesizedInterfaceReader"/> converts a synthesized reference's
    /// written arguments with (an <c>__eq__(self, other: Foo)</c> row names <c>Foo</c>). Null when
    /// this computer was built without a <see cref="SemanticInfo"/>, in which case only rows whose
    /// arguments are already resolved, empty, or the declaring type's own parameters are read.
    /// </summary>
    private TypeResolver? SynthesizedArgumentResolver
        => _semanticInfo == null
            ? null
            : _synthesizedArgumentResolver ??= new TypeResolver(_symbolTable, _semanticInfo);

    /// <summary>
    /// Sets CodeGenInfo in SemanticBinding. Symbol properties are populated later
    /// by MaterializeCodeGenInfo() at the freeze point.
    /// </summary>
    private void SetCodeGenInfo(Symbol symbol, CodeGenInfo info)
    {
        System.Diagnostics.Debug.WriteLineIf(
            _symbolTable.BuiltinRegistry.IsBuiltinSymbol(symbol),
            $"[CodeGenInfoComputer] SetCodeGenInfo on builtin '{symbol.Name}' from {_sourceFilePath}");
        _semanticBinding.SetCodeGenInfo(symbol, info);
    }

    /// <summary>
    /// Compute CodeGenInfo for all symbols in the module.
    /// </summary>
    /// <param name="isEntryPoint">
    /// Whether the module is the program's entry file. A non-entry module's top-level <c>main</c>
    /// cannot be named <c>Main</c> (a second C# entry-point candidate), so its materialized name is
    /// <c>MainFunc</c> — read by the declaration and every reference alike (#2065).
    /// </param>
    /// <param name="sourceRootPath">
    /// The project's source root — the recorded module layout's namespace segments are the file's
    /// directories below it (#2039). Null when unknown: the module is then its own namespace.
    /// </param>
    /// <param name="importFacts">
    /// The binding import resolution wrote a from-import's resolved source file and the .NET-module
    /// marks to — the shared project binding when this computer writes a per-file one (#2039). Null:
    /// the computer's own binding.
    /// </param>
    public void ComputeForModule(Module module, string? sourceFilePath = null, bool isEntryPoint = false,
        string? sourceRootPath = null, SemanticBinding? importFacts = null)
    {
        _sourceFilePath = sourceFilePath;
        _sourceRootPath = sourceRootPath;
        _importFacts = importFacts ?? _semanticBinding;
        _isEntryPoint = isEntryPoint;

        // Run execution order analysis first to detect variables that need special handling
        var analyzer = new ExecutionOrderAnalyzer(_symbolTable);
        _variablesWithExecutionOrderIssues = analyzer.Analyze(module.Body);

        // First pass: Process module-level declarations (top-level statements)
        ProcessModuleLevelDeclarations(module);

        // Second pass: Process type declarations (classes, structs, interfaces, enums)
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ProcessClassDef(classDef);
                    break;
                case StructDef structDef:
                    ProcessStructDef(structDef);
                    break;
                case InterfaceDef interfaceDef:
                    ProcessInterfaceDef(interfaceDef);
                    break;
                case EnumDef enumDef:
                    ProcessEnumDef(enumDef);
                    break;
                case UnionDef unionDef:
                    DetectUnionEnclosingTypeCollisions(unionDef);
                    break;
                case FunctionDef funcDef:
                    ProcessFunctionDef(funcDef, isModuleLevel: true);
                    break;
            }
        }

        // Every declared TYPE — top-level and nested, of every kind — carries its emitted C# name
        // and its source spelling, the fact the emitter's [SharpyName] stamp reads (#2006, R-CF).
        foreach (var stmt in module.Body)
            SetDeclaredTypeNames(stmt, enclosing: null);

        // The module-as-namespace layout (#2039): every top-level type is a sibling of <X> in the
        // module namespace, and the module's own layout is recorded on its root for the emitter.
        MarkNamespaceSiblings(module);
        var layout = RecordOwnModuleLayout(module);

        // Third pass: Detect module-level name collisions
        DetectModuleLevelCollisions(module, layout);

        // Fourth pass: name every local of every function-like scope. Runs last so that a chain
        // whose head is a module-level variable (write-through from a function) inherits the
        // module-level CodeGenInfo set above, and fields/methods are already named and skipped.
        // One call for the whole table — methods, constructors, accessors, lambdas and nested
        // defs alike — so no owner kind can be left unallocated (#1560 C1).
        AllocateLocalNames();
    }

    /// <summary>
    /// Marks every top-level type of the module as a namespace sibling of its members class (#2039,
    /// Decision 28 (a)). Only a type that already carries a <see cref="CodeGenInfo"/> is marked —
    /// class, struct, interface and enum; union and delegate type symbols carry none until the
    /// python-name channel (#2006, Decision 22) materializes one.
    /// </summary>
    private void MarkNamespaceSiblings(Module module)
    {
        foreach (var stmt in module.Body)
        {
            var name = stmt.UnwrapDecorated() switch
            {
                ClassDef c => c.Name,
                StructDef st => st.Name,
                InterfaceDef i => i.Name,
                EnumDef e => e.Name,
                UnionDef u => u.Name,
                DelegateDef d => d.Name,
                _ => null,
            };
            if (name != null
                && _symbolTable.Lookup(name) is TypeSymbol typeSymbol
                && _semanticBinding.GetCodeGenInfo(typeSymbol) is { } info)
            {
                SetCodeGenInfo(typeSymbol, info with { IsNamespaceSibling = true });
            }
        }
    }

    /// <summary>
    /// Records the file's own layout on its <see cref="Module"/> root (the module being emitted has
    /// no <see cref="ModuleSymbol"/>, #2039): its namespace segments, <c>&lt;X&gt;</c>, and the other
    /// classes it emits into that namespace — the test class <c>&lt;X&gt;Tests</c> when a top-level
    /// function carries a test decorator, and one <c>&lt;Name&gt;Fixture</c> per <c>@test.fixture</c>.
    /// Null when the file has no name (nothing to derive a layout from).
    /// </summary>
    private ModuleLayout? RecordOwnModuleLayout(Module module)
    {
        if (string.IsNullOrEmpty(_sourceFilePath))
            return null;

        var membersClass = ModuleIdentifiers.LayoutMembersClassName(_sourceFilePath);
        var functions = module.Body.Select(s => s.UnwrapDecorated()).OfType<FunctionDef>().ToList();
        var fixtures = functions
            .Where(f => f.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.TestFixture))
            .Select(f => NameMangler.ToPascalCase(f.Name) + "Fixture")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var hasTests = functions.Any(f => f.Decorators.Any(DecoratorNames.IsTestDecorator)
            && !f.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.TestFixture));
        var layout = new ModuleLayout(
            ModuleIdentifiers.LayoutNamespaceSegments(_sourceRootPath, _sourceFilePath),
            membersClass,
            hasTests ? membersClass + "Tests" : null,
            fixtures.Count > 0 ? fixtures : null);
        _semanticInfo?.SetModuleLayout(module, layout);
        return layout;
    }

    /// <summary>
    /// The layout of a module a file imports (#2039): a project module's from its file path and the
    /// source root, a .NET (discovered) module's from its reflected namespace and module class.
    /// Null when neither is known.
    /// </summary>
    private ModuleLayout? ImportedModuleLayout(string? filePath, string? netNamespace, string? netClassName, bool isNetModule)
    {
        if (isNetModule)
        {
            return netClassName == null
                ? null
                : new ModuleLayout(
                    (netNamespace ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries).ToList(), netClassName);
        }

        return string.IsNullOrEmpty(filePath)
            ? null
            : new ModuleLayout(
                ModuleIdentifiers.LayoutNamespaceSegments(_sourceRootPath, filePath),
                ModuleIdentifiers.LayoutMembersClassName(filePath));
    }

    private void AllocateLocalNames()
    {
        if (_semanticInfo == null)
            return;

        new LocalNameAllocator(_semanticBinding, _semanticInfo).AllocateAll(_symbolTable);
    }

    private void ProcessModuleLevelDeclarations(Module module)
    {
        foreach (var stmt in module.Body)
        {
            // Unwrap for classification only: a suppress-decorated import (#1124) must still
            // produce its module-level CodeGenInfo. Definitions/variables are never wrapped
            // (decorators attach directly), so unwrap is a no-op for the other cases.
            switch (stmt.UnwrapDecorated())
            {
                case VariableDeclaration varDecl when varDecl.IsConst:
                    ProcessModuleLevelConstant(varDecl);
                    break;
                case VariableDeclaration varDecl:
                    ProcessModuleLevelVariable(varDecl);
                    break;
                case ImportStatement import:
                    ProcessImport(import);
                    break;
                case FromImportStatement fromImport:
                    ProcessFromImport(fromImport);
                    break;
                case PropertyDef propDef:
                    ProcessModuleLevelProperty(propDef);
                    break;
            }
        }
    }

    private void ProcessModuleLevelProperty(PropertyDef propDef)
    {
        var symbol = _symbolTable.Lookup(propDef.Name);
        if (symbol is VariableSymbol { IsModuleProperty: true } varSymbol && !_semanticBinding.HasCodeGenInfo(varSymbol))
        {
            var escaped = propDef.IsNameBacktickEscaped;
            var csharpName = NameCasing.ResolveField(propDef.Name, escaped);

            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = csharpName,
                OriginalName = propDef.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = false,
                HasExecutionOrderIssues = false
            });
        }
    }

    private void ProcessModuleLevelVariable(VariableDeclaration varDecl)
    {
        var symbol = _symbolTable.Lookup(varDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            // Use execution order analysis result instead of simple initializer check
            var hasIssues = _variablesWithExecutionOrderIssues.Contains(varDecl.Name);

            // Variables with execution order issues become locals in Main() and use camelCase
            // ALL_CAPS names (Python-style constants) use ToConstantCase (preserves SCREAMING_SNAKE_CASE)
            // Other module-level fields use ToPascalCase
            var escaped = varDecl.IsNameBacktickEscaped;
            string csharpName;
            if (hasIssues)
                csharpName = NameCasing.ResolveVariable(varDecl.Name, escaped);
            else if (NameFormDetector.IsConstantCaseName(varDecl.Name))
                csharpName = NameCasing.ResolveConstant(varDecl.Name, escaped);
            else
                csharpName = NameCasing.ResolveField(varDecl.Name, escaped);

            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = csharpName,
                OriginalName = varDecl.Name,
                Version = 0,
                IsModuleLevel = !hasIssues,  // Not module-level if has execution order issues
                IsConstant = false,
                HasExecutionOrderIssues = hasIssues
            });
            _processedModuleLevelVars.Add(varDecl.Name);
        }
    }

    private void ProcessModuleLevelConstant(VariableDeclaration constDecl)
    {
        var symbol = _symbolTable.Lookup(constDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            // The compile-time constant fact is computed by ConstEligibility during semantic
            // analysis and stored on SemanticBinding (#1791). This method copies it.
            var isCompileTime = _semanticBinding.GetCompileTimeConstant(varSymbol);

            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveConstant(constDecl.Name, constDecl.IsNameBacktickEscaped),
                OriginalName = constDecl.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = true,
                IsCompileTimeConstant = isCompileTime,
                HasExecutionOrderIssues = false
            });
        }
    }

    // Phase 1 moved const-eligibility analysis to ConstEligibility.cs.
    // The ConstantPositionValidator calls ConstEligibility.LowersToConstantExpression directly.
    private void ProcessImport(ImportStatement import)
    {
        foreach (var alias in import.Names)
        {
            var effectiveName = alias.AsName ?? alias.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol is ModuleSymbol moduleSymbol)
            {
                var layout = ImportedModuleLayout(moduleSymbol.FilePath, moduleSymbol.CSharpNamespace,
                    moduleSymbol.CSharpClassName, moduleSymbol.IsNetModule);
                SetCodeGenInfo(moduleSymbol, new CodeGenInfo
                {
                    CSharpName = effectiveName.Replace(".", "_", StringComparison.Ordinal),
                    OriginalName = effectiveName,
                    ImportKind = alias.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.ModuleImport,
                    NamespaceSegments = layout?.NamespaceSegments,
                    MembersClassName = layout?.MembersClassName
                });
            }
        }
    }

    private void ProcessFromImport(FromImportStatement fromImport)
    {
        var isNetModule = _semanticBinding.IsNetModule(fromImport.Module);

        // The source module's layout, on the import node (a from-import names no ModuleSymbol, #2039).
        // Read from the binding import resolution wrote to: in a project build `_semanticBinding` is
        // the per-file binding, which holds neither the resolved path nor the .NET-module marks.
        var importFacts = _importFacts ?? _semanticBinding;
        var sourceIsNetModule = importFacts.IsNetModule(fromImport.Module);
        var sourceLayout = ImportedModuleLayout(
            importFacts.GetResolvedModuleFilePath(fromImport),
            sourceIsNetModule ? importFacts.GetNetModuleCSharpNamespace(fromImport.Module) : null,
            sourceIsNetModule ? importFacts.GetNetModuleCSharpClassName(fromImport.Module) : null,
            sourceIsNetModule);
        if (sourceLayout != null)
            _semanticInfo?.SetModuleLayout(fromImport, sourceLayout);

        foreach (var imported in fromImport.Names)
        {
            var effectiveName = imported.AsName ?? imported.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol != null)
            {
                var originalName = imported.AsName != null ? imported.Name : null;
                var csharpName = DetermineCSharpNameForFromImport(imported.Name, symbol, isNetModule);

                SetCodeGenInfo(symbol, new CodeGenInfo
                {
                    CSharpName = csharpName,
                    OriginalName = effectiveName,
                    ImportKind = imported.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.FromImport,
                    OriginalImportName = originalName,
                    // A Sharpy module's exported types are its top-level types: siblings of its <X>.
                    IsNamespaceSibling = symbol is TypeSymbol && !sourceIsNetModule && sourceLayout != null
                });
            }
        }
    }

    private string DetermineCSharpNameForFromImport(string name, Symbol symbol, bool isNetModule)
    {
        // For .NET module variable fields, SCREAMING_SNAKE_CASE names match C# verbatim
        // (e.g., csv.QUOTE_ALL). Other names need PascalCase conversion to match the
        // generated C# field names (e.g., string.digits → Digits, math.pi → Pi).
        if (isNetModule && symbol is VariableSymbol)
        {
            if (NameFormDetector.IsConstantCaseName(name))
                return name;
            return NameCasing.ResolveMethod(name, symbol.IsNameBacktickEscaped);
        }

        // Use the same logic as RoslynEmitter for from-imports:
        // - ALL_CAPS names (constants) use ToConstantCase (preserves SCREAMING_SNAKE_CASE)
        // - Other names become PascalCase
        if (NameFormDetector.IsConstantCaseName(name))
        {
            return NameCasing.ResolveConstant(name, symbol.IsNameBacktickEscaped);
        }
        return NameCasing.ResolveMethod(name, symbol.IsNameBacktickEscaped);
    }

    /// <summary>
    /// Records a type declaration's emitted C# name and source spelling on its symbol — and on its
    /// union cases' and nested types' symbols — with the spellers the emitter's declaration sites
    /// use. The emitter stamps <c>[SharpyName]</c> from this when the two differ (#2006, R-CF):
    /// union, union-case, delegate and every nested type symbol had no type-level
    /// <see cref="CodeGenInfo"/> before. Merged into an existing record, never replacing its other
    /// facts.
    /// </summary>
    private void SetDeclaredTypeNames(Statement stmt, TypeSymbol? enclosing)
    {
        var (name, csharpName, body) = stmt switch
        {
            ClassDef d => (d.Name, NameCasing.ResolveType(d.Name, d.IsNameBacktickEscaped), d.Body),
            StructDef d => (d.Name, NameCasing.ResolveType(d.Name, d.IsNameBacktickEscaped), d.Body),
            InterfaceDef d => (d.Name, NameCasing.ResolveInterface(d.Name, d.IsNameBacktickEscaped), d.Body),
            EnumDef d => (d.Name, NameCasing.ResolveType(d.Name, d.IsNameBacktickEscaped), ImmutableArray<Statement>.Empty),
            UnionDef d => (d.Name, NameCasing.ResolveType(d.Name, d.IsNameBacktickEscaped), d.Body),
            DelegateDef d => (d.Name, NameCasing.ResolveType(d.Name, d.IsNameBacktickEscaped), ImmutableArray<Statement>.Empty),
            _ => ((string?)null, (string?)null, ImmutableArray<Statement>.Empty),
        };
        if (name == null || csharpName == null)
            return;

        var symbol = enclosing != null
            ? enclosing.NestedTypes.FirstOrDefault(t => t.Name == name)
            : _symbolTable.Lookup(name) as TypeSymbol;
        if (symbol == null)
            return;

        SetTypeNames(symbol, csharpName, name);
        if (stmt is UnionDef union)
        {
            foreach (var caseDef in union.Cases)
            {
                if (symbol.UnionCases.FirstOrDefault(c => c.Name == caseDef.Name) is { } caseSymbol)
                    SetTypeNames(caseSymbol, NameMangler.Transform(caseDef.Name, NameContext.Type), caseDef.Name);
            }
        }

        foreach (var member in body)
            SetDeclaredTypeNames(member, symbol);
    }

    private void SetTypeNames(TypeSymbol typeSymbol, string csharpName, string originalName)
    {
        var existing = _semanticBinding.GetCodeGenInfo(typeSymbol);
        SetCodeGenInfo(typeSymbol, existing is null
            ? new CodeGenInfo { CSharpName = csharpName, OriginalName = originalName }
            : existing with { CSharpName = csharpName, OriginalName = originalName });
    }

    private void ProcessClassDef(ClassDef classDef)
    {
        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveType(classDef.Name, classDef.IsNameBacktickEscaped),
                OriginalName = classDef.Name
            });

            ComputeSynthesizedInterfaces(typeSymbol, classDef.Body);

            // Process class members
            ProcessTypeMembers(typeSymbol, classDef.Body);
            DetectMemberCollisions(typeSymbol, classDef.Body,
                enclosingCSharpName: NameCasing.ResolveType(classDef.Name, classDef.IsNameBacktickEscaped));
        }
    }

    private void ProcessStructDef(StructDef structDef)
    {
        var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveType(structDef.Name, structDef.IsNameBacktickEscaped),
                OriginalName = structDef.Name
            });

            ComputeSynthesizedInterfaces(typeSymbol, structDef.Body);

            ProcessTypeMembers(typeSymbol, structDef.Body);
            DetectMemberCollisions(typeSymbol, structDef.Body,
                enclosingCSharpName: NameCasing.ResolveType(structDef.Name, structDef.IsNameBacktickEscaped));
        }
    }

    private void ProcessInterfaceDef(InterfaceDef interfaceDef)
    {
        var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Interfaces preserve their exact name (which should already have I prefix)
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveInterface(interfaceDef.Name, interfaceDef.IsNameBacktickEscaped),
                OriginalName = interfaceDef.Name
            });

            ProcessTypeMembers(typeSymbol, interfaceDef.Body);
            // No enclosing-type seed: CS0542 is a class/struct rule, an interface member may share
            // its interface's name.
            DetectMemberCollisions(typeSymbol, interfaceDef.Body, enclosingCSharpName: null);
        }
    }

    private void ProcessEnumDef(EnumDef enumDef)
    {
        var typeSymbol = _symbolTable.Lookup(enumDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Determine if this is a string enum (has at least one string literal value) — the
            // shared predicate, so the emitted shape and the checker's rules cannot disagree (#1442).
            var isStringEnum = NameResolver.IsStringEnum(enumDef);

            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveType(enumDef.Name, enumDef.IsNameBacktickEscaped),
                OriginalName = enumDef.Name,
                IsStringEnum = isStringEnum
            });

            MaterializeEnumMemberNames(typeSymbol, enumDef, isStringEnum);
            DetectEnumMemberCollisions(enumDef, isStringEnum);
            DetectEnumEnclosingTypeCollisions(enumDef);
        }
    }

    /// <summary>
    /// Materializes each enum member's C# identifier on its member symbol (#2037, Rule 2 pattern a):
    /// <see cref="NameCasing.ResolveEnumMember"/> with the DECLARATION's escape flag, which then
    /// governs every reference — the declaration arms, <c>C.x</c>, <c>case C.x:</c> and a qualified
    /// <c>H.C.x</c> all read this one fact, so a plain use of an escaped member and an escaped use of
    /// a plain one spell what the declaration emitted (they were six spellers, and an escaped string
    /// member or a nested camelCase int member was CS0117). Top-level and nested enums alike.
    /// </summary>
    private void MaterializeEnumMemberNames(TypeSymbol enumSymbol, EnumDef enumDef, bool isStringEnum)
    {
        foreach (var member in enumDef.Members)
        {
            if (enumSymbol.Fields.FirstOrDefault(f => f.Name == member.Name) is not { } memberSymbol)
                continue;

            var csharpName = NameCasing.ResolveEnumMember(member.Name, isStringEnum, member.IsNameBacktickEscaped);
            SetCodeGenInfo(memberSymbol, new CodeGenInfo
            {
                CSharpName = csharpName,
                OriginalName = member.Name,
                // The python-name channel (#2007): an int-enum field whose emitted identifier is not
                // the declared name records the declared one, so str/repr/.name read it at runtime.
                EnumMemberPythonName = !isStringEnum && csharpName != member.Name ? member.Name : null
            });
        }
    }

    private void ProcessTypeMembers(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        // A struct with no explicit __init__ gets a synthesized constructor whose roster is its
        // instance fields AND its defaulted auto-properties (#1938).
        bool synthesizesStructConstructor = typeSymbol.TypeKind == TypeKind.Struct
            && !body.OfType<FunctionDef>().Any(f => f.Name == DunderNames.Init);

        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case VariableDeclaration fieldDecl:
                    ProcessField(typeSymbol, fieldDecl);
                    break;
                case PropertyDef propDef when synthesizesStructConstructor:
                    MarkStructConstructorProperty(typeSymbol, propDef);
                    break;
                case FunctionDef funcDef:
                    ProcessMethodDef(typeSymbol, funcDef);
                    break;

                // A NESTED type's members reach no other pass: the module-body loop sees only
                // top-level declarations, so a nested class's field consts had no CodeGenInfo at
                // all and the emitter read the default (`static readonly`) whatever the fact said
                // (#1791). The nested symbol is its enclosing type's own child. Routed through the
                // one nested-declaration classifier (#1729, R-I) so a nested union joins the same
                // member processing (its body carries methods/consts); enum, delegate and alias
                // carry no member body — the classifier returns an empty Body and they fall through.
                default:
                    if (stmt.TryGetNestedDeclaration(out var nested) && !nested.Body.IsEmpty)
                    {
                        ProcessNestedTypeMembers(typeSymbol, nested.Name, nested.Body);
                        DetectNestedEnclosingTypeCollisions(typeSymbol, stmt, nested);
                    }
                    else if (stmt is UnionDef nestedUnion)
                    {
                        DetectUnionEnclosingTypeCollisions(nestedUnion);
                    }
                    else if (stmt is EnumDef nestedEnum)
                    {
                        var nestedIsStringEnum = NameResolver.IsStringEnum(nestedEnum);
                        if (typeSymbol.NestedTypes.FirstOrDefault(t => t.Name == nestedEnum.Name) is { } nestedEnumSymbol)
                            MaterializeEnumMemberNames(nestedEnumSymbol, nestedEnum, nestedIsStringEnum);
                        // The member walk runs for a nested enum too — it only ever ran from the
                        // module-level ProcessEnumDef, so a nested pair was CS0102 behind SPY0908 (#2036).
                        DetectEnumMemberCollisions(nestedEnum, nestedIsStringEnum);
                        DetectEnumEnclosingTypeCollisions(nestedEnum);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Materializes <see cref="PropertySymbol.IsConstructorParameter"/> (#1938): a defaulted instance
    /// auto-property of a constructor-synthesizing struct is a roster member when its default is an
    /// admitted constant parameter default — the same admission a def parameter default passes, so
    /// the synthesized constructor can carry it as a C# default value (a non-constant one would be
    /// CS1736). No resolvers are passed (as for <see cref="RequiresPerInstanceDefault"/>): a literal,
    /// a signed literal or <c>None</c> qualifies; any other default keeps its initializer, and the
    /// struct still gets its explicit parameterless constructor from the emitter.
    /// </summary>
    private static void MarkStructConstructorProperty(TypeSymbol typeSymbol, PropertyDef propDef)
    {
        if (!MemberClassification.IsConstructorOwnedProperty(propDef))
            return;

        var kind = Validation.ConstantDefaultClassifier.Classify(propDef.DefaultValue!);
        if (!Validation.ConstantDefaultClassifier.IsAdmitted(kind, Validation.AdmissionTable.ParameterDefault))
            return;

        var index = typeSymbol.Properties.FindIndex(p => p.Name == propDef.Name && !p.IsStatic);
        if (index >= 0)
            typeSymbol.Properties[index] = typeSymbol.Properties[index] with { IsConstructorParameter = true };
    }

    private void ProcessNestedTypeMembers(TypeSymbol enclosing, string name, IEnumerable<Statement> body)
    {
        var nested = enclosing.NestedTypes.FirstOrDefault(t => t.Name == name);
        if (nested != null)
            ProcessTypeMembers(nested, body);
    }

    private void ProcessField(TypeSymbol typeSymbol, VariableDeclaration fieldDecl)
    {
        var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
        if (fieldSymbol != null)
        {
            // The compile-time constant fact is computed by ConstEligibility (#1791).
            var isCompileTime = fieldDecl.IsConst && _semanticBinding.GetCompileTimeConstant(fieldSymbol);

            SetCodeGenInfo(fieldSymbol, new CodeGenInfo
            {
                CSharpName = NameCasing.ResolveField(fieldDecl.Name, fieldDecl.IsNameBacktickEscaped),
                OriginalName = fieldDecl.Name,
                IsModuleLevel = false,
                IsConstant = fieldDecl.IsConst,
                IsCompileTimeConstant = isCompileTime,
                RequiresPerInstanceDefault = RequiresPerInstanceDefault(fieldDecl, fieldSymbol),
            });
        }
    }

    /// <summary>
    /// The ONE classification authority for a dataclass/struct field default's shape (#1684, R-A):
    /// <see cref="Validation.ConstantDefaultClassifier"/>, the same call
    /// <c>ConstantPositionValidator.ValidateDataclassFieldDefaults</c>/<c>ValidateStructFieldDefaults</c>
    /// check against <c>AdmissionTable.PerInstanceFieldDefault</c>. Mirrors that validator's full
    /// verdict, not just the shape: a <c>const</c> or <c>@static</c> field is never a
    /// constructor-parameter default (#1900), a field with no default has nothing to classify, and a
    /// NULLABLE/OPTIONAL-typed field keeps the validator's SPY0400 refusal (the <c>arg ?? &lt;default&gt;</c>
    /// sentinel would collide with a caller-supplied <c>None</c>) — so compilation never reaches code
    /// generation for that combination and this reads false rather than a value CodeGen must never
    /// act on. No resolvers are passed to <c>Classify</c>: the mutable-collection kinds
    /// (<see cref="Validation.EmittableConstantKind.Collection"/>,
    /// <see cref="Validation.EmittableConstantKind.Comprehension"/>) are decided purely by the AST
    /// shape — a list/dict/set literal, a <c>list()</c>/<c>dict()</c>/<c>set()</c> call, or a
    /// list/dict/set comprehension — never by the Identifier/MemberAccess/operator branches the
    /// resolvers inform.
    /// </summary>
    private static bool RequiresPerInstanceDefault(VariableDeclaration fieldDecl, VariableSymbol fieldSymbol)
    {
        if (fieldDecl.InitialValue == null || fieldDecl.IsConst
            || fieldDecl.Decorators.Any(d => d.Name == DecoratorNames.Static)
            || fieldSymbol.Type is NullableType or OptionalType)
            return false;

        var kind = Validation.ConstantDefaultClassifier.Classify(fieldDecl.InitialValue);
        return kind is Validation.EmittableConstantKind.Collection
            or Validation.EmittableConstantKind.Comprehension;
    }

    /// <summary>
    /// The six comparison dunders whose C# operator carries a parameter-shape decision. Immutable
    /// (<see cref="FrozenSet{T}"/>, the <c>KnownPythonCollectionVerbs</c> precedent) because a
    /// mutable static in the compiler assembly is per-process state a warm build can carry across
    /// compilations — <c>StaticStateConformanceTests</c> enumerates every static field and a
    /// <c>HashSet</c> here is a listed failure, never an allowlist entry (#1719).
    /// </summary>
    private static readonly FrozenSet<string> ComparisonDunders = new HashSet<string>(StringComparer.Ordinal)
    {
        DunderNames.Eq, DunderNames.Ne, DunderNames.Lt, DunderNames.Le, DunderNames.Gt, DunderNames.Ge
    }.ToFrozenSet(StringComparer.Ordinal);

    private void ProcessMethodDef(TypeSymbol typeSymbol, FunctionDef funcDef)
    {
        var methodSymbol = typeSymbol.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
        if (methodSymbol != null)
        {
            SetCodeGenInfo(methodSymbol, new CodeGenInfo
            {
                CSharpName = DunderNameMapping.ResolveCSharpName(funcDef.Name)
                    ?? NameCasing.ResolveMethod(funcDef.Name, funcDef.IsNameBacktickEscaped),
                OriginalName = funcDef.Name,
                IsModuleLevel = false,
                StripsOverrideKeyword = ShouldStripOverrideKeyword(typeSymbol, funcDef.Name),
                ImplementsInterfaceMethod = ImplementsInterfaceMethod(typeSymbol, funcDef.Name),
                OperatorParameterShape = ComputeOperatorParameterShape(methodSymbol)
            });
        }
    }

    private static EqualityParameterShape? ComputeOperatorParameterShape(FunctionSymbol method)
    {
        if (!ComparisonDunders.Contains(method.Name))
            return null;

        var otherParam = method.Parameters
            .FirstOrDefault(p => p.Name != Shared.PythonNames.Self);
        if (otherParam?.Type == null || otherParam.Type is UnknownType)
            return null;

        if (otherParam.Type is UserDefinedType { Name: "object" })
            return null;

        return otherParam.Type.IsValueType
            ? EqualityParameterShape.ValueOrOptional
            : EqualityParameterShape.Reference;
    }

    private bool ShouldStripOverrideKeyword(TypeSymbol typeSymbol, string methodName)
    {
        var baseTypes = TypeHierarchyService.GetAllBaseTypes(typeSymbol, _semanticBinding);
        foreach (var baseType in baseTypes)
        {
            if (baseType.Methods.Any(m => m.Name == methodName && (m.IsVirtual || m.IsAbstract || m.IsOverride)))
                return false;
        }

        var interfaceRefs = _semanticBinding.GetInterfaces(typeSymbol)
            ?? (IReadOnlyList<InterfaceReference>)typeSymbol.Interfaces;
        foreach (var ifaceRef in interfaceRefs)
        {
            if (ifaceRef.Definition.Methods.Any(m => m.Name == methodName))
                return true;
        }

        return false;
    }

    private bool ImplementsInterfaceMethod(TypeSymbol typeSymbol, string methodName)
    {
        var interfaces = TypeHierarchyService.GetAllInterfaces(typeSymbol, _semanticBinding);
        foreach (var iface in interfaces)
        {
            if (iface.Methods.Any(m => m.Name == methodName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the dunder-synthesized interfaces off the MATERIALIZED supertype closure (#1746).
    /// </summary>
    /// <remarks>
    /// <para>This is a read, not a decision. <c>NameResolver</c> classified every row at
    /// inheritance resolution and enqueued it as a flagged <see cref="InterfaceReference"/>;
    /// materialization then applied its dedupe. So an explicit <c>ISized</c> base plus
    /// <c>__len__</c> leaves ONE entry — the explicit one, unflagged — and this read produces
    /// nothing, which is exactly why the emitted base list no longer carries <c>ISized</c> twice
    /// (CS0528). A recomputation here, however careful, is a second decider that disagrees with
    /// the closure the type checker and the SPY0607 gate already used.</para>
    /// <para>SPY1001 stays on this per-file path deliberately: the incremental cache serves a
    /// restored file's diagnostics from its per-file bag (#1553), so announcing synthesis here
    /// keeps the note cacheable. <c>CompilerAnalyzeTests</c> asserts the code and message only,
    /// never the phase.</para>
    /// </remarks>
    private void ComputeSynthesizedInterfaces(TypeSymbol typeSymbol, IReadOnlyList<Statement> body)
    {
        var result = SynthesizedInterfaceReader.Read(typeSymbol, SynthesizedArgumentResolver);
        if (result.Count == 0)
            return;

        // SPY1001 per-file emission — cacheable by the incremental cache (#1553).
        var dunderFuncs = new Dictionary<string, FunctionDef>();
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef funcDef && DunderDetector.IsDunderMethod(funcDef.Name)
                && !dunderFuncs.ContainsKey(funcDef.Name))
                dunderFuncs[funcDef.Name] = funcDef;
        }

        foreach (var info in result)
        {
            var displayName = info.TypeArgs.Length > 0
                ? $"{info.InterfaceName}<{string.Join(", ", info.TypeArgs.Select(t => t.GetDisplayName()))}>"
                : info.InterfaceName;
            var qualifiedName = info.Namespace.Length > 0
                ? $"{info.Namespace}.{displayName}"
                : displayName;

            dunderFuncs.TryGetValue(info.TriggeringDunder, out var triggeringFunc);
            _diagnostics.AddInfo(
                $"Type '{typeSymbol.Name}' implicitly implements '{qualifiedName}' via '{info.TriggeringDunder}'.",
                triggeringFunc?.LineStart ?? 0,
                triggeringFunc?.ColumnStart ?? 0,
                _sourceFilePath,
                code: DiagnosticCodes.Info.ImplicitInterfaceSynthesis,
                phase: CompilerPhase.TypeChecking);
        }

        _semanticBinding.SetSynthesizedInterfaces(typeSymbol, result);

    }


    /// <summary>
    /// The one C# name of a function. A non-entry module's top-level entry-shaped <c>main</c>
    /// (<see cref="ModuleIdentifiers.IsEntryMain"/> — an escaped <c>`main`</c> is verbatim) is
    /// <c>MainFunc</c>: a static <c>Main</c> there would be a second entry-point candidate. The
    /// emitter used to apply this rename at the declaration only, while every reference read
    /// <c>Main</c> from here (CS0117 behind SPY0908, #2065).
    /// </summary>
    private string ModuleLevelFunctionCSharpName(FunctionDef funcDef, bool isModuleLevel)
        => isModuleLevel && !_isEntryPoint && ModuleIdentifiers.IsEntryMain(funcDef)
            ? "MainFunc"
            : DunderNameMapping.ResolveCSharpName(funcDef.Name)
                ?? NameCasing.ResolveMethod(funcDef.Name, funcDef.IsNameBacktickEscaped);

    private void ProcessFunctionDef(FunctionDef funcDef, bool isModuleLevel)
    {
        // For overloaded module-level functions, the symbol table only holds the
        // first overload, so resolve the specific symbol by declaration line.
        var overloads = _symbolTable.LookupFunctionOverloads(funcDef.Name);
        var funcSymbol = overloads is { Count: > 1 }
            ? overloads.FirstOrDefault(o => o.DeclarationLine == funcDef.LineStart)
            : _symbolTable.Lookup(funcDef.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            SetCodeGenInfo(funcSymbol, new CodeGenInfo
            {
                CSharpName = ModuleLevelFunctionCSharpName(funcDef, isModuleLevel),
                OriginalName = funcDef.Name,
                IsModuleLevel = isModuleLevel
            });
        }
    }

    /// <summary>
    /// A protocol whose presence makes the emitter synthesize C# members the user did not write, and
    /// whose names are therefore occupied.
    /// </summary>
    /// <param name="Triggers">The dunders whose presence turns the protocol on. Any one suffices.</param>
    /// <param name="ReservedNames">The C# names the synthesis occupies.</param>
    /// <param name="Description">Named in the diagnostic, as "a synthesized {Description} member".</param>
    private readonly record struct SynthesizedProtocolSurface(
        string[] Triggers, string[] ReservedNames, string Description);

    /// <summary>
    /// The C# member names the emitter synthesizes per protocol. A user member that mangles to one
    /// of them collides in the emitted C#, and this walk is the only thing standing between that and
    /// CS0102 behind SPY0908.
    ///
    /// <para><b>Why a protocol needs a row here at all.</b> Most synthesized members are already
    /// covered by the ordinary collision walk, because the DUNDER ITSELF mangles to the emitted name
    /// and so appears in <see cref="EnumerateMemberNames"/> alongside the user's member:
    /// <c>__len__</c>→<c>Count</c>, <c>__str__</c>→<c>ToString</c>,
    /// <c>__hash__</c>→<c>GetHashCode</c>, <c>__reversed__</c>→<c>GetReverseEnumerator</c> all
    /// report SPY0522 with no help from this table (verified by measurement, not assumed). A row is
    /// needed exactly when the emitted name is NOT the dunder's mangled name, so the walk is looking
    /// at a different string than the emitter writes.</para>
    ///
    /// <para>Three such protocols exist and all three are listed. The iterator row shipped first.
    /// The other two were found by auditing the surfaces against the emitter rather than against
    /// the issue: an unattributed C# indexer takes the implicit metadata name <c>Item</c> while this
    /// walk sees <c>GetItem</c>/<c>SetItem</c> (#1499), and <c>IBoolConvertible</c> synthesizes an
    /// <c>IsTrue</c> property while the walk sees <c>Bool</c> — a member spelled <c>is_true</c>
    /// produced the same CS0102, and had no issue of its own.</para>
    /// </summary>
    private static readonly SynthesizedProtocolSurface[] SynthesizedProtocolSurfaces =
    {
        new(new[] { DunderNames.Next },
            new[] { "Current", "MoveNext", "Reset", "Dispose", "GetEnumerator" },
            "iterator protocol"),
        new(new[] { DunderNames.GetItem, DunderNames.SetItem },
            new[] { "Item" },
            "indexer protocol"),
        new(new[] { DunderNames.Bool },
            new[] { "IsTrue" },
            "bool protocol"),
    };

    /// <summary>
    /// The <c>(line N)</c> the collision messages append for the FIRST of the two declarations.
    ///
    /// <para>Kept alongside the structured related location rather than replaced by it: an editor
    /// reads <see cref="CompilerDiagnostic.RelatedLocations"/>, but <c>DiagnosticRenderer</c> and
    /// the <c>.error</c> fixture sidecars have no channel for a second position, and dropping the
    /// prose would lose it for them entirely (#1388).</para>
    /// </summary>
    private static string FirstDeclarationProse(DeclarationPosition? position)
        => position.HasValue ? $" (line {position.Value.Line})" : "";

    /// <summary>
    /// The structured form of the same fact — what an editor turns into a clickable
    /// "first declared here" beside the primary squiggle.
    /// </summary>
    private IReadOnlyList<DiagnosticRelatedLocation>? FirstDeclarationRelatedLocations(
        string originalName, DeclarationPosition? position)
        => position.HasValue
            ? new[]
            {
                new DiagnosticRelatedLocation(
                    $"'{originalName}' is first declared here",
                    position.Value.Line,
                    position.Value.Column,
                    _sourceFilePath)
            }
            : null;

    /// <summary>
    /// Every member a type contributes to the generated C# type's single member namespace, paired
    /// with the C# name it compiles to. One enumeration rather than a walk per kind: a member kind
    /// missing here is a CS0102 the user only ever sees as SPY0908, which is exactly how properties,
    /// events and nested types escaped the check (#1385).
    ///
    /// <para>Two sources, because neither alone is complete. Fields and methods come from the
    /// symbol table — the only source that also holds implicitly declared fields (<c>self.x = ...</c>
    /// in <c>__init__</c>, dataclass fields), which have no statement in <paramref name="body"/>.
    /// Properties, events and nested types come from the AST: <see cref="PropertySymbol"/> and
    /// <see cref="EventSymbol"/> are standalone records carrying neither an escape flag nor a
    /// <c>CodeGenInfo</c>, and emission derives their C# names from the AST node itself
    /// (<c>NameCasing.ResolveMethod(def.Name, def.IsNameBacktickEscaped)</c> — see
    /// <c>RoslynEmitter.ClassMembers.Properties.cs</c> and <c>.Events.cs</c>), so the check reads
    /// the same source it has to agree with.</para>
    ///
    /// <para>The same Sharpy name may be yielded more than once — method overloads, a split
    /// <c>property get</c>/<c>property set</c> pair, an <c>event add</c>/<c>event remove</c> pair —
    /// because those intentionally compile to one C# member. Callers dedupe on the original name.</para>
    /// </summary>
    private IEnumerable<(string OriginalName, string CSharpName)> EnumerateMemberNames(
        TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        foreach (var symbol in typeSymbol.Fields.Cast<Symbol>().Concat(typeSymbol.Methods))
        {
            var info = _semanticBinding.GetCodeGenInfo(symbol);
            if (info != null)
                yield return (symbol.Name, info.CSharpName);
        }

        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case PropertyDef propDef:
                    yield return (propDef.Name,
                        NameCasing.ResolveMethod(propDef.Name, propDef.IsNameBacktickEscaped));
                    break;
                case EventDef eventDef:
                    yield return (eventDef.Name,
                        NameCasing.ResolveMethod(eventDef.Name, eventDef.IsNameBacktickEscaped));
                    break;
                // Nested types occupy the same member namespace as fields and methods. A nested
                // union emits an abstract base class member and a nested delegate a C# delegate
                // member, so both take a member slot (#1729, R-I). TypeAlias is deliberately absent:
                // it is compile-time only and emits nothing.
                case ClassDef nestedClass:
                    yield return (nestedClass.Name,
                        NameCasing.ResolveType(nestedClass.Name, nestedClass.IsNameBacktickEscaped));
                    break;
                case StructDef nestedStruct:
                    yield return (nestedStruct.Name,
                        NameCasing.ResolveType(nestedStruct.Name, nestedStruct.IsNameBacktickEscaped));
                    break;
                case EnumDef nestedEnum:
                    yield return (nestedEnum.Name,
                        NameCasing.ResolveType(nestedEnum.Name, nestedEnum.IsNameBacktickEscaped));
                    break;
                case InterfaceDef nestedInterface:
                    yield return (nestedInterface.Name,
                        NameCasing.ResolveInterface(nestedInterface.Name, nestedInterface.IsNameBacktickEscaped));
                    break;
                case UnionDef nestedUnion:
                    yield return (nestedUnion.Name,
                        NameCasing.ResolveType(nestedUnion.Name, nestedUnion.IsNameBacktickEscaped));
                    break;
                case DelegateDef nestedDelegate:
                    yield return (nestedDelegate.Name,
                        NameCasing.ResolveType(nestedDelegate.Name, nestedDelegate.IsNameBacktickEscaped));
                    break;
            }
        }
    }

    /// <summary>
    /// Detects name collisions among the members of a type after mangling — fields, methods,
    /// properties, events and nested types, plus the type's own type parameters — and, for a class
    /// or struct (<paramref name="enclosingCSharpName"/> non-null), a member whose emitted name
    /// equals the type's own (SPY0525, #1871).
    /// </summary>
    private void DetectMemberCollisions(
        TypeSymbol typeSymbol, IEnumerable<Statement> body, string? enclosingCSharpName)
    {
        // CSharpName → (originalName, where it was first declared)
        var seen = new Dictionary<string, (string originalName, DeclarationPosition? position)>();
        var reportedEnclosing = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tp in typeSymbol.TypeParameters)
        {
            // Key on the name the emitter writes, not the source spelling: type parameters go out
            // through CSharpKeywords.EscapeIfNeeded (`class` → `@class`), which is the same form a
            // member's CSharpName takes, so a raw key could not match one.
            seen[CSharpKeywords.EscapeIfNeeded(tp.Name)] =
                (tp.Name, DeclarationPosition.From(tp.LineStart, tp.ColumnStart));
        }

        foreach (var (originalName, csharpName) in EnumerateMemberNames(typeSymbol, body))
        {
            // __format__ compiles to IFormattable's TWO-argument ToString, a C# overload beside the
            // parameterless ToString of __str__/__repr__ — not a same-name collision. Its own
            // signature-aware check is DetectFormatMemberCollisions (#2009).
            if (originalName == DunderNames.Format)
                continue;

            var position = FindMemberPosition(body, originalName);

            // Checked BEFORE the same-name dedupe below: a member spelled exactly like its type
            // (`class Q: Q: int`) has the type's original name too, and must not be read as a
            // redeclaration of it.
            if (csharpName == enclosingCSharpName)
            {
                if (reportedEnclosing.Add(originalName))
                    ReportMemberEnclosingTypeCollision(
                        originalName, csharpName, typeSymbol.Name, position, EnclosingCollisionMemberKind.None);
                continue;
            }

            if (seen.TryGetValue(csharpName, out var existing))
            {
                // One Sharpy name declared more than once — overloads, a split get/set property,
                // an add/remove event pair — intentionally compiles to a single C# member.
                // Mirrors the module-level arm's overload guard.
                if (string.Equals(originalName, existing.originalName, StringComparison.Ordinal))
                    continue;

                _diagnostics.AddErrorWithRelatedLocations(
                    $"Name collision: '{originalName}' and '{existing.originalName}'" +
                    $"{FirstDeclarationProse(existing.position)} both compile to " +
                    $"'{csharpName}'. Rename one, or backtick-escape the name you need to keep.",
                    FirstDeclarationRelatedLocations(existing.originalName, existing.position),
                    line: position?.Line,
                    column: position?.Column,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[csharpName] = (originalName, position);
            }
        }

        // Check for collisions with members the emitter synthesizes per protocol.
        var declaredDunders = new HashSet<string>(
            typeSymbol.Methods.Select(m => m.Name), StringComparer.Ordinal);

        if (declaredDunders.Contains(DunderNames.Format))
            DetectFormatMemberCollisions(typeSymbol, body);

        foreach (var surface in SynthesizedProtocolSurfaces)
        {
            if (!surface.Triggers.Any(declaredDunders.Contains))
                continue;

            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (originalName, csharpName) in EnumerateMemberNames(typeSymbol, body))
            {
                // A dunder that PARTICIPATES in the protocol is not colliding with it — it is what
                // turned the synthesis on. `__iter__` rides with `__next__` for the same reason.
                if (surface.Triggers.Contains(originalName) || originalName == DunderNames.Iter)
                    continue;

                if (surface.ReservedNames.Contains(csharpName, StringComparer.Ordinal)
                    && reported.Add(originalName))
                {
                    var position = FindMemberPosition(body, originalName);
                    _diagnostics.AddError(
                        $"Name collision: '{originalName}' compiles to '{csharpName}', " +
                        $"which conflicts with a synthesized {surface.Description} member. " +
                        $"Rename the member to avoid the collision.",
                        line: position?.Line,
                        column: position?.Column,
                        code: DiagnosticCodes.CodeGen.MemberNameCollision,
                        phase: CompilerPhase.CodeGeneration);
                }
            }
        }
    }

    /// <summary>
    /// <c>__format__</c> emits <c>ToString(string?, IFormatProvider?)</c> (#2009). Another member that
    /// compiles to that same C# member is SPY0522, not CS0111/CS0102 behind SPY0908: a method named
    /// like <c>ToString</c> taking two arguments — the explicit <c>class F(IFormattable)</c>
    /// <c>to_string(fmt, provider)</c> spelling of the same protocol — or any non-method member
    /// named <c>ToString</c>. A <c>ToString</c> of another arity (<c>__str__</c>) is an overload.
    /// </summary>
    private void DetectFormatMemberCollisions(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        const string csharpName = "ToString";
        var methodArity = typeSymbol.Methods
            .Where(m => m.Name != DunderNames.Format)
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => g.Select(m => m.Parameters.Count(p => p.Name != PythonNames.Self)).ToHashSet(),
                StringComparer.Ordinal);

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (originalName, memberCSharpName) in EnumerateMemberNames(typeSymbol, body))
        {
            if (originalName == DunderNames.Format || memberCSharpName != csharpName)
                continue;

            var collides = !methodArity.TryGetValue(originalName, out var arities) || arities.Contains(2);
            if (!collides || !reported.Add(originalName))
                continue;

            var position = FindMemberPosition(body, originalName);
            _diagnostics.AddError(
                $"Name collision: '{originalName}' compiles to '{csharpName}', which conflicts with the " +
                $"System.IFormattable member '{DunderNames.Format}' synthesizes " +
                "(ToString(string? format, IFormatProvider? formatProvider)). Keep one spelling of the " +
                "format protocol: remove the member, or remove __format__.",
                line: position?.Line,
                column: position?.Column,
                code: DiagnosticCodes.CodeGen.MemberNameCollision,
                phase: CompilerPhase.CodeGeneration);
        }
    }

    /// <summary>
    /// SPY0525 (#1871, R-AW): a member whose emitted C# name equals its enclosing class's or
    /// struct's is CS0542, whatever the member kind — refused here by name rather than leaking the
    /// C# error as SPY0908. Rung 4 by necessity: no CLR surface spells "collides after PascalCasing".
    /// The steer is the corrected R-AW wording: an escaped DECLARATION alone is not enough, because
    /// every unescaped use still spells the PascalCased name (the closed #478 contract — access sites
    /// read their own escape flag; #2046 tracks it as the outlier); a union case and a union case field
    /// have no escape hatch at all (their declarations are emitted without consulting the escape
    /// flag). A string-enum member's escaped DECLARATION is enough (#2037, ruling 3: the declaration
    /// governs every reference), so its steer offers the escape — unless the member is already
    /// spelled like the enum, when only a rename helps.
    /// </summary>
    private void ReportMemberEnclosingTypeCollision(
        string originalName,
        string csharpName,
        string enclosingOriginalName,
        DeclarationPosition? position,
        EnclosingCollisionMemberKind memberKind)
    {
        var (noun, steer) = memberKind switch
        {
            EnclosingCollisionMemberKind.EnumMember => ("Enum member",
                NameCasing.ResolveEnumMember(originalName, isStringEnum: true, isBacktickEscaped: true) == csharpName
                    ? "Rename the member."
                    : $"Rename the member, or backtick-escape its declaration (`{originalName}`) to keep the Python spelling — every use follows the declaration."),
            EnclosingCollisionMemberKind.Case => ("Union case", "Rename the case (a union case name cannot be backtick-escaped)."),
            EnclosingCollisionMemberKind.CaseField => ("Union case field", "Rename the field (a union case field cannot be backtick-escaped)."),
            _ => ("Member", $"Rename the member, or backtick-escape the declaration AND every use (`{originalName}`, " +
                            $"v.`{originalName}`) to keep the Python spelling."),
        };
        _diagnostics.AddError(
            $"{noun} '{originalName}' would be emitted as '{csharpName}', the same name as its enclosing " +
            $"type '{enclosingOriginalName}' (C# forbids this, CS0542). {steer}",
            line: position?.Line,
            column: position?.Column,
            code: DiagnosticCodes.CodeGen.MemberEnclosingTypeCollision,
            phase: CompilerPhase.CodeGeneration);
    }

    /// <summary>
    /// Which kind of member a SPY0525 names when it is not an ordinary class/struct member — a union
    /// case, a union case field, or a string-enum member; none of the three has an escape hatch.
    /// </summary>
    private enum EnclosingCollisionMemberKind { None, Case, CaseField, EnumMember }

    /// <summary>
    /// SPY0525 in a NESTED host (#1871): the module-level walk (<see cref="DetectMemberCollisions"/>)
    /// never visits a nested type's own members, so `class Outer: class Inner: inner: int` was CS0542
    /// behind SPY0908. A nested class/struct checks its members against its own emitted name; a
    /// nested union its cases and case fields.
    /// </summary>
    private void DetectNestedEnclosingTypeCollisions(TypeSymbol enclosing, Statement stmt, NestedDeclaration nested)
    {
        string? hostName = stmt switch
        {
            ClassDef c => NameCasing.ResolveType(c.Name, c.IsNameBacktickEscaped),
            StructDef st => NameCasing.ResolveType(st.Name, st.IsNameBacktickEscaped),
            _ => null
        };
        if (stmt is UnionDef nestedUnion)
            DetectUnionEnclosingTypeCollisions(nestedUnion);
        if (hostName == null)
            return;

        var nestedSymbol = enclosing.NestedTypes.FirstOrDefault(t => t.Name == nested.Name);
        if (nestedSymbol == null)
            return;

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (originalName, csharpName) in EnumerateMemberNames(nestedSymbol, nested.Body))
        {
            if (csharpName == hostName && reported.Add(originalName))
            {
                ReportMemberEnclosingTypeCollision(
                    originalName, csharpName, nested.Name,
                    FindMemberPosition(nested.Body, originalName), EnclosingCollisionMemberKind.None);
            }
        }
    }

    /// <summary>
    /// The union host of SPY0525 (#1871): a union emits an abstract base class holding one sealed
    /// case class per case, and each case's fields are properties of its case class — so a case
    /// named like its union, or a field named like its case, is CS0542. Spelled exactly as
    /// <c>GenerateUnionDeclaration</c>/<c>GenerateUnionCaseClass</c> spell them (the union through
    /// <c>NameCasing.ResolveType</c>, a case through <c>NameMangler.Transform(Type)</c>, a field
    /// through <c>NameCasing.ResolveField</c>, which ignore the escape flag for cases and fields).
    /// </summary>
    private void DetectUnionEnclosingTypeCollisions(UnionDef unionDef)
    {
        var unionName = NameCasing.ResolveType(unionDef.Name, unionDef.IsNameBacktickEscaped);

        // The union's OWN members (audit R4): the body is emitted into the abstract base class by
        // GenerateClassMembers, so `union Shape: … def shape(self)` is CS0542 like any class member.
        var reportedOwn = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (originalName, csharpName) in UnionBodyMemberNames(unionDef.Body))
        {
            if (csharpName == unionName && reportedOwn.Add(originalName))
            {
                ReportMemberEnclosingTypeCollision(
                    originalName, csharpName, unionDef.Name,
                    FindMemberPosition(unionDef.Body, originalName), EnclosingCollisionMemberKind.None);
            }
        }
        foreach (var caseDef in unionDef.Cases)
        {
            var caseName = NameMangler.Transform(caseDef.Name, NameContext.Type);
            // A case SPELLED like its union (`union Opt: case Opt(v: int)`) is SPY0368
            // (UnionCaseNameConflict, TypeChecker.Definitions) — one defect, one diagnostic, so this
            // arm owns only the case that collides AFTER mangling (`union Q: case q()`).
            if (caseName == unionName && !string.Equals(caseDef.Name, unionDef.Name, StringComparison.Ordinal))
            {
                ReportMemberEnclosingTypeCollision(
                    caseDef.Name, caseName, unionDef.Name,
                    DeclarationPosition.From(caseDef.NameLineStart, caseDef.NameColumnStart, caseDef.LineStart, caseDef.ColumnStart),
                    EnclosingCollisionMemberKind.Case);
            }

            foreach (var field in caseDef.Fields)
            {
                var fieldName = NameCasing.ResolveField(field.Name, false);
                if (fieldName == caseName)
                {
                    ReportMemberEnclosingTypeCollision(
                        field.Name, fieldName, caseDef.Name,
                        DeclarationPosition.From(field.LineStart, field.ColumnStart, field.LineStart, field.ColumnStart),
                        EnclosingCollisionMemberKind.CaseField);
                }
            }
        }
    }

    /// <summary>
    /// The enum host of SPY0525 (#1871): a STRING enum lowers to a sealed class
    /// (<c>GenerateStringEnumClass</c>) holding one singleton field per member plus the synthesized
    /// <see cref="StringEnumShape.SynthesizedMembers"/>, so a member whose field is named like the
    /// enum (<c>enum Color: Color = "c"</c>) — or an enum named like a synthesized member
    /// (<c>enum Value</c>) — is CS0542. Both names come from <see cref="StringEnumShape"/>, the
    /// authority the emitter declares them through; the enum itself goes through
    /// <c>NameCasing.ResolveType</c> as there. An int-backed enum is a real C# <c>enum</c>, whose
    /// member MAY share its name (<c>enum Color { Color }</c> compiles and runs, measured), so it is
    /// not walked. Called for top-level and nested enums alike.
    /// </summary>
    private void DetectEnumEnclosingTypeCollisions(EnumDef enumDef)
    {
        if (!NameResolver.IsStringEnum(enumDef))
            return;

        var className = NameCasing.ResolveType(enumDef.Name, enumDef.IsNameBacktickEscaped);

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in enumDef.Members)
        {
            var fieldName = NameCasing.ResolveEnumMember(member.Name, isStringEnum: true, member.IsNameBacktickEscaped);
            if (fieldName == className && reported.Add(member.Name))
            {
                ReportMemberEnclosingTypeCollision(
                    member.Name, fieldName, enumDef.Name,
                    DeclarationPosition.From(member.LineStart, member.ColumnStart), EnclosingCollisionMemberKind.EnumMember);
            }
        }

        if (StringEnumShape.SynthesizedMembers.Contains(className, StringComparer.Ordinal))
        {
            var position = DeclarationPosition.From(
                enumDef.NameLineStart, enumDef.NameColumnStart, enumDef.LineStart, enumDef.ColumnStart);
            _diagnostics.AddError(
                $"String enum '{enumDef.Name}' would be emitted as the class '{className}', which declares the " +
                $"synthesized member '{className}' — the same name as its enclosing type (C# forbids this, " +
                $"CS0542). Rename the enum (a string enum's class synthesizes the members " +
                $"{string.Join(", ", StringEnumShape.SynthesizedMembers)}).",
                line: position?.Line,
                column: position?.Column,
                code: DiagnosticCodes.CodeGen.MemberEnclosingTypeCollision,
                phase: CompilerPhase.CodeGeneration);
        }
    }

    /// <summary>
    /// The emitted names of a union body's members, spelled as the emitter spells them for a type
    /// whose members carry no <c>CodeGenInfo</c> (a module-level union's body is emitted through
    /// <c>GenerateClassMembers</c> from the AST): a method through <c>DunderNameMapping</c> else
    /// <c>ResolveMethod</c>, a field/const through <c>ResolveField</c>, a property/event through
    /// <c>ResolveMethod</c>, a nested type through <c>ResolveType</c>/<c>ResolveInterface</c>.
    /// </summary>
    private static IEnumerable<(string OriginalName, string CSharpName)> UnionBodyMemberNames(
        IEnumerable<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case FunctionDef f:
                    yield return (f.Name, DunderNameMapping.ResolveCSharpName(f.Name)
                        ?? NameCasing.ResolveMethod(f.Name, f.IsNameBacktickEscaped));
                    break;
                case VariableDeclaration v:
                    yield return (v.Name, NameCasing.ResolveField(v.Name, v.IsNameBacktickEscaped));
                    break;
                case PropertyDef p:
                    yield return (p.Name, NameCasing.ResolveMethod(p.Name, p.IsNameBacktickEscaped));
                    break;
                case EventDef e:
                    yield return (e.Name, NameCasing.ResolveMethod(e.Name, e.IsNameBacktickEscaped));
                    break;
                case ClassDef c:
                    yield return (c.Name, NameCasing.ResolveType(c.Name, c.IsNameBacktickEscaped));
                    break;
                case StructDef st:
                    yield return (st.Name, NameCasing.ResolveType(st.Name, st.IsNameBacktickEscaped));
                    break;
                case EnumDef en:
                    yield return (en.Name, NameCasing.ResolveType(en.Name, en.IsNameBacktickEscaped));
                    break;
                case InterfaceDef i:
                    yield return (i.Name, NameCasing.ResolveInterface(i.Name, i.IsNameBacktickEscaped));
                    break;
            }
        }
    }

    /// <summary>
    /// Detects name collisions among an enum's members after mangling. Enums have their own walk
    /// because their members are not symbols and their naming rule is neither a field's nor a
    /// method's (#1385). Called for top-level and nested enums alike (#2036). A STRING enum's
    /// class also declares the members its lowering synthesizes (<see cref="StringEnumShape.SynthesizedMembers"/>),
    /// so a member that compiles to one of those names collides with it (CS0102 before #2036).
    /// </summary>
    private void DetectEnumMemberCollisions(EnumDef enumDef, bool isStringEnum)
    {
        // CSharpName → (originalName, where it was first declared)
        var seen = new Dictionary<string, (string originalName, DeclarationPosition? position)>();

        foreach (var member in enumDef.Members)
        {
            var csharpName = NameCasing.ResolveEnumMember(member.Name, isStringEnum, member.IsNameBacktickEscaped);
            var position = DeclarationPosition.From(member.LineStart, member.ColumnStart);

            if (isStringEnum && StringEnumShape.SynthesizedMembers.Contains(csharpName, StringComparer.Ordinal))
            {
                var escapeHelps = NameCasing.ResolveEnumMember(member.Name, isStringEnum: true, isBacktickEscaped: true) != csharpName;
                _diagnostics.AddError(
                    $"Name collision: enum member '{member.Name}' compiles to '{csharpName}', which conflicts " +
                    $"with the member its string enum's class synthesizes. " +
                    (escapeHelps
                        ? $"Rename the member, or backtick-escape its declaration (`{member.Name}`) to keep the Python spelling."
                        : "Rename the member."),
                    line: position?.Line,
                    column: position?.Column,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
                continue;
            }

            if (seen.TryGetValue(csharpName, out var existing))
            {
                // A member spelled identically twice is a duplicate declaration, not a mangling
                // collision — leave that to the declaration checks so it is reported once.
                if (string.Equals(member.Name, existing.originalName, StringComparison.Ordinal))
                    continue;

                // The escape is the other fix: an escaped member compiles verbatim, and every
                // reference follows the declaration (#2037).
                _diagnostics.AddErrorWithRelatedLocations(
                    $"Name collision: enum members '{member.Name}' and '{existing.originalName}'" +
                    $"{FirstDeclarationProse(existing.position)} both compile to '{csharpName}'. Rename one.",
                    FirstDeclarationRelatedLocations(existing.originalName, existing.position),
                    line: position?.Line,
                    column: position?.Column,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[csharpName] = (member.Name, position);
            }
        }
    }

    /// <summary>
    /// Detects name collisions among module-level symbols (functions, variables, and types).
    /// All of these become members of the same module class in generated C#. Under the recorded
    /// module-as-namespace layout (#2039, Decision 28 (h)) the module namespace is also seeded with
    /// <c>&lt;X&gt;</c> (a declaration spelled like it is SPY0523, ruling 15), the test class
    /// <c>&lt;X&gt;Tests</c> and the fixture classes (a type spelled like one is SPY0522).
    /// </summary>
    private void DetectModuleLevelCollisions(Module module, ModuleLayout? layout)
    {
        if (layout != null)
            DetectLayoutSeedCollisions(module, layout);

        // The emitter's own authority with the emitter's own entry bit (#2013): a main.spy without an
        // entry-point main() emits class Main, not Program.
        var moduleClassName = string.IsNullOrEmpty(_sourceFilePath)
            ? null
            : ModuleIdentifiers.ModuleClassName(_sourceFilePath, ModuleIdentifiers.DeclaresEntryMain(module.Body));

        // Check each function/variable against the module class name
        if (moduleClassName != null)
        {
            foreach (var stmt in module.Body)
            {
                string? symbolName = null;
                DeclarationPosition? stmtPos = null;

                switch (stmt)
                {
                    case FunctionDef funcDef:
                        symbolName = funcDef.Name;
                        stmtPos = DeclarationPosition.From(
                            funcDef.NameLineStart, funcDef.NameColumnStart, funcDef.LineStart, funcDef.ColumnStart);
                        break;
                    case VariableDeclaration varDecl:
                        symbolName = varDecl.Name;
                        stmtPos = DeclarationPosition.From(
                            varDecl.NameLineStart, varDecl.NameColumnStart, varDecl.LineStart, varDecl.ColumnStart);
                        break;
                }

                if (symbolName == null)
                    continue;

                var symbol = _symbolTable.Lookup(symbolName);
                if (symbol == null)
                    continue;

                var info = _semanticBinding.GetCodeGenInfo(symbol);
                if (info == null)
                    continue;

                if (info.CSharpName == moduleClassName)
                {
                    _diagnostics.AddError(
                        $"Function '{symbolName}' compiles to '{info.CSharpName}', which conflicts " +
                        $"with the module class name derived from the filename. Rename the function " +
                        $"to avoid the collision.",
                        line: stmtPos?.Line,
                        column: stmtPos?.Column,
                        code: DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
                        phase: CompilerPhase.CodeGeneration);
                }
            }
        }

        // CSharpName → (originalName, where it was first declared)
        var seen = new Dictionary<string, (string originalName, DeclarationPosition? position)>();

        foreach (var stmt in module.Body)
        {
            string? symbolName = null;
            DeclarationPosition? stmtPos = null;

            switch (stmt)
            {
                case FunctionDef funcDef:
                    symbolName = funcDef.Name;
                    stmtPos = DeclarationPosition.From(
                        funcDef.NameLineStart, funcDef.NameColumnStart, funcDef.LineStart, funcDef.ColumnStart);
                    break;
                case VariableDeclaration varDecl:
                    symbolName = varDecl.Name;
                    stmtPos = DeclarationPosition.From(
                        varDecl.NameLineStart, varDecl.NameColumnStart, varDecl.LineStart, varDecl.ColumnStart);
                    break;
                case ClassDef classDef:
                    symbolName = classDef.Name;
                    stmtPos = DeclarationPosition.From(
                        classDef.NameLineStart, classDef.NameColumnStart, classDef.LineStart, classDef.ColumnStart);
                    break;
                case StructDef structDef:
                    symbolName = structDef.Name;
                    stmtPos = DeclarationPosition.From(
                        structDef.NameLineStart, structDef.NameColumnStart, structDef.LineStart, structDef.ColumnStart);
                    break;
                case InterfaceDef interfaceDef:
                    symbolName = interfaceDef.Name;
                    stmtPos = DeclarationPosition.From(
                        interfaceDef.NameLineStart, interfaceDef.NameColumnStart,
                        interfaceDef.LineStart, interfaceDef.ColumnStart);
                    break;
                case EnumDef enumDef:
                    symbolName = enumDef.Name;
                    stmtPos = DeclarationPosition.From(
                        enumDef.NameLineStart, enumDef.NameColumnStart, enumDef.LineStart, enumDef.ColumnStart);
                    break;
                case DelegateDef delegateDef:
                    symbolName = delegateDef.Name;
                    stmtPos = DeclarationPosition.From(
                        delegateDef.NameLineStart, delegateDef.NameColumnStart,
                        delegateDef.LineStart, delegateDef.ColumnStart);
                    break;
            }

            if (symbolName == null)
                continue;

            var symbol = _symbolTable.Lookup(symbolName);
            if (symbol == null)
                continue;

            var info = _semanticBinding.GetCodeGenInfo(symbol);
            var csharpName = info?.CSharpName;
            if (csharpName == null && symbol is TypeSymbol { TypeKind: TypeKind.Delegate })
                csharpName = NameCasing.ResolveType(symbolName, symbol.IsNameBacktickEscaped);
            if (csharpName == null)
                continue;

            if (seen.TryGetValue(csharpName, out var existing))
            {
                // Overloads of the same module-level function share an identical
                // Python name and intentionally compile to the same C# name — this
                // is not a collision.
                if (symbolName == existing.originalName)
                    continue;

                _diagnostics.AddErrorWithRelatedLocations(
                    $"Name collision: '{symbolName}' and '{existing.originalName}'" +
                    $"{FirstDeclarationProse(existing.position)} both compile to " +
                    $"'{csharpName}'. Rename one, or backtick-escape the name you need to keep.",
                    FirstDeclarationRelatedLocations(existing.originalName, existing.position),
                    line: stmtPos?.Line,
                    column: stmtPos?.Column,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[csharpName] = (symbolName, stmtPos);
            }
        }
    }

    /// <summary>
    /// The layout seeds of the module namespace (#2039, Decision 28 (h)): a top-level function,
    /// variable or type whose emitted name is <c>&lt;X&gt;</c> is SPY0523 (a member cannot share its
    /// class's name, a type cannot share the members class's); a top-level type spelled like the test
    /// class or a fixture class the module emits beside it is SPY0522. A declaration the legacy
    /// module-class check (below) already reports — a package's <c>__init__</c>, whose module class IS
    /// <c>&lt;X&gt;</c> — is not reported twice.
    /// </summary>
    private void DetectLayoutSeedCollisions(Module module, ModuleLayout layout)
    {
        var legacyModuleClass = string.IsNullOrEmpty(_sourceFilePath)
            ? null
            : ModuleIdentifiers.ModuleClassName(_sourceFilePath, ModuleIdentifiers.DeclaresEntryMain(module.Body));
        var classSeeds = new Dictionary<string, string>(StringComparer.Ordinal);
        if (layout.TestClassName != null)
            classSeeds[layout.TestClassName] = "the module's test class";
        foreach (var fixture in layout.FixtureClassNames ?? Array.Empty<string>())
            classSeeds.TryAdd(fixture, "a test fixture class");

        foreach (var stmt in module.Body)
        {
            var decl = stmt.UnwrapDecorated();
            var (name, isType, position) = decl switch
            {
                FunctionDef f => (f.Name, false, DeclarationPosition.From(f.NameLineStart, f.NameColumnStart, f.LineStart, f.ColumnStart)),
                VariableDeclaration v => (v.Name, false, DeclarationPosition.From(v.NameLineStart, v.NameColumnStart, v.LineStart, v.ColumnStart)),
                ClassDef c => (c.Name, true, DeclarationPosition.From(c.NameLineStart, c.NameColumnStart, c.LineStart, c.ColumnStart)),
                StructDef st => (st.Name, true, DeclarationPosition.From(st.NameLineStart, st.NameColumnStart, st.LineStart, st.ColumnStart)),
                InterfaceDef i => (i.Name, true, DeclarationPosition.From(i.NameLineStart, i.NameColumnStart, i.LineStart, i.ColumnStart)),
                EnumDef e => (e.Name, true, DeclarationPosition.From(e.NameLineStart, e.NameColumnStart, e.LineStart, e.ColumnStart)),
                UnionDef u => (u.Name, true, DeclarationPosition.From(u.NameLineStart, u.NameColumnStart, u.LineStart, u.ColumnStart)),
                DelegateDef d => (d.Name, true, DeclarationPosition.From(d.NameLineStart, d.NameColumnStart, d.LineStart, d.ColumnStart)),
                _ => (null, false, (DeclarationPosition?)null),
            };
            if (name == null || _symbolTable.Lookup(name) is not { } symbol)
                continue;

            var csharpName = _semanticBinding.GetCodeGenInfo(symbol)?.CSharpName
                ?? (isType ? NameCasing.ResolveType(name, symbol.IsNameBacktickEscaped) : null);
            if (csharpName == null)
                continue;

            if (csharpName == layout.MembersClassName)
            {
                // The legacy check reports a function/variable spelled like the module class; when
                // that class IS <X> (a package's __init__), one report is enough.
                if (!isType && csharpName == legacyModuleClass)
                    continue;
                _diagnostics.AddError(
                    $"'{name}' compiles to '{csharpName}', which is this module's members class — the class " +
                    $"its functions, variables and constants are emitted into ('{layout.MembersClassName}'). " +
                    "Rename it.",
                    line: position?.Line,
                    column: position?.Column,
                    code: DiagnosticCodes.CodeGen.FunctionModuleClassCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else if (isType && classSeeds.TryGetValue(csharpName, out var seed))
            {
                _diagnostics.AddError(
                    $"Name collision: '{name}' compiles to '{csharpName}', which is {seed} this module emits " +
                    "in the same namespace. Rename it.",
                    line: position?.Line,
                    column: position?.Column,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
        }
    }

    /// <summary>
    /// Finds where a member is declared in the type body. Covers every kind
    /// <see cref="EnumerateMemberNames"/> yields — a kind missing here reports the collision with
    /// no position at all.
    ///
    /// <para>The column comes from the NAME token, not the statement start, so the caret lands on
    /// the identifier the user has to rename rather than on the <c>def</c>/<c>property</c> keyword.
    /// Returning a line alone (which is all this did before #1388) forced every consumer to render
    /// column 0.</para>
    /// </summary>
    private static DeclarationPosition? FindMemberPosition(IEnumerable<Statement> body, string memberName)
    {
        foreach (var stmt in body)
        {
            switch (stmt.UnwrapDecorated())
            {
                case VariableDeclaration v when v.Name == memberName:
                    return DeclarationPosition.From(v.NameLineStart, v.NameColumnStart, v.LineStart, v.ColumnStart);
                case FunctionDef f when f.Name == memberName:
                    return DeclarationPosition.From(f.NameLineStart, f.NameColumnStart, f.LineStart, f.ColumnStart);
                case PropertyDef p when p.Name == memberName:
                    return DeclarationPosition.From(p.NameLineStart, p.NameColumnStart, p.LineStart, p.ColumnStart);
                case EventDef e when e.Name == memberName:
                    return DeclarationPosition.From(e.NameLineStart, e.NameColumnStart, e.LineStart, e.ColumnStart);
                case ClassDef c when c.Name == memberName:
                    return DeclarationPosition.From(c.NameLineStart, c.NameColumnStart, c.LineStart, c.ColumnStart);
                case StructDef s when s.Name == memberName:
                    return DeclarationPosition.From(s.NameLineStart, s.NameColumnStart, s.LineStart, s.ColumnStart);
                case EnumDef en when en.Name == memberName:
                    return DeclarationPosition.From(en.NameLineStart, en.NameColumnStart, en.LineStart, en.ColumnStart);
                case InterfaceDef i when i.Name == memberName:
                    return DeclarationPosition.From(i.NameLineStart, i.NameColumnStart, i.LineStart, i.ColumnStart);
            }
        }
        return null;
    }

    // Note: HasExecutionOrderIssues and ContainsRuntimeExpression methods were removed.
    // The ExecutionOrderAnalyzer class now handles execution order detection with
    // proper multi-pass analysis including assignment-before-declaration and
    // transitive dependency detection.
}
