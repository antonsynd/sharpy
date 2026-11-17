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
/// private static T N__field;  private static byte N__state;       // no initializers
/// private static void N__init() { N__field = &lt;initializer&gt;; }
/// private static void N__slow() =&gt; StaticInit.Read(ref __host, __Sweep, ref N__state, "n", N__init);
/// public static ref T N { get { if (!__Swept.Done) N__slow(); return ref N__field; } }   // ref readonly for a const
/// public static T N__set(T value) { ... StaticInit.Store(...) ...; return value; }           // every plain store
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
    private static string GuardedStateName(string name) => name + "__state";
    private static string GuardedInitName(string name) => name + "__init";
    private static string GuardedSlowName(string name) => name + "__slow";

    /// <summary>The sweep of the host being emitted (a module, or a class / struct / interface with
    /// Guarded values): one <c>StaticInit.Run</c> per value, in declaration order (D5).</summary>
    private sealed class StaticHostFrame
    {
        public readonly List<StatementSyntax> Sweep = new();
        public bool HasValues;
    }

    private StaticHostFrame? _staticHost;

    /// <summary>Opens a host frame for a type or module body; the returned frame is closed by
    /// <see cref="CloseStaticHost"/>, which restores the enclosing one.</summary>
    private StaticHostFrame OpenStaticHost(out StaticHostFrame? enclosing)
    {
        enclosing = _staticHost;
        _staticHost = new StaticHostFrame();
        return _staticHost;
    }

    /// <summary>Closes <paramref name="frame"/>: its host members when it has a value, none otherwise.</summary>
    private IEnumerable<MemberDeclarationSyntax> CloseStaticHost(StaticHostFrame frame, StaticHostFrame? enclosing)
    {
        _staticHost = enclosing;
        return frame.HasValues ? GenerateStaticHostMembers(frame) : Array.Empty<MemberDeclarationSyntax>();
    }

    private StaticHostFrame CurrentStaticHost
        => _staticHost ?? throw new InternalCompilerErrorException("RoslynEmitter",
            $"a Guarded value was emitted outside any host (plan-775c50 D5; file='{_context.SourceFilePath}')");

    private static NameSyntax StaticInitName(string member) => MakeGlobalQualifiedName("Sharpy", "StaticInit", member);

    private static ExpressionSyntax SweptDone()
        => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(SweptClassName), IdentifierName("Done"));

    private static ArgumentSyntax RefArgument(string name)
        => Argument(IdentifierName(name)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword));

    private static SyntaxTokenList PrivateStatic()
        => TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword));

    /// <summary>
    /// The host's own members: <c>__host</c>, <c>__Sweep</c> and <c>__Swept</c> (D5; the documented
    /// shape on <c>Sharpy.StaticInit</c>). Private: a host's sweep is required through its own values.
    /// </summary>
    private static IEnumerable<MemberDeclarationSyntax> GenerateStaticHostMembers(StaticHostFrame frame)
    {
        // private static global::Sharpy.StaticInitHost __host;
        yield return FieldDeclaration(VariableDeclaration(MakeGlobalQualifiedName("Sharpy", "StaticInitHost"))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(HostFieldName)))))
            .WithModifiers(PrivateStatic());

        // private static void __Sweep() { StaticInit.Run(ref a__state, "a", a__init); ... }
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(SweepMethodName))
            .WithModifiers(PrivateStatic())
            .WithBody(Block(frame.Sweep));

        // private static class __Swept { internal static readonly bool Done; static __Swept() { Done = StaticInit.TryLoad(ref __host, __Sweep); } }
        var done = FieldDeclaration(VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier("Done")))))
            .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));
        var cctor = ConstructorDeclaration(Identifier(SweptClassName))
            .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)))
            .WithBody(Block(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("Done"),
                InvocationExpression(StaticInitName("TryLoad"))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        RefArgument(HostFieldName),
                        Argument(IdentifierName(SweepMethodName)),
                    })))))));
        yield return ClassDeclaration(SweptClassName)
            .WithModifiers(PrivateStatic())
            .WithMembers(List(new MemberDeclarationSyntax[] { done, cctor }));
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
        SyntaxList<AttributeListSyntax> FieldAttributes);

    /// <summary>The value's slow path and sweep step, shared by the variable and the property arms.</summary>
    private IEnumerable<MemberDeclarationSyntax> GenerateGuardedStorage(
        string csharpName, string sourceName, string fieldName, TypeSyntax type, Expression? initializer,
        SyntaxList<AttributeListSyntax> fieldAttributes, bool declareField = true,
        ExpressionSyntax? generatedInitializer = null, SyntaxTokenList? fieldModifiers = null)
    {
        var host = CurrentStaticHost;
        host.HasValues = true;
        var stateName = GuardedStateName(csharpName);
        var initName = GuardedInitName(csharpName);

        if (declareField)
        {
            yield return FieldDeclaration(VariableDeclaration(type)
                    .WithVariables(SingletonSeparatedList(VariableDeclarator(EscapedIdentifier(fieldName)))))
                .WithModifiers(fieldModifiers ?? PrivateStatic())
                .WithAttributeLists(fieldAttributes);
        }

        yield return FieldDeclaration(VariableDeclaration(PredefinedType(Token(SyntaxKind.ByteKeyword)))
                .WithVariables(SingletonSeparatedList(VariableDeclarator(Identifier(stateName)))))
            .WithModifiers(PrivateStatic());

        // private static void N__init() { <hoisted>; N__field = <initializer>; }
        ExpressionSyntax value = generatedInitializer!;
        var hoisted = generatedInitializer != null
            ? new List<StatementSyntax>()
            : WithScopeSink(() => value = GenerateExpression(initializer!));
        var initBody = new List<StatementSyntax>(hoisted)
        {
            ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression, EscapedIdentifierName(fieldName), value)),
        };
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(initName))
            .WithModifiers(PrivateStatic())
            .WithBody(Block(initBody));

        // private static void N__slow() => StaticInit.Read(ref __host, __Sweep, ref N__state, "n", N__init);
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(GuardedSlowName(csharpName)))
            .WithModifiers(PrivateStatic())
            .WithExpressionBody(ArrowExpressionClause(InvocationExpression(StaticInitName("Read"))
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    RefArgument(HostFieldName),
                    Argument(IdentifierName(SweepMethodName)),
                    RefArgument(stateName),
                    Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(sourceName))),
                    Argument(IdentifierName(initName)),
                })))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        // The sweep step, at the value's declaration position: StaticInit.Run(ref N__state, "n", N__init);
        host.Sweep.Add(ExpressionStatement(InvocationExpression(StaticInitName("Run"))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                RefArgument(stateName),
                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(sourceName))),
                Argument(IdentifierName(initName)),
            })))));
    }

    /// <summary><c>if (!__Swept.Done) N__slow();</c></summary>
    private static StatementSyntax GuardedReadPrelude(string csharpName)
        => IfStatement(
            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SweptDone()),
            ExpressionStatement(InvocationExpression(IdentifierName(GuardedSlowName(csharpName)))));

    /// <summary><c>if (__Swept.Done) field = value; else StaticInit.Store(ref __host, __Sweep, ref N__state, ref field, value);</c></summary>
    private static StatementSyntax GuardedStoreStatement(string csharpName, string fieldName, ExpressionSyntax value)
        => IfStatement(
            SweptDone(),
            ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression, EscapedIdentifierName(fieldName), value)),
            ElseClause(ExpressionStatement(InvocationExpression(StaticInitName("Store"))
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    RefArgument(HostFieldName),
                    Argument(IdentifierName(SweepMethodName)),
                    RefArgument(GuardedStateName(csharpName)),
                    Argument(EscapedIdentifierName(fieldName)).WithRefKindKeyword(Token(SyntaxKind.RefKeyword)),
                    Argument(value),
                }))))));

    private static TypeSyntax StrongBoxOf(TypeSyntax type)
        => NullableType(QualifiedName(
            (NameSyntax)MakeGlobalQualifiedName("System", "Runtime", "CompilerServices"),
            GenericName(Identifier("StrongBox")).WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(type)))));

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
        var access = TokenList(Token(v.Access), Token(SyntaxKind.StaticKeyword));
        // The backing field has the value's accessibility: an `async def` body names it after
        // N__ensure() (D6), from wherever it can name the value.
        foreach (var member in GenerateGuardedStorage(name, v.SourceName, fieldName, v.Type, v.Initializer, v.FieldAttributes,
                     fieldModifiers: access))
            yield return member;

        // public static ref [readonly] T N { get { if (!__Swept.Done) N__slow(); return ref N__field; } }
        var refType = RefType(v.Type);
        if (v.IsReadOnly)
            refType = refType.WithReadOnlyKeyword(Token(SyntaxKind.ReadOnlyKeyword));
        yield return PropertyDeclaration(refType, EscapedIdentifier(name))
            .WithModifiers(access)
            .WithAttributeLists(v.PropertyAttributes)
            .WithAccessorList(AccessorList(SingletonList(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(Block(
                    GuardedReadPrelude(name),
                    ReturnStatement(RefExpression(EscapedIdentifierName(fieldName))))))));

        // public static void N__ensure() { if (!__Swept.Done) N__slow(); }   (D6)
        yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(name + GuardedEnsureSuffix))
            .WithModifiers(access)
            .WithBody(Block(GuardedReadPrelude(name)));

        if (v.IsReadOnly)
            yield break;

        // public static T N__set(T value) { if (__Swept.Done) N__field = value; else StaticInit.Store(...); return value; }
        yield return MethodDeclaration(v.Type, Identifier(name + GuardedSetSuffix))
            .WithModifiers(access)
            .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("value")).WithType(v.Type))))
            .WithBody(Block(
                GuardedStoreStatement(name, fieldName, IdentifierName("value")),
                ReturnStatement(IdentifierName("value"))));

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
            .WithBody(Block(
                IfStatement(
                    PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SweptDone()),
                    Block(
                        ExpressionStatement(InvocationExpression(StaticInitName("Load"))
                            .WithArgumentList(ArgumentList(SeparatedList(new[]
                            {
                                RefArgument(HostFieldName),
                                Argument(IdentifierName(SweepMethodName)),
                            })))),
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
        var boxValue = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("spill"), IdentifierName("Value"));
        yield return MethodDeclaration(RefType(v.Type), Identifier(name + GuardedOutSuffix))
            .WithModifiers(access)
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
                output.AddRange(GenerateGuardedStorage(name, first.Name, fieldName, type, null,
                    new SyntaxList<AttributeListSyntax>(), declareField: backing == null, generatedInitializer: initializer));
                var guarded = GuardPropertyAccessors(property, name, fieldName);
                output.Add(guarded);
                // The plain-store seam is one spelling for every Guarded value (D4): a property
                // default's N__set stores through its setter, which keeps get / set.
                if (guarded.AccessorList!.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)))
                {
                    output.Add(MethodDeclaration(type, Identifier(name + GuardedSetSuffix))
                        .WithModifiers(TokenList(guarded.Modifiers.Where(m => m.IsKind(SyntaxKind.PublicKeyword)
                            || m.IsKind(SyntaxKind.InternalKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword)
                            || m.IsKind(SyntaxKind.PrivateKeyword) || m.IsKind(SyntaxKind.StaticKeyword))))
                        .WithParameterList(ParameterList(SingletonSeparatedList(Parameter(Identifier("value")).WithType(type))))
                        .WithBody(Block(
                            ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                EscapedIdentifierName(name), IdentifierName("value"))),
                            ReturnStatement(IdentifierName("value")))));
                }
            }
            else if (ReferenceEquals(member, backing))
            {
                var variable = backing.Declaration.Variables[0].WithInitializer(null);
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

    private static StatementSyntax GuardStoresOf(StatementSyntax statement, string name, string fieldName)
        => statement is ExpressionStatementSyntax
        {
            Expression: AssignmentExpressionSyntax { Left: IdentifierNameSyntax left } assignment
        } && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) && left.Identifier.ValueText == fieldName
            ? GuardedStoreStatement(name, fieldName, assignment.Right)
            : statement;

    // ------------------------------------------------------------------------------------------- //
    // The use sites: a plain store and an `out` operand (D4)
    // ------------------------------------------------------------------------------------------- //

    /// <summary>
    /// Whether a store or operand <paramref name="target"/> names a Guarded value that takes a store:
    /// the recorded <see cref="CodeGenInfo.StaticStorage"/> of the symbol the checker bound, no const.
    /// Every such value has <c>N__set</c> (a property default's calls its setter, D4); the checker
    /// refuses an <c>out</c> operand on a const or a property (D7).
    /// </summary>
    private bool IsGuardedVariableTarget(Expression target)
    {
        var symbol = target switch
        {
            Identifier id => _context.SemanticInfo?.GetIdentifierSymbol(id)
                ?? (StoreAssignsExistingSlot(id) ? _context.LookupSymbol(id.Name) : null),
            MemberAccess ma => _context.SemanticInfo?.GetMemberAccessResolution(ma)?.Member
                ?? _context.SemanticInfo?.GetModuleMemberResolution(ma)?.Member,
            _ => null,
        };
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
    {
        if (IsGuardedVariableTarget(targetNode))
        {
            return InvocationExpression(GuardedMember(target, GuardedSetSuffix, targetNode))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));
        }
        return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, value);
    }

    /// <summary><see cref="PlainStore(Expression, ExpressionSyntax, ExpressionSyntax)"/> for a store
    /// site that carries its bound symbol's recorded facts rather than a target expression (a walrus).</summary>
    private ExpressionSyntax PlainStore(CodeGenInfo? info, Node node, ExpressionSyntax target, ExpressionSyntax value)
    {
        if (IsGuardedStore(info))
        {
            return InvocationExpression(GuardedMember(target, GuardedSetSuffix, node))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));
        }
        return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, value);
    }

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
    /// </summary>
    private ExpressionSyntax GenerateCallWithOutCommits(FunctionCall call, Func<ExpressionSyntax> generate)
    {
        var saved = _outCommits;
        _outCommits = null;
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
        }
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
                    CommitCall(commit, ReplacePlaceholder(result, commit.Placeholder!, SpillValue(commit.Spill)))));
                continue;
            }
            result = CommitCall(commit, result);
        }
        return result;
    }

    private static ExpressionSyntax CommitCall(OutCommit commit, ExpressionSyntax result)
        => InvocationExpression(commit.Commit).WithArgumentList(ArgumentList(SeparatedList(new[]
        {
            Argument(result),
            Argument(IdentifierName(commit.Spill)),
        })));

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
    /// reference lives across an <c>await</c>.
    /// </summary>
    private ExpressionSyntax GenerateWithAsyncEnsure(Expression node, Func<ExpressionSyntax> generate)
    {
        var generated = generate();
        if (AsyncGuardedUseOf(node) != AsyncGuardedUseKind.Ensure)
            return generated;
        HoistEvaluation(ExpressionStatement(InvocationExpression(GuardedMember(generated, GuardedEnsureSuffix, node))));
        return GuardedMember(generated, GuardedFieldSuffix, node);
    }
}
