from io import TextIOBase

from SharpyParser import SharpyParser


# Export
ParseTreeNode = SharpyParser.File_inputContext


# TODO: Figure out how to abstract the actual ANTLR parse tree node
# from the parser interface.
class ParserBase:
    def parse(self, input: TextIOBase) -> ParseTreeNode | None:
        raise NotImplementedError()
