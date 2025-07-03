from SharpyParser import SharpyParser
from SharpyParserVisitor import SharpyParserVisitor

from sharpy.compiler_toolchain.abc.ast_builder import ASTBuilder
from sharpy.compiler_toolchain.ast import *

from ..antlr import ParseTreeNode


class AntlrASTBuilder(ASTBuilder, SharpyParserVisitor):
    def __init__(self):
        super().__init__()

        self._root: Node | None = None

    def _generate_ast(self, parse_tree: ParseTreeNode) -> Node:
        parse_tree.accept(self)

        if not self._root:
            raise Exception("Failed to generate an AST")

        return self._root

    # Visit a parse tree produced by SharpyParser#file_input.
    def visitFile_input(self, ctx: SharpyParser.File_inputContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statements.
    def visitStatements(self, ctx: SharpyParser.StatementsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statement.
    def visitStatement(self, ctx: SharpyParser.StatementContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statement_newline.
    def visitStatement_newline(self, ctx: SharpyParser.Statement_newlineContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#simple_stmts.
    def visitSimple_stmts(self, ctx: SharpyParser.Simple_stmtsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#simple_stmt.
    def visitSimple_stmt(self, ctx: SharpyParser.Simple_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#compound_stmt.
    def visitCompound_stmt(self, ctx: SharpyParser.Compound_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assignment.
    def visitAssignment(self, ctx: SharpyParser.AssignmentContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#annotated_rhs.
    def visitAnnotated_rhs(self, ctx: SharpyParser.Annotated_rhsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#augassign.
    def visitAugassign(self, ctx: SharpyParser.AugassignContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#return_stmt.
    def visitReturn_stmt(self, ctx: SharpyParser.Return_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#raise_stmt.
    def visitRaise_stmt(self, ctx: SharpyParser.Raise_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#global_stmt.
    def visitGlobal_stmt(self, ctx: SharpyParser.Global_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#nonlocal_stmt.
    def visitNonlocal_stmt(self, ctx: SharpyParser.Nonlocal_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_stmt.
    def visitDel_stmt(self, ctx: SharpyParser.Del_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#yield_stmt.
    def visitYield_stmt(self, ctx: SharpyParser.Yield_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assert_stmt.
    def visitAssert_stmt(self, ctx: SharpyParser.Assert_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#import_stmt.
    def visitImport_stmt(self, ctx: SharpyParser.Import_stmtContext) -> Node | None:
        # import_stmt: import_name | import_from

        if ctx.import_name():
            return self.visit(ctx.import_name())
        elif ctx.import_from():
            return self.visit(ctx.import_from())

        return None

    def visitImport_name(self, ctx: SharpyParser.Import_nameContext) -> Import:
        # import_name: 'import' dotted_as_names

        dotted_as_names = self.visit(ctx.dotted_as_names())

        return Import(names=dotted_as_names)

    def visitImport_from(self, ctx: SharpyParser.Import_fromContext) -> ImportFrom:
        # import_from: 'from' ('.' | '...')* dotted_name 'import' import_from_targets
        #            | 'from' ('.' | '...')+ 'import' import_from_targets

        # Calculate level (number of dots)
        level: int = 0
        for child in ctx.children:
            if child.getText() == ".":
                level += 1
            elif child.getText() == "...":
                level += 3
            else:
                break

        # Get module name if present
        module: str | None = None

        if ctx.dotted_name():
            module: str | None = ctx.dotted_name().getText()

        # Get imported names
        names: Sequence[alias] = self.visit(ctx.import_from_targets())

        return ImportFrom(module=module, names=names, level=level)

    def visitImport_from_targets(self, ctx: SharpyParser.Import_from_targetsContext):
        # import_from_targets: '(' import_from_as_names ','? ')' | import_from_as_names | '*'

        if ctx.getText() == "*":
            return ["*"]

        if ctx.import_from_as_names():
            return self.visit(ctx.import_from_as_names())

        return []

    def visitImport_from_as_names(self, ctx: SharpyParser.Import_from_as_namesContext):
        # import_from_as_names: import_from_as_name (',' import_from_as_name)*

        return [self.visit(child) for child in ctx.import_from_as_name()]

    def visitImport_from_as_name(self, ctx: SharpyParser.Import_from_as_nameContext) -> alias:
        # import_from_as_name: name ('as' name)?

        name: str = ctx.name(0).getText()
        asname: str | None = ctx.name(1).getText() if ctx.name(1) else None

        return alias(name=name, asname=asname)

    def visitDotted_as_names(self, ctx: SharpyParser.Dotted_as_namesContext):
        # dotted_as_names: dotted_as_name (',' dotted_as_name)*

        return [self.visit(child) for child in ctx.dotted_as_name()]

    def visitDotted_as_name(self, ctx: SharpyParser.Dotted_as_nameContext) -> alias:
        # dotted_as_name: dotted_name ('as' name)?

        name: str = ctx.dotted_name().getText()
        asname: str | None = ctx.name().getText() if ctx.name() else None

        return alias(name=name, asname=asname)

    def visitDotted_name(self, ctx: SharpyParser.Dotted_nameContext) -> str:
        # dotted_name: dotted_name '.' name | name

        # Return the full dotted name as a string
        return ctx.getText()

    # Visit a parse tree produced by SharpyParser#block.
    def visitBlock(self, ctx: SharpyParser.BlockContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#decorators.
    def visitDecorators(self, ctx: SharpyParser.DecoratorsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_def.
    def visitClass_def(self, ctx: SharpyParser.Class_defContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_def_raw.
    def visitClass_def_raw(self, ctx: SharpyParser.Class_def_rawContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#struct_def.
    def visitStruct_def(self, ctx: SharpyParser.Struct_defContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#struct_def_raw.
    def visitStruct_def_raw(self, ctx: SharpyParser.Struct_def_rawContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#protocol_def.
    def visitProtocol_def(self, ctx: SharpyParser.Protocol_defContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#protocol_def_raw.
    def visitProtocol_def_raw(self, ctx: SharpyParser.Protocol_def_rawContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def.
    def visitFunction_def(self, ctx: SharpyParser.Function_defContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def_raw.
    def visitFunction_def_raw(self, ctx: SharpyParser.Function_def_rawContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#params.
    def visitParams(self, ctx: SharpyParser.ParamsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#parameters.
    def visitParameters(self, ctx: SharpyParser.ParametersContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#slash_no_default.
    def visitSlash_no_default(self, ctx: SharpyParser.Slash_no_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#slash_with_default.
    def visitSlash_with_default(self, ctx: SharpyParser.Slash_with_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_etc.
    def visitStar_etc(self, ctx: SharpyParser.Star_etcContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#kwds.
    def visitKwds(self, ctx: SharpyParser.KwdsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_no_default.
    def visitParam_no_default(self, ctx: SharpyParser.Param_no_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_no_default_star_annotation.
    def visitParam_no_default_star_annotation(
        self, ctx: SharpyParser.Param_no_default_star_annotationContext
    ):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_with_default.
    def visitParam_with_default(self, ctx: SharpyParser.Param_with_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_maybe_default.
    def visitParam_maybe_default(self, ctx: SharpyParser.Param_maybe_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param.
    def visitParam(self, ctx: SharpyParser.ParamContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_star_annotation.
    def visitParam_star_annotation(self, ctx: SharpyParser.Param_star_annotationContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#annotation.
    def visitAnnotation(self, ctx: SharpyParser.AnnotationContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_annotation.
    def visitStar_annotation(self, ctx: SharpyParser.Star_annotationContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#default_assignment.
    def visitDefault_assignment(self, ctx: SharpyParser.Default_assignmentContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#if_stmt.
    def visitIf_stmt(self, ctx: SharpyParser.If_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#elif_stmt.
    def visitElif_stmt(self, ctx: SharpyParser.Elif_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#else_block.
    def visitElse_block(self, ctx: SharpyParser.Else_blockContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#while_stmt.
    def visitWhile_stmt(self, ctx: SharpyParser.While_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#for_stmt.
    def visitFor_stmt(self, ctx: SharpyParser.For_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_stmt.
    def visitWith_stmt(self, ctx: SharpyParser.With_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_item.
    def visitWith_item(self, ctx: SharpyParser.With_itemContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#try_stmt.
    def visitTry_stmt(self, ctx: SharpyParser.Try_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_block.
    def visitExcept_block(self, ctx: SharpyParser.Except_blockContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_star_block.
    def visitExcept_star_block(self, ctx: SharpyParser.Except_star_blockContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#finally_block.
    def visitFinally_block(self, ctx: SharpyParser.Finally_blockContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#match_stmt.
    def visitMatch_stmt(self, ctx: SharpyParser.Match_stmtContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#subject_expr.
    def visitSubject_expr(self, ctx: SharpyParser.Subject_exprContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#case_block.
    def visitCase_block(self, ctx: SharpyParser.Case_blockContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#guard.
    def visitGuard(self, ctx: SharpyParser.GuardContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#patterns.
    def visitPatterns(self, ctx: SharpyParser.PatternsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#pattern.
    def visitPattern(self, ctx: SharpyParser.PatternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#as_pattern.
    def visitAs_pattern(self, ctx: SharpyParser.As_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#or_pattern.
    def visitOr_pattern(self, ctx: SharpyParser.Or_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#closed_pattern.
    def visitClosed_pattern(self, ctx: SharpyParser.Closed_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#literal_pattern.
    def visitLiteral_pattern(self, ctx: SharpyParser.Literal_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#literal_expr.
    def visitLiteral_expr(self, ctx: SharpyParser.Literal_exprContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#complex_number.
    def visitComplex_number(self, ctx: SharpyParser.Complex_numberContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#signed_number.
    def visitSigned_number(self, ctx: SharpyParser.Signed_numberContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#signed_real_number.
    def visitSigned_real_number(self, ctx: SharpyParser.Signed_real_numberContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#real_number.
    def visitReal_number(self, ctx: SharpyParser.Real_numberContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#imaginary_number.
    def visitImaginary_number(self, ctx: SharpyParser.Imaginary_numberContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#capture_pattern.
    def visitCapture_pattern(self, ctx: SharpyParser.Capture_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#pattern_capture_target.
    def visitPattern_capture_target(self, ctx: SharpyParser.Pattern_capture_targetContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#wildcard_pattern.
    def visitWildcard_pattern(self, ctx: SharpyParser.Wildcard_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#value_pattern.
    def visitValue_pattern(self, ctx: SharpyParser.Value_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#attr.
    def visitAttr(self, ctx: SharpyParser.AttrContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#name_or_attr.
    def visitName_or_attr(self, ctx: SharpyParser.Name_or_attrContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#group_pattern.
    def visitGroup_pattern(self, ctx: SharpyParser.Group_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#sequence_pattern.
    def visitSequence_pattern(self, ctx: SharpyParser.Sequence_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#open_sequence_pattern.
    def visitOpen_sequence_pattern(self, ctx: SharpyParser.Open_sequence_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#maybe_sequence_pattern.
    def visitMaybe_sequence_pattern(self, ctx: SharpyParser.Maybe_sequence_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#maybe_star_pattern.
    def visitMaybe_star_pattern(self, ctx: SharpyParser.Maybe_star_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_pattern.
    def visitStar_pattern(self, ctx: SharpyParser.Star_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#mapping_pattern.
    def visitMapping_pattern(self, ctx: SharpyParser.Mapping_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#items_pattern.
    def visitItems_pattern(self, ctx: SharpyParser.Items_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#key_value_pattern.
    def visitKey_value_pattern(self, ctx: SharpyParser.Key_value_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#double_star_pattern.
    def visitDouble_star_pattern(self, ctx: SharpyParser.Double_star_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_pattern.
    def visitClass_pattern(self, ctx: SharpyParser.Class_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#positional_patterns.
    def visitPositional_patterns(self, ctx: SharpyParser.Positional_patternsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#keyword_patterns.
    def visitKeyword_patterns(self, ctx: SharpyParser.Keyword_patternsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#keyword_pattern.
    def visitKeyword_pattern(self, ctx: SharpyParser.Keyword_patternContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_alias.
    def visitType_alias(self, ctx: SharpyParser.Type_aliasContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_params.
    def visitType_params(self, ctx: SharpyParser.Type_paramsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_param_seq.
    def visitType_param_seq(self, ctx: SharpyParser.Type_param_seqContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_param.
    def visitType_param(self, ctx: SharpyParser.Type_paramContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_param_bound.
    def visitType_param_bound(self, ctx: SharpyParser.Type_param_boundContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_param_default.
    def visitType_param_default(self, ctx: SharpyParser.Type_param_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_param_starred_default.
    def visitType_param_starred_default(self, ctx: SharpyParser.Type_param_starred_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#expressions.
    def visitExpressions(self, ctx: SharpyParser.ExpressionsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#expression.
    def visitExpression(self, ctx: SharpyParser.ExpressionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#yield_expr.
    def visitYield_expr(self, ctx: SharpyParser.Yield_exprContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_expressions.
    def visitStar_expressions(self, ctx: SharpyParser.Star_expressionsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_expression.
    def visitStar_expression(self, ctx: SharpyParser.Star_expressionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_named_expressions.
    def visitStar_named_expressions(self, ctx: SharpyParser.Star_named_expressionsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_named_expression.
    def visitStar_named_expression(self, ctx: SharpyParser.Star_named_expressionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assignment_expression.
    def visitAssignment_expression(self, ctx: SharpyParser.Assignment_expressionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#named_expression.
    def visitNamed_expression(self, ctx: SharpyParser.Named_expressionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#disjunction.
    def visitDisjunction(self, ctx: SharpyParser.DisjunctionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#conjunction.
    def visitConjunction(self, ctx: SharpyParser.ConjunctionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#inversion.
    def visitInversion(self, ctx: SharpyParser.InversionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#comparison.
    def visitComparison(self, ctx: SharpyParser.ComparisonContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#compare_op_bitwise_or_pair.
    def visitCompare_op_bitwise_or_pair(self, ctx: SharpyParser.Compare_op_bitwise_or_pairContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#eq_bitwise_or.
    def visitEq_bitwise_or(self, ctx: SharpyParser.Eq_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#noteq_bitwise_or.
    def visitNoteq_bitwise_or(self, ctx: SharpyParser.Noteq_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lte_bitwise_or.
    def visitLte_bitwise_or(self, ctx: SharpyParser.Lte_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lt_bitwise_or.
    def visitLt_bitwise_or(self, ctx: SharpyParser.Lt_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#gte_bitwise_or.
    def visitGte_bitwise_or(self, ctx: SharpyParser.Gte_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#gt_bitwise_or.
    def visitGt_bitwise_or(self, ctx: SharpyParser.Gt_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#notin_bitwise_or.
    def visitNotin_bitwise_or(self, ctx: SharpyParser.Notin_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#in_bitwise_or.
    def visitIn_bitwise_or(self, ctx: SharpyParser.In_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#isnot_bitwise_or.
    def visitIsnot_bitwise_or(self, ctx: SharpyParser.Isnot_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#is_bitwise_or.
    def visitIs_bitwise_or(self, ctx: SharpyParser.Is_bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#bitwise_or.
    def visitBitwise_or(self, ctx: SharpyParser.Bitwise_orContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#bitwise_xor.
    def visitBitwise_xor(self, ctx: SharpyParser.Bitwise_xorContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#bitwise_and.
    def visitBitwise_and(self, ctx: SharpyParser.Bitwise_andContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#shift_expr.
    def visitShift_expr(self, ctx: SharpyParser.Shift_exprContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#sum.
    def visitSum(self, ctx: SharpyParser.SumContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#term.
    def visitTerm(self, ctx: SharpyParser.TermContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#factor.
    def visitFactor(self, ctx: SharpyParser.FactorContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#power.
    def visitPower(self, ctx: SharpyParser.PowerContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#await_primary.
    def visitAwait_primary(self, ctx: SharpyParser.Await_primaryContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#primary.
    def visitPrimary(self, ctx: SharpyParser.PrimaryContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#slices.
    def visitSlices(self, ctx: SharpyParser.SlicesContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#slice.
    def visitSlice(self, ctx: SharpyParser.SliceContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#atom.
    def visitAtom(self, ctx: SharpyParser.AtomContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#group.
    def visitGroup(self, ctx: SharpyParser.GroupContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambdef.
    def visitLambdef(self, ctx: SharpyParser.LambdefContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_params.
    def visitLambda_params(self, ctx: SharpyParser.Lambda_paramsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_parameters.
    def visitLambda_parameters(self, ctx: SharpyParser.Lambda_parametersContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_slash_no_default.
    def visitLambda_slash_no_default(self, ctx: SharpyParser.Lambda_slash_no_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_slash_with_default.
    def visitLambda_slash_with_default(self, ctx: SharpyParser.Lambda_slash_with_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_star_etc.
    def visitLambda_star_etc(self, ctx: SharpyParser.Lambda_star_etcContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_kwds.
    def visitLambda_kwds(self, ctx: SharpyParser.Lambda_kwdsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_param_no_default.
    def visitLambda_param_no_default(self, ctx: SharpyParser.Lambda_param_no_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_param_with_default.
    def visitLambda_param_with_default(self, ctx: SharpyParser.Lambda_param_with_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_param_maybe_default.
    def visitLambda_param_maybe_default(self, ctx: SharpyParser.Lambda_param_maybe_defaultContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#lambda_param.
    def visitLambda_param(self, ctx: SharpyParser.Lambda_paramContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#fstring_middle.
    def visitFstring_middle(self, ctx: SharpyParser.Fstring_middleContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#fstring_replacement_field.
    def visitFstring_replacement_field(self, ctx: SharpyParser.Fstring_replacement_fieldContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#fstring_conversion.
    def visitFstring_conversion(self, ctx: SharpyParser.Fstring_conversionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#fstring_full_format_spec.
    def visitFstring_full_format_spec(self, ctx: SharpyParser.Fstring_full_format_specContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#fstring_format_spec.
    def visitFstring_format_spec(self, ctx: SharpyParser.Fstring_format_specContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#fstring.
    def visitFstring(self, ctx: SharpyParser.FstringContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#string.
    def visitString(self, ctx: SharpyParser.StringContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#strings.
    def visitStrings(self, ctx: SharpyParser.StringsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#list.
    def visitList(self, ctx: SharpyParser.ListContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#tuple.
    def visitTuple(self, ctx: SharpyParser.TupleContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#set.
    def visitSet(self, ctx: SharpyParser.SetContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#dict.
    def visitDict(self, ctx: SharpyParser.DictContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#double_starred_kvpairs.
    def visitDouble_starred_kvpairs(self, ctx: SharpyParser.Double_starred_kvpairsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#double_starred_kvpair.
    def visitDouble_starred_kvpair(self, ctx: SharpyParser.Double_starred_kvpairContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#kvpair.
    def visitKvpair(self, ctx: SharpyParser.KvpairContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#for_if_clauses.
    def visitFor_if_clauses(self, ctx: SharpyParser.For_if_clausesContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#for_if_clause.
    def visitFor_if_clause(self, ctx: SharpyParser.For_if_clauseContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#listcomp.
    def visitListcomp(self, ctx: SharpyParser.ListcompContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#setcomp.
    def visitSetcomp(self, ctx: SharpyParser.SetcompContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#genexp.
    def visitGenexp(self, ctx: SharpyParser.GenexpContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#dictcomp.
    def visitDictcomp(self, ctx: SharpyParser.DictcompContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#arguments.
    def visitArguments(self, ctx: SharpyParser.ArgumentsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#args.
    def visitArgs(self, ctx: SharpyParser.ArgsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#kwargs.
    def visitKwargs(self, ctx: SharpyParser.KwargsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#starred_expression.
    def visitStarred_expression(self, ctx: SharpyParser.Starred_expressionContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#kwarg_or_starred.
    def visitKwarg_or_starred(self, ctx: SharpyParser.Kwarg_or_starredContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#kwarg_or_double_starred.
    def visitKwarg_or_double_starred(self, ctx: SharpyParser.Kwarg_or_double_starredContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_targets.
    def visitStar_targets(self, ctx: SharpyParser.Star_targetsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_targets_list_seq.
    def visitStar_targets_list_seq(self, ctx: SharpyParser.Star_targets_list_seqContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_targets_tuple_seq.
    def visitStar_targets_tuple_seq(self, ctx: SharpyParser.Star_targets_tuple_seqContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_target.
    def visitStar_target(self, ctx: SharpyParser.Star_targetContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#target_with_star_atom.
    def visitTarget_with_star_atom(self, ctx: SharpyParser.Target_with_star_atomContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_atom.
    def visitStar_atom(self, ctx: SharpyParser.Star_atomContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#single_target.
    def visitSingle_target(self, ctx: SharpyParser.Single_targetContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#single_subscript_attribute_target.
    def visitSingle_subscript_attribute_target(
        self, ctx: SharpyParser.Single_subscript_attribute_targetContext
    ):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#t_primary.
    def visitT_primary(self, ctx: SharpyParser.T_primaryContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_targets.
    def visitDel_targets(self, ctx: SharpyParser.Del_targetsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_target.
    def visitDel_target(self, ctx: SharpyParser.Del_targetContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_t_atom.
    def visitDel_t_atom(self, ctx: SharpyParser.Del_t_atomContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#type_expressions.
    def visitType_expressions(self, ctx: SharpyParser.Type_expressionsContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#func_type_comment.
    def visitFunc_type_comment(self, ctx: SharpyParser.Func_type_commentContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#name_except_underscore.
    def visitName_except_underscore(self, ctx: SharpyParser.Name_except_underscoreContext):
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#name.
    def visitName(self, ctx: SharpyParser.NameContext):
        return self.visitChildren(ctx)
