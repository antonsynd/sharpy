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
    private readonly DiagnosticBag _diagnostics;
    private readonly HashSet<string> _processedModuleLevelVars = new();
    private HashSet<string> _variablesWithExecutionOrderIssues = new();

    public CodeGenInfoComputer(SymbolTable symbolTable, SemanticBinding? semanticBinding = null,
        DiagnosticBag? diagnostics = null)
    {
        _symbolTable = symbolTable;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
        _diagnostics = diagnostics ?? new DiagnosticBag();
    }

    /// <summary>
    /// Sets CodeGenInfo in SemanticBinding. Symbol properties are populated later
    /// by MaterializeCodeGenInfo() at the freeze point.
    /// </summary>
    private void SetCodeGenInfo(Symbol symbol, CodeGenInfo info)
    {
        _semanticBinding.SetCodeGenInfo(symbol, info);
    }

    /// <summary>
    /// Compute CodeGenInfo for all symbols in the module.
    /// </summary>
    public void ComputeForModule(Module module)
    {
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
                case FunctionDef funcDef:
                    ProcessFunctionDef(funcDef, isModuleLevel: true);
                    break;
            }
        }

        // Third pass: Detect module-level name collisions
        DetectModuleLevelCollisions(module);
    }

    private void ProcessModuleLevelDeclarations(Module module)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
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
            }
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
            // ALL_CAPS names (Python-style constants) use ToConstantCase (→ PascalCase)
            // Other module-level fields use ToPascalCase
            string csharpName;
            if (hasIssues)
                csharpName = NameMangler.ToCamelCase(varDecl.Name);
            else if (NameFormDetector.IsConstantCaseName(varDecl.Name))
                csharpName = NameMangler.ToConstantCase(varDecl.Name);
            else
                csharpName = NameMangler.ToPascalCase(varDecl.Name);

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
            SetCodeGenInfo(varSymbol, new CodeGenInfo
            {
                CSharpName = NameMangler.ToConstantCase(constDecl.Name),
                OriginalName = constDecl.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = true,
                HasExecutionOrderIssues = false // Constants are always compile-time
            });
        }
    }

    private void ProcessImport(ImportStatement import)
    {
        foreach (var alias in import.Names)
        {
            var effectiveName = alias.AsName ?? alias.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol is ModuleSymbol moduleSymbol)
            {
                SetCodeGenInfo(moduleSymbol, new CodeGenInfo
                {
                    CSharpName = effectiveName.Replace(".", "_", StringComparison.Ordinal),
                    OriginalName = effectiveName,
                    ImportKind = alias.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.ModuleImport
                });
            }
        }
    }

    private void ProcessFromImport(FromImportStatement fromImport)
    {
        var isNetModule = _semanticBinding.IsNetModule(fromImport.Module);

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
                    OriginalImportName = originalName
                });
            }
        }
    }

    private string DetermineCSharpNameForFromImport(string name, Symbol symbol, bool isNetModule)
    {
        // For .NET module variable fields (e.g., string.digits), the CLR field name
        // may differ from PascalCase convention. Sharpy.Core uses Python-style
        // snake_case names for string module constants. Use the CLR name directly.
        if (isNetModule && symbol is VariableSymbol)
        {
            return name;
        }

        // Use the same logic as RoslynEmitter for from-imports:
        // - ALL_CAPS names (constants) use ToConstantCase (→ PascalCase)
        // - Other names become PascalCase
        if (NameFormDetector.IsConstantCaseName(name))
        {
            return NameMangler.ToConstantCase(name);
        }
        return NameMangler.ToPascalCase(name);
    }

    private void ProcessClassDef(ClassDef classDef)
    {
        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(classDef.Name),
                OriginalName = classDef.Name
            });

            // Process class members
            ProcessTypeMembers(typeSymbol, classDef.Body);
            DetectMemberCollisions(typeSymbol, classDef.Body);
        }
    }

    private void ProcessStructDef(StructDef structDef)
    {
        var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(structDef.Name),
                OriginalName = structDef.Name
            });

            ProcessTypeMembers(typeSymbol, structDef.Body);
            DetectMemberCollisions(typeSymbol, structDef.Body);
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
                CSharpName = NameMangler.ToInterfaceName(interfaceDef.Name),
                OriginalName = interfaceDef.Name
            });

            ProcessTypeMembers(typeSymbol, interfaceDef.Body);
            DetectMemberCollisions(typeSymbol, interfaceDef.Body);
        }
    }

    private void ProcessEnumDef(EnumDef enumDef)
    {
        var typeSymbol = _symbolTable.Lookup(enumDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Determine if this is a string enum (has at least one string literal value)
            var isStringEnum = enumDef.Members.Any(m => m.Value is StringLiteral);

            SetCodeGenInfo(typeSymbol, new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(enumDef.Name),
                OriginalName = enumDef.Name,
                IsStringEnum = isStringEnum
            });

            // Enum members keep their exact names - no CodeGenInfo needed
            // as they are emitted as-is in C#
        }
    }

    private void ProcessTypeMembers(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case VariableDeclaration fieldDecl:
                    ProcessField(typeSymbol, fieldDecl);
                    break;
                case FunctionDef funcDef:
                    ProcessMethodDef(typeSymbol, funcDef);
                    break;
            }
        }
    }

    private void ProcessField(TypeSymbol typeSymbol, VariableDeclaration fieldDecl)
    {
        var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
        if (fieldSymbol != null)
        {
            SetCodeGenInfo(fieldSymbol, new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(fieldDecl.Name),
                OriginalName = fieldDecl.Name,
                IsModuleLevel = false,
                IsConstant = fieldDecl.IsConst
            });
        }
    }

    private void ProcessMethodDef(TypeSymbol typeSymbol, FunctionDef funcDef)
    {
        var methodSymbol = typeSymbol.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
        if (methodSymbol != null)
        {
            SetCodeGenInfo(methodSymbol, new CodeGenInfo
            {
                CSharpName = DunderNameMapping.ResolveCSharpName(funcDef.Name)
                    ?? NameMangler.ToPascalCase(funcDef.Name),
                OriginalName = funcDef.Name,
                IsModuleLevel = false
            });
        }
    }

    private void ProcessFunctionDef(FunctionDef funcDef, bool isModuleLevel)
    {
        var funcSymbol = _symbolTable.Lookup(funcDef.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            SetCodeGenInfo(funcSymbol, new CodeGenInfo
            {
                CSharpName = DunderNameMapping.ResolveCSharpName(funcDef.Name)
                    ?? NameMangler.ToPascalCase(funcDef.Name),
                OriginalName = funcDef.Name,
                IsModuleLevel = isModuleLevel
            });
        }
    }

    /// <summary>
    /// C# member names synthesized by the iterator protocol (__next__).
    /// User-defined members must not mangle to any of these names.
    /// </summary>
    private static readonly HashSet<string> IteratorProtocolReservedNames = new()
    {
        "Current", "MoveNext", "Reset", "Dispose", "GetEnumerator"
    };

    /// <summary>
    /// Detects name collisions among members of a type (fields + methods) after mangling.
    /// </summary>
    private void DetectMemberCollisions(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        var seen = new Dictionary<string, string>(); // CSharpName → originalName

        foreach (var symbol in typeSymbol.Fields.Cast<Symbol>().Concat(typeSymbol.Methods))
        {
            var info = _semanticBinding.GetCodeGenInfo(symbol);
            if (info == null)
                continue;

            if (seen.TryGetValue(info.CSharpName, out var existingOriginal))
            {
                // Find the AST node for the colliding symbol to get location info
                var line = FindMemberLine(body, symbol.Name);

                _diagnostics.AddError(
                    $"Name collision: '{symbol.Name}' and '{existingOriginal}' both compile to " +
                    $"'{info.CSharpName}'. Rename one of the conflicting symbols.",
                    line: line,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[info.CSharpName] = symbol.Name;
            }
        }

        // Check for collisions with synthesized iterator protocol members
        var hasNext = false;
        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name == DunderNames.Next)
            {
                hasNext = true;
                break;
            }
        }

        if (hasNext)
        {
            foreach (var symbol in typeSymbol.Fields.Cast<Symbol>().Concat(typeSymbol.Methods))
            {
                // Skip __next__ and __iter__ — they are part of the iterator protocol, not collisions
                if (symbol.Name == DunderNames.Next || symbol.Name == DunderNames.Iter)
                    continue;

                var info = _semanticBinding.GetCodeGenInfo(symbol);
                if (info == null)
                    continue;

                if (IteratorProtocolReservedNames.Contains(info.CSharpName))
                {
                    var line = FindMemberLine(body, symbol.Name);
                    _diagnostics.AddError(
                        $"Name collision: '{symbol.Name}' compiles to '{info.CSharpName}', " +
                        $"which conflicts with a synthesized iterator protocol member. " +
                        $"Rename the member to avoid the collision.",
                        line: line,
                        code: DiagnosticCodes.CodeGen.MemberNameCollision,
                        phase: CompilerPhase.CodeGeneration);
                }
            }
        }
    }

    /// <summary>
    /// Detects name collisions among module-level symbols (functions, variables, and types).
    /// All of these become members of the same module class in generated C#.
    /// </summary>
    private void DetectModuleLevelCollisions(Module module)
    {
        var seen = new Dictionary<string, (string originalName, int? line)>(); // CSharpName → (originalName, line)

        foreach (var stmt in module.Body)
        {
            string? symbolName = null;
            int? stmtLine = null;

            switch (stmt)
            {
                case FunctionDef funcDef:
                    symbolName = funcDef.Name;
                    stmtLine = funcDef.LineStart;
                    break;
                case VariableDeclaration varDecl:
                    symbolName = varDecl.Name;
                    stmtLine = varDecl.LineStart;
                    break;
                case ClassDef classDef:
                    symbolName = classDef.Name;
                    stmtLine = classDef.LineStart;
                    break;
                case StructDef structDef:
                    symbolName = structDef.Name;
                    stmtLine = structDef.LineStart;
                    break;
                case InterfaceDef interfaceDef:
                    symbolName = interfaceDef.Name;
                    stmtLine = interfaceDef.LineStart;
                    break;
                case EnumDef enumDef:
                    symbolName = enumDef.Name;
                    stmtLine = enumDef.LineStart;
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

            if (seen.TryGetValue(info.CSharpName, out var existing))
            {
                _diagnostics.AddError(
                    $"Name collision: '{symbolName}' and '{existing.originalName}' both compile to " +
                    $"'{info.CSharpName}'. Rename one of the conflicting symbols.",
                    line: stmtLine,
                    code: DiagnosticCodes.CodeGen.MemberNameCollision,
                    phase: CompilerPhase.CodeGeneration);
            }
            else
            {
                seen[info.CSharpName] = (symbolName, stmtLine);
            }
        }
    }

    /// <summary>
    /// Finds the line number for a member declaration in the type body.
    /// </summary>
    private static int? FindMemberLine(IEnumerable<Statement> body, string memberName)
    {
        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case VariableDeclaration v when v.Name == memberName:
                    return v.LineStart;
                case FunctionDef f when f.Name == memberName:
                    return f.LineStart;
            }
        }
        return null;
    }

    // Note: HasExecutionOrderIssues and ContainsRuntimeExpression methods were removed.
    // The ExecutionOrderAnalyzer class now handles execution order detection with
    // proper multi-pass analysis including assignment-before-declaration and
    // transitive dependency detection.
}
