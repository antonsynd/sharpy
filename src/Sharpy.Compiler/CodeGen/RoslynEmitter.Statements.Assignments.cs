using System.Collections.Immutable;
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
/// RoslynEmitter partial class: Assignment statements, variable declarations, and unpacking
/// </summary>
internal partial class RoslynEmitter
{
    private StatementSyntax GenerateAssignment(Assignment assign)
    {
        // A ConstructorReferenceType value here is unreachable, and this is the tripwire that says
        // so. Two tiers remain since #1248 retired the call-only alias: tier 1 replaces the carrier
        // with the pinned FunctionType, and everything else is refused in semantic analysis
        // (SPY0342, or SPY0346 for a type with no construction at all). NO BINDING CARRIES THE
        // CARRIER, so reaching here means the checker bound one where it should have refused. Fail
        // loudly rather than letting the generic identifier fallback emit a method group into a
        // non-delegate C# type (#1182, #1248).
        if (_context.SemanticInfo?.GetExpressionType(assign.Value) is ConstructorReferenceType crt)
        {
            _context.AddError(
                $"Internal: constructor reference to '{crt.Name}' reached code generation in a non-elidable binding. This is a compiler bug — please report it.",
                DiagnosticCodes.CodeGen.EmitError,
                assign.LineStart,
                assign.ColumnStart);
            return EmptyStatement();
        }

        // Check if this is an assignment of a lambda with default parameters to a simple
        // identifier (first declaration). Emit as a local function instead of a delegate
        // variable, because C# delegates / Func<> don't support optional parameters.
        if (assign.Operator == AssignmentOperator.Assign
            && assign.Target is Identifier lambdaTargetId
            && assign.Value is LambdaExpression lambdaWithDefaults
            && HasDefaultParameters(lambdaWithDefaults))
        {
            var baseName = LocalBaseName(lambdaTargetId.Name, lambdaTargetId.IsNameBacktickEscaped);
            var symbol = _context.LookupSymbol(lambdaTargetId.Name);
            var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
            var existsAsLocal = _context.SemanticInfo?.GetTargetBinding(lambdaTargetId)?.Kind == TargetBindingKind.Rebinds;

            if (!existsAsModuleLevel && !existsAsLocal)
            {
                // First declaration — emit as local function
                var localFuncName = GetMangledVariableName(lambdaTargetId, isNewDeclaration: true);
                return GenerateLambdaAsLocalFunction(lambdaWithDefaults, localFuncName);
            }
        }

        var value = GenerateExpression(assign.Value);

        // Handle simple identifier assignment
        if (assign.Target is Identifier name)
        {
            // Assigning to the accessor's named incoming value REBINDS that value the way a Python
            // parameter rebinds — it does not declare a new local. The read side maps the name onto
            // the C# slot that carries it (`value` for a setter/event accessor, the captured
            // old-value local for an after_set observer), and every one of those is assignable, so
            // the write must land on the same slot. Without this arm the emitter declared
            // `var v = value + 1` while every later read still emitted `value`, silently dropping
            // the write (#1500, measured: CPython 101, Sharpy 100; and with the assignment under an
            // `if`, CPython 5, Sharpy 100). Not reached from a shadowing binder — the rewrite is
            // suspended there (see SuspendAccessorParamRewriteIfShadowed).
            // (Augmented assignment lands on the same slot through AccessorParamSlotName below,
            // which keeps that path's operator lowering intact.)
            if (assign.Operator == AssignmentOperator.Assign
                && AccessorParamSlotName(name) is { } accessorSlot)
            {
                return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(accessorSlot),
                        value));
            }

