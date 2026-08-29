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
/// Generates C# code using Roslyn syntax trees.
///
/// Name Resolution:
/// All names resolve through Symbol.CodeGenInfo, which is computed during semantic
/// analysis. Module-level symbols, local variables (including redeclarations), constants,
/// functions, types, and imports all have CodeGenInfo with pre-computed CSharpName and
/// Version. TargetBinding (node-keyed in SemanticInfo) tells binding sites whether to
/// emit a declaration or an assignment.
/// - Type detection (class/struct instantiation): Use SymbolTable lookup
/// - String enum detection: Use CodeGenInfo.IsStringEnum
/// </summary>
[NotThreadSafe(Reason = "Maintains mutable emission state; create per-file instance")]
internal partial class RoslynEmitter : ICodeEmitter
{
    private readonly CodeGenContext _context;
    private readonly TypeSyntaxMapper _typeMapper;
    private readonly NameResolutionService _nameResolutionService;
    private readonly CancellationToken _cancellationToken;
    // Note: Local scope tracking fields (_declaredVariables, _variableVersions,
    // _slotSpellings, _sourceVariableNames, _constVariables, _localFunctionNames,
    // _localVariableTypes) were deleted when all locals gained pre-computed
    // CodeGenInfo and TargetBinding (#1560, #1647).

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

    // Note: _classNames, _structNames, and _stringEnumNames tracking sets were removed.
    // Type detection is now done via SymbolTable lookup (for classes/structs) and
    // CodeGenInfo.IsStringEnum (for string enums). This information is populated
    // during semantic analysis.

    private readonly DunderCodeGenRegistry _dunderRegistry;
    private readonly Dictionary<string, InterfaceDef> _interfaceDefinitions = new(); // Track interface definitions for abstract class stub generation
    private int _tempVarCounter = 0;

    /// <summary>
    /// Set only by a test-side <see cref="ICodeEmitterFactory"/>; null in every production compile
    /// (#1334). See <see cref="IExpressionGenerationRecorder"/>.
    /// </summary>
    private IExpressionGenerationRecorder? _generationRecorder;

    /// <summary>Installs the re-entry recorder. Test seam; see <see cref="IExpressionGenerationRecorder"/>.</summary>
    internal void SetGenerationRecorder(IExpressionGenerationRecorder recorder)
        => _generationRecorder = recorder;

    /// <summary>
    /// Tracks statements that need to be hoisted before the containing statement.
    /// Used by walrus operator (:=) for variable declarations and by multi-for
    /// comprehensions for imperative loop codegen. Populated during expression
    /// generation, consumed and cleared during statement generation.
    /// </summary>
    private readonly List<StatementSyntax> _hoistedStatements = new();

    /// <summary>
    /// Pool of scratch statement buffers reused by <see cref="GenerateSuiteBlock"/> when
    /// building a <c>BlockSyntax</c>. Instance-owned: the emitter is single-threaded per
    /// compilation and the pooled list is always copied into the block before it is returned.
    /// </summary>
    private readonly Utilities.ScratchListPool<StatementSyntax> _statementListPool = new();

    /// <summary>
    /// When true, walrus expressions emit inline assignment expressions (varName = value)
    /// instead of hoisted declarations. Used in while-loop conditions where the assignment
    /// must be re-evaluated on each iteration.
    /// </summary>
    private bool _walrusInlineMode;

    /// <summary>
    /// Typed variable declarations (no initializer) for walrus variables used in inline mode.
    /// These are emitted before the while loop: <c>int val;</c>
    /// </summary>
    private readonly List<LocalDeclarationStatementSyntax> _walrusPreDeclarations = new();

    // Resolved return type of the function/method currently being generated.
    // Used so that a bare `return None` against an Optional<T> return type emits
    // Optional<T>.None rather than a bare `null` (which won't convert to the struct).
    private SemanticType? _currentReturnType;

    // Track if the current method being generated is a generator (contains yield statements).
    // When true, GenerateReturn emits yield break instead of return, and the method's
    // return type is wrapped in IEnumerable<T> or IEnumerator<T>.
    private bool _isCurrentMethodGenerator;

    // Track if the current method being generated is async.
    // When true, GenerateYield emits 'await foreach' for async iterables in yield from.
    private bool _isCurrentMethodAsync;

    // Track if the current function/method being generated is decorated with @test.
    // When true, assert statements are rewritten to xUnit assertions instead of Debug.Assert.
    private bool _isInTestFunction;

    // Track if the current class being generated inherits from unittest.TestCase.
    // When true, setup/teardown methods become private (called from synthesized
    // constructor/Dispose) instead of being exposed publicly.
    private bool _isInTestCaseClass;

    // Collects module-level @test functions during module member generation so they can
    // be emitted into a sibling test class instead of the regular module class.
    private readonly List<FunctionDef> _pendingTestFunctions = new();

    // The resolved module class name (e.g., "TestParametrize" or "Program"). Set early in
    // GenerateModuleMembers so [MemberData] attributes generated for
    // @test.parametrize(VARIABLE) decorators can reference the module class via MemberType.
    private string? _resolvedModuleClassName;

