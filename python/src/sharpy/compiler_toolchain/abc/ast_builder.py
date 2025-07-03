from sharpy.compiler_toolchain.abc.parser import Node as ParseTreeNode
from sharpy.compiler_toolchain.ast import Node as ASTNode


class ASTBuilder:
    """
    Base class for AST builders.
    """

    def generate_ast(self, input: ParseTreeNode) -> ASTNode | None:
        raise NotImplementedError()
