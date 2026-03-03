namespace Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Base class for AST visitors that perform void operations.
/// Uses central switch dispatch (not double dispatch with Accept()).
/// Override specific Visit methods to handle nodes of interest.
/// Unoverridden methods default to traversing child nodes via GetChildNodes().
/// </summary>
public abstract class AstVisitor
{
    /// <summary>
    /// Dispatches to the appropriate typed Visit method based on the node's runtime type.
    /// </summary>
    public virtual void Visit(Node node)
    {
        switch (node)
        {
            // Module
            case Module n:
                VisitModule(n);
                break;

            // Expressions - Literals
            case IntegerLiteral n:
                VisitIntegerLiteral(n);
                break;
            case FloatLiteral n:
                VisitFloatLiteral(n);
                break;
            case StringLiteral n:
                VisitStringLiteral(n);
                break;
            case FStringLiteral n:
                VisitFStringLiteral(n);
                break;
            case BooleanLiteral n:
                VisitBooleanLiteral(n);
                break;
            case NoneLiteral n:
                VisitNoneLiteral(n);
                break;
            case EllipsisLiteral n:
                VisitEllipsisLiteral(n);
                break;

            // Expressions - Collections
            case ListLiteral n:
                VisitListLiteral(n);
                break;
            case DictLiteral n:
                VisitDictLiteral(n);
                break;
            case SetLiteral n:
                VisitSetLiteral(n);
                break;
            case TupleLiteral n:
                VisitTupleLiteral(n);
                break;

            // Expressions - Comprehensions
            case ListComprehension n:
                VisitListComprehension(n);
                break;
            case SetComprehension n:
                VisitSetComprehension(n);
                break;
            case DictComprehension n:
                VisitDictComprehension(n);
                break;

            // ComprehensionClauses
            case ForClause n:
                VisitForClause(n);
                break;
            case IfClause n:
                VisitIfClause(n);
                break;

            // Expressions - Primaries
            case Identifier n:
                VisitIdentifier(n);
                break;
            case MemberAccess n:
                VisitMemberAccess(n);
                break;
            case IndexAccess n:
                VisitIndexAccess(n);
                break;
            case SliceAccess n:
                VisitSliceAccess(n);
                break;
            case FunctionCall n:
                VisitFunctionCall(n);
                break;

            // Expressions - Operators
            case UnaryOp n:
                VisitUnaryOp(n);
                break;
            case BinaryOp n:
                VisitBinaryOp(n);
                break;
            case ComparisonChain n:
                VisitComparisonChain(n);
                break;

            // Expressions - Advanced
            case ConditionalExpression n:
                VisitConditionalExpression(n);
                break;
            case LambdaExpression n:
                VisitLambdaExpression(n);
                break;
            case TypeCast n:
                VisitTypeCast(n);
                break;
            case TypeCoercion n:
                VisitTypeCoercion(n);
                break;
            case TypeCheck n:
                VisitTypeCheck(n);
                break;
            case Parenthesized n:
                VisitParenthesized(n);
                break;
            case SuperExpression n:
                VisitSuperExpression(n);
                break;
            case WalrusExpression n:
                VisitWalrusExpression(n);
                break;
            case TryExpression n:
                VisitTryExpression(n);
                break;
            case MaybeExpression n:
                VisitMaybeExpression(n);
                break;
            case StarExpression n:
                VisitStarExpression(n);
                break;
            case SpreadElement n:
                VisitSpreadElement(n);
                break;

            // Expressions - Future
            case AwaitExpression n:
                VisitAwaitExpression(n);
                break;
            case MatchExpression n:
                VisitMatchExpression(n);
                break;

            // Statements - Simple
            case ExpressionStatement n:
                VisitExpressionStatement(n);
                break;
            case Assignment n:
                VisitAssignment(n);
                break;
            case VariableDeclaration n:
                VisitVariableDeclaration(n);
                break;
            case AssertStatement n:
                VisitAssertStatement(n);
                break;
            case PassStatement n:
                VisitPassStatement(n);
                break;
            case BreakStatement n:
                VisitBreakStatement(n);
                break;
            case BreakWithFlagStatement n:
                VisitBreakWithFlagStatement(n);
                break;
            case ContinueStatement n:
                VisitContinueStatement(n);
                break;
            case ReturnStatement n:
                VisitReturnStatement(n);
                break;
            case YieldStatement n:
                VisitYieldStatement(n);
                break;
            case RaiseStatement n:
                VisitRaiseStatement(n);
                break;

            // Statements - Compound
            case IfStatement n:
                VisitIfStatement(n);
                break;
            case WhileStatement n:
                VisitWhileStatement(n);
                break;
            case ForStatement n:
                VisitForStatement(n);
                break;
            case TryStatement n:
                VisitTryStatement(n);
                break;
            case WithStatement n:
                VisitWithStatement(n);
                break;

            // Statements - Definitions
            case FunctionDef n:
                VisitFunctionDef(n);
                break;
            case ClassDef n:
                VisitClassDef(n);
                break;
            case StructDef n:
                VisitStructDef(n);
                break;
            case InterfaceDef n:
                VisitInterfaceDef(n);
                break;
            case EnumDef n:
                VisitEnumDef(n);
                break;
            case TypeAlias n:
                VisitTypeAlias(n);
                break;
            case PropertyDef n:
                VisitPropertyDef(n);
                break;

            // Statements - Imports
            case ImportStatement n:
                VisitImportStatement(n);
                break;
            case FromImportStatement n:
                VisitFromImportStatement(n);
                break;

            // Statements - Future
            case MatchStatement n:
                VisitMatchStatement(n);
                break;
            case UnionDef n:
                VisitUnionDef(n);
                break;
            case DelegateDef n:
                VisitDelegateDef(n);
                break;
            case EventDef n:
                VisitEventDef(n);
                break;

            // Patterns
            case WildcardPattern n:
                VisitWildcardPattern(n);
                break;
            case BindingPattern n:
                VisitBindingPattern(n);
                break;
            case LiteralPattern n:
                VisitLiteralPattern(n);
                break;
            case TypePattern n:
                VisitTypePattern(n);
                break;
            case UnionCasePattern n:
                VisitUnionCasePattern(n);
                break;
            case TuplePattern n:
                VisitTuplePattern(n);
                break;
            case ListPattern n:
                VisitListPattern(n);
                break;
            case OrPattern n:
                VisitOrPattern(n);
                break;
            case AndPattern n:
                VisitAndPattern(n);
                break;
            case GuardPattern n:
                VisitGuardPattern(n);
                break;
            case MemberAccessPattern n:
                VisitMemberAccessPattern(n);
                break;
            case RelationalPattern n:
                VisitRelationalPattern(n);
                break;
            case PropertyPatternField n:
                VisitPropertyPatternField(n);
                break;
            case PropertyPattern n:
                VisitPropertyPattern(n);
                break;
            case PositionalPattern n:
                VisitPositionalPattern(n);
                break;

            default:
                DefaultVisit(node);
                break;
        }
    }

