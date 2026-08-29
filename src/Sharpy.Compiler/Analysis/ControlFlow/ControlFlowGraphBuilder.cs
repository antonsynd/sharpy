using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Builds a control flow graph from a function body.
/// </summary>
/// <remarks>
/// The builder operates on the immutable AST nodes (Statement, Expression).
/// It does NOT modify the AST. The resulting CFG references AST nodes
/// but is a separate data structure.
/// </remarks>
internal class ControlFlowGraphBuilder
{
    private readonly List<BasicBlock> _blocks = new();
    private BasicBlock _currentBlock = null!;
    private BasicBlock _entry = null!;
    private BasicBlock _exit = null!;

    // Optional set of match statements known to be exhaustive (precomputed by caller)
    private readonly HashSet<MatchStatement>? _exhaustiveMatches;

    private readonly SemanticInfo? _semanticInfo;

    // Loop tracking for break/continue
    private readonly Stack<LoopContext> _loopStack = new();

    // Exception handler tracking for re-raise
    private readonly Stack<BasicBlock> _handlerStack = new();

    // Defer bodies collected during body build, inserted at scope exit (LIFO)
    private readonly List<DeferStatement> _deferBodies = new();

    public ControlFlowGraphBuilder() : this(null, null) { }

    public ControlFlowGraphBuilder(HashSet<MatchStatement>? exhaustiveMatches,
        SemanticInfo? semanticInfo = null)
    {
        _exhaustiveMatches = exhaustiveMatches;
        _semanticInfo = semanticInfo;
    }

    /// <summary>
    /// Context for loop constructs, tracking where break/continue should go.
    /// </summary>
    private record LoopContext(
        BasicBlock Header,        // Where continue jumps to
        BasicBlock Exit,          // Where break jumps to
        BasicBlock? ElseBlock     // Optional else block (runs if loop completes without break)
    );

