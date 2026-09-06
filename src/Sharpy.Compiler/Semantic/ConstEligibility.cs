using System.Numerics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Why a <c>const</c> is not a C# compile-time constant. Decided ONCE by
/// <see cref="ConstEligibility"/> and stored with the fact, so every refusing consumer — a
/// parameter default (SPY0401), a decorator argument (SPY0425), a match-case constant (SPY0605) —
/// steers from the same cause instead of re-deriving one.
/// </summary>
public enum ConstIneligibilityCause
{
    /// <summary>The const IS a compile-time constant (or was never analysed).</summary>
    None,

    /// <summary>Declared <c>T?</c> — an <c>Optional&lt;T&gt;</c> struct has no constant form.</summary>
    OptionalType,

    /// <summary>Declared <c>T | None</c> over a value type — C# has no <c>const int?</c>.</summary>
    NullableType,

    /// <summary>Declared as a type C# does not admit for <c>const</c> at all.</summary>
    IneligibleType,

    /// <summary>The initializer is a call (or a constructor / collection / lambda).</summary>
    CallInitializer,

    /// <summary>The initializer uses an operator whose lowering is a call.</summary>
    CallLoweredOperator,

    /// <summary>The initializer reads another const that is not itself compile-time.</summary>
    NonConstantReference,

    /// <summary>An integer const whose initializer does not fold to a value (#1460).</summary>
    UnfoldedInteger,
}

/// <summary>
/// The compile-time-constant fact for one <c>const</c> symbol: the answer, why it is negative, and
/// the declared type as the user SPELLED it (<c>int?</c>, not the canonical <c>int32?</c>) so a
/// refusal quotes source rather than a display name.
/// </summary>
public readonly record struct ConstFact(
    bool IsCompileTime, ConstIneligibilityCause Cause, string? DeclaredSpelling = null);