    /// <summary>
    /// Default behavior: traverse all child nodes.
    /// Override to change the default traversal strategy.
    /// </summary>
    public virtual void DefaultVisit(Node node)
    {
        foreach (var child in node.GetChildNodes())
            Visit(child);
    }

    #region Category methods

    /// <summary>
    /// Called for all expression nodes that don't have a specific override.
    /// </summary>
    public virtual void VisitExpression(Expression node) => DefaultVisit(node);

    /// <summary>
    /// Called for all statement nodes that don't have a specific override.
    /// </summary>
    public virtual void VisitStatement(Statement node) => DefaultVisit(node);

    /// <summary>
    /// Called for all pattern nodes that don't have a specific override.
    /// </summary>
    public virtual void VisitPattern(Pattern node) => DefaultVisit(node);

    /// <summary>
    /// Called for all comprehension clause nodes that don't have a specific override.
    /// </summary>
    public virtual void VisitComprehensionClause(ComprehensionClause node) => DefaultVisit(node);

    #endregion

    #region Module

    public virtual void VisitModule(Module node) => DefaultVisit(node);

    #endregion

    #region Expressions - Literals

    public virtual void VisitIntegerLiteral(IntegerLiteral node) => VisitExpression(node);
    public virtual void VisitFloatLiteral(FloatLiteral node) => VisitExpression(node);
    public virtual void VisitStringLiteral(StringLiteral node) => VisitExpression(node);
    public virtual void VisitFStringLiteral(FStringLiteral node) => VisitExpression(node);
    public virtual void VisitBooleanLiteral(BooleanLiteral node) => VisitExpression(node);
    public virtual void VisitNoneLiteral(NoneLiteral node) => VisitExpression(node);
    public virtual void VisitEllipsisLiteral(EllipsisLiteral node) => VisitExpression(node);

