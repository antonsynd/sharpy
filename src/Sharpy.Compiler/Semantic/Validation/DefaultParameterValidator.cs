using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates default parameter values in function definitions and <c>@dataclass</c> field defaults
/// (which become the synthesized constructor's parameter defaults):
/// - Early-bound defaults must be compile-time constant expressions
/// - Mutable defaults ([], {}, set()) are not allowed in early-bound position
/// - None is only allowed for nullable parameter types
/// - Late-bound defaults (=>) must not reference their own parameter (self-reference)
/// - Late-bound defaults (=>) must not reference parameters declared after them (forward-reference)
///
/// This is the pipeline-compatible version of DefaultParameterValidator.
/// </summary>
internal class DefaultParameterValidator : ValidatingAstWalker
{
    public override string Name => "DefaultParameterValidator";
    public override int Order => 250; // Before type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting default parameter validation");
        base.Validate(module, context);
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        ValidateFunctionDefaults(node);
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
        base.VisitClassDef(node);
    }

    /// <summary>
    /// A <c>@dataclass</c> field default IS a constructor-parameter default: DataclassSynthesis
    /// orders the fields into the synthesized <c>__init__</c>'s parameter vector and the emitter hands
    /// each field's initializer to the same GenerateParameterDefault the def/lambda/__init__ hosts
    /// use — so it is admitted by the same table. Unvisited, <c>x: int? = Some(1)</c>, <c>(1, 2)</c>,
    /// <c>[1]</c>, <c>g()</c> and <c>Ok(1)</c> reached Roslyn as parameter defaults and ICEd CS1736
    /// while their def twins were SPY0401/SPY0402 (#1762, #1769). An explicit <c>__init__</c>
    /// suppresses the synthesis (DataclassSynthesis.SynthesizeMembers), and with it this check: the
    /// initializer is then an ordinary property initializer, where any expression is legal.
    /// </summary>
    private void ValidateDataclassFieldDefaults(ClassDef classDef)
    {
        if (DataclassSynthesis.ReadOptions(classDef) == null)
            return;
        if (classDef.Body.OfType<FunctionDef>().Any(f => f.Name == DunderNames.Init))
            return;

        foreach (var field in classDef.Body.OfType<VariableDeclaration>())
        {
            // The membership rule of DataclassSynthesis.CollectFields: static and unannotated
            // fields are not dataclass fields, so they are not constructor parameters.
            if (field.InitialValue == null || field.Type == null
                || field.Decorators.Any(d => d.Name == DecoratorNames.Static))
                continue;

            ValidateDefaultValue(DataclassFieldSlot(field, classDef.Name), AdmissionTable.ParameterDefault);
        }
    }

    /// <summary>
    /// Validates all default parameter values in a function definition.
    /// </summary>
    private void ValidateFunctionDefaults(FunctionDef functionDef)
    {
        // Build set of all parameter names for forward-reference detection
        var allParamNames = new HashSet<string>(
            functionDef.Parameters.Select(p => p.Name),
            StringComparer.Ordinal);

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

    /// <summary>
    /// Validates a late-bound default expression for self-reference and forward-reference.
    /// </summary>
    private void ValidateLateBoundDefault(Parameter param, FunctionDef functionDef)
    {
        var referencedNames = CollectIdentifierNames(param.DefaultValue!);

        // Self-reference: the default expression references the parameter itself
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

        // Forward-reference: the default expression references a parameter declared after this one
        // Collect names of parameters that come AFTER this parameter
        bool foundSelf = false;
        foreach (var other in functionDef.Parameters)
        {
            if (!foundSelf)
            {
                if (other.Name == param.Name)
                    foundSelf = true;
                continue;
            }
            // other comes after param
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

    /// <summary>
    /// Collects all identifier names referenced anywhere in an expression (recursive).
    /// </summary>
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
    /// steer that quotes it. A <see cref="TypeAnnotation"/> is a record, so its <c>ToString()</c> is
    /// the record dump (<c>TypeAnnotation { LineStart = 1, … }</c>) — which is what users saw in the
    /// SPY0401 steer before this helper.
    /// </summary>
    private static string TypeSpelling(TypeAnnotation? type) =>
        type != null ? TypeAnnotationHelper.GetName(type) : "T?";

    /// <summary>
    /// Validates a single slot's default value against the host's admission table.
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

        // A reference resolves iff it names a const with the same backtick-escape spelling — the
        // rule TypeChecker.TryFoldConstantValue applies when it folds the const's own value.
        var kind = ConstantDefaultClassifier.Classify(defaultValue, id =>
            Context.SymbolTable.Lookup(id.Name) is VariableSymbol { IsConstant: true } constSymbol
            && constSymbol.IsNameBacktickEscaped == id.IsNameBacktickEscaped);

        if (!ConstantDefaultClassifier.IsAdmitted(kind, table))
        {
            var refusal = $"Default value for {slot.Subject} in {slot.Host} must be a compile-time constant expression";
            var steer = kind switch
            {
                EmittableConstantKind.CaseConstructor => $"{refusal}. {slot.CaseConstructorSteer}",
                EmittableConstantKind.TupleLiteral =>
                    $"{refusal}. Tuple literals are not emittable as parameter defaults; initialize in {slot.BodySteer} instead.",
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

            // None is only valid for nullable/optional types
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
    /// Checks if an expression is a mutable default value.
    /// Mutable defaults include: [], {}, set()
    /// </summary>
    private static bool IsMutableDefault(Expression expr)
    {
        return expr switch
        {
            // Empty list literal [] or list with elements [1, 2, 3]
            ListLiteral => true,

            // Empty dict literal {} (not to be confused with empty set)
            // DictLiteral is always mutable regardless of contents
            DictLiteral => true,

            // Set literal {1, 2, 3}
            SetLiteral => true,

            // Function call to set()/list()/dict() - collection constructors. Matched against the
            // canonical (paren-stripped) callee so `(list)()` is as mutable as `list()` (#1170).
            FunctionCall call when AstHelper.UnwrapParenthesized(call.Function)
                is Identifier { Name: BuiltinNames.Set or BuiltinNames.List or BuiltinNames.Dict } => true,

            // Parenthesized expression - check inner expression
            Parenthesized paren => IsMutableDefault(paren.Expression),

            _ => false
        };
    }

}
