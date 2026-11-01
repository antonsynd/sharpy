using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// The forward-read refusal of the static-initializer order contract (P75, #2396, owner ruling R-JT): a type's static
/// initializers — a <c>@static</c> field's initializer, a <c>const</c>'s when it is not a compile-time constant (it is
/// emitted <c>static readonly</c>), a <c>@static</c> property's default — run in SOURCE order, because the emitter
/// lays the type's members out as written and C# runs static initializers in the generated text's order. A static
/// initializer that reads a static sibling of the SAME type declared at or after it would see that sibling's default
/// value, so the read is refused (SPY0618): a <c>@static</c> field, a <c>@static</c> property, a <c>@static</c>
/// method (the call arm — a later method may read statics not initialized yet) and a <c>const</c> that is not a
/// compile-time constant. A compile-time <c>const</c> is order-free (C# <c>const</c>, #1795).
///
/// <para><b>Keyed on the resolved owner, never the spelling.</b> A <see cref="MemberAccess"/> is a read of a sibling
/// when its receiver denotes the enclosing type: a bare type name the checker bound to it, or any receiver the checker
/// marked a type reference whose recorded type is it — an alias (<c>type H = Host</c>, <c>H.x</c>), a module-qualified
/// name, a constructed reference to the enclosing generic (<c>Host[int].x</c>, the same storage for that
/// instantiation). A bare <see cref="Identifier"/> is one when the checker admitted it as a class-body sibling read
/// (<see cref="SemanticInfo.GetClassBodySiblingRead"/>): P21h's F1 refuses every bare forward read of a <c>@static</c>
/// member in the checker, and exempts every <c>const</c> — the compile-time fact is recorded after the checker's walk
/// (<c>ConstEligibility.AnalyzeModule</c>) — so the non-compile-time <c>const</c> is the one bare read this validator
/// refuses.</para>
///
/// <para><b>One anchor rule.</b> The sibling's declaration and the reader's are found by name through
/// <see cref="TypeBodyMembership.FirstDeclarationOf"/>, the helper the checker's F1 uses: a split property is ONE C#
/// property emitted at its FIRST half, so both the reader and the sibling anchor there (P21h verify C2).</para>
///
/// <para>A lambda inside an initializer is not walked: its body runs when it is called, not where the initializer
/// stands (#2398 owns that rule). A generator expression that IS the initializer's value is walked only through its
/// first iterable — the rest runs when the stored iterator is consumed (<see cref="WalkInitializer"/>). An instance
/// member read through the type name is the checker's SPY0290.</para>
/// </summary>
internal sealed class StaticInitializerOrderValidator : ValidatingAstWalker
{
    public override string Name => "StaticInitializerOrderValidator";

    // After ConstantPositionValidator (250): both read ConstEligibility's compile-time fact, recorded before the
    // pipeline runs.
    public override int Order => 251;

    public override void VisitClassDef(ClassDef node)
    {
        ValidateTypeBody(node, node.Name, node.Body);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        ValidateTypeBody(node, node.Name, node.Body);
        base.VisitStructDef(node);
    }

    // An interface body declares consts (static readonly when not compile-time) and is emitted in
    // source order too (GenerateInterfaceMembers).
    public override void VisitInterfaceDef(InterfaceDef node)
    {
        ValidateTypeBody(node, node.Name, node.Body);
        base.VisitInterfaceDef(node);
    }

    /// <summary>A static initializer of the type being validated: its declaration's anchor and display name.</summary>
    private readonly record struct StaticReader(Node Anchor, string Name, string Phrase);

    /// <summary>Walks every static initializer of one type body. Deliberately partial: only a field (a <c>@static</c>
    /// one, or a non-compile-time <c>const</c>) and a <c>@static</c> property with a default declare a static
    /// initializer; every other statement kind (a method, an event, a nested type — visited on its own — a
    /// <c>pass</c>) declares none (the static-initializer order contract, #2396).</summary>
    private void ValidateTypeBody(Statement definition, string typeName, ImmutableArray<Statement> body)
    {
        if (Context.DeclaredTypes?.SymbolFor(definition, typeName, Context.SymbolTable) is not { } owner)
            return;

        foreach (var statement in body)
        {
            switch (statement.UnwrapDecorated())
            {
                // A const on a constant cycle has its one verdict already (the checker's SPY0278 at its
                // declaration): its reads are not re-reported as order reads.
                case VariableDeclaration { InitialValue: { } initializer } field
                    when IsStaticInitializer(field, owner) && !(field.IsConst && IsOnConstantCycle(field)):
                    WalkInitializer(initializer, new StaticReader(field, field.Name,
                        field.IsConst ? "a 'const' initializer" : "a '@static' initializer"), owner, typeName, body);
                    break;

                case PropertyDef { DefaultValue: { } defaultValue } property when HasStatic(property.Decorators):
                    var anchor = property.ExplicitInterface == null
                        ? TypeBodyMembership.FirstDeclarationOf(body, property.Name) ?? property
                        : property;
                    WalkInitializer(defaultValue, new StaticReader(anchor, property.Name, "a '@static' initializer"), owner, typeName, body);
                    break;
            }
        }
    }