    // Module-level variable names (original Sharpy names) referenced by
    // @test.parametrize(VARIABLE) decorators. Populated by a pre-scan in
    // GenerateModuleMembers; each entry gets a companion MemberData wrapper property
    // on the module class satisfying xUnit's IEnumerable<object[]> contract.
    private readonly HashSet<string> _memberDataVariables = new();

    // Collects module-level @test.fixture functions during module member generation. Each is
    // emitted as a sibling fixture class (parameterless constructor for setup, optional
    // IDisposable for yield-based teardown). Test methods consuming these by parameter name
    // are wired up via IClassFixture<T>.
    private readonly List<FunctionDef> _pendingFixtures = new();

    // In library mode (non-entry-point files), top-level type declarations (class/struct/
    // interface/enum/union) are extracted out of the static module class and emitted as
    // sibling types annotated with [SharpyModuleType]. GenerateModuleMembers populates this
    // list; GenerateCompilationUnit emits them at namespace level (wrapped in the same
    // directory-wrapper hierarchy as the module class for multi-file isolation).
    private readonly List<MemberDeclarationSyntax> _extractedTypes = new();

    // Maps fixture function name (e.g., "db_connection") → metadata describing the generated
    // C# fixture class (class name, return type, field name in consuming test classes).
    // Populated as fixtures are emitted, consulted by test method generation to detect
    // fixture-consuming parameters.
    private readonly Dictionary<string, FixtureInfo> _fixtureRegistry = new();

    // Track the current TypeSymbol being generated (for IEquatable virtual detection, etc.)
    private TypeSymbol? _currentTypeSymbol;

    // When set, `self` maps to this identifier instead of `this`.
    // Used for inlining dunder bodies into static operators (self → left/value).
    private string? _selfReplacementIdentifier;

    // When set, identifier references to Source are rewritten to Target inside an accessor body.
    // Sharpy lets an accessor NAME its incoming value; C# does not — a setter and an event
    // accessor both receive an implicit `value`, and nothing declares the Sharpy spelling. So the
    // name is a mapping, not a declaration. Three shapes share the rule:
    //   - event add/remove: the handler parameter → `value`
    //   - property set/init: the value parameter → `value` (#1405)
    //   - property observers (#416): before_set's parameter → `value`, after_set's → the captured
    //     old-value local
    // One field rather than one per shape: it was two, and the third was about to be added when
    // #1405 showed that "the rule exists for one arm and is missing for its siblings" is the defect
    // class this whole area keeps producing.
    private (string Source, string Target)? _accessorParamRewrite;

    /// <summary>
    /// Opens an accessor-parameter rewrite for the duration of one body's generation: while it is
    /// alive, identifier references to <paramref name="source"/> emit as <paramref name="target"/>.
    /// Restores the previous rewrite on dispose, so nesting (an observer inside a setter) is safe.
    /// A null or empty <paramref name="source"/> installs nothing — accessors with no named value
    /// parameter take the same code path without a special case.
    /// </summary>
    private IDisposable AccessorParamRewrite(string? source, string target)
    {
        var previous = _accessorParamRewrite;
        if (!string.IsNullOrEmpty(source))
            _accessorParamRewrite = (source!, target);
        return new AccessorParamRewriteScope(this, previous);
    }

    /// <summary>
    /// The C# slot carrying the accessor's incoming value when <paramref name="name"/> IS that
    /// value's Sharpy spelling under an open rewrite, else null. Write positions consult this so a
    /// rebinding assignment lands on the same slot the read side maps onto (#1500).
    /// </summary>
    private string? AccessorParamSlotName(Identifier name)
        => _accessorParamRewrite is { } rewrite
            && string.Equals(name.Name, rewrite.Source, StringComparison.Ordinal)
            ? rewrite.Target
            : null;

    /// <summary>
    /// Suspends an open accessor-parameter rewrite for the duration of a scope that RE-BINDS the
    /// rewritten name, restoring it on dispose. A lambda parameter, a nested <c>def</c> parameter or
    /// a comprehension target named like the accessor's value parameter declares its OWN slot; the
    /// mapping onto C#'s <c>value</c> describes only the accessor's own binding, so it must not
    /// reach inside (#1500 — measured before the guard: CPython 106, Sharpy 300, no diagnostic).
    ///
    /// <para>Keyed on the binder's bound names rather than on the resolved symbol deliberately.
    /// Symbol identity is NOT usable for this decision: assigning to the accessor's value parameter
    /// makes the checker define a fresh <c>VariableSymbol</c> for the name
    /// (<c>TypeChecker.Statements.cs:178-195</c>), so reads after a reassignment resolve to a
    /// symbol that is not the accessor's parameter yet must still be rewritten. Shadowing is a
    /// property of the binder, and the binder is exactly what this scope wraps.</para>
    /// </summary>
    private IDisposable SuspendAccessorParamRewriteIfShadowed(IEnumerable<string>? boundNames)
    {
        var previous = _accessorParamRewrite;
        if (previous is { } rewrite && boundNames != null
            && boundNames.Any(n => string.Equals(n, rewrite.Source, StringComparison.Ordinal)))
        {
            _accessorParamRewrite = null;
        }

        return new AccessorParamRewriteScope(this, previous);
    }

