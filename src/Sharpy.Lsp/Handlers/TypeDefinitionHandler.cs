using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;

namespace Sharpy.Lsp.Handlers;

internal sealed class SharpyTypeDefinitionHandler : TypeDefinitionHandlerBase
{
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;

    public SharpyTypeDefinitionHandler(LanguageService languageService, CompilerApi api)
    {
        _languageService = languageService;
        _api = api;
    }

    public override async Task<LocationOrLocationLinks?> Handle(
        TypeDefinitionParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var type = ResolveType(node, analysis, line, col);
        if (type == null)
            return null;

        var typeSymbol = GetTypeSymbol(type);
        if (typeSymbol == null)
            return null;

        var location = SymbolLocationHelper.GetSymbolLocation(typeSymbol, uri);
        if (location == null)
            return null;

        return new LocationOrLocationLinks(location);
    }

    /// <summary>
    /// Resolves the type to navigate to for the node under the cursor.
    /// </summary>
    /// <remarks>
    /// A cursor on a DECLARATION (a module/local/field variable name, a parameter name, a
    /// <c>def</c> name) does not land on an <see cref="Identifier"/> — <c>FindNodeAtPosition</c>
    /// returns the declaration statement, which none of the expression arms below answer for. That
    /// route is the shared <see cref="DeclarationCursorResolver"/> seam, the same one references,
    /// rename and document-highlight use; this handler was named there as a future consumer (#1539).
    /// Until #1736 corrected suite extents, <c>class Foo:</c>'s extent swallowed the line after its
    /// body, so a module declaration under the cursor answered from the <see cref="ClassDef"/> arm
    /// by accident; with the corrected extent the declaration route is the only one that answers.
    /// </remarks>
    private static SemanticType? ResolveType(Node node, SemanticResult analysis, int line, int col)
    {
        var query = analysis.SemanticQuery!;

        switch (node)
        {
            case Identifier id:
                {
                    var symbol = query.GetIdentifierSymbol(id);
                    if (symbol is TypeSymbol)
                        return query.GetEffectiveType(id);
                    return symbol switch
                    {
                        VariableSymbol vs => vs.Type,
                        FunctionSymbol => null,
                        _ => query.GetEffectiveType(id)
                    };
                }

            case FunctionCall call:
                return query.GetEffectiveType(call);

            case MemberAccess ma:
                return query.GetEffectiveType(ma);

            case ClassDef cd:
                {
                    var sym = analysis.SymbolTable?.Lookup(cd.Name)
                        ?? analysis.SymbolTable?.LookupInModuleScopes(cd.Name);
                    if (sym is TypeSymbol ts)
                        return new UserDefinedType { Name = ts.Name, Symbol = ts };
                    return null;
                }

            case InterfaceDef ifd:
                {
                    var sym = analysis.SymbolTable?.Lookup(ifd.Name)
                        ?? analysis.SymbolTable?.LookupInModuleScopes(ifd.Name);
                    if (sym is TypeSymbol ts)
                        return new UserDefinedType { Name = ts.Name, Symbol = ts };
                    return null;
                }

            default:
                return ResolveDeclarationType(node, query, line, col)
                    ?? ResolveAnnotationType(node, query, line, col)
                    ?? (node is Expression expr ? query.GetEffectiveType(expr) : null);
        }
    }

    /// <summary>
    /// The declaration route: the cursor sits on a declared NAME, so the shared resolver answers
    /// with the bound symbol and the symbol's declared type is what go-to-type-definition wants.
    /// A <see cref="FunctionSymbol"/> resolves to no navigable type (a <c>def</c> name has a
    /// function type) and falls through to null, matching the Identifier arm above.
    /// </summary>
    private static SemanticType? ResolveDeclarationType(
        Node node, ISemanticQuery query, int line, int col)
    {
        var symbol = DeclarationCursorResolver.Resolve(node, query, line, col, logger: null);

        return symbol switch
        {
            TypeSymbol ts => new UserDefinedType { Name = ts.Name, Symbol = ts },
            VariableSymbol vs => vs.Type,
            _ => null
        };
    }