            // Check if this is a simple assignment or augmented assignment
            if (assign.Operator == AssignmentOperator.Assign)
            {
                // Simple assignment: x = value. Whether this declares a fresh C# local or assigns
                // to a live one is the recorded TargetBinding (#1560); a module-level variable is
                // always assigned (it is a field).
                var symbol = _context.LookupSymbol(name.Name);
                var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
                var existsAsLocal = _context.SemanticInfo?.GetTargetBinding(name)?.Kind == TargetBindingKind.Rebinds;

                if (existsAsModuleLevel || existsAsLocal)
                {
                    // Variable exists - just update it with a regular assignment
                    var currentName = GetMangledVariableName(name, isNewDeclaration: false);

                    // `x = None` for an Optional<T> variable must produce Optional<T>.None
                    // rather than a bare `null` (which cannot convert to the struct).
                    // Use the symbol's type (authoritative since semantic analysis) or the
                    // target expression type.
                    var assignTargetType = (symbol is VariableSymbol varSym ? GetVariableType(varSym) : null)
                        ?? GetExpressionSemanticType(assign.Target)
                        ?? (symbol as VariableSymbol)?.Type;
                    var bareNoneValue = TryGenerateBareNoneForOptional(assign.Value, assignTargetType);
                    if (bareNoneValue != null)
                    {
                        value = bareNoneValue;
                    }
                    else if (symbol is VariableSymbol declaredVarSym)
                    {
                        value = ApplyOptionalDelegateConversion(assign.Value, value, declaredVarSym.Type);
                    }

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            EscapedIdentifierName(currentName),
                            value));
                }
                else
                {
                    // First declaration of this variable in this scope
                    var varName = GetMangledVariableName(name, isNewDeclaration: true);

                    // Check if the value is a lambda/function — C# can't infer delegate
                    // types with 'var'. Use explicit Func<>/Action<> from semantic type.
                    TypeSyntax declType;
                    var semanticType = GetExpressionSemanticType(assign.Value);
                    if (semanticType is not Semantic.FunctionType)
                    {
                        var varSymbol = _context.LookupSymbol(name.Name);
                        if (varSymbol is VariableSymbol vs && vs.Type is Semantic.FunctionType)
                            semanticType = vs.Type;
                    }
                    if (semanticType is Semantic.FunctionType ft && !ft.HasUnresolvedTypes())
                        declType = _typeMapper.MapSemanticType(semanticType);
                    else
                        declType = IdentifierName("var");

                    var declaration = VariableDeclaration(declType)
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(varName))
                                .WithInitializer(EqualsValueClause(value))));

                    return LocalDeclarationStatement(declaration);
                }
            }
            else
            {
                // Augmented assignment target: the name lives in the C# slot the accessor rewrite
                // names when it IS the accessor's incoming value (#1500).
                var varName = AccessorParamSlotName(name)
                    ?? GetMangledVariableName(name, isNewDeclaration: false);

                // #1428: if the TypeChecker materialized an in-place mutation for this assignment,
                // emit a method call instead of the read-modify-write rebind. The receiver is a
                // READ of the target, so the narrowed-read accessor applies: x on a narrowed
                // Optional<list[int]> mutates as x.Unwrap().Extend(...).
                var mutationMethod = _context.SemanticInfo?.GetAugmentedAssignMutation(assign);
                if (mutationMethod != null)
                {
                    return ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ApplyNarrowedReadLowering(name, EscapedIdentifierName(varName)),
                                IdentifierName(mutationMethod)))
                        .WithArgumentList(
                            ArgumentList(SingletonSeparatedList(Argument(value)))));
                }

                // Augmented assignment: x += value — references the current version and rebinds it.
                var target = EscapedIdentifierName(varName);

                // For the read side of augmented assignment, apply the narrowed-read accessor the
                // TypeChecker recorded for the target identifier so x += 1 with a narrowed
                // Optional<int> reads as x.Unwrap() + 1 (or .Value / ! for nullables). The write side
                // (target) is the un-narrowed identifier.
                //
                // Nothing to hoist here (#1227): an identifier target has no subexpressions, so
                // the read and write forms are two spellings of the same name and evaluating
                // both is free. The index and member paths below are where the double splice
                // becomes a double evaluation.
                var readExpr = ApplyNarrowedReadLowering(name, EscapedIdentifierName(varName));

                var augmentedValue = GenerateAugmentedValue(assign.Operator, readExpr, value, assign.Target, assign.Value, assign);

                return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        target,
                        augmentedValue));
            }
        }

        // Handle index assignment: arr[0] = value
        if (assign.Target is IndexAccess indexAccess)
        {
            var obj = GenerateExpression(indexAccess.Object);
            var index = GenerateExpression(indexAccess.Index);

            // An augmented index target is spliced TWICE — once into the read (`obj[index]` or
            // ArrayHelpers.GetItem(obj, index)) and once into the write — so `xs[idx()] += 1`
            // called idx() twice where CPython calls it once (#1227). Hoist the non-trivial
            // parts to temps first; both forms below then read the temps. A simple assignment
            // splices each part exactly once and must stay byte-identical, so it is excluded.
            if (assign.Operator != AssignmentOperator.Assign)
            {
                obj = HoistAugmentedTargetOperand(indexAccess.Object, obj, isWriteReceiver: true);
                index = HoistAugmentedTargetOperand(indexAccess.Index, index, isWriteReceiver: false);
            }

            // Array index assignment: route through ArrayHelpers for negative index support
            var objectType = GetExpressionSemanticType(indexAccess.Object);
            if (objectType is Semantic.GenericType { Name: BuiltinNames.Array })
            {
                var arrayHelpersSetItem = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                        IdentifierName("SetItem")));

                if (assign.Operator == AssignmentOperator.Assign)
                {
                    return ExpressionStatement(
                        arrayHelpersSetItem.AddArgumentListArguments(
                            Argument(obj),
                            Argument(index),
                            Argument(value)));
                }

                // Compound assignment (+=, -=, etc.): read via GetItem, compute, write via SetItem
                var getItem = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                        IdentifierName("GetItem")))
                    .AddArgumentListArguments(
                        Argument(obj),
                        Argument(index));

                var augmented = GenerateAugmentedValue(assign.Operator, getItem, value, assign.Target, assign.Value, assign);

                return ExpressionStatement(
                    arrayHelpersSetItem.AddArgumentListArguments(
                        Argument(obj),
                        Argument(index),
                        Argument(augmented)));
            }

            var elementAccess = ElementAccessExpression(obj)
                .WithArgumentList(BracketedArgumentList(
                    SingletonSeparatedList(Argument(index))));

            var augmentedValue = assign.Operator == AssignmentOperator.Assign
                ? value
                : GenerateAugmentedValue(assign.Operator, elementAccess, value, assign.Target, assign.Value, assign);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    elementAccess,
                    augmentedValue));
        }

        // Handle member assignment: obj.field = value
        if (assign.Target is MemberAccess memberAccess)
        {
            // Event subscription/unsubscription: obj.on_change += handler / -= handler
            // Emit native C# event += / -= instead of desugaring through GenerateAugmentedValue
            if (_context.SemanticInfo?.IsEventAccess(memberAccess) == true
                && (assign.Operator == AssignmentOperator.PlusAssign
                    || assign.Operator == AssignmentOperator.MinusAssign))
            {
                var eventTarget = GenerateMemberAccess(memberAccess);
                var assignKind = assign.Operator == AssignmentOperator.PlusAssign
                    ? SyntaxKind.AddAssignmentExpression
                    : SyntaxKind.SubtractAssignmentExpression;

                return ExpressionStatement(
                    AssignmentExpression(assignKind, eventTarget, value));
            }

            // Narrowing applies only to reads. For a simple (rebinding) assignment, emit the raw
            // field target (this.BestScore) rather than the narrowed read (this.BestScore.Unwrap());
            // the TypeChecker may record a narrowed-read lowering on this node, which must not be
            // applied to the write (a narrowed LHS is not an lvalue). Augmented assignments keep the
            // read-side accessor so `self.x += v` reads the narrowed value.
            var target = GenerateMemberAccess(memberAccess,
                applyNarrowing: assign.Operator != AssignmentOperator.Assign);

            // An augmented member target is generated once and spliced TWICE — into the read
            // side of the augmented value and into the assignment's LHS — so
            // `get_obj().value += 1` called get_obj() twice (#1227, measured). Retarget both
            // onto a temp holding the receiver. Only a plain member access is rewritten: a
            // narrowed read generates as an invocation (`obj.Field.Unwrap()`), which this
            // deliberately leaves alone rather than guessing at its shape. A simple assignment
            // splices the target once and is excluded so its emission stays byte-identical.
            if (assign.Operator != AssignmentOperator.Assign
                && target is MemberAccessExpressionSyntax memberTarget)
            {
                var receiver = HoistAugmentedTargetOperand(
                    memberAccess.Object, memberTarget.Expression, isWriteReceiver: true);
                if (receiver != memberTarget.Expression)
                    target = memberTarget.WithExpression(receiver);
            }

            // Method group → Optional<delegate> field needs an explicit delegate cast.
            // `obj.field = None` for an Optional<T> field must produce Optional<T>.None.
            var targetMemberType = GetExpressionSemanticType(assign.Target);
            var assignmentValue = assign.Operator == AssignmentOperator.Assign
                ? TryGenerateBareNoneForOptional(assign.Value, targetMemberType)
                    ?? ApplyOptionalDelegateConversion(assign.Value, value, targetMemberType)
                : GenerateAugmentedValue(assign.Operator, target, value, assign.Target, assign.Value, assign);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    target,
                    assignmentValue));
        }

        // Handle tuple unpacking: x, y = 1, 2
        if (assign.Target is TupleLiteral tuple)
        {
            // Star unpacking: first, *rest = items
            if (tuple.Elements.Any(e => e is StarExpression))
            {
                var starStmts = new List<StatementSyntax>();
                var starTempVar = $"__t{_tempVarCounter++}";
                starStmts.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(starTempVar))
                                .WithInitializer(EqualsValueClause(value))))));

                var valueType = GetExpressionSemanticType(assign.Value);
                GenerateStarUnpacking(tuple.Elements, starTempVar, valueType, starStmts);

                for (int i = 0; i < starStmts.Count - 1; i++)
                    _hoistedStatements.Add(starStmts[i]);
                return starStmts[^1];
            }

            // Generate C# tuple deconstruction
            // C#: var (x, y) = (1, 2)

            // Check if all elements are identifiers
            bool allIdentifiers = tuple.Elements.All(e => e is Identifier);

            if (allIdentifiers)
            {
                var identifiers = tuple.Elements.Cast<Identifier>().ToList();

                // Check which variables already exist (mirrors simple assignment path)
                var existenceFlags = identifiers.Select(id =>
                {
                    var baseName = LocalBaseName(id.Name, id.IsNameBacktickEscaped);
                    var symbol = _context.LookupSymbol(id.Name);
                    var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
                    var existsAsLocal = _context.SemanticInfo?.GetTargetBinding(id)?.Kind == TargetBindingKind.Rebinds;
                    return existsAsModuleLevel || existsAsLocal;
                }).ToList();

                bool allExist = existenceFlags.All(e => e);
                bool noneExist = existenceFlags.All(e => !e);

                if (noneExist)
                {
                    // All new — emit: var (a, b) = expr
                    var variables = identifiers
                        .Select(id =>
                        {
                            var varName = GetMangledVariableName(id, isNewDeclaration: true);
                            return SingleVariableDesignation(EscapedIdentifier(varName));
                        })
                        .ToList();

                    var tuplePattern = ParenthesizedVariableDesignation(
                        SeparatedList<VariableDesignationSyntax>(variables));

                    var declExpr = DeclarationExpression(
                        IdentifierName("var"),
                        tuplePattern);

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            declExpr,
                            value));
                }
                else if (allExist)
                {
                    // All existing — emit: (a, b) = expr (no var)
                    var arguments = identifiers
                        .Select(id =>
                        {
                            var currentName = GetMangledVariableName(id, isNewDeclaration: false);
                            return Argument(EscapedIdentifierName(currentName));
                        })
                        .ToList();

                    var tupleExpr = TupleExpression(SeparatedList(arguments));

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            tupleExpr,
                            value));
                }
                else
                {
                    // Mixed — some new, some existing: use temp + individual assignments.
                    // .ItemN access is only valid on ValueTuple types. When the RHS is
                    // a non-tuple type (e.g., list), fall back to deconstruction into
                    // fresh temp variables, then assign each to the correct target.
                    var rhsType = GetExpressionSemanticType(assign.Value);
                    var isTupleRhs = rhsType is Semantic.TupleType;

                    var stmts = new List<StatementSyntax>();

                    if (isTupleRhs)
                    {
                        // ValueTuple RHS — use .ItemN access (common case: a, b = b, a + b)
                        var mixedTempName = $"__t{_tempVarCounter++}";
                        stmts.Add(LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(EscapedIdentifier(mixedTempName))
                                        .WithInitializer(EqualsValueClause(value))))));

                        for (int i = 0; i < identifiers.Count; i++)
                        {
                            var id = identifiers[i];
                            var itemAccess = MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(mixedTempName),
                                IdentifierName($"Item{i + 1}"));

                            if (existenceFlags[i])
                            {
                                var currentName = GetMangledVariableName(id, isNewDeclaration: false);
                                stmts.Add(ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        EscapedIdentifierName(currentName),
                                        itemAccess)));
                            }
                            else
                            {
                                var varName = GetMangledVariableName(id, isNewDeclaration: true);
                                stmts.Add(LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("var"))
                                        .WithVariables(SingletonSeparatedList(
                                            VariableDeclarator(EscapedIdentifier(varName))
                                                .WithInitializer(EqualsValueClause(itemAccess))))));
                            }
                        }
                    }
                    else
                    {
                        // Non-tuple RHS — deconstruct into fresh temps, then assign
                        var tempNames = identifiers.Select(_ => $"__d{_tempVarCounter++}").ToList();
                        var tempDesignations = tempNames
                            .Select(n => (VariableDesignationSyntax)SingleVariableDesignation(EscapedIdentifier(n)))
                            .ToList();

                        var deconstructPattern = ParenthesizedVariableDesignation(
                            SeparatedList(tempDesignations));
                        stmts.Add(ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                DeclarationExpression(IdentifierName("var"), deconstructPattern),
                                value)));

                        for (int i = 0; i < identifiers.Count; i++)
                        {
                            var id = identifiers[i];
                            var tempRef = IdentifierName(tempNames[i]);

                            if (existenceFlags[i])
                            {
                                var currentName = GetMangledVariableName(id, isNewDeclaration: false);
                                stmts.Add(ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        EscapedIdentifierName(currentName),
                                        tempRef)));
                            }
                            else
                            {
                                var varName = GetMangledVariableName(id, isNewDeclaration: true);
                                stmts.Add(LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("var"))
                                        .WithVariables(SingletonSeparatedList(
                                            VariableDeclarator(EscapedIdentifier(varName))
                                                .WithInitializer(EqualsValueClause(tempRef))))));
                            }
                        }
                    }

                    // Hoist all but the last statement
                    for (int i = 0; i < stmts.Count - 1; i++)
                        _hoistedStatements.Add(stmts[i]);
                    return stmts[^1];
                }
            }

            // Complex tuple unpacking: (a, b), c = expr
            // Lower to temp variables + .ItemN access, hoisted as flat siblings
            var unpackStmts = new List<StatementSyntax>();
            var tempVarName = $"__t{_tempVarCounter++}";
            unpackStmts.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(tempVarName))
                            .WithInitializer(EqualsValueClause(value))))));
            GenerateRecursiveTupleUnpacking(tuple.Elements, tempVarName, unpackStmts);

            // Hoist all but the last statement; return the last as the result
            for (int i = 0; i < unpackStmts.Count - 1; i++)
                _hoistedStatements.Add(unpackStmts[i]);
            return unpackStmts[^1];
        }

        return GenerateStore(assign.Target, value);
    }

    private SyntaxKind GetAugmentedAssignmentOperator(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => SyntaxKind.AddExpression,
            AssignmentOperator.MinusAssign => SyntaxKind.SubtractExpression,
            AssignmentOperator.StarAssign => SyntaxKind.MultiplyExpression,
            AssignmentOperator.PercentAssign => SyntaxKind.ModuloExpression,
            AssignmentOperator.AndAssign => SyntaxKind.BitwiseAndExpression,
            AssignmentOperator.OrAssign => SyntaxKind.BitwiseOrExpression,
            AssignmentOperator.XorAssign => SyntaxKind.ExclusiveOrExpression,
            AssignmentOperator.LeftShiftAssign => SyntaxKind.LeftShiftExpression,
            AssignmentOperator.RightShiftAssign => SyntaxKind.RightShiftExpression,
            // Special cases handled by GenerateAugmentedValue (require casts or method calls)
            AssignmentOperator.SlashAssign => SyntaxKind.None,  // True division needs cast to double
            AssignmentOperator.DoubleSlashAssign => SyntaxKind.None,
            AssignmentOperator.PowerAssign => SyntaxKind.None,
            AssignmentOperator.NullCoalesceAssign => SyntaxKind.None,
            _ => SyntaxKind.None
        };
    }

    /// <summary>
    /// True when evaluating this augmented-assignment target subexpression twice is
    /// indistinguishable from evaluating it once, so it may be spliced into both the read and
    /// the write form without a temp. Identifiers (including <c>self</c>), <c>super</c> and
    /// literals qualify, as do pure compositions of them — a field chain, a parenthesized
    /// expression, an arithmetic index such as <c>i + 1</c>, a negated literal index.
    /// <para>
    /// Anything containing a call, an index read (a user <c>__getitem__</c> IS a call), a
    /// comprehension or an await does not qualify: those are exactly the shapes #1227 is about.
    /// Keeping the predicate tight is deliberate — every expression it accepts keeps its
    /// emission byte-identical to what it was before the hoist existed, which is what the C#
    /// snapshot fixtures pin.
    /// </para>
    /// </summary>
    private bool IsRepeatableTargetOperand(Expression expr)
        => IsRepeatableOperand(expr, MemberReadIsPlainField);

    /// <summary>
    /// The shape of the repeatability rule, with its one falsifiable arm supplied by the caller.
    /// Everything but <c>MemberAccess</c> is decided structurally — an identifier is a leaf
    /// whatever its type, and a composition of repeatable parts is repeatable — so
    /// <paramref name="memberReadIsRepeatable"/> is the ONLY place the answer depends on what the
    /// type system says, and therefore the only place it can be wrong.
    /// <para>
    /// Parameterized rather than duplicated (#1351): the splice tripwire in
    /// <c>GenerateExpressionReentryTests</c> needs this same recursion with the member arm
    /// answering "no", so that a wrong <see cref="MemberReadIsPlainField"/> cannot silence the
    /// guard that exists to catch it. Two hand-written copies of one recursion would drift; one
    /// recursion with two judgments cannot.
    /// </para>
    /// </summary>
    private static bool IsRepeatableOperand(
        Expression expr, Func<MemberAccess, bool> memberReadIsRepeatable) => expr switch
        {
            _ when IsRepeatableLeafOperand(expr) => true,
            Parenthesized paren => IsRepeatableOperand(paren.Expression, memberReadIsRepeatable),
            MemberAccess member => IsRepeatableOperand(member.Object, memberReadIsRepeatable)
                                   && memberReadIsRepeatable(member),
            UnaryOp unary => IsRepeatableOperand(unary.Operand, memberReadIsRepeatable),
            BinaryOp binary => IsRepeatableOperand(binary.Left, memberReadIsRepeatable)
                               && IsRepeatableOperand(binary.Right, memberReadIsRepeatable),
            _ => false
        };

    /// <summary>
    /// The LEAF arm: reads with nothing underneath them, so evaluating one twice is
    /// indistinguishable from evaluating it once no matter what the surrounding types are. Shared
    /// with the re-GENERATION tripwire, which exempts exactly these (a compound expression
    /// re-generated still duplicates the operation, so only leaves are exempt there).
    /// </summary>
    internal static bool IsRepeatableLeafOperand(Expression expr) => expr
        is Parser.Ast.Identifier or SuperExpression or NoneLiteral or BooleanLiteral
            or IntegerLiteral or FloatLiteral or StringLiteral;

    /// <summary>
    /// Repeatability decided WITHOUT consulting the type system: leaves and pure structural
    /// compositions of them (<c>xs[-1] += 1</c>, <c>xs[i + 1] += 1</c>, <c>(x) += 1</c>). A member
    /// read is never repeatable here, because whether it is depends on
    /// <see cref="MemberReadIsPlainField"/> — the answer <c>abc5bf4b0</c> got wrong.
    /// <para>
    /// This is the re-SPLICE tripwire's exemption (#1351). Exempting exactly the type-free cases
    /// is what makes the guard non-circular: if it deferred to
    /// <see cref="IsRepeatableTargetOperand"/> it would be asking the emitter whether the
    /// emitter's own decision was right, and inverting <see cref="MemberReadIsPlainField"/> would
    /// silence it instead of tripping it.
    /// </para>
    /// </summary>
    internal static bool IsRepeatableWithoutTypeInformation(Expression expr)
        => IsRepeatableOperand(expr, static _ => false);

    /// <summary>
    /// A member read is repeatable only when it is a plain FIELD on a known type: a property
    /// read runs a getter, and a function-style getter can carry arbitrary side effects, so
    /// repeating it is the #1227 defect wearing a member access (found live: `xs[b.idx] += 1`
    /// ran a counting getter twice while printing the right sum). Everything unknown — an
    /// unresolved receiver, a member found on neither list, an inherited member this symbol
    /// does not own — declines and hoists instead, which trades nothing: the hoist is
    /// byte-neutral in behavior for a pure read and correcting for an impure one. Only the
    /// KNOWN-field case may keep the no-hoist fast path the snapshots pin.
    /// </summary>
    private bool MemberReadIsPlainField(MemberAccess member)
    {
        var receiverSymbol = GetExpressionSemanticType(member.Object) switch
        {
            UserDefinedType { Symbol: { } s } => s,
            GenericType { GenericDefinition: { } d } => d,
            _ => null
        };
        if (receiverSymbol == null)
            return false;

        if (receiverSymbol.Properties.Any(p => p.Name == member.Member))
            return false;

        return receiverSymbol.Fields.Any(f => f.Name == member.Member);
    }

    /// <summary>
    /// True when a temp holding this expression's value still designates the same storage the
    /// expression itself designates — i.e. when the augmented WRITE may be retargeted onto the
    /// temp. Only reference types qualify.
    /// <para>
    /// Value types must not: <c>var t = get_point(); t.X = t.X + 1;</c> compiles and mutates a
    /// COPY, silently dropping the update, whereas the un-hoisted <c>GetPoint().X = …</c> is a
    /// C# error (CS1612, surfaced as SPY0908 — measured). Trading a loud error for a wrong
    /// answer is strictly worse than leaving the double evaluation in place, so an unresolved
    /// type or a value type declines the hoist and keeps today's emission exactly. The index
    /// EXPRESSION never comes through here — it is an rvalue passed by value on both sides, so
    /// hoisting it can never change which storage is written.
    /// </para>
    /// </summary>
    private bool CanRetargetAugmentedWrite(Expression expr)
    {
        return GetExpressionSemanticType(expr) switch
        {
            UserDefinedType { Symbol: { } symbol } =>
                symbol.TypeKind is Semantic.TypeKind.Class or Semantic.TypeKind.Interface,
            // The builtin collections are Sharpy.List/Dict/Set wrappers and CLR arrays, all
            // reference types. A user generic type is judged by its definition; anything whose
            // definition is unknown is declined rather than assumed.
            GenericType generic => generic.GenericDefinition is { } definition
                ? definition.TypeKind is Semantic.TypeKind.Class or Semantic.TypeKind.Interface
                : generic.Name is BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set
                    or BuiltinNames.Array or BuiltinNames.DefaultDict,
            _ => false
        };
    }

    /// <summary>
    /// Hoists one subexpression of an augmented-assignment target into a temp so that the read
    /// and write forms — which both splice it — evaluate it exactly once (#1227). Returns the
    /// temp identifier, or <paramref name="generated"/> unchanged when no hoist is needed or
    /// permitted.
    /// <para>
    /// The hoisted declaration goes into <c>_hoistedStatements</c>, which the statement emitter
    /// flushes as flat siblings ahead of the statement being generated. Evaluating the target's
    /// subexpressions before the value also matches CPython, which for
    /// <c>xs[idx()] += val()</c> prints <c>idx</c> then <c>val</c> (verified with python3);
    /// the un-hoisted emission printed <c>idx</c>, <c>idx</c>, <c>val</c>.
    /// </para>
    /// </summary>
    /// <param name="ast">The target subexpression's AST; null declines the hoist.</param>
    /// <param name="generated">Its already-generated C# syntax.</param>
    /// <param name="isWriteReceiver">
    /// True for the receiver the write is applied to (an indexer's collection, a member
    /// access's object), which additionally requires <see cref="CanRetargetAugmentedWrite"/>.
    /// False for a pure rvalue such as the index expression.
    /// </param>
    private ExpressionSyntax HoistAugmentedTargetOperand(
        Expression? ast, ExpressionSyntax generated, bool isWriteReceiver)
    {
        // Declining hands ONE generation back to a caller that splices it into both the read and
        // the write form — two evaluations of one operand. Reported to the recorder (null in
        // production; no allocation, no emission change) so the re-entry suite can check the
        // decision rather than trust it: generation counting cannot see a splice by construction,
        // which is why reintroducing abc5bf4b0 left that sweep green (#1351).
        if (ast == null || IsRepeatableTargetOperand(ast))
        {
            if (ast != null)
                _generationRecorder?.OnSplice(ast);
            return generated;
        }

        if (isWriteReceiver && !CanRetargetAugmentedWrite(ast))
        {
            _generationRecorder?.OnSplice(ast);
            return generated;
        }

        var tempName = $"__aug{_tempVarCounter++}";
        _hoistedStatements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tempName))
                        .WithInitializer(EqualsValueClause(generated))))));

        return IdentifierName(tempName);
    }

    /// <summary>
    /// Generates the value expression for an augmented assignment.
    /// Handles special cases like //= (floor divide) and **= (power) that require
    /// method calls or casts instead of simple binary expressions.
    /// </summary>
    /// <param name="op">The assignment operator</param>
    /// <param name="left">Generated C# expression for the target</param>
    /// <param name="right">Generated C# expression for the value</param>
    /// <param name="targetAst">Original AST target expression (for type inference)</param>
    /// <param name="valueAst">Original AST value expression (for type inference)</param>
    private ExpressionSyntax GenerateAugmentedValue(AssignmentOperator op, ExpressionSyntax left, ExpressionSyntax right, Expression? targetAst = null, Expression? valueAst = null, Assignment? assignNode = null)
    {
        return op switch
        {
            // x **= y → checked integer power for integral operands, Math.Pow otherwise —
            // decided by the binary `**` routing wrapper this site shares, so it cannot miss an
            // operand class the binary site handles. Until #1227 this arm emitted a raw
            // `Math.Pow(x, y)` and assigned the double straight back into the target, so the
            // integral forms did not compile at all: `x: int = 2; x **= 10` and `n: long = 3;
            // n **= 4` both failed with SPY0908 / CS0266 ("cannot implicitly convert 'double' to
            // 'int'/'long'"). The target's own type selects the integer width.
            AssignmentOperator.PowerAssign =>
                GeneratePowerValue(left, right,
                    assignNode ?? throw new InvalidOperationException(
                        "Augmented '**=' needs its Assignment node to read the recorded power lowering (#1623)")),

            // x /= y → true division with Python semantics (always returns float64)
            // Cast left to double if both operands are integers
            AssignmentOperator.SlashAssign => GenerateTrueDivisionAugmented(left, right, assignNode),

            // x //= y → floor division with Python semantics (toward negative infinity):
            // Builtins.FloorDiv (int/long/float/double overloads) or the native truncating
            // decimal quotient, selected by the OperatorLowering tag recorded on this Assignment
            // (#1658) inside the binary `//` routing wrapper this site shares, so it cannot miss
            // an operand class the binary site handles.
            AssignmentOperator.DoubleSlashAssign =>
                GenerateFloorDivideValue(left, right,
                    assignNode ?? throw new InvalidOperationException(
                        "Augmented '//=' needs its Assignment node to read the recorded floor-division lowering (#1658)")),

            // x %= y → Python floored modulo (sign of divisor) for int/long/float operands, and
            // the zero-guarded native remainder for decimal — selected by the OperatorLowering tag
            // recorded on this Assignment (#1658) inside the binary `%` routing wrapper this site
            // shares, so it cannot miss an operand class the binary site handles. User __mod__
            // types (operator %) and other CLR op_Modulus types have no record, get null back,
            // and fall through to the native `%=` (PercentAssign → ModuloExpression).
            AssignmentOperator.PercentAssign
                when GenerateModuloValue(left, right,
                    assignNode ?? throw new InvalidOperationException(
                        "Augmented '%=' needs its Assignment node to read the recorded modulo lowering (#1658)"))
                    is { } moduloValue =>
                moduloValue,

            // x ??= y → lowered null coalescing (Optional-aware)
            AssignmentOperator.NullCoalesceAssign =>
                GenerateNullCoalesceValue(left, right, assignNode),

            // x @= y → x = x.MatMul(y) (matrix multiplication, PEP 465; no native C# operator)
            AssignmentOperator.MatMulAssign =>
                GenerateMatMulCall(left, right),

            // All other operators use simple binary expressions
            _ => GenerateAugmentedBinaryExpression(op, left, right, targetAst, assignNode)
        };
    }

    private ExpressionSyntax GenerateAugmentedBinaryExpression(AssignmentOperator op, ExpressionSyntax left, ExpressionSyntax right, Expression? sourceAst = null, Assignment? assignNode = null)
    {
        var kind = GetAugmentedAssignmentOperator(op);
        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: augmented assignment operator '{op}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator,
                sourceAst?.LineStart, sourceAst?.ColumnStart);
        }

        if (assignNode != null)
        {
            var augLowering = _context.SemanticInfo?.GetOperatorLowering(assignNode)?.Kind;
            if (augLowering == OperatorLoweringKind.StringRepeatStrLeft)
                return GenerateStringRepeat(left, right);
            if (augLowering == OperatorLoweringKind.StringRepeatStrRight)
                return GenerateStringRepeat(right, left);
        }

        if (assignNode != null
            && _context.SemanticInfo?.GetOperatorLowering(assignNode)?.Kind
                == OperatorLoweringKind.ShiftCountCastToInt)
        {
            right = CastExpression(
                PredefinedType(Token(SyntaxKind.IntKeyword)),
                ParenthesizedExpression(right));
        }

        return BinaryExpression(kind, left, right);
    }

    /// <summary>
    /// Generates a null-coalescing value, aware of Optional vs nullable types.
    /// Reads the OperatorLowering tag to decide between Optional ternary and native ?? (#1623).
    /// </summary>
    private ExpressionSyntax GenerateNullCoalesceValue(ExpressionSyntax left, ExpressionSyntax right, Assignment? assignNode)
    {
        if (assignNode != null
            && _context.SemanticInfo?.GetOperatorLowering(assignNode)?.Kind
                == OperatorLoweringKind.OptionalCoalesceBothOptional)
        {
            return ConditionalExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, left, IdentifierName("IsSome")),
                left,
                right);
        }
        return BinaryExpression(SyntaxKind.CoalesceExpression, left, right);
    }

    /// <summary>
    /// Generates true division for augmented assignment (x /= y).
    /// Reads the OperatorLowering tag to decide whether to cast the left operand (#1623).
    /// </summary>
    private ExpressionSyntax GenerateTrueDivisionAugmented(ExpressionSyntax left, ExpressionSyntax right, Assignment? assignNode)
    {
        if (assignNode != null
            && _context.SemanticInfo?.GetOperatorLowering(assignNode)?.Kind
                == OperatorLoweringKind.TrueDivisionCastLeft)
        {
            return BinaryExpression(SyntaxKind.DivideExpression,
                CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
                right);
        }
        return BinaryExpression(SyntaxKind.DivideExpression, left, right);
    }

    private StatementSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        // No carrier check here, deliberately. The alias elision this path carried inline until
        // #1248 is gone with the alias itself; a carrier reaching a DECLARED type is caught by
        // TypeSyntaxMapper, which throws on it.

        // Track const variables by their original Sharpy name for consistent reference resolution
        if (varDecl.IsConst)
        {
        }

        // Check if this is a lambda with default parameters — emit as a local function
        // instead of a delegate. C# delegates / Func<> don't support optional parameters,
        // but local functions do, so `f = lambda x: int, y: int = 10: x + y` becomes
        //   long f(long x, long y = 10) => x + y;
        if (varDecl.InitialValue is LambdaExpression lambdaWithDefaults
            && HasDefaultParameters(lambdaWithDefaults))
        {
            var localFuncName = GetMangledVariableName(varDecl, isNewDeclaration: true);
            return GenerateLambdaAsLocalFunction(lambdaWithDefaults, localFuncName);
        }

        // Resolve the declared type up front so a direct `x: T? = None` initializer can
        // target Optional<T>.None and later reassignments can do the same.
        var declaredType = varDecl.Type != null
            ? _context.SemanticInfo?.GetTypeAnnotation(varDecl.Type)
            : null;

        // IMPORTANT: Generate the initializer expression FIRST, before updating version tracking.
        // This ensures that references to the same variable in the initializer (e.g., x: int = x + 1)
        // use the PREVIOUS version of the variable, not the new one being declared.
        ExpressionSyntax? initialValue = null;
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            // This allows list/dict/set literals to use the declared type
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                // `x: T? = None` (direct bare None) must produce Optional<T>.None.
                initialValue = TryGenerateBareNoneForOptional(varDecl.InitialValue, declaredType)
                    ?? GenerateExpression(varDecl.InitialValue);
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        // The declared name is the symbol's recorded spelling — for a local const too. The
        // allocator versions a const re-declared in a sibling block (K, K_1) and every reference
        // reads that spelling; declaring it from NameCasing.ResolveConstant here left the
        // declaration at K while the references said K_1 (CS0103 behind SPY0908, #1560 R4).
        var varName = GetMangledVariableName(varDecl, isNewDeclaration: true);

        // Handle 'auto' type annotation - use 'var' in C#
        // For const without type annotation, infer type from initializer (C# const can't use 'var')
        TypeSyntax typeSyntax;
        if (varDecl.Type != null && varDecl.Type.Name == "auto")
        {
            // Check if the initializer is a lambda or function reference — C# can't
            // infer lambda/method-group types with 'var'. Use semantic type to emit
            // explicit Func<>/Action<> instead.
            var initSemanticType = varDecl.InitialValue != null
                ? GetExpressionSemanticType(varDecl.InitialValue)
                : null;

            // Also check the variable's own symbol type (may have better inference)
            if (initSemanticType is not Semantic.FunctionType)
            {
                var varSymbol = _context.LookupSymbol(varDecl.Name);
                if (varSymbol is VariableSymbol vs && vs.Type is Semantic.FunctionType)
                    initSemanticType = vs.Type;
            }

            if (initSemanticType is Semantic.FunctionType ft && !ft.HasUnresolvedTypes())
            {
                typeSyntax = _typeMapper.MapSemanticType(initSemanticType);
            }
            else
            {
                typeSyntax = IdentifierName("var");
            }
        }
        else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            typeSyntax = ResolveInferredDeclarationType(varDecl.InitialValue);
        }
        else
        {
            typeSyntax = _typeMapper.MapType(varDecl.Type);
        }

        // Track this variable as declared

        // Method group → Optional<delegate> needs an explicit delegate cast:
        // f: ((str) -> None)? = printer → Optional<Action<string>> f = (Action<string>)printer
        if (initialValue != null && varDecl.InitialValue != null
            && varDecl.Type is { IsOptional: true, Name: "function" } optFuncAnnotation
            && IsMethodGroupOrLambda(varDecl.InitialValue))
        {
            var delegateTypeSyntax = _typeMapper.MapType(optFuncAnnotation with { IsOptional = false });
            initialValue = ParenthesizedExpression(
                CastExpression(delegateTypeSyntax, ParenthesizedExpression(initialValue)));
        }

        VariableDeclaratorSyntax declarator;
        if (initialValue != null)
        {
            declarator = VariableDeclarator(EscapedIdentifier(varName))
                .WithInitializer(EqualsValueClause(initialValue));
        }
        else if (varDecl.InitialValue == null && _context.SemanticInfo?.IsDefinitelyAssignedBareLocal(varDecl) == true)
        {
            declarator = VariableDeclarator(EscapedIdentifier(varName))
                .WithInitializer(EqualsValueClause(
                    PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                        LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword)))));
        }
        else
        {
            declarator = VariableDeclarator(EscapedIdentifier(varName));
        }

        var declaration = VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(declarator));

        // C# const only works with predefined types (int, string, bool, etc.)
        var modifiers = varDecl.IsConst && IsConstEligibleType(typeSyntax)
            ? TokenList(Token(SyntaxKind.ConstKeyword))
            : TokenList();

        return LocalDeclarationStatement(declaration)
            .WithModifiers(modifiers);
    }

    /// <summary>
    /// Resolves the C# declaration type for an auto/const variable or field whose type is inferred
    /// from its initializer. The authoritative type is the initializer's inferred type recorded by
    /// the TypeChecker in <c>SemanticInfo</c>; <c>object</c> is the neutral result only when no
    /// concrete type was inferred (e.g. a bare <c>None</c> initializer).
    /// </summary>
    private TypeSyntax ResolveInferredDeclarationType(Expression? initialValue)
    {
        if (initialValue != null
            && GetExpressionSemanticType(initialValue) is { } inferred
            && inferred is not UnknownType)
        {
            return _typeMapper.MapSemanticType(inferred);
        }

        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
    }

    private FieldDeclarationSyntax? GenerateModuleLevelField(VariableDeclaration varDecl)
    {
        // Check if this variable has execution order issues (assigned before declared, or multiple declarations)
        // If so, skip generating a field - it will be handled as a local variable in Main()
        // UNLESS _forceModuleLevelFields is true (when there's a user-defined main function)
        var symbol = _context.LookupSymbol(varDecl.Name);
        if (symbol != null && HasExecutionOrderIssues(symbol) && !_forceModuleLevelFields)
        {
            return null;
        }
        // Note: If symbol is null, we can't check execution order issues
        // This shouldn't happen in well-typed code that went through semantic analysis

        // Track const variables by their original Sharpy name for consistent reference resolution
        if (varDecl.IsConst)
        {
        }

        // Module-level fields naming:
        // - Explicitly const declarations use CONSTANT_CASE
        // - Names that look like constants (ALL_CAPS) also use CONSTANT_CASE
        //   This supports Python-style convention where MAX_SIZE implies a constant
        // - Other names use PascalCase
        string varName;
        if (varDecl.IsConst || NameFormDetector.IsConstantCaseName(varDecl.Name))
        {
            varName = NameCasing.ResolveConstant(varDecl.Name, varDecl.IsNameBacktickEscaped);
        }
        else
        {
            varName = NameCasing.ResolveField(varDecl.Name, varDecl.IsNameBacktickEscaped);
        }

        // Check if we've already generated a field with this name (redefinition)
        // Sharpy allows variable redefinition at module level with different types.
        // When there are redefinitions, we return null to handle them as executable
        // statements in Main() to preserve proper execution order semantics.
        if (_moduleFieldNames.Contains(varName))
        {
            // This is a redefinition - handle as executable statement in Main
            return null;
        }

        // Track this field name to detect future redefinitions
        _moduleFieldNames.Add(varName);

        // Handle 'auto' type annotation - for fields, we must resolve to concrete type
        // For const without type annotation, infer type from initializer
        TypeSyntax typeSyntax;
        if (varDecl.Type != null && varDecl.Type.Name == "auto")
        {
            // Infer type from initializer
            if (varDecl.InitialValue != null)
            {
                typeSyntax = ResolveInferredDeclarationType(varDecl.InitialValue);
            }
            else
            {
                // No initializer - default to object
                typeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
            }
        }
        else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            typeSyntax = ResolveInferredDeclarationType(varDecl.InitialValue);
        }
        else
        {
            typeSyntax = _typeMapper.MapType(varDecl.Type);
        }

        VariableDeclaratorSyntax declarator;
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var value = GenerateInitializerValue(varDecl.InitialValue, varDecl.Type);
                declarator = VariableDeclarator(EscapedIdentifier(varName))
                    .WithInitializer(EqualsValueClause(value));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }
        else
        {
            declarator = VariableDeclarator(EscapedIdentifier(varName));
        }

        var declaration = VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(declarator));

        // Module-level fields must be static
        // For const variables, use C# const when CodeGenInfo says the initializer is compile-time
        // foldable (#1460 — covers expressions like `100 + 100`, not just bare literals).
        // Otherwise fall back to public static readonly.
        // Regular variables become "public static"
        SyntaxTokenList modifiers;
        var codeGenInfo = symbol != null ? GetCodeGenInfo(symbol) : null;
        if (varDecl.IsConst
            && (codeGenInfo?.IsCompileTimeConstant == true
                || (IsCompileTimeLiteral(varDecl.InitialValue) && IsConstEligibleType(typeSyntax))))
        {
            // Use const for compile-time literals with const-eligible types
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.ConstKeyword));
        }
        else if (varDecl.IsConst)
        {
            // Use static readonly for non-literal const values
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword));
        }
        else
        {
            // Regular variables become public static
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword));
        }

        var fieldDeclaration = FieldDeclaration(declaration)
            .WithModifiers(modifiers);

        // The reverse mangle cannot recover every original spelling from the CLR name —
        // it is non-injective for single characters (Sharpy `e` → CLR `E` by PascalCase,
        // Sharpy `I` → CLR `I` by constant-case identity). When inference would round-trip
        // wrong, record the authoritative Python name for discovery to read (#1607).
        if (NameMangler.ToSharpyName(varName, ReverseNameContext.Field) != varDecl.Name)
        {
            fieldDeclaration = fieldDeclaration.AddAttributeLists(
                AttributeList(SingletonSeparatedList(
                    Attribute(MakeGlobalQualifiedName("Sharpy", "SharpyFieldName"))
                        .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(
                            AttributeArgument(LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(varDecl.Name)))))))));
        }

        return fieldDeclaration;
    }

    /// <summary>
    /// Generates star/rest unpacking: first, *rest, last = items
    /// Lowers to indexed access for non-star elements and slicing for the star element.
    /// </summary>
    private void GenerateStarUnpacking(
        ImmutableArray<Expression> elements, string sourceVar, SemanticType? valueType,
        List<StatementSyntax> statements)
    {
        // Find star position
        int starIndex = -1;
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] is StarExpression)
            {
                starIndex = i;
                break;
            }
        }

        int numBefore = starIndex;
        int numAfter = elements.Length - starIndex - 1;

        // Check if source is a tuple (ValueTuple) — needs .ItemN access instead of indexing
        var isTupleSource = valueType is Semantic.TupleType;
        var tupleArity = isTupleSource ? ((Semantic.TupleType)valueType!).ElementTypes.Count : 0;

        // Determine element type for the Sharpy.List<T> wrapper
        TypeSyntax elementTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        if (valueType is GenericType { Name: BuiltinNames.List } listType && listType.TypeArguments.Count > 0)
        {
            elementTypeSyntax = _typeMapper.MapSemanticType(listType.TypeArguments[0]);
        }
        else if (valueType is Semantic.TupleType tupleType)
        {
            // Collect the rest element types (those that go into the star variable)
            var restTypes = new List<SemanticType>();
            for (int ri = numBefore; ri < tupleArity - numAfter; ri++)
            {
                if (ri >= 0 && ri < tupleType.ElementTypes.Count)
                    restTypes.Add(tupleType.ElementTypes[ri]);
            }

            if (restTypes.Count > 0 && restTypes.All(t => t.Equals(restTypes[0])))
            {
                elementTypeSyntax = _typeMapper.MapSemanticType(restTypes[0]);
            }
        }

        // Elements before star: name = __t[i] or __t.ItemN (for tuples)
        for (int i = 0; i < numBefore; i++)
        {
            ExpressionSyntax indexExpr;
            if (isTupleSource)
            {
                indexExpr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(sourceVar),
                    IdentifierName($"Item{i + 1}"));
            }
            else
            {
                indexExpr = ElementAccessExpression(IdentifierName(sourceVar))
                    .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(
                        Argument(LiteralExpression(
                            SyntaxKind.NumericLiteralExpression,
                            Literal(i))))));
            }

            statements.Add(GenerateStore(elements[i], indexExpr));
        }

        // Star element: rest = __t.GetSlice(...) or new Sharpy.List<T> { __t.ItemN, ... } (for tuples)
        if (elements[starIndex] is StarExpression starExpr && starExpr.Operand is Identifier starId)
        {
            var starBaseName = LocalBaseName(starId.Name, starId.IsNameBacktickEscaped);
            var starSym = _context.LookupSymbol(starId.Name);
            var starExistsAsModuleLevel = starSym != null && GetCodeGenInfo(starSym)?.IsModuleLevel == true;
            var starExistsAsLocal = _context.SemanticInfo?.GetTargetBinding(starId)?.Kind == TargetBindingKind.Rebinds;
            var starIsExisting = starExistsAsModuleLevel || starExistsAsLocal;

            ExpressionSyntax starValueExpr;
            if (isTupleSource)
            {
                // Build: new Sharpy.List<T> { __t.ItemN, __t.ItemM, ... }
                var listTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(
                    CSharpTypeNames.SharpyList, elementTypeSyntax);
                var restItems = new List<ExpressionSyntax>();
                for (int ri = numBefore; ri < tupleArity - numAfter; ri++)
                {
                    restItems.Add(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName($"Item{ri + 1}")));
                }

                starValueExpr = ObjectCreationExpression(listTypeSyntax)
                    .WithArgumentList(ArgumentList())
                    .WithInitializer(InitializerExpression(
                        SyntaxKind.CollectionInitializerExpression,
                        SeparatedList(restItems)));
            }
            else
            {
                var startArg = numBefore > 0
                    ? (ExpressionSyntax)LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numBefore))
                    : LiteralExpression(SyntaxKind.NullLiteralExpression);

                var endArg = numAfter > 0
                    ? (ExpressionSyntax)PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numAfter)))
                    : LiteralExpression(SyntaxKind.NullLiteralExpression);

                // __t.GetSlice(new global::Sharpy.Slice(start, end))
                var newSlice = ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "Slice"))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(startArg),
                        Argument(endArg)
                    })));

                starValueExpr = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName("GetSlice")))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(newSlice))));
            }

            if (starIsExisting)
            {
                var currentStarName = GetMangledVariableName(starId, isNewDeclaration: false);
                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        EscapedIdentifierName(currentStarName),
                        starValueExpr)));
            }
            else
            {
                var starVarName = GetMangledVariableName(starId, isNewDeclaration: true);
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(starVarName))
                                .WithInitializer(EqualsValueClause(starValueExpr))))));
            }
        }

        // Elements after star: name = __t[-n] or __t.ItemN (for tuples)
        for (int i = 0; i < numAfter; i++)
        {
            int elemIndex = starIndex + 1 + i;

            ExpressionSyntax afterExpr;
            if (isTupleSource)
            {
                int itemIndex = tupleArity - numAfter + i + 1;
                afterExpr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(sourceVar),
                    IdentifierName($"Item{itemIndex}"));
            }
            else
            {
                int negIndex = numAfter - i;
                afterExpr = ElementAccessExpression(IdentifierName(sourceVar))
                    .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(
                        Argument(PrefixUnaryExpression(
                            SyntaxKind.UnaryMinusExpression,
                            LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(negIndex)))))));
            }

            statements.Add(GenerateStore(elements[elemIndex], afterExpr));
        }
    }

    private void GenerateRecursiveTupleUnpacking(
        ImmutableArray<Expression> targets, string sourceVarName, List<StatementSyntax> statements)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var itemAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(sourceVarName),
                IdentifierName($"Item{i + 1}"));

            if (targets[i] is TupleLiteral nestedTuple)
            {
                var tempVarName = $"__t{_tempVarCounter++}";
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(tempVarName))
                                .WithInitializer(EqualsValueClause(itemAccess))))));
                GenerateRecursiveTupleUnpacking(nestedTuple.Elements, tempVarName, statements);
            }
            else
            {
                statements.Add(GenerateStore(targets[i], itemAccess));
            }
        }
    }

    private StatementSyntax GenerateStore(Expression target, ExpressionSyntax value)
    {
        switch (target)
        {
            case Identifier id:
                {
                    var symbol = _context.LookupSymbol(id.Name);
                    var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
                    var existsAsLocal = _context.SemanticInfo?.GetTargetBinding(id)?.Kind == TargetBindingKind.Rebinds;

                    if (existsAsModuleLevel || existsAsLocal)
                    {
                        var currentName = GetMangledVariableName(id, isNewDeclaration: false);
                        return ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                EscapedIdentifierName(currentName),
                                value));
                    }

                    var varName = GetMangledVariableName(id, isNewDeclaration: true);
                    return LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(EscapedIdentifier(varName))
                                    .WithInitializer(EqualsValueClause(value)))));
                }

            case IndexAccess indexAccess:
                {
                    var obj = GenerateExpression(indexAccess.Object);
                    var index = GenerateExpression(indexAccess.Index);

                    var objectType = GetExpressionSemanticType(indexAccess.Object);
                    if (objectType is Semantic.GenericType { Name: BuiltinNames.Array })
                    {
                        return ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                                    IdentifierName("SetItem")))
                                .AddArgumentListArguments(
                                    Argument(obj),
                                    Argument(index),
                                    Argument(value)));
                    }

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            ElementAccessExpression(obj)
                                .WithArgumentList(BracketedArgumentList(
                                    SingletonSeparatedList(Argument(index)))),
                            value));
                }

            case MemberAccess memberAccess:
                {
                    var memberTarget = GenerateMemberAccess(memberAccess, applyNarrowing: false);
                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            memberTarget,
                            value));
                }

            case TupleLiteral tuple:
                {
                    var stmts = new List<StatementSyntax>();
                    var tempVarName = $"__t{_tempVarCounter++}";
                    stmts.Add(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(EscapedIdentifier(tempVarName))
                                    .WithInitializer(EqualsValueClause(value))))));
                    GenerateRecursiveTupleUnpacking(tuple.Elements, tempVarName, stmts);

                    for (int i = 0; i < stmts.Count - 1; i++)
                        _hoistedStatements.Add(stmts[i]);
                    return stmts[^1];
                }

            default:
                return EmitNotImplementedStatement(
                    $"Unsupported store target type '{target.GetType().Name}'",
                    DiagnosticCodes.CodeGen.UnsupportedExpressionType,
                    target.LineStart, target.ColumnStart);
        }
    }
}
