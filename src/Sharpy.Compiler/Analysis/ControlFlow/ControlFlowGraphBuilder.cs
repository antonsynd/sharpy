using Sharpy.Compiler.Parser.Ast;

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

    // Loop tracking for break/continue
    private readonly Stack<LoopContext> _loopStack = new();

    // Exception handler tracking for re-raise
    private readonly Stack<BasicBlock> _handlerStack = new();

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

            case MatchStatement matchStmt:
                BuildMatch(matchStmt);
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

        // Connect current block to header
        Connect(_currentBlock, headerBlock);
        _currentBlock.Terminator = new BranchTerminator(headerBlock);

        BasicBlock loopExitTarget;
        BasicBlock? elseBlock = null;

        if (stmt.ElseBody.Length > 0)
        {
            // With else clause: normal exit goes to else block, break goes to exit
            elseBlock = CreateBlock("for_else");
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

            // Simplified: all handlers are reachable from try body (exception edges)
            // We don't model which specific exceptions go to which handlers
            Connect(tryBlock, handlerBlock);

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
        // With statement is a straight-through block (like try without handlers).
        // The body executes linearly; disposal happens at the end.
        BuildStatements(stmt.Body);
    }

    private void BuildMatch(MatchStatement stmt)
    {
        var mergeBlock = CreateBlock("match_merge");
        var condBlock = _currentBlock;

        foreach (var matchCase in stmt.Cases)
        {
            var caseBlock = CreateBlock("match_case");
            Connect(condBlock, caseBlock);
            _currentBlock = caseBlock;

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
            c.Guard == null && IsUnconditionallyExhaustivePattern(c.Pattern));

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
    private static bool IsUnconditionallyExhaustivePattern(Pattern pattern)
    {
        return pattern switch
        {
            WildcardPattern => true,
            BindingPattern => true,
            // If ANY alternative is exhaustive, the OrPattern as a whole can match everything
            OrPattern orPat => orPat.Alternatives.Any(IsUnconditionallyExhaustivePattern),
            // GuardPattern is forward-declared; guard conditions use MatchCase.Guard instead
            GuardPattern => false,
            _ => false
        };
    }
}