    /// <summary>
    /// The annotation route: the cursor sits on the type spelling itself (<c>f: Foo = Foo()</c> with
    /// the cursor on <c>Foo</c>). <c>TypeAnnotation</c> is a <c>Node</c> but is deliberately not
    /// exposed to traversal, so <c>FindNodeAtPosition</c> returns the enclosing declaration and the
    /// annotation must be located by its own recorded extent. The resolved type is read from the
    /// annotation the checker recorded (#1737), innermost first, so <c>list[Item]</c> navigates to
    /// <c>Item</c> when the cursor is inside the type argument and to <c>list</c> otherwise.
    /// </summary>
    private static SemanticType? ResolveAnnotationType(
        Node node, ISemanticQuery query, int line, int col)
    {
        foreach (var annotation in AnnotationsOf(node))
        {
            foreach (var candidate in ContainingAnnotations(annotation, line, col))
            {
                var resolved = query.GetTypeAnnotation(candidate);
                if (resolved != null)
                    return resolved;
            }
        }

        return null;
    }

    /// <summary>
    /// The annotations a cursor can land on within <paramref name="node"/> without any deeper node
    /// being returned by the position index. Only the declaration kinds this handler's callers
    /// reach are listed; every other node kind yields nothing and the caller falls through to the
    /// expression route.
    /// </summary>
    private static IEnumerable<TypeAnnotation> AnnotationsOf(Node node)
    {
        switch (node)
        {
            case VariableDeclaration decl:
                if (decl.Type != null)
                    yield return decl.Type;
                break;

            case FunctionDef funcDef:
                foreach (var parameter in funcDef.Parameters)
                {
                    if (parameter.Type != null)
                        yield return parameter.Type;
                }
                if (funcDef.ReturnType != null)
                    yield return funcDef.ReturnType;
                break;

            case LambdaExpression lambda:
                foreach (var parameter in lambda.Parameters)
                {
                    if (parameter.Type != null)
                        yield return parameter.Type;
                }
                break;
        }
    }

    /// <summary>
    /// The annotations containing the cursor, innermost first: a type argument or the <c>!E</c>
    /// error type is more specific than the annotation that spells it.
    /// </summary>
    private static IEnumerable<TypeAnnotation> ContainingAnnotations(
        TypeAnnotation annotation, int line, int col)
    {
        if (!ContainsPosition(annotation, line, col))
            yield break;

        foreach (var argument in annotation.TypeArguments)
        {
            foreach (var inner in ContainingAnnotations(argument, line, col))
                yield return inner;
        }

        if (annotation.ErrorType != null)
        {
            foreach (var inner in ContainingAnnotations(annotation.ErrorType, line, col))
                yield return inner;
        }

        yield return annotation;
    }

    /// <summary>
    /// Whether a 1-based compiler position is inside an annotation's recorded extent.
    /// <c>ColumnEnd</c> is the parser's exclusive end (token column + token length).
    /// </summary>
    private static bool ContainsPosition(TypeAnnotation annotation, int line, int col)
    {
        if (line < annotation.LineStart || line > annotation.LineEnd)
            return false;
        if (line == annotation.LineStart && col < annotation.ColumnStart)
            return false;
        if (line == annotation.LineEnd && col >= annotation.ColumnEnd)
            return false;
        return true;
    }

    private static TypeSymbol? GetTypeSymbol(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Symbol,
            GenericType gt => gt.GenericDefinition,
            NullableType nt => GetTypeSymbol(nt.UnderlyingType),
            OptionalType ot => GetTypeSymbol(ot.UnderlyingType),
            // No ConstructorReferenceType arm: since the tier-2 alias retirement (#1248) no binding
            // carries the transient carrier, so a carrier-typed node here would be a checker bug —
            // fall through to null rather than silently resolving it (delete-don't-strand, plan-d3207d).
            _ => null
        };
    }

    protected override TypeDefinitionRegistrationOptions CreateRegistrationOptions(
        TypeDefinitionCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TypeDefinitionRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
