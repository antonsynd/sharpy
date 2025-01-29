import ast
import re
from typing import Mapping, MutableSequence, Optional

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


class PythonToCSharp(ast.NodeVisitor):
    def __init__(self):
        self._result: MutableSequence[str] = []
        self._indent: int = 0
        self._file_name: str = "example.spy"

    def indent(self) -> int:
        self._indent += 1

        return self._indent * 4

    def get_indent(self) -> int:
        return self._indent * 4

    def newline(self) -> None:
        self._result.append("")

    def source_line(
        self, line_num: int, col_offset: int, end_line_num: int, end_col_offset: int
    ) -> None:
        self.append(
            f'#line ({line_num}, {col_offset}) - ({end_line_num}, {end_col_offset}) "{self._file_name}"'
        )

    def dedent(self) -> int:
        if self._indent > 0:
            self._indent -= 1
        else:
            raise Exception("Cannot dedent anymore")

        return self._indent * 4

    def append(self, s: str) -> None:
        self._result.append(" " * self.get_indent() + s)

    def visit_Module(self, node):
        for stmt in node.body:
            self.visit(stmt)

    def visit_FunctionDef(self, node: ast.FunctionDef):
        self.newline()

        # Convert return type
        ret_type: str = map_python_type_to_cs_type(ast.unparse(node.returns))
        args: MutableSequence[str] = []

        for i, arg in enumerate(node.args.args):
            # Self is implied in C#
            if i > 1 or (i == 0 and arg.arg != "self"):
                arg_type = map_python_type_to_cs_type(ast.unparse(arg.annotation))
                args.append(f"{arg_type} {arg.arg}")

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"public {ret_type} {node.name}({', '.join(args)}) {{")
        self.indent()

        for i, stmt in enumerate(node.body):
            self.visit(stmt)

        self.dedent()
        self.append("}")

    def visit_Return(self, node: ast.Return):
        value = ast.unparse(node.value) if node.value else ""

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"return {value};")

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
        self.newline()

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"if ({ast.unparse(node.test)}) {{")
        self.indent()

        for stmt in node.body:
            self.visit(stmt)

        self.dedent()
        self.append("}")

        if node.orelse:
            self.append("else {")
            self.indent()

            for stmt in node.orelse:
                self.visit(stmt)

            self.dedent()
            self.append("}")

    def visit_While(self, node: ast.While):
        self.newline()

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"while ({ast.unparse(node.test)}) {{")
        self.indent()

        for stmt in node.body:
            self.visit(stmt)

        self.dedent()
        self.append("}")

    def visit_For(self, node: ast.For):
        self.newline()

        iter_expr: str = ast.unparse(node.iter)
        target: str = ast.unparse(node.target)

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"foreach (var {target} in {iter_expr}) {{")
        self.indent()

        for stmt in node.body:
            self.visit(stmt)

        self.dedent()
        self.append("}")

    def visit_ClassDef(self, node: ast.ClassDef):
        self.newline()

        self.source_line(node.lineno, node.col_offset, node.end_lineno, node.end_col_offset)
        self.append(f"public class {node.name} {{")
        self.indent()

        for stmt in node.body:
            self.visit(stmt)

        self.dedent()
        self.append("}")

    def generate_csharp(self, python_code: str) -> str:
        tree: ast.Module = ast.parse(python_code)

        self.visit(tree)

        return "\n".join(self._result)
