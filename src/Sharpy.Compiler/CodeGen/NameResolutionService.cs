using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Consolidated service for resolving Sharpy names to C# identifiers during code generation.
///
/// Resolution Order (explicit and documented):
/// 1. CodeGenInfo.CSharpName — Precomputed during semantic analysis for module-level symbols
/// 2. Local variable versioning — For redeclared locals: x, x_1, x_2
/// 3. NameMangler fallback — snake_case → PascalCase/camelCase based on symbol kind
///
/// This service centralizes name resolution logic that was previously scattered across
/// multiple methods in RoslynEmitter (GetMangledVariableName, TryGetCSharpNameFromCodeGenInfo,
/// GetCSharpNameForSymbol).
/// </summary>
internal sealed class NameResolutionService
{
    private readonly ICompilerLogger? _logger;

    // C# keywords that need @ prefix when used as identifiers
    private static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object",
        "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while"
    };

    /// <summary>
    /// Creates a new NameResolutionService.
    /// </summary>
    /// <param name="logger">Optional logger for tracing resolution decisions.</param>
    public NameResolutionService(ICompilerLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Resolves a symbol to its C# identifier name.
    ///
    /// Resolution order:
    /// 1. CodeGenInfo.CSharpName (precomputed during semantic analysis)
    /// 2. Local variable versioning (for redeclared locals: x, x_1, x_2)
    /// 3. NameMangler fallback (snake_case → PascalCase/camelCase)
    /// </summary>
    /// <param name="symbol">The symbol to resolve.</param>
    /// <param name="codeGenInfo">The CodeGenInfo for the symbol, if available.</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition.</param>
    /// <param name="variableVersions">Current variable versions for local scope tracking.</param>
    /// <param name="sourceVariableNames">Set of source variable names (C# camelCase) in current scope.</param>
    /// <param name="forceModuleLevelFields">When true, force module-level treatment for execution order issues.</param>
    /// <returns>The C# identifier name to use.</returns>
    public string ResolveName(
        Symbol symbol,
        CodeGenInfo? codeGenInfo,
        bool isNewDeclaration = false,
        IReadOnlyDictionary<string, int>? variableVersions = null,
        IReadOnlySet<string>? sourceVariableNames = null,
        bool forceModuleLevelFields = false)
    {
        LogTrace($"ResolveName: symbol='{symbol.Name}', kind={symbol.Kind}, isNewDeclaration={isNewDeclaration}");

        // Step 1: Try CodeGenInfo-based resolution
        var codeGenResult = TryResolveFromCodeGenInfo(
            symbol,
            codeGenInfo,
            isNewDeclaration,
            forceModuleLevelFields);

        if (codeGenResult != null)
        {
            LogTrace($"ResolveName: resolved via CodeGenInfo → '{codeGenResult}'");
            return codeGenResult;
        }

        // Step 2: Try local variable versioning
        if (variableVersions != null && sourceVariableNames != null)
        {
            var localResult = ResolveLocalName(
                symbol.Name,
                isNewDeclaration,
                variableVersions,
                sourceVariableNames);

            if (localResult != null)
            {
                LogTrace($"ResolveName: resolved via local versioning → '{localResult}'");
                return localResult;
            }
        }

        // Step 3: Fallback to NameMangler based on symbol kind
        var fallbackResult = ResolveBySymbolKind(symbol);
        LogTrace($"ResolveName: resolved via fallback → '{fallbackResult}'");
        return fallbackResult;
    }

    /// <summary>
    /// Resolves a local variable name with versioning support.
    /// Handles redeclared variables (x → x, x_1, x_2) and avoids collisions
    /// with user-declared variable names.
    /// </summary>
    /// <param name="originalName">The original Sharpy variable name.</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition.</param>
    /// <param name="variableVersions">Mutable dictionary tracking version numbers per variable.</param>
    /// <param name="sourceVariableNames">Set of source variable names to avoid collisions.</param>
    /// <returns>The C# variable name with version suffix, or null if not a local variable.</returns>
    public string? ResolveLocalName(
        string originalName,
        bool isNewDeclaration,
        IReadOnlyDictionary<string, int> variableVersions,
        IReadOnlySet<string> sourceVariableNames)
    {
        var baseName = NameMangler.ToCamelCase(originalName);

        // Check if this is a known local variable
        if (!variableVersions.ContainsKey(baseName))
        {
            return null; // Not a local variable
        }

        if (isNewDeclaration)
        {
            // This is a redefinition - find next available version
            var currentVersion = variableVersions[baseName];
            var newVersion = currentVersion + 1;
            var candidateName = $"{baseName}_{newVersion}";

            // Skip versions that collide with user-declared names
            while (sourceVariableNames.Contains(candidateName))
            {
                newVersion++;
                candidateName = $"{baseName}_{newVersion}";
            }

            // Note: The caller is responsible for updating variableVersions
            // This method is read-only for thread safety
            return candidateName;
        }
        else
        {
            // This is a reference - return current version
            var currentVersion = variableVersions[baseName];
            return currentVersion == 0 ? baseName : $"{baseName}_{currentVersion}";
        }
    }

    /// <summary>
    /// Computes what the next version number would be for a local variable redeclaration.
    /// Used to update the variableVersions dictionary after ResolveLocalName returns a new version.
    /// </summary>
    /// <param name="originalName">The original Sharpy variable name.</param>
    /// <param name="currentVersion">The current version number.</param>
    /// <param name="sourceVariableNames">Set of source variable names to avoid collisions.</param>
    /// <returns>The next version number to use.</returns>
    public int ComputeNextVersion(
        string originalName,
        int currentVersion,
        IReadOnlySet<string> sourceVariableNames)
    {
        var baseName = NameMangler.ToCamelCase(originalName);
        var newVersion = currentVersion + 1;
        var candidateName = $"{baseName}_{newVersion}";

        while (sourceVariableNames.Contains(candidateName))
        {
            newVersion++;
            candidateName = $"{baseName}_{newVersion}";
        }

        return newVersion;
    }

    /// <summary>
    /// Gets the base C# name for a variable (camelCase) without version suffix.
    /// </summary>
    public string GetBaseName(string originalName)
    {
        return NameMangler.ToCamelCase(originalName);
    }

    /// <summary>
    /// Escapes a C# keyword by prefixing with @.
    /// </summary>
    public static string EscapeCSharpKeyword(string name)
    {
        return CSharpKeywords.Contains(name.ToLowerInvariant())
            ? "@" + name
            : name;
    }

    /// <summary>
    /// Tries to resolve a name from CodeGenInfo.
    /// </summary>
    private string? TryResolveFromCodeGenInfo(
        Symbol symbol,
        CodeGenInfo? info,
        bool isNewDeclaration,
        bool forceModuleLevelFields)
    {
        if (info == null)
            return null;

        // For new declarations, check if this is a local redeclaration
        // Local variable redeclarations still need runtime tracking via variableVersions
        // because they happen during emission, not semantic analysis
        // Exception: when forceModuleLevelFields is true, variables with execution order issues
        // are still generated as static fields
        if (isNewDeclaration && !info.IsModuleLevel && !forceModuleLevelFields)
        {
            return null; // Let local variable resolution handle it
        }

        // When forceModuleLevelFields is true and this symbol has execution order issues,
        // the CodeGenInfo name was computed as camelCase (for a local) but we need PascalCase
        // (for a static field). Override the name in this case.
        if (forceModuleLevelFields && info.HasExecutionOrderIssues && symbol is VariableSymbol)
        {
            return NameMangler.ToPascalCase(symbol.Name);
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
    /// Resolves a symbol name based on its kind using NameMangler.
    /// This is the fallback when CodeGenInfo is not available.
    /// </summary>
    private string ResolveBySymbolKind(Symbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.Variable => NameMangler.ToCamelCase(symbol.Name),
            SymbolKind.Function => NameMangler.ToPascalCase(symbol.Name),
            SymbolKind.Type => NameMangler.ToPascalCase(symbol.Name),
            SymbolKind.Module => EscapeCSharpKeyword(symbol.Name.Replace(".", "_")),
            SymbolKind.Parameter => NameMangler.ToCamelCase(symbol.Name),
            _ => symbol.Name
        };
    }

    private void LogTrace(string message)
    {
        if (_logger?.IsEnabled(CompilerLogLevel.Trace) == true)
        {
            _logger.LogTrace($"[NameResolution] {message}");
        }
    }
}