    private sealed class AccessorParamRewriteScope : IDisposable
    {
        private readonly RoslynEmitter _emitter;
        private readonly (string Source, string Target)? _previous;
        private bool _disposed;

        public AccessorParamRewriteScope(RoslynEmitter emitter, (string Source, string Target)? previous)
        {
            _emitter = emitter;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _emitter._accessorParamRewrite = _previous;
        }
    }

    /// <summary>
    /// Resets per-method state for a new method/function scope.
    /// Call at the start of each method, function, property accessor, or operator body.
    /// </summary>
    private void ResetMethodScope(FunctionDef? funcDef = null)
    {
        // Resolve the current return type so `return None` can target Optional<T>.None.
        _currentReturnType = funcDef?.ReturnType != null
            ? _context.SemanticInfo?.GetTypeAnnotation(funcDef.ReturnType)
            : null;
    }

    /// <summary>
    /// Sets the generator scope flag and returns a disposable that restores the previous value.
    /// </summary>
    private GeneratorScope SetGeneratorScope(bool isGenerator)
    {
        var previous = _isCurrentMethodGenerator;
        _isCurrentMethodGenerator = isGenerator;
        return new GeneratorScope(this, previous);
    }

    private readonly struct GeneratorScope : IDisposable
    {
        private readonly RoslynEmitter _emitter;
        private readonly bool _previous;

        public GeneratorScope(RoslynEmitter emitter, bool previous)
        {
            _emitter = emitter;
            _previous = previous;
        }

        public void Dispose() => _emitter._isCurrentMethodGenerator = _previous;
    }

    /// <summary>
    /// Sets the async scope flag and returns a disposable that restores the previous value.
    /// </summary>
    private AsyncScope SetAsyncScope(bool isAsync)
    {
        var previous = _isCurrentMethodAsync;
        _isCurrentMethodAsync = isAsync;
        return new AsyncScope(this, previous);
    }

    private readonly struct AsyncScope : IDisposable
    {
        private readonly RoslynEmitter _emitter;
        private readonly bool _previous;

        public AsyncScope(RoslynEmitter emitter, bool previous)
        {
            _emitter = emitter;
            _previous = previous;
        }

        public void Dispose() => _emitter._isCurrentMethodAsync = _previous;
    }

