using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Consolidated service for resolving Sharpy names to C# identifiers during code generation.
///
/// Resolution Order (explicit and documented):
/// 1. CodeGenInfo.CSharpName (+ Version) — precomputed during semantic analysis for every symbol,
///    locals included: the LocalNameAllocator records x, x_1, x_2 for redeclared locals (#1560)
/// 2. NameMangler fallback — snake_case → PascalCase/camelCase based on symbol kind, for symbols
///    that carry no CodeGenInfo (never a local: the emitter throws for those)
///
/// This service centralizes name resolution logic that was previously scattered across
/// multiple methods in RoslynEmitter (GetMangledVariableName, TryGetCSharpNameFromCodeGenInfo,
/// GetCSharpNameForSymbol).
/// </summary>
internal sealed class NameResolutionService
{
    private readonly ICompilerLogger? _logger;

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
    /// 2. NameMangler fallback (snake_case → PascalCase/camelCase)
    /// </summary>
    /// <param name="symbol">The symbol to resolve.</param>
    /// <param name="codeGenInfo">The CodeGenInfo for the symbol, if available.</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition.</param>
    /// <param name="forceModuleLevelFields">When true, force module-level treatment for execution order issues.</param>
    /// <returns>The C# identifier name to use.</returns>
    public string ResolveName(
        Symbol symbol,
        CodeGenInfo? codeGenInfo,
        bool isNewDeclaration = false,
        bool forceModuleLevelFields = false)
    {
        LogTrace($"ResolveName: symbol='{symbol.Name}', kind={symbol.Kind}, isNewDeclaration={isNewDeclaration}");

        // Step 1: Try CodeGenInfo-based resolution
        var codeGenResult = TryResolveFromCodeGenInfoInternal(
            symbol,
            codeGenInfo,
            isNewDeclaration,
            forceModuleLevelFields);

        if (codeGenResult != null)
        {
            LogTrace($"ResolveName: resolved via CodeGenInfo → '{codeGenResult}'");
            return codeGenResult;
        }

        // Step 2: Fallback to NameMangler based on symbol kind
        var fallbackResult = ResolveBySymbolKind(symbol);
        LogTrace($"ResolveName: resolved via fallback → '{fallbackResult}'");
        return fallbackResult;
    }

    // Note: ResolveLocalName and ComputeNextVersion were deleted — the
    // LocalNameAllocator now pre-computes all local names (#1560, #1647).

    /// <summary>
    /// Gets the base C# name for a local variable — the name it is emitted under, and the key its
    /// slot is filed under — without the version suffix.
    /// </summary>
    /// <remarks>
    /// This is the one place a local's base name is computed, and it is escape-aware because the
    /// key and the emitted name must be the same string: a backtick-escaped local emits verbatim
    /// (owner decision on #1357), so <c>`Zed`</c> keys and emits <c>Zed</c> while a plain
    /// <c>zed</c> keys and emits <c>zed</c>. Keying on the camelCase base regardless would give
    /// the two ONE slot — the escaped binding would take the redeclaration path and come out as
    /// <c>zed_1</c> — and would leave the declaration emitting <c>Zed</c> while every reference
    /// resolved to <c>zed</c> (CS0103). Unescaped names are unaffected:
    /// <see cref="NameCasing.ResolveVariable"/>'s plain arm is <c>ToCamelCase</c>.
    /// </remarks>
    public string GetBaseName(string originalName, bool isBacktickEscaped = false)
    {
        return NameCasing.ResolveVariable(originalName, isBacktickEscaped);
    }

    /// <summary>
    /// Escapes a C# keyword by prefixing with @.
    /// </summary>
    public static string EscapeCSharpKeyword(string name)
    {
        return Shared.CSharpKeywords.EscapeIfNeeded(name);
    }

    /// <summary>
    /// Tries to resolve a name using only CodeGenInfo, without falling back to NameMangler.
    /// Returns null if:
    /// - CodeGenInfo is not available
    /// - This is a local redeclaration that should use local variable versioning
    ///
    /// Use this method when you need to know if CodeGenInfo-based resolution succeeded
    /// before falling back to other resolution strategies.
    /// </summary>
    /// <param name="symbol">The symbol to resolve.</param>
    /// <param name="codeGenInfo">The CodeGenInfo for the symbol.</param>
    /// <param name="isNewDeclaration">True if this is a new declaration/redefinition.</param>
    /// <param name="forceModuleLevelFields">When true, force module-level treatment.</param>
    /// <returns>The resolved name, or null if resolution should fall through.</returns>
    public string? TryResolveFromCodeGenInfo(
        Symbol symbol,
        CodeGenInfo? codeGenInfo,
        bool isNewDeclaration,
        bool forceModuleLevelFields = false)
    {
        return TryResolveFromCodeGenInfoInternal(symbol, codeGenInfo, isNewDeclaration, forceModuleLevelFields);
    }

    /// <summary>
    /// Internal implementation of CodeGenInfo resolution.
    /// Since #1560 all locals have CodeGenInfo, so this no longer returns null for local new
    /// declarations.
    /// </summary>
    private string? TryResolveFromCodeGenInfoInternal(
        Symbol symbol,
        CodeGenInfo? info,
        bool isNewDeclaration,
        bool forceModuleLevelFields)
    {
        if (info == null)
            return null;

        // When forceModuleLevelFields is true and this symbol has execution order issues,
        // the CodeGenInfo name was computed as camelCase (for a local) but we need PascalCase
        // (for a static field). Override the name in this case.
        if (forceModuleLevelFields && info.HasExecutionOrderIssues && symbol is VariableSymbol)
        {
            return NameCasing.ResolveField(symbol.Name, false);
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
    /// Resolves a symbol name based on its kind using NameCasing.
    /// This is the fallback when CodeGenInfo is not available.
    /// </summary>
    private string ResolveBySymbolKind(Symbol symbol)
    {
        var escaped = symbol.IsNameBacktickEscaped;
        return symbol.Kind switch
        {
            SymbolKind.Variable => NameCasing.ResolveVariable(symbol.Name, escaped),
            SymbolKind.Function => NameCasing.ResolveMethod(symbol.Name, escaped),
            SymbolKind.Type => NameCasing.ResolveType(symbol.Name, escaped),
            SymbolKind.Module => EscapeCSharpKeyword(symbol.Name.Replace(".", "_", StringComparison.Ordinal)),
            SymbolKind.Parameter => NameCasing.ResolveVariable(symbol.Name, escaped),
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
