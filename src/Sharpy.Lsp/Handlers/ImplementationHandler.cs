using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/implementation requests.
/// Returns locations of implementing classes for interfaces/abstract classes,
/// and overriding methods for abstract/virtual methods.
/// </summary>
internal sealed class SharpyImplementationHandler : ImplementationHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyImplementationHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<LocationOrLocationLinks?> Handle(
        ImplementationParams request,
        CancellationToken ct)
    {
        try
        {
            var uri = request.TextDocument.Uri.ToString();
            var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

            if (analysis?.Ast == null || analysis.SemanticQuery == null)
                return null;

            var (line, col) = PositionConverter.ToCompiler(request.Position);
            var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

            if (node == null)
                return null;

            var symbol = ResolveSymbol(node, analysis);
            if (symbol == null)
                return null;

            var symbolTable = GetBestSymbolTable(analysis);
            if (symbolTable == null)
                return null;

            // Re-resolve the symbol from the best symbol table so that reference
            // equality works when the TypeHierarchyIndex is built from the same table.
            symbol = ReResolveInTable(symbol, symbolTable) ?? symbol;

            var locations = FindImplementations(symbol, symbolTable, uri);
            if (locations is not { Count: > 0 })
                return null;

            return new LocationOrLocationLinks(
                locations.Select(loc => new LocationOrLocationLink(loc)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Return null instead of crashing the LSP server. Cross-file
            // implementation lookups can fail when symbol tables from
            // different files have incompatible reference identities.
            System.Diagnostics.Trace.TraceWarning(
                "textDocument/implementation failed: {0}", ex.Message);
            return null;
        }
    }

    private static Symbol? ResolveSymbol(Node node, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        return node switch
        {
            Identifier id => query.GetIdentifierSymbol(id),
            FunctionCall call => query.GetCallTarget(call),
            MemberAccess ma => ResolveFromMemberAccess(ma, analysis),
            ClassDef cd => analysis.SymbolTable?.Lookup(cd.Name),
            InterfaceDef ifd => analysis.SymbolTable?.Lookup(ifd.Name),
            FunctionDef fd => ResolveFunction(fd, analysis),
            _ => null
        };
    }

    private static Symbol? ResolveFunction(FunctionDef fd, SemanticResult analysis)
    {
        // For methods inside a class, search all type symbols for a matching method.
        if (analysis.SymbolTable == null)
            return null;

        foreach (var sym in analysis.SymbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            var method = sym.Methods.Find(m =>
                string.Equals(m.Name, fd.Name, StringComparison.Ordinal)
                && m.DeclarationLine == fd.LineStart);
            if (method != null)
                return method;
        }

        // Fall back to top-level function lookup.
        return analysis.SymbolTable.Lookup(fd.Name);
    }

    private static Symbol? ResolveFromMemberAccess(MemberAccess ma, SemanticResult analysis)
    {
        var query = analysis.SemanticQuery!;

        var objType = query.GetEffectiveType(ma.Object);
        if (objType is UserDefinedType udt && udt.Symbol != null)
        {
            var memberName = ma.Member;

            var method = udt.Symbol.Methods.Find(m =>
                string.Equals(m.Name, memberName, StringComparison.Ordinal));
            if (method != null)
                return method;

            var field = udt.Symbol.Fields.Find(f =>
                string.Equals(f.Name, memberName, StringComparison.Ordinal));
            if (field != null)
                return field;
        }

        return null;
    }

    private static Symbol? ReResolveInTable(Symbol symbol, SymbolTable symbolTable)
    {
        if (symbol is TypeSymbol)
            return symbolTable.Lookup(symbol.Name);

        if (symbol is FunctionSymbol funcSym)
        {
            // Methods aren't top-level — search declaring types by name + line.
            foreach (var ts in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
            {
                var method = ts.Methods.Find(m =>
                    string.Equals(m.Name, funcSym.Name, StringComparison.Ordinal)
                    && m.DeclarationLine == funcSym.DeclarationLine);
                if (method != null)
                    return method;
            }

            // Fall back to top-level function.
            return symbolTable.Lookup(symbol.Name);
        }

        return symbolTable.Lookup(symbol.Name);
    }

    private SymbolTable? GetBestSymbolTable(SemanticResult analysis)
    {
        // Prefer project-wide symbol table for full type hierarchy coverage.
        var projectAnalysis = _languageService.ProjectAnalysis;
        return projectAnalysis?.ProjectModel.GlobalSymbols ?? analysis.SymbolTable;
    }

    private static System.Collections.Generic.List<Location>? FindImplementations(
        Symbol symbol, SymbolTable symbolTable, string fallbackUri)
    {
        if (symbol is TypeSymbol typeSymbol)
        {
            // Only interfaces and abstract classes have implementations to find.
            if (typeSymbol.TypeKind != TypeKind.Interface && !typeSymbol.IsAbstract)
            {
                var loc = SymbolLocationHelper.GetSymbolLocation(typeSymbol, fallbackUri);
                return loc != null ? new System.Collections.Generic.List<Location> { loc } : null;
            }

            var index = TypeHierarchyIndex.Build(symbolTable);
            return FindTypeImplementations(typeSymbol, index, fallbackUri);
        }

        if (symbol is FunctionSymbol funcSymbol && (funcSymbol.IsAbstract || funcSymbol.IsVirtual))
        {
            var index = TypeHierarchyIndex.Build(symbolTable);
            return FindMethodImplementations(funcSymbol, symbolTable, index, fallbackUri);
        }

        // For concrete, non-virtual symbols, fall back to the definition location.
        var defLocation = SymbolLocationHelper.GetSymbolLocation(symbol, fallbackUri);
        return defLocation != null ? new System.Collections.Generic.List<Location> { defLocation } : null;
    }

    private static System.Collections.Generic.List<Location>? FindTypeImplementations(
        TypeSymbol typeSymbol, TypeHierarchyIndex index, string fallbackUri)
    {
        var subtypes = index.GetDirectSubtypes(typeSymbol);

        if (subtypes.Count == 0)
            return null;

        var locations = new System.Collections.Generic.List<Location>();
        foreach (var subtype in subtypes)
        {
            var loc = SymbolLocationHelper.GetSymbolLocation(subtype, fallbackUri);
            if (loc != null)
                locations.Add(loc);
        }

        return locations;
    }

    private static System.Collections.Generic.List<Location>? FindMethodImplementations(
        FunctionSymbol funcSymbol, SymbolTable symbolTable, TypeHierarchyIndex index,
        string fallbackUri)
    {
        // Find the declaring type by matching method name and declaration line.
        TypeSymbol? declaringType = null;
        foreach (var sym in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            if (sym.Methods.Any(m =>
                string.Equals(m.Name, funcSymbol.Name, StringComparison.Ordinal)
                && m.DeclarationLine == funcSymbol.DeclarationLine))
            {
                declaringType = sym;
                break;
            }
        }

        if (declaringType == null)
            return null;

        var subtypes = index.GetDirectSubtypes(declaringType);

        var locations = new System.Collections.Generic.List<Location>();
        foreach (var subtype in subtypes)
        {
            var overridingMethod = subtype.Methods.Find(m =>
                string.Equals(m.Name, funcSymbol.Name, StringComparison.Ordinal)
                && m.IsOverride);
            if (overridingMethod != null)
            {
                var loc = SymbolLocationHelper.GetSymbolLocation(overridingMethod, fallbackUri);
                if (loc != null)
                    locations.Add(loc);
            }
        }

        return locations.Count > 0 ? locations : null;
    }

    protected override ImplementationRegistrationOptions CreateRegistrationOptions(
        ImplementationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new ImplementationRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