    #endregion

    #region Expressions - Collections

    public virtual void VisitListLiteral(ListLiteral node) => VisitExpression(node);
    public virtual void VisitDictLiteral(DictLiteral node) => VisitExpression(node);
    public virtual void VisitSetLiteral(SetLiteral node) => VisitExpression(node);
    public virtual void VisitTupleLiteral(TupleLiteral node) => VisitExpression(node);

    #endregion

    #region Expressions - Comprehensions

    public virtual void VisitListComprehension(ListComprehension node) => VisitExpression(node);
    public virtual void VisitSetComprehension(SetComprehension node) => VisitExpression(node);
    public virtual void VisitDictComprehension(DictComprehension node) => VisitExpression(node);

    #endregion

    #region ComprehensionClauses

    public virtual void VisitForClause(ForClause node) => VisitComprehensionClause(node);
    public virtual void VisitIfClause(IfClause node) => VisitComprehensionClause(node);

    #endregion

    #region Expressions - Primaries

    public virtual void VisitIdentifier(Identifier node) => VisitExpression(node);
    public virtual void VisitMemberAccess(MemberAccess node) => VisitExpression(node);
    public virtual void VisitIndexAccess(IndexAccess node) => VisitExpression(node);
    public virtual void VisitSliceAccess(SliceAccess node) => VisitExpression(node);
    public virtual void VisitFunctionCall(FunctionCall node) => VisitExpression(node);

    #endregion

    #region Expressions - Operators

    public virtual void VisitUnaryOp(UnaryOp node) => VisitExpression(node);
    public virtual void VisitBinaryOp(BinaryOp node) => VisitExpression(node);
    public virtual void VisitComparisonChain(ComparisonChain node) => VisitExpression(node);

    #endregion

    #region Expressions - Advanced

    public virtual void VisitConditionalExpression(ConditionalExpression node) => VisitExpression(node);
    public virtual void VisitLambdaExpression(LambdaExpression node) => VisitExpression(node);
    public virtual void VisitTypeCast(TypeCast node) => VisitExpression(node);
    public virtual void VisitTypeCoercion(TypeCoercion node) => VisitExpression(node);
    public virtual void VisitTypeCheck(TypeCheck node) => VisitExpression(node);
    public virtual void VisitParenthesized(Parenthesized node) => VisitExpression(node);
    public virtual void VisitSuperExpression(SuperExpression node) => VisitExpression(node);
    public virtual void VisitWalrusExpression(WalrusExpression node) => VisitExpression(node);
    public virtual void VisitTryExpression(TryExpression node) => VisitExpression(node);
    public virtual void VisitMaybeExpression(MaybeExpression node) => VisitExpression(node);
    public virtual void VisitStarExpression(StarExpression node) => VisitExpression(node);
    public virtual void VisitSpreadElement(SpreadElement node) => VisitExpression(node);

    #endregion

    #region Expressions - Future

    public virtual void VisitAwaitExpression(AwaitExpression node) => VisitExpression(node);
    public virtual void VisitMatchExpression(MatchExpression node) => VisitExpression(node);

    #endregion

    #region Statements - Simple

    public virtual void VisitExpressionStatement(ExpressionStatement node) => VisitStatement(node);
    public virtual void VisitAssignment(Assignment node) => VisitStatement(node);
    public virtual void VisitVariableDeclaration(VariableDeclaration node) => VisitStatement(node);
    public virtual void VisitAssertStatement(AssertStatement node) => VisitStatement(node);
    public virtual void VisitPassStatement(PassStatement node) => VisitStatement(node);
    public virtual void VisitBreakStatement(BreakStatement node) => VisitStatement(node);
    public virtual void VisitBreakWithFlagStatement(BreakWithFlagStatement node) => VisitStatement(node);
    public virtual void VisitContinueStatement(ContinueStatement node) => VisitStatement(node);
    public virtual void VisitReturnStatement(ReturnStatement node) => VisitStatement(node);
    public virtual void VisitYieldStatement(YieldStatement node) => VisitStatement(node);
    public virtual void VisitRaiseStatement(RaiseStatement node) => VisitStatement(node);

    #endregion

    #region Statements - Compound

