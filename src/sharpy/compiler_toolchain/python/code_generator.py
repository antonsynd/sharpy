import ast
import re
import subprocess
import sys

from enum import Enum, auto
from io import StringIO, TextIOBase
from logging import Logger
from typing import Mapping, MutableSequence, Optional, Sequence

# Mapping Python types to C#
TYPE_MAP: Mapping[str, str] = {
    "bool": "bool",
    "byte": "byte",
    "decimal": "decimal",
    "double": "double",
    "int": "int",
    "float": "float",
    "long": "long",
    "None": "void",  # Only in return types
    "object": "object",
    "sbyte": "sbyte",
    "short": "short",
    "str": "string",
    "uint": "uint",
    "ulong": "ulong",
    "ushort": "ushort",
}

SINGLE_TEMPLATE_TYPE_MAP: Mapping[str, str] = {
    "list": "System.Collections.Generic.List",
}

template_pattern = re.compile(r"(array|list|dict|tuple|Optional)\[(.+)\]")


def map_python_type_to_cs_type(s: str) -> str:
    if s in TYPE_MAP:
        return TYPE_MAP[s]

    res = template_pattern.match(s)

    if not res:
        return "object"

    main_type, template_type = res.groups()

    if main_type in SINGLE_TEMPLATE_TYPE_MAP:
        main_type: str = SINGLE_TEMPLATE_TYPE_MAP[main_type]
        template_type: str = map_python_type_to_cs_type(template_type)

        return f"{main_type}<{template_type}>"

    if main_type == "Optional":
        template_type: str = map_python_type_to_cs_type(template_type)
        return f"{template_type}?"

    if main_type == "array":
        template_type: str = map_python_type_to_cs_type(template_type)
        return f"{template_type}[]"

    if main_type == "dict":
        main_type: str = "System.Collections.Generic.HashMap"
        key_type, value_type = template_type.split(",")
        key_type: str = map_python_type_to_cs_type(key_type.strip())
        value_type: str = map_python_type_to_cs_type(value_type.strip())

        return f"{main_type}<{key_type}, {value_type}>"

    return "object"


class CodegenNameFormat(Enum):
    FUNCTION_NAME = auto()
    CLASS_NAME = auto()
    NAMESPACE_NAME = auto()
    FUNCTION_VARIABLE_NAME = auto()
    CLASS_MEMBER_NAME = auto()
    CLASS_METHOD_NAME = auto()
    CLASS_PROPERTY_NAME = auto()


class CodegenContextType(Enum):
    MODULE = auto()
    NAMESPACE_NAME = auto()
    FUNCTION_NAME = auto()
    FUNCTION_RETURN_TYPE = auto()
    FUNCTION_ARGUMENT_TYPE = auto()
    FUNCTION_BODY = auto()
    CLASS_MEMBER = auto()
    CLASS_METHOD = auto()
    CLASS_NAME = auto()


class CodegenContext:
    def __init__(self, context_type: CodegenContextType):
        self._context_type: CodegenContextType = context_type

    def get_type(self) -> CodegenContextType:
        return self._context_type


class CodegenDirective(Enum):
    NOSPACE = auto()
    SPACE = auto()
    ONELINE = auto()
    MULTILINE = auto()


class SourceLine:
    def __init__(self, node: ast.AST, file_name: str) -> None:
        self._line_num: int = node.lineno
        self._col_offset: int = node.col_offset
        self._end_line_num: int = node.end_lineno
        self._end_col_offset: int = node.end_col_offset
        self._file_name: str = file_name

    def __repr__(self) -> str:
        # Python column offsets are 0-indexed, but C# is 1-indexed
        return f'\n#line ({self._line_num}, {self._col_offset + 1}) - ({self._end_line_num}, {self._end_col_offset + 1}) "{self._file_name}"\n'


CodegenElement = SourceLine | CodegenDirective | str