    /// <summary>
    /// Wraps a type T in System.Collections.Generic.IEnumerable&lt;T&gt;.
    /// Used for standalone generator functions whose annotated return type is the element type.
    /// </summary>
    private static NameSyntax WrapInIEnumerable(TypeSyntax elementType)
    {
        return QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                IdentifierName("Generic")),
            GenericName("IEnumerable")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));
    }

    /// <summary>
    /// Wraps a type T in System.Collections.Generic.IAsyncEnumerable&lt;T&gt;.
    /// Used for async generator functions (async def with yield).
    /// </summary>
    private static NameSyntax WrapInIAsyncEnumerable(TypeSyntax elementType)
    {
        return QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                IdentifierName("Generic")),
            GenericName("IAsyncEnumerable")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));
    }

    /// <summary>
    /// Wraps a type T in System.Collections.Generic.IEnumerator&lt;T&gt;.
    /// Used for generator __iter__ (GetEnumerator) and __reversed__ (GetReverseEnumerator).
    /// </summary>
    private static NameSyntax WrapInIEnumerator(TypeSyntax elementType)
    {
        return QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                IdentifierName("Generic")),
            GenericName("IEnumerator")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));
    }

    /// <summary>
    /// Wraps a type T in System.Threading.Tasks.Task&lt;T&gt;.
    /// Used for async functions whose annotated return type is the result type.
    /// </summary>
    private static NameSyntax WrapInTask(TypeSyntax resultType)
    {
        return QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Threading")),
                IdentifierName("Tasks")),
            GenericName("Task")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(resultType))));
    }

    /// <summary>
    /// Returns System.Threading.Tasks.Task (no type parameter).
    /// Used for async void functions.
    /// </summary>
    private static NameSyntax TaskType()
    {
        return QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Threading")),
                IdentifierName("Tasks")),
            IdentifierName("Task"));
    }

    /// <summary>
    /// Returns true when the given TypeSyntax represents the <c>void</c> keyword.
    /// Used to distinguish <c>async def f() -> None</c> (which must map to <c>Task</c>)
    /// from <c>async def f() -> int</c> (which must map to <c>Task&lt;long&gt;</c>).
    /// </summary>
    private static bool IsVoidType(TypeSyntax type)
    {
        return type is PredefinedTypeSyntax predefined
            && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
    }

    // Maps original parameter base names (camelCase) to C# replacement names.
    // Used for inlined operator bodies: e.g., "other" → "right".
    private Dictionary<string, string>? _parameterNameOverrides;

    public RoslynEmitter(CodeGenContext context, CancellationToken cancellationToken = default)
    {
        _context = context;
        _typeMapper = new TypeSyntaxMapper(context);
        _nameResolutionService = new NameResolutionService(context.Logger);
        _cancellationToken = cancellationToken;
        _dunderRegistry = BuildDunderRegistry();
    }

    /// <summary>
    /// Build a fully-qualified name using the global:: alias qualifier via explicit Roslyn syntax nodes.
    /// ParseName("global::X.Y") misparses "global" as a regular identifier instead of the alias qualifier,
    /// which breaks in constrained expression contexts (e.g., f-string interpolation holes).
    /// </summary>
    /// <param name="parts">The namespace/type segments after global:: (e.g., "Sharpy", "Builtins", "Len").</param>
    internal static NameSyntax MakeGlobalQualifiedName(params string[] parts)
    {
        NameSyntax name = AliasQualifiedName(
            IdentifierName(Token(SyntaxKind.GlobalKeyword)),
            EscapedIdentifierName(parts[0]));
        for (int i = 1; i < parts.Length; i++)
            name = QualifiedName(name, EscapedIdentifierName(parts[i]));
        return name;
    }

    /// <summary>
    /// Parses a C# name that may carry a <c>global::</c> alias prefix into proper Roslyn syntax. A
    /// plain (non-<c>global::</c>) name goes through <see cref="ParseName"/> unchanged — it tokenizes
    /// reparse-equivalently. A <c>global::</c>-prefixed name is rebuilt with a real
    /// <see cref="AliasQualifiedNameSyntax"/> on its leftmost segment (via <see cref="Globalize"/>),
    /// because <c>ParseName("global::X.Y")</c> mis-tokenizes <c>global</c> as an ordinary identifier —
    /// the tree prints correctly but fails to bind when handed straight to
    /// <c>CSharpSyntaxTree.Create</c> (#1095). Output text is identical either way, so snapshots are
    /// unaffected.
    /// </summary>
    internal static NameSyntax ParseQualifiedName(string name) =>
        name.StartsWith("global::", StringComparison.Ordinal)
            ? Globalize((NameSyntax)ParseName(name["global::".Length..])) // conformance:allow-raw-parse — sanctioned wrapper
            : ParseName(name); // conformance:allow-raw-parse — sanctioned wrapper

    /// <summary>
    /// Type-position counterpart of <see cref="ParseQualifiedName"/>: parses a C# type name that may
    /// carry a <c>global::</c> alias prefix (including generic type names such as
    /// <c>global::System.Collections.Generic.IEnumerable&lt;object[]&gt;</c>) into reparse-equivalent
    /// syntax (#1095). Non-<c>global::</c> type names pass through <see cref="ParseTypeName"/>.
    /// </summary>
    internal static TypeSyntax ParseQualifiedTypeName(string name) =>
        name.StartsWith("global::", StringComparison.Ordinal)
            ? Globalize((NameSyntax)ParseTypeName(name["global::".Length..])) // conformance:allow-raw-parse — sanctioned wrapper
            : ParseTypeName(name); // conformance:allow-raw-parse — sanctioned wrapper

    /// <summary>
    /// Rewrites the leftmost simple-name segment of <paramref name="name"/> (walking the left spine of
    /// nested <see cref="QualifiedNameSyntax"/>) into a <c>global::</c> alias-qualified name. This is
    /// the correct structural form of a global-qualified name: <c>global::A.B.C</c> is
    /// <c>QualifiedName(QualifiedName(AliasQualifiedName(global, A), B), C)</c>, so only the leftmost
    /// identifier gets the alias qualifier.
    /// </summary>
    private static NameSyntax Globalize(NameSyntax name)
    {
        var leftmost = LeftmostSimpleName(name);
        var aliased = AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), leftmost);
        return leftmost == name ? aliased : name.ReplaceNode(leftmost, aliased);
    }

    /// <summary>Returns the leftmost <see cref="SimpleNameSyntax"/> on a name's left spine.</summary>
    private static SimpleNameSyntax LeftmostSimpleName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => LeftmostSimpleName(qualified.Left),
        SimpleNameSyntax simple => simple,
        AliasQualifiedNameSyntax alias => alias.Name,
        _ => throw new InvalidOperationException(
            $"Cannot global-qualify name of kind {name.Kind()}: {name.ToFullString()}"),
    };

    /// <summary>
    /// Builds the array type for a variadic (<c>params</c>) parameter with a parser-shaped rank
    /// specifier. The rank must carry an <see cref="OmittedArraySizeExpressionSyntax"/> — a bare
    /// <c>ArrayRankSpecifier()</c> has an empty size list, which prints identically (<c>T[]</c>)
    /// but binds as a different array shape under direct tree handoff (CS0225, #1095).
    /// </summary>
    private static ArrayTypeSyntax VariadicArrayType(TypeSyntax elementType) =>
        ArrayType(elementType)
            .WithRankSpecifiers(SingletonList(ArrayRankSpecifier(
                SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression()))));

    /// <summary>
    /// Builds an <see cref="IdentifierNameSyntax"/> from a C# name that may be a verbatim
    /// (<c>@</c>-escaped) identifier. <c>SyntaxFactory.Identifier("@default")</c> gives the token
    /// ValueText <c>"@default"</c>, but Roslyn's parser produces a verbatim identifier whose
    /// ValueText is <c>"default"</c> (Text <c>"@default"</c>). That mismatch makes an <c>@</c>-escaped
    /// name — e.g. a <c>NameColon</c> argument name — resolve to the wrong thing (CS1739) when the
    /// emitter tree is handed straight to <c>CSharpSyntaxTree.Create</c> (#1095). Splitting Text and
    /// ValueText makes the token reparse-equivalent; the printed text is unchanged.
    /// </summary>
    internal static IdentifierNameSyntax EscapedIdentifierName(string csharpName) =>
        IdentifierName(EscapedIdentifier(csharpName));

    /// <summary>
    /// Token counterpart of <see cref="EscapedIdentifierName"/>: builds an identifier
    /// <see cref="SyntaxToken"/> from a C# name that may be a verbatim (<c>@</c>-escaped) identifier,
    /// with the same Text/ValueText split Roslyn's parser produces (Text <c>"@default"</c>, ValueText
    /// <c>"default"</c>). Used for declaration positions (parameters, variable declarators, variable
    /// designations, tuple element names) so an <c>@</c>-escaped name binds the same way its
    /// references do under direct <c>CSharpSyntaxTree.Create</c> handoff (#1095). Printed text is
    /// unchanged. A plain name passes through <see cref="Identifier(string)"/> unchanged.
    /// </summary>
    internal static SyntaxToken EscapedIdentifier(string csharpName) =>
        csharpName.StartsWith("@", StringComparison.Ordinal)
            ? Identifier(TriviaList(), SyntaxKind.IdentifierToken, csharpName, csharpName[1..], TriviaList())
            : Identifier(csharpName);

    /// <summary>
    /// Identifier token for a type-parameter name, which — unlike every other name reaching the
    /// emitter — arrives WITHOUT the <c>@</c> already applied (#1327).
    /// </summary>
    /// <remarks>
    /// <see cref="EscapedIdentifier"/> preserves an escape that is already there; it does not add
    /// one. Every other declaration position gets its <c>@</c> from name mangling upstream, but a
    /// type parameter's name is emitted verbatim from the AST, so <c>class Box[`class`]</c> produced
    /// <c>class Box&lt;class&gt;</c> — 24 syntax errors from one token. This applies the escape at
    /// the point where the name becomes C#.
    /// </remarks>
    internal static SyntaxToken TypeParameterIdentifier(string name) =>
        EscapedIdentifier(CSharpKeywords.EscapeIfNeeded(name));

    /// <summary>Name-syntax counterpart of <see cref="TypeParameterIdentifier"/>.</summary>
    internal static IdentifierNameSyntax TypeParameterIdentifierName(string name) =>
        IdentifierName(TypeParameterIdentifier(name));

    /// <summary>
    /// Resolve the C# name for a variable using CodeGenInfo. Returns null only when the symbol
    /// has no CodeGenInfo at all (unresolved references). Since #1560, all locals have CodeGenInfo.
    /// </summary>
    private string? TryGetCSharpNameFromCodeGenInfo(string sharpyName, bool isNewDeclaration)
    {
        var symbol = _context.LookupSymbol(sharpyName);
        if (symbol == null)
            return null;

        var info = GetCodeGenInfo(symbol);
        if (info == null)
            return null;

        // Delegate to NameResolutionService for CodeGenInfo-based resolution
        return _nameResolutionService.TryResolveFromCodeGenInfo(
            symbol,
            info,
            isNewDeclaration,
            _forceModuleLevelFields);
    }

    /// <summary>
    /// Get the mangled variable name for a Sharpy name. After #1560 all local variables have
    /// pre-computed CodeGenInfo, so this method no longer tracks slot state. The resolution
    /// order is: parameter overrides, TypeSymbol/ModuleSymbol lookup, CodeGenInfo.
    /// </summary>
    /// <param name="name">The original Sharpy variable name</param>
    /// <param name="isNewDeclaration">Ignored for locals (TargetBinding decides); kept for module-level CodeGenInfo.</param>
    /// <param name="isBacktickEscaped">
    /// True when the source spelled this name backtick-escaped. Purely a syntactic property of the
    /// token — the same one already read at the call and member sites — not a resolution decision
    /// made here.
    /// </param>
    /// <returns>The C# variable name (possibly versioned, e.g. "x", "x_1", "x_2")</returns>
    private string GetMangledVariableName(Identifier id, bool isNewDeclaration)
        => ResolveViaNodeKeyedSymbol(_context.SemanticInfo?.GetIdentifierSymbol(id), isNewDeclaration)
           ?? GetMangledVariableName(id.Name, isNewDeclaration, id.IsNameBacktickEscaped);

    private string GetMangledVariableName(VariableDeclaration varDecl, bool isNewDeclaration)
        => ResolveViaNodeKeyedSymbol(_context.SemanticInfo?.GetDeclarationSymbol(varDecl), isNewDeclaration)
           ?? (varDecl.IsConst
               // No symbol (AST-only unit tests): the syntactic const casing, as the allocator
               // would have recorded it (NameCasing.ResolveConstant, version 0).
               ? NameCasing.ResolveConstant(varDecl.Name, varDecl.IsNameBacktickEscaped)
               : GetMangledVariableName(varDecl.Name, isNewDeclaration, varDecl.IsNameBacktickEscaped));

    private string? ResolveViaNodeKeyedSymbol(Symbol? symbol, bool isNewDeclaration)
    {
        if (symbol == null)
            return null;
        var info = GetCodeGenInfo(symbol);
        if (info == null)
            return null;
        var resolved = _nameResolutionService.TryResolveFromCodeGenInfo(
            symbol, info, isNewDeclaration, _forceModuleLevelFields)
            ?? info.GetVersionedCSharpName();
        if (!isNewDeclaration && _parameterNameOverrides != null
            && _parameterNameOverrides.TryGetValue(resolved, out var overrideName))
            return overrideName;
        return resolved;
    }

    private string GetMangledVariableName(string name, bool isNewDeclaration, bool isBacktickEscaped = false)
    {
        var baseName = _nameResolutionService.GetBaseName(name, isBacktickEscaped);

        // Check parameter name overrides (used for inlined operator bodies: "other" → "right")
        if (_parameterNameOverrides != null
            && !isNewDeclaration
            && _parameterNameOverrides.TryGetValue(baseName, out var overrideName))
        {
            return overrideName;
        }

        // Look up the symbol to check its kind
        var symbol = _context.LookupSymbol(name);

        // Check if this is a REFERENCE to a class or struct name - preserve PascalCase.
        if (symbol is TypeSymbol typeSymbol &&
            !isNewDeclaration &&
            (typeSymbol.TypeKind == Semantic.TypeKind.Class ||
             typeSymbol.TypeKind == Semantic.TypeKind.Struct) &&
            isBacktickEscaped == typeSymbol.IsNameBacktickEscaped)
        {
            return NameCasing.ResolveType(name, typeSymbol.IsNameBacktickEscaped);
        }

        // Check if this is a module symbol - use service for name resolution
        if (symbol is ModuleSymbol moduleSymbol)
        {
            if (moduleSymbol.NetNamespaceName != null)
                return moduleSymbol.NetNamespaceName;
            return NameResolutionService.EscapeCSharpKeyword(name.Replace(".", "_", StringComparison.Ordinal));
        }

        // Try CodeGenInfo-based resolution (handles all locals, module-level vars, imports)
        var codeGenName = TryGetCSharpNameFromCodeGenInfo(name, isNewDeclaration);
        if (codeGenName != null)
            return codeGenName;

        // A variable with no CodeGenInfo is a missing fact, never a spelling to improvise: every
        // local is named by the LocalNameAllocator and every module-level variable by the
        // CodeGenInfoComputer. Silently returning the base name is how a lambda-scoped ledger's
        // locals came out CS0136 behind SPY0908 (#1560 C2).
        if (symbol is VariableSymbol)
            throw MissingLocalCodeGenInfo(name);

        // Nested functions (local defs) that have no CodeGenInfo AND no VariableSymbol
        // resolve via NameCasing. Only fires when the symbol table has a FunctionSymbol
        // and nothing else — a lambda assigned to a variable has a VariableSymbol whose
        // CodeGenInfo was already tried above.
        if (!isNewDeclaration && symbol is FunctionSymbol
            && GetCodeGenInfo(symbol) == null)
            return NameCasing.ResolveMethod(name, isBacktickEscaped);

        // Fallback for names with NO symbol at all (AST-only unit tests, unresolved references).
        return baseName;
    }

    /// <summary>
    /// The one exception for a variable the allocator did not name. Thrown, not logged: an
    /// improvised spelling compiles into the wrong C# local or fails with CS0136/CS0103 behind
    /// SPY0908, and either is worse than a loud compiler bug.
    /// </summary>
    private static InvalidOperationException MissingLocalCodeGenInfo(string name)
        => new($"No CodeGenInfo for local '{name}' — the LocalNameAllocator must name every ledger entry");

    // Note: RegisterLocalSlot, SetSlotVersion, ReleaseLocalSlot, RestoreSlotTable,
    // CarryForwardOuterSlot, SlotState, CaptureSlot, RestoreSlot, SlotAnswersSpelling,
    // TryFindLocalSlot, ProbeLocalSlot, IsLocalSlotInScope were all deleted when
    // locals gained pre-computed CodeGenInfo (#1560, #1647).

    /// <summary>
    /// The C# name a local binding is emitted under, and the key its slot is filed under. Shorthand
    /// for <see cref="NameResolutionService.GetBaseName"/>; every declaration path that computes a
    /// local's base name itself must go through here, or its slot key and its emitted name disagree
    /// for a backtick-escaped spelling (#1357).
    /// </summary>
    private string LocalBaseName(string name, bool isBacktickEscaped)
        => _nameResolutionService.GetBaseName(name, isBacktickEscaped);

    /// <summary>
    /// The C# name a parameter is emitted under. A parameter is a local binding, so the escape
    /// means the same thing there as anywhere else: <c>def f(`Zed`: int)</c> declares <c>Zed</c>,
    /// which is what the body's references resolve to (#1357). Unescaped names are unchanged —
    /// <see cref="NameCasing.ResolveVariable"/>'s plain arm is the <c>ToCamelCase</c> that
    /// <c>NameMangler.Transform(..., NameContext.Parameter)</c> already applied.
    /// </summary>
    private string ParameterCSharpName(Parameter param)
    {
        // A parameter is a ledger entry like every other local (#1560, #1647): a nested def's or a
        // lambda's parameter spelled like an enclosing local is VERSIONED by the allocator, and the
        // declaration must say what the references say. The escape-aware base spelling is only
        // for parameters the checker never bound (AST-only unit tests).
        var symbol = _context.SemanticInfo?.GetParameterSymbol(param);
        return symbol != null
            ? GetCSharpNameForSymbol(symbol)
            : NameCasing.ResolveVariable(param.Name, param.IsNameBacktickEscaped);
    }

    /// <summary>
    /// The C# name for a parameter known only through its SEMANTIC symbol — constructor and
    /// module-class forwarders, delegate stubs.
    /// </summary>
    /// <remarks>
    /// <see cref="ParameterSymbol"/> now carries <see cref="ParameterSymbol.IsNameBacktickEscaped"/>
    /// (#1455), so this resolves through the same escape-aware helper as the AST overload: a
    /// <c>`Zed`</c> parameter keeps its verbatim spelling across a forwarder, which declares AND
    /// passes the parameter through this one string, so both ends agree.
    /// </remarks>
    private static string ParameterCSharpName(ParameterSymbol param)
        => NameCasing.ResolveVariable(param.Name, param.IsNameBacktickEscaped);

    // Note: CollectSourceVariableNames, CollectSourceVariableNamesFromStatement,
    // CollectVariableNamesFromExpression, CollectVariableNamesFromPattern were all
    // deleted — the LocalNameAllocator pre-scans during semantic analysis (#1560, #1647).

    // ============================================================
    // CodeGenInfo helper methods
    //
    // These methods read CodeGenInfo via SemanticBinding — the sole store
    // since MaterializeCodeGenInfo folds bridge tables back into
    // _codeGenInfo instead of writing Symbol.CodeGenInfo (#1633).
    // ============================================================

    /// <summary>
    /// Get CodeGenInfo for a symbol from SemanticBinding.
    /// </summary>
    private CodeGenInfo? GetCodeGenInfo(Symbol symbol)
        => _context.SemanticBinding.GetCodeGenInfo(symbol);

    /// <summary>
    /// Gets the original CLR method name for a symbol, if it is a discovery-loaded
    /// CLR method. Prefers the materialized CodeGenInfo, falling back to the
    /// FunctionSymbol property. Returns null for user-defined or unresolved symbols,
    /// in which case code generation applies normal name mangling.
    /// </summary>
    private string? GetClrMethodName(Symbol? symbol)
    {
        if (symbol is null)
            return null;
        return GetCodeGenInfo(symbol)?.ClrMethodName
            ?? (symbol as FunctionSymbol)?.ClrMethodName;
    }

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
    /// <remarks>
    /// Since #1560, every local variable has CodeGenInfo assigned by LocalNameAllocator.
    /// A VariableSymbol without CodeGenInfo is an allocator bug and throws.
    /// </remarks>
    private string GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)
    {
        var info = GetCodeGenInfo(symbol);
        if (info != null)
        {
            // Route through TryResolveFromCodeGenInfo to honour _forceModuleLevelFields
            // (module-level vars with execution-order issues use PascalCase when forced).
            var resolved = _nameResolutionService.TryResolveFromCodeGenInfo(
                symbol, info, isNewDeclaration, _forceModuleLevelFields);
            if (resolved != null)
                return resolved;
            return info.GetVersionedCSharpName();
        }

        // For non-variable symbols, delegate to NameResolutionService
        if (symbol.Kind != Semantic.SymbolKind.Variable)
        {
            return _nameResolutionService.ResolveName(symbol, codeGenInfo: null);
        }

        // A variable without CodeGenInfo is a missing fact (#1560 C2). Not a NameCasing fallback:
        // that is exactly the silent path that spelled a lambda-ledger local from nothing.
        throw MissingLocalCodeGenInfo(symbol.Name);
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

    // ============================================================
    // Parameter reordering helpers
    //
    // C# requires: required params before optional, params array last.
    // These methods reorder Sharpy parameters (which may have keyword-only
    // params with defaults before non-keyword-only required params) into
    // C#-compliant order while preserving relative declaration order within
    // each group.
    //
    // Order:
    //   1. Non-variadic, non-keyword-only, required (no default)
    //   2. Keyword-only, required (no default)
    //   3. Non-variadic, non-keyword-only, with default
    //   4. Keyword-only, with default
    //   5. Variadic (params) — always last
    // ============================================================

    /// <summary>
    /// Generic reordering of parameter-like items into C#-compliant order.
    /// Required parameters come before optional, and the variadic (params) parameter is last.
    /// Declaration order within each group is preserved.
    /// </summary>
    /// <typeparam name="T">The parameter type (AST <see cref="Parameter"/> or <see cref="ParameterSymbol"/>).</typeparam>
    /// <param name="items">Parameters to reorder.</param>
    /// <param name="isVariadic">Returns true if the item is a variadic (params) parameter.</param>
    /// <param name="isKeywordOnly">Returns true if the item is keyword-only.</param>
    /// <param name="hasDefault">Returns true if the item has a default value.</param>
    /// <returns>Items in C#-compliant order.</returns>
    private static T[] ReorderForCSharp<T>(
        IEnumerable<T> items,
        Func<T, bool> isVariadic,
        Func<T, bool> isKeywordOnly,
        Func<T, bool> hasDefault)
    {
        var itemList = items as IList<T> ?? items.ToList();

        // Fast path: if no variadic and no keyword-only params, no reordering needed
        bool foundVariadic = false;
        bool foundKeywordOnly = false;
        foreach (var item in itemList)
        {
            if (isVariadic(item))
                foundVariadic = true;
            if (isKeywordOnly(item))
                foundKeywordOnly = true;
        }

        if (!foundVariadic && !foundKeywordOnly)
            return itemList as T[] ?? itemList.ToArray();

        var normalRequired = new List<T>();
        var keywordOnlyRequired = new List<T>();
        var normalOptional = new List<T>();
        var keywordOnlyOptional = new List<T>();
        T? variadic = default;
        bool hasVariadicItem = false;

        foreach (var item in itemList)
        {
            if (isVariadic(item))
            {
                variadic = item;
                hasVariadicItem = true;
            }
            else if (isKeywordOnly(item))
            {
                if (hasDefault(item))
                    keywordOnlyOptional.Add(item);
                else
                    keywordOnlyRequired.Add(item);
            }
            else
            {
                if (hasDefault(item))
                    normalOptional.Add(item);
                else
                    normalRequired.Add(item);
            }
        }

        var capacity = normalRequired.Count + keywordOnlyRequired.Count
                     + normalOptional.Count + keywordOnlyOptional.Count
                     + (hasVariadicItem ? 1 : 0);
        var result = new T[capacity];
        int i = 0;
        foreach (var p in normalRequired)
            result[i++] = p;
        foreach (var p in keywordOnlyRequired)
            result[i++] = p;
        foreach (var p in normalOptional)
            result[i++] = p;
        foreach (var p in keywordOnlyOptional)
            result[i++] = p;
        if (hasVariadicItem)
            result[i++] = variadic!;
        return result;
    }

    /// <summary>
    /// Reorders AST <see cref="Parameter"/> nodes into C#-compliant order.
    /// </summary>
    private static Parameter[] ReorderParametersForCSharp(IEnumerable<Parameter> parameters)
        => ReorderForCSharp(
            parameters,
            static p => p.IsVariadic,
            static p => p.Kind == ParameterKind.KeywordOnly,
            static p => p.DefaultValue != null);

    /// <summary>
    /// Reorders <see cref="ParameterSymbol"/> instances into C#-compliant order.
    /// </summary>
    private static ParameterSymbol[] ReorderParameterSymbolsForCSharp(IEnumerable<ParameterSymbol> parameters)
        => ReorderForCSharp(
            parameters,
            static p => p.IsVariadic,
            static p => p.IsKeywordOnly,
            static p => p.HasDefault);

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

    /// <summary>
    /// Returns true if the lambda expression has any parameters with default values.
    /// C# delegates / Func&lt;&gt; don't support optional parameters, so lambdas with defaults
    /// must be hoisted to local functions.
    /// </summary>
    private static bool HasDefaultParameters(LambdaExpression lambda)
        => lambda.Parameters.Any(p => p.DefaultValue != null);

    /// <summary>
    /// Emits a diagnostic for a not-yet-implemented feature in code generation and returns
    /// a <c>default</c> literal as a safe placeholder expression. The diagnostic error ensures
    /// compilation reports failure, so this code should never execute.
    /// </summary>
    private ExpressionSyntax EmitNotImplementedExpression(string message, string code, int? line = null, int? column = null)
    {
        _context.AddError(message, code, line, column);
        return LiteralExpression(SyntaxKind.DefaultLiteralExpression);
    }

    /// <summary>
    /// Emits a diagnostic for a not-yet-implemented feature in code generation and returns
    /// an empty statement as a safe fallback.
    /// </summary>
    private StatementSyntax EmitNotImplementedStatement(string message, string code, int? line = null, int? column = null)
    {
        _context.AddError(message, code, line, column);
        return EmptyStatement();
    }

    private static SyntaxKind GetAccessModifierFromNameConvention(string memberName)
    {
        return AccessLevelConventions.FromName(memberName) switch
        {
            AccessLevel.Private => SyntaxKind.PrivateKeyword,
            AccessLevel.Protected => SyntaxKind.ProtectedKeyword,
            _ => SyntaxKind.PublicKeyword,
        };
    }

    /// <summary>
    /// Maps underscore naming convention to access modifiers for module-level functions.
    /// Unlike class members where _name → protected, module-level _name → internal
    /// (assembly-private, matching Python's "module-private" convention).
    /// </summary>
    private static SyntaxKind GetModuleLevelAccessModifier(string functionName)
    {
        var level = AccessLevelConventions.FromName(functionName);
        return level switch
        {
            AccessLevel.Private => SyntaxKind.PrivateKeyword,
            AccessLevel.Protected => SyntaxKind.InternalKeyword,
            _ => SyntaxKind.PublicKeyword,
        };
    }
}