    public virtual void VisitIfStatement(IfStatement node) => VisitStatement(node);
    public virtual void VisitWhileStatement(WhileStatement node) => VisitStatement(node);
    public virtual void VisitForStatement(ForStatement node) => VisitStatement(node);
    public virtual void VisitTryStatement(TryStatement node) => VisitStatement(node);
    public virtual void VisitWithStatement(WithStatement node) => VisitStatement(node);

    #endregion

    #region Statements - Definitions

    public virtual void VisitFunctionDef(FunctionDef node) => VisitStatement(node);
    public virtual void VisitClassDef(ClassDef node) => VisitStatement(node);
    public virtual void VisitStructDef(StructDef node) => VisitStatement(node);
    public virtual void VisitInterfaceDef(InterfaceDef node) => VisitStatement(node);
    public virtual void VisitEnumDef(EnumDef node) => VisitStatement(node);
    public virtual void VisitTypeAlias(TypeAlias node) => VisitStatement(node);
    public virtual void VisitPropertyDef(PropertyDef node) => VisitStatement(node);

    #endregion

    #region Statements - Imports

    public virtual void VisitImportStatement(ImportStatement node) => VisitStatement(node);
    public virtual void VisitFromImportStatement(FromImportStatement node) => VisitStatement(node);

    #endregion

    #region Statements - Future

    public virtual void VisitMatchStatement(MatchStatement node) => VisitStatement(node);
    public virtual void VisitUnionDef(UnionDef node) => VisitStatement(node);
    public virtual void VisitDelegateDef(DelegateDef node) => VisitStatement(node);
    public virtual void VisitEventDef(EventDef node) => VisitStatement(node);

    #endregion

    #region Patterns

    public virtual void VisitWildcardPattern(WildcardPattern node) => VisitPattern(node);
    public virtual void VisitBindingPattern(BindingPattern node) => VisitPattern(node);
    public virtual void VisitLiteralPattern(LiteralPattern node) => VisitPattern(node);
    public virtual void VisitTypePattern(TypePattern node) => VisitPattern(node);
    public virtual void VisitUnionCasePattern(UnionCasePattern node) => VisitPattern(node);
    public virtual void VisitTuplePattern(TuplePattern node) => VisitPattern(node);
    public virtual void VisitListPattern(ListPattern node) => VisitPattern(node);
    public virtual void VisitOrPattern(OrPattern node) => VisitPattern(node);
    public virtual void VisitAndPattern(AndPattern node) => VisitPattern(node);
    public virtual void VisitGuardPattern(GuardPattern node) => VisitPattern(node);
    public virtual void VisitMemberAccessPattern(MemberAccessPattern node) => VisitPattern(node);
    public virtual void VisitRelationalPattern(RelationalPattern node) => VisitPattern(node);
    public virtual void VisitPropertyPatternField(PropertyPatternField node) => DefaultVisit(node);
    public virtual void VisitPropertyPattern(PropertyPattern node) => VisitPattern(node);
    public virtual void VisitPositionalPattern(PositionalPattern node) => VisitPattern(node);

    #endregion
}

