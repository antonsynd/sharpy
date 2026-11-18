using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: the readiness shape of a Guarded module or <c>@static</c> value
/// (plan-775c50 D4, rule N3) and of the host that sweeps it (D5). The arms are picked by the
/// recorded <see cref="StaticStorage"/> fact (<see cref="CodeGenInfo.StaticStorage"/>,
/// <see cref="PropertySymbol.StaticStorage"/>), never by a type; the run-time half is
/// <c>Sharpy.StaticInit</c> (Sharpy.Core, D3).
/// <para>Per Guarded value <c>N</c> of type <c>T</c>, in place of today's initialized field:</para>
/// <code>
/// private static T N__field = default!;  private static byte N__state;   // the CLR default (F15)
/// private static void N__init() { N__field = &lt;initializer&gt;; }
/// private static void N__slow() =&gt; StaticInit.Read(ref __host, __Sweep, ref N__state, "n", N__init);
/// public static ref T N { get { if (!__Swept.Done) N__slow(); return ref N__field; } }   // ref readonly for a const
/// private static void N__storeSlow(T value) =&gt; StaticInit.Store(ref __host, __Sweep, ref N__state, ref N__field, value);
/// public static T N__set(T value) { if (__Swept.Done) N__field = value; else N__storeSlow(value); return value; }  // every plain store
/// public static ref T N__out(out StrongBox&lt;T&gt;? spill) { ... }                             // an `out` operand
/// public static R N__commit&lt;R&gt;(R result, StrongBox&lt;T&gt;? spill) { ... }                // stores the spill
/// public static void N__ensure() { if (!__Swept.Done) N__slow(); }                          // D6: before the statement
/// public static StrongBox&lt;T&gt;? N__pick() { ... }                                        // D6: an `out` arm, by state
/// </code>
/// In an <c>async def</c> body (D6) a use other than a plain read or store names the backing field
/// after <c>N__ensure()</c>, so the field has the value's own accessibility.
/// A property default keeps <c>get</c> / <c>set</c> with the same calls. Per host, once:
/// <c>__host</c>, <c>__Sweep</c> (every value's <c>StaticInit.Run</c> in declaration order) and
/// <c>__Swept</c>, whose explicit static constructor keeps it from being <c>beforefieldinit</c>.
/// </summary>
internal partial class RoslynEmitter
{
    internal const string HostFieldName = "__host";
    internal const string SweepMethodName = "__Sweep";
    internal const string SweptClassName = "__Swept";
    internal const string GuardedSetSuffix = "__set";
    internal const string GuardedOutSuffix = "__out";
    internal const string GuardedCommitSuffix = "__commit";
    internal const string GuardedEnsureSuffix = "__ensure";
    internal const string GuardedPickSuffix = "__pick";
    internal const string GuardedFieldSuffix = "__field";

    private static string GuardedFieldName(string name) => name + GuardedFieldSuffix;

