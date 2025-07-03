from typing import MutableSequence, Sequence

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
        self._root = parse_tree.accept(self)

        if not self._root:
            raise Exception("Failed to generate an AST")

        return self._root

    # Visit a parse tree produced by SharpyParser#file_input.
    def visitFile_input(self, ctx: SharpyParser.File_inputContext):
        print("Visiting file input")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statements.
    def visitStatements(self, ctx: SharpyParser.StatementsContext):
        print("Visiting statements")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statement.
    def visitStatement(self, ctx: SharpyParser.StatementContext):
        print("Visiting statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statement_newline.
    def visitStatement_newline(self, ctx: SharpyParser.Statement_newlineContext):
        print("Visiting statement newline")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#simple_stmts.
    def visitSimple_stmts(self, ctx: SharpyParser.Simple_stmtsContext):
        print("Visiting simple statements")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#simple_stmt.
    def visitSimple_stmt(self, ctx: SharpyParser.Simple_stmtContext):
        print("Visiting simple statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#compound_stmt.
    def visitCompound_stmt(self, ctx: SharpyParser.Compound_stmtContext):
        print("Visiting compound statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assignment.
    def visitAssignment(self, ctx: SharpyParser.AssignmentContext):
        print("Visiting assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#annotated_rhs.
    def visitAnnotated_rhs(self, ctx: SharpyParser.Annotated_rhsContext):
        print("Visiting annotated right-hand side")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#augassign.
    def visitAugassign(self, ctx: SharpyParser.AugassignContext):
        print("Visiting augmented assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#return_stmt.
    def visitReturn_stmt(self, ctx: SharpyParser.Return_stmtContext):
        print("Visiting return statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#raise_stmt.
    def visitRaise_stmt(self, ctx: SharpyParser.Raise_stmtContext):
        print("Visiting raise statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#global_stmt.
    def visitGlobal_stmt(self, ctx: SharpyParser.Global_stmtContext):
        print("Visiting global statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#nonlocal_stmt.
    def visitNonlocal_stmt(self, ctx: SharpyParser.Nonlocal_stmtContext):
        print("Visiting nonlocal statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_stmt.
    def visitDel_stmt(self, ctx: SharpyParser.Del_stmtContext):
        print("Visiting delete statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#yield_stmt.
    def visitYield_stmt(self, ctx: SharpyParser.Yield_stmtContext):
        print("Visiting yield statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assert_stmt.
    def visitAssert_stmt(self, ctx: SharpyParser.Assert_stmtContext):
        print("Visiting assert statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#import_stmt.
    def visitImport_stmt(self, ctx: SharpyParser.Import_stmtContext) -> Node | None:
        print("Visiting import statement")
        # import_stmt: import_name | import_from

        if ctx.import_name():
            return self.visit(ctx.import_name())
        elif ctx.import_from():
            return self.visit(ctx.import_from())

        return None

    def visitImport_name(self, ctx: SharpyParser.Import_nameContext) -> Import:
        print("Visiting import name")
        # import_name: 'import' dotted_as_names

        dotted_as_names = self.visit(ctx.dotted_as_names())

        return Import(names=dotted_as_names)

    def visitImport_from(self, ctx: SharpyParser.Import_fromContext) -> ImportFrom:
        print("Visiting import from")
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
        print("Visiting import from targets")
        # import_from_targets: '(' import_from_as_names ','? ')' | import_from_as_names | '*'

        if ctx.getText() == "*":
            return ["*"]

        if ctx.import_from_as_names():
            return self.visit(ctx.import_from_as_names())

        return []

    def visitImport_from_as_names(self, ctx: SharpyParser.Import_from_as_namesContext):
        print("Visiting import from as names")
        # import_from_as_names: import_from_as_name (',' import_from_as_name)*

        return [self.visit(child) for child in ctx.import_from_as_name()]

    def visitImport_from_as_name(self, ctx: SharpyParser.Import_from_as_nameContext) -> alias:
        print("Visiting import from as name")
        # import_from_as_name: name ('as' name)?

        name: str = ctx.name(0).getText()
        asname: str | None = ctx.name(1).getText() if ctx.name(1) else None

        return alias(name=name, asname=asname)

    def visitDotted_as_names(self, ctx: SharpyParser.Dotted_as_namesContext):
        print("Visiting dotted as names")
        # dotted_as_names: dotted_as_name (',' dotted_as_name)*

        return [self.visit(child) for child in ctx.dotted_as_name()]

    def visitDotted_as_name(self, ctx: SharpyParser.Dotted_as_nameContext) -> alias:
        print("Visiting dotted as name")
        # dotted_as_name: dotted_name ('as' name)?

        name: str = ctx.dotted_name().getText()
        asname: str | None = ctx.name().getText() if ctx.name() else None

        return alias(name=name, asname=asname)

    def visitDotted_name(self, ctx: SharpyParser.Dotted_nameContext) -> str:
        print("Visiting dotted name")
        # dotted_name: dotted_name '.' name | name

        # Return the full dotted name as a string
        return ctx.getText()

    # Visit a parse tree produced by SharpyParser#block.
    def visitBlock(self, ctx: SharpyParser.BlockContext):
        print("Visiting block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#decorators.
    def visitDecorators(self, ctx: SharpyParser.DecoratorsContext):
        print("Visiting decorators")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_def.
    def visitClass_def(self, ctx: SharpyParser.Class_defContext):
        print("Visiting class definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_def_raw.
    def visitClass_def_raw(self, ctx: SharpyParser.Class_def_rawContext):
        print("Visiting class definition (raw)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#struct_def.
    def visitStruct_def(self, ctx: SharpyParser.Struct_defContext):
        print("Visiting struct definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#struct_def_raw.
    def visitStruct_def_raw(self, ctx: SharpyParser.Struct_def_rawContext):
        print("Visiting struct definition (raw)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#protocol_def.
    def visitProtocol_def(self, ctx: SharpyParser.Protocol_defContext):
        print("Visiting protocol definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#protocol_def_raw.
    def visitProtocol_def_raw(self, ctx: SharpyParser.Protocol_def_rawContext):
        print("Visiting protocol definition (raw)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def.
    def visitFunction_def(self, ctx: SharpyParser.Function_defContext):
        print("Visiting function definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def_raw.
    def visitFunction_def_raw(self, ctx: SharpyParser.Function_def_rawContext):
        print("Visiting function definition (raw)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#params.
    def visitParams(self, ctx: SharpyParser.ParamsContext):
        print("Visiting parameters")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#parameters.
    def visitParameters(self, ctx: SharpyParser.ParametersContext):
        print("Visiting parameter list")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#slash_no_default.
    def visitSlash_no_default(self, ctx: SharpyParser.Slash_no_defaultContext):
        print("Visiting slash without default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#slash_with_default.
    def visitSlash_with_default(self, ctx: SharpyParser.Slash_with_defaultContext):
        print("Visiting slash with default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_etc.
    def visitStar_etc(self, ctx: SharpyParser.Star_etcContext):
        print("Visiting star etc")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#kwds.
    def visitKwds(self, ctx: SharpyParser.KwdsContext):
        print("Visiting keyword arguments")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_no_default.
    def visitParam_no_default(self, ctx: SharpyParser.Param_no_defaultContext):
        print("Visiting parameter without default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_no_default_star_annotation.
    def visitParam_no_default_star_annotation(
        self, ctx: SharpyParser.Param_no_default_star_annotationContext
    ):
        print("Visiting parameter without default (star annotation)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_with_default.
    def visitParam_with_default(self, ctx: SharpyParser.Param_with_defaultContext):
        print("Visiting parameter with default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_maybe_default.
    def visitParam_maybe_default(self, ctx: SharpyParser.Param_maybe_defaultContext):
        print("Visiting parameter maybe with default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param.
    def visitParam(self, ctx: SharpyParser.ParamContext):
        print("Visiting parameter")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_star_annotation.
    def visitParam_star_annotation(self, ctx: SharpyParser.Param_star_annotationContext):
        print("Visiting parameter (star annotation)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#annotation.
    def visitAnnotation(self, ctx: SharpyParser.AnnotationContext):
        print("Visiting annotation")
        # annotation: '@' dotted_name ('.' name)* ('.' '(' ')')
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#star_annotation.
    def visitStar_annotation(self, ctx: SharpyParser.Star_annotationContext):
        print("Visiting star annotation")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#default_assignment.
    def visitDefault_assignment(self, ctx: SharpyParser.Default_assignmentContext):
        print("Visiting default assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#if_stmt.
    def visitIf_stmt(self, ctx: SharpyParser.If_stmtContext):
        print("Visiting if statement")
        # if_stmt: 'if' expression ':' block (elif_stmt | else_block)*
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#elif_stmt.
    def visitElif_stmt(self, ctx: SharpyParser.Elif_stmtContext):
        print("Visiting elif statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#else_block.
    def visitElse_block(self, ctx: SharpyParser.Else_blockContext):
        print("Visiting else block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#while_stmt.
    def visitWhile_stmt(self, ctx: SharpyParser.While_stmtContext):
        print("Visiting while statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#for_stmt.
    def visitFor_stmt(self, ctx: SharpyParser.For_stmtContext):
        print("Visiting for statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_stmt.
    def visitWith_stmt(self, ctx: SharpyParser.With_stmtContext):
        print("Visiting with statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_item.
    def visitWith_item(self, ctx: SharpyParser.With_itemContext):
        print("Visiting with item")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#try_stmt.
    def visitTry_stmt(self, ctx: SharpyParser.Try_stmtContext):
        print("Visiting try statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_block.
    def visitExcept_block(self, ctx: SharpyParser.Except_blockContext):
        print("Visiting except block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_star_block.
    def visitExcept_star_block(self, ctx: SharpyParser.Except_star_blockContext):
        print("Visiting except star block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#finally_block.
    def visitFinally_block(self, ctx: SharpyParser.Finally_blockContext):
        print("Visiting finally block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#match_stmt.
    def visitMatch_stmt(self, ctx: SharpyParser.Match_stmtContext):
        print("Visiting match statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#subject_expr.
    def visitSubject_expr(self, ctx: SharpyParser.Subject_exprContext):
        print("Visiting subject expression")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#case_block.
    def visitCase_block(self, ctx: SharpyParser.Case_blockContext):
        print("Visiting case block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#guard.
    def visitGuard(self, ctx: SharpyParser.GuardContext):
        print("Visiting guard")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#patterns.
    def visitPatterns(self, ctx: SharpyParser.PatternsContext):
        print("Visiting patterns")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#pattern.
    def visitPattern(self, ctx: SharpyParser.PatternContext):
        print("Visiting pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#as_pattern.
    def visitAs_pattern(self, ctx: SharpyParser.As_patternContext):
        print("Visiting as pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#or_pattern.
    def visitOr_pattern(self, ctx: SharpyParser.Or_patternContext):
        print("Visiting or pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#closed_pattern.
    def visitClosed_pattern(self, ctx: SharpyParser.Closed_patternContext):
        print("Visiting closed pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#literal_pattern.
    def visitLiteral_pattern(self, ctx: SharpyParser.Literal_patternContext):
        print("Visiting literal pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#literal_expr.
    def visitLiteral_expr(self, ctx: SharpyParser.Literal_exprContext):
        print("Visiting literal expression")
        # literal_expr: string | complex_number | signed_number | 'None' | 'True' | 'False'

        if ctx.strings():
            return self.visit(ctx.strings())
        elif ctx.complex_number():
            return self.visit(ctx.complex_number())
        elif ctx.signed_number():
            return self.visit(ctx.signed_number())
        elif ctx.getText() == "None":
            return Constant(value=None)
        elif ctx.getText() == "True":
            return Constant(value=True)
        elif ctx.getText() == "False":
            return Constant(value=False)

        return None

    # Visit a parse tree produced by SharpyParser#complex_number.
    def visitComplex_number(self, ctx: SharpyParser.Complex_numberContext):
        print("Visiting complex number")
        # complex_number: signed_real_number? imaginary_number

        text: str = ctx.getText()

        try:
            value = complex(text.replace("J", "j"))
        except Exception as e:
            raise ValueError(f"Invalid complex number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#signed_number.
    def visitSigned_number(self, ctx: SharpyParser.Signed_numberContext):
        print("Visiting signed number")
        # signed_number: ('+' | '-')? real_number

        text: str = ctx.getText()

        try:
            if "." in text or "e" in text or "E" in text:
                value = float(text)
            else:
                value = int(text)
        except Exception as e:
            raise ValueError(f"Invalid signed number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#real_number.
    def visitReal_number(self, ctx: SharpyParser.Real_numberContext):
        print("Visiting real number")
        text: str = ctx.getText()

        try:
            if "." in text or "e" in text or "E" in text:
                value = float(text)
            else:
                value = int(text)
        except Exception as e:
            raise ValueError(f"Invalid real number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#imaginary_number.
    def visitImaginary_number(self, ctx: SharpyParser.Imaginary_numberContext):
        print("Visiting imaginary number")
        text: str = ctx.getText()

        try:
            value = complex(text.replace("J", "j"))
        except Exception as e:
            raise ValueError(f"Invalid imaginary number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#list.
    def visitList(self, ctx: SharpyParser.ListContext):
        print("Visiting list")
        # list: '[' star_named_expressions? ']'

        elts: Sequence[Node] = []
        if ctx.star_named_expressions():
            elts: Sequence[Node] = self.visit(ctx.star_named_expressions())

        return List(elts=elts, ctx=Load())

    # Visit a parse tree produced by SharpyParser#tuple.
    def visitTuple(self, ctx: SharpyParser.TupleContext):
        print("Visiting tuple")
        # tuple: '(' star_named_expressions? ')'

        elts: Sequence[Node] = []

        if ctx.star_named_expressions():
            elts: Sequence[Node] = self.visit(ctx.star_named_expressions())

        return Tuple(elts=elts, ctx=Load())

    # Visit a parse tree produced by SharpyParser#set.
    def visitSet(self, ctx: SharpyParser.SetContext):
        print("Visiting set")
        # set: '{' star_named_expressions '}'

        elts: Sequence[Node] = (
            self.visit(ctx.star_named_expressions()) if ctx.star_named_expressions() else []
        )

        return Set(elts=elts)

    # Visit a parse tree produced by SharpyParser#dict.
    def visitDict(self, ctx: SharpyParser.DictContext):
        print("Visiting dict")
        # dict: '{' double_starred_kvpairs? '}'

        keys: MutableSequence[Node] = []
        values: MutableSequence[Node] = []

        if ctx.double_starred_kvpairs():
            kvpairs = self.visit(ctx.double_starred_kvpairs())

            for k, v in kvpairs:
                keys.append(k)
                values.append(v)

        return Dict(keys=keys, values=values)

    # Visit a parse tree produced by SharpyParser#double_starred_kvpairs.
    def visitDouble_starred_kvpairs(self, ctx: SharpyParser.Double_starred_kvpairsContext):
        print("Visiting double-starred key-value pairs")
        # double_starred_kvpairs: double_starred_kvpair (',' double_starred_kvpair)* ','?

        return [self.visit(child) for child in ctx.double_starred_kvpair()]

    # Visit a parse tree produced by SharpyParser#double_starred_kvpair.
    def visitDouble_starred_kvpair(self, ctx: SharpyParser.Double_starred_kvpairContext):
        print("Visiting double-starred key-value pair")
        # double_starred_kvpair: kvpair | '**' expression

        if ctx.kvpair():
            return self.visit(ctx.kvpair())
        else:
            # For dict literals, ignore **expr for now (not a literal)
            return (None, None)

    # Visit a parse tree produced by SharpyParser#kvpair.
    def visitKvpair(self, ctx: SharpyParser.KvpairContext):
        print("Visiting key-value pair")
        # kvpair: expression ':' expression

        key = self.visit(ctx.expression(0))
        value = self.visit(ctx.expression(1))

        return (key, value)

    # Visit a parse tree produced by SharpyParser#strings.
    def visitStrings(self, ctx: SharpyParser.StringsContext):
        print("Visiting strings")
        # strings: string+

        # Concatenate all string tokens (Python does this for adjacent strings)
        string_nodes = [self.visit(child) for child in ctx.getChildren()]

        # If each is a Constant, concatenate their values
        value = "".join(str(node.value) for node in string_nodes if hasattr(node, "value"))

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#star_named_expressions.
    def visitStar_named_expressions(self, ctx: SharpyParser.Star_named_expressionsContext):
        print("Visiting star named expressions")
        # star_named_expressions: star_named_expression (',' star_named_expression)* ','?

        return [self.visit(child) for child in ctx.star_named_expression()]

    # Visit a parse tree produced by SharpyParser#star_named_expression.
    def visitStar_named_expression(self, ctx: SharpyParser.Star_named_expressionContext):
        print("Visiting star named expression")
        # star_named_expression: expression | '*' expression

        # For now, just return the expression (ignore starred for literals)
        if ctx.getChildCount() == 2 and ctx.getChild(0).getText() == "*":
            # Starred expression, e.g. *a
            return self.visit(ctx.named_expression())
        else:
            return self.visit(ctx.named_expression())