    /// <summary>
    /// Build a CFG from a function definition.
    /// </summary>
    public ControlFlowGraph Build(FunctionDef function)
    {
        Reset();

        _entry = CreateBlock("entry");
        _exit = CreateBlock("exit");

        var bodyStart = CreateBlock("body_start");
        Connect(_entry, bodyStart);
        _entry.Terminator = new BranchTerminator(bodyStart);
        _currentBlock = bodyStart;

        BuildStatements(function.Body);

        // Insert deferred bodies (LIFO) on the fall-through exit path
        InsertDeferChain();

        // If we didn't explicitly return, connect to exit
        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, _exit);
            _currentBlock.Terminator = new BranchTerminator(_exit);
        }

        return new ControlFlowGraph(_entry, _exit, _blocks, function);
    }

    /// <summary>
    /// Build a CFG from a list of top-level statements (module body).
    /// </summary>
    public ControlFlowGraph Build(IReadOnlyList<Statement> statements)
    {
        Reset();

        _entry = CreateBlock("entry");
        _exit = CreateBlock("exit");

        var bodyStart = CreateBlock("body_start");
        Connect(_entry, bodyStart);
        _entry.Terminator = new BranchTerminator(bodyStart);
        _currentBlock = bodyStart;

        BuildStatements(statements);

        InsertDeferChain();

        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, _exit);
            _currentBlock.Terminator = new BranchTerminator(_exit);
        }

        return new ControlFlowGraph(_entry, _exit, _blocks);
    }

    private void Reset()
    {
        _blocks.Clear();
        _loopStack.Clear();
        _handlerStack.Clear();
        _deferBodies.Clear();
        _currentBlock = null!;
    }

    private BasicBlock CreateBlock(string label = "")
    {
        var block = new BasicBlock(label);
        _blocks.Add(block);
        return block;
    }

    private void Connect(BasicBlock from, BasicBlock to)
    {
        from.AddSuccessor(to);
        to.AddPredecessor(from);
    }

    /// <summary>
    /// Connects two blocks with an exception edge. The successor relationship is maintained
    /// (so forward reachability still works), but the handler sees this as an exception
    /// predecessor rather than a normal predecessor. This distinction allows dataflow
    /// analyses to use conservative assumptions for exception edges.
    /// </summary>
    private void ConnectException(BasicBlock from, BasicBlock to)
    {
        from.AddSuccessor(to);
        to.AddExceptionPredecessor(from);
    }

    private void BuildStatements(IReadOnlyList<Statement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            BuildStatement(statements[i]);

            // If current block is terminated, remaining statements are unreachable.
            // Create a disconnected block so FindUnreachableBlocks() can find them.
            if (_currentBlock.Terminator != null && i + 1 < statements.Count)
            {
                var unreachableBlock = CreateBlock("unreachable");
                _currentBlock = unreachableBlock;

                // Add remaining statements to the unreachable block
                for (int j = i + 1; j < statements.Count; j++)
                {
                    BuildStatement(statements[j]);
                    if (_currentBlock.Terminator != null && j + 1 < statements.Count)
                    {
                        // If the unreachable block itself terminates, create another
                        var nextUnreachable = CreateBlock("unreachable");
                        _currentBlock = nextUnreachable;
                    }
                }
                break;
            }
        }
    }

    private void BuildStatement(Statement stmt)
    {
        switch (stmt)
        {
            case ReturnStatement ret:
                BuildReturn(ret);
                break;

            case IfStatement ifStmt:
                BuildIf(ifStmt);
                break;

            case WhileStatement whileStmt:
                BuildWhile(whileStmt);
                break;

            case ForStatement forStmt:
                BuildFor(forStmt);
                break;

            case BreakStatement breakStmt:
                BuildBreak(breakStmt);
                break;

            case BreakWithFlagStatement breakWithFlag:
                // BreakWithFlagStatement is an internal statement for loop-else support.
                // It sets a flag variable then breaks. We treat it as a break for CFG purposes.
                BuildBreakWithFlag(breakWithFlag);
                break;

            case ContinueStatement contStmt:
                BuildContinue(contStmt);
                break;

            case TryStatement tryStmt:
                BuildTry(tryStmt);
                break;

            case WithStatement withStmt:
                BuildWith(withStmt);
                break;

            case RaiseStatement raiseStmt:
                BuildRaise(raiseStmt);
                break;

            case FunctionDef:
            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
            case PropertyDef:
            case TypeAlias:
            case ImportStatement:
            case FromImportStatement:
                // Type/function/property definitions and imports don't affect control flow
                break;

            case DecoratedStatement decorated:
                // @suppress wrapper (#1024): decorators are compile-time-only; the inner statement
                // drives control flow.
                BuildStatement(decorated.Statement);
                break;

            case MatchStatement matchStmt:
                BuildMatch(matchStmt);
                break;

            case DeferStatement deferStmt:
                BuildDefer(deferStmt);
                break;

            case YieldStatement:
                // Yield does not terminate a block — it produces a value and continues
                AddStatement(stmt);
                break;

            default:
                // Simple statements - add to current block
                AddStatement(stmt);
                break;
        }
    }

    private void AddStatement(Statement stmt)
    {
        _currentBlock.AddStatement(stmt);

        if (!_currentBlock.ContainsAwait && ContainsAwaitExpression(stmt))
        {
            _currentBlock.ContainsAwait = true;
        }

        // A match EXPRESSION's subject is evaluated within this block's flow, in the same position an
        // `if` condition or a match STATEMENT's subject occupies — so record it, letting the flow
        // analysis freeze the block's out-set for it and narrowing reach the arms (#1502). This is
        // the builder's only expression-walking, kept targeted to MatchExpression subjects.
        CollectMatchExpressionSubjects(stmt, _currentBlock.MatchExpressionSubjects);
    }

    /// <summary>
    /// Appends the subject of every match EXPRESSION reachable in <paramref name="node"/>'s tree to
    /// <paramref name="into"/>, without descending into lambda or nested-function bodies (they form
    /// their own graphs and never see this scope's facts, mirroring the builder's per-function
    /// scoping). Targeted to <see cref="MatchExpression"/> nodes — not a general expression CFG (#1502).
    /// </summary>
    private static void CollectMatchExpressionSubjects(Node node, List<Expression> into)
    {
        if (node is MatchExpression matchExpr)
            into.Add(matchExpr.Scrutinee);

        if (node is LambdaExpression or FunctionDef)
            return;

        foreach (var child in node.GetChildNodes())
            CollectMatchExpressionSubjects(child, into);
    }

    /// <summary>
    /// Recursively scans an AST node tree for AwaitExpression nodes.
    /// </summary>
    private static bool ContainsAwaitExpression(Node node)
    {
        if (node is AwaitExpression)
            return true;

        foreach (var child in node.GetChildNodes())
        {
            if (ContainsAwaitExpression(child))
                return true;
        }

        return false;
    }

    private void BuildReturn(ReturnStatement stmt)
    {
        AddStatement(stmt);
        Connect(_currentBlock, _exit);
        _currentBlock.Terminator = new ReturnTerminator(stmt.Value)
        {
            SourceStatement = stmt
        };
    }

    private void BuildDefer(DeferStatement stmt)
    {
        _deferBodies.Add(stmt);
    }

    private void InsertDeferChain()
    {
        if (_deferBodies.Count == 0)
            return;

        for (int i = _deferBodies.Count - 1; i >= 0; i--)
        {
            if (_currentBlock.Terminator != null)
                break;

            var deferBlock = CreateBlock("defer_body");
            Connect(_currentBlock, deferBlock);
            _currentBlock.Terminator = new BranchTerminator(deferBlock);
            _currentBlock = deferBlock;
            BuildStatements(_deferBodies[i].Body);
        }
    }

    private void BuildRaise(RaiseStatement stmt)
    {
        AddStatement(stmt);

        if (stmt.Exception == null)
        {
            // Bare 'raise' - re-raises current exception
            // Only valid inside an except handler (validated by ControlFlowValidator)
            _currentBlock.Terminator = new RethrowTerminator
            {
                SourceStatement = stmt
            };
        }
        else
        {
            // raise Exception() or raise Exception() from cause
            _currentBlock.Terminator = new ThrowTerminator(stmt.Exception)
            {
                SourceStatement = stmt
            };
        }
        // Note: Exception flow modeling is simplified. Throw terminates the block
        // but doesn't connect to handlers. Full exception flow can be added later.
    }

    private void BuildBreak(BreakStatement stmt)
    {
        AddStatement(stmt);

        if (_loopStack.Count == 0)
        {
            // Error: break outside loop - use BreakTerminator with null target
            // so ControlFlowValidator can detect and report the error
            _currentBlock.Terminator = new BreakTerminator(null!)
            {
                SourceStatement = stmt
            };
            return;
        }

        var loop = _loopStack.Peek();
        Connect(_currentBlock, loop.Exit);
        _currentBlock.Terminator = new BreakTerminator(loop.Exit)
        {
            SourceStatement = stmt
        };
    }

    private void BuildBreakWithFlag(BreakWithFlagStatement stmt)
    {
        // BreakWithFlagStatement is generated internally for loop-else support.
        // It sets a flag to false before breaking, so the else clause knows not to run.
        // For CFG purposes, we treat it the same as a regular break.

        AddStatement(stmt);

        if (_loopStack.Count == 0)
        {
            // Shouldn't happen with internally generated statements, but handle it
            _currentBlock.Terminator = new BreakTerminator(null!)
            {
                SourceStatement = stmt
            };
            return;
        }

        var loop = _loopStack.Peek();
        Connect(_currentBlock, loop.Exit);
        _currentBlock.Terminator = new BreakTerminator(loop.Exit)
        {
            SourceStatement = stmt
        };
    }

    private void BuildContinue(ContinueStatement stmt)
    {
        AddStatement(stmt);

        if (_loopStack.Count == 0)
        {
            // Error: continue outside loop - use ContinueTerminator with null target
            // so ControlFlowValidator can detect and report the error
            _currentBlock.Terminator = new ContinueTerminator(null!)
            {
                SourceStatement = stmt
            };
            return;
        }

        var loop = _loopStack.Peek();
        Connect(_currentBlock, loop.Header);
        _currentBlock.Terminator = new ContinueTerminator(loop.Header)
        {
            SourceStatement = stmt
        };
    }

    private void BuildIf(IfStatement stmt)
    {
        var mergeBlock = CreateBlock("if_merge");

        // Collect all branches: if + elifs + else
        var branches = new List<(Expression? condition, IReadOnlyList<Statement> body, string label)>
        {
            (stmt.Test, stmt.ThenBody, "if_then")
        };

        for (int i = 0; i < stmt.ElifClauses.Length; i++)
        {
            var elif = stmt.ElifClauses[i];
            branches.Add((elif.Test, elif.Body, $"elif_{i}"));
        }

        if (stmt.ElseBody.Length > 0)
        {
            branches.Add((null, stmt.ElseBody, "if_else")); // null condition = unconditional else
        }

        // Process each branch
        var currentCondBlock = _currentBlock;

        for (int i = 0; i < branches.Count; i++)
        {
            var (condition, body, label) = branches[i];
            var isLast = i == branches.Count - 1;
            var hasElse = stmt.ElseBody.Length > 0;

            if (condition == null)
            {
                // This is the else branch - just build the body
                _currentBlock = currentCondBlock;
                BuildStatements(body);

                if (_currentBlock.Terminator == null)
                {
                    Connect(_currentBlock, mergeBlock);
                    _currentBlock.Terminator = new BranchTerminator(mergeBlock);
                }
            }
            else
            {
                // Create block for the body
                var bodyBlock = CreateBlock(label);

                // Determine false target
                BasicBlock falseTarget;
                if (isLast && !hasElse)
                {
                    // Last condition with no else - false goes to merge
                    falseTarget = mergeBlock;
                }
                else if (isLast && hasElse)
                {
                    // Last condition before else - false goes to else block
                    falseTarget = CreateBlock("if_else_entry");
                }
                else
                {
                    // More conditions follow - false goes to next condition block
                    falseTarget = CreateBlock($"elif_{i}_cond");
                }

                // Set up conditional branch
                Connect(currentCondBlock, bodyBlock);
                Connect(currentCondBlock, falseTarget);
                currentCondBlock.Terminator = new ConditionalBranchTerminator(condition, bodyBlock, falseTarget);

                // Build body
                _currentBlock = bodyBlock;
                BuildStatements(body);

                if (_currentBlock.Terminator == null)
                {
                    Connect(_currentBlock, mergeBlock);
                    _currentBlock.Terminator = new BranchTerminator(mergeBlock);
                }

                // Move to next condition block if there are more branches
                currentCondBlock = falseTarget;
            }
        }

        // If no else clause, the last false target was merge, which is correct
        // If there was an else clause, we've already processed it above

        _currentBlock = mergeBlock;
    }

    private void BuildWhile(WhileStatement stmt)
    {
        var headerBlock = CreateBlock("while_header");
        var bodyBlock = CreateBlock("while_body");
        var exitBlock = CreateBlock("while_exit");

        // Connect current block to header
        Connect(_currentBlock, headerBlock);
        _currentBlock.Terminator = new BranchTerminator(headerBlock);

        BasicBlock loopExitTarget;
        BasicBlock? elseBlock = null;

        if (stmt.ElseBody.Length > 0)
        {
            // With else clause: normal exit goes to else block, break goes to exit
            elseBlock = CreateBlock("while_else");
            loopExitTarget = elseBlock;
        }
        else
        {
            // No else clause: normal exit goes directly to exit
            loopExitTarget = exitBlock;
        }

        // Header: condition check
        // True → body, False → else (if present) or exit
        Connect(headerBlock, bodyBlock);
        Connect(headerBlock, loopExitTarget);
        headerBlock.Terminator = new ConditionalBranchTerminator(stmt.Test, bodyBlock, loopExitTarget);

        // Push loop context for break/continue
        // break always goes to exitBlock (bypassing else), continue goes to header
        _loopStack.Push(new LoopContext(headerBlock, exitBlock, elseBlock));

        // Build body
        _currentBlock = bodyBlock;
        BuildStatements(stmt.Body);

        // Connect body back to header (if not terminated by break/return/etc.)
        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, headerBlock);
            _currentBlock.Terminator = new BranchTerminator(headerBlock);
        }

        _loopStack.Pop();

        // Build else clause if present
        if (elseBlock != null)
        {
            _currentBlock = elseBlock;
            BuildStatements(stmt.ElseBody);

            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, exitBlock);
                _currentBlock.Terminator = new BranchTerminator(exitBlock);
            }
        }

        _currentBlock = exitBlock;
    }

    private void BuildFor(ForStatement stmt)
    {
        // For loops iterate over a collection: for x in items: body
        // The CFG models this as: header (has next?) → body → back to header
        // We use the Iterator expression as a placeholder for the "has next" condition

        var headerBlock = CreateBlock("for_header");
        var bodyBlock = CreateBlock("for_body");
        var exitBlock = CreateBlock("for_exit");

        // The loop variable(s) are rebound on every entry to the body, so narrowing facts about them
        // must not survive into the loop (#1042). The binder is block-scoped, so the exit and else
        // blocks restore the pre-loop state for those names (#1635).
        bodyBlock.EntryRebinds = CollectBindingKeys(stmt.Target);
        exitBlock.RebindScopeEntry = bodyBlock;

        // Connect current block to header
        Connect(_currentBlock, headerBlock);
        _currentBlock.Terminator = new BranchTerminator(headerBlock);

        BasicBlock loopExitTarget;
        BasicBlock? elseBlock = null;

        if (stmt.ElseBody.Length > 0)
        {
            // With else clause: normal exit goes to else block, break goes to exit
            elseBlock = CreateBlock("for_else");
            elseBlock.RebindScopeEntry = bodyBlock;
            loopExitTarget = elseBlock;
        }
        else
        {
            // No else clause: normal exit goes directly to exit
            loopExitTarget = exitBlock;
        }

        // Header: "iterator has more items?" check
        // Note: We use stmt.Iterator as the condition expression.
        // This is a simplification - actual iteration semantics are handled at code gen.
        Connect(headerBlock, bodyBlock);
        Connect(headerBlock, loopExitTarget);
        headerBlock.Terminator = new ConditionalBranchTerminator(stmt.Iterator, bodyBlock, loopExitTarget);

        // Push loop context for break/continue
        _loopStack.Push(new LoopContext(headerBlock, exitBlock, elseBlock));

        // Build body
        _currentBlock = bodyBlock;
        BuildStatements(stmt.Body);

        // Connect body back to header
        if (_currentBlock.Terminator == null)
        {
            Connect(_currentBlock, headerBlock);
            _currentBlock.Terminator = new BranchTerminator(headerBlock);
        }

        _loopStack.Pop();

        // Build else clause if present
        if (elseBlock != null)
        {
            _currentBlock = elseBlock;
            BuildStatements(stmt.ElseBody);

            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, exitBlock);
                _currentBlock.Terminator = new BranchTerminator(exitBlock);
            }
        }

        _currentBlock = exitBlock;
    }

    private void BuildTry(TryStatement stmt)
    {
        // Structure:
        // - try body: executes normally, may throw
        // - handlers: catch exceptions (simplified - we don't model which catches what)
        // - else: runs if try completes without exception
        // - finally: always runs before exit

        var tryBlock = CreateBlock("try_body");
        var mergeBlock = CreateBlock("try_merge");

        // Connect to try block
        Connect(_currentBlock, tryBlock);
        _currentBlock.Terminator = new BranchTerminator(tryBlock);

        // Build try body
        _currentBlock = tryBlock;
        BuildStatements(stmt.Body);
        var tryExitBlock = _currentBlock;

        // Build handlers
        var handlerExitBlocks = new List<BasicBlock>();
        foreach (var handler in stmt.Handlers)
        {
            var typeName = handler.ExceptionType switch
            {
                TypeAnnotation ta => ta.Name,
                _ => "all"
            };
            var handlerBlock = CreateBlock($"except_{typeName}");

            // Exception edge: handler is reachable from try body via exception.
            // Uses ConnectException so dataflow analyses can distinguish exception
            // predecessors from normal predecessors (conservative must-assign).
            ConnectException(tryBlock, handlerBlock);

            if (!string.IsNullOrEmpty(handler.Name))
                handlerBlock.EntryRebinds = new[] { handler.Name };

            // Push handler context for bare raise
            _handlerStack.Push(handlerBlock);

            _currentBlock = handlerBlock;
            BuildStatements(handler.Body);
            handlerExitBlocks.Add(_currentBlock);

            _handlerStack.Pop();
        }

        // Build else body (runs if try completes without exception)
        BasicBlock? elseExitBlock = null;
        if (stmt.ElseBody.Length > 0)
        {
            var elseBlock = CreateBlock("try_else");

            // else runs only if try completed normally (no exception)
            if (tryExitBlock.Terminator == null)
            {
                Connect(tryExitBlock, elseBlock);
                tryExitBlock.Terminator = new BranchTerminator(elseBlock);
            }

            _currentBlock = elseBlock;
            BuildStatements(stmt.ElseBody);
            elseExitBlock = _currentBlock;
        }

        // Build finally body (always runs)
        if (stmt.FinallyBody.Length > 0)
        {
            var finallyBlock = CreateBlock("finally");

            // Normal path: try (or else) → finally
            var normalExit = elseExitBlock ?? tryExitBlock;
            if (normalExit.Terminator == null)
            {
                Connect(normalExit, finallyBlock);
                normalExit.Terminator = new BranchTerminator(finallyBlock);
            }

            // Handler paths: each handler → finally
            foreach (var handlerExit in handlerExitBlocks)
            {
                if (handlerExit.Terminator == null)
                {
                    Connect(handlerExit, finallyBlock);
                    handlerExit.Terminator = new BranchTerminator(finallyBlock);
                }
            }

            _currentBlock = finallyBlock;
            BuildStatements(stmt.FinallyBody);

            // finally → merge
            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, mergeBlock);
                _currentBlock.Terminator = new BranchTerminator(mergeBlock);
            }
        }
        else
        {
            // No finally: connect normal path to merge
            var normalExit = elseExitBlock ?? tryExitBlock;
            if (normalExit.Terminator == null)
            {
                Connect(normalExit, mergeBlock);
                normalExit.Terminator = new BranchTerminator(mergeBlock);
            }

            // Connect handler paths to merge
            foreach (var handlerExit in handlerExitBlocks)
            {
                if (handlerExit.Terminator == null)
                {
                    Connect(handlerExit, mergeBlock);
                    handlerExit.Terminator = new BranchTerminator(mergeBlock);
                }
            }
        }

        _currentBlock = mergeBlock;
    }

    private void BuildWith(WithStatement stmt)
    {
        // `with assert_raises(E):` (unittest) compiles to a try/catch, so an exception raised
        // anywhere in the body is caught and control always continues after the block. Model it
        // like a try with a catch-all handler so statements after the block are not reported as
        // unreachable when the body unconditionally raises. Not gated on @test: as of #1413 the
        // lowering fires in every function.
        if (IsAssertRaisesWith(stmt))
        {
            var bodyBlock = CreateBlock("assert_raises_body");
            Connect(_currentBlock, bodyBlock);
            _currentBlock.Terminator = new BranchTerminator(bodyBlock);

            _currentBlock = bodyBlock;
            BuildStatements(stmt.Body);

            var mergeBlock = CreateBlock("assert_raises_merge");

            // Exception edge: the raised exception is caught by Assert.Throws,
            // so the continuation is reachable from the body entry.
            ConnectException(bodyBlock, mergeBlock);

            // Normal exit path (body may also complete without raising —
            // Assert.Throws then fails the test at runtime, but for CFG
            // purposes control still flows to the continuation).
            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, mergeBlock);
                _currentBlock.Terminator = new BranchTerminator(mergeBlock);
            }

            _currentBlock = mergeBlock;
            return;
        }

        // With statement is a straight-through block (like try without handlers). The body executes
        // linearly; disposal happens at the end. When there is an `as` binding, the body runs in a
        // dedicated block so the narrowing analysis can kill facts about the rebound name on entry
        // (#1042); without a binding no extra block is created, so ordinary `with` CFGs are unchanged.
        foreach (var item in stmt.Items)
            _currentBlock.Expressions.Add(item.ContextExpression);

        var withBindings = CollectWithBindingKeys(stmt);
        if (withBindings.Count == 0)
        {
            BuildStatements(stmt.Body);
            return;
        }

        var withBodyBlock = CreateBlock("with_body");
        withBodyBlock.EntryRebinds = withBindings;
        Connect(_currentBlock, withBodyBlock);
        _currentBlock.Terminator = new BranchTerminator(withBodyBlock);
        _currentBlock = withBodyBlock;

        BuildStatements(stmt.Body);

        // The `as` binder is block-scoped (#1647): the statements after the `with` run in a block
        // that restores the pre-binding state for those names, so a must-assign analysis neither
        // credits the binder to an outer bare local of the same name nor loses an outer local that
        // was assigned before the statement (#1635).
        if (_currentBlock.Terminator == null)
        {
            var withExitBlock = CreateBlock("with_exit");
            withExitBlock.RebindScopeEntry = withBodyBlock;
            Connect(_currentBlock, withExitBlock);
            _currentBlock.Terminator = new BranchTerminator(withExitBlock);
            _currentBlock = withExitBlock;
        }
    }

    /// <summary>
    /// Collects the narrowing keys bound by a <c>for</c>-loop target — a single name, a subscript/
    /// member path, or a tuple of these (unpacking) — so the narrowing analysis can kill facts about
    /// them at the loop body's entry. Targets that cannot be keyed contribute nothing.
    /// </summary>
    private static IReadOnlyList<string> CollectBindingKeys(Expression target)
    {
        var keys = new List<string>();
        CollectBindingKeysInto(target, keys);
        return keys;
    }

    private static void CollectBindingKeysInto(Expression target, List<string> keys)
    {
        switch (target)
        {
            case Parenthesized paren:
                CollectBindingKeysInto(paren.Expression, keys);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectBindingKeysInto(element, keys);
                break;
            default:
                var key = AstHelper.ExtractNarrowingKey(target);
                if (key != null)
                    keys.Add(key);
                break;
        }
    }

    private static IReadOnlyList<string> CollectPatternBindingKeys(Pattern pattern)
    {
        var keys = new List<string>();
        CollectPatternBindingKeysInto(pattern, keys);
        return keys;
    }

    private static void CollectPatternBindingKeysInto(Pattern pattern, List<string> keys)
    {
        switch (pattern)
        {
            case BindingPattern bp:
                keys.Add(bp.Name.Name);
                break;
            case AsPattern ap:
                keys.Add(ap.Name.Name);
                CollectPatternBindingKeysInto(ap.Inner, keys);
                break;
            case OrPattern or:
                foreach (var alt in or.Alternatives)
                    CollectPatternBindingKeysInto(alt, keys);
                break;
            case AndPattern and:
                CollectPatternBindingKeysInto(and.Left, keys);
                CollectPatternBindingKeysInto(and.Right, keys);
                break;
            case TuplePattern tp:
                foreach (var el in tp.Elements)
                    CollectPatternBindingKeysInto(el, keys);
                break;
            case ListPattern lp:
                foreach (var el in lp.Elements)
                    CollectPatternBindingKeysInto(el, keys);
                break;
            case StarPattern sp when sp.Capture != null:
                CollectPatternBindingKeysInto(sp.Capture, keys);
                break;
            case PositionalPattern pp:
                foreach (var el in pp.Elements)
                    CollectPatternBindingKeysInto(el, keys);
                break;
            case PropertyPattern prop:
                foreach (var field in prop.Fields)
                    CollectPatternBindingKeysInto(field.Pattern, keys);
                break;
            case GuardPattern gp:
                CollectPatternBindingKeysInto(gp.Inner, keys);
                break;
            case UnionCasePattern ucp:
                foreach (var fp in ucp.FieldPatterns)
                    CollectPatternBindingKeysInto(fp, keys);
                break;
        }
    }

    /// <summary>
    /// Collects the <c>as</c>-binding names of a <c>with</c> statement's items.
    /// </summary>
    private static IReadOnlyList<string> CollectWithBindingKeys(WithStatement stmt)
    {
        List<string>? keys = null;
        foreach (var item in stmt.Items)
        {
            if (item.Name != null)
                (keys ??= new List<string>()).Add(item.Name);
        }
        return (IReadOnlyList<string>?)keys ?? System.Array.Empty<string>();
    }

    /// <summary>
    /// Returns true if this <c>with</c> is the <c>assert_raises</c> form the emitter rewrites into
    /// the framework-neutral flag/try-catch lowering (#1413). Asks the shared authority; the form
    /// is recognised by SPELLING in every context — the old <c>@test</c> gate is gone, so the CFG
    /// models the catch-all edge everywhere the rewrite happens, which is now everywhere.
    /// </summary>
    private bool IsAssertRaisesWith(WithStatement stmt)
        => AssertRaisesForm.IsRewritten(stmt);

    private void BuildMatch(MatchStatement stmt)
    {
        var mergeBlock = CreateBlock("match_merge");
        var condBlock = _currentBlock;

        // The subject is evaluated at the end of this block, after its statements — exactly where an
        // `if` condition is evaluated. Recording it lets the narrowing analysis freeze the block's
        // out-set for the subject, so `if isinstance(o, Box[int]): match o:` sees the narrowed type
        // (#1299). Without it the match statement was untracked and the subject read the pre-branch
        // fact set.
        condBlock.MatchSubject = stmt.Scrutinee;
        condBlock.Expressions.Add(stmt.Scrutinee);

        foreach (var matchCase in stmt.Cases)
        {
            var caseBlock = CreateBlock("match_case");
            Connect(condBlock, caseBlock);
            _currentBlock = caseBlock;

            var captureNames = CollectPatternBindingKeys(matchCase.Pattern);
            if (captureNames.Count > 0)
                caseBlock.EntryRebinds = captureNames;

            if (matchCase.Guard != null)
                caseBlock.Expressions.Add(matchCase.Guard);

            BuildStatements(matchCase.Body);

            if (_currentBlock.Terminator == null)
            {
                Connect(_currentBlock, mergeBlock);
                _currentBlock.Terminator = new BranchTerminator(mergeBlock);
            }
        }

        // Only connect the condition block to merge if the match is not exhaustive.
        // An exhaustive match guarantees one of the cases will always be taken,
        // so there is no "fall-through" path to the merge block.
        bool isExhaustive = stmt.Cases.Any(c =>
            c.Guard == null && IsUnconditionallyExhaustivePattern(c.Pattern))
            || IsSemanticallyExhaustiveMatch(stmt);

        if (!isExhaustive)
        {
            Connect(condBlock, mergeBlock);
        }

        _currentBlock = mergeBlock;
    }

    /// <summary>
    /// Checks whether a pattern unconditionally matches all values.
    /// Recurses into OrPattern alternatives.
    /// </summary>
    private bool IsUnconditionallyExhaustivePattern(Pattern pattern)
    {
        return ExhaustivenessHelper.IsIrrefutable(pattern, _semanticInfo);
    }

    /// <summary>
    /// Checks whether a match statement is semantically exhaustive.
    /// Uses the precomputed set of exhaustive match statements passed to the constructor.
    /// </summary>
    private bool IsSemanticallyExhaustiveMatch(MatchStatement stmt)
    {
        return _exhaustiveMatches?.Contains(stmt) == true;
    }
}