class PythonToCSharp(ast.NodeVisitor):
    def __init__(
        self,
        logger: Logger,
        emit_line_metadata: bool = False,
        format: bool = False,
        csharpier_path: Optional[str] = None,
    ):
        self._logger: Logger = logger
        self._result: MutableSequence[CodegenElement] = []
        self._emit_line_metadata: bool = emit_line_metadata
        self._format: bool = format
        self._csharpier_path: Optional[str] = csharpier_path

        self._file_name: str = "anonymous.spy"
        self._context_stack: MutableSequence[CodegenContext] = []

    def debug(self, *args) -> None:
        self._logger.debug(*args)

    def info(self, *args) -> None:
        self._logger.info(*args)

    def error(self, *args) -> None:
        self._logger.error(*args)

    def warning(self, *args) -> None:
        self._logger.warning(*args)

    def set_file_name(self, s: str) -> None:
        self._file_name = s if s else "anonymous.spy"

    def peek_context_type(self) -> CodegenContextType:
        return self._context_stack[-1].get_type()

    def push_context(self, context: CodegenContext) -> None:
        self._context_stack.append(context)

    def pop_context(self) -> CodegenContext:
        return self._context_stack.pop()

    def source_line(self, node: ast.AST) -> None:
        if self._emit_line_metadata:
            self.append(SourceLine(node=node, file_name=self._file_name))

    def append(self, s: CodegenElement) -> None:
        self._result.append(s)

    def adjust_identifier(self, s: str, context_type: Optional[CodegenContextType] = None) -> str:
        context_type: CodegenContextType = (
            context_type if context_type else self.peek_context_type()
        )

        match context_type:
            case CodegenContextType.CLASS_NAME:
                # PascalCase for class names
                return "".join(x.capitalize() for x in s.split("_"))
            case CodegenContextType.FUNCTION_BODY:
                # camelCase for function variables
                parts: Sequence[str] = s.split("_")
                return parts[0] + "".join(w.capitalize() for w in parts[1:])
            case _:
                # TODO: Adjust for whether it is a local variable, class member, etc.
                return "".join(x.capitalize() for x in s.split("_"))

    def get_module_name(self) -> str:
        return self._file_name.split(".spy")[0]

    def dump_to_buffer(self, buffer: TextIOBase) -> None:
        emit_space: bool = True
        multiline: bool = False

        for s in self._result:
            if isinstance(s, CodegenDirective):
                if s == CodegenDirective.NOSPACE:
                    emit_space = False
                elif s == CodegenDirective.SPACE:
                    emit_space = True
                elif s == CodegenDirective.ONELINE:
                    multiline = False
                elif s == CodegenDirective.MULTILINE:
                    multiline = True
                else:
                    raise ValueError(f"Unsupported directive {s}")

                continue
            elif isinstance(s, str):
                buffer.write(s)
            elif isinstance(s, SourceLine):
                if self._emit_line_metadata and multiline:
                    buffer.write(str(s))

                continue
            else:
                raise ValueError(f"Unsupported element {s}")

            if emit_space:
                buffer.write(" ")

    def generate_csharp(
        self, buffer: TextIOBase, file_name: Optional[str] = None, format: bool = False
    ) -> str:
        self.set_file_name(file_name)

        tree: ast.Module = ast.parse(buffer)

        namespace_name: str = self.get_module_name()
        namespace_name = self.adjust_identifier(
            namespace_name, context_type=CodegenContextType.NAMESPACE_NAME
        )
        self.append(f"namespace {namespace_name} {{")

        self.visit(tree)

        self.append("}")

        if self._format and self._csharpier_path:
            try:
                proc: subprocess.Popen = subprocess.Popen(
                    args=[
                        self._csharpier_path,
                        "--no-cache",
                        "--no-msbuild-check",
                        "--fast",
                        "--write-stdout",
                    ],
                    text=True,
                    stdin=subprocess.PIPE,
                    stdout=subprocess.PIPE,
                )

                self.dump_to_buffer(proc.stdin)

                proc.stdin.close()
            except subprocess.CalledProcessError as e:
                self.error(e.stderr)
                self.error(e)
                sys.exit(1)

            return proc.stdout.read()
        else:
            buffer = StringIO()

            self.dump_to_buffer(buffer)

            return buffer.getvalue()

    #########################
    # BEGIN VISITOR METHODS #
    #########################

    def visit_Add(self, node: ast.Add):
        self.debug("Add")
        self.append("+")

    def visit_alias(self, node: ast.alias):
        self.source_line(node)

        if node.asname:
            self.append(node.name)
            self.append("as")
            self.append(node.asname)
        else:
            self.append(node.name)

    def visit_And(self, node: ast.And):
        self.debug("And")
        self.append("&&")

    def visit_AnnAssign(self, node: ast.AnnAssign):
        self.debug("AnnAssign")
        var_type: str = map_python_type_to_cs_type(ast.unparse(node.annotation))
        target: str = ast.unparse(node.target)
        value: str = ast.unparse(node.value) if node.value else ""

        self.source_line(node)
        self.append(f"{var_type} {target} = {value};")

    def visit_arg(self, node: ast.arg):
        self.debug("arg")
        self.source_line(node)

    def visit_arguments(self, node: ast.arguments):
        self.debug("arguments")

    def visit_Assert(self, node: ast.Assert):
        self.debug("Assert")
        self.source_line(node)

    def visit_Assign(self, node: ast.Assign):
        self.debug("Assign")
        targets: str = ", ".join(ast.unparse(t) for t in node.targets)
        value: str = ast.unparse(node.value)

        self.source_line(node)
        self.append(f"var {targets} = {value};")

    def visit_AsyncFor(self, node: ast.AsyncFor):
        self.debug("AsyncFor")
        self.source_line(node)

    def visit_AsyncFunctionDef(self, node: ast.AsyncFunctionDef):
        self.debug("AsyncFunctionDef")
        self.source_line(node)

    def visit_AsyncWith(self, node: ast.AsyncWith):
        self.debug("AsyncWith")
        self.source_line(node)

    def visit_Attribute(self, node: ast.Attribute):
        self.debug("Attribute")
        self.source_line(node)

    def visit_AugAssign(self, node: ast.AugAssign):
        self.debug("AugAssign")
        self.source_line(node)

    def visit_AugLoad(self, node: ast.AugLoad):
        self.debug("AugLoad")

    def visit_AugStore(self, node: ast.AugStore):
        self.debug("AugStore")

    def visit_Await(self, node: ast.Await):
        self.debug("Await")
        self.source_line(node)

    def visit_BinOp(self, node: ast.BinOp):
        self.debug("BinOp")
        self.source_line(node)
        self.visit(node.left)
        self.visit(node.op)
        self.visit(node.right)

    def visit_BitAnd(self, node: ast.BitAnd):
        self.debug("BitAnd")
        self.source_line(node)

    def visit_BitOr(self, node: ast.BitOr):
        self.debug("BitOr")
        self.source_line(node)

    def visit_BitXor(self, node: ast.BitXor):
        self.debug("BitXor")
        self.source_line(node)

    def visit_BoolOp(self, node: ast.BoolOp):
        self.debug("BoolOp")
        self.source_line(node)

    def visit_Break(self, node: ast.Break):
        self.debug("Break")
        self.source_line(node)
        self.append("break;")

    def visit_Bytes(self, node: ast.Bytes):
        self.debug("Bytes")
        self.source_line(node)

    def visit_Call(self, node: ast.Call):
        self.debug("Call")
        self.source_line(node)
        self.visit(node.func)

    def visit_ClassDef(self, node: ast.ClassDef):
        self.debug("ClassDef")
        self.source_line(node)
        class_name: str = self.adjust_identifier(
            s=node.name, context_type=CodegenContextType.CLASS_NAME
        )
        self.append(f"public class {class_name} {{")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_Compare(self, node: ast.Compare):
        self.debug("Compare")
        self.source_line(node)
        self.visit(node.left)
        self.visit(node.ops[0])
        self.visit(node.comparators[0])

    def visit_comprehension(self, node: ast.comprehension):
        self.debug("comprehension")

    def visit_Constant(self, node: ast.Constant):
        self.debug("Constant")
        match ast.unparse(node):
            case "None":
                self.append("null")
            case "True" | "False" as boolean_literal:
                self.append(boolean_literal.lower())
            case _ as str_literal:
                self.append(f'"{str_literal[1:-1]}"')

    def visit_Continue(self, node: ast.Continue):
        self.debug("Continue")
        self.source_line(node)
        self.append("continue;")

    def visit_Del(self, node: ast.Del):
        self.debug("Del")

    def visit_Delete(self, node: ast.Delete):
        self.debug("Delete")

    def visit_Dict(self, node: ast.Dict):
        self.debug("Dict")

    def visit_DictComp(self, node: ast.DictComp):
        self.debug("DictComp")

    def visit_Div(self, node: ast.Div):
        self.debug("Div")
        self.append("/")

    def visit_Ellipsis(self, node: ast.Ellipsis):
        self.debug("Ellipsis")

    def visit_Eq(self, node: ast.Eq):
        self.debug("Eq")
        self.append("=")

    def visit_ExceptHandler(self, node: ast.ExceptHandler):
        self.debug("ExceptHandler")
        self.source_line(node)
        exception_name: str = self.adjust_identifier(node.name)
        exception_type: str = ast.unparse(node.type)

        self.append(f"catch ({exception_type} {exception_name}) {{")

        for i, stmt in enumerate(node.body):
            self.visit(stmt)

        self.append("}")

    def visit_Expr(self, node: ast.Expr):
        self.debug("Expr")
        self.source_line(node)
        self.append(ast.unparse(node) + ";")

    def visit_expr(self, node: ast.expr):
        self.debug("expr")
        self.debug(f"expr: {node}")

    def visit_Expression(self, node: ast.Expression):
        self.debug("Expression")

    def visit_ExtSlice(self, node: ast.ExtSlice):
        self.debug("ExtSlice")
        raise DeprecationWarning("ast.ExtSlice is deprecated. Use ast.Tuple instead.")

    def visit_FloorDiv(self, node: ast.FloorDiv):
        self.debug("FloorDiv")
        self.append("/")

    def visit_For(self, node: ast.For):
        self.debug("For")
        self.source_line(node)
        self.append("foreach (")
        if node.type_comment:
            self.append(node.type_comment)
            self.append(" ")
        else:
            self.append("var ")
        self.visit(node.target)
        self.append("in")
        self.visit(node.iter)
        self.append(")")

        # self.append(f"foreach (var {target} in {iter_expr}) {{")

        self.append("{")
        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_FormattedValue(self, node: ast.FormattedValue):
        self.debug("FormattedValue")

    def visit_FunctionDef(self, node: ast.FunctionDef):
        self.debug(f"FunctionDef {node.name} {node.args} {node.body} {node.returns}")

        self.source_line(node)
        self.append("public")

        # Return type
        self.push_context(CodegenContext(context_type=CodegenContextType.FUNCTION_RETURN_TYPE))

        if node.returns:
            self.visit(node.returns)

        self.pop_context()

        # Name and modifiers
        func_name: str = self.adjust_identifier(node.name)
        self.append(func_name)

        # Arguments
        self.push_context(CodegenContext(context_type=CodegenContextType.FUNCTION_ARGUMENT_TYPE))

        self.append("(")
        for i, arg in enumerate(node.args.args):
            # Self is implied in C#
            if i > 1 or (i == 0 and arg.arg != "self"):
                # arg_type = map_python_type_to_cs_type(ast.unparse(arg.annotation))
                arg_type = "Todo"
                self.append(f"{arg_type} {arg.arg}")

        self.append(")")

        self.pop_context()

        self.append("{")

        for i, stmt in enumerate(node.body):
            self.visit(stmt)

        self.append("}")

    def visit_GeneratorExp(self, node: ast.GeneratorExp):
        self.debug("GeneratorExp")

    def visit_Global(self, node: ast.Global):
        self.debug("Global")

    def visit_Gt(self, node: ast.Gt):
        self.debug("Gt")
        self.append(">")

    def visit_GtE(self, node: ast.GtE):
        self.debug("GtE")
        self.append(">=")

    def visit_If(self, node: ast.If):
        self.debug("If")
        self.source_line(node)

        self.append("if (")
        self.visit(node.test)
        self.append(") {")

        for s in node.body:
            self.visit(s)

        self.append("}")

        if node.orelse:
            self.append("else {")

            for s in node.orelse:
                self.visit(s)

        self.append("}")

    def visit_IfExp(self, node: ast.IfExp):
        self.debug("IfExp")

    def visit_Import(self, node: ast.Import):
        self.source_line(node)
        self.append(CodegenDirective.ONELINE)
        self.append("using")

        for n in node.names:
            self.visit(n)

        self.append(";")
        self.append(CodegenDirective.MULTILINE)

    def visit_ImportFrom(self, node: ast.ImportFrom):
        self.source_line(node)

        if node.module:
            for n in node.names:
                self.append("using")
                self.append(CodegenDirective.NOSPACE)
                self.append(node.module)
                self.append(".")
                self.visit(n)
                self.append(CodegenDirective.SPACE)
                self.append(";")
        else:
            raise NotImplementedError("Import from with relative imports")

    def visit_In(self, node: ast.In):
        self.debug("In")

    def visit_Index(self, node: ast.Index):
        self.debug("Index")

    def visit_Interactive(self, node: ast.Interactive):
        self.debug("Interactive")

    def visit_Is(self, node: ast.Is):
        self.debug("Is")

    def visit_IsNot(self, node: ast.IsNot):
        self.debug("IsNot")

    def visit_Invert(self, node: ast.Invert):
        self.debug("Invert")

    def visit_JoinedStr(self, node: ast.JoinedStr):
        self.debug("JoinedStr")

    def visit_keyword(self, node: ast.keyword):
        self.debug("keyword")

    def visit_Lambda(self, node: ast.Lambda):
        self.debug("Lambda")

    def visit_List(self, node: ast.List):
        self.debug("List")
        self.debug(f"List {node.ctx}")
        self.source_line(node)

        # TODO: correctly infer type
        self.append("new List<object> {")
        if node.elts:
            for x in node.elts:
                self.visit(x)
                self.append(",")

        self.append("}")

    def visit_ListComp(self, node: ast.ListComp):
        self.debug("ListComp")

    def visit_Load(self, node: ast.Load):
        self.debug("Load")

    def visit_LShift(self, node: ast.LShift):
        self.debug("LShift")

    def visit_Lt(self, node: ast.Lt):
        self.debug("Lt")
        self.append("<")

    def visit_LtE(self, node: ast.LtE):
        self.debug("LtE")
        self.append("<=")

    def visit(self, node):
        super().visit(node)

    def visit_Match(self, node: ast.Match):
        self.debug("Match")

    def visit_match_case(self, node: ast.match_case):
        self.debug("match_case")

    def visit_MatchAs(self, node: ast.MatchAs):
        self.debug("MatchAs")

    def visit_MatchClass(self, node: ast.MatchClass):
        self.debug("MatchClass")

    def visit_MatchMapping(self, node: ast.MatchMapping):
        self.debug("MatchMapping")

    def visit_MatchOr(self, node: ast.MatchOr):
        self.debug("MatchOr")

    def visit_MatchSequence(self, node: ast.MatchSequence):
        self.debug("MatchSequence")

    def visit_MatchSingleton(self, node: ast.MatchSingleton):
        self.debug("MatchSingleton")

    def visit_MatchStar(self, node: ast.MatchStar):
        self.debug("MatchStar")

    def visit_MatchValue(self, node: ast.MatchValue):
        self.debug("MatchValue")

    def visit_MatMult(self, node: ast.MatMult):
        self.debug("MatMult")

    def visit_Mod(self, node: ast.Mod):
        self.debug("Mod")

    def visit_Module(self, node: ast.Module):
        self.push_context(CodegenContext(context_type=CodegenContextType.MODULE))

        for stmt in node.body:
            self.visit(stmt)

        self.pop_context()

    def visit_Mult(self, node: ast.Mult):
        self.debug("Mult")

    def visit_Name(self, node: ast.Name):
        self.debug("Name")
        self.append(node.id)

    def visit_NameConstant(self, node: ast.NameConstant):
        self.debug("NameConstant")

    def visit_NamedExpr(self, node: ast.NamedExpr):
        self.debug("NamedExpr")

    def visit_Nonlocal(self, node: ast.Nonlocal):
        self.debug("Nonlocal")

    def visit_Not(self, node: ast.Not):
        self.debug("Not")
        self.append("!")
        self.source_line(node)

    def visit_NotEq(self, node: ast.NotEq):
        self.debug("NotEq")
        self.append("!=")
        self.source_line(node)

    def visit_NotIn(self, node: ast.NotIn):
        self.debug("NotIn")
        self.source_line(node)

    def visit_Num(self, node: ast.Num):
        self.debug("Num")

    def visit_Or(self, node: ast.Or):
        self.debug("Or")
        self.append("||")
        self.source_line(node)

    def visit_Param(self, node: ast.Param):
        self.debug("Param")

    def visit_ParamSpec(self, node: ast.ParamSpec):
        self.debug("ParamSpec")
        self.source_line(node)

    def visit_Pass(self, node: ast.Pass):
        self.debug("Pass")
        pass

    def visit_Pow(self, node: ast.Pow):
        self.debug("Pow")
        self.source_line(node)

    def visit_Raise(self, node: ast.Raise):
        self.debug("Raise")
        self.source_line(node)

    def visit_Return(self, node: ast.Return):
        self.debug("Return")
        self.source_line(node)
        self.append(f"return")

        if node.value:
            self.visit(node.value)

        self.append(";")

    def visit_RShift(self, node: ast.RShift):
        self.debug("RShift")

    def visit_Set(self, node: ast.Set):
        self.debug("Set")

    def visit_SetComp(self, node: ast.SetComp):
        self.debug("SetComp")

    def visit_Slice(self, node: ast.Slice):
        self.debug("Slice")

    def visit_Starred(self, node: ast.Starred):
        self.debug("Starred")

    def visit_Store(self, node: ast.Store):
        self.debug("Store")

    def visit_Str(self, node: ast.Str):
        self.debug("Str")

    def visit_Sub(self, node: ast.Sub):
        self.debug("Sub")

    def visit_Subscript(self, node: ast.Subscript):
        self.debug("Subscript")

    def visit_Suite(self, node: ast.Suite):
        self.debug("Suite")

    def visit_Try(self, node: ast.Try):
        self.debug("Try")
        self.source_line(node)
        self.append("try {")

        for (
            i,
            stmt,
        ) in enumerate(node.body):
            self.visit(stmt)

        self.append("}")

        for i, handler in enumerate(node.handlers):
            self.visit(handler)

        if node.finalbody:
            self.append("finally {")

            for i, stmt in enumerate(node.finalbody):
                self.visit(stmt)

            self.append("}")

    def visit_TryStar(self, node: ast.TryStar):
        self.debug("TryStar")

    def visit_Tuple(self, node: ast.Tuple):
        self.debug("Tuple")

    def visit_TypeAlias(self, node: ast.TypeAlias):
        self.debug("TypeAlias")

    def visit_TypeIgnore(self, node: ast.TypeIgnore):
        self.debug("TypeIgnore")

    def visit_TypeVar(self, node: ast.TypeVar):
        self.debug("TypeVar")

    def visit_TypeVarTuple(self, node: ast.TypeVarTuple):
        self.debug("TypeVarTuple")

    def visit_UAdd(self, node: ast.UAdd):
        self.debug("UAdd")

    def visit_UnaryOp(self, node: ast.UnaryOp):
        self.debug("UnaryOp")

    def visit_USub(self, node: ast.USub):
        self.debug("USub")

    def visit_While(self, node: ast.While):
        self.debug("While")
        self.source_line(node)
        self.append(f"while (")
        self.visit(node.test)
        self.append(") {")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_With(self, node: ast.With):
        self.debug("With")
        self.source_line(node)

    def visit_withitem(self, node: ast.withitem):
        self.debug("withitem")

    def visit_Yield(self, node: ast.Yield):
        self.debug("Yield")
        self.source_line(node)
        self.append(f"yield return {ast.unparse(node.value)}")

    def visit_YieldFrom(self, node: ast.YieldFrom):
        self.debug("YieldFrom")
        self.source_line(node)

    #######################
    # END VISITOR METHODS #
    #######################