    /// <summary>Whether a field's initializer is a static initializer whose position is ordered: a <c>@static</c>
    /// field, or a <c>const</c> that is not a compile-time constant (a compile-time const's initializer reads only
    /// compile-time constants, which are order-free).</summary>
    private bool IsStaticInitializer(VariableDeclaration field, TypeSymbol owner)
        => field.IsConst ? !IsCompileTimeConst(owner, field.Name) : HasStatic(field.Decorators);

    private bool IsCompileTimeConst(TypeSymbol owner, string name)
        => owner.Fields.FirstOrDefault(f => f.Name == name) is { } symbol
            && ConstEligibility.IsCompileTimeConstant(Context.SemanticBinding, symbol);

    private static bool HasStatic(ImmutableArray<Decorator> decorators)
        => decorators.Any(d => d.Name == DecoratorNames.Static);

    /// <summary>An initializer whose whole value is a generator expression STORES a deferred iterator: its element,
    /// its conditions and every iterable after the first run when the iterator is consumed, not where the initializer
    /// stands — like an earlier method's body, which may read a later static (C#'s rule, admitted in both spellings) —
    /// so only the first iterable, which Python and the lowered LINQ chain evaluate eagerly, is walked. A generator
    /// CONSUMED inside the initializer (<c>sum(x + Host.s for x in xs)</c>) runs there and is walked whole: whether a
    /// nested generator is consumed is not decidable here, so only the stored root is exempt (P75 verify, owner ruling
    /// 2026-11-01).</summary>
    private void WalkInitializer(Expression initializer, StaticReader reader, TypeSymbol owner, string typeName,
        ImmutableArray<Statement> body)
    {
        var root = initializer;
        while (root is Parenthesized parenthesized)
            root = parenthesized.Expression;
        if (root is GeneratorExpression { Clauses: [ForClause first, ..] })
        {
            Walk(first.Iterator, reader, owner, typeName, body);
            return;
        }

        Walk(initializer, reader, owner, typeName, body);
    }

    /// <summary>The initializer's reads, outside any lambda — deliberately partial: a lambda's body runs when it is
    /// called, not where the initializer stands (#2398), and every node kind but a type-qualified member access and an
    /// admitted bare sibling read is only descended into. <paramref name="isCallee"/> is whether
    /// <paramref name="node"/> is the function a call invokes: a later <c>@static</c> method is refused only when it is
    /// called — a method group read as a value creates a delegate and runs nothing.</summary>
    private void Walk(Node node, StaticReader reader, TypeSymbol owner, string typeName, ImmutableArray<Statement> body,
        bool isCallee = false)
    {
        switch (node)
        {
            case LambdaExpression:
                return;
            case MemberAccess access when !access.IsNullConditional && DenotesType(access.Object, owner):
                Check(access, access.Member, reader, owner, typeName, body, isCallee);
                break;
            case Identifier id when Context.SemanticInfo.GetClassBodySiblingRead(id) != null:
                Check(id, id.Name, reader, owner, typeName, body, isCallee);
                break;
        }

        var function = (node as FunctionCall)?.Function;
        foreach (var child in node.GetChildNodes())
            Walk(child, reader, owner, typeName, body, isCallee: ReferenceEquals(child, function));
    }

    /// <summary>Whether <paramref name="receiver"/> denotes <paramref name="owner"/> — by the symbol the checker bound
    /// or the type it recorded, never by how the receiver is spelled.</summary>
    private bool DenotesType(Expression receiver, TypeSymbol owner)
        => ReferenceEquals(DenotedType(receiver), owner);

    /// <summary>The type a receiver denotes: a type name the checker bound; a receiver it marked a type reference
    /// whose recorded type names one (an alias, a module-qualified name, a constructed generic); or a NESTED type
    /// reached through its declaring type (<c>Outer.Inner</c>) — a chain the member-typing arms leave untyped when it
    /// is itself a qualifier (#1941), answered structurally as the checker's <c>DenotedTypeSymbolOf</c> answers it.
    /// Null for a value receiver.</summary>
    private TypeSymbol? DenotedType(Expression receiver)
    {
        var info = Context.SemanticInfo;
        if (receiver is Identifier id && info.GetIdentifierSymbol(id) is TypeSymbol bound)
            return bound;
        if (info.IsTypeReference(receiver)
            && info.GetExpressionType(receiver) switch
            {
                UserDefinedType { Symbol: { } symbol } => symbol,
                GenericType { GenericDefinition: { } definition } => definition,
                _ => null,
            } is { } denoted)
        {
            return denoted;
        }

        return receiver is MemberAccess { IsNullConditional: false } nested && DenotedType(nested.Object) is { } outer
            ? outer.NestedTypes.FirstOrDefault(n => n.Name == nested.Member)
            : null;
    }