/// <summary>
/// Base class for AST visitors that return a value of type <typeparamref name="T"/>.
/// Uses central switch dispatch (not double dispatch with Accept()).
/// Override specific Visit methods to handle nodes of interest.
/// Unoverridden methods default to traversing child nodes and returning <c>default!</c>.
/// </summary>
/// <typeparam name="T">The return type of Visit methods.</typeparam>
public abstract class AstVisitor<T>
{
    /// <summary>
    /// Dispatches to the appropriate typed Visit method based on the node's runtime type.
    /// </summary>
    public virtual T Visit(Node node)
    {
        return node switch
        {
            // Module
            Module n => VisitModule(n),

            // Expressions - Literals
            IntegerLiteral n => VisitIntegerLiteral(n),
            FloatLiteral n => VisitFloatLiteral(n),
            StringLiteral n => VisitStringLiteral(n),
            FStringLiteral n => VisitFStringLiteral(n),
            BooleanLiteral n => VisitBooleanLiteral(n),
            NoneLiteral n => VisitNoneLiteral(n),
            EllipsisLiteral n => VisitEllipsisLiteral(n),

            // Expressions - Collections
            ListLiteral n => VisitListLiteral(n),
            DictLiteral n => VisitDictLiteral(n),
            SetLiteral n => VisitSetLiteral(n),
            TupleLiteral n => VisitTupleLiteral(n),

            // Expressions - Comprehensions
            ListComprehension n => VisitListComprehension(n),
            SetComprehension n => VisitSetComprehension(n),
            DictComprehension n => VisitDictComprehension(n),

            // ComprehensionClauses
            ForClause n => VisitForClause(n),
            IfClause n => VisitIfClause(n),

            // Expressions - Primaries
            Identifier n => VisitIdentifier(n),
            MemberAccess n => VisitMemberAccess(n),
            IndexAccess n => VisitIndexAccess(n),
            SliceAccess n => VisitSliceAccess(n),
            FunctionCall n => VisitFunctionCall(n),

            // Expressions - Operators
            UnaryOp n => VisitUnaryOp(n),
            BinaryOp n => VisitBinaryOp(n),
            ComparisonChain n => VisitComparisonChain(n),

            // Expressions - Advanced
            ConditionalExpression n => VisitConditionalExpression(n),
            LambdaExpression n => VisitLambdaExpression(n),
            TypeCast n => VisitTypeCast(n),
            TypeCoercion n => VisitTypeCoercion(n),
            TypeCheck n => VisitTypeCheck(n),
            Parenthesized n => VisitParenthesized(n),
            SuperExpression n => VisitSuperExpression(n),
            WalrusExpression n => VisitWalrusExpression(n),
            TryExpression n => VisitTryExpression(n),
            MaybeExpression n => VisitMaybeExpression(n),
            StarExpression n => VisitStarExpression(n),
            SpreadElement n => VisitSpreadElement(n),

            // Expressions - Future
            AwaitExpression n => VisitAwaitExpression(n),
            MatchExpression n => VisitMatchExpression(n),

            // Statements - Simple
            ExpressionStatement n => VisitExpressionStatement(n),
            Assignment n => VisitAssignment(n),
            VariableDeclaration n => VisitVariableDeclaration(n),
            AssertStatement n => VisitAssertStatement(n),
            PassStatement n => VisitPassStatement(n),
            BreakStatement n => VisitBreakStatement(n),
            BreakWithFlagStatement n => VisitBreakWithFlagStatement(n),
            ContinueStatement n => VisitContinueStatement(n),
            ReturnStatement n => VisitReturnStatement(n),
            YieldStatement n => VisitYieldStatement(n),
            RaiseStatement n => VisitRaiseStatement(n),

            // Statements - Compound
            IfStatement n => VisitIfStatement(n),
            WhileStatement n => VisitWhileStatement(n),
            ForStatement n => VisitForStatement(n),
            TryStatement n => VisitTryStatement(n),
            WithStatement n => VisitWithStatement(n),

            // Statements - Definitions
            FunctionDef n => VisitFunctionDef(n),
            ClassDef n => VisitClassDef(n),
            StructDef n => VisitStructDef(n),
            InterfaceDef n => VisitInterfaceDef(n),
            EnumDef n => VisitEnumDef(n),
            TypeAlias n => VisitTypeAlias(n),
            PropertyDef n => VisitPropertyDef(n),

            // Statements - Imports
            ImportStatement n => VisitImportStatement(n),
            FromImportStatement n => VisitFromImportStatement(n),

            // Statements - Future
            MatchStatement n => VisitMatchStatement(n),
            UnionDef n => VisitUnionDef(n),
            DelegateDef n => VisitDelegateDef(n),
            EventDef n => VisitEventDef(n),

            // Patterns
            WildcardPattern n => VisitWildcardPattern(n),
            BindingPattern n => VisitBindingPattern(n),
            LiteralPattern n => VisitLiteralPattern(n),
            TypePattern n => VisitTypePattern(n),
            UnionCasePattern n => VisitUnionCasePattern(n),
            TuplePattern n => VisitTuplePattern(n),
            ListPattern n => VisitListPattern(n),
            OrPattern n => VisitOrPattern(n),
            AndPattern n => VisitAndPattern(n),
            GuardPattern n => VisitGuardPattern(n),
            MemberAccessPattern n => VisitMemberAccessPattern(n),
            RelationalPattern n => VisitRelationalPattern(n),
            PropertyPatternField n => VisitPropertyPatternField(n),
            PropertyPattern n => VisitPropertyPattern(n),
            PositionalPattern n => VisitPositionalPattern(n),

            _ => DefaultVisit(node)
        };
    }

    /// <summary>
    /// Default behavior: traverse all child nodes and return <c>default!</c>.
    /// Override to change the default traversal strategy.
    /// </summary>
    public virtual T DefaultVisit(Node node)
    {
        foreach (var child in node.GetChildNodes())
            Visit(child);
        return default!;
    }

