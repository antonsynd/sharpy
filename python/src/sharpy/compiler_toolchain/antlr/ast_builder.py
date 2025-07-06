from typing import MutableSequence, Sequence

from SharpyParser import SharpyParser
from SharpyParserVisitor import SharpyParserVisitor

from sharpy.compiler_toolchain.abc.ast_builder import ASTBuilder
from sharpy.compiler_toolchain.ast import *
from sharpy.compiler_toolchain.logging import logger

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

    def visitAtom(self, ctx: SharpyParser.AtomContext) -> Constant:
        logger.debug("Visiting atom")
        logger.debug(f"Atom value: {ctx.getText()}")

        return Constant(ctx.getText())

    # Visit a parse tree produced by SharpyParser#simple_statement.
    def visitSimple_statement(self, ctx: SharpyParser.Simple_statementContext):
        logger.debug("Visiting simple statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#compound_statement.
    def visitCompound_statement(self, ctx: SharpyParser.Compound_statementContext):
        logger.debug("Visiting compound statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assignment.
    def visitAssignment(self, ctx: SharpyParser.AssignmentContext):
        logger.debug("Visiting assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#annotated_rhs.
    def visitAnnotated_rhs(self, ctx: SharpyParser.Annotated_rhsContext):
        logger.debug("Visiting annotated right-hand side")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#AugmentedAssignmentContext.
    def visitAugmentedAssignmentContext(self, ctx: SharpyParser.Augmented_assignmentContext):
        logger.debug("Visiting augmented assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#return_statement.
    def visitReturn_statement(self, ctx: SharpyParser.Return_statementContext):
        logger.debug("Visiting return statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#raise_statement.
    def visitRaise_statement(self, ctx: SharpyParser.Raise_statementContext):
        logger.debug("Visiting raise statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#global_statement.
    def visitGlobal_statement(self, ctx: SharpyParser.Global_statementContext):
        logger.debug("Visiting global statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#nonlocal_statement.
    def visitNonlocal_statement(self, ctx: SharpyParser.Nonlocal_statementContext):
        logger.debug("Visiting nonlocal statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#del_statement.
    def visitDel_statement(self, ctx: SharpyParser.Del_statementContext):
        logger.debug("Visiting delete statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#yield_statement.
    def visitYield_statement(self, ctx: SharpyParser.Yield_statementContext):
        logger.debug("Visiting yield statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#assert_statement.
    def visitAssert_statement(self, ctx: SharpyParser.Assert_statementContext):
        logger.debug("Visiting assert statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#import_statement.
    def visitImport_statement(self, ctx: SharpyParser.Import_statementContext) -> Node | None:
        logger.debug("Visiting import statement")
        # import_statement: import_name | import_from

        if ctx.import_name():
            return self.visit(ctx.import_name())
        elif ctx.import_from():
            return self.visit(ctx.import_from())

        return None

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

    def visitImport_from_targets(self, ctx: SharpyParser.Import_from_targetsContext):
        logger.debug("Visiting import from targets")
        # import_from_targets: '(' import_from_as_names ','? ')' | import_from_as_names | '*'

        if ctx.getText() == "*":
            return ["*"]

        if ctx.import_from_as_names():
            return self.visit(ctx.import_from_as_names())

        return []

    def visitImport_from_as_names(self, ctx: SharpyParser.Import_from_as_namesContext):
        logger.debug("Visiting import from as names")
        # import_from_as_names: import_from_as_name (',' import_from_as_name)*

        return [self.visit(child) for child in ctx.import_from_as_name()]

    def visitImport_from_as_name(self, ctx: SharpyParser.Import_from_as_nameContext) -> alias:
        logger.debug("Visiting import from as name")
        # import_from_as_name: name ('as' name)?

        name: str = ctx.name(0).getText()
        asname: str | None = ctx.name(1).getText() if ctx.name(1) else None

        return alias(name=name, asname=asname)

    def visitDotted_as_names(self, ctx: SharpyParser.Dotted_as_namesContext):
        logger.debug("Visiting dotted as names")
        # dotted_as_names: dotted_as_name (',' dotted_as_name)*

        return [self.visit(child) for child in ctx.dotted_as_name()]

    def visitDotted_as_name(self, ctx: SharpyParser.Dotted_as_nameContext) -> alias:
        logger.debug("Visiting dotted as name")
        # dotted_as_name: dotted_name ('as' name)?

        name: str = ctx.dotted_name().getText()
        asname: str | None = ctx.name().getText() if ctx.name() else None

        return alias(name=name, asname=asname)

    def visitDotted_name(self, ctx: SharpyParser.Dotted_nameContext) -> str:
        logger.debug("Visiting dotted name")
        # dotted_name: dotted_name '.' name | name

        # Return the full dotted name as a string
        return ctx.getText()

    # Visit a parse tree produced by SharpyParser#block.
    def visitBlock(self, ctx: SharpyParser.BlockContext):
        logger.debug("Visiting block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#decorators.
    def visitDecorators(self, ctx: SharpyParser.DecoratorsContext):
        logger.debug("Visiting decorators")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#class_def.
    def visitClass_def(self, ctx: SharpyParser.Class_defContext):
        logger.debug("Visiting class definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#struct_def.
    def visitStruct_def(self, ctx: SharpyParser.Struct_defContext):
        logger.debug("Visiting struct definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#protocol_def.
    def visitProtocol_def(self, ctx: SharpyParser.Protocol_defContext):
        logger.debug("Visiting protocol definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def.
    def visitFunction_def(self, ctx: SharpyParser.Function_defContext):
        logger.debug("Visiting function definition")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#function_def_raw.
    def visitFunction_def_raw(self, ctx: SharpyParser.Function_def_rawContext):
        logger.debug("Visiting function definition (raw)")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#params.
    def visitParams(self, ctx: SharpyParser.ParamsContext):
        logger.debug("Visiting parameters")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#parameters.
    def visitParameters(self, ctx: SharpyParser.ParametersContext):
        logger.debug("Visiting parameter list")
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

    # Visit a parse tree produced by SharpyParser#annotation.
    def visitAnnotation(self, ctx: SharpyParser.AnnotationContext):
        logger.debug("Visiting annotation")
        # annotation: '@' dotted_name ('.' name)* ('.' '(' ')')
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#default_assignment.
    def visitDefault_assignment(self, ctx: SharpyParser.Default_assignmentContext):
        logger.debug("Visiting default assignment")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#if_statement.
    def visitIf_statement(self, ctx: SharpyParser.If_statementContext):
        logger.debug("Visiting if statement")
        # if_statement: 'if' expression ':' block (elif_statement | else_block)*
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#elif_statement.
    def visitElif_statement(self, ctx: SharpyParser.Elif_statementContext):
        logger.debug("Visiting elif statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#else_block.
    def visitElse_block(self, ctx: SharpyParser.Else_blockContext):
        logger.debug("Visiting else block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#while_statement.
    def visitWhile_statement(self, ctx: SharpyParser.While_statementContext):
        logger.debug("Visiting while statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#for_statement.
    def visitFor_statement(self, ctx: SharpyParser.For_statementContext):
        logger.debug("Visiting for statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_statement.
    def visitWith_statement(self, ctx: SharpyParser.With_statementContext):
        logger.debug("Visiting with statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#with_item.
    def visitWith_item(self, ctx: SharpyParser.With_itemContext):
        logger.debug("Visiting with item")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#try_statement.
    def visitTry_statement(self, ctx: SharpyParser.Try_statementContext):
        logger.debug("Visiting try statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_block.
    def visitExcept_block(self, ctx: SharpyParser.Except_blockContext):
        logger.debug("Visiting except block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#except_star_block.
    def visitExcept_star_block(self, ctx: SharpyParser.Except_star_blockContext):
        logger.debug("Visiting except star block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#finally_block.
    def visitFinally_block(self, ctx: SharpyParser.Finally_blockContext):
        logger.debug("Visiting finally block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#match_statement.
    def visitMatch_statement(self, ctx: SharpyParser.Match_statementContext):
        logger.debug("Visiting match statement")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#subject_expression.
    def visitSubject_expression(self, ctx: SharpyParser.Subject_expressionContext):
        logger.debug("Visiting subject expression")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#case_block.
    def visitCase_block(self, ctx: SharpyParser.Case_blockContext):
        logger.debug("Visiting case block")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#guard.
    def visitGuard(self, ctx: SharpyParser.GuardContext):
        logger.debug("Visiting guard")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#patterns.
    def visitPatterns(self, ctx: SharpyParser.PatternsContext):
        logger.debug("Visiting patterns")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#pattern.
    def visitPattern(self, ctx: SharpyParser.PatternContext):
        logger.debug("Visiting pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#as_pattern.
    def visitAs_pattern(self, ctx: SharpyParser.As_patternContext):
        logger.debug("Visiting as pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#or_pattern.
    def visitOr_pattern(self, ctx: SharpyParser.Or_patternContext):
        logger.debug("Visiting or pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#closed_pattern.
    def visitClosed_pattern(self, ctx: SharpyParser.Closed_patternContext):
        logger.debug("Visiting closed pattern")
        return self.visitChildren(ctx)

    # Visit a parse tree produced by SharpyParser#literal_pattern.
    def visitLiteral_pattern(self, ctx: SharpyParser.Literal_patternContext):
        logger.debug("Visiting literal pattern")
        return self.visitChildren(ctx)

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
        elif ctx.getText() == "None":
            return Constant(value=None)
        elif ctx.getText() == "True":
            return Constant(value=True)
        elif ctx.getText() == "False":
            return Constant(value=False)

        return None

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

    # Visit a parse tree produced by SharpyParser#imaginary_number.
    def visitImaginary_number(self, ctx: SharpyParser.Imaginary_numberContext):
        logger.debug("Visiting imaginary number")
        text: str = ctx.getText()

        try:
            value = complex(text.replace("J", "j"))
        except Exception as e:
            raise ValueError(f"Invalid imaginary number: {text}") from e

        return Constant(value=value)

    # Visit a parse tree produced by SharpyParser#list.
    def visitList(self, ctx: SharpyParser.ListContext):
        logger.debug("Visiting list")
        # list: '[' star_named_expressions? ']'

        elts: Sequence[Node] = []
        if ctx.named_expressions():
            elts: Sequence[Node] = self.visit(ctx.named_expressions())

        return List(elts=elts, ctx=Load())

    # Visit a parse tree produced by SharpyParser#tuple.
    def visitTuple(self, ctx: SharpyParser.TupleContext):
        logger.debug("Visiting tuple")
        # tuple: '(' star_named_expressions? ')'

        elts: Sequence[Node] = []

        if ctx.named_expressions():
            elts: Sequence[Node] = self.visit(ctx.named_expressions())

        return Tuple(elts=elts, ctx=Load())

    # Visit a parse tree produced by SharpyParser#set.
    def visitSet(self, ctx: SharpyParser.SetContext):
        logger.debug("Visiting set")
        # set: '{' star_named_expressions '}'

        elts: Sequence[Node] = (
            self.visit(ctx.named_expressions()) if ctx.named_expressions() else []
        )

        return Set(elts=elts)

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

    # Visit a parse tree produced by SharpyParser#kvpair.
    def visitKvpair(self, ctx: SharpyParser.KvpairContext):
        logger.debug("Visiting key-value pair")
        # kvpair: expression ':' expression

        key = self.visit(ctx.expression(0))
        value = self.visit(ctx.expression(1))

        return (key, value)

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
        value = "".join(str(node.value) for node in string_nodes if hasattr(node, "value"))

        return Constant(value=value)
