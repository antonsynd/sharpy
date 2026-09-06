using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// One validator for every constant position — def/lambda/__init__/dataclass parameter defaults,
/// bracket-attribute arguments, and match-case constant patterns — replacing the former
/// <c>DefaultParameterValidator</c> and absorbing <c>DecoratorValidator.ValidateDecoratorArgumentsAreConstants</c>.
///
/// <para>The classifier reads the SHAPE of the expression; two facts a shape alone cannot carry are
/// supplied by the caller:
/// <list type="bullet">
/// <item><c>constResolver</c> — whether an <see cref="Identifier"/> names a compile-time constant.
/// Uses <see cref="SemanticInfo.GetIdentifierSymbol"/> (scope-correct, set during type checking) rather
/// than <c>SymbolTable.Lookup</c> (scope-blind).</item>
/// <item><c>operatorLowersToConstant</c> — whether an operator node lowers to a C# constant operator.
/// Reuses <see cref="ConstEligibility.LowersToConstantExpression(Expression, SemanticInfo)"/>.</item>
/// </list>
/// </para>
/// </summary>
internal class ConstantPositionValidator : ValidatingAstWalker
{
    public override string Name => "ConstantPositionValidator";
    public override int Order => 250; // Keeping the deleted DefaultParameterValidator's slot

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting constant-position validation");