/// <summary>
/// ONE compile-time-constant fact per <c>const</c> symbol, computed once for EVERY host — module
/// body, function body (and any nested block), class field, struct field, interface field and the
/// body of a NESTED type — and read by every consumer (the emitter's three <c>const</c> sites via
/// <see cref="CodeGenInfo.IsCompileTimeConstant"/>, and <c>ConstantPositionValidator</c> for
/// parameter defaults, decorator arguments and match-case constant patterns).
///
/// <para><b>Contract.</b> A <c>const</c> is a C# compile-time constant iff
/// (1) its declared type is one C# admits for <c>const</c> — every <c>PrimitiveCatalog</c> primitive
/// except <c>object</c>/<c>void</c>, plus enum types; (2) <c>ConstantDefaultClassifier</c> admits its
/// initializer for <see cref="Validation.AdmissionTable.ConstInitializer"/>, with the two hooks a
/// shape cannot carry — "the referenced const is itself compile-time" and "this operator node lowers
/// to a C# constant operator"; and (3) for integer kinds only, the initializer folds to a value
/// (<see cref="VariableSymbol.ConstantValue"/>, the #1460 range/overflow gate).</para>
///
/// <para><b>The fold is part of this seam</b> (#1791). The checker folds module-level and
/// function-level consts at its declaration arms, but a class/struct FIELD const never reaches
/// them — so gating integers on a fold only some hosts run made <c>class C: const K: int = 1</c>
/// answer false and emit <c>static readonly</c>. This analysis folds any const whose value the
/// checker did not already produce, through the same pure <see cref="IntegerConstantEvaluator"/>
/// and the same const resolver, so every host is gated by one fold.</para>
///
/// <para><b>Ordering.</b> Runs at the end of <see cref="TypeChecker.CheckModule"/>, after the
/// statement pass (it needs expression types and recorded operator lowerings) and BEFORE
/// <c>_validationPipeline.Validate</c>, so the validator reads a finished fact.
/// The result is stored symbol-keyed on <see cref="SemanticBinding.SetCompileTimeConstant"/>;
/// <c>CodeGenInfoComputer</c> and <see cref="LocalNameAllocator"/> copy it onto
/// <see cref="CodeGenInfo.IsCompileTimeConstant"/> so the emitter reads a frozen fact (Rule 2a).</para>
/// </summary>
internal sealed class ConstEligibility
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticBinding _semanticBinding;
    private readonly SemanticInfo? _semanticInfo;

    /// <summary>
    /// One <c>const</c> declaration the walk found, with the scope that owns it. <see cref="Ordinal"/>
    /// is its position among that scope's consts, so a scope that does NOT allow forward references
    /// (a function body or a nested block, where C# and the checker both resolve in order) can refuse
    /// a later declaration while a module or type body accepts one.
    /// </summary>
    private sealed class ConstEntry
    {
        public ConstEntry(VariableDeclaration decl, VariableSymbol symbol, ConstScope scope, int ordinal)
        {
            Decl = decl;
            Symbol = symbol;
            Scope = scope;
            Ordinal = ordinal;
        }

        public VariableDeclaration Decl { get; }
        public VariableSymbol Symbol { get; }
        public ConstScope Scope { get; }
        public int Ordinal { get; }
    }

    /// <summary>
    /// A declaration index for one lexical scope. <see cref="AllowsForwardReference"/> is true for
    /// the module body and for type bodies (C# resolves const dependency order itself) and false for
    /// function bodies and nested blocks.
    /// </summary>
    private sealed class ConstScope
    {
        public ConstScope(ConstScope? parent, bool allowsForwardReference)
        {
            Parent = parent;
            AllowsForwardReference = allowsForwardReference;
        }

        public ConstScope? Parent { get; }
        public bool AllowsForwardReference { get; }
        public Dictionary<string, ConstEntry> ByName { get; } = new(StringComparer.Ordinal);
        public int Count { get; set; }
    }

    private readonly Dictionary<VariableSymbol, ConstEntry> _bySymbol = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ConstEntry, bool> _fact = new();
    private readonly HashSet<ConstEntry> _factInProgress = new();
    private readonly HashSet<ConstEntry> _foldInProgress = new();

    public ConstEligibility(SymbolTable symbolTable, SemanticBinding semanticBinding, SemanticInfo? semanticInfo)
    {
        _symbolTable = symbolTable;
        _semanticBinding = semanticBinding;
        _semanticInfo = semanticInfo;
    }

    /// <summary>
    /// Analyze every <c>const</c> declaration the module contains — at any depth, in any host — and
    /// store the compile-time fact on <see cref="SemanticBinding"/>.
    /// </summary>
    public void AnalyzeModule(Module module)
    {
        _bySymbol.Clear();
        _fact.Clear();
        _factInProgress.Clear();
        _foldInProgress.Clear();

        var moduleScope = new ConstScope(parent: null, allowsForwardReference: true);
        Collect(module.Body, moduleScope, owningType: null);

        // Fold first (a fact reads the fold), then record every fact.
        foreach (var entry in _bySymbol.Values)
            EnsureFolded(entry);

        foreach (var entry in _bySymbol.Values)
        {
            var fact = ComputeFact(entry);
            _semanticBinding.SetCompileTimeConstant(
                entry.Symbol,
                fact,
                ComputeCause(entry, fact),
                entry.Decl.Type != null ? TypeAnnotationHelper.GetName(entry.Decl.Type) : null);
            // The fact also rides the SYMBOL, because a project build gives every file its own
            // SemanticBinding: the exporting module's binding is not the importing module's, and
            // the symbol is the one object both see (ProjectCompiler.ResolveOwnExportedSymbol).
            // SymbolSerializer round-trips it, so a warm build reads the same answer as a cold one.
            entry.Symbol.IsCompileTimeConstant = fact;
        }
    }

    // ── The walk ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Collects the <c>const</c> declarations of one statement list. Type bodies and function bodies
    /// open a new scope; every other statement kind recurses into its child STATEMENTS in the same
    /// scope, so no statement kind can be missed by omission (a block does not own const names for
    /// this index — the checker's own scope chain does — and the index is only the fallback for
    /// identifiers the checker did not bind).
    /// </summary>
    private void Collect(IEnumerable<Statement> body, ConstScope scope, TypeSymbol? owningType)
    {
        foreach (var raw in body)
        {
            var stmt = raw.UnwrapDecorated();
            switch (stmt)
            {
                case VariableDeclaration { IsConst: true } constDecl:
                    Declare(constDecl, scope, owningType);
                    break;

                case ClassDef classDef:
                    CollectTypeBody(classDef.Name, classDef.Body, scope, owningType);
                    break;

                case StructDef structDef:
                    CollectTypeBody(structDef.Name, structDef.Body, scope, owningType);
                    break;

                case InterfaceDef interfaceDef:
                    CollectTypeBody(interfaceDef.Name, interfaceDef.Body, scope, owningType);
                    break;

                case FunctionDef funcDef:
                    // A function body is a fresh, order-sensitive scope, and its consts are locals
                    // (never fields) however deeply the function nests inside a type.
                    Collect(funcDef.Body, new ConstScope(scope, allowsForwardReference: false), owningType: null);
                    break;

                default:
                    // Every remaining statement kind (if / while / for / with / try / match /
                    // defer / …) contributes its child statements to the SAME scope.
                    Collect(stmt.GetChildNodes().OfType<Statement>(), scope, owningType);
                    break;
            }
        }
    }

    private void CollectTypeBody(string typeName, IEnumerable<Statement> body, ConstScope scope, TypeSymbol? enclosing)
    {
        var typeSymbol = ResolveTypeSymbol(typeName, enclosing);
        // A type body allows forward references, as C# does.
        Collect(body, new ConstScope(scope, allowsForwardReference: true), typeSymbol);
    }

    /// <summary>
    /// The declaring type of a nested type is its enclosing <see cref="TypeSymbol.NestedTypes"/>;
    /// a top-level type is looked up in the symbol table. A type declared inside a function has no
    /// resolvable symbol here — its field consts get no fact and the emitter keeps the non-const
    /// shape.
    /// </summary>
    private TypeSymbol? ResolveTypeSymbol(string name, TypeSymbol? enclosing)
        => enclosing != null
            ? enclosing.NestedTypes.FirstOrDefault(t => t.Name == name)
            : _symbolTable.Lookup(name) as TypeSymbol;

    private void Declare(VariableDeclaration decl, ConstScope scope, TypeSymbol? owningType)
    {
        var symbol = ResolveDeclaredSymbol(decl, owningType);
        if (symbol == null)
            return;

        var entry = new ConstEntry(decl, symbol, scope, scope.Count++);
        scope.ByName[decl.Name] = entry;
        _bySymbol[symbol] = entry;
    }

    /// <summary>
    /// The symbol a <c>const</c> declaration declares: a field of the owning type, or — for module
    /// and function-level consts — the symbol the checker recorded for the declaration node
    /// (<see cref="SemanticInfo.GetDeclarationSymbol"/>), falling back to the symbol table.
    /// </summary>
    private VariableSymbol? ResolveDeclaredSymbol(VariableDeclaration decl, TypeSymbol? owningType)
    {
        if (owningType != null)
        {
            return owningType.Fields.FirstOrDefault(
                f => f.Name == decl.Name && f.IsNameBacktickEscaped == decl.IsNameBacktickEscaped);
        }

        if (_semanticInfo?.GetDeclarationSymbol(decl) is VariableSymbol declared)
            return declared;

        return _symbolTable.Lookup(decl.Name) is VariableSymbol looked
            && looked.IsConstant
            && looked.IsNameBacktickEscaped == decl.IsNameBacktickEscaped
                ? looked
                : null;
    }

    // ── The fact ─────────────────────────────────────────────────────────────────────────────

    private bool ComputeFact(ConstEntry entry)
    {
        if (_fact.TryGetValue(entry, out var known))
            return known;
        if (!_factInProgress.Add(entry))
            return false; // reference cycle: no member of it can fold

        var result = Compute(entry);

        _factInProgress.Remove(entry);
        _fact[entry] = result;
        return result;
    }

    private bool Compute(ConstEntry entry)
    {
        if (entry.Decl.InitialValue == null)
            return false;

        var type = GetVariableType(entry.Symbol);
        if (!IsConstEligibleSemanticType(type))
            return false;

        var kind = Validation.ConstantDefaultClassifier.Classify(
            entry.Decl.InitialValue,
            id => ResolvesToCompileTimeConst(id, entry),
            LowersToConstantExpression,
            ResolvesToCompileTimeMemberConst);
        if (!Validation.ConstantDefaultClassifier.IsAdmitted(kind, Validation.AdmissionTable.ConstInitializer))
            return false;

        // Integer kinds require a folded value (range/overflow gate #1460) — at EVERY host.
        var primitiveInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(type);
        if (primitiveInfo != null && IsIntegerKind(primitiveInfo))
            return entry.Symbol.ConstantValue != null;

        return true;
    }

    /// <summary>
    /// The cause a refusing consumer steers from. Reads the same inputs the fact read, in the same
    /// order, so the cause and the answer cannot disagree.
    /// </summary>
    private ConstIneligibilityCause ComputeCause(ConstEntry entry, bool isCompileTime)
    {
        if (isCompileTime)
            return ConstIneligibilityCause.None;

        var type = GetVariableType(entry.Symbol);
        if (type is OptionalType)
            return ConstIneligibilityCause.OptionalType;
        if (type is NullableType)
            return ConstIneligibilityCause.NullableType;
        if (!IsConstEligibleSemanticType(type))
            return ConstIneligibilityCause.IneligibleType;
        if (entry.Decl.InitialValue == null)
            return ConstIneligibilityCause.IneligibleType;

        var kind = Validation.ConstantDefaultClassifier.Classify(
            entry.Decl.InitialValue,
            id => ResolvesToCompileTimeConst(id, entry),
            LowersToConstantExpression,
            ResolvesToCompileTimeMemberConst);

        return kind switch
        {
            Validation.EmittableConstantKind.Call
                or Validation.EmittableConstantKind.CaseConstructor
                or Validation.EmittableConstantKind.Collection
                or Validation.EmittableConstantKind.Comprehension
                or Validation.EmittableConstantKind.Lambda => ConstIneligibilityCause.CallInitializer,
            Validation.EmittableConstantKind.Other when entry.Decl.InitialValue is FunctionCall =>
                ConstIneligibilityCause.CallInitializer,
            Validation.EmittableConstantKind.Other when entry.Decl.InitialValue is BinaryOp or UnaryOp
                or ConditionalExpression => ConstIneligibilityCause.CallLoweredOperator,
            Validation.EmittableConstantKind.Other when entry.Decl.InitialValue is Identifier =>
                ConstIneligibilityCause.NonConstantReference,
            _ => ConstIneligibilityCause.UnfoldedInteger,
        };
    }

    private static bool IsIntegerKind(Registry.PrimitiveCatalog.PrimitiveInfo info)
        => info.Kind is Registry.PrimitiveCatalog.NumericKind.SignedInteger
            or Registry.PrimitiveCatalog.NumericKind.UnsignedInteger;

    // ── The fold (one seam for every host) ───────────────────────────────────────────────────

    /// <summary>
    /// Ensures <paramref name="entry"/>'s integer value is folded. The checker's declaration arms
    /// already fold module-level and function-level consts; a class/struct field const reaches no
    /// such arm, so this fills the gap through the same evaluator and the same resolver — one fold
    /// rule for every host.
    /// </summary>
    private BigInteger? EnsureFolded(ConstEntry entry)
    {
        if (entry.Symbol.ConstantValue != null)
            return entry.Symbol.ConstantValue;
        if (entry.Decl.InitialValue == null)
            return null;

        var info = Registry.PrimitiveCatalog.GetPrimitiveInfo(GetVariableType(entry.Symbol));
        if (info == null || !IsIntegerKind(info))
            return null;

        if (!_foldInProgress.Add(entry))
            return null; // reference cycle

        if (IntegerConstantEvaluator.TryGetConstantInteger(
                entry.Decl.InitialValue, out var folded, id => ResolveConstantInteger(id, entry)))
        {
            entry.Symbol.ConstantValue = folded;
        }

        _foldInProgress.Remove(entry);
        return entry.Symbol.ConstantValue;
    }

    private BigInteger? ResolveConstantInteger(Identifier id, ConstEntry from)
    {
        if (Resolve(id, from) is { } referenced)
            return EnsureFolded(referenced);

        return _symbolTable.Lookup(id.Name) is VariableSymbol { IsConstant: true } sym
            && sym.IsNameBacktickEscaped == id.IsNameBacktickEscaped
                ? sym.ConstantValue
                : null;
    }

    // ── The classifier's two hooks ───────────────────────────────────────────────────────────

    /// <summary>
    /// The classifier's identifier hook: the name resolves iff it denotes a <c>const</c> that is
    /// itself a compile-time constant. Resolution prefers the symbol the CHECKER bound to this
    /// identifier (scope-correct); the per-scope declaration index is the fallback for identifiers
    /// no binding was recorded for, and an imported symbol answers from its own carried fact.
    /// </summary>
    private bool ResolvesToCompileTimeConst(Identifier id, ConstEntry from)
    {
        if (Resolve(id, from) is { } referenced)
            return ComputeFact(referenced);

        return _symbolTable.Lookup(id.Name) is VariableSymbol { IsConstant: true } sym
            && sym.IsNameBacktickEscaped == id.IsNameBacktickEscaped
            && IsImportedCompileTimeConstant(sym);
    }

    /// <summary>
    /// The classifier's qualified-name hook: for <c>Holder.A</c> / <c>Outer.Holder.A</c>, whether the
    /// member it resolves to is a <c>const</c> that is itself compile-time. Null when the chain names
    /// something other than a const (an enum member, a CLR static), where the shape rule stands.
    /// </summary>
    private bool? ResolvesToCompileTimeMemberConst(MemberAccess member)
        => ResolvesToCompileTimeMember(member, _semanticInfo, _semanticBinding);

    /// <summary>
    /// Whether a qualified name in a constant position names something C# will accept there.
    /// A FIELD of a non-enum type answers from the fact — a plain (non-<c>const</c>) field is never
    /// a constant expression, and a <c>const</c> field answers from its own fact. Everything else
    /// (an enum member, a method group, a nested type, a member the checker did not resolve)
    /// answers null and the classifier's shape rule stands.
    /// </summary>
    internal static bool? ResolvesToCompileTimeMember(
        MemberAccess member, SemanticInfo? semanticInfo, SemanticBinding binding)
    {
        var resolved = ResolveQualifiedField(member, semanticInfo);
        if (resolved == null)
            return null;

        var (owner, field) = resolved.Value;
        if (owner.TypeKind == TypeKind.Enum)
            return null;

        return field.IsConstant && IsCompileTimeConstant(binding, field);
    }

    /// <summary>
    /// The (owner, field) a qualified name denotes. One level (<c>Holder.A</c>, <c>Color.RED</c>) is
    /// the resolution the CHECKER recorded. A deeper chain (<c>Outer.Holder.A</c>) is not recorded as
    /// one node, so it is followed through the type symbols themselves — nested type by nested type,
    /// then the field — which is the same structure the walk uses to find the declaration. Returns
    /// null for anything that is not a field of a type (a method group, a nested type, a module
    /// member, a receiver expression), where the classifier's shape rule stands.
    /// </summary>
    private static (TypeSymbol Owner, VariableSymbol Field)? ResolveQualifiedField(
        MemberAccess member, SemanticInfo? semanticInfo)
    {
        if (semanticInfo == null)
            return null;

        if (semanticInfo.GetMemberAccessResolution(member) is
            { Owner: { } recordedOwner, Member: VariableSymbol recordedField })
        {
            return (recordedOwner, recordedField);
        }

        var names = new List<string>();
        Expression current = member;
        while (current is MemberAccess access)
        {
            names.Insert(0, access.Member);
            current = access.Object;
        }

        if (current is not Identifier root || names.Count < 2)
            return null;

        if (semanticInfo.GetIdentifierSymbol(root) is not TypeSymbol type)
            return null;

        for (var i = 0; i < names.Count - 1; i++)
        {
            type = type.NestedTypes.FirstOrDefault(t => t.Name == names[i])!;
            if (type == null)
                return null;
        }

        var field = type.Fields.FirstOrDefault(f => f.Name == names[^1]);
        return field == null ? null : (type, field);
    }

    /// <summary>
    /// The <c>const</c> DECLARATION a qualified name resolves to, or null. Reads only what the
    /// checker recorded (<see cref="SemanticInfo.GetMemberAccessResolution"/>) — no re-resolution.
    ///
    /// <para>An ENUM MEMBER is deliberately not one: name resolution registers every member as a
    /// static constant field, but <c>Color.RED</c> is a C# enum member, always a constant expression,
    /// and it has no initializer of its own for this analysis to judge.</para>
    /// </summary>
    internal static VariableSymbol? MemberConstSymbol(MemberAccess member, SemanticInfo? semanticInfo)
        => ResolveQualifiedField(member, semanticInfo) is
            { Owner.TypeKind: not TypeKind.Enum, Field: { IsConstant: true } sym }
            ? sym
            : null;

    /// <summary>
    /// The entry an identifier inside <paramref name="from"/>'s initializer denotes, or null.
    /// </summary>
    private ConstEntry? Resolve(Identifier id, ConstEntry from)
    {
        if (_semanticInfo?.GetIdentifierSymbol(id) is VariableSymbol bound
            && bound.IsNameBacktickEscaped == id.IsNameBacktickEscaped
            && _bySymbol.TryGetValue(bound, out var byBinding))
        {
            return byBinding;
        }

        for (var scope = from.Scope; scope != null; scope = scope.Parent)
        {
            if (!scope.ByName.TryGetValue(id.Name, out var candidate)
                || candidate.Symbol.IsNameBacktickEscaped != id.IsNameBacktickEscaped)
            {
                continue;
            }
            // An order-sensitive scope (function body, nested block) cannot see a LATER const.
            if (!scope.AllowsForwardReference && scope == from.Scope && candidate.Ordinal >= from.Ordinal)
                return null;
            return candidate;
        }

        return null;
    }

    /// <summary>
    /// The classifier's operator hook: whether the checker's recorded lowering for one operator node
    /// is a C# constant operator. Reads only recorded facts — never operator names alone.
    ///
    /// <para>The BinaryOp roster names the operators whose NATIVE form is a C# constant expression;
    /// <c>**</c> is on it because the checker folds a constant integer power to a literal and
    /// records NO power lowering for it, while every unfolded power (<c>2.0 ** 3.0</c> →
    /// <c>FloatPow</c>, <c>x ** 2</c> → <c>IntegerPowInt</c>) records one and is refused by the tail
    /// check below. <c>is</c>/<c>is not</c> is on it because <c>None is None</c> emits
    /// <c>null == null</c>, while an Optional test records <c>OptionalNoneTest</c>.</para>
    /// </summary>
    private bool LowersToConstantExpression(Expression node) =>
        LowersToConstantExpression(node, _semanticInfo);

    internal static bool LowersToConstantExpression(Expression node, SemanticInfo? semanticInfo)
    {
        if (semanticInfo == null)
            return false;

        if (node is BinaryOp binary)
        {
            if (binary.Operator is not (BinaryOperator.Add or BinaryOperator.Subtract
                or BinaryOperator.Multiply or BinaryOperator.Divide or BinaryOperator.Power
                or BinaryOperator.Equal or BinaryOperator.NotEqual
                or BinaryOperator.Is or BinaryOperator.IsNot
                or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                or BinaryOperator.And or BinaryOperator.Or
                or BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or BinaryOperator.BitwiseXor
                or BinaryOperator.LeftShift or BinaryOperator.RightShift))
                return false;

            if (semanticInfo.GetBinaryOpLoweringForIr(binary) != BinaryOpLowering.NativeOperator)
                return false;

            if (!IsConstantOperand(binary.Left, semanticInfo) || !IsConstantOperand(binary.Right, semanticInfo))
                return false;

            if (binary.Operator is BinaryOperator.And or BinaryOperator.Or
                && !(IsBoolTyped(binary.Left, semanticInfo) && IsBoolTyped(binary.Right, semanticInfo)))
                return false;
        }
        else if (node is UnaryOp { Operator: UnaryOperator.Not } logicalNot)
        {
            if (!IsBoolTyped(logicalNot.Operand, semanticInfo))
                return false;
        }
        else if (node is ConditionalExpression cond)
        {
            if (!IsBoolTyped(cond.Test, semanticInfo))
                return false;
        }

        var lowering = semanticInfo.GetOperatorLowering(node);
        return lowering == null || lowering.Kind is OperatorLoweringKind.Native
            or OperatorLoweringKind.TrueDivisionCastLeft
            or OperatorLoweringKind.ShiftCountCastToInt
            or OperatorLoweringKind.NegateLiteralInt
            or OperatorLoweringKind.NegateLiteralLong;
    }

    // ── The type predicate ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether <paramref name="type"/> is a C#-const-eligible type.
    /// </summary>
    internal static bool IsConstEligibleSemanticType(SemanticType type)
    {
        // Primitive types (except object and void).
        var info = Registry.PrimitiveCatalog.GetPrimitiveInfo(type);
        if (info != null)
            return info.ClrType != typeof(object) && info.ClrType != typeof(void);

        // Enum types: C# admits const for enums.
        if (type is UserDefinedType { Symbol.TypeKind: TypeKind.Enum })
            return true;

        return false;
    }

    // ── The one reader every consumer uses ───────────────────────────────────────────────────

    /// <summary>
    /// THE compile-time-constant answer for one const symbol, for every consumer: the fact this
    /// analysis recorded for a const declared in the file under compilation, or — for a symbol
    /// imported from another module or a compiled reference — the fact that travelled with it.
    /// </summary>
    internal static bool IsCompileTimeConstant(SemanticBinding binding, VariableSymbol symbol)
    {
        if (binding.TryGetCompileTimeConstant(symbol, out var recorded))
            return recorded;
        return IsImportedCompileTimeConstant(symbol);
    }

    /// <summary>
    /// The fact for a symbol this analysis never walked: an in-project import (ModuleLoader
    /// computed it from the exporting module's declaration) or a compiled reference (an integer
    /// const carries its folded value).
    /// </summary>
    private static bool IsImportedCompileTimeConstant(VariableSymbol symbol)
        => symbol.IsCompileTimeConstant || symbol.ConstantValue != null;

    /// <summary>
    /// The exported-const rule for <c>ModuleLoader</c>, which reads a module's AST without a
    /// SemanticInfo and therefore has no recorded operator lowerings. It admits only the shapes
    /// whose C# form is a constant expression for EVERY operand type the declaration could have:
    /// a literal, an enum member, a reference to another exported const that is itself compile-time,
    /// and folds/conditionals over those using operators that are native regardless of operand type.
    /// <c>*</c> (str repeat), ordering comparisons (ordinal string compare), <c>//</c>, <c>%</c> and
    /// <c>**</c> are excluded because their lowering depends on operand types this route cannot see;
    /// an integer const answers from its folded value instead, which is exact.
    /// </summary>
    internal static bool IsExportedConstCompileTime(
        SemanticType declaredType,
        Expression? initializer,
        BigInteger? foldedValue,
        Func<Identifier, bool> constResolver)
    {
        if (initializer == null)
            return false;
        if (!IsConstEligibleSemanticType(declaredType))
            return false;

        var info = Registry.PrimitiveCatalog.GetPrimitiveInfo(declaredType);
        if (info != null && IsIntegerKind(info))
            return foldedValue != null;

        var kind = Validation.ConstantDefaultClassifier.Classify(
            initializer, constResolver, IsOperandTypeIndependentNativeOperator);
        return Validation.ConstantDefaultClassifier.IsAdmitted(
            kind, Validation.AdmissionTable.ConstInitializer);
    }

    private static bool IsOperandTypeIndependentNativeOperator(Expression node) => node switch
    {
        BinaryOp binary => binary.Operator is BinaryOperator.Add or BinaryOperator.Subtract
            or BinaryOperator.Divide
            or BinaryOperator.Equal or BinaryOperator.NotEqual
            or BinaryOperator.And or BinaryOperator.Or
            or BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or BinaryOperator.BitwiseXor
            or BinaryOperator.LeftShift or BinaryOperator.RightShift,
        UnaryOp unary => unary.Operator is UnaryOperator.Not or UnaryOperator.Minus
            or UnaryOperator.Plus or UnaryOperator.BitwiseNot,
        ConditionalExpression => true,
        _ => true,
    };

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static bool IsBoolTyped(Expression expr, SemanticInfo? semanticInfo) =>
        semanticInfo?.GetExpressionType(AstHelper.UnwrapParenthesized(expr)) is { } type
        && TypeUtils.IsBool(type);

    /// <summary>
    /// An operand a C# constant expression can be built from: a primitive- or string-typed
    /// expression, or the <c>None</c> literal (which prints as <c>null</c>).
    /// </summary>
    private static bool IsConstantOperand(Expression expr, SemanticInfo? semanticInfo)
    {
        var unwrapped = AstHelper.UnwrapParenthesized(expr);
        if (unwrapped is NoneLiteral)
            return true;
        return semanticInfo?.GetExpressionType(unwrapped) is { } type
            && (Registry.PrimitiveCatalog.GetPrimitiveInfo(type) != null || TypeUtils.IsString(type));
    }

    /// <summary>
    /// Gets the resolved type for a variable, falling back to the symbol's Type property.
    /// </summary>
    private SemanticType GetVariableType(VariableSymbol symbol)
    {
        var type = _semanticBinding.GetVariableType(symbol);
        return type is UnknownType ? symbol.Type : type;
    }
}