    #region Category methods

    /// <summary>
    /// Called for all expression nodes that don't have a specific override.
    /// </summary>
    public virtual T VisitExpression(Expression node) => DefaultVisit(node);

    /// <summary>
    /// Called for all statement nodes that don't have a specific override.
    /// </summary>
    public virtual T VisitStatement(Statement node) => DefaultVisit(node);

    /// <summary>
    /// Called for all pattern nodes that don't have a specific override.
    /// </summary>
    public virtual T VisitPattern(Pattern node) => DefaultVisit(node);

    /// <summary>
    /// Called for all comprehension clause nodes that don't have a specific override.
    /// </summary>
    public virtual T VisitComprehensionClause(ComprehensionClause node) => DefaultVisit(node);

    #endregion

    #region Module

    public virtual T VisitModule(Module node) => DefaultVisit(node);

    #endregion

    #region Expressions - Literals

    public virtual T VisitIntegerLiteral(IntegerLiteral node) => VisitExpression(node);
    public virtual T VisitFloatLiteral(FloatLiteral node) => VisitExpression(node);
    public virtual T VisitStringLiteral(StringLiteral node) => VisitExpression(node);
    public virtual T VisitFStringLiteral(FStringLiteral node) => VisitExpression(node);
    public virtual T VisitBooleanLiteral(BooleanLiteral node) => VisitExpression(node);
    public virtual T VisitNoneLiteral(NoneLiteral node) => VisitExpression(node);
    public virtual T VisitEllipsisLiteral(EllipsisLiteral node) => VisitExpression(node);

    #endregion

    #region Expressions - Collections

    public virtual T VisitListLiteral(ListLiteral node) => VisitExpression(node);
    public virtual T VisitDictLiteral(DictLiteral node) => VisitExpression(node);
    public virtual T VisitSetLiteral(SetLiteral node) => VisitExpression(node);
    public virtual T VisitTupleLiteral(TupleLiteral node) => VisitExpression(node);

    #endregion

    #region Expressions - Comprehensions

    public virtual T VisitListComprehension(ListComprehension node) => VisitExpression(node);
    public virtual T VisitSetComprehension(SetComprehension node) => VisitExpression(node);
    public virtual T VisitDictComprehension(DictComprehension node) => VisitExpression(node);

    #endregion

    #region ComprehensionClauses

    public virtual T VisitForClause(ForClause node) => VisitComprehensionClause(node);
    public virtual T VisitIfClause(IfClause node) => VisitComprehensionClause(node);

    #endregion

    #region Expressions - Primaries

    public virtual T VisitIdentifier(Identifier node) => VisitExpression(node);
    public virtual T VisitMemberAccess(MemberAccess node) => VisitExpression(node);
    public virtual T VisitIndexAccess(IndexAccess node) => VisitExpression(node);
    public virtual T VisitSliceAccess(SliceAccess node) => VisitExpression(node);
    public virtual T VisitFunctionCall(FunctionCall node) => VisitExpression(node);

    #endregion

    #region Expressions - Operators

    public virtual T VisitUnaryOp(UnaryOp node) => VisitExpression(node);
    public virtual T VisitBinaryOp(BinaryOp node) => VisitExpression(node);
    public virtual T VisitComparisonChain(ComparisonChain node) => VisitExpression(node);

    #endregion

    #region Expressions - Advanced

    public virtual T VisitConditionalExpression(ConditionalExpression node) => VisitExpression(node);
    public virtual T VisitLambdaExpression(LambdaExpression node) => VisitExpression(node);
    public virtual T VisitTypeCast(TypeCast node) => VisitExpression(node);
    public virtual T VisitTypeCoercion(TypeCoercion node) => VisitExpression(node);
    public virtual T VisitTypeCheck(TypeCheck node) => VisitExpression(node);
    public virtual T VisitParenthesized(Parenthesized node) => VisitExpression(node);
    public virtual T VisitSuperExpression(SuperExpression node) => VisitExpression(node);
    public virtual T VisitWalrusExpression(WalrusExpression node) => VisitExpression(node);
    public virtual T VisitTryExpression(TryExpression node) => VisitExpression(node);
    public virtual T VisitMaybeExpression(MaybeExpression node) => VisitExpression(node);
    public virtual T VisitStarExpression(StarExpression node) => VisitExpression(node);
    public virtual T VisitSpreadElement(SpreadElement node) => VisitExpression(node);

