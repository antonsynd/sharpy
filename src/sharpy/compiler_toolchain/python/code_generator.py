import ast
import re
import subprocess
import sys

from pathlib import Path
from enum import Enum, auto
from io import TextIOBase
from typing import Mapping, MutableSequence, Optional, Sequence

# Mapping Python types to C#
TYPE_MAP: Mapping[str, str] = {
    "bool": "bool",
    "decimal": "decimal",
    "double": "double",
    "int": "int",
    "float": "float",
    "long": "long",
    "None": "void",  # Only in return types
    "object": "object",
    "short": "short",
    "str": "string",
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

    def visit_Module(self, node):
        self.push_context(CodegenContext(context_type=CodegenContextType.MODULE))

        for stmt in node.body:
            self.visit(stmt)

        self.pop_context()

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

    def visit_Return(self, node: ast.Return):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"return")

        if node.value:
            self.visit_expr(node.value)

        self.append(";")

    def visit_AnnAssign(self, node: ast.AnnAssign):
        var_type: str = map_python_type_to_cs_type(ast.unparse(node.annotation))
        target: str = ast.unparse(node.target)
        value: str = ast.unparse(node.value) if node.value else ""

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"{var_type} {target} = {value};")

    def visit_Assign(self, node: ast.Assign):
        targets: str = ", ".join(ast.unparse(t) for t in node.targets)
        value: str = ast.unparse(node.value)

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"var {targets} = {value};")

    def visit_Expr(self, node: ast.Expr):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(ast.unparse(node) + ";")

    def visit_If(self, node: ast.If):

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"if ({ast.unparse(node.test)}) {{")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

        if node.orelse:
            self.append("else {")

        for stmt in node.orelse:
            self.visit(stmt)

        self.append("}")

    def visit_Constant(self, node):
        match ast.unparse(node):
            case "None":
                self.append("null")
            case "True" | "False" as boolean_literal:
                self.append(boolean_literal.lower())
            case _ as str_literal:
                self.append(f'"{str_literal[1:-1]}"')

    def visit_Break(self, node: ast.Break):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append("break;")

    def visit_Continue(self, node: ast.Continue):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append("continue;")

    def visit_Pass(self, node: ast.Pass):
        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append("// pass\n")

    def visit_BinOp(self, node: ast.BinOp):
        self.visit_expr(node.left)
        self.visit_expr(node.op)
        self.visit_expr(node.right)

    def visit_Add(self, node: ast.Add):
        self.append(" + ")

    def visit_Name(self, node: ast.Name):
        self.append(node.id)

    def visit_expr(self, node: ast.expr):
        match type(node):
            case ast.Compare:
                self.visit_Compare(node)
            case ast.BoolOp:
                self.visit_BoolOp(node)
            case ast.Name:
                self.visit_Name(node)
            case ast.Constant:
                self.visit_Constant(node)
            case ast.BinOp:
                self.visit_BinOp(node)
            case ast.Add:
                self.visit_Add(node)
            case _ as unsupported:
                print(unsupported)

        return ast.unparse(node)  # Default case

    def visit_While(self, node: ast.While):

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"while (")
        self.visit_expr(node.test)
        self.append(") {")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_For(self, node: ast.For):

        iter_expr: str = ast.unparse(node.iter)
        target: str = ast.unparse(node.target)

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"foreach (var {target} in {iter_expr}) {{")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def visit_ClassDef(self, node: ast.ClassDef):

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        class_name: str = self.adjust_identifier(
            s=node.name, context_type=CodegenContextType.CLASS_NAME
        )
        self.append(f"public class {class_name} {{")

        for stmt in node.body:
            self.visit(stmt)

        self.append("}")

    def generate_csharp(
        self, buffer: TextIOBase, file_name: Optional[str] = None, format: bool = False
    ) -> str:
        self.set_file_name(file_name)

        tree: ast.Module = ast.parse(buffer)

        self.append("namespace test {")

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