    /// <summary>
    /// <c>= default!</c>: a Guarded value's storage holds the CLR default its type initializer gives
    /// it, written out so a nullable-enabled consumer (Sharpy.Stdlib, warnings as errors) sees the
    /// field assigned (F15). It is no initializer of the value: the slow path never reads it before
    /// the value's own initializer or a store (D4).
    /// </summary>
    private static EqualsValueClauseSyntax ClrDefaultInitializer()
        => EqualsValueClause(PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
            LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword))));
    private static string GuardedStateName(string name) => name + "__state";
    private static string GuardedInitName(string name) => name + "__init";
    private static string GuardedSlowName(string name) => name + "__slow";
    private static string GuardedStoreSlowName(string name) => name + "__storeSlow";

    /// <summary>
    /// The sweep of the host being emitted (a module, or a class / struct / interface with Guarded
    /// values), in declaration order (D5): one <c>StaticInit.Run</c> per value, and the requirement
    /// of each host it requires at its position: an imported module at its import, a non-generic
    /// nested type at its line (W7). A type with no value is no host: its nested requirements pass
    /// to the sweep around it.
    /// </summary>
    private sealed class StaticHostFrame
    {
        public readonly List<SweepStep> Sweep = new();

        /// <summary>Whether the host declares a value: its sweep runs one.</summary>
        public bool HasValues => Sweep.Any(s => s is RunStep);

        /// <summary>A value's recorded <see cref="CodeGenInfo.IsOnGenericHost"/> (or the property's): a
        /// closed generic type is swept by the first use of one of its values, never at a line.</summary>
        public bool IsGenericHost;

        /// <summary>The module host (always emitted, with its load entry); else a type's.</summary>
        public bool IsModule => TypeName == null;

        /// <summary>The type's C# identifier (a type frame); null for the module's.</summary>
        public string? TypeName;
    }

    /// <summary>One step of a host's sweep.</summary>
    private abstract record SweepStep;

    /// <summary>A value's <c>StaticInit.Run</c>.</summary>
    private sealed record RunStep(StatementSyntax Statement) : SweepStep;

    /// <summary>The requirement of another host, <c>Path.Load()</c>: relative to the host the step is in
    /// (a nested type), or global (an imported module's members class).</summary>
    private sealed record RequireStep(IReadOnlyList<string> Path, string Load, bool IsGlobal) : SweepStep;

    private StaticHostFrame? _staticHost;

    /// <summary>A type's load entry: <c>__Load_&lt;Name&gt;</c>, per type, so a derived host's entry
    /// hides no base host's (CS0108).</summary>
    private static string TypeLoadName(string typeName) => "__Load_" + typeName;

    /// <summary>A module's load entry, <c>public static void __Load()</c>, on every Sharpy module.</summary>
    internal const string ModuleLoadName = "__Load";

    /// <summary>Opens the host frame of a type body (<paramref name="typeName"/>), or of the module
    /// (null); closed by <see cref="CloseStaticHost"/>, which restores the enclosing one.</summary>
    private StaticHostFrame OpenStaticHost(out StaticHostFrame? enclosing, string? typeName)
    {
        enclosing = _staticHost;
        _staticHost = new StaticHostFrame { TypeName = typeName };
        return _staticHost;
    }

    /// <summary>
    /// Closes <paramref name="frame"/> and returns its host members: the module's always; a type's
    /// when it has a value, and then the sweep around it requires a non-generic one at its line
    /// (D5). A type with no value passes its own requirements to the sweep around it.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> CloseStaticHost(StaticHostFrame frame, StaticHostFrame? enclosing)
    {
        _staticHost = enclosing;
        if (frame.IsModule)
        {
            // A module with nothing to sweep (no value, no requirement) still has its entry, which
            // does nothing: no host, sweep or Swept class is emitted for it.
            return frame.Sweep.Count == 0
                ? new[] { ModuleLoadEntry(Block()) }
                : GenerateStaticHostMembers(frame, ModuleLoadName);
        }

        if (!frame.HasValues)
        {
            if (enclosing != null)
            {
                foreach (var step in frame.Sweep.OfType<RequireStep>())
                    enclosing.Sweep.Add(step.IsGlobal ? step : step with { Path = step.Path.Prepend(frame.TypeName!).ToList() });
            }
            return Array.Empty<MemberDeclarationSyntax>();
        }

        if (frame.IsGenericHost)
            return GenerateStaticHostMembers(frame, null);

        var load = TypeLoadName(frame.TypeName!);
        enclosing?.Sweep.Add(new RequireStep(new[] { frame.TypeName! }, load, IsGlobal: false));
        return GenerateStaticHostMembers(frame, load);
    }

    private StaticHostFrame CurrentStaticHost
        => _staticHost ?? throw new InternalCompilerErrorException("RoslynEmitter",
            $"a Guarded value was emitted outside any host (plan-775c50 D5; file='{_context.SourceFilePath}')");

    private static NameSyntax StaticInitName(string member) => MakeGlobalQualifiedName("Sharpy", "StaticInit", member);

    private static ExpressionSyntax SweptDone()
        => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(SweptClassName), IdentifierName("Done"));

    private static ArgumentSyntax RefArgument(string name)
        => Argument(IdentifierName(name)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword));

    /// <summary><c>ref __host, __Sweep</c>: the host's arguments, which every <c>StaticInit</c> entry
    /// that may run the sweep takes first.</summary>
    private static ArgumentSyntax[] HostArguments(params ArgumentSyntax[] rest)
        => new[] { RefArgument(HostFieldName), Argument(IdentifierName(SweepMethodName)) }.Concat(rest).ToArray();

    /// <summary><c>StaticInit.&lt;member&gt;(ref __host, __Sweep, rest...)</c>.</summary>
    private static InvocationExpressionSyntax HostCall(string member, params ArgumentSyntax[] rest)
        => InvocationExpression(StaticInitName(member))
            .WithArgumentList(ArgumentList(SeparatedList(HostArguments(rest))));

    /// <summary><c>StaticInit.Load(ref __host, __Sweep);</c></summary>
    private static StatementSyntax LoadCall() => ExpressionStatement(HostCall("Load"));

    private static SyntaxTokenList PrivateStatic()
        => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword));

    /// <summary>
    /// A helper's access (D10): within the assembly, never wider than its value's. A consumer by
    /// reference names only the value's property, after the module's entry ran, so no helper is
    /// public; a private value's stays private, a protected one's is <c>private protected</c>.
    /// </summary>
    private static SyntaxTokenList HelperAccess(SyntaxKind valueAccess) => valueAccess switch
    {
        SyntaxKind.PrivateKeyword => PrivateStatic(),
        SyntaxKind.ProtectedKeyword => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.StaticKeyword)),
        _ => TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)),
    };

    /// <summary><c>[global::System.Runtime.CompilerServices.CompilerGenerated]</c>, on every member D4
    /// and D5 add (D10): discovery skips a member that carries it.</summary>
    private static AttributeListSyntax CompilerGeneratedAttribute()
        => AttributeList(SingletonSeparatedList(Attribute(
            (NameSyntax)MakeGlobalQualifiedName("System", "Runtime", "CompilerServices", "CompilerGenerated"))));

    private static SyntaxList<AttributeListSyntax> Generated() => SingletonList(CompilerGeneratedAttribute());

    /// <summary><c>[global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]</c>, on the
    /// property a Guarded value's consumers name (D10): the storage discovery reads.</summary>
    private static AttributeListSyntax GuardedStorageAttribute()
        => AttributeList(SingletonSeparatedList(Attribute((NameSyntax)MakeGlobalQualifiedName("Sharpy", "SharpyStaticStorage"))
            .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(AttributeArgument(
                MakeGlobalQualifiedName("Sharpy", "StaticStorageKind", "Guarded")))))));

    /// <summary><c>[CompilerGenerated, global::Sharpy.SharpyLoadEntry(hasSweep)]</c>, on a module's load entry (D10).</summary>
    private static SyntaxList<AttributeListSyntax> LoadEntryAttributes(bool hasSweep)
        => List(new[]
        {
            CompilerGeneratedAttribute(),
            AttributeList(SingletonSeparatedList(Attribute((NameSyntax)MakeGlobalQualifiedName("Sharpy", "SharpyLoadEntry"))
                .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(AttributeArgument(
                    LiteralExpression(hasSweep ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression))))))),
        });

    /// <summary>
    /// The host's own members: <c>__host</c>, <c>__Sweep</c> and <c>__Swept</c> (D5; the documented
    /// shape on <c>Sharpy.StaticInit</c>). Private: a host's sweep is required through its own values.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateStaticHostMembers(StaticHostFrame frame, string? loadName)
    {
        // private static global::Sharpy.StaticInitHost __host;
        yield return FieldDeclaration(VariableDeclaration(MakeGlobalQualifiedName("Sharpy", "StaticInitHost"))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(HostFieldName)))))
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated());

        // private static void __Sweep() { StaticInit.Run(ref a__state, "a", a__init); ... }
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(SweepMethodName))
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated())
            .WithBody(Block(frame.Sweep.Select(step => SweepStatement(step, frame))));

        // private static class __Swept { internal static readonly bool Done; static __Swept() { Done = StaticInit.TryLoad(ref __host, __Sweep); } }
        var done = FieldDeclaration(VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier("Done")))))
            .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));
        var cctor = ConstructorDeclaration(Identifier(SweptClassName))
            .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)))
            .WithBody(Block(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("Done"),
                HostCall("TryLoad")))));
        yield return ClassDeclaration(SweptClassName)
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated())
            .WithMembers(List(new MemberDeclarationSyntax[] { done, cctor }));

        if (loadName == null)
            yield break;

        // internal static void __Load_T() { if (!__Swept.Done) StaticInit.Load(ref __host, __Sweep); }
        if (frame.IsModule)
        {
            yield return ModuleLoadEntry(Block(LoadStatement()));
            yield break;
        }
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(loadName))
            .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithAttributeLists(Generated())
            .WithBody(Block(LoadStatement()));
    }

    /// <summary>
    /// <c>[CompilerGenerated, SharpyLoadEntry(hasSweep)] public static void __Load() { body }</c>: a
    /// module's entry carries its sweep fact for an importer by reference (D10) — the module's own
    /// recorded HasSweep, else true (a load of a host with nothing to sweep returns at once).
    /// </summary>
    private MethodDeclarationSyntax ModuleLoadEntry(BlockSyntax body)
        => MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(ModuleLoadName))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithAttributeLists(LoadEntryAttributes(
                (_context.SourceFilePath is { } sourcePath ? _context.SemanticBinding.GetModuleHasSweep(sourcePath) : null) ?? true))
            .WithBody(body);

    /// <summary><c>if (!__Swept.Done) StaticInit.Load(ref __host, __Sweep);</c></summary>
    private static StatementSyntax LoadStatement()
        => IfStatement(PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SweptDone()), LoadCall());

    /// <summary>A sweep step's statement in <paramref name="frame"/>: a value's run, or a requirement,
    /// spelled from the module's namespace in the module host and relative to a type host.</summary>
    private StatementSyntax SweepStatement(SweepStep step, StaticHostFrame frame) => step switch
    {
        RunStep run => run.Statement,
        RequireStep require => ExpressionStatement(InvocationExpression(MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            RequirementPath(require, frame),
            IdentifierName(require.Load)))),
        _ => throw new InternalCompilerErrorException("RoslynEmitter", $"unknown sweep step {step} (plan-775c50 D5)"),
    };

    private ExpressionSyntax RequirementPath(RequireStep require, StaticHostFrame frame)
    {
        if (require.IsGlobal)
            return MakeGlobalQualifiedName(require.Path.ToArray());
        if (frame.IsModule)
            return MakeGlobalQualifiedName(_moduleShape!.NamespaceParts.Concat(require.Path).ToArray());
        ExpressionSyntax path = EscapedIdentifierName(require.Path[0]);
        foreach (var segment in require.Path.Skip(1))
            path = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, path, EscapedIdentifierName(segment));
        return path;
    }

    /// <summary>One Guarded value's declaration, read from its declaration and its recorded facts.</summary>
    private sealed record GuardedValue(
        string CSharpName,
        string SourceName,
        TypeSyntax Type,
        Expression Initializer,
        SyntaxKind Access,
        bool IsReadOnly,
        SyntaxList<AttributeListSyntax> PropertyAttributes,
        SyntaxList<AttributeListSyntax> FieldAttributes,
        bool IsOnGenericHost);

    /// <summary>
    /// The value's slow path and sweep step, shared by the variable and the property arms:
    /// <c>N__init</c> runs <paramref name="hoisted"/> (the initializer's scope sink, if any), then
    /// stores <paramref name="value"/>. <paramref name="isOnGenericHost"/> is the value's recorded
    /// host fact, which makes its host one a first use sweeps (D5).
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateGuardedStorage(
        string csharpName, string sourceName, string fieldName, TypeSyntax type,
        IReadOnlyList<StatementSyntax> hoisted, ExpressionSyntax value, bool isOnGenericHost,
        SyntaxList<AttributeListSyntax> fieldAttributes, bool declareField = true, SyntaxTokenList? fieldModifiers = null)
    {
        var host = CurrentStaticHost;
        host.IsGenericHost |= isOnGenericHost;
        var stateName = GuardedStateName(csharpName);
        var initName = GuardedInitName(csharpName);

        if (declareField)
        {
            yield return FieldDeclaration(VariableDeclaration(type)
                    .WithVariables(SingletonSeparatedList(VariableDeclarator(EscapedIdentifier(fieldName))
                        .WithInitializer(ClrDefaultInitializer()))))
                .WithModifiers(fieldModifiers ?? PrivateStatic())
                .WithAttributeLists(fieldAttributes.Add(CompilerGeneratedAttribute()));
        }

        yield return FieldDeclaration(VariableDeclaration(PredefinedType(Token(SyntaxKind.ByteKeyword)))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(stateName)))))
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated());

        // private static void N__init() { <hoisted>; N__field = <initializer>; }
        var initBody = new List<StatementSyntax>(hoisted)
        {
            ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression, EscapedIdentifierName(fieldName), value)),
        };
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(initName))
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated())
            .WithBody(Block(initBody));

        // private static void N__slow() => StaticInit.Read(ref __host, __Sweep, ref N__state, "n", N__init);
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(GuardedSlowName(csharpName)))
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated())
            .WithExpressionBody(ArrowExpressionClause(HostCall("Read",
                RefArgument(stateName),
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(sourceName))),
                Argument(IdentifierName(initName)))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        // The sweep step, at the value's declaration position: StaticInit.Run(ref N__state, "n", N__init);
        host.Sweep.Add(new RunStep(ExpressionStatement(InvocationExpression(StaticInitName("Run"))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                RefArgument(stateName),
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(sourceName))),
                Argument(IdentifierName(initName)),
            }))))));
    }

    /// <summary><c>if (!__Swept.Done) N__slow();</c></summary>
    private static StatementSyntax GuardedReadPrelude(string csharpName)
        => IfStatement(
            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SweptDone()),
            ExpressionStatement(InvocationExpression(IdentifierName(GuardedSlowName(csharpName)))));

    /// <summary>
    /// <c>if (__Swept.Done) field = value; else N__storeSlow(value);</c>: the store's slow path is out
    /// of line, as the read's is in <c>N__slow</c>, so the store stays small enough for the JIT to
    /// inline it into its caller (the module value store measured 4.26x V0 with it inline, A12).
    /// </summary>
    private static StatementSyntax GuardedStoreStatement(string csharpName, string fieldName, ExpressionSyntax value)
        => IfStatement(
            SweptDone(),
            ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression, EscapedIdentifierName(fieldName), value)),
            ElseClause(ExpressionStatement(InvocationExpression(IdentifierName(GuardedStoreSlowName(csharpName)))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value)))))));

    /// <summary><c>private static void N__storeSlow(T value) =&gt; StaticInit.Store(ref __host, __Sweep,
    /// ref N__state, ref N__field, value);</c>: a store before the sweep is done (D4).</summary>
    private static MethodDeclarationSyntax GuardedStoreSlow(string csharpName, string fieldName, TypeSyntax fieldType)
        => MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(GuardedStoreSlowName(csharpName)))
            .WithModifiers(PrivateStatic())
            .WithAttributeLists(Generated())
            .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("value")).WithType(fieldType))))
            .WithExpressionBody(ArrowExpressionClause(HostCall("Store",
                RefArgument(GuardedStateName(csharpName)),
                Argument(EscapedIdentifierName(fieldName)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword)),
                Argument(IdentifierName("value")))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

    /// <summary><c>&lt;access&gt; T N__set(T value) { store; return value; }</c>: the plain-store seam of
    /// every Guarded value that takes a store (D4).</summary>
    private static MethodDeclarationSyntax SetMethod(string name, TypeSyntax type, SyntaxTokenList access, StatementSyntax store)
        => MethodDeclaration(type, Identifier(name + GuardedSetSuffix))
            .WithModifiers(access)
            .WithAttributeLists(Generated())
            .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("value")).WithType(type))))
            .WithBody(Block(store, ReturnStatement(IdentifierName("value"))));

    private static TypeSyntax StrongBoxOf(TypeSyntax type)
        => NullableType(QualifiedName(
            (NameSyntax)MakeGlobalQualifiedName("System", "Runtime", "CompilerServices"),
            GenericName(Identifier("StrongBox")).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(type)))));

    /// <summary>
    /// A Guarded value's initializer (D4), its one user expression, generated alone under its own
    /// scope sink: the value <c>N__init</c> stores, and the statements it hoists, which run first.
    /// </summary>
    private (List<StatementSyntax> Hoisted, ExpressionSyntax Value) GenerateGuardedInitializer(Expression initializer)
    {
        ExpressionSyntax value = null!;
        var hoisted = WithScopeSink(() => value = GenerateExpression(initializer));
        return (hoisted, value);
    }

    /// <summary>
    /// A Guarded variable, <c>@static</c> field or computed const (D4): backing field, state, init,
    /// slow path, and a property of the value's own C# name that returns by reference (a const:
    /// <c>ref readonly</c>); a writable value also has its plain store (<c>N__set</c>) and its
    /// <c>out</c> operand pair (<c>N__out</c> / <c>N__commit</c>).
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateGuardedValue(GuardedValue v)
    {
        var name = v.CSharpName;
        var fieldName = GuardedFieldName(name);
        var valueAccess = TokenList(Token(v.Access), Token(SyntaxKind.StaticKeyword));
        // The backing field and the helpers are the assembly's (D10): an `async def` body names the
        // field after N__ensure() (D6), and every store and `out` site is compiled here.
        var access = HelperAccess(v.Access);
        var (hoisted, value) = GenerateGuardedInitializer(v.Initializer);
        foreach (var member in GenerateGuardedStorage(name, v.SourceName, fieldName, v.Type, hoisted, value, v.IsOnGenericHost,
                     v.FieldAttributes, fieldModifiers: access))
            yield return member;

        // public static ref [readonly] T N { get { if (!__Swept.Done) N__slow(); return ref N__field; } }
        var refType = RefType(v.Type);
        if (v.IsReadOnly)
            refType = refType.WithReadOnlyKeyword(Token(SyntaxKind.ReadOnlyKeyword));
        yield return PropertyDeclaration(refType, EscapedIdentifier(name))
            .WithModifiers(valueAccess)
            .WithAttributeLists(v.PropertyAttributes.Add(GuardedStorageAttribute()))
            .WithAccessorList(AccessorList(SingletonList(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(
                    GuardedReadPrelude(name),
                    ReturnStatement(RefExpression(EscapedIdentifierName(fieldName))))))));

        // public static void N__ensure() { if (!__Swept.Done) N__slow(); }   (D6)
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(name + GuardedEnsureSuffix))
            .WithModifiers(access)
            .WithAttributeLists(Generated())
            .WithBody(Block(GuardedReadPrelude(name)));

        if (v.IsReadOnly)
            yield break;

        // public static T N__set(T value) { if (__Swept.Done) N__field = value; else N__storeSlow(value); return value; }
        yield return GuardedStoreSlow(name, fieldName, v.Type);
        yield return SetMethod(name, v.Type, access, GuardedStoreStatement(name, fieldName, IdentifierName("value")));

        // public static StrongBox<T>? N__pick()
        // {
        //     if (!__Swept.Done)
        //     {
        //         StaticInit.Load(ref __host, __Sweep);
        //         if (!StaticInit.IsReady(ref N__state)) return new StrongBox<T>();
        //     }
        //     return null;
        // }
        // The `out` arm by the value's state at the operand (D4): null is the storage, a box the
        // temporary its commit stores. An `async def` body branches on it (D6).
        var box = StrongBoxOf(v.Type);
        yield return MethodDeclaration(box, Identifier(name + GuardedPickSuffix))
            .WithModifiers(access)
            .WithAttributeLists(Generated())
            .WithBody(Block(
                IfStatement(
                    PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SweptDone()),
                    Block(
                        LoadCall(),
                        IfStatement(
                            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
                                InvocationExpression(StaticInitName("IsReady"))
                                    .WithArgumentList(ArgumentList(SingletonSeparatedList(RefArgument(GuardedStateName(name)))))),
                            ReturnStatement(ObjectCreationExpression(((NullableTypeSyntax)box).ElementType)
                                .WithArgumentList(ArgumentList()))))),
                ReturnStatement(LiteralExpression(SyntaxKind.NullLiteralExpression))));

        // public static ref T N__out(out StrongBox<T>? spill)
        // {
        //     spill = N__pick();
        //     if (spill != null) return ref spill.Value;
        //     return ref N__field;
        // }
        // The temporary is a heap box, so the method returns no `out` parameter by reference (F15:
        // no [UnscopedRef], which netstandard2.1 lacks, on either target).
        // The box's Value is [MaybeNull] on net10.0: a pick's box is only ever read after the call
        // that assigns it through `out` (F15: nullable-clean on both targets).
        var boxValue = PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("spill"), IdentifierName("Value")));
        yield return MethodDeclaration(RefType(v.Type), Identifier(name + GuardedOutSuffix))
            .WithModifiers(access)
            .WithAttributeLists(Generated())
            .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("spill"))
                .WithModifiers(TokenList(Token(SyntaxKind.OutKeyword)))
                .WithType(box))))
            .WithBody(Block(
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("spill"), InvocationExpression(IdentifierName(name + GuardedPickSuffix)))),
                IfStatement(
                    BinaryExpression(SyntaxKind.NotEqualsExpression, IdentifierName("spill"), LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    ReturnStatement(RefExpression(boxValue))),
                ReturnStatement(RefExpression(EscapedIdentifierName(fieldName)))));

        // public static R N__commit<R>(R result, StrongBox<T>? spill) { if (spill != null) N__set(spill.Value); return result; }
        yield return MethodDeclaration(IdentifierName("R"), Identifier(name + GuardedCommitSuffix))
            .WithModifiers(access)
            .WithAttributeLists(Generated())
            .WithTypeParameterList(TypeParameterList(SingletonSeparatedList(TypeParameter("R"))))
            .WithParameterList(ParameterList(SeparatedList(new[]
            {
                Parameter(Identifier("result")).WithType(IdentifierName("R")),
                Parameter(Identifier("spill")).WithType(box),
            })))
            .WithBody(Block(
                IfStatement(
                    BinaryExpression(SyntaxKind.NotEqualsExpression, IdentifierName("spill"), LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    ExpressionStatement(InvocationExpression(IdentifierName(name + GuardedSetSuffix))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(boxValue)))))),
                ReturnStatement(IdentifierName("result"))));
    }

    /// <summary>
    /// Whether a property group declares a Guarded static value (a default on a static auto half):
    /// a class property's <see cref="PropertySymbol.StaticStorage"/>, or a module property's
    /// <see cref="CodeGenInfo.StaticStorage"/> (plan-775c50 D2).
    /// </summary>
    private bool IsGuardedPropertyGroup(PropertyDef first)
    {
        if (_currentTypeSymbol != null)
            return _currentTypeSymbol.FindProperty(first.Name, first.ExplicitInterface) is { StaticStorage: StaticStorage.Guarded };
        return _context.LookupSymbol(first.Name) is { } moduleProperty && IsGuarded(GetCodeGenInfo(moduleProperty));
    }

    /// <summary>
    /// A Guarded property default (plan-775c50 D4): the property keeps <c>get</c> / <c>set</c>, its
    /// storage loses its initializer, and the accessors read through the slow path and store through
    /// <c>StaticInit.Store</c>. <paramref name="members"/> is the property group's emission: one
    /// property, and the private backing field an expanded arm declares. A custom accessor's own
    /// body is the user's: a getter gains the read prelude, a setter is left as written.
    /// </summary>
    private IEnumerable<MemberDeclarationSyntax> GuardPropertyGroup(List<MemberDeclarationSyntax> members, PropertyDef first)
    {
        var property = members.OfType<PropertyDeclarationSyntax>().Single();
        var backing = members.OfType<FieldDeclarationSyntax>()
            .SingleOrDefault(f => f.Declaration.Variables[0].Initializer != null);
        var initializer = property.Initializer?.Value ?? backing?.Declaration.Variables[0].Initializer?.Value;
        if (initializer == null)
            throw new InternalCompilerErrorException("RoslynEmitter",
                $"a Guarded property '{first.Name}' has no default in its emission (plan-775c50 D4)", first);

        var name = property.Identifier.ValueText;
        var fieldName = backing?.Declaration.Variables[0].Identifier.ValueText ?? GuardedFieldName(name);
        var type = property.Type;

        var output = new List<MemberDeclarationSyntax>();
        foreach (var member in members)
        {
            if (ReferenceEquals(member, property))
            {
                // A closed generic type is swept by first use, never at a line (D5): the property's own fact.
                var isOnGenericHost = _currentTypeSymbol?.FindProperty(first.Name, first.ExplicitInterface) is { IsOnGenericHost: true };
                output.AddRange(GenerateGuardedStorage(name, first.Name, fieldName, type, Array.Empty<StatementSyntax>(), initializer,
                    isOnGenericHost, new SyntaxList<AttributeListSyntax>(), declareField: backing == null));
                var guarded = GuardPropertyAccessors(property, name, fieldName).AddAttributeLists(GuardedStorageAttribute());
                output.Add(guarded);
                // A writing accessor's guarded stores call the out-of-line slow path (a custom
                // setter with no store of the field has none).
                if (CallsMethod(guarded, GuardedStoreSlowName(name)))
                    output.Add(GuardedStoreSlow(name, fieldName, backing?.Declaration.Type ?? type));
                // The plain-store seam is one spelling for every Guarded value (D4): a property
                // default's N__set stores through its setter, which keeps get / set.
                if (guarded.AccessorList!.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
                {
                    var propertyAccess = guarded.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)) ? SyntaxKind.PrivateKeyword
                        : guarded.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword)) ? SyntaxKind.ProtectedKeyword
                        : SyntaxKind.InternalKeyword;
                    output.Add(SetMethod(name, type, HelperAccess(propertyAccess),
                        ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            EscapedIdentifierName(name), IdentifierName("value")))));
                }
            }
            else if (ReferenceEquals(member, backing))
            {
                var variable = backing.Declaration.Variables[0].WithInitializer(ClrDefaultInitializer());
                output.Add(backing
                    .WithDeclaration(backing.Declaration.WithVariables(SingletonSeparatedList(variable)))
                    .WithModifiers(PrivateStatic()));
            }
            else
            {
                output.Add(member);
            }
        }
        return output;
    }

    private static PropertyDeclarationSyntax GuardPropertyAccessors(PropertyDeclarationSyntax property, string name, string fieldName)
    {
        var accessors = new List<AccessorDeclarationSyntax>();
        foreach (var accessor in property.AccessorList!.Accessors)
        {
            var isAuto = accessor.Body == null && accessor.ExpressionBody == null;
            if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                var statements = new List<StatementSyntax> { GuardedReadPrelude(name) };
                if (isAuto)
                    statements.Add(ReturnStatement(EscapedIdentifierName(fieldName)));
                else if (accessor.ExpressionBody != null)
                    statements.Add(ReturnStatement(accessor.ExpressionBody.Expression));
                else
                    statements.AddRange(accessor.Body!.Statements);
                accessors.Add(accessor.WithExpressionBody(null).WithSemicolonToken(default).WithBody(Block(statements)));
                continue;
            }

            // A writing accessor: the auto store, or each store of the backing field in an expanded
            // arm's generated body, becomes the guarded store.
            BlockSyntax body;
            if (isAuto)
                body = Block(GuardedStoreStatement(name, fieldName, IdentifierName("value")));
            else if (accessor.ExpressionBody != null)
                body = Block(GuardStoresOf(ExpressionStatement(accessor.ExpressionBody.Expression), name, fieldName));
            else
                body = Block(accessor.Body!.Statements.Select(st => GuardStoresOf(st, name, fieldName)));
            // A generated setter that also READS the backing field (an `after_set` observer's
            // `var __old = _name;`) reads it like any other read: initialized first (D4), never a
            // CLR default for a value not yet given one. Judged on the accessor as generated, before
            // the guarded store (whose `ref field` is no read) replaced its stores.
            if (!isAuto && ReadsField(accessor, fieldName))
                body = body.WithStatements(body.Statements.Insert(0, GuardedReadPrelude(name)));
            accessors.Add(accessor.WithExpressionBody(null).WithSemicolonToken(default).WithBody(body));
        }

        var modifiers = property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
            ? property.Modifiers
            : property.Modifiers.Add(Token(SyntaxKind.StaticKeyword));
        return property
            .WithInitializer(null)
            .WithSemicolonToken(default)
            .WithModifiers(modifiers)
            .WithAccessorList(AccessorList(List(accessors)));
    }

    /// <summary>Whether <paramref name="node"/> calls the simple name <paramref name="method"/>.</summary>
    private static bool CallsMethod(SyntaxNode node, string method)
        => node.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(call => call.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == method);

    /// <summary>Whether <paramref name="body"/> names the backing field anywhere but as a simple
    /// store's target.</summary>
    private static bool ReadsField(SyntaxNode body, string fieldName)
        => body.DescendantNodes().OfType<IdentifierNameSyntax>().Any(id =>
            id.Identifier.ValueText == fieldName
            && !(id.Parent is AssignmentExpressionSyntax store
                && store.IsKind(SyntaxKind.SimpleAssignmentExpression) && store.Left == id));

    private static StatementSyntax GuardStoresOf(StatementSyntax statement, string name, string fieldName)
        => statement is ExpressionStatementSyntax
        {
            Expression: AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment
        } && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && left.Identifier.ValueText == fieldName
            ? GuardedStoreStatement(name, fieldName, assignment.Right)
            : statement;

    /// <summary>
    /// An import's requirements in the module's sweep, at its position (D5): each Sharpy module the
    /// statement makes Python run, in the order <c>SemanticInfo.GetImportRequirements</c> records
    /// (ancestor packages, the module, a from-import's submodules), through its load entry, when its
    /// layout records a sweep: a source module's under the project namespace, a module imported by
    /// reference's by its members class's CLR name (D10). A module the fact does not list (a CLR
    /// module with no load entry) is not required.
    /// </summary>
    private void AddImportRequirements(Statement import)
    {
        foreach (var module in _context.SemanticInfo?.GetImportRequirements(import) ?? [])
        {
            if (!module.HasSweep)
                continue;
            // The layout's members class: a module imported by reference names its discovered CLR
            // class (a nested one joined by `+`), a source module's is under the project namespace.
            var path = module.MembersClassFullName(_context.ProjectNamespace).Replace('+', '.').Split('.');
            CurrentStaticHost.Sweep.Add(new RequireStep(path, ModuleLoadName, IsGlobal: true));
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // The use sites: a plain store and an `out` operand (D4)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>
    /// Whether a store or operand <paramref name="target"/> names a Guarded value that takes a store:
    /// the recorded <see cref="CodeGenInfo.StaticStorage"/> of the symbol the checker bound, no const.
    /// Every such value has <c>N__set</c> (a property default's calls its setter, D4); the checker
    /// refuses an <c>out</c> operand on a const or a property (D7). The bound symbol is the only
    /// source, on a bare name as on a qualified one, as <c>AsyncGuardedUseAnalyzer</c> reads it: never
    /// a lookup by the spelling, which the use's position would decide (plan-775c50 Defect Class).
    /// </summary>
    private bool IsGuardedVariableTarget(Expression target)
    {
        var symbol = _context.SemanticInfo?.GetBoundValueSymbol(target);
        return IsGuardedStore(symbol == null ? null : GetCodeGenInfo(symbol));
    }

    /// <summary>Whether a store to the value <paramref name="info"/> records is its <c>N__set</c>: a
    /// Guarded value that is no const (a const has no store; the checker's refusal, or #2435's
    /// cross-host cell, sees the plain assignment).</summary>
    private static bool IsGuardedStore(CodeGenInfo? info) => info is { StaticStorage: StaticStorage.Guarded, IsConstant: false };

    /// <summary>Whether <paramref name="info"/> records a Guarded value (plan-775c50 D2).</summary>
    private static bool IsGuarded(CodeGenInfo? info) => info is { StaticStorage: StaticStorage.Guarded };

    /// <summary>The member of a Guarded value's spelling <paramref name="target"/> (<c>N</c>,
    /// <c>Q.N</c>) with <paramref name="suffix"/> appended to its name.</summary>
    private ExpressionSyntax GuardedMember(ExpressionSyntax target, string suffix, Node node) => target switch
    {
        IdentifierNameSyntax id => IdentifierName(id.Identifier.ValueText + suffix),
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } ma
            => ma.WithName(IdentifierName(name.Identifier.ValueText + suffix)),
        ParenthesizedExpressionSyntax p => GuardedMember(p.Expression, suffix, node),
        _ => throw new InternalCompilerErrorException("RoslynEmitter",
            $"a Guarded value's store target has no member spelling (plan-775c50 D4): '{target}'", node),
    };

    /// <summary>
    /// The ONE plain-store seam (D4): <c>target = value</c>, or <c>N__set(value)</c> when
    /// <paramref name="targetNode"/> names a Guarded variable. Both are expressions of the stored value.
    /// </summary>
    private ExpressionSyntax PlainStore(Expression targetNode, ExpressionSyntax target, ExpressionSyntax value)
        => PlainStore(IsGuardedVariableTarget(targetNode), targetNode, target, value);

    /// <summary><see cref="PlainStore(Expression, ExpressionSyntax, ExpressionSyntax)"/> for a store
    /// site that carries its bound symbol's recorded facts rather than a target expression (a walrus).</summary>
    private ExpressionSyntax PlainStore(CodeGenInfo? info, Node node, ExpressionSyntax target, ExpressionSyntax value)
        => PlainStore(IsGuardedStore(info), node, target, value);

    /// <summary>The two spellings of the plain-store seam: <c>N__set(value)</c> when the store is a
    /// Guarded value's (<paramref name="isGuardedStore"/>), else <c>target = value</c>.</summary>
    private ExpressionSyntax PlainStore(bool isGuardedStore, Node node, ExpressionSyntax target, ExpressionSyntax value)
        => isGuardedStore
            ? InvocationExpression(GuardedMember(target, GuardedSetSuffix, node))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))))
            : AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, value);

    /// <summary>
    /// One <c>out</c> operand's commit (D4): <c>N__commit</c> on the operand's spill. In an
    /// <c>async def</c> body (D6) the operand is the <paramref name="Placeholder"/> name, which the
    /// branch on the spill replaces by <paramref name="Field"/> (the storage) or the spill's box.
    /// </summary>
    private sealed record OutCommit(ExpressionSyntax Commit, string Spill, ExpressionSyntax? Field = null, string? Placeholder = null)
    {
        public bool IsAsync => Placeholder != null;
    }

    /// <summary>The commits of the call being generated: each <c>out</c> operand on a Guarded value
    /// adds one, applied when the call returns (D4; W10).</summary>
    private List<OutCommit>? _outCommits;

    /// <summary>A statement call's commits, left for its expression statement to place after it.</summary>
    private List<OutCommit>? _pendingVoidCallCommits;

    /// <summary>The call an expression statement is, while it is generated.</summary>
    private Expression? _statementCall;

    /// <summary>
    /// An <c>out</c> operand naming a Guarded variable (D4): <c>N__out(out var __spillK)</c>, which is
    /// the storage once the value is Ready and a temporary before; the call's commit stores it.
    /// In an <c>async def</c> body (the D6 fact) no reference may live across an <c>await</c>: the arm
    /// is picked before the statement, <c>var __spillK = N__pick();</c>, and the call is emitted
    /// twice, on the backing field and on the spill's box (<see cref="GenerateCallWithOutCommits"/>).
    /// </summary>
    private ExpressionSyntax GuardedOutOperand(Expression operand, ExpressionSyntax target)
    {
        // The operand's commit is its own call's (W10): a call whose arguments are generated outside
        // GenerateCallWithOutCommits would leave the commit to an enclosing call, or to none.
        if (_outCommitCall == null || !IsOwnOutOperand(_outCommitCall, operand))
            throw new InternalCompilerErrorException("RoslynEmitter",
                "an `out` operand on a Guarded value was generated outside its call's commit seam (plan-775c50 D4)", operand);
        var spill = $"__spill{_tempVarCounter++}";
        var commit = GuardedMember(target, GuardedCommitSuffix, operand);
        if (AsyncGuardedUseOf(operand) == AsyncGuardedUseKind.OutOperand)
        {
            HoistEvaluation(LocalDeclarationStatement(VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(spill))
                    .WithInitializer(EqualsValueClause(InvocationExpression(GuardedMember(target, GuardedPickSuffix, operand))))))));
            var placeholder = $"__outOperand{_tempVarCounter++}";
            (_outCommits ??= new()).Add(new OutCommit(commit, spill, GuardedMember(target, GuardedFieldSuffix, operand), placeholder));
            return IdentifierName(placeholder);
        }

        (_outCommits ??= new()).Add(new OutCommit(commit, spill));
        return InvocationExpression(GuardedMember(target, GuardedOutSuffix, operand))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(DeclarationExpression(IdentifierName("var"), SingleVariableDesignation(Identifier(spill))))
                    .WithRefKindKeyword(Token(SyntaxKind.OutKeyword)))));
    }

    /// <summary>
    /// Generates a call and applies its <c>out</c> commits: a valued call becomes
    /// <c>N__commit(call, __spillK)</c>, so the store lands before the operand to its right (W10); a
    /// void call leaves its commits to the expression statement it is. In an <c>async def</c> body a
    /// valued call is <c>(__spillK == null ? call(out N__field) : N__commit(call(out __spillK.Value), __spillK))</c> (D6).
    /// Such a call is emitted once per arm, as D4 asks of a Ready value's storage across an
    /// <c>await</c> (C# has no <c>ref</c> conditional beside an <c>await</c>, CS8325). One arm runs, so
    /// each operand is evaluated once; no copy redeclares a name: an inline <c>out</c> declaration is
    /// declared before the statement (<see cref="IsEmittedPerArm"/>), and a temporary the call declares
    /// is renamed in the spill arm (<see cref="RenameDeclaredTemporaries"/>).
    /// </summary>
    private ExpressionSyntax GenerateCallWithOutCommits(FunctionCall call, Func<ExpressionSyntax> generate)
    {
        var saved = _outCommits;
        var savedCall = _outCommitCall;
        _outCommits = null;
        _outCommitCall = call;
        var perArm = IsEmittedPerArm(call);
        if (perArm)
            _callsEmittedPerArm++;
        ExpressionSyntax result;
        List<OutCommit>? commits;
        try
        {
            result = generate();
        }
        finally
        {
            commits = _outCommits;
            _outCommits = saved;
            _outCommitCall = savedCall;
            if (perArm)
                _callsEmittedPerArm--;
        }
        // The arms the operands took are the ones IsEmittedPerArm read before the call was generated.
        if ((commits?.Any(c => c.IsAsync) ?? false) != perArm)
            throw new InternalCompilerErrorException("RoslynEmitter",
                "a call's `out` arms disagree with its operands' D6 facts (plan-775c50 D6)", call);
        if (commits == null)
            return result;

        // The expression statement's own call places its commits after it (a void call has no value
        // to commit through); W10's order is then the statement's.
        if (ReferenceEquals(call, _statementCall))
        {
            if (_pendingVoidCallCommits != null)
                throw new InternalCompilerErrorException("RoslynEmitter",
                    "an `out` operand's commit was left unplaced (plan-775c50 D4)", call);
            if (commits.Any(c => c.IsAsync) && commits.Any(c => !c.IsAsync))
                throw new InternalCompilerErrorException("RoslynEmitter",
                    "a void call mixes an `async def` body's `out` arm with a synchronous one (plan-775c50 D6)", call);
            _pendingVoidCallCommits = commits;
            return result;
        }

        foreach (var commit in commits)
        {
            if (commit.IsAsync)
            {
                result = ParenthesizedExpression(ConditionalExpression(
                    SpillIsNull(commit.Spill),
                    ReplacePlaceholder(result, commit.Placeholder!, commit.Field!),
                    CommitCall(commit, RenameDeclaredTemporaries(ReplacePlaceholder(result, commit.Placeholder!, SpillValue(commit.Spill))))));
                continue;
            }
            result = CommitCall(commit, result);
        }
        return result;
    }

    /// <summary>The call whose arguments are being generated under <see cref="GenerateCallWithOutCommits"/>.</summary>
    private FunctionCall? _outCommitCall;

    /// <summary>Whether <paramref name="operand"/> is an <c>out</c> operand of <paramref name="call"/>
    /// itself, positional or keyword, rather than of a call nested in or around it.</summary>
    private static bool IsOwnOutOperand(FunctionCall call, Expression operand)
        => call.Arguments.Concat(call.KeywordArguments.Select(k => k.Value)).Any(argument =>
            argument is ModifiedArgument { Modifier: ParameterModifier.Out, InlineName: null } own
            && ReferenceEquals(own.Argument, operand));

    /// <summary>The depth of calls being generated that are emitted once per arm (D6): an inline
    /// <c>out</c> declaration under one is declared before the statement, so no copy redeclares it.</summary>
    private int _callsEmittedPerArm;

    /// <summary>
    /// Whether <paramref name="call"/> is emitted once per arm (D6): an <c>out</c> operand of it names
    /// a Guarded value that takes a store and carries the <see cref="AsyncGuardedUseKind.OutOperand"/>
    /// fact, the condition on which <see cref="GuardedOutOperand"/> picks the arm before the statement.
    /// Read from the operands' facts before the call is generated.
    /// </summary>
    private bool IsEmittedPerArm(FunctionCall call)
        => call.Arguments.Concat(call.KeywordArguments.Select(k => k.Value)).Any(argument =>
            argument is ModifiedArgument { Modifier: ParameterModifier.Out, InlineName: null } operand
            && IsGuardedVariableTarget(operand.Argument)
            && AsyncGuardedUseOf(operand.Argument) == AsyncGuardedUseKind.OutOperand);

    private static ExpressionSyntax CommitCall(OutCommit commit, ExpressionSyntax result)
        => InvocationExpression(commit.Commit).WithArgumentList(ArgumentList(SeparatedList(new[]
        {
            Argument(result),
            Argument(IdentifierName(commit.Spill)),
        })));

    /// <summary>
    /// The spill arm's copy of a valued call emitted once per arm (D6), each name it declares renamed:
    /// both arms of the conditional are one C# scope, so a temporary the call declares
    /// (<c>x is var __opt_N</c>) would be declared twice (CS0128). Such a temporary is read only in the
    /// expression that declares it; an inline <c>out</c> declaration, read after the statement, is
    /// declared before it instead (<see cref="IsEmittedPerArm"/>).
    /// <para>Only the declaration and the simple-name reads of it are renamed (<see cref="IsVariableReference"/>):
    /// a member name (<c>box.v</c>, <c>?.v</c>), a named argument (<c>v:</c>), a property-pattern
    /// member and a type spelled the same are other things, and keep their names.</para>
    /// </summary>
    private ExpressionSyntax RenameDeclaredTemporaries(ExpressionSyntax copy)
        => RenameDeclaredNames(copy, declared => $"{declared}__arm{_tempVarCounter++}");

    /// <summary><see cref="RenameDeclaredTemporaries"/>, each declared name's fresh spelling from
    /// <paramref name="fresh"/>.</summary>
    internal static ExpressionSyntax RenameDeclaredNames(ExpressionSyntax copy, Func<string, string> fresh)
    {
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var designation in copy.DescendantNodes().OfType<SingleVariableDesignationSyntax>())
        {
            if (!renames.ContainsKey(designation.Identifier.ValueText))
                renames[designation.Identifier.ValueText] = fresh(designation.Identifier.ValueText);
        }
        return renames.Count == 0
            ? copy
            : copy.ReplaceTokens(
                copy.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)
                    && (token.Parent is SingleVariableDesignationSyntax
                        || token.Parent is IdentifierNameSyntax name && IsVariableReference(name))
                    && renames.ContainsKey(token.ValueText)),
                (token, _) => Identifier(token.LeadingTrivia, renames[token.ValueText], token.TrailingTrivia));
    }

    /// <summary>Whether <paramref name="name"/> is a simple name read as a variable: not the member
    /// of a member access or binding, a named argument or property-pattern member, nor a type.</summary>
    private static bool IsVariableReference(IdentifierNameSyntax name)
        => name.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == name,
            MemberBindingExpressionSyntax or NameColonSyntax or ExpressionColonSyntax or NameEqualsSyntax
                or QualifiedNameSyntax or AliasQualifiedNameSyntax => false,
            _ => !SyntaxFacts.IsInNamespaceOrTypeContext(name),
        };

    private static ExpressionSyntax SpillIsNull(string spill)
        => BinaryExpression(SyntaxKind.EqualsExpression, IdentifierName(spill), LiteralExpression(SyntaxKind.NullLiteralExpression));

    private static ExpressionSyntax SpillValue(string spill)
        => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(spill), IdentifierName("Value"));

    private static T ReplacePlaceholder<T>(T node, string placeholder, ExpressionSyntax with) where T : SyntaxNode
        => node.ReplaceNodes(
            node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(i => i.Identifier.ValueText == placeholder),
            (_, _) => with);

    /// <summary>
    /// Places a void call statement's commits (D4): <c>N__commit(false, __spillK);</c> after it. In an
    /// <c>async def</c> body (D6) the statement is branched on each spill:
    /// <c>if (__spillK == null) stmt(out N__field); else { stmt(out __spillK.Value); N__commit(false, __spillK); }</c>.
    /// Returns the statements in order.
    /// </summary>
    private List<StatementSyntax> PlaceVoidCallCommits(StatementSyntax statement, List<OutCommit> commits)
    {
        if (commits.All(c => c.IsAsync))
        {
            foreach (var commit in commits)
            {
                statement = IfStatement(
                    SpillIsNull(commit.Spill),
                    Block(ReplacePlaceholder(statement, commit.Placeholder!, commit.Field!)),
                    ElseClause(Block(
                        ReplacePlaceholder(statement, commit.Placeholder!, SpillValue(commit.Spill)),
                        VoidCommit(commit))));
            }
            return new List<StatementSyntax> { statement };
        }

        var output = new List<StatementSyntax> { statement };
        output.AddRange(commits.Select(VoidCommit));
        return output;
    }

    private static StatementSyntax VoidCommit(OutCommit commit)
        => ExpressionStatement(CommitCall(commit, LiteralExpression(SyntaxKind.FalseLiteralExpression)));

    /// <summary>A void call's commits, taken by the expression statement that places them.</summary>
    private List<OutCommit>? TakeVoidCallCommits()
    {
        var commits = _pendingVoidCallCommits;
        _pendingVoidCallCommits = null;
        return commits;
    }

    // ------------------------------------------------------------------------------------------- //
    // An `async def` body (D6)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>The D6 fact on <paramref name="node"/>: how an <c>async def</c> body uses the Guarded
    /// value it names, or null for a plain read or store and outside an <c>async def</c> body.</summary>
    private AsyncGuardedUseKind? AsyncGuardedUseOf(Expression node)
        => _context.SemanticInfo?.GetAsyncGuardedUse(node)?.Kind;

    /// <summary>
    /// A use in an <c>async def</c> body that is neither a plain read nor a plain store (D6):
    /// <c>N__ensure();</c> before the statement, then the backing field at the node, so no accessor's
    /// reference lives across an <c>await</c>. <paramref name="generate"/> spells the node as any other
    /// use. Outside an <c>async def</c> method no node carries the fact, so none is looked up.
    /// <para>The ensure and the field are named from <see cref="GuardedValueSpelling"/>; the narrowed
    /// read the checker recorded on the node then applies to the field, which holds the same value.</para>
    /// </summary>
    private ExpressionSyntax GenerateWithAsyncEnsure(Expression node, Func<ExpressionSyntax> generate)
        => IsAsyncEnsured(node) ? ApplyNarrowedReadLowering(node, EnsuredField(node)) : generate();

    /// <summary>
    /// A <c>ref</c> / <c>in</c> operand: in an <c>async def</c> body, one naming a Guarded value is the
    /// backing field after <c>N__ensure()</c> (D6), passed as the variable it is: a narrowed read
    /// (<c>((T)N!)</c>, <c>N.Value</c>) is no variable to pass by reference (CS1510).
    /// </summary>
    private ExpressionSyntax GenerateReferenceOperand(Expression operand)
        => IsAsyncEnsured(operand) ? EnsuredField(operand) : GenerateExpression(operand);

    private bool IsAsyncEnsured(Expression node)
        => _isCurrentMethodAsync && node is Parser.Ast.Identifier or MemberAccess && AsyncGuardedUseOf(node) == AsyncGuardedUseKind.Ensure;

    /// <summary><c>N__ensure();</c> hoisted before the statement, and the backing field.</summary>
    private ExpressionSyntax EnsuredField(Expression node)
    {
        var value = GuardedValueSpelling(node);
        HoistEvaluation(ExpressionStatement(InvocationExpression(GuardedMember(value, GuardedEnsureSuffix, node))));
        return GuardedMember(value, GuardedFieldSuffix, node);
    }

    /// <summary>
    /// The value's own spelling at <paramref name="node"/>, the name or member access naming a Guarded
    /// value: the name its <see cref="CodeGenInfo"/> resolves to on its host, with no narrowed read
    /// applied, as a store target is spelled. Its helpers (<c>N__ensure</c>, <c>N__field</c>,
    /// <c>N__out</c>, <c>N__pick</c>, <c>N__commit</c>) are named from it, never from a narrowed read
    /// (<c>N!</c>, <c>N.Value</c>, <c>((T)N!)</c>), which is no member spelling (D4, D6).
    /// </summary>
    private ExpressionSyntax GuardedValueSpelling(Expression node) => node switch
    {
        Parser.Ast.Identifier name => GenerateIdentifierExpression(name, applyNarrowing: false),
        MemberAccess memberAccess => GenerateMemberAccess(memberAccess, applyNarrowing: false),
        _ => throw new InternalCompilerErrorException("RoslynEmitter",
            $"a Guarded value is named by neither a name nor a member access (plan-775c50 D4): {node.GetType().Name}", node),
    };
}