    #endregion

    #region Expressions - Future

    public virtual T VisitAwaitExpression(AwaitExpression node) => VisitExpression(node);
    public virtual T VisitMatchExpression(MatchExpression node) => VisitExpression(node);

    #endregion

    #region Statements - Simple

    public virtual T VisitExpressionStatement(ExpressionStatement node) => VisitStatement(node);
    public virtual T VisitAssignment(Assignment node) => VisitStatement(node);
    public virtual T VisitVariableDeclaration(VariableDeclaration node) => VisitStatement(node);
    public virtual T VisitAssertStatement(AssertStatement node) => VisitStatement(node);
    public virtual T VisitPassStatement(PassStatement node) => VisitStatement(node);
    public virtual T VisitBreakStatement(BreakStatement node) => VisitStatement(node);
    public virtual T VisitBreakWithFlagStatement(BreakWithFlagStatement node) => VisitStatement(node);
    public virtual T VisitContinueStatement(ContinueStatement node) => VisitStatement(node);
    public virtual T VisitReturnStatement(ReturnStatement node) => VisitStatement(node);
    public virtual T VisitYieldStatement(YieldStatement node) => VisitStatement(node);
    public virtual T VisitRaiseStatement(RaiseStatement node) => VisitStatement(node);

    #endregion

    #region Statements - Compound

    public virtual T VisitIfStatement(IfStatement node) => VisitStatement(node);
    public virtual T VisitWhileStatement(WhileStatement node) => VisitStatement(node);
    public virtual T VisitForStatement(ForStatement node) => VisitStatement(node);
    public virtual T VisitTryStatement(TryStatement node) => VisitStatement(node);
    public virtual T VisitWithStatement(WithStatement node) => VisitStatement(node);

    #endregion

    #region Statements - Definitions

    public virtual T VisitFunctionDef(FunctionDef node) => VisitStatement(node);
    public virtual T VisitClassDef(ClassDef node) => VisitStatement(node);
    public virtual T VisitStructDef(StructDef node) => VisitStatement(node);
    public virtual T VisitInterfaceDef(InterfaceDef node) => VisitStatement(node);
    public virtual T VisitEnumDef(EnumDef node) => VisitStatement(node);
    public virtual T VisitTypeAlias(TypeAlias node) => VisitStatement(node);
    public virtual T VisitPropertyDef(PropertyDef node) => VisitStatement(node);

    #endregion

    #region Statements - Imports

    public virtual T VisitImportStatement(ImportStatement node) => VisitStatement(node);
    public virtual T VisitFromImportStatement(FromImportStatement node) => VisitStatement(node);

    #endregion

    #region Statements - Future

    public virtual T VisitMatchStatement(MatchStatement node) => VisitStatement(node);
    public virtual T VisitUnionDef(UnionDef node) => VisitStatement(node);
    public virtual T VisitDelegateDef(DelegateDef node) => VisitStatement(node);
    public virtual T VisitEventDef(EventDef node) => VisitStatement(node);

    #endregion

    #region Patterns

    public virtual T VisitWildcardPattern(WildcardPattern node) => VisitPattern(node);
    public virtual T VisitBindingPattern(BindingPattern node) => VisitPattern(node);
    public virtual T VisitLiteralPattern(LiteralPattern node) => VisitPattern(node);
    public virtual T VisitTypePattern(TypePattern node) => VisitPattern(node);
    public virtual T VisitUnionCasePattern(UnionCasePattern node) => VisitPattern(node);
    public virtual T VisitTuplePattern(TuplePattern node) => VisitPattern(node);
    public virtual T VisitListPattern(ListPattern node) => VisitPattern(node);
    public virtual T VisitOrPattern(OrPattern node) => VisitPattern(node);
    public virtual T VisitAndPattern(AndPattern node) => VisitPattern(node);
    public virtual T VisitGuardPattern(GuardPattern node) => VisitPattern(node);
    public virtual T VisitMemberAccessPattern(MemberAccessPattern node) => VisitPattern(node);
    public virtual T VisitRelationalPattern(RelationalPattern node) => VisitPattern(node);
    public virtual T VisitPropertyPatternField(PropertyPatternField node) => DefaultVisit(node);
    public virtual T VisitPropertyPattern(PropertyPattern node) => VisitPattern(node);
    public virtual T VisitPositionalPattern(PositionalPattern node) => VisitPattern(node);

    #endregion
}
