using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Computes the compile-time constant fact for every <c>const</c> declaration in a module.
/// A const is compile-time iff (1) its declared type is one C# admits for <c>const</c> --
/// every <c>PrimitiveCatalog</c> primitive except <c>object</c> and <c>void</c>, plus
/// enum types; (2) the <c>ConstantDefaultClassifier</c> admits its initializer for
/// <c>AdmissionTable.ModuleConst</c>; and (3) for integer kinds only, the checker folded
/// a value (<c>VariableSymbol.ConstantValue</c>).
///
/// <para>This analysis runs once at the end of the module pass in
/// <see cref="TypeChecker.CheckModule"/>, BEFORE <c>_validationPipeline.Validate</c>. The
/// result is stored on <see cref="SemanticBinding.SetCompileTimeConstant"/> so
/// <c>CodeGenInfoComputer</c> copies it onto <c>CodeGenInfo.IsCompileTimeConstant</c> and the
/// emitter reads a frozen fact.</para>
/// </summary>
internal sealed class ConstEligibility
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticBinding _semanticBinding;
    private readonly SemanticInfo? _semanticInfo;

    // Module-level const declarations by name, and the memoized "emits as C# const" answer.
    private readonly Dictionary<string, VariableDeclaration> _moduleConstDecls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _moduleConstIsCompileTime = new(StringComparer.Ordinal);
    private readonly HashSet<string> _moduleConstsInProgress = new(StringComparer.Ordinal);

    public ConstEligibility(SymbolTable symbolTable, SemanticBinding semanticBinding, SemanticInfo? semanticInfo)
    {
        _symbolTable = symbolTable;
        _semanticBinding = semanticBinding;
        _semanticInfo = semanticInfo;
    }

    /// <summary>
    /// Analyze every <c>const</c> declaration in the module and store the compile-time fact
    /// on <see cref="SemanticBinding"/>. Called from <see cref="TypeChecker.CheckModule"/>
    /// before the validation pipeline runs.
    /// </summary>
    public void AnalyzeModule(Module module)
    {
        _moduleConstDecls.Clear();
        _moduleConstIsCompileTime.Clear();
        _moduleConstsInProgress.Clear();

        // Build the declaration index first so forward references resolve.
        foreach (var stmt in module.Body)
        {
            if (stmt.UnwrapDecorated() is VariableDeclaration { IsConst: true } constDecl)
                _moduleConstDecls[constDecl.Name] = constDecl;
        }

        // Compute and store the fact for every module-level const.
        foreach (var (name, _) in _moduleConstDecls)
        {
            var isCompileTime = IsModuleConstCompileTime(name);
            if (_symbolTable.Lookup(name) is VariableSymbol varSymbol)
            {
                _semanticBinding.SetCompileTimeConstant(varSymbol, isCompileTime);
            }
        }

        // Walk into type bodies (class/struct) to cover field consts.
        // Local consts are handled by LocalNameAllocator using a type-only check (#1791).
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    AnalyzeTypeBody(classDef.Name, classDef.Body);
                    break;
                case StructDef structDef:
                    AnalyzeTypeBody(structDef.Name, structDef.Body);
                    break;
            }
        }
    }

    /// <summary>
    /// Analyze const fields inside a class or struct body.
    /// </summary>
    private void AnalyzeTypeBody(string typeName, IEnumerable<Statement> body)
    {
        var typeSymbol = _symbolTable.Lookup(typeName) as TypeSymbol;
        if (typeSymbol == null)
            return;

        foreach (var stmt in body)
        {
            if (stmt is VariableDeclaration { IsConst: true } fieldDecl)
            {
                var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
                if (fieldSymbol != null)
                {
                    var isCompileTime = ComputeFieldConstIsCompileTime(fieldSymbol, fieldDecl);
                    _semanticBinding.SetCompileTimeConstant(fieldSymbol, isCompileTime);
                }
            }
        }
    }

    /// <summary>
    /// Compute whether a field const is compile-time. Uses the same rules as module consts
    /// but operates on the symbol directly rather than through the module-const declaration index.
    /// </summary>
    private bool ComputeFieldConstIsCompileTime(VariableSymbol varSymbol, VariableDeclaration decl)
    {
        if (decl.InitialValue == null)
            return false;

        var type = GetVariableType(varSymbol);
        if (!IsConstEligibleSemanticType(type))
            return false;

        // Use the classifier with the module-const hooks so a field const referencing a
        // module-level const resolves correctly.
        var kind = Validation.ConstantDefaultClassifier.Classify(
            decl.InitialValue, ResolvesToCompileTimeConst, LowersToConstantExpression);
        if (!Validation.ConstantDefaultClassifier.IsAdmitted(kind, Validation.AdmissionTable.ModuleConst))
            return false;

        // Integer kinds require a folded value.
        var primitiveInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(type);
        if (primitiveInfo != null)
        {
            var isInteger = primitiveInfo.Kind is Registry.PrimitiveCatalog.NumericKind.SignedInteger
                or Registry.PrimitiveCatalog.NumericKind.UnsignedInteger;
            return !isInteger || varSymbol.ConstantValue != null;
        }

        return true;
    }

    /// <summary>
    /// Whether the module-level <c>const</c> named <paramref name="name"/> emits as a C#
    /// <c>const</c> -- a compile-time constant every constant-position consumer can read.
    ///
    /// <para><b>Contract.</b> A const is compile-time iff (1) its declared type is
    /// const-eligible; (2) the classifier admits its initializer for
    /// <c>AdmissionTable.ModuleConst</c>; and (3) for integer kinds only, the checker folded a
    /// value. Forward references resolve through the declaration index, not processing order,
    /// because C# resolves const dependency order itself; a reference cycle answers false for
    /// every member.</para>
    /// </summary>
    internal bool IsModuleConstCompileTime(string name)
    {
        if (_moduleConstIsCompileTime.TryGetValue(name, out var known))
            return known;
        if (!_moduleConstDecls.TryGetValue(name, out var decl))
            return false;
        if (!_moduleConstsInProgress.Add(name))
            return false; // cycle: no member of it can fold

        var result = _symbolTable.Lookup(name) is VariableSymbol varSymbol
            && ComputeModuleConstIsCompileTime(varSymbol, decl);

        _moduleConstsInProgress.Remove(name);
        _moduleConstIsCompileTime[name] = result;
        return result;
    }

    private bool ComputeModuleConstIsCompileTime(VariableSymbol varSymbol, VariableDeclaration decl)
    {
        if (decl.InitialValue == null)
            return false;

        var type = GetVariableType(varSymbol);
        if (!IsConstEligibleSemanticType(type))
            return false;

        var kind = Validation.ConstantDefaultClassifier.Classify(
            decl.InitialValue, ResolvesToCompileTimeConst, LowersToConstantExpression);
        if (!Validation.ConstantDefaultClassifier.IsAdmitted(kind, Validation.AdmissionTable.ModuleConst))
            return false;

        // Integer kinds require a folded value (range/overflow gate #1460).
        var primitiveInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(type);
        if (primitiveInfo != null)
        {
            var isInteger = primitiveInfo.Kind is Registry.PrimitiveCatalog.NumericKind.SignedInteger
                or Registry.PrimitiveCatalog.NumericKind.UnsignedInteger;
            return !isInteger || varSymbol.ConstantValue != null;
        }

        // Enum types: the initializer shape (EnumMember) is admitted by the table, and the type
        // is const-eligible -- no further gate needed.
        return true;
    }

    /// <summary>
    /// Whether the declared (or inferred) type of <paramref name="symbol"/> is one C# admits for
    /// <c>const</c>. Every <c>PrimitiveCatalog</c> primitive except <c>object</c> and <c>void</c>,
    /// plus enum types (C# admits <c>const Color C = Color.RED</c>).
    /// </summary>
    internal bool IsConstEligibleType(VariableSymbol symbol)
    {
        return IsConstEligibleSemanticType(GetVariableType(symbol));
    }

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

    /// <summary>
    /// The classifier's identifier hook: the name resolves iff it is a <c>const</c> symbol that
    /// emits as a C# const -- a const declared in this module whose own initializer is compile-time,
    /// or an IMPORTED const carrying a folded <c>ConstantValue</c>.
    /// </summary>
    private bool ResolvesToCompileTimeConst(Identifier id)
    {
        var sym = _symbolTable.Lookup(id.Name);
        if (sym is not VariableSymbol { IsConstant: true } constSymbol
            || sym.IsNameBacktickEscaped != id.IsNameBacktickEscaped)
            return false;
        if (_moduleConstDecls.ContainsKey(id.Name))
            return IsModuleConstCompileTime(id.Name);
        return constSymbol.ConstantValue != null;
    }

    /// <summary>
    /// The classifier's operator hook: whether the checker's recorded lowering for one operator
    /// node is a C# constant operator. Reads only recorded facts.
    /// </summary>
    private bool LowersToConstantExpression(Expression node)
    {
        if (_semanticInfo == null)
            return false;

        if (node is BinaryOp binary)
        {
            if (binary.Operator is not (BinaryOperator.Add or BinaryOperator.Subtract
                or BinaryOperator.Multiply or BinaryOperator.Divide
                or BinaryOperator.Equal or BinaryOperator.NotEqual
                or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                or BinaryOperator.And or BinaryOperator.Or
                or BinaryOperator.BitwiseAnd or BinaryOperator.BitwiseOr or BinaryOperator.BitwiseXor
                or BinaryOperator.LeftShift or BinaryOperator.RightShift))
                return false;

            if (_semanticInfo.GetBinaryOpLoweringForIr(binary) != BinaryOpLowering.NativeOperator)
                return false;

            if (!IsPrimitiveTyped(binary.Left) || !IsPrimitiveTyped(binary.Right))
                return false;

            if (binary.Operator is BinaryOperator.And or BinaryOperator.Or
                && !(IsBoolTyped(binary.Left) && IsBoolTyped(binary.Right)))
                return false;
        }
        else if (node is UnaryOp { Operator: UnaryOperator.Not } logicalNot)
        {
            if (!IsBoolTyped(logicalNot.Operand))
                return false;
        }
        else if (node is ConditionalExpression cond)
        {
            if (!IsBoolTyped(cond.Test))
                return false;
        }

        var lowering = _semanticInfo.GetOperatorLowering(node);
        return lowering == null || lowering.Kind is OperatorLoweringKind.Native
            or OperatorLoweringKind.TrueDivisionCastLeft
            or OperatorLoweringKind.ShiftCountCastToInt
            or OperatorLoweringKind.NegateLiteralInt
            or OperatorLoweringKind.NegateLiteralLong;
    }

    private bool IsBoolTyped(Expression expr) =>
        _semanticInfo?.GetExpressionType(AstHelper.UnwrapParenthesized(expr)) is { } type
        && TypeUtils.IsBool(type);

    private bool IsPrimitiveTyped(Expression expr) =>
        _semanticInfo?.GetExpressionType(AstHelper.UnwrapParenthesized(expr)) is { } type
        && (Registry.PrimitiveCatalog.GetPrimitiveInfo(type) != null || TypeUtils.IsString(type));

    /// <summary>
    /// Gets the resolved type for a variable, falling back to the symbol's Type property.
    /// </summary>
    private SemanticType GetVariableType(VariableSymbol symbol)
    {
        var type = _semanticBinding.GetVariableType(symbol);
        return type is UnknownType ? symbol.Type : type;
    }
}
