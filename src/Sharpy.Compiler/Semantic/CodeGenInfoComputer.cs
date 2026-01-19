using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Computes CodeGenInfo for all symbols in a module.
/// This class runs after type checking to populate CodeGenInfo on symbols.
///
/// The computation mirrors what RoslynEmitter currently does at emission time,
/// but does it once during semantic analysis instead of dynamically during emission.
/// </summary>
public class CodeGenInfoComputer
{
    private readonly SymbolTable _symbolTable;
    private readonly HashSet<string> _processedModuleLevelVars = new();
    private HashSet<string> _variablesWithExecutionOrderIssues = new();

    public CodeGenInfoComputer(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
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

            varSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(varDecl.Name),
                OriginalName = varDecl.Name,
                Version = 0,
                IsModuleLevel = !hasIssues,  // Not module-level if has execution order issues
                IsConstant = false,
                HasExecutionOrderIssues = hasIssues
            };
            _processedModuleLevelVars.Add(varDecl.Name);
        }
    }

    private void ProcessModuleLevelConstant(VariableDeclaration constDecl)
    {
        var symbol = _symbolTable.Lookup(constDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            varSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToConstantCase(constDecl.Name),
                OriginalName = constDecl.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = true,
                HasExecutionOrderIssues = false // Constants are always compile-time
            };
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
                moduleSymbol.CodeGenInfo = new CodeGenInfo
                {
                    CSharpName = effectiveName.Replace(".", "_"),
                    OriginalName = effectiveName,
                    ImportKind = alias.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.ModuleImport
                };
            }
        }
    }

    private void ProcessFromImport(FromImportStatement fromImport)
    {
        foreach (var imported in fromImport.Names)
        {
            var effectiveName = imported.AsName ?? imported.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol != null)
            {
                var originalName = imported.AsName != null ? imported.Name : null;
                var csharpName = DetermineCSharpNameForFromImport(imported.Name, symbol);

                symbol.CodeGenInfo = new CodeGenInfo
                {
                    CSharpName = csharpName,
                    OriginalName = effectiveName,
                    ImportKind = imported.AsName != null ? ImportKind.FromImportWithAlias : ImportKind.FromImport,
                    OriginalImportName = originalName
                };
            }
        }
    }

    private string DetermineCSharpNameForFromImport(string name, Symbol symbol)
    {
        // Use the same logic as RoslynEmitter for from-imports:
        // - ALL_CAPS names (constants) stay as CONSTANT_CASE
        // - Other names become PascalCase
        if (IsConstantCaseName(name))
        {
            return NameMangler.ToConstantCase(name);
        }
        return NameMangler.ToPascalCase(name);
    }

    private static bool IsConstantCaseName(string name)
    {
        // A name is considered CONSTANT_CASE if it's all uppercase with underscores
        return name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c))
               && name.Any(char.IsUpper);
    }

    private void ProcessClassDef(ClassDef classDef)
    {
        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(classDef.Name),
                OriginalName = classDef.Name
            };

            // Process class members
            ProcessTypeMembers(typeSymbol, classDef.Body);
        }
    }

    private void ProcessStructDef(StructDef structDef)
    {
        var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(structDef.Name),
                OriginalName = structDef.Name
            };

            ProcessTypeMembers(typeSymbol, structDef.Body);
        }
    }

    private void ProcessInterfaceDef(InterfaceDef interfaceDef)
    {
        var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Interfaces preserve their exact name (which should already have I prefix)
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToInterfaceName(interfaceDef.Name),
                OriginalName = interfaceDef.Name
            };

            ProcessTypeMembers(typeSymbol, interfaceDef.Body);
        }
    }

    private void ProcessEnumDef(EnumDef enumDef)
    {
        var typeSymbol = _symbolTable.Lookup(enumDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(enumDef.Name),
                OriginalName = enumDef.Name
            };

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
            fieldSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToCamelCase(fieldDecl.Name),
                OriginalName = fieldDecl.Name,
                IsModuleLevel = false,
                IsConstant = fieldDecl.IsConst
            };
        }
    }

    private void ProcessMethodDef(TypeSymbol typeSymbol, FunctionDef funcDef)
    {
        var methodSymbol = typeSymbol.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
        if (methodSymbol != null)
        {
            methodSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(funcDef.Name),
                OriginalName = funcDef.Name,
                IsModuleLevel = false
            };
        }
    }

    private void ProcessFunctionDef(FunctionDef funcDef, bool isModuleLevel)
    {
        var funcSymbol = _symbolTable.Lookup(funcDef.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            funcSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(funcDef.Name),
                OriginalName = funcDef.Name,
                IsModuleLevel = isModuleLevel
            };
        }
    }

    // Note: HasExecutionOrderIssues and ContainsRuntimeExpression methods were removed.
    // The ExecutionOrderAnalyzer class now handles execution order detection with
    // proper multi-pass analysis including assignment-before-declaration and
    // transitive dependency detection.
}