    /// <summary>Refuses the read of <paramref name="member"/> at <paramref name="read"/> when the member's declaration
    /// in this body is ordered storage standing at or after the reader's.</summary>
    private void Check(Expression read, string member, StaticReader reader, TypeSymbol owner, string typeName,
        ImmutableArray<Statement> body, bool isCallee)
    {
        if (TypeBodyMembership.FirstDeclarationOf(body, member) is not { } declaration
            || OrderedKind(declaration, owner) is not { } kind
            || (kind == OrderedMember.Method && !isCallee))
        {
            return;
        }

        // A const reading itself is a constant cycle, the checker's SPY0278 wherever it is written.
        var self = ReferenceEquals(declaration, reader.Anchor);
        if (self && kind == OrderedMember.NonConstantConst)
            return;
        if (!self && (declaration.LineStart, declaration.ColumnStart).CompareTo(
                (reader.Anchor.LineStart, reader.Anchor.ColumnStart)) < 0)
        {
            return;
        }

        if (HasErrorAtPosition(read.LineStart, read.ColumnStart))
            return;

        AddError(Describe(typeName, member, reader, kind, self), read.LineStart, read.ColumnStart,
            code: DiagnosticCodes.SemanticOverflow.StaticInitializerForwardRead, span: read.Span);
    }

    private enum OrderedMember
    {
        Field,
        NonConstantConst,
        Property,
        Method,
    }

    /// <summary>The kind of a type-body declaration whose storage a static initializer reads in order, or null — an
    /// instance member, a compile-time const, an event, anything else. Deliberately partial (the order contract,
    /// R-JT): only ordered static storage and the <c>@static</c> method a call runs are ordered.</summary>
    private OrderedMember? OrderedKind(Node declaration, TypeSymbol owner) => declaration switch
    {
        VariableDeclaration { IsConst: true } constant =>
            IsCompileTimeConst(owner, constant.Name) ? null : OrderedMember.NonConstantConst,
        VariableDeclaration field when HasStatic(field.Decorators) => OrderedMember.Field,
        PropertyDef property when HasStatic(property.Decorators) => OrderedMember.Property,
        FunctionDef method when HasStatic(method.Decorators)
            || !method.Parameters.Any(p => string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            => OrderedMember.Method,
        _ => null,
    };

    private static string Describe(string typeName, string member, StaticReader reader, OrderedMember kind, bool self)
    {
        var qualified = $"'{typeName}.{member}'";
        if (self)
        {
            return $"{qualified} is read in {reader.Phrase} before it is initialized: '{member}' reads itself, and the "
                + $"member is {DeclaredLaterPhrase} than its own initializer runs, so the read would see its default "
                + $"value. Initialize '{member}' from a member declared above it, or in a method";
        }

        if (kind == OrderedMember.Method)
        {
            return $"{qualified} is called in {reader.Phrase} before it is declared: '{member}' is {DeclaredLaterPhrase} "
                + $"than '{reader.Name}' in '{typeName}', and static initializers run top to bottom, so the method could "
                + $"read a static that is not initialized yet. Move '{member}' above '{reader.Name}', or call it in a method";
        }

        var message = $"{qualified} is read in {reader.Phrase} before it is initialized: '{member}' is {DeclaredLaterPhrase} "
            + $"than '{reader.Name}' in '{typeName}', and static initializers run top to bottom, so the read would see its "
            + $"default value. Move '{member}' above '{reader.Name}', or read it in a method";
        return kind == OrderedMember.NonConstantConst
            ? message + $", or make '{member}' a compile-time constant"
            : message;
    }

    /// <summary>The phrase every order refusal carries — P21h's F1 text and this one agree.</summary>
    private const string DeclaredLaterPhrase = "declared later";

    private bool IsOnConstantCycle(VariableDeclaration constant)
    {
        var errors = Context.Diagnostics.GetErrors();
        for (int i = 0; i < errors.Count; i++)
        {
            if (errors[i].Code == DiagnosticCodes.Semantic.CircularConstantReference
                && errors[i].Line == constant.LineStart && errors[i].Column == constant.ColumnStart)
                return true;
        }

        return false;
    }

    private bool HasErrorAtPosition(int line, int column)
    {
        var errors = Context.Diagnostics.GetErrors();
        for (int i = 0; i < errors.Count; i++)
        {
            if (errors[i].Line == line && errors[i].Column == column)
                return true;
        }

        return false;
    }
}
