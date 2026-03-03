using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Warns about local variables that are assigned but never read.
/// Skips parameters, underscore-prefixed variables, and loop variables.
/// </summary>
internal class UnusedVariableValidator : SemanticValidatorBase
{
    public override string Name => "UnusedVariableValidator";
    public override int Order => 420;

    private SemanticContext _context = null!;
    // Per-function state for tracking definitions from within expressions (walrus operator)
    private Dictionary<string, VariableInfo> _currentDefined = null!;
    private HashSet<string> _currentParameters = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;

        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef func)
                ValidateFunction(func);
            else if (stmt is ClassDef cls)
                ValidateClass(cls);
            else if (stmt is StructDef str)
                ValidateStruct(str);
        }
    }

    private void ValidateClass(ClassDef cls)
    {
        foreach (var member in cls.Body)
        {
            if (member is FunctionDef method)
                ValidateFunction(method);
        }
    }

    private void ValidateStruct(StructDef str)
    {
        foreach (var member in str.Body)
        {
            if (member is FunctionDef method)
                ValidateFunction(method);
        }
    }

    private void ValidateFunction(FunctionDef func)
    {
        // Skip abstract methods and stub bodies
        if (func.Decorators.Any(d => d.Name == DecoratorNames.Abstract))
            return;
        if (func.Body.Length == 1 && func.Body[0] is ExpressionStatement { Expression: EllipsisLiteral })
            return;

        var defined = new Dictionary<string, VariableInfo>();
        var read = new HashSet<string>();

        // Parameters are defined but should not be warned about
        var parameters = new HashSet<string>();
        foreach (var param in func.Parameters)
        {
            parameters.Add(param.Name);
        }

        // Save and restore per-function instance state for walrus operator tracking.
        // This is necessary because ValidateFunction is called recursively for nested
        // functions, and without save/restore the instance fields would point to the
        // nested function's data after the recursive call returns.
        var savedDefined = _currentDefined;
        var savedParameters = _currentParameters;
        _currentDefined = defined;
        _currentParameters = parameters;

        // Collect definitions and reads from the function body
        foreach (var stmt in func.Body)
        {
            CollectFromStatement(stmt, defined, read, parameters);
        }

        // Restore outer scope state
        _currentDefined = savedDefined;
        _currentParameters = savedParameters;

        // Emit warnings for unused variables
        foreach (var (name, info) in defined)
        {
            if (read.Contains(name))
                continue;

            // Skip underscore-prefixed (intentionally unused convention)
            if (name.StartsWith('_'))
                continue;

            // Skip parameters
            if (parameters.Contains(name))
                continue;

            // Skip loop variables
            if (info.IsLoopVariable)
                continue;

            AddWarning(_context,
                $"Local variable '{name}' is assigned but never used",
                info.Line, info.Column,
                code: DiagnosticCodes.Validation.UnusedVariable,
                span: info.Span);
        }
    }

    private void CollectFromStatement(Statement stmt, Dictionary<string, VariableInfo> defined,
        HashSet<string> read, HashSet<string> parameters)
    {
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                if (!parameters.Contains(varDecl.Name))
                {
                    defined[varDecl.Name] = new VariableInfo(
                        varDecl.LineStart, varDecl.ColumnStart, varDecl.Span, false);
                }
                if (varDecl.InitialValue != null)
                    CollectReadsFromExpression(varDecl.InitialValue, read);
                break;

            case Assignment assign:
                // Collect reads from the value side first
                CollectReadsFromExpression(assign.Value, read);

                if (assign.Target is Identifier targetId)
                {
                    if (assign.Operator == AssignmentOperator.Assign)
                    {
                        // Simple assignment defines a variable
                        if (!parameters.Contains(targetId.Name))
                        {
                            defined[targetId.Name] = new VariableInfo(
                                assign.LineStart, assign.ColumnStart, assign.Span, false);
                        }
                    }
                    else
                    {
                        // Augmented assignment (+=, -=, etc.) reads the variable too
                        read.Add(targetId.Name);
                    }
                }
                else if (assign.Target is TupleLiteral tuple)
                {
                    // Tuple unpacking - each element is a definition
                    foreach (var elem in tuple.Elements)
                    {
                        if (elem is Identifier tupleId && !parameters.Contains(tupleId.Name))
                        {
                            defined[tupleId.Name] = new VariableInfo(
                                assign.LineStart, assign.ColumnStart, elem.Span, false);
                        }
                    }
                }
                else
                {
                    // Member access, index, etc. - reads the target
                    CollectReadsFromExpression(assign.Target, read);
                }
                break;

            case ReturnStatement ret:
                if (ret.Value != null)
                    CollectReadsFromExpression(ret.Value, read);
                break;

            case ExpressionStatement exprStmt:
                CollectReadsFromExpression(exprStmt.Expression, read);
                break;

            case IfStatement ifStmt:
                CollectReadsFromExpression(ifStmt.Test, read);
                foreach (var s in ifStmt.ThenBody)
                    CollectFromStatement(s, defined, read, parameters);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    CollectReadsFromExpression(elif.Test, read);
                    foreach (var s in elif.Body)
                        CollectFromStatement(s, defined, read, parameters);
                }
                foreach (var s in ifStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters);
                break;

            case WhileStatement whileStmt:
                CollectReadsFromExpression(whileStmt.Test, read);
                foreach (var s in whileStmt.Body)
                    CollectFromStatement(s, defined, read, parameters);
                foreach (var s in whileStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters);
                break;

            case ForStatement forStmt:
                CollectReadsFromExpression(forStmt.Iterator, read);
                // Loop variable is defined but marked as loop variable (skip warning)
                CollectForTarget(forStmt.Target, defined, parameters, isLoopVariable: true);
                foreach (var s in forStmt.Body)
                    CollectFromStatement(s, defined, read, parameters);
                foreach (var s in forStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters);
                break;

            case TryStatement tryStmt:
                foreach (var s in tryStmt.Body)
                    CollectFromStatement(s, defined, read, parameters);
                foreach (var handler in tryStmt.Handlers)
                {
                    if (handler.Name != null)
                    {
                        // except Exception as e: defines variable 'e'
                        defined[handler.Name] = new VariableInfo(
                            handler.LineStart, handler.ColumnStart, handler.Span, false);
                    }
                    foreach (var s in handler.Body)
                        CollectFromStatement(s, defined, read, parameters);
                }
                foreach (var s in tryStmt.ElseBody)
                    CollectFromStatement(s, defined, read, parameters);
                foreach (var s in tryStmt.FinallyBody)
                    CollectFromStatement(s, defined, read, parameters);
                break;

            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                {
                    CollectReadsFromExpression(item.ContextExpression, read);
                    if (item.Name != null)
                    {
                        defined[item.Name] = new VariableInfo(
                            item.LineStart, item.ColumnStart, item.Span, false);
                    }
                }
                foreach (var s in withStmt.Body)
                    CollectFromStatement(s, defined, read, parameters);
                break;

            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    CollectReadsFromExpression(raiseStmt.Exception, read);
                break;

            case AssertStatement assertStmt:
                CollectReadsFromExpression(assertStmt.Test, read);
                if (assertStmt.Message != null)
                    CollectReadsFromExpression(assertStmt.Message, read);
                break;

            case FunctionDef nestedFunc:
                // Validate the nested function independently for its own unused variables
                ValidateFunction(nestedFunc);
                // Scan the nested function's body for reads of enclosing scope variables (closures)
                CollectReadsFromNestedFunction(nestedFunc, read);
                break;

            case MatchStatement matchStmt:
                CollectReadsFromExpression(matchStmt.Scrutinee, read);
                foreach (var matchCase in matchStmt.Cases)
                {
                    CollectDefinitionsFromPattern(matchCase.Pattern, defined, parameters);
                    if (matchCase.Guard != null)
                        CollectReadsFromExpression(matchCase.Guard, read);
                    foreach (var s in matchCase.Body)
                        CollectFromStatement(s, defined, read, parameters);
                }
                break;

            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
            case PropertyDef:
            case EventDef:
                // These define their own scope and are validated at the top level
                break;
        }
    }

    private void CollectDefinitionsFromPattern(Pattern pattern,
        Dictionary<string, VariableInfo> defined, HashSet<string> parameters)
    {
        switch (pattern)
        {
            case BindingPattern binding:
                if (!parameters.Contains(binding.Name.Name))
                {
                    defined[binding.Name.Name] = new VariableInfo(
                        binding.LineStart, binding.ColumnStart, binding.Span, false);
                }
                break;

            case TypePattern typePattern:
                if (typePattern.BindingName != null && !parameters.Contains(typePattern.BindingName.Name))
                {
                    defined[typePattern.BindingName.Name] = new VariableInfo(
                        typePattern.LineStart, typePattern.ColumnStart, typePattern.Span, false);
                }
                break;

            case OrPattern orPattern:
                foreach (var alt in orPattern.Alternatives)
                    CollectDefinitionsFromPattern(alt, defined, parameters);
                break;

            case TuplePattern tuplePattern:
                foreach (var elem in tuplePattern.Elements)
                    CollectDefinitionsFromPattern(elem, defined, parameters);
                break;

            case PropertyPattern propertyPattern:
                foreach (var field in propertyPattern.Fields)
                    CollectDefinitionsFromPattern(field.Pattern, defined, parameters);
                break;

            case PositionalPattern positionalPattern:
                foreach (var elem in positionalPattern.Elements)
                    CollectDefinitionsFromPattern(elem, defined, parameters);
                break;
        }
    }

    private void CollectForTarget(Expression target, Dictionary<string, VariableInfo> defined,
        HashSet<string> parameters, bool isLoopVariable)
    {
        if (target is Identifier id && !parameters.Contains(id.Name))
        {
            defined[id.Name] = new VariableInfo(
                id.LineStart, id.ColumnStart, id.Span, isLoopVariable);
        }
        else if (target is TupleLiteral tuple)
        {
            foreach (var elem in tuple.Elements)
                CollectForTarget(elem, defined, parameters, isLoopVariable);
        }
    }

    /// <summary>
    /// Scan a nested function for identifier reads that may reference
    /// variables from an enclosing scope (closure captures).
    /// Includes parameter default values and decorator names, which are
    /// evaluated in the enclosing scope, not the nested function's scope.
    /// </summary>
    private void CollectReadsFromNestedFunction(FunctionDef func, HashSet<string> outerRead)
    {
        // Parameter default values are evaluated in the enclosing scope
        foreach (var param in func.Parameters)
        {
            if (param.DefaultValue != null)
                CollectReadsFromExpression(param.DefaultValue, outerRead);
        }

        // Decorator names may reference enclosing scope variables
        foreach (var decorator in func.Decorators)
            outerRead.Add(decorator.Name);

        foreach (var stmt in func.Body)
        {
            CollectReadsFromStatement(stmt, outerRead);
        }
    }

    /// <summary>
    /// Recursively collect all identifier reads from a statement, without tracking definitions.
    /// Used for scanning nested function bodies to detect closure captures.
    /// </summary>
    private void CollectReadsFromStatement(Statement stmt, HashSet<string> read)
    {
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                if (varDecl.InitialValue != null)
                    CollectReadsFromExpression(varDecl.InitialValue, read);
                break;

            case Assignment assign:
                CollectReadsFromExpression(assign.Value, read);
                if (assign.Target is Identifier targetId && assign.Operator != AssignmentOperator.Assign)
                    read.Add(targetId.Name);
                else if (assign.Target is not Identifier)
                    CollectReadsFromExpression(assign.Target, read);
                break;

            case ReturnStatement ret:
                if (ret.Value != null)
                    CollectReadsFromExpression(ret.Value, read);
                break;

            case ExpressionStatement exprStmt:
                CollectReadsFromExpression(exprStmt.Expression, read);
                break;

            case IfStatement ifStmt:
                CollectReadsFromExpression(ifStmt.Test, read);
                foreach (var s in ifStmt.ThenBody)
                    CollectReadsFromStatement(s, read);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    CollectReadsFromExpression(elif.Test, read);
                    foreach (var s in elif.Body)
                        CollectReadsFromStatement(s, read);
                }
                foreach (var s in ifStmt.ElseBody)
                    CollectReadsFromStatement(s, read);
                break;

            case WhileStatement whileStmt:
                CollectReadsFromExpression(whileStmt.Test, read);
                foreach (var s in whileStmt.Body)
                    CollectReadsFromStatement(s, read);
                foreach (var s in whileStmt.ElseBody)
                    CollectReadsFromStatement(s, read);
                break;

            case ForStatement forStmt:
                CollectReadsFromExpression(forStmt.Iterator, read);
                foreach (var s in forStmt.Body)
                    CollectReadsFromStatement(s, read);
                foreach (var s in forStmt.ElseBody)
                    CollectReadsFromStatement(s, read);
                break;

            case TryStatement tryStmt:
                foreach (var s in tryStmt.Body)
                    CollectReadsFromStatement(s, read);
                foreach (var handler in tryStmt.Handlers)
                {
                    foreach (var s in handler.Body)
                        CollectReadsFromStatement(s, read);
                }
                foreach (var s in tryStmt.ElseBody)
                    CollectReadsFromStatement(s, read);
                foreach (var s in tryStmt.FinallyBody)
                    CollectReadsFromStatement(s, read);
                break;

            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                    CollectReadsFromExpression(item.ContextExpression, read);
                foreach (var s in withStmt.Body)
                    CollectReadsFromStatement(s, read);
                break;

            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    CollectReadsFromExpression(raiseStmt.Exception, read);
                break;

            case AssertStatement assertStmt:
                CollectReadsFromExpression(assertStmt.Test, read);
                if (assertStmt.Message != null)
                    CollectReadsFromExpression(assertStmt.Message, read);
                break;

            case MatchStatement matchStmt:
                CollectReadsFromExpression(matchStmt.Scrutinee, read);
                foreach (var matchCase in matchStmt.Cases)
                {
                    if (matchCase.Guard != null)
                        CollectReadsFromExpression(matchCase.Guard, read);
                    foreach (var s in matchCase.Body)
                        CollectReadsFromStatement(s, read);
                }
                break;

            case FunctionDef nestedFunc:
                // Continue scanning deeper nested functions for closure reads
                CollectReadsFromNestedFunction(nestedFunc, read);
                break;
        }
    }

    private void CollectReadsFromExpression(Expression expr, HashSet<string> read)
    {
        switch (expr)
        {
            case Identifier id:
                read.Add(id.Name);
                break;

            case BinaryOp binOp:
                CollectReadsFromExpression(binOp.Left, read);
                CollectReadsFromExpression(binOp.Right, read);
                break;

            case UnaryOp unaryOp:
                CollectReadsFromExpression(unaryOp.Operand, read);
                break;

            case FunctionCall call:
                CollectReadsFromExpression(call.Function, read);
                foreach (var arg in call.Arguments)
                    CollectReadsFromExpression(arg, read);
                foreach (var kwarg in call.KeywordArguments)
                    CollectReadsFromExpression(kwarg.Value, read);
                break;

            case MemberAccess memberAccess:
                CollectReadsFromExpression(memberAccess.Object, read);
                break;

            case IndexAccess indexAccess:
                CollectReadsFromExpression(indexAccess.Object, read);
                CollectReadsFromExpression(indexAccess.Index, read);
                break;

            case SliceAccess sliceAccess:
                CollectReadsFromExpression(sliceAccess.Object, read);
                if (sliceAccess.Start != null)
                    CollectReadsFromExpression(sliceAccess.Start, read);
                if (sliceAccess.Stop != null)
                    CollectReadsFromExpression(sliceAccess.Stop, read);
                if (sliceAccess.Step != null)
                    CollectReadsFromExpression(sliceAccess.Step, read);
                break;

            case ListLiteral listLit:
                foreach (var elem in listLit.Elements)
                    CollectReadsFromExpression(elem, read);
                break;

            case DictLiteral dictLit:
                foreach (var entry in dictLit.Entries)
                {
                    if (entry.Key != null)
                        CollectReadsFromExpression(entry.Key, read);
                    CollectReadsFromExpression(entry.Value, read);
                }
                break;

            case SetLiteral setLit:
                foreach (var elem in setLit.Elements)
                    CollectReadsFromExpression(elem, read);
                break;

            case TupleLiteral tupleLit:
                foreach (var elem in tupleLit.Elements)
                    CollectReadsFromExpression(elem, read);
                break;

            case ConditionalExpression condExpr:
                CollectReadsFromExpression(condExpr.Test, read);
                CollectReadsFromExpression(condExpr.ThenValue, read);
                CollectReadsFromExpression(condExpr.ElseValue, read);
                break;

            case FStringLiteral fStr:
                foreach (var part in fStr.Parts)
                {
                    if (part.Expression != null)
                        CollectReadsFromExpression(part.Expression, read);
                }
                break;

            case ListComprehension listComp:
                CollectReadsFromComprehension(listComp.Element, listComp.Clauses, read);
                break;

            case SetComprehension setComp:
                CollectReadsFromComprehension(setComp.Element, setComp.Clauses, read);
                break;

            case DictComprehension dictComp:
                CollectReadsFromExpression(dictComp.Key, read);
                CollectReadsFromExpression(dictComp.Value, read);
                foreach (var clause in dictComp.Clauses)
                {
                    if (clause is ForClause forClause)
                        CollectReadsFromExpression(forClause.Iterator, read);
                    else if (clause is IfClause ifClause)
                        CollectReadsFromExpression(ifClause.Condition, read);
                }
                break;

            case LambdaExpression lambda:
                CollectReadsFromExpression(lambda.Body, read);
                break;

            case TypeCast castExpr:
                CollectReadsFromExpression(castExpr.Value, read);
                break;

            case TypeCoercion coercion:
                CollectReadsFromExpression(coercion.Value, read);
                break;

            case TypeCheck typeCheck:
                CollectReadsFromExpression(typeCheck.Value, read);
                break;

            case ComparisonChain chain:
                foreach (var operand in chain.Operands)
                    CollectReadsFromExpression(operand, read);
                break;

            case Parenthesized paren:
                CollectReadsFromExpression(paren.Expression, read);
                break;

            case TryExpression tryExpr:
                CollectReadsFromExpression(tryExpr.Operand, read);
                break;

            case MaybeExpression maybeExpr:
                CollectReadsFromExpression(maybeExpr.Operand, read);
                break;

            case MatchExpression matchExpr:
                CollectReadsFromExpression(matchExpr.Scrutinee, read);
                foreach (var arm in matchExpr.Arms)
                {
                    if (arm.Guard != null)
                        CollectReadsFromExpression(arm.Guard, read);
                    CollectReadsFromExpression(arm.Result, read);
                }
                break;

            case WalrusExpression walrus:
                // Walrus (name := value) defines the target variable
                if (!_currentParameters.Contains(walrus.Target))
                {
                    _currentDefined[walrus.Target] = new VariableInfo(
                        walrus.LineStart, walrus.ColumnStart, walrus.Span, false);
                }
                CollectReadsFromExpression(walrus.Value, read);
                break;

            // Literals and other terminals - no variable reads
            case IntegerLiteral:
            case FloatLiteral:
            case StringLiteral:
            case BooleanLiteral:
            case NoneLiteral:
            case EllipsisLiteral:
            case SuperExpression:
                break;
        }
    }

    private void CollectReadsFromComprehension(Expression element,
        System.Collections.Immutable.ImmutableArray<ComprehensionClause> clauses, HashSet<string> read)
    {
        CollectReadsFromExpression(element, read);
        foreach (var clause in clauses)
        {
            if (clause is ForClause forClause)
                CollectReadsFromExpression(forClause.Iterator, read);
            else if (clause is IfClause ifClause)
                CollectReadsFromExpression(ifClause.Condition, read);
        }
    }

    private record VariableInfo(int? Line, int? Column, Text.TextSpan? Span, bool IsLoopVariable);
}
