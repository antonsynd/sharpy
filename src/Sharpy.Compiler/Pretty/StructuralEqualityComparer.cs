using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Pretty;

public sealed class StructuralEqualityComparer : IEqualityComparer<Node>
{
    public static readonly StructuralEqualityComparer Instance = new();

    public bool Equals(Node? x, Node? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;
        if (x.GetType() != y.GetType())
            return false;

        return x switch
        {
            Module a => ModuleEquals(a, (Module)y),
            IntegerLiteral a => a.Value == ((IntegerLiteral)y).Value && a.Suffix == ((IntegerLiteral)y).Suffix,
            FloatLiteral a => a.Value == ((FloatLiteral)y).Value && a.Suffix == ((FloatLiteral)y).Suffix,
            StringLiteral a => a.Value == ((StringLiteral)y).Value && a.IsRaw == ((StringLiteral)y).IsRaw,
            BytesLiteralExpression a => a.Value == ((BytesLiteralExpression)y).Value,
            FStringLiteral a => FStringEquals(a, (FStringLiteral)y),
            TStringLiteral a => TStringEquals(a, (TStringLiteral)y),
            BooleanLiteral a => a.Value == ((BooleanLiteral)y).Value,
            NoneLiteral => true,
            EllipsisLiteral => true,
            ListLiteral a => NodesEqual(a.Elements, ((ListLiteral)y).Elements),
            DictLiteral a => DictLiteralEquals(a, (DictLiteral)y),
            SetLiteral a => NodesEqual(a.Elements, ((SetLiteral)y).Elements),
            TupleLiteral a => TupleLiteralEquals(a, (TupleLiteral)y),
            ListComprehension a => ComprehensionEquals(a.Element, a.Clauses, ((ListComprehension)y).Element, ((ListComprehension)y).Clauses),
            SetComprehension a => ComprehensionEquals(a.Element, a.Clauses, ((SetComprehension)y).Element, ((SetComprehension)y).Clauses),
            DictComprehension a => DictComprehensionEquals(a, (DictComprehension)y),
            DictSpreadComprehension a => Equals(a.Spread, ((DictSpreadComprehension)y).Spread) && ClausesEqual(a.Clauses, ((DictSpreadComprehension)y).Clauses),
            ForClause a => Equals(a.Target, ((ForClause)y).Target) && Equals(a.Iterator, ((ForClause)y).Iterator),
            IfClause a => Equals(a.Condition, ((IfClause)y).Condition),
            Identifier a => a.Name == ((Identifier)y).Name && a.IsNameBacktickEscaped == ((Identifier)y).IsNameBacktickEscaped,
            MemberAccess a => MemberAccessEquals(a, (MemberAccess)y),
            IndexAccess a => Equals(a.Object, ((IndexAccess)y).Object) && Equals(a.Index, ((IndexAccess)y).Index),
            SliceAccess a => SliceEquals(a, (SliceAccess)y),
            FunctionCall a => FunctionCallEquals(a, (FunctionCall)y),
            UnaryOp a => a.Operator == ((UnaryOp)y).Operator && Equals(a.Operand, ((UnaryOp)y).Operand),
            BinaryOp a => a.Operator == ((BinaryOp)y).Operator && Equals(a.Left, ((BinaryOp)y).Left) && Equals(a.Right, ((BinaryOp)y).Right),
            ComparisonChain a => ComparisonChainEquals(a, (ComparisonChain)y),
            ConditionalExpression a => Equals(a.Test, ((ConditionalExpression)y).Test) && Equals(a.ThenValue, ((ConditionalExpression)y).ThenValue) && Equals(a.ElseValue, ((ConditionalExpression)y).ElseValue),
            LambdaExpression a => LambdaEquals(a, (LambdaExpression)y),
            TypeCoercion a => Equals(a.Value, ((TypeCoercion)y).Value) && TypeAnnotationEquals(a.TargetType, ((TypeCoercion)y).TargetType),
            TypeCheck a => Equals(a.Value, ((TypeCheck)y).Value) && TypeAnnotationEquals(a.CheckType, ((TypeCheck)y).CheckType),
            Parenthesized a => Equals(a.Expression, ((Parenthesized)y).Expression),
            SuperExpression => true,
            WalrusExpression a => a.Target == ((WalrusExpression)y).Target && Equals(a.Value, ((WalrusExpression)y).Value),
            TryExpression a => Equals(a.Operand, ((TryExpression)y).Operand) && TypeAnnotationsEqual(a.ExceptionTypes, ((TryExpression)y).ExceptionTypes),
            MaybeExpression a => Equals(a.Operand, ((MaybeExpression)y).Operand),
            StarExpression a => Equals(a.Operand, ((StarExpression)y).Operand),
            SpreadElement a => Equals(a.Value, ((SpreadElement)y).Value),
            ModifiedArgument a => ModifiedArgumentEquals(a, (ModifiedArgument)y),
            AwaitExpression a => Equals(a.Operand, ((AwaitExpression)y).Operand),
            MatchExpression a => MatchExprEquals(a, (MatchExpression)y),
            ExpressionStatement a => Equals(a.Expression, ((ExpressionStatement)y).Expression),
            Assignment a => AssignmentEquals(a, (Assignment)y),
            VariableDeclaration a => VarDeclEquals(a, (VariableDeclaration)y),
            AssertStatement a => Equals(a.Test, ((AssertStatement)y).Test) && NullableNodeEquals(a.Message, ((AssertStatement)y).Message),
            PassStatement => true,
            BreakStatement => true,
            BreakWithFlagStatement a => a.FlagName == ((BreakWithFlagStatement)y).FlagName,
            ContinueStatement => true,
            ReturnStatement a => NullableNodeEquals(a.Value, ((ReturnStatement)y).Value),
            YieldStatement a => a.IsFrom == ((YieldStatement)y).IsFrom && Equals(a.Value, ((YieldStatement)y).Value),
            RaiseStatement a => NullableNodeEquals(a.Exception, ((RaiseStatement)y).Exception) && NullableNodeEquals(a.Cause, ((RaiseStatement)y).Cause),
            IfStatement a => IfEquals(a, (IfStatement)y),
            WhileStatement a => Equals(a.Test, ((WhileStatement)y).Test) && StatementsEqual(a.Body, ((WhileStatement)y).Body) && StatementsEqual(a.ElseBody, ((WhileStatement)y).ElseBody),
            ForStatement a => ForEquals(a, (ForStatement)y),
            TryStatement a => TryEquals(a, (TryStatement)y),
            WithStatement a => WithEquals(a, (WithStatement)y),
            FunctionDef a => FunctionDefEquals(a, (FunctionDef)y),
            ClassDef a => ClassDefEquals(a, (ClassDef)y),
            StructDef a => StructDefEquals(a, (StructDef)y),
            InterfaceDef a => InterfaceDefEquals(a, (InterfaceDef)y),
            EnumDef a => EnumDefEquals(a, (EnumDef)y),
            TypeAlias a => TypeAliasEquals(a, (TypeAlias)y),
            PropertyDef a => PropertyDefEquals(a, (PropertyDef)y),
            ImportStatement a => ImportEquals(a, (ImportStatement)y),
            FromImportStatement a => FromImportEquals(a, (FromImportStatement)y),
            MatchStatement a => MatchStmtEquals(a, (MatchStatement)y),
            UnionDef a => UnionDefEquals(a, (UnionDef)y),
            DelegateDef a => DelegateDefEquals(a, (DelegateDef)y),
            EventDef a => EventDefEquals(a, (EventDef)y),
            WildcardPattern => true,
            BindingPattern a => BindingPatternEquals(a, (BindingPattern)y),
            LiteralPattern a => Equals(a.Literal, ((LiteralPattern)y).Literal),
            TypePattern a => TypeAnnotationEquals(a.Type, ((TypePattern)y).Type) && NullableNodeEquals(a.BindingName, ((TypePattern)y).BindingName),
            UnionCasePattern a => UnionCasePatternEquals(a, (UnionCasePattern)y),
            TuplePattern a => NodesEqual(a.Elements, ((TuplePattern)y).Elements),
            ListPattern a => ListPatternEquals(a, (ListPattern)y),
            StarPattern a => NullableNodeEquals(a.Capture, ((StarPattern)y).Capture),
            OrPattern a => NodesEqual(a.Alternatives, ((OrPattern)y).Alternatives),
            AndPattern a => Equals(a.Left, ((AndPattern)y).Left) && Equals(a.Right, ((AndPattern)y).Right),
            GuardPattern a => Equals(a.Inner, ((GuardPattern)y).Inner) && Equals(a.Guard, ((GuardPattern)y).Guard),
            MemberAccessPattern a => a.Parts.SequenceEqual(((MemberAccessPattern)y).Parts),
            RelationalPattern a => a.Operator == ((RelationalPattern)y).Operator && Equals(a.Value, ((RelationalPattern)y).Value),
            PropertyPatternField a => a.Name == ((PropertyPatternField)y).Name && Equals(a.Pattern, ((PropertyPatternField)y).Pattern),
            PropertyPattern a => PropertyPatternEquals(a, (PropertyPattern)y),
            PositionalPattern a => PositionalPatternEquals(a, (PositionalPattern)y),
            _ => false
        };
    }

