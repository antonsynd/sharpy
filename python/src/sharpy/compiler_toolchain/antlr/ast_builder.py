from typing import MutableSequence, Sequence

from SharpyParser import SharpyParser
from SharpyParserVisitor import SharpyParserVisitor

from sharpy.compiler_toolchain.abc.ast_builder import ASTBuilder
from sharpy.compiler_toolchain.ast import *
from sharpy.compiler_toolchain.logging import logger

from ..antlr import ParseTreeNode


def stringify_string_constant(ctx: Constant) -> str | None:
    """
    Convert a string constant to a Python string literal. Handles both
    single and double quotes. Returns None if none of the preconditions
    are met.
    """
    if isinstance(ctx, Constant):
        text = ctx.value()

        if isinstance(text, str):
            # If it's a string, return it without the quotes
            if text.startswith("'") or text.startswith('"'):
                return text[1:-1]

    return None


class AntlrASTBuilder(ASTBuilder, SharpyParserVisitor):
    def __init__(self):
        super().__init__()

        self._root: Node | None = None

    def _generate_ast(self, parse_tree: ParseTreeNode) -> Node:
        self._root = parse_tree.accept(self)

        if not self._root:
            raise Exception("Failed to generate an AST")

        return self._root

    # Visit a parse tree produced by SharpyParser#annotated_rhs.
    def visitAnnotated_rhs(self, ctx: SharpyParser.Annotated_rhsContext):
        logger.debug("Visiting annotated right-hand side")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#as_pattern.
    def visitAs_pattern(self, ctx: SharpyParser.As_patternContext):
        logger.debug("Visiting as pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assert_statement.
    def visitAssert_statement(self, ctx: SharpyParser.Assert_statementContext):
        logger.debug("Visiting assert statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assignment.
    def visitAssignment(self, ctx: SharpyParser.AssignmentContext):
        logger.debug("Visiting assignment")
        return self.visitChildren(ctx)

    def visitAtom(self, ctx: SharpyParser.AtomContext) -> Node:
        logger.debug("Visiting atom")
        logger.debug(f"Atom value: {ctx.getText()}")

        if ctx.name():
            return self.visit(ctx.name())
        if ctx.ellipsis_literal():
            return self.visit(ctx.ellipsis_literal())
        if ctx.none_literal():
            return self.visit(ctx.none_literal())
        if ctx.true_literal():
            return self.visit(ctx.true_literal())
        if ctx.false_literal():
            return self.visit(ctx.false_literal())
        # if ctx.signed_number():
        #     return self.visit(ctx.signed_number())
        # if ctx.complex_number():
        #     return self.visit(ctx.complex_number())
        if ctx.strings():
            return self.visit(ctx.strings())
        # if ctx.list():
        #     return self.visit(ctx.list())
        # if ctx.tuple():
        #     return self.visit(ctx.tuple())
        # if ctx.set():
        #     return self.visit(ctx.set())
        # if ctx.dict():
        #     return self.visit(ctx.dict())

        return Constant(value=ctx.getText())

    # Visit a parse tree produced by SharpyParser#AugmentedAssignmentContext.
    def visitAugmentedAssignmentContext(self, ctx: SharpyParser.Augmented_assignmentContext):
        logger.debug("Visiting augmented assignment")
        return self.visitChildren(ctx)

    def visitBitwise_and(self, ctx: SharpyParser.Bitwise_andContext):
        logger.debug("Visiting bitwise and")

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single term
            return self.visit(ctx.shift_expression())

        left: Node = self.visit(ctx.getChild(0))  # The left operand
        right: Node = self.visit(ctx.getChild(2))  # The right operand

        return BinOp(left=left, op=BitAnd(), right=right)

    def visitBitwise_or(self, ctx: SharpyParser.Bitwise_orContext):
        logger.debug("Visiting bitwise or")

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single term
            return self.visit(ctx.bitwise_xor())

        left: Node = self.visit(ctx.getChild(0))  # The left operand
        right: Node = self.visit(ctx.getChild(2))  # The right operand

        return BinOp(left=left, op=BitOr(), right=right)

    def visitBitwise_xor(self, ctx: SharpyParser.Bitwise_xorContext):
        logger.debug("Visiting bitwise xor")

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single term
            return self.visit(ctx.bitwise_and())

        left: Node = self.visit(ctx.getChild(0))  # The left operand
        right: Node = self.visit(ctx.getChild(2))  # The right operand

        return BinOp(left=left, op=BitXor(), right=right)

    # Visit a parse tree produced by SharpyParser#block.
    def visitBlock(self, ctx: SharpyParser.BlockContext):
        logger.debug("Visiting block")

        statements: MutableSequence[Node] = []
        for i in range(ctx.getChildCount()):
            child: ParseTreeNode = ctx.getChild(i)
            node: Node | MutableSequence[Node] | None = self.visit(child)

            if isinstance(node, MutableSequence):
                statements.extend(node)
            elif node is not None:
                statements.append(node)

        return statements

    def visitBreak_statement(self, ctx: SharpyParser.Break_statementContext):
        logger.debug("Visiting break statement")
        # break_statement: 'break'
        return Break()

    # Visit a parse tree produced by SharpyParser#case_block.
    def visitCase_block(self, ctx: SharpyParser.Case_blockContext):
        logger.debug("Visiting case block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_def.
    def visitClass_def(self, ctx: SharpyParser.Class_defContext):
        logger.debug("Visiting class definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#closed_pattern.
    def visitClosed_pattern(self, ctx: SharpyParser.Closed_patternContext):
        logger.debug("Visiting closed pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#complex_number.
    def visitComplex_number(self, ctx: SharpyParser.Complex_numberContext):
        logger.debug("Visiting complex number")
        # complex_number: signed_real_number? imaginary_number

        text: str = ctx.getText()

        try:
            value = complex(text.replace("J", "j"))
        except Exception as e:
            raise ValueError(f"Invalid complex number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#compound_statement.
    def visitCompound_statement(self, ctx: SharpyParser.Compound_statementContext):
        logger.debug("Visiting compound statement")
        return self.visitChildren(ctx)

    def visitConjunction(self, ctx: SharpyParser.ConjunctionContext):
        logger.debug("Visiting conjunction")
        # conjunction: inversion ( AND inversion )*

        values: MutableSequence[Node] = []
        for i in range(0, ctx.getChildCount(), 2):
            values.append(self.visit(ctx.getChild(i)))

        return BoolOp(op=And(), values=values)

    def visitContinue_statement(self, ctx: SharpyParser.Continue_statementContext):
        logger.debug("Visiting continue statement")
        # continue_statement: 'continue'
        return Continue()

    # Visit a parse tree produced by SharpyParser#decorators.
    def visitDecorators(self, ctx: SharpyParser.DecoratorsContext):
        logger.debug("Visiting decorators")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#default_assignment.
    def visitDefault_assignment(self, ctx: SharpyParser.Default_assignmentContext):
        logger.debug("Visiting default assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_statement.
    def visitDel_statement(self, ctx: SharpyParser.Del_statementContext):
        logger.debug("Visiting delete statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#dict.
    def visitDict(self, ctx: SharpyParser.DictContext):
        logger.debug("Visiting dict")
        # dict: '{' double_starred_kvpairs? '}'

        keys: MutableSequence[Node] = []
        values: MutableSequence[Node] = []

        if ctx.kvpairs():
            kvpairs = self.visit(ctx.kvpairs())

            for k, v in kvpairs:
                keys.append(k)
                values.append(v)

        return Dict(keys=keys, values=values)

    def visitDisjunction(self, ctx: SharpyParser.DisjunctionContext):
        logger.debug("Visiting disjunction")
        # disjunction: conjunction ( OR conjunction )*

        values: MutableSequence[Node] = []
        for i in range(0, ctx.getChildCount(), 2):
            values.append(self.visit(ctx.getChild(i)))

        return BoolOp(op=Or(), values=values)

    def visitDotted_as_name(self, ctx: SharpyParser.Dotted_as_nameContext) -> alias:
        logger.debug("Visiting dotted as name")
        # dotted_as_name: dotted_name ('as' name)?

        name: str = ctx.dotted_name().getText()
        asname: str | None = ctx.name().getText() if ctx.name() else None

        return alias(name=name, asname=asname)

    def visitDotted_as_names(self, ctx: SharpyParser.Dotted_as_namesContext):
        logger.debug("Visiting dotted as names")
        # dotted_as_names: dotted_as_name (',' dotted_as_name)*

        return [self.visit(child) for child in ctx.dotted_as_name()]

    def visitDotted_name(self, ctx: SharpyParser.Dotted_nameContext) -> str:
        logger.debug("Visiting dotted name")
        # dotted_name: dotted_name '.' name | name

        # Return the full dotted name as a string
        return ctx.getText()

    # Visit a parse tree produced by SharpyParser#elif_statement.
    def visitElif_statement(self, ctx: SharpyParser.Elif_statementContext):
        logger.debug("Visiting elif statement")
        test: Node = self.visit(ctx.getChild(1))
        body: Sequence[Node] = self.visit(ctx.getChild(3))

        # TODO: This is probably wrong, as else is nested under the elif
        # above it
        return If(test=test, body=body, orelse=[])

    # Visit a parse tree produced by SharpyParser#else_block.
    def visitElse_block(self, ctx: SharpyParser.Else_blockContext):
        logger.debug("Visiting else block")
        return self.visit(ctx.getChild(2))  # Visit the block after 'else'

    # Visit a parse tree produced by SharpyParser#else_blocks.
    def visitElse_blocks(self, ctx: SharpyParser.Else_blocksContext):
        logger.debug("Visiting else blocks")
        return [self.visit(child) for child in ctx.getChildren()]

    # Visit a parse tree produced by SharpyParser#except_block.
    def visitExcept_block(self, ctx: SharpyParser.Except_blockContext):
        logger.debug("Visiting except block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_star_block.
    def visitExcept_star_block(self, ctx: SharpyParser.Except_star_blockContext):
        logger.debug("Visiting except star block")
        return self.visitChildren(ctx)

    def visitFalse_literal(self, ctx: SharpyParser.False_literalContext):
        logger.debug("Visiting False literal")
        # false_literal: 'False'
        retval = Constant(value=False)
        logger.debug(f"Returning False literal: {id(retval)}")
        return retval

    # Visit a parse tree produced by SharpyParser#finally_block.
    def visitFinally_block(self, ctx: SharpyParser.Finally_blockContext):
        logger.debug("Visiting finally block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#for_statement.
    def visitFor_statement(self, ctx: SharpyParser.For_statementContext):
        logger.debug("Visiting for statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def.
    def visitFunction_def(self, ctx: SharpyParser.Function_defContext):
        logger.debug("Visiting function definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#global_statement.
    def visitGlobal_statement(self, ctx: SharpyParser.Global_statementContext):
        logger.debug("Visiting global statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#guard.
    def visitGuard(self, ctx: SharpyParser.GuardContext):
        logger.debug("Visiting guard")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#if_statement.
    def visitIf_statement(self, ctx: SharpyParser.If_statementContext):
        logger.debug("Visiting if statement")
        test: Node = self.visit(ctx.getChild(1))
        body: Sequence[Node] = self.visit(ctx.getChild(3))
        else_blocks: Sequence[Node] = []

        if ctx.getChildCount() > 4:
            # If there are else blocks, visit them
            else_blocks = self.visit(ctx.getChild(4))

        return If(test=test, body=body, orelse=else_blocks)

    # Visit a parse tree produced by SharpyParser#imaginary_number.
    def visitImaginary_number(self, ctx: SharpyParser.Imaginary_numberContext):
        logger.debug("Visiting imaginary number")
        text: str = ctx.getText()

        try:
            value = complex(text.replace("J", "j"))
        except Exception as e:
            raise ValueError(f"Invalid imaginary number: {text}") from e

        return Constant(value=value)

    def visitImport_name(self, ctx: SharpyParser.Import_nameContext) -> Import:
        logger.debug("Visiting import name")
        # import_name: 'import' dotted_as_names

        dotted_as_names = self.visit(ctx.dotted_as_names())

        return Import(names=dotted_as_names)

    def visitImport_from(self, ctx: SharpyParser.Import_fromContext) -> ImportFrom:
        logger.debug("Visiting import from")
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

    def visitImport_from_as_name(self, ctx: SharpyParser.Import_from_as_nameContext) -> alias:
        logger.debug("Visiting import from as name")
        # import_from_as_name: name ('as' name)?

        name: str = ctx.name(0).getText()
        asname: str | None = ctx.name(1).getText() if ctx.name(1) else None

        return alias(name=name, asname=asname)

    def visitImport_from_as_names(self, ctx: SharpyParser.Import_from_as_namesContext):
        logger.debug("Visiting import from as names")
        # import_from_as_names: import_from_as_name (',' import_from_as_name)*

        return [self.visit(child) for child in ctx.import_from_as_name()]

    def visitImport_from_targets(self, ctx: SharpyParser.Import_from_targetsContext):
        logger.debug("Visiting import from targets")
        # import_from_targets: '(' import_from_as_names ','? ')' | import_from_as_names | '*'

        if ctx.getText() == "*":
            return ["*"]

        if ctx.import_from_as_names():
            return self.visit(ctx.import_from_as_names())

        return []

    # Visit a parse tree produced by SharpyParser#import_statement.
    def visitImport_statement(self, ctx: SharpyParser.Import_statementContext) -> Node | None:
        logger.debug("Visiting import statement")
        # import_statement: import_name | import_from

        if ctx.import_name():
            return self.visit(ctx.import_name())
        elif ctx.import_from():
            return self.visit(ctx.import_from())

        return None

    def visitInversion(self, ctx: SharpyParser.InversionContext):
        logger.debug("Visiting inversion")

        if ctx.getChildCount() == 2:
            operand: Expression = self.visit(ctx.getChild(1))
            return UnaryOp(Not(), operand)
        elif ctx.getChildCount() == 1:
            return self.visit(ctx.getChild(0))
        else:
            raise ValueError("Unknown inversion type")

    def visitKey_expression(self, ctx: SharpyParser.Key_expressionContext) -> Node:
        logger.debug("Visiting key expression")
        # key_expression: expression
        return self.visit(ctx.expression())

    # Visit a parse tree produced by SharpyParser#kvpair.
    def visitKvpair(self, ctx: SharpyParser.KvpairContext):
        logger.debug("Visiting key-value pair")
        # kvpair: expression ':' expression

        if ctx.getChildCount() != 3:
            raise ValueError("Invalid key-value pair format")

        # TODO: Strange why this doesn't work...
        # TODO: It might be because of the use of the actual token names
        # as opposed to raw characters, I see the same issue with visitSum()
        # print(ctx.getChildCount(), ctx.key_expression(), ctx.value_expression())
        # print(dir(ctx))

        # key = self.visit(ctx.key_expression())
        # value = self.visit(ctx.value_expression())
        key = self.visit(ctx.children[0])
        value = self.visit(ctx.children[2])

        return (key, value)

    def visitKvpairs(self, ctx: SharpyParser.KvpairsContext) -> MutableSequence[tuple[Node, Node]]:
        logger.debug("Visiting key-value pairs")
        # kvpairs: kvpair (',' kvpair)*
        return [self.visitKvpair(kvpair) for kvpair in ctx.kvpair()]

    # Visit a parse tree produced by SharpyParser#list.
    def visitList(self, ctx: SharpyParser.ListContext):
        logger.debug("Visiting list")
        # list: '[' star_named_expressions? ']'

        elts: Sequence[Node] = []

        if ctx.named_expressions():
            for node in ctx.named_expressions().getChildren():

                if node.getText() == ",":
                    continue

                elts.append(self.visit(node))

        logger.debug(f"List elements: {elts}")

        return List(elts=elts, ctx=Load())

    # Visit a parse tree produced by SharpyParser#literal_expression.
    def visitLiteral_expression(self, ctx: SharpyParser.Literal_expressionContext):
        logger.debug("Visiting literal expression")
        # literal_expression: string | complex_number | signed_number | 'None' | 'True' | 'False'

        if ctx.strings():
            return self.visit(ctx.strings())
        elif ctx.complex_number():
            return self.visit(ctx.complex_number())
        elif ctx.signed_number():
            return self.visit(ctx.signed_number())
        elif ctx.none_literal():
            return self.visit(ctx.none_literal())
        elif ctx.true_literal():
            return self.visit(ctx.true_literal())
        elif ctx.false_literal():
            return self.visit(ctx.false_literal())

        return None

    # Visit a parse tree produced by SharpyParser#literal_pattern.
    def visitLiteral_pattern(self, ctx: SharpyParser.Literal_patternContext):
        logger.debug("Visiting literal pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#match_statement.
    def visitMatch_statement(self, ctx: SharpyParser.Match_statementContext):
        logger.debug("Visiting match statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#module.
    def visitModule(self, ctx: SharpyParser.ModuleContext) -> Module:
        logger.debug("Visiting module")
        body: MutableSequence[Node] = []

        for i in range(ctx.getChildCount()):
            child: ParseTreeNode = ctx.getChild(i)
            node: Node | MutableSequence[Node] | None = self.visit(child)

            if isinstance(node, MutableSequence):
                body.extend(node)
            elif node is not None:
                body.append(node)

        return Module(body)

    def visitName_except_underscore(self, ctx: SharpyParser.Name_except_underscoreContext):
        return Name(
            id=ctx.getText(),
            ctx=Load(),
        )

    def visitNone_literal(self, ctx: SharpyParser.None_literalContext):
        logger.debug("Visiting None literal")
        # none_literal: 'None'
        retval = Constant(value=None)
        logger.debug(f"Returning None literal: {id(retval)}")
        return retval

    # Visit a parse tree produced by SharpyParser#nonlocal_statement.
    def visitNonlocal_statement(self, ctx: SharpyParser.Nonlocal_statementContext):
        logger.debug("Visiting nonlocal statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#or_pattern.
    def visitOr_pattern(self, ctx: SharpyParser.Or_patternContext):
        logger.debug("Visiting or pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_no_default.
    def visitParam_no_default(self, ctx: SharpyParser.Param_no_defaultContext):
        logger.debug("Visiting parameter without default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param_with_default.
    def visitParam_with_default(self, ctx: SharpyParser.Param_with_defaultContext):
        logger.debug("Visiting parameter with default")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#param.
    def visitParam(self, ctx: SharpyParser.ParamContext):
        logger.debug("Visiting parameter")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#parameters.
    def visitParameters(self, ctx: SharpyParser.ParametersContext):
        logger.debug("Visiting parameter list")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#params.
    def visitParams(self, ctx: SharpyParser.ParamsContext):
        logger.debug("Visiting parameters")
        return self.visitChildren(ctx)

    def visitPass_statement(self, ctx: SharpyParser.Pass_statementContext):
        logger.debug("Visiting pass statement")
        # pass_statement: 'pass'
        return Pass()

    # Visit a parse tree produced by SharpyParser#pattern.
    def visitPattern(self, ctx: SharpyParser.PatternContext):
        logger.debug("Visiting pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#patterns.
    def visitPatterns(self, ctx: SharpyParser.PatternsContext):
        logger.debug("Visiting patterns")
        return self.visitChildren(ctx)

    def visitPower(self, ctx: SharpyParser.PowerContext):
        logger.debug("Visiting power expression")

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single term `await_primary`
            return self.visit(ctx.getChild(0))

        left: Node = self.visit(ctx.getChild(0))  # The left operand
        right: Node = self.visit(ctx.getChild(2))  # The right operand

        return BinOp(left=left, op=Pow(), right=right)

    # Visit a parse tree produced by SharpyParser#protocol_def.
    def visitProtocol_def(self, ctx: SharpyParser.Protocol_defContext):
        logger.debug("Visiting protocol definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#raise_statement.
    def visitRaise_statement(self, ctx: SharpyParser.Raise_statementContext):
        logger.debug("Visiting raise statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#real_number.
    def visitReal_number(self, ctx: SharpyParser.Real_numberContext):
        logger.debug("Visiting real number")
        text: str = ctx.getText()

        try:
            if "." in text or "e" in text or "E" in text:
                value = float(text)
            else:
                value = int(text)
        except Exception as e:
            raise ValueError(f"Invalid real number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#return_statement.
    def visitReturn_statement(self, ctx: SharpyParser.Return_statementContext):
        logger.debug("Visiting return statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#set.
    def visitSet(self, ctx: SharpyParser.SetContext):
        logger.debug("Visiting set")
        # set: '{' star_named_expressions '}'

        elts: Sequence[Node] = []

        if ctx.named_expressions():
            for node in ctx.named_expressions().getChildren():

                if node.getText() == ",":
                    continue

                elts.append(self.visit(node))

        logger.debug(f"Set elements: {elts}")

        return Set(elts=elts)

    def visitShift_expression(self, ctx: SharpyParser.Shift_expressionContext):
        logger.debug("Visiting shift expression")

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single term
            return self.visit(ctx.sum_())

        left: Node = self.visit(ctx.getChild(0))  # The left operand
        right: Node = self.visit(ctx.getChild(2))  # The right operand

        if ctx.leftshift_operator():
            return BinOp(left=left, op=LShift(), right=right)
        elif ctx.rightshift_operator():
            return BinOp(left=left, op=RShift(), right=right)
        else:
            raise ValueError("Invalid shift operator in shift expression")

    # Visit a parse tree produced by SharpyParser#signed_number.
    def visitSigned_number(self, ctx: SharpyParser.Signed_numberContext):
        logger.debug("Visiting signed number")
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

    # Visit a parse tree produced by SharpyParser#simple_statement.
    def visitSimple_statement(self, ctx: SharpyParser.Simple_statementContext):
        logger.debug("Visiting simple statement")

        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#statements.
    def visitStatement(self, ctx: SharpyParser.StatementContext) -> MutableSequence[Node]:
        logger.debug("Visiting statements")

        statements: MutableSequence[Node] = []

        for i in range(ctx.getChildCount()):
            child: ParseTreeNode = ctx.getChild(i)
            node: Node | None = self.visit(child)

            if node is not None:
                statements.append(node)

        return statements

    # Visit a parse tree produced by SharpyParser#strings.
    def visitString(self, ctx: SharpyParser.StringContext):
        logger.debug("Visiting string")

        logger.debug(f"String text: {ctx.getText()}")

        return Constant(value=ctx.getText())

    # Visit a parse tree produced by SharpyParser#strings.
    def visitStrings(self, ctx: SharpyParser.StringsContext):
        logger.debug("Visiting strings")
        # strings: string+

        # Concatenate all string tokens (Python does this for adjacent strings)
        string_nodes = [self.visit(child) for child in ctx.getChildren()]

        # If each is a Constant, concatenate their values
        value = "".join(
            stringify_string_constant(node) or "" for node in string_nodes if hasattr(node, "value")
        )
        value = f'"{value}"'

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#struct_def.
    def visitStruct_def(self, ctx: SharpyParser.Struct_defContext):
        logger.debug("Visiting struct definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#subject_expression.
    def visitSubject_expression(self, ctx: SharpyParser.Subject_expressionContext):
        logger.debug("Visiting subject expression")
        return self.visitChildren(ctx)

    def visitSum(self, ctx: SharpyParser.SumContext):
        logger.debug("Visiting sum")
        # sum: sum ('+' | '-') term | term

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single term
            return self.visit(ctx.term())

        logger.debug(f"Sum context: {ctx.term()}")

        # TODO: sum_() and term() don't work for some reason
        left: Node = self.visit(ctx.getChild(0))  # The left operand

        if ctx.addition_operator() is not None:
            op_type: Node = Add()
        else:
            op_type: Node = Sub()

        right: Node = self.visit(ctx.getChild(2))  # The right operand

        return BinOp(left=left, op=op_type, right=right)

    def visitTerm(self, ctx: SharpyParser.TermContext):
        logger.debug("Visiting term")
        # term: term ('*' | '/' | '//' | '%' | '<<' | '>>') factor | fator

        if ctx.getChildCount() == 1:
            # If there's only one child, it's a single factor
            return self.visit(ctx.factor())

        left: Node = self.visit(ctx.getChild(0))  # The left operand
        right: Node = self.visit(ctx.getChild(2))  # The right operand

        if ctx.multiplication_operator():
            op_type = Mult()
        elif ctx.division_operator():
            op_type = Div()
        elif ctx.floor_division_operator():
            op_type = FloorDiv()
        elif ctx.modulo_operator():
            op_type = Mod()
        else:
            raise ValueError("Invalid operator in term")

        return BinOp(left=left, op=op_type, right=right)

    def visitTernary_expression(self, ctx: SharpyParser.Ternary_expressionContext):
        return IfExp(
            test=self.visit(ctx.test), body=self.visit(ctx.body), orelse=self.visit(ctx.orelse)
        )

    def visitTrue_literal(self, ctx: SharpyParser.True_literalContext):
        logger.debug("Visiting True literal")
        # true_literal: 'True'
        retval = Constant(value=True)
        logger.debug(f"Returning True literal: {id(retval)}")
        return retval

    # Visit a parse tree produced by SharpyParser#try_statement.
    def visitTry_statement(self, ctx: SharpyParser.Try_statementContext):
        logger.debug("Visiting try statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#tuple.
    def visitTuple(self, ctx: SharpyParser.TupleContext):
        logger.debug("Visiting tuple")
        # tuple: '(' star_named_expressions? ')'

        elts: Sequence[Node] = []

        if ctx.named_expressions():
            for node in ctx.named_expressions().getChildren():

                if node.getText() == ",":
                    continue

                elts.append(self.visit(node))

        logger.debug(f"Tuple elements: {elts}")

        return Tuple(elts=elts, ctx=Load())

    # Visit a parse tree produced by SharpyParser#type_annotation.
    def visitType_annotation(self, ctx: SharpyParser.Type_annotationContext):
        logger.debug("Visiting type annotation")
        return self.visitChildren(ctx)

    def visitValue_expression(self, ctx: SharpyParser.Value_expressionContext) -> Node:
        logger.debug("Visiting value expression")
        # value_expression: expression
        return self.visit(ctx.expression())

    # Visit a parse tree produced by SharpyParser#while_statement.
    def visitWhile_statement(self, ctx: SharpyParser.While_statementContext):
        logger.debug("Visiting while statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_item.
    def visitWith_item(self, ctx: SharpyParser.With_itemContext):
        logger.debug("Visiting with item")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_statement.
    def visitWith_statement(self, ctx: SharpyParser.With_statementContext):
        logger.debug("Visiting with statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#yield_statement.
    def visitYield_statement(self, ctx: SharpyParser.Yield_statementContext):
        logger.debug("Visiting yield statement")
        return self.visitChildren(ctx)
