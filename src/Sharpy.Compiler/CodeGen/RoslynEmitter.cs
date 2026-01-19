using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Generates C# code using Roslyn syntax trees
/// </summary>
public partial class RoslynEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeMapper _typeMapper;
    private readonly HashSet<string> _declaredVariables = new();

    // ============================================================
    // LEGACY TRACKING FIELDS - Deprecated in favor of CodeGenInfo
    //
    // These fields track mutable state during emission and are used
    // by GetMangledVariableName and related methods. They will be
    // removed once all emission code migrates to use the CodeGenInfo-
    // aware helper methods (GetCSharpNameForSymbol, etc.).
    //
    // Migration path: Use Symbol.CodeGenInfo instead of these fields.
    // See helper methods at the end of this file for CodeGenInfo usage.
    // ============================================================

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.Version instead.
    /// Tracks variable version numbers for handling variable redeclarations.
    /// </summary>
    private readonly Dictionary<string, int> _variableVersions = new();

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.IsConstant instead.
    /// Tracks const variable names (original Sharpy names) within local scopes.
    /// </summary>
    private readonly HashSet<string> _constVariables = new();

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.IsModuleLevel and IsConstant instead.
    /// Tracks module-level const names (preserved across function scopes).
    /// </summary>
    private readonly HashSet<string> _moduleConstVariables = new();

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.IsModuleLevel instead.
    /// Tracks module-level variable names (for PascalCase reference).
    /// </summary>
    private readonly HashSet<string> _moduleVariables = new();

    /// <summary>
    /// Tracks module-level field names (C# names) to prevent duplicate field declarations.
    /// Note: This is still needed during emission even with CodeGenInfo.
    /// </summary>
    private readonly HashSet<string> _moduleFieldNames = new();

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.HasExecutionOrderIssues instead.
    /// Tracks variables that should not become static fields due to execution order issues.
    /// </summary>
    private HashSet<string> _variablesWithExecutionOrderIssues = new();

    // ============================================================
    // END LEGACY TRACKING FIELDS
    // ============================================================

    private readonly HashSet<string> _classNames = new(); // Track class names defined in the current module
    private readonly HashSet<string> _structNames = new(); // Track struct names defined in the current module
    private readonly HashSet<string> _stringEnumNames = new(); // Track string enum names (enums with string values)

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.ImportKind instead.
    /// Tracks symbols imported via "from X import Y" for proper casing.
    /// </summary>
    private readonly HashSet<string> _fromImportSymbols = new();

    /// <summary>
    /// [DEPRECATED] Use Symbol.CodeGenInfo.OriginalImportName instead.
    /// Maps alias → original name for from-imports.
    /// </summary>
    private readonly Dictionary<string, string> _importAliasToOriginal = new();

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
    /// Get the mangled variable name with version suffix if this is a redefinition.
    /// </summary>
    /// <param name="name">The original Sharpy variable name</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition, false if this is a reference</param>
    /// <returns>The C# variable name with version suffix (e.g., "x", "x_1", "x_2")</returns>
    private string GetMangledVariableName(string name, bool isNewDeclaration)
    {
        var baseName = NameMangler.ToCamelCase(name);

        // FIRST: Check if this is a local variable that shadows a module-level one
        // Local variables take precedence over module-level variables
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
        if (_constVariables.Contains(name))
        {
            return NameMangler.ToConstantCase(name);
        }

        // Check if this is a reference to a module-level const - use constant case
        if (_moduleConstVariables.Contains(name))
        {
            return NameMangler.ToConstantCase(name);
        }

        // Check if this is a reference to a module-level variable - use PascalCase
        if (_moduleVariables.Contains(name))
        {
            return NameMangler.ToPascalCase(name);
        }

        // Check if this is a reference to a class or struct name - preserve PascalCase
        if (_classNames.Contains(name) || _structNames.Contains(name))
        {
            return NameMangler.ToPascalCase(name);
        }

        // Check if this is a symbol imported via "from X import Y"
        // These are accessed via "using static" and must match the exported name casing
        if (_fromImportSymbols.Contains(name))
        {
            // If this is an alias, use the original name for code generation
            // e.g., "from config import MAX_VALUE as MAX" → MAX maps to MAX_VALUE
            var actualName = _importAliasToOriginal.TryGetValue(name, out var originalName)
                ? originalName
                : name;

            // Use the same casing rules as exported module members:
            // - ALL_CAPS names (constants) stay as CONSTANT_CASE
            // - Other names become PascalCase
            if (IsConstantCaseName(actualName))
            {
                return NameMangler.ToConstantCase(actualName);
            }
            else
            {
                return NameMangler.ToPascalCase(actualName);
            }
        }

        // Check if this is a module symbol - preserve the exact name (with sanitization)
        // This ensures imported module names match their using alias (e.g., math_ops stays math_ops)
        var symbol = _context.LookupSymbol(name);
        if (symbol is ModuleSymbol)
        {
            // Use the same sanitization as in GenerateImportUsings
            // Also escape C# keywords like "base" -> "@base"
            return EscapeCSharpKeyword(name.Replace(".", "_"));
        }

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
    // CodeGenInfo-aware helper methods (Phase 4 of Recommendation #4)
    // These check for pre-computed CodeGenInfo first, then fall back
    // to existing logic for backwards compatibility.
    // ============================================================

    /// <summary>
    /// Get the C# name for a symbol, using CodeGenInfo if available.
    /// Falls back to existing logic if CodeGenInfo is not computed.
    /// </summary>
    private string GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)
    {
        // If CodeGenInfo is computed, use it
        if (symbol.CodeGenInfo != null)
        {
            return symbol.CodeGenInfo.GetVersionedCSharpName();
        }

        // Fall back to existing logic based on symbol kind
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
    /// Check if a symbol is a module-level constant, using CodeGenInfo if available.
    /// </summary>
    private bool IsModuleLevelConstant(Symbol symbol)
    {
        if (symbol.CodeGenInfo != null)
        {
            return symbol.CodeGenInfo.IsModuleLevel && symbol.CodeGenInfo.IsConstant;
        }

        // Fall back to existing tracking
        return _moduleConstVariables.Contains(symbol.Name);
    }

    /// <summary>
    /// Check if a symbol is a module-level variable (not constant), using CodeGenInfo if available.
    /// </summary>
    private bool IsModuleLevelVariable(Symbol symbol)
    {
        if (symbol.CodeGenInfo != null)
        {
            return symbol.CodeGenInfo.IsModuleLevel && !symbol.CodeGenInfo.IsConstant;
        }

        // Fall back to existing tracking
        return _moduleVariables.Contains(symbol.Name);
    }

    /// <summary>
    /// Check if a symbol has execution order issues, using CodeGenInfo if available.
    /// </summary>
    private bool HasExecutionOrderIssues(Symbol symbol)
    {
        if (symbol.CodeGenInfo != null)
        {
            return symbol.CodeGenInfo.HasExecutionOrderIssues;
        }

        // Fall back to existing tracking
        return _variablesWithExecutionOrderIssues.Contains(symbol.Name);
    }

    /// <summary>
    /// Check if a symbol is a from-import symbol, using CodeGenInfo if available.
    /// </summary>
    private bool IsFromImportSymbol(Symbol symbol)
    {
        if (symbol.CodeGenInfo != null)
        {
            return symbol.CodeGenInfo.ImportKind == ImportKind.FromImport ||
                   symbol.CodeGenInfo.ImportKind == ImportKind.FromImportWithAlias;
        }

        // Fall back to existing tracking
        return _fromImportSymbols.Contains(symbol.Name);
    }

    /// <summary>
    /// Get the original import name for an aliased from-import, using CodeGenInfo if available.
    /// </summary>
    private string? GetOriginalImportName(Symbol symbol)
    {
        if (symbol.CodeGenInfo != null)
        {
            return symbol.CodeGenInfo.OriginalImportName;
        }

        // Fall back to existing tracking
        return _importAliasToOriginal.TryGetValue(symbol.Name, out var originalName) ? originalName : null;
    }
}
