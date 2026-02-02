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
/// Generates C# code using Roslyn syntax trees.
///
/// Name Resolution:
/// - Module-level symbols (variables, constants, functions, types, imports):
///   Use Symbol.CodeGenInfo which is computed during semantic analysis
/// - Local variables: Use runtime tracking (_declaredVariables, _variableVersions)
///   because local variable redeclarations happen during emission
/// - Type detection (class/struct instantiation): Use SymbolTable lookup
/// - String enum detection: Use CodeGenInfo.IsStringEnum
/// </summary>
public partial class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    private readonly HashSet<string> _declaredVariables = new();

    // ============================================================
    // LOCAL SCOPE TRACKING FIELDS
    //
    // These fields track mutable state during emission for LOCAL variables.
    // They are needed because local variable redeclarations happen during
    // emission, not during semantic analysis (so CodeGenInfo can't pre-compute them).
    //
    // For module-level symbols (variables, functions, types, imports),
    // use Symbol.CodeGenInfo which is computed during semantic analysis.
    // ============================================================

    /// <summary>
    /// Tracks variable version numbers for handling local variable redeclarations.
    /// E.g., x = 1; x = "hello" produces x then x_1.
    /// </summary>
    private readonly Dictionary<string, int> _variableVersions = new();

    /// <summary>
    /// Tracks const variable names (original Sharpy names) within local scopes.
    /// Needed for local const declarations within functions.
    /// </summary>
    private readonly HashSet<string> _constVariables = new();

    /// <summary>
    /// Tracks module-level field names (C# names) to prevent duplicate field declarations.
    /// This is still needed during emission even with CodeGenInfo because we need to
    /// track which C# field names have already been emitted.
    /// </summary>
    private readonly HashSet<string> _moduleFieldNames = new();

    /// <summary>
    /// When true, forces module-level variable declarations to be generated as static fields
    /// even if they have execution order issues. This is set when there's a user-defined main()
    /// function, because in that case the user is responsible for execution order.
    /// </summary>
    private bool _forceModuleLevelFields;

    // ============================================================
    // END LOCAL SCOPE TRACKING FIELDS
    // ============================================================

    // Note: _classNames, _structNames, and _stringEnumNames tracking sets were removed.
    // Type detection is now done via SymbolTable lookup (for classes/structs) and
    // CodeGenInfo.IsStringEnum (for string enums). This information is populated
    // during semantic analysis.

    private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new(); // Track interface definitions for abstract class stub generation
    private int _tempVarCounter = 0;

    // Target type context for collection literal type inference
    // Set before generating expressions that need target type information
    private TypeAnnotation? _targetTypeContext;

    // Track if we're currently generating methods for an abstract class
    // Used for implicit abstract method detection (ellipsis body in abstract class = abstract method)
    private bool _isInAbstractClass;

    // Common .NET namespace acronyms that should be all uppercase
    private static readonly HashSet<string> UpperCaseAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "io", "ui", "xml", "html", "api", "sql", "db", "http", "ftp",
        "smtp", "tcp", "udp", "ip", "uri", "url", "json", "csv", "guid"
    };

    public RoslynEmitter(CodeGenContext context)
    {
        _context = context;
        _typeMapper = new TypeMapper(context);
    }

    /// <summary>
    /// Resolve the C# name for a variable using CodeGenInfo.
    /// Returns null if CodeGenInfo is not available or if this is a local redeclaration.
    /// </summary>
    private string? TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)
    {
        var symbol = _context.LookupSymbol(sharpyName);
        if (symbol == null)
            return null;

        var info = GetCodeGenInfo(symbol);
        if (info == null)
            return null;

        // For new declarations, check if this is a local redeclaration
        // Local variable redeclarations still need runtime tracking via _variableVersions
        // because they happen during emission, not semantic analysis
        // Exception: when _forceModuleLevelFields is true, variables with execution order issues
        // are still generated as static fields
        if (isNewDeclaration && !info.IsModuleLevel && !_forceModuleLevelFields)
        {
            return null; // Let GetMangledVariableName handle local redeclarations
        }

        // When _forceModuleLevelFields is true and this symbol has execution order issues,
        // the CodeGenInfo name was computed as camelCase (for a local) but we need PascalCase
        // (for a static field). Override the name in this case.
        if (_forceModuleLevelFields && info.HasExecutionOrderIssues && symbol is VariableSymbol)
        {
            // Use PascalCase for module-level fields
            return NameMangler.ToPascalCase(sharpyName);
        }

        var csharpName = info.GetVersionedCSharpName();

        // Module imports need C# keyword escaping (e.g., "base" -> "@base")
        if (symbol is ModuleSymbol)
        {
            return EscapeCSharpKeyword(csharpName);
        }

        return csharpName;
    }

    /// <summary>
    /// Get the mangled variable name with version suffix if this is a redefinition.
    /// </summary>
    /// <param name="name">The original Sharpy variable name</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition, false if this is a reference</param>
    /// <returns>The C# variable name with version suffix (e.g., "x", "x_1", "x_2")</returns>
    private string GetMangledVariableName(string name, bool isNewDeclaration)
    {
        var baseName = NameMangler.ToCamelCase(name);

        // FIRST: Check if this is a local variable (including parameters)
        // Local variables take precedence over module-level variables and CodeGenInfo
        // This handles parameter shadowing correctly (parameter x shadows global x)
        if (_variableVersions.ContainsKey(baseName))
        {
            // There's a local variable with this name - use local resolution
            if (isNewDeclaration)
            {
                // This is a redefinition of an existing local variable
                var currentVersion = _variableVersions[baseName];
                var newVersion = currentVersion + 1;
                _variableVersions[baseName] = newVersion;
                return $"{baseName}_{newVersion}";
            }
            else
            {
                // This is a reference to the local variable
                var currentVersion = _variableVersions[baseName];
                return currentVersion == 0 ? baseName : $"{baseName}_{currentVersion}";
            }
        }

        // Check if this is a reference to a local const variable - use constant case
        // (still needed for local scope tracking during emission)
        if (_constVariables.Contains(name))
        {
            return NameMangler.ToConstantCase(name);
        }

        // Look up the symbol to check its kind
        var symbol = _context.LookupSymbol(name);

        // Check if this is a reference to a class or struct name - preserve PascalCase
        // Uses symbol table lookup instead of legacy tracking sets
        if (symbol is TypeSymbol typeSymbol &&
            (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
             typeSymbol.TypeKind == Semantic.TypeKind.Struct))
        {
            return NameMangler.ToPascalCase(name);
        }

        // Check if this is a module symbol - preserve the exact name (with sanitization)
        // This ensures imported module names match their using alias (e.g., math_ops stays math_ops)
        if (symbol is ModuleSymbol)
        {
            // Use the same sanitization as in GenerateImportUsings
            // Also escape C# keywords like "base" -> "@base"
            return EscapeCSharpKeyword(name.Replace(".", "_"));
        }

        // Try CodeGenInfo-based resolution for module-level symbols and from-imports
        // CodeGenInfo handles: module-level variables, constants, from-imports (with aliases)
        // This comes after local variable checks to ensure parameters shadow globals correctly
        var codeGenName = TryGetCSharpNameFromCodeGenInfo(name, isNewDeclaration);
        if (codeGenName != null)
            return codeGenName;

        // If we reach here, this is a new local variable that doesn't shadow any module-level var
        if (isNewDeclaration)
        {
            // First declaration of this local variable
            _variableVersions[baseName] = 0;
            return baseName;
        }
        else
        {
            // Reference to a variable not yet declared (shouldn't happen in valid code)
            // Fall back to just returning the base name
            return baseName;
        }
    }

    // ============================================================
    // CodeGenInfo helper methods
    //
    // These methods read CodeGenInfo via SemanticBinding (preferred)
    // with fallback to Symbol.CodeGenInfo (post-materialization).
    // Materialization copies data from SemanticBinding to Symbol
    // properties at phase boundaries, so both sources should agree
    // after materialization.
    // ============================================================

    /// <summary>
    /// Get CodeGenInfo for a symbol from SemanticBinding.
    /// Falls back to symbol.CodeGenInfo for symbols not tracked by this binding.
    /// </summary>
    private CodeGenInfo? GetCodeGenInfo(Symbol symbol)
        => _context.SemanticBinding.GetCodeGenInfo(symbol) ?? symbol.CodeGenInfo;

    /// <summary>
    /// Get the type for a VariableSymbol from SemanticBinding.
    /// Falls back to symbol.Type for symbols not tracked by this binding.
    /// </summary>
    private SemanticType GetVariableType(VariableSymbol symbol)
    {
        var bindingType = _context.SemanticBinding.GetVariableType(symbol);
        return bindingType != SemanticType.Unknown ? bindingType : symbol.Type;
    }

    /// <summary>
    /// Get the C# name for a symbol using CodeGenInfo.
    /// </summary>
    private string GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)
    {
        var info = GetCodeGenInfo(symbol);
        if (info != null)
        {
            return info.GetVersionedCSharpName();
        }

        // CodeGenInfo not available - use fallback logic for symbol kind
        // This should only happen for symbols not processed by CodeGenInfoComputer
        // (e.g., local variables during emission)
        return symbol.Kind switch
        {
            Semantic.SymbolKind.Variable => GetMangledVariableName(symbol.Name, isNewDeclaration),
            Semantic.SymbolKind.Function => NameMangler.ToPascalCase(symbol.Name),
            Semantic.SymbolKind.Type => NameMangler.ToPascalCase(symbol.Name),
            Semantic.SymbolKind.Module => EscapeCSharpKeyword(symbol.Name.Replace(".", "_")),
            Semantic.SymbolKind.Parameter => NameMangler.ToCamelCase(symbol.Name),
            _ => symbol.Name
        };
    }

    /// <summary>
    /// Check if a symbol is a module-level constant using CodeGenInfo.
    /// </summary>
    private bool IsModuleLevelConstant(Symbol symbol)
    {
        var info = GetCodeGenInfo(symbol);
        return info?.IsModuleLevel == true && info.IsConstant;
    }

    /// <summary>
    /// Check if a symbol is a module-level variable (not constant) using CodeGenInfo.
    /// </summary>
    private bool IsModuleLevelVariable(Symbol symbol)
    {
        var info = GetCodeGenInfo(symbol);
        return info?.IsModuleLevel == true && !info.IsConstant;
    }

    /// <summary>
    /// Check if a symbol has execution order issues using CodeGenInfo.
    /// </summary>
    private bool HasExecutionOrderIssues(Symbol symbol)
    {
        return GetCodeGenInfo(symbol)?.HasExecutionOrderIssues == true;
    }

    /// <summary>
    /// Check if a symbol is a from-import symbol using CodeGenInfo.
    /// </summary>
    private bool IsFromImportSymbol(Symbol symbol)
    {
        var info = GetCodeGenInfo(symbol);
        return info?.ImportKind == ImportKind.FromImport ||
               info?.ImportKind == ImportKind.FromImportWithAlias;
    }

    /// <summary>
    /// Get the original import name for an aliased from-import using CodeGenInfo.
    /// </summary>
    private string? GetOriginalImportName(Symbol symbol)
    {
        return GetCodeGenInfo(symbol)?.OriginalImportName;
    }

    // ============================================================
    // SemanticBinding helper methods for FromImportStatement data
    //
    // These methods read from SemanticBinding when available,
    // falling back to direct AST properties for backward compatibility.
    // ============================================================

    /// <summary>
    /// Gets the resolved module path for a FromImportStatement from SemanticBinding or AST.
    /// </summary>
    private string? GetResolvedModulePath(FromImportStatement fromImport)
    {
        return _context.SemanticBinding.GetResolvedModulePath(fromImport)
            ?? fromImport.ResolvedModulePath;
    }

    /// <summary>
    /// Gets the re-exported symbols for a FromImportStatement from SemanticBinding or AST.
    /// </summary>
    private Dictionary<string, Symbol>? GetReExportedSymbols(FromImportStatement fromImport)
    {
        return _context.SemanticBinding.GetReExportedSymbols(fromImport)
            ?? fromImport.ReExportedSymbols;
    }

    /// <summary>
    /// Checks if a FromImportStatement has re-exported symbols.
    /// </summary>
    private bool HasReExportedSymbols(FromImportStatement fromImport)
    {
        var symbols = GetReExportedSymbols(fromImport);
        return symbols != null && symbols.Count > 0;
    }

    /// <summary>
    /// Emits a diagnostic for an unrecognized statement type in code generation.
    /// Returns null so it can be used in switch expressions.
    /// </summary>
    private SyntaxNode? EmitUnrecognizedStatementDiagnostic(Statement stmt)
    {
        _context.AddError(
            $"Internal: unrecognized statement type '{stmt.GetType().Name}' was not emitted. This is a compiler bug — please report it.",
            DiagnosticCodes.CodeGen.UnrecognizedStatementType,
            stmt.LineStart,
            stmt.ColumnStart);
        return null;
    }
}
