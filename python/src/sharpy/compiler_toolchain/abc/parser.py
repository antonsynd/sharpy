from io import TextIOBase
from typing import Self


class Node:
    """
    Base class for all nodes in the parse tree.
    """

    def children(self) -> list[Self]:
        raise NotImplementedError()

    def text(self) -> str:
        raise NotImplementedError()

    def rule_name(self) -> str:
        """
        Return the name of the grammar rule that produced this node, or token type
        """
        raise NotImplementedError()

    def get_token_type(self) -> str | None:
        """
        Optionally return token type if this node is a token node.
        """
        return None


class Parser:
    """
    Base class for all parsers.
    """

    def parse(self, input: TextIOBase) -> Node | None:
        raise NotImplementedError()
