import ast
import re
import subprocess
import sys
from enum import Enum, auto
from io import TextIOBase
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


class PythonToCSharp(ast.NodeVisitor):
    def __init__(
        self,
        emit_line_metadata: bool = False,
        format: bool = False,
        csharpier_path: Optional[str] = None,
    ):
        self._result: MutableSequence[str] = []
        self._emit_line_metadata: bool = emit_line_metadata
        self._format: bool = format
        self._csharpier_path: Optional[str] = csharpier_path

        self._file_name: str = "anonymous.spy"
        self._context_stack: MutableSequence[CodegenContext] = []

    def set_file_name(self, s: str) -> None:
        self._file_name = s if s else "anonymous.spy"

    def peek_context_type(self) -> CodegenContextType:
        return self._context_stack[-1].get_type()

    def push_context(self, context: CodegenContext) -> None:
        self._context_stack.append(context)

    def pop_context(self) -> CodegenContext:
        return self._context_stack.pop()

    def source_line(
        self, line_num: int, col_offset: int, end_line_num: int, end_col_offset: int
    ) -> None:
        if self._emit_line_metadata:
            # Python column offsets are 0-indexed, but C# is 1-indexed
            self.append(
                f'\n#line ({line_num}, {col_offset + 1}) - ({end_line_num}, {end_col_offset + 1}) "{self._file_name}"\n'
            )

    def append(self, s: str) -> None:
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

    def visit_Add(self, node: ast.Add):
        self.append(" + ")

    def visit_alias(self, node: ast.alias):
        print("alias")

    def visit_And(self, node: ast.And):
        self.append(" && ")

    def visit_AnnAssign(self, node: ast.AnnAssign):
        var_type: str = map_python_type_to_cs_type(ast.unparse(node.annotation))
        target: str = ast.unparse(node.target)
        value: str = ast.unparse(node.value) if node.value else ""

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"{var_type} {target} = {value};")

    def visit_arg(self, node: ast.arg):
        print("arg")

    def visit_arguments(self, node: ast.arguments):
        print("arguments")

    def visit_Assert(self, node: ast.Assert):
        print("[DEBUG] Assert")

    def visit_Assign(self, node: ast.Assign):
        targets: str = ", ".join(ast.unparse(t) for t in node.targets)
        value: str = ast.unparse(node.value)

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"var {targets} = {value};")

    def visit_AsyncFor(self, node: ast.AsyncFor):
        print("[DEBUG] AsyncFor")

    def visit_AsyncFunctionDef(self, node: ast.AsyncFunctionDef):
        print("[DEBUG] AsyncFunctionDef")

    def visit_AsyncWith(self, node: ast.AsyncWith):
        print("[DEBUG] AsyncWith")

    def visit_Attribute(self, node: ast.Attribute):
        print("[DEBUG] Attribute")

    def visit_AugAssign(self, node: ast.AugAssign):
        print("[DEBUG] AugAssign")

    def visit_AugLoad(self, node: ast.AugLoad):
        print("[DEBUG] AugLoad")

    def visit_AugStore(self, node: ast.AugStore):
        print("[DEBUG] AugStore")

    def visit_Await(self, node: ast.Await):
        print("[DEBUG] Await")

    def visit_BinOp(self, node: ast.BinOp):
        self.visit(node.left)
        self.visit(node.op)
        self.visit(node.right)

    def visit_BitAnd(self, node: ast.BitAnd):
        print("[DEBUG] BitAnd")

    def visit_BitOr(self, node: ast.BitOr):
        print("[DEBUG] BitOr")

    def visit_BitXor(self, node: ast.BitXor):
        print("[DEBUG] BitXor")

    def visit_BoolOp(self, node: ast.BoolOp):
        print("[DEBUG] BoolOp")

    def visit_Break(self, node: ast.Break):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append("break;")

    def visit_Bytes(self, node: ast.Bytes):
        print("[DEBUG] Bytes")

    def visit_Call(self, node: ast.Call):
        print("[DEBUG] Call")

    def visit_ClassDef(self, node: ast.ClassDef):

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        class_name: str = self.adjust_identifier(
            s=node.name, context_type=CodegenContextType.CLASS_NAME
        )
        self.append(f"public class {class_name} {{")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_Compare(self, node: ast.Compare):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.visit(node.left)
        self.visit(node.ops[0])
        self.visit(node.comparators[0])

    def visit_comprehension(self, node: ast.comprehension):
        print("[DEBUG] comprehension")

    def visit_Constant(self, node: ast.Constant):
        match ast.unparse(node):
            case "None":
                self.append("null")
            case "True" | "False" as boolean_literal:
                self.append(boolean_literal.lower())
            case _ as str_literal:
                self.append(f'"{str_literal[1:-1]}"')

    def visit_Continue(self, node: ast.Continue):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append("continue;")

    def visit_Del(self, node: ast.Del):
        print("[DEBUG] Del")

    def visit_Delete(self, node: ast.Delete):
        print("[DEBUG] Delete")

    def visit_Dict(self, node: ast.Dict):
        print("[DEBUG] Dict")

    def visit_DictComp(self, node: ast.DictComp):
        print("[DEBUG] DictComp")

    def visit_Div(self, node: ast.Div):
        print("[DEBUG] Div")

    def visit_Ellipsis(self, node: ast.Ellipsis):
        print("[DEBUG] Ellipsis")

    def visit_Eq(self, node: ast.Eq):
        self.append(" = ")

    def visit_ExceptHandler(self, node: ast.ExceptHandler):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        exception_name: str = self.adjust_identifier(node.name)
        exception_type: str = ast.unparse(node.type)

        self.append(f"catch ({exception_type} {exception_name}) {{")

        for i, stmt in enumerate(node.body):
            self.visit(stmt)

        self.append("}")

    def visit_Expr(self, node: ast.Expr):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(ast.unparse(node) + ";")

    def visit_Expression(self, node: ast.Expression):
        print("[DEBUG] Expression")

    def visit_ExtSlice(self, node: ast.ExtSlice):
        print("[DEBUG] ExtSlice")

    def visit_FloorDiv(self, node: ast.FloorDiv):
        print("[DEBUG] FloorDiv")

    def visit_For(self, node: ast.For):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append("foreach (var ")
        self.visit(node.iter)
        self.append(" in ")
        self.visit(node.target)
        self.append(")")

        # self.append(f"foreach (var {target} in {iter_expr}) {{")

        self.append("{")
        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_FormattedValue(self, node: ast.FormattedValue):
        print("[DEBUG] FormattedValue")

    def visit_FunctionDef(self, node: ast.FunctionDef):
        # Return type
        self.push_context(CodegenContext(context_type=CodegenContextType.FUNCTION_RETURN_TYPE))
        ret_type: str = map_python_type_to_cs_type(ast.unparse(node.returns))
        self.pop_context()

        # Arguments
        self.push_context(CodegenContext(context_type=CodegenContextType.FUNCTION_ARGUMENT_TYPE))
        args: MutableSequence[str] = []

        for i, arg in enumerate(node.args.args):
            # Self is implied in C#
            if i > 1 or (i == 0 and arg.arg != "self"):
                arg_type = map_python_type_to_cs_type(ast.unparse(arg.annotation))
                args.append(f"{arg_type} {arg.arg}")
        self.pop_context()

        # Name and modifiers
        func_name: str = self.adjust_identifier(node.name)

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"public {ret_type} {func_name}({', '.join(args)}) {{")

        for i, stmt in enumerate(node.body):
            self.visit(stmt)

        self.append("}")

    def visit_GeneratorExp(self, node: ast.GeneratorExp):
        print("[DEBUG] GeneratorExp")

    def visit_Global(self, node: ast.Global):
        print("[DEBUG] Global")

    def visit_Gt(self, node: ast.Gt):
        print("[DEBUG] Gt")

    def visit_GtE(self, node: ast.GtE):
        print("[DEBUG] GtE")

    def visit_If(self, node: ast.If):
        print("[DEBUG] If")

    def visit_IfExp(self, node: ast.IfExp):
        print("[DEBUG] IfExp")

    def visit_Import(self, node: ast.Import):
        print("[DEBUG] Import")

    def visit_ImportFrom(self, node: ast.ImportFrom):
        print("[DEBUG] ImportFrom")

    def visit_In(self, node: ast.In):
        print("[DEBUG] In")

    def visit_Index(self, node: ast.Index):
        print("[DEBUG] Index")

    def visit_Interactive(self, node: ast.Interactive):
        print("[DEBUG] Interactive")

    def visit_Is(self, node: ast.Is):
        print("[DEBUG] Is")

    def visit_IsNot(self, node: ast.IsNot):
        print("[DEBUG] IsNot")

    def visit_Invert(self, node: ast.Invert):
        print("[DEBUG] Invert")

    def visit_JoinedStr(self, node: ast.JoinedStr):
        print("[DEBUG] JoinedStr")

    def visit_keyword(self, node: ast.keyword):
        print("[DEBUG] keyword")

    def visit_Lambda(self, node: ast.Lambda):
        print("[DEBUG] Lambda")

    def visit_List(self, node: ast.List):
        print("[DEBUG] List")

    def visit_ListComp(self, node: ast.ListComp):
        print("[DEBUG] ListComp")

    def visit_Load(self, node: ast.Load):
        print("[DEBUG] Load")

    def visit_LShift(self, node: ast.LShift):
        print("[DEBUG] LShift")

    def visit_Lt(self, node: ast.Lt):
        print("[DEBUG] Lt")

    def visit_LtE(self, node: ast.LtE):
        print("[DEBUG] LtE")

    def visit_Match(self, node: ast.Match):
        print("[DEBUG] Match")

    def visit_match_case(self, node: ast.match_case):
        print("[DEBUG] match_case")

    def visit_MatchAs(self, node: ast.MatchAs):
        print("[DEBUG] MatchAs")

    def visit_MatchClass(self, node: ast.MatchClass):
        print("[DEBUG] MatchClass")

    def visit_MatchMapping(self, node: ast.MatchMapping):
        print("[DEBUG] MatchMapping")

    def visit_MatchOr(self, node: ast.MatchOr):
        print("[DEBUG] MatchOr")

    def visit_MatchSequence(self, node: ast.MatchSequence):
        print("[DEBUG] MatchSequence")

    def visit_MatchSingleton(self, node: ast.MatchSingleton):
        print("[DEBUG] MatchSingleton")

    def visit_MatchStar(self, node: ast.MatchStar):
        print("[DEBUG] MatchStar")

    def visit_MatchValue(self, node: ast.MatchValue):
        print("[DEBUG] MatchValue")

    def visit_MatMult(self, node: ast.MatMult):
        print("[DEBUG] MatMult")

    def visit_Mod(self, node: ast.Mod):
        print("[DEBUG] Mod")

    def visit_Module(self, node: ast.Module):
        self.push_context(CodegenContext(context_type=CodegenContextType.MODULE))

        for stmt in node.body:
            self.visit(stmt)

        self.pop_context()

    def visit_Mult(self, node: ast.Mult):
        print("[DEBUG] Mult")

    def visit_Name(self, node: ast.Name):
        self.append(node.id)

    def visit_NameConstant(self, node: ast.NameConstant):
        print("[DEBUG] NameConstant")

    def visit_NamedExpr(self, node: ast.NamedExpr):
        print("[DEBUG] NamedExpr")

    def visit_Nonlocal(self, node: ast.Nonlocal):
        print("[DEBUG] Nonlocal")

    def visit_Not(self, node: ast.Not):
        print("[DEBUG] Not")

    def visit_NotEq(self, node: ast.NotEq):
        self.append(" != ")

    def visit_NotIn(self, node: ast.NotIn):
        print("[DEBUG] NotIn")

    def visit_Num(self, node: ast.Num):
        print("[DEBUG] Num")

    def visit_Or(self, node: ast.Or):
        self.append(" || ")

    def visit_Param(self, node: ast.Param):
        print("[DEBUG] Param")

    def visit_ParamSpec(self, node: ast.ParamSpec):
        print("[DEBUG] ParamSpec")

    def visit_Pass(self, node: ast.Pass):
        pass

    def visit_Pow(self, node: ast.Pow):
        print("[DEBUG] Pow")

    def visit_Raise(self, node: ast.Raise):
        print("[DEBUG] Raise")

    def visit_Return(self, node: ast.Return):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"return")

        if node.value:
            self.visit(node.value)

        self.append(";")

    def visit_RShift(self, node: ast.RShift):
        print("[DEBUG] RShift")

    def visit_Set(self, node: ast.Set):
        print("[DEBUG] Set")

    def visit_SetComp(self, node: ast.SetComp):
        print("[DEBUG] SetComp")

    def visit_Slice(self, node: ast.Slice):
        print("[DEBUG] Slice")

    def visit_Starred(self, node: ast.Starred):
        print("[DEBUG] Starred")

    def visit_Store(self, node: ast.Store):
        print("[DEBUG] Store")

    def visit_Str(self, node: ast.Str):
        print("[DEBUG] Str")

    def visit_Sub(self, node: ast.Sub):
        print("[DEBUG] Sub")

    def visit_Subscript(self, node: ast.Subscript):
        print("[DEBUG] Subscript")

    def visit_Suite(self, node: ast.Suite):
        print("[DEBUG] Suite")

    def visit_Try(self, node: ast.Try):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
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
        print("[DEBUG] TryStar")

    def visit_Tuple(self, node: ast.Tuple):
        print("[DEBUG] Tuple")

    def visit_TypeAlias(self, node: ast.TypeAlias):
        print("[DEBUG] TypeAlias")

    def visit_TypeIgnore(self, node: ast.TypeIgnore):
        print("[DEBUG] TypeIgnore")

    def visit_TypeVar(self, node: ast.TypeVar):
        print("[DEBUG] TypeVar")

    def visit_TypeVarTuple(self, node: ast.TypeVarTuple):
        print("[DEBUG] TypeVarTuple")

    def visit_UAdd(self, node: ast.UAdd):
        print("[DEBUG] UAdd")

    def visit_UnaryOp(self, node: ast.UnaryOp):
        print("[DEBUG] UnaryOp")

    def visit_USub(self, node: ast.USub):
        print("[DEBUG] USub")

    def visit_While(self, node: ast.While):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"while (")
        self.visit(node.test)
        self.append(") {")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_With(self, node: ast.With):
        print("[DEBUG] With")

    def visit_withitem(self, node: ast.withitem):
        print("[DEBUG] withitem")

    def visit_Yield(self, node: ast.Yield):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"yield return {ast.unparse(node.value)}")

    def visit_YieldFrom(self, node: ast.YieldFrom):
        print("[DEBUG] YieldFrom")

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

                for s in self._result:
                    proc.stdin.write(s)

                proc.stdin.close()
            except subprocess.CalledProcessError as e:
                print(e.stderr, file=sys.stderr)
                print(e, file=sys.stderr)
                sys.exit(1)

            return proc.stdout.read()
        else:
            return "".join(self._result)
