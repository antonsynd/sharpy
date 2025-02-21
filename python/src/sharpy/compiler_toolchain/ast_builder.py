from io import TextIOBase

from sharpy.compiler_toolchain.ast import AST


class ASTBuilderBase:
    def __init__(self):
        pass

    def generate_ast(self, input: TextIOBase) -> AST:
        raise NotImplementedError()
