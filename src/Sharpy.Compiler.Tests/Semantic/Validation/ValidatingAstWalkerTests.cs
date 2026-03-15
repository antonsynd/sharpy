using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ValidatingAstWalkerTests
{
    private static SemanticContext CreateTestContext()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        return new SemanticContext(symbolTable, semanticInfo, typeResolver);
    }

    #region Test 1: Walker visits all statement types in nested bodies

    [Fact]
    public void Validate_VisitsAllStatementTypesInNestedBodies()
    {
        // Build a module with nested if/while/for/try containing function definitions
        // and variable declarations at various depths.
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = new BooleanLiteral { Value = true },
                    ThenBody = ImmutableArray.Create<Statement>(
                        new VariableDeclaration { Name = "x", InitialValue = new IntegerLiteral { Value = "1" } },
                        new WhileStatement
                        {
                            Test = new BooleanLiteral { Value = true },
                            Body = ImmutableArray.Create<Statement>(
                                new VariableDeclaration { Name = "y", InitialValue = new IntegerLiteral { Value = "2" } },
                                new ForStatement
                                {
                                    Target = new Identifier { Name = "i" },
                                    Iterator = new Identifier { Name = "items" },
                                    Body = ImmutableArray.Create<Statement>(
                                        new PassStatement(),
                                        new TryStatement
                                        {
                                            Body = ImmutableArray.Create<Statement>(
                                                new FunctionDef
                                                {
                                                    Name = "nested_func",
                                                    Body = ImmutableArray.Create<Statement>(
                                                        new ReturnStatement { Value = new IntegerLiteral { Value = "42" } }
                                                    )
                                                }
                                            )
                                        }
                                    )
                                }
                            )
                        }
                    )
                }
            )
        };

        var walker = new NodeTypeRecordingWalker();
        var context = CreateTestContext();
        walker.Validate(module, context);

        // Assert all expected node types were visited
        Assert.Contains(typeof(IfStatement), walker.VisitedNodeTypes);
        Assert.Contains(typeof(WhileStatement), walker.VisitedNodeTypes);
        Assert.Contains(typeof(ForStatement), walker.VisitedNodeTypes);
        Assert.Contains(typeof(TryStatement), walker.VisitedNodeTypes);
        Assert.Contains(typeof(FunctionDef), walker.VisitedNodeTypes);
        Assert.Contains(typeof(VariableDeclaration), walker.VisitedNodeTypes);
        Assert.Contains(typeof(PassStatement), walker.VisitedNodeTypes);
        Assert.Contains(typeof(ReturnStatement), walker.VisitedNodeTypes);
        Assert.Contains(typeof(BooleanLiteral), walker.VisitedNodeTypes);
        Assert.Contains(typeof(IntegerLiteral), walker.VisitedNodeTypes);
        Assert.Contains(typeof(Identifier), walker.VisitedNodeTypes);
    }

    #endregion

    #region Test 2: Overriding VisitXxx receives the correct node

    [Fact]
    public void Validate_OverriddenVisitFunctionDef_ReceivesCorrectNodes()
    {
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "alpha",
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                },
                new FunctionDef
                {
                    Name = "beta",
                    Body = ImmutableArray.Create<Statement>(new PassStatement())
                }
            )
        };

        var walker = new FunctionNameRecordingWalker();
        var context = CreateTestContext();
        walker.Validate(module, context);

        Assert.Equal(2, walker.FunctionNames.Count);
        Assert.Contains("alpha", walker.FunctionNames);
        Assert.Contains("beta", walker.FunctionNames);
    }

    #endregion

    #region Test 3: base.VisitXxx continues traversal into children

    [Fact]
    public void Validate_BaseVisitFunctionDef_ContinuesTraversalIntoChildren()
    {
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "outer",
                    Body = ImmutableArray.Create<Statement>(
                        new VariableDeclaration { Name = "inside_func", InitialValue = new IntegerLiteral { Value = "99" } }
                    )
                }
            )
        };

        var walker = new FunctionAndVarDeclWalker();
        var context = CreateTestContext();
        walker.Validate(module, context);

        // The walker overrides VisitFunctionDef and calls base, so it should
        // also traverse into the function body and find the variable declaration.
        Assert.Single(walker.FunctionNames);
        Assert.Equal("outer", walker.FunctionNames[0]);
        Assert.Single(walker.VarDeclNames);
        Assert.Equal("inside_func", walker.VarDeclNames[0]);
    }

    #endregion

    #region Test 4: Not overriding a method still traverses children

    [Fact]
    public void Validate_UnoverriddenMethod_StillTraversesChildren()
    {
        // The walker only overrides VisitVariableDeclaration, but the variable
        // declarations are nested inside an if body. Since VisitIfStatement is
        // not overridden, DefaultVisit should traverse into the if body.
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = new BooleanLiteral { Value = true },
                    ThenBody = ImmutableArray.Create<Statement>(
                        new VariableDeclaration { Name = "found_inside_if", InitialValue = new IntegerLiteral { Value = "1" } },
                        new WhileStatement
                        {
                            Test = new BooleanLiteral { Value = true },
                            Body = ImmutableArray.Create<Statement>(
                                new VariableDeclaration { Name = "found_inside_while", InitialValue = new IntegerLiteral { Value = "2" } }
                            )
                        }
                    )
                }
            )
        };

        var walker = new VarDeclOnlyWalker();
        var context = CreateTestContext();
        walker.Validate(module, context);

        Assert.Equal(2, walker.VarDeclNames.Count);
        Assert.Contains("found_inside_if", walker.VarDeclNames);
        Assert.Contains("found_inside_while", walker.VarDeclNames);
    }

    #endregion

    #region Test 5: AddError adds diagnostic via Context

    [Fact]
    public void Validate_AddError_AddsDiagnosticToContext()
    {
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "bad_func",
                    Body = ImmutableArray.Create<Statement>(new PassStatement()),
                    LineStart = 5,
                    ColumnStart = 3
                }
            )
        };

        var walker = new ErrorReportingWalker();
        var context = CreateTestContext();
        walker.Validate(module, context);

        var errors = context.Diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("bad_func", errors[0].Message);
        Assert.Equal("SPY9999", errors[0].Code);
        Assert.Equal(5, errors[0].Line);
        Assert.Equal(3, errors[0].Column);
        Assert.Equal(CompilerPhase.Validation, errors[0].Phase);
    }

    #endregion

    #region Test 6: AddWarning adds diagnostic via Context

    [Fact]
    public void Validate_AddWarning_AddsDiagnosticToContext()
    {
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new FunctionDef
                {
                    Name = "suspicious_func",
                    Body = ImmutableArray.Create<Statement>(new PassStatement()),
                    LineStart = 10,
                    ColumnStart = 1
                }
            )
        };

        var walker = new WarningReportingWalker();
        var context = CreateTestContext();
        walker.Validate(module, context);

        var warnings = context.Diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("suspicious_func", warnings[0].Message);
        Assert.Equal("SPY8888", warnings[0].Code);
        Assert.Equal(10, warnings[0].Line);
        Assert.Equal(1, warnings[0].Column);
        Assert.Equal(CompilerPhase.Validation, warnings[0].Phase);
    }

    #endregion

    #region Test walker implementations

    /// <summary>
    /// Records the types of all nodes visited via DefaultVisit.
    /// Overrides DefaultVisit to record the type, then calls base to continue traversal.
    /// </summary>
    private class NodeTypeRecordingWalker : ValidatingAstWalker
    {
        public override string Name => "NodeTypeRecordingWalker";
        public override int Order => 0;

        public HashSet<Type> VisitedNodeTypes { get; } = new();

        public override void DefaultVisit(Node node)
        {
            VisitedNodeTypes.Add(node.GetType());
            base.DefaultVisit(node);
        }

        // Override specific visit methods to record those types too,
        // since they call VisitStatement/VisitExpression -> DefaultVisit,
        // but we want to ensure the exact type is captured before dispatch.
        public override void VisitIfStatement(IfStatement node)
        {
            VisitedNodeTypes.Add(typeof(IfStatement));
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatement node)
        {
            VisitedNodeTypes.Add(typeof(WhileStatement));
            base.VisitWhileStatement(node);
        }

        public override void VisitForStatement(ForStatement node)
        {
            VisitedNodeTypes.Add(typeof(ForStatement));
            base.VisitForStatement(node);
        }

        public override void VisitTryStatement(TryStatement node)
        {
            VisitedNodeTypes.Add(typeof(TryStatement));
            base.VisitTryStatement(node);
        }

        public override void VisitFunctionDef(FunctionDef node)
        {
            VisitedNodeTypes.Add(typeof(FunctionDef));
            base.VisitFunctionDef(node);
        }

        public override void VisitVariableDeclaration(VariableDeclaration node)
        {
            VisitedNodeTypes.Add(typeof(VariableDeclaration));
            base.VisitVariableDeclaration(node);
        }

        public override void VisitPassStatement(PassStatement node)
        {
            VisitedNodeTypes.Add(typeof(PassStatement));
            base.VisitPassStatement(node);
        }

        public override void VisitReturnStatement(ReturnStatement node)
        {
            VisitedNodeTypes.Add(typeof(ReturnStatement));
            base.VisitReturnStatement(node);
        }
    }

    /// <summary>
    /// Records function names from VisitFunctionDef (does NOT call base, stops traversal).
    /// </summary>
    private class FunctionNameRecordingWalker : ValidatingAstWalker
    {
        public override string Name => "FunctionNameRecordingWalker";
        public override int Order => 0;

        public List<string> FunctionNames { get; } = new();

        public override void VisitFunctionDef(FunctionDef node)
        {
            FunctionNames.Add(node.Name);
            // Intentionally not calling base to verify that the correct node is received
        }
    }

    /// <summary>
    /// Overrides VisitFunctionDef (with base call) and VisitVariableDeclaration.
    /// Verifies that calling base.VisitFunctionDef traverses into child nodes.
    /// </summary>
    private class FunctionAndVarDeclWalker : ValidatingAstWalker
    {
        public override string Name => "FunctionAndVarDeclWalker";
        public override int Order => 0;

        public List<string> FunctionNames { get; } = new();
        public List<string> VarDeclNames { get; } = new();

        public override void VisitFunctionDef(FunctionDef node)
        {
            FunctionNames.Add(node.Name);
            base.VisitFunctionDef(node);
        }

        public override void VisitVariableDeclaration(VariableDeclaration node)
        {
            VarDeclNames.Add(node.Name);
            // No need to call base; VariableDeclaration has no statement children
        }
    }

    /// <summary>
    /// Only overrides VisitVariableDeclaration. All other nodes use default traversal.
    /// </summary>
    private class VarDeclOnlyWalker : ValidatingAstWalker
    {
        public override string Name => "VarDeclOnlyWalker";
        public override int Order => 0;

        public List<string> VarDeclNames { get; } = new();

        public override void VisitVariableDeclaration(VariableDeclaration node)
        {
            VarDeclNames.Add(node.Name);
        }
    }

    /// <summary>
    /// Calls AddError when visiting a FunctionDef.
    /// </summary>
    private class ErrorReportingWalker : ValidatingAstWalker
    {
        public override string Name => "ErrorReportingWalker";
        public override int Order => 0;

        public override void VisitFunctionDef(FunctionDef node)
        {
            AddError($"Error in function '{node.Name}'", line: node.LineStart, column: node.ColumnStart, code: "SPY9999");
        }
    }

    /// <summary>
    /// Calls AddWarning when visiting a FunctionDef.
    /// </summary>
    private class WarningReportingWalker : ValidatingAstWalker
    {
        public override string Name => "WarningReportingWalker";
        public override int Order => 0;

        public override void VisitFunctionDef(FunctionDef node)
        {
            AddWarning($"Warning in function '{node.Name}'", line: node.LineStart, column: node.ColumnStart, code: "SPY8888");
        }
    }

    #endregion
}
