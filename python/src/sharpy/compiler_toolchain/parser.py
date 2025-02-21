from io import TextIOBase

from PythonParser import PythonParser

# Export
ParseTreeNode = PythonParser.File_inputContext


class ParserBase:
    def parse(self, input: TextIOBase) -> ParseTreeNode:
        raise NotImplementedError()