    public int GetHashCode(Node obj) => obj.GetType().GetHashCode();

    #region Node comparisons

    private bool ModuleEquals(Module a, Module b) =>
        a.DocString == b.DocString && StatementsEqual(a.Body, b.Body);

    private bool DictLiteralEquals(DictLiteral a, DictLiteral b)
    {
        if (a.Entries.Length != b.Entries.Length)
            return false;
        for (int i = 0; i < a.Entries.Length; i++)
        {
            if (!NullableNodeEquals(a.Entries[i].Key, b.Entries[i].Key))
                return false;
            if (!Equals(a.Entries[i].Value, b.Entries[i].Value))
                return false;
        }
        return true;
    }

    private bool TupleLiteralEquals(TupleLiteral a, TupleLiteral b) =>
        NodesEqual(a.Elements, b.Elements) && a.ElementNames.SequenceEqual(b.ElementNames);

    private bool FStringEquals(FStringLiteral a, FStringLiteral b) =>
        FStringPartsEqual(a.Parts, b.Parts);

    private bool TStringEquals(TStringLiteral a, TStringLiteral b) =>
        FStringPartsEqual(a.Parts, b.Parts);

    private bool FStringPartsEqual(ImmutableArray<FStringPart> a, ImmutableArray<FStringPart> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Text != b[i].Text)
                return false;
            if (a[i].FormatSpec != b[i].FormatSpec)
                return false;
            if (a[i].Conversion != b[i].Conversion)
                return false;
            if (a[i].SourceText != b[i].SourceText)
                return false;
            if (a[i].IsSelfDocumenting != b[i].IsSelfDocumenting)
                return false;
            if (!NullableNodeEquals(a[i].Expression, b[i].Expression))
                return false;
        }
        return true;
    }

    private bool MemberAccessEquals(MemberAccess a, MemberAccess b) =>
        a.Member == b.Member && a.IsNullConditional == b.IsNullConditional && Equals(a.Object, b.Object);

    private bool SliceEquals(SliceAccess a, SliceAccess b) =>
        Equals(a.Object, b.Object) && NullableNodeEquals(a.Start, b.Start) && NullableNodeEquals(a.Stop, b.Stop) && NullableNodeEquals(a.Step, b.Step);

    private bool FunctionCallEquals(FunctionCall a, FunctionCall b)
    {
        if (!Equals(a.Function, b.Function))
            return false;
        if (!NodesEqual(a.Arguments, b.Arguments))
            return false;
        return KeywordArgsEqual(a.KeywordArguments, b.KeywordArguments);
    }

    private bool ComparisonChainEquals(ComparisonChain a, ComparisonChain b) =>
        a.Operators.SequenceEqual(b.Operators) && NodesEqual(a.Operands, b.Operands);

    private bool LambdaEquals(LambdaExpression a, LambdaExpression b) =>
        a.IsArrowSyntax == b.IsArrowSyntax
        && ParametersEqual(a.Parameters, b.Parameters)
        && NullableTypeEquals(a.ReturnType, b.ReturnType)
        && Equals(a.Body, b.Body);

    private bool ModifiedArgumentEquals(ModifiedArgument a, ModifiedArgument b) =>
        a.Modifier == b.Modifier
        && a.InlineName == b.InlineName
        && NullableTypeEquals(a.InlineType, b.InlineType)
        && Equals(a.Argument, b.Argument);

    private bool MatchExprEquals(MatchExpression a, MatchExpression b)
    {
        if (!Equals(a.Scrutinee, b.Scrutinee))
            return false;
        if (a.Arms.Length != b.Arms.Length)
            return false;
        for (int i = 0; i < a.Arms.Length; i++)
        {
            if (!Equals(a.Arms[i].Pattern, b.Arms[i].Pattern))
                return false;
            if (!NullableNodeEquals(a.Arms[i].Guard, b.Arms[i].Guard))
                return false;
            if (!Equals(a.Arms[i].Result, b.Arms[i].Result))
                return false;
        }
        return true;
    }

    private bool AssignmentEquals(Assignment a, Assignment b) =>
        a.Operator == b.Operator && Equals(a.Target, b.Target) && Equals(a.Value, b.Value);

    private bool VarDeclEquals(VariableDeclaration a, VariableDeclaration b) =>
        a.Name == b.Name
        && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.IsConst == b.IsConst
        && NullableTypeEquals(a.Type, b.Type)
        && NullableNodeEquals(a.InitialValue, b.InitialValue)
        && DecoratorsEqual(a.Decorators, b.Decorators);

    private bool IfEquals(IfStatement a, IfStatement b)
    {
        if (!Equals(a.Test, b.Test))
            return false;
        if (!StatementsEqual(a.ThenBody, b.ThenBody))
            return false;
        if (a.ElifClauses.Length != b.ElifClauses.Length)
            return false;
        for (int i = 0; i < a.ElifClauses.Length; i++)
        {
            if (!Equals(a.ElifClauses[i].Test, b.ElifClauses[i].Test))
                return false;
            if (!StatementsEqual(a.ElifClauses[i].Body, b.ElifClauses[i].Body))
                return false;
        }
        return StatementsEqual(a.ElseBody, b.ElseBody);
    }

    private bool ForEquals(ForStatement a, ForStatement b) =>
        a.IsAsync == b.IsAsync && Equals(a.Target, b.Target) && Equals(a.Iterator, b.Iterator)
        && StatementsEqual(a.Body, b.Body) && StatementsEqual(a.ElseBody, b.ElseBody);

    private bool TryEquals(TryStatement a, TryStatement b)
    {
        if (!StatementsEqual(a.Body, b.Body))
            return false;
        if (a.Handlers.Length != b.Handlers.Length)
            return false;
        for (int i = 0; i < a.Handlers.Length; i++)
        {
            var ha = a.Handlers[i];
            var hb = b.Handlers[i];
            if (ha.IsExceptStar != hb.IsExceptStar)
                return false;
            if (ha.Name != hb.Name)
                return false;
            if (!NullableTypeEquals(ha.ExceptionType, hb.ExceptionType))
                return false;
            if (!NullableNodeEquals(ha.Filter, hb.Filter))
                return false;
            if (!StatementsEqual(ha.Body, hb.Body))
                return false;
        }
        return StatementsEqual(a.ElseBody, b.ElseBody) && StatementsEqual(a.FinallyBody, b.FinallyBody);
    }

    private bool WithEquals(WithStatement a, WithStatement b)
    {
        if (a.IsAsync != b.IsAsync)
            return false;
        if (a.Items.Length != b.Items.Length)
            return false;
        for (int i = 0; i < a.Items.Length; i++)
        {
            if (a.Items[i].Name != b.Items[i].Name)
                return false;
            if (!Equals(a.Items[i].ContextExpression, b.Items[i].ContextExpression))
                return false;
        }
        return StatementsEqual(a.Body, b.Body);
    }

    private bool FunctionDefEquals(FunctionDef a, FunctionDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped && a.IsAsync == b.IsAsync
        && a.DocString == b.DocString
        && TypeParametersEqual(a.TypeParameters, b.TypeParameters)
        && ParametersEqual(a.Parameters, b.Parameters)
        && NullableTypeEquals(a.ReturnType, b.ReturnType)
        && DecoratorsEqual(a.Decorators, b.Decorators)
        && StatementsEqual(a.Body, b.Body);

    private bool ClassDefEquals(ClassDef a, ClassDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.DocString == b.DocString
        && TypeParametersEqual(a.TypeParameters, b.TypeParameters)
        && TypeAnnotationsEqual(a.BaseClasses, b.BaseClasses)
        && DecoratorsEqual(a.Decorators, b.Decorators)
        && StatementsEqual(a.Body, b.Body);

    private bool StructDefEquals(StructDef a, StructDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.DocString == b.DocString
        && TypeParametersEqual(a.TypeParameters, b.TypeParameters)
        && TypeAnnotationsEqual(a.BaseClasses, b.BaseClasses)
        && DecoratorsEqual(a.Decorators, b.Decorators)
        && StatementsEqual(a.Body, b.Body);

    private bool InterfaceDefEquals(InterfaceDef a, InterfaceDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.DocString == b.DocString
        && TypeParametersEqual(a.TypeParameters, b.TypeParameters)
        && TypeAnnotationsEqual(a.BaseInterfaces, b.BaseInterfaces)
        && DecoratorsEqual(a.Decorators, b.Decorators)
        && StatementsEqual(a.Body, b.Body);

    private bool EnumDefEquals(EnumDef a, EnumDef b)
    {
        if (a.Name != b.Name || a.IsNameBacktickEscaped != b.IsNameBacktickEscaped)
            return false;
        if (a.DocString != b.DocString)
            return false;
        if (!DecoratorsEqual(a.Decorators, b.Decorators))
            return false;
        if (a.Members.Length != b.Members.Length)
            return false;
        for (int i = 0; i < a.Members.Length; i++)
        {
            if (a.Members[i].Name != b.Members[i].Name)
                return false;
            if (!NullableNodeEquals(a.Members[i].Value, b.Members[i].Value))
                return false;
        }
        return true;
    }

    private bool TypeAliasEquals(TypeAlias a, TypeAlias b) =>
        a.Name == b.Name
        && TypeParametersEqual(a.TypeParameters, b.TypeParameters)
        && NullableTypeEquals(a.Type, b.Type)
        && NullableFuncTypeEquals(a.FunctionType, b.FunctionType);

    private bool PropertyDefEquals(PropertyDef a, PropertyDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.Accessor == b.Accessor && a.IsFunctionStyle == b.IsFunctionStyle
        && a.ExplicitInterface == b.ExplicitInterface
        && NullableTypeEquals(a.Type, b.Type)
        && NullableTypeEquals(a.ReturnType, b.ReturnType)
        && NullableNodeEquals(a.DefaultValue, b.DefaultValue)
        && ParametersEqual(a.Parameters, b.Parameters)
        && DecoratorsEqual(a.Decorators, b.Decorators)
        && StatementsEqual(a.Body, b.Body);

    private bool ImportEquals(ImportStatement a, ImportStatement b) =>
        ImportAliasesEqual(a.Names, b.Names);

    private bool FromImportEquals(FromImportStatement a, FromImportStatement b) =>
        a.Module == b.Module && a.ImportAll == b.ImportAll && ImportAliasesEqual(a.Names, b.Names);

    private bool MatchStmtEquals(MatchStatement a, MatchStatement b)
    {
        if (!Equals(a.Scrutinee, b.Scrutinee))
            return false;
        if (a.Cases.Length != b.Cases.Length)
            return false;
        for (int i = 0; i < a.Cases.Length; i++)
        {
            if (!Equals(a.Cases[i].Pattern, b.Cases[i].Pattern))
                return false;
            if (!NullableNodeEquals(a.Cases[i].Guard, b.Cases[i].Guard))
                return false;
            if (!StatementsEqual(a.Cases[i].Body, b.Cases[i].Body))
                return false;
        }
        return true;
    }

    private bool UnionDefEquals(UnionDef a, UnionDef b)
    {
        if (a.Name != b.Name || a.IsNameBacktickEscaped != b.IsNameBacktickEscaped)
            return false;
        if (a.DocString != b.DocString)
            return false;
        if (!TypeParametersEqual(a.TypeParameters, b.TypeParameters))
            return false;
        if (!DecoratorsEqual(a.Decorators, b.Decorators))
            return false;
        if (a.Cases.Length != b.Cases.Length)
            return false;
        for (int i = 0; i < a.Cases.Length; i++)
        {
            if (a.Cases[i].Name != b.Cases[i].Name)
                return false;
            if (a.Cases[i].Fields.Length != b.Cases[i].Fields.Length)
                return false;
            for (int j = 0; j < a.Cases[i].Fields.Length; j++)
            {
                if (a.Cases[i].Fields[j].Name != b.Cases[i].Fields[j].Name)
                    return false;
                if (!TypeAnnotationEquals(a.Cases[i].Fields[j].Type, b.Cases[i].Fields[j].Type))
                    return false;
            }
        }
        return true;
    }

    private bool DelegateDefEquals(DelegateDef a, DelegateDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.DocString == b.DocString
        && TypeParametersEqual(a.TypeParameters, b.TypeParameters)
        && ParametersEqual(a.Parameters, b.Parameters)
        && NullableTypeEquals(a.ReturnType, b.ReturnType);

    private bool EventDefEquals(EventDef a, EventDef b) =>
        a.Name == b.Name && a.IsNameBacktickEscaped == b.IsNameBacktickEscaped
        && a.Accessor == b.Accessor && a.IsFunctionStyle == b.IsFunctionStyle
        && NullableTypeEquals(a.Type, b.Type)
        && ParametersEqual(a.Parameters, b.Parameters)
        && DecoratorsEqual(a.Decorators, b.Decorators)
        && StatementsEqual(a.Body, b.Body);

    private bool BindingPatternEquals(BindingPattern a, BindingPattern b) =>
        Equals(a.Name, b.Name) && NullableTypeEquals(a.Type, b.Type);

    private bool UnionCasePatternEquals(UnionCasePattern a, UnionCasePattern b) =>
        a.CaseName == b.CaseName
        && NullableTypeEquals(a.UnionType, b.UnionType)
        && NodesEqual(a.FieldPatterns, b.FieldPatterns);

    private bool ListPatternEquals(ListPattern a, ListPattern b) =>
        NodesEqual(a.Elements, b.Elements) && NullableNodeEquals(a.RestPattern, b.RestPattern);

    private bool PropertyPatternEquals(PropertyPattern a, PropertyPattern b) =>
        NullableTypeEquals(a.Type, b.Type) && NodesEqual(a.Fields, b.Fields);

    private bool PositionalPatternEquals(PositionalPattern a, PositionalPattern b) =>
        NullableTypeEquals(a.Type, b.Type) && NodesEqual(a.Elements, b.Elements);

    #endregion

    #region Collection helpers

    private bool NodesEqual<T>(ImmutableArray<T> a, ImmutableArray<T> b) where T : Node
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (!Equals(a[i], b[i]))
                return false;
        return true;
    }

    private bool StatementsEqual(ImmutableArray<Statement> a, ImmutableArray<Statement> b) =>
        NodesEqual(a, b);

    private bool ClausesEqual(ImmutableArray<ComprehensionClause> a, ImmutableArray<ComprehensionClause> b) =>
        NodesEqual(a, b);

    private bool ComprehensionEquals(Expression elemA, ImmutableArray<ComprehensionClause> clausesA, Expression elemB, ImmutableArray<ComprehensionClause> clausesB) =>
        Equals(elemA, elemB) && ClausesEqual(clausesA, clausesB);

    private bool DictComprehensionEquals(DictComprehension a, DictComprehension b) =>
        Equals(a.Key, b.Key) && Equals(a.Value, b.Value) && ClausesEqual(a.Clauses, b.Clauses);

    private bool NullableNodeEquals(Node? a, Node? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return Equals(a, b);
    }

    private static bool NullableTypeEquals(TypeAnnotation? a, TypeAnnotation? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return TypeAnnotationEquals(a, b);
    }

    private static bool TypeAnnotationEquals(TypeAnnotation a, TypeAnnotation b)
    {
        if (a.Name != b.Name)
            return false;
        if (a.IsOptional != b.IsOptional)
            return false;
        if (a.IsCSharpNullable != b.IsCSharpNullable)
            return false;
        if (!NullableTypeEquals(a.ErrorType, b.ErrorType))
            return false;
        if (!a.TupleElementNames.SequenceEqual(b.TupleElementNames))
            return false;
        if (a.TypeArguments.Length != b.TypeArguments.Length)
            return false;
        for (int i = 0; i < a.TypeArguments.Length; i++)
            if (!TypeAnnotationEquals(a.TypeArguments[i], b.TypeArguments[i]))
                return false;
        return true;
    }

    private static bool TypeAnnotationsEqual(ImmutableArray<TypeAnnotation> a, ImmutableArray<TypeAnnotation> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (!TypeAnnotationEquals(a[i], b[i]))
                return false;
        return true;
    }

    private static bool NullableFuncTypeEquals(FunctionType? a, FunctionType? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return TypeAnnotationsEqual(a.ParameterTypes, b.ParameterTypes)
            && TypeAnnotationEquals(a.ReturnType, b.ReturnType);
    }

    private bool ParametersEqual(ImmutableArray<Parameter> a, ImmutableArray<Parameter> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
            if (a[i].IsNameBacktickEscaped != b[i].IsNameBacktickEscaped)
                return false;
            if (a[i].IsVariadic != b[i].IsVariadic)
                return false;
            if (a[i].IsLateBound != b[i].IsLateBound)
                return false;
            if (a[i].Kind != b[i].Kind)
                return false;
            if (a[i].Modifier != b[i].Modifier)
                return false;
            if (!NullableTypeEquals(a[i].Type, b[i].Type))
                return false;
            if (!NullableNodeEquals(a[i].DefaultValue, b[i].DefaultValue))
                return false;
        }
        return true;
    }

    private static bool TypeParametersEqual(ImmutableArray<TypeParameterDef> a, ImmutableArray<TypeParameterDef> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
            if (a[i].Variance != b[i].Variance)
                return false;
            if (!NullableTypeEquals(a[i].DefaultType, b[i].DefaultType))
                return false;
            if (a[i].Constraints.Length != b[i].Constraints.Length)
                return false;
            for (int j = 0; j < a[i].Constraints.Length; j++)
            {
                if (!ConstraintEquals(a[i].Constraints[j], b[i].Constraints[j]))
                    return false;
            }
        }
        return true;
    }

    private static bool ConstraintEquals(ConstraintClause a, ConstraintClause b)
    {
        if (a.GetType() != b.GetType())
            return false;
        if (a is TypeConstraint ta && b is TypeConstraint tb)
            return TypeAnnotationEquals(ta.Type, tb.Type);
        return true;
    }

    private bool DecoratorsEqual(ImmutableArray<Decorator> a, ImmutableArray<Decorator> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].QualifiedParts.SequenceEqual(b[i].QualifiedParts))
                return false;
            if (!NodesEqual(a[i].Arguments, b[i].Arguments))
                return false;
            if (!KeywordArgsEqual(a[i].KeywordArguments, b[i].KeywordArguments))
                return false;
        }
        return true;
    }

    private bool KeywordArgsEqual(ImmutableArray<KeywordArgument> a, ImmutableArray<KeywordArgument> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
            if (!Equals(a[i].Value, b[i].Value))
                return false;
        }
        return true;
    }

    private static bool ImportAliasesEqual(ImmutableArray<ImportAlias> a, ImmutableArray<ImportAlias> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name)
                return false;
            if (a[i].AsName != b[i].AsName)
                return false;
        }
        return true;
    }

    #endregion
}