        base.Validate(module, context);
    }

    // ── Parameter defaults ──────────────────────────────────────────────

    public override void VisitFunctionDef(FunctionDef node)
    {
        ValidateFunctionDefaults(node);
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitFunctionDef(node);
    }

    public override void VisitLambdaExpression(LambdaExpression node)
    {
        ValidateLambdaDefaults(node);
        base.VisitLambdaExpression(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        ValidateDataclassFieldDefaults(node);
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitClassDef(node);
    }

    public override void VisitStructDef(StructDef node)
    {
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitStructDef(node);
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitInterfaceDef(node);
    }

    public override void VisitEnumDef(EnumDef node)
    {
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitEnumDef(node);
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitEventDef(node);
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        ValidateBracketAttributeArguments(node.Decorators);
        base.VisitVariableDeclaration(node);
    }

    // ── Match-case constant patterns ────────────────────────────────────

    public override void VisitBindingPattern(BindingPattern node)
    {
        ValidateConstantPattern(node);
        base.VisitBindingPattern(node);
    }

    // ── DefaultSlot machinery (from DefaultParameterValidator) ──────────

    /// <summary>
    /// One constant-position slot — a def/lambda parameter or a <c>@dataclass</c> field. The hosts
    /// share every rule; they differ only in how a diagnostic names the slot (<see cref="Subject"/>
    /// in <see cref="Host"/>), the noun the None steer uses, and where the steer says to initialize
    /// instead (<see cref="BodySteer"/>).
    /// </summary>
    private sealed record DefaultSlot(
        string Name,
        TypeAnnotation? Type,
        Expression DefaultValue,
        int LineStart,
        int ColumnStart,
        TextSpan? Span,
        string Subject,
        string Host,
        string Noun,
        string BodySteer,
        string CaseConstructorSteer);

    private static DefaultSlot ParameterSlot(Parameter param, string functionName) => new(
        param.Name,
        param.Type,
        param.DefaultValue!,
        param.LineStart,
        param.ColumnStart,
        param.Span,
        Subject: $"parameter '{param.Name}'",
        Host: $"function '{functionName}'",
        Noun: "parameter",
        BodySteer: "the function body",
        CaseConstructorSteer:
            $"Use 'def {functionName}({param.Name}: {TypeSpelling(param.Type)} = None()) -> ...: {param.Name} ??= Some(...)' instead.");

    private static DefaultSlot DataclassFieldSlot(VariableDeclaration field, string className) => new(
        field.Name,
        field.Type,
        field.InitialValue!,
        field.LineStart,
        field.ColumnStart,
        field.Span,
        Subject: $"field '{field.Name}'",
        Host: $"dataclass '{className}'",
        Noun: "field",
        BodySteer: "__post_init__",
        CaseConstructorSteer:
            $"Use '{field.Name}: {TypeSpelling(field.Type)} = None()' and assign 'self.{field.Name} ??= Some(...)' in __post_init__ instead.");

    /// <summary>
    /// The annotation's SOURCE spelling (<c>int?</c>, <c>list[int]</c>, <c>str | None</c>) for a
    /// steer that quotes it.
    /// </summary>
    private static string TypeSpelling(TypeAnnotation? type) =>
        type != null ? TypeAnnotationHelper.GetName(type) : "T?";

    // ── Core validation ─────────────────────────────────────────────────

    /// <summary>
    /// A <c>@dataclass</c> field default IS a constructor-parameter default: DataclassSynthesis
    /// orders the fields into the synthesized <c>__init__</c>'s parameter vector.
    /// </summary>
    private void ValidateDataclassFieldDefaults(ClassDef classDef)
    {
        if (DataclassSynthesis.ReadOptions(classDef) == null)
            return;
        if (classDef.Body.OfType<FunctionDef>().Any(f => f.Name == DunderNames.Init))
            return;

        foreach (var field in classDef.Body.OfType<VariableDeclaration>())
        {
            if (field.InitialValue == null || field.Type == null
                || field.Decorators.Any(d => d.Name == DecoratorNames.Static))
                continue;

            ValidateDefaultValue(DataclassFieldSlot(field, classDef.Name), AdmissionTable.ParameterDefault);
        }
    }

    private void ValidateFunctionDefaults(FunctionDef functionDef)
    {
        foreach (var param in functionDef.Parameters)
        {
            if (param.DefaultValue == null)
                continue;

            if (param.IsLateBound)
            {
                ValidateLateBoundDefault(param, functionDef);
            }
            else
            {
                ValidateDefaultValue(ParameterSlot(param, functionDef.Name), AdmissionTable.ParameterDefault);
            }
        }
    }

    private void ValidateLambdaDefaults(LambdaExpression lambda)
    {
        foreach (var param in lambda.Parameters)
        {
            if (param.DefaultValue == null || param.IsLateBound)
                continue;

            ValidateDefaultValue(ParameterSlot(param, "lambda"), AdmissionTable.LambdaParameterDefault);
        }
    }

    private void ValidateLateBoundDefault(Parameter param, FunctionDef functionDef)
    {
        var referencedNames = CollectIdentifierNames(param.DefaultValue!);

        if (referencedNames.Contains(param.Name))
        {
            AddError(
                $"Late-bound default for parameter '{param.Name}' in function '{functionDef.Name}' cannot reference itself.",
                param.LineStart,
                param.ColumnStart,
                code: DiagnosticCodes.Validation.LateBoundSelfReference,
                span: param.Span);
            return;
        }

        bool foundSelf = false;
        foreach (var other in functionDef.Parameters)
        {
            if (!foundSelf)
            {
                if (other.Name == param.Name)
                    foundSelf = true;
                continue;
            }
            if (referencedNames.Contains(other.Name))
            {
                AddError(
                    $"Late-bound default for parameter '{param.Name}' in function '{functionDef.Name}' cannot reference later parameter '{other.Name}'.",
                    param.LineStart,
                    param.ColumnStart,
                    code: DiagnosticCodes.Validation.LateBoundForwardReference,
                    span: param.Span);
                return;
            }
        }
    }

    private static HashSet<string> CollectIdentifierNames(Expression expr)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        CollectIdentifierNamesInto(expr, names);
        return names;
    }

    private static void CollectIdentifierNamesInto(Expression expr, HashSet<string> names)
    {
        switch (expr)
        {
            case Identifier id:
                names.Add(id.Name);
                break;
            case BinaryOp bin:
                CollectIdentifierNamesInto(bin.Left, names);
                CollectIdentifierNamesInto(bin.Right, names);
                break;
            case UnaryOp unary:
                CollectIdentifierNamesInto(unary.Operand, names);
                break;
            case Parenthesized paren:
                CollectIdentifierNamesInto(paren.Expression, names);
                break;
            case ConditionalExpression cond:
                CollectIdentifierNamesInto(cond.Test, names);
                CollectIdentifierNamesInto(cond.ThenValue, names);
                CollectIdentifierNamesInto(cond.ElseValue, names);
                break;
            case FunctionCall call:
                CollectIdentifierNamesInto(call.Function, names);
                foreach (var arg in call.Arguments)
                    CollectIdentifierNamesInto(arg, names);
                foreach (var kwarg in call.KeywordArguments)
                    CollectIdentifierNamesInto(kwarg.Value, names);
                break;
            case MemberAccess memberAccess:
                CollectIdentifierNamesInto(memberAccess.Object, names);
                break;
            case IndexAccess indexAccess:
                CollectIdentifierNamesInto(indexAccess.Object, names);
                CollectIdentifierNamesInto(indexAccess.Index, names);
                break;
            case TupleLiteral tuple:
                foreach (var elem in tuple.Elements)
                    CollectIdentifierNamesInto(elem, names);
                break;
            case ListLiteral list:
                foreach (var elem in list.Elements)
                    CollectIdentifierNamesInto(elem, names);
                break;
            default:
                // walker-default-contract: literals and other leaf nodes contribute no
                // identifiers — any kind not listed above is deliberately ignored by this walker
                // (rostered in DispatchSiteInventoryTests).
                break;
        }
    }

    /// <summary>
    /// Validates a single slot's default value against the host's admission table.
    /// Uses the scope-correct <see cref="SemanticInfo.GetIdentifierSymbol"/> binding and
    /// the <see cref="ConstEligibility.LowersToConstantExpression(Expression, SemanticInfo)"/>
    /// operator hook.
    /// </summary>
    private void ValidateDefaultValue(DefaultSlot slot, AdmissionTable table)
    {
        var defaultValue = slot.DefaultValue;

        // Check for mutable defaults first (these are never allowed)
        if (IsMutableDefault(defaultValue))
        {
            AddError(
                $"Mutable default value is not allowed for {slot.Subject} in {slot.Host}. " +
                $"Use None as default and initialize in {slot.BodySteer} instead.",
                slot.LineStart,
                slot.ColumnStart, code: DiagnosticCodes.Validation.MutableDefault,
                span: slot.Span);
            return;
        }

        var semanticInfo = Context.SemanticInfo;
        var kind = ConstantDefaultClassifier.Classify(
            defaultValue,
            constResolver: id => IsConstReferenceAdmissible(id, semanticInfo),
            operatorLowersToConstant: node =>
                ConstEligibility.LowersToConstantExpression(node, semanticInfo),
            memberConstResolver: member => IsMemberConstAdmissible(member, semanticInfo));

        if (!ConstantDefaultClassifier.IsAdmitted(kind, table))
        {
            var reason = DescribeRefusalReason(kind, defaultValue, semanticInfo, slot.Noun);
            var refusal = $"Default value for {slot.Subject} in {slot.Host} must be a compile-time constant expression";
            var steer = kind switch
            {
                EmittableConstantKind.CaseConstructor when IsResultConstructor(slot.DefaultValue) =>
                    $"{refusal}. A Result is not a compile-time constant: make the {slot.Noun} required and pass Ok(...)/Err(...) at the call site.",
                EmittableConstantKind.CaseConstructor => $"{refusal}. {slot.CaseConstructorSteer}",
                EmittableConstantKind.TupleLiteral =>
                    $"{refusal}. Tuple literals are not emittable as parameter defaults; initialize in {slot.BodySteer} instead.",
                _ when reason != null => $"{refusal} ({reason})",
                _ => refusal,
            };

            AddError(
                steer,
                slot.LineStart,
                slot.ColumnStart, code: DiagnosticCodes.Validation.NonConstDefault,
                span: slot.Span);
            return;
        }

        // Check None assignment to non-nullable types
        if (defaultValue is NoneLiteral)
        {
            var slotType = Context.TypeResolver.ResolveTypeAnnotation(slot.Type);

            if (slotType is not NullableType and not OptionalType && slotType is not UnknownType)
            {
                AddError(
                    $"Cannot use 'None' as default value for non-nullable {slot.Subject} of type '{slotType.GetDisplayName()}' in {slot.Host}. " +
                    $"Use '{slotType.GetDisplayName()}?' to make the {slot.Noun} nullable.",
                    slot.LineStart,
                    slot.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDefaultValue,
                    span: slot.Span);
            }
        }

        // Check None() assignment to non-optional types
        if (defaultValue is FunctionCall { Function: NoneLiteral } noneCall
            && noneCall.Arguments.Length == 0 && noneCall.KeywordArguments.Length == 0)
        {
            var slotType = Context.TypeResolver.ResolveTypeAnnotation(slot.Type);

            if (slotType is not OptionalType && slotType is not UnknownType)
            {
                AddError(
                    $"Cannot use 'None()' as default value for non-optional {slot.Subject} of type '{slotType.GetDisplayName()}' in {slot.Host}. " +
                    $"Use '{slotType.GetDisplayName()}?' to make the {slot.Noun} optional.",
                    slot.LineStart,
                    slot.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDefaultValue,
                    span: slot.Span);
            }
        }
    }

    /// <summary>
    /// The constResolver, for every constant position: an <see cref="Identifier"/> resolves iff the
    /// symbol the CHECKER bound to it is a <c>const</c> with the same backtick-escape spelling and
    /// that const's compile-time fact is true.
    ///
    /// <para>The binding comes from <see cref="SemanticInfo.GetIdentifierSymbol"/>, which is
    /// scope-correct — a local const in an enclosing function, or a class const read from a method's
    /// signature, is the symbol the checker actually bound. <c>SymbolTable.Lookup</c> answers at
    /// MODULE scope and cannot see either; it survives only as the fallback for an identifier the
    /// checker never visited (a decorator argument on a declaration it skipped).</para>
    ///
    /// <para>The fact itself is <see cref="ConstEligibility"/>'s, for every host and every route —
    /// this validator computes no eligibility of its own (#1791).</para>
    /// </summary>
    private bool IsConstReferenceAdmissible(Identifier id, SemanticInfo semanticInfo)
    {
        var sym = ResolveConstSymbol(id, semanticInfo);
        return sym != null && ConstEligibility.IsCompileTimeConstant(Context.SemanticBinding, sym);
    }

    /// <summary>
    /// The qualified-name twin of <see cref="IsConstReferenceAdmissible"/>: <c>Holder.A</c> resolves
    /// iff the member the checker bound is a <c>const</c> whose fact is true. Null when the chain
    /// names something else (an enum member, a CLR static), where the shape rule stands.
    /// </summary>
    private bool? IsMemberConstAdmissible(MemberAccess member, SemanticInfo semanticInfo)
        => ConstEligibility.ResolvesToCompileTimeMember(member, semanticInfo, Context.SemanticBinding);

    /// <summary>
    /// The <c>const</c> symbol an identifier in a constant position denotes, or null.
    /// </summary>
    private VariableSymbol? ResolveConstSymbol(Identifier id, SemanticInfo semanticInfo)
    {
        if (semanticInfo.GetIdentifierSymbol(id) is VariableSymbol { IsConstant: true } bound
            && bound.IsNameBacktickEscaped == id.IsNameBacktickEscaped)
        {
            return bound;
        }

        return Context.SymbolTable.Lookup(id.Name) is VariableSymbol { IsConstant: true } fallback
            && fallback.IsNameBacktickEscaped == id.IsNameBacktickEscaped
                ? fallback
                : null;
    }

    /// <summary>
    /// Produces a human-readable reason for why a constant position was refused, naming the specific
    /// non-constant element. Returns null when no specific reason can be given.
    ///
    /// <para>An identifier's reason comes from the CAUSE <see cref="ConstEligibility"/> recorded with
    /// the fact, so the refusal and the analysis cannot tell different stories.</para>
    /// </summary>
    private string? DescribeRefusalReason(
        EmittableConstantKind kind, Expression defaultValue, SemanticInfo semanticInfo, string? noun = null)
    {
        if (kind != EmittableConstantKind.Other)
            return null;

        if (defaultValue is BinaryOp binary)
        {
            var operatorName = binary.Operator switch
            {
                BinaryOperator.FloorDivide =>
                    "'//' lowers to FloorDiv which is not a C# constant operator",
                BinaryOperator.Modulo =>
                    "'%' lowers to FloorMod which is not a C# constant operator",
                BinaryOperator.Power =>
                    "'**' lowers to Math.Pow which is not a C# constant operator",
                BinaryOperator.Multiply when IsStringTimesInt(binary, semanticInfo) =>
                    "'*' on str lowers to string.Repeat which is not a C# constant operator",
                BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                    or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual
                    when IsStringOperand(binary, semanticInfo) =>
                    "ordering str operands lowers to string.CompareOrdinal which is not a C# constant operator",
                BinaryOperator.In or BinaryOperator.NotIn =>
                    "'in' lowers to a containment call which is not a C# constant operator",
                BinaryOperator.MatMul =>
                    "'@' lowers to a call which is not a C# constant operator",
                _ => null,
            };
            if (operatorName != null)
                return operatorName;
        }

        if (defaultValue is Identifier refId)
        {
            var refSym = ResolveConstSymbol(refId, semanticInfo);
            if (refSym != null)
                return DescribeConstReason(refSym, noun);
        }

        if (defaultValue is MemberAccess member)
        {
            if (ConstEligibility.MemberConstSymbol(member, semanticInfo) is { } memberSym)
                return DescribeConstReason(memberSym, noun);

            if (ConstEligibility.ResolvesToCompileTimeMember(
                    member, semanticInfo, Context.SemanticBinding) == false)
            {
                return $"'{member.Member}' is a field, not a const, so it is not a compile-time constant";
            }
        }

        return null;
    }

    /// <summary>
    /// Why the const <paramref name="symbol"/> is not compile-time, in the reader's words. Reads the
    /// cause <see cref="ConstEligibility"/> recorded with the fact — no second analysis.
    /// </summary>
    private string? DescribeConstReason(VariableSymbol symbol, string? noun = null)
    {
        var name = symbol.Name;
        var cause = Context.SemanticBinding.GetConstIneligibilityCause(symbol);
        var type = Context.SemanticBinding.GetVariableType(symbol);
        if (type is UnknownType)
            type = symbol.Type;
        var spelling = Context.SemanticBinding.GetConstDeclaredSpelling(symbol) ?? type.GetDisplayName();
        var slot = noun ?? "variable";

        return cause switch
        {
            ConstIneligibilityCause.OptionalType =>
                $"'{name}' is not a compile-time constant: an Optional ('{spelling}') cannot be a "
                + $"compile-time constant; declare the {slot} '{spelling} = None()' and coalesce in the body",
            ConstIneligibilityCause.NullableType =>
                $"'{name}' is not a compile-time constant: a nullable value type ('{spelling}') "
                + "cannot be a compile-time constant in C#",
            ConstIneligibilityCause.IneligibleType =>
                $"'{name}' is not a compile-time constant: C# admits no const of type '{spelling}'",
            ConstIneligibilityCause.CallInitializer =>
                $"'{name}' is not a compile-time constant: its initializer is a call",
            ConstIneligibilityCause.CallLoweredOperator =>
                $"'{name}' is not a compile-time constant: its initializer uses an operator that "
                + "lowers to a call",
            ConstIneligibilityCause.NonConstantReference =>
                $"'{name}' is not a compile-time constant: it reads another const that is not one",
            ConstIneligibilityCause.UnfoldedInteger =>
                $"'{name}' is not a compile-time constant: its integer initializer does not fold to a value",
            _ => $"'{name}' is not a compile-time constant",
        };
    }

    private static bool IsStringOperand(BinaryOp binary, SemanticInfo semanticInfo)
    {
        var leftType = semanticInfo.GetExpressionType(AstHelper.UnwrapParenthesized(binary.Left));
        return leftType != null && TypeUtils.IsString(leftType);
    }

    private static bool IsStringTimesInt(BinaryOp binary, SemanticInfo semanticInfo)
    {
        var leftType = semanticInfo.GetExpressionType(AstHelper.UnwrapParenthesized(binary.Left));
        return leftType != null && TypeUtils.IsString(leftType);
    }

    // ── Bracket-attribute argument validation (from DecoratorValidator) ──

    /// <summary>
    /// Validates bracket-attribute arguments are compile-time constants. Extracted from
    /// <c>DecoratorValidator.ValidateDecoratorArgumentsAreConstants</c>. Source-generator
    /// bracket attributes are exempt (their arguments are runtime values).
    /// </summary>
    private void ValidateBracketAttributeArguments(IEnumerable<Decorator> decorators)
    {
        foreach (var decorator in decorators)
        {
            if (!decorator.IsBracketAttribute)
                continue;

            var symbol = Context.SymbolTable.LookupType(decorator.Name);
            if (symbol is { IsSourceGenerator: true })
                continue;

            ValidateDecoratorConstantArguments(decorator);
        }
    }

    private void ValidateDecoratorConstantArguments(Decorator decorator)
    {
        var semanticInfo = Context.SemanticInfo;

        foreach (var arg in decorator.Arguments)
        {
            var kind = ConstantDefaultClassifier.Classify(
                arg,
                constResolver: id => IsConstReferenceAdmissible(id, semanticInfo),
                operatorLowersToConstant: node =>
                    ConstEligibility.LowersToConstantExpression(node, semanticInfo),
                memberConstResolver: member => IsMemberConstAdmissible(member, semanticInfo));

            if (!ConstantDefaultClassifier.IsAdmitted(kind, AdmissionTable.DecoratorArgument))
            {
                var message = DescribeDecoratorRefusal(arg, kind, semanticInfo);
                AddError(
                    message,
                    arg.LineStart,
                    arg.ColumnStart,
                    code: DiagnosticCodes.Validation.NonConstantDecoratorArgument,
                    span: arg.Span);
            }
        }

        foreach (var kwArg in decorator.KeywordArguments)
        {
            var kind = ConstantDefaultClassifier.Classify(
                kwArg.Value,
                constResolver: id => IsConstReferenceAdmissible(id, semanticInfo),
                operatorLowersToConstant: node =>
                    ConstEligibility.LowersToConstantExpression(node, semanticInfo),
                memberConstResolver: member => IsMemberConstAdmissible(member, semanticInfo));

            if (!ConstantDefaultClassifier.IsAdmitted(kind, AdmissionTable.DecoratorArgument))
            {
                var message = DescribeDecoratorRefusal(kwArg.Value, kind, semanticInfo);
                AddError(
                    message,
                    kwArg.Value.LineStart,
                    kwArg.Value.ColumnStart,
                    code: DiagnosticCodes.Validation.NonConstantDecoratorArgument,
                    span: kwArg.Value.Span);
            }
        }
    }

    /// <summary>
    /// The refusal text for one bracket-attribute argument, naming the constancy reason the analysis
    /// recorded (a C# attribute argument is a constant expression, so the reasons are the same ones
    /// a parameter default reports).
    /// </summary>
    private string DescribeDecoratorRefusal(
        Expression arg, EmittableConstantKind kind, SemanticInfo semanticInfo)
    {
        if (arg is Identifier id)
        {
            var sym = ResolveConstSymbol(id, semanticInfo);
            if (sym == null)
                return $"Variable reference '{id.Name}' is not a compile-time constant; use a literal or enum member access";

            // A const that IS compile-time still cannot be printed here: code generation has no arm
            // for a name in an attribute argument (#1801). Say that rather than claiming the const
            // is not constant, which the analysis would flatly contradict.
            return ConstEligibility.IsCompileTimeConstant(Context.SemanticBinding, sym)
                ? $"'{id.Name}' is a compile-time constant, but a const reference is not yet "
                  + "supported in an attribute argument; write the literal value"
                : DescribeConstReason(sym, "argument")!;
        }

        var reason = DescribeRefusalReason(kind, arg, semanticInfo, "argument");
        return reason == null
            ? "Decorator argument must be a compile-time constant"
            : $"Decorator argument must be a compile-time constant ({reason})";
    }

    // ── Match-case constant pattern validation ──────────────────────────

    /// <summary>
    /// A <see cref="BindingPattern"/> the checker resolved to a const via
    /// <see cref="SemanticInfo.SetPatternConstantSymbol"/> is emitted as a C# constant pattern.
    /// If the const is not compile-time (<c>static readonly</c>), Roslyn refuses it with CS9135.
    /// Emit SPY0605 with a guard steer instead.
    /// </summary>
    private void ValidateConstantPattern(BindingPattern node)
    {
        var constSym = Context.SemanticInfo.GetPatternConstantSymbol(node);
        if (constSym == null)
            return; // not a constant pattern — it's a capture binding

        // The ONE fact, same as every other constant position (#1791).
        if (ConstEligibility.IsCompileTimeConstant(Context.SemanticBinding, constSym))
            return;

        var reason = DescribeConstReason(constSym);
        var because = reason == null ? "" : $" ({reason})";

        {
            AddError(
                $"Constant pattern '{node.Name.Name}' is not a compile-time constant{because}; " +
                $"compare in a guard: 'case _ if v == {node.Name.Name}:'",
                node.LineStart,
                node.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.ConstantPatternNotCompileTime,
                span: node.Span);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static bool IsMutableDefault(Expression expr)
    {
        return expr switch
        {
            ListLiteral => true,
            DictLiteral => true,
            SetLiteral => true,
            FunctionCall call when AstHelper.UnwrapParenthesized(call.Function)
                is Identifier { Name: BuiltinNames.Set or BuiltinNames.List or BuiltinNames.Dict } => true,
            Parenthesized paren => IsMutableDefault(paren.Expression),
            _ => false
        };
    }

    /// <summary><c>Ok(...)</c> / <c>Err(...)</c>.</summary>
    private static bool IsResultConstructor(Expression defaultValue) =>
        defaultValue is FunctionCall { Function: Identifier { Name: "Ok" or "Err" } };
}
