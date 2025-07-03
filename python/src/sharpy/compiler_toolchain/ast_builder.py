from io import TextIOBase

from sharpy.compiler_toolchain.ast import Node


class ASTBuilderBase:
    def __init__(self):
        pass

    def generate_ast(self, input: TextIOBase) -> Node:
        raise NotImplementedError()
