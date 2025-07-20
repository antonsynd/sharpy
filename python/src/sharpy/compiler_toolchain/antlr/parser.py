from io import TextIOBase
from typing import MutableSequence

from antlr4 import CommonTokenStream, InputStream, ParserRuleContext
from SharpyLexer import SharpyLexer
from SharpyParser import SharpyParser
from SharpyParserListener import SharpyParserListener

from sharpy.compiler_toolchain.abc.parser import Parser

from ..antlr import ParseTreeNode


class AntlrParser(Parser, SharpyParserListener):
    def __init__(self):
        super().__init__()

    def parse_antlr(self, input: TextIOBase) -> ParseTreeNode | None:
        parse_tree: ParseTreeNode = self._generate_parse_tree(input=input)
        return self._postprocess_parse_tree(parse_tree=parse_tree)

    def _create_parser(self, input: TextIOBase) -> SharpyParser:
        stream = InputStream(data=input.read())
        lexer = SharpyLexer(input=stream)
        token_stream = CommonTokenStream(lexer=lexer)

        return SharpyParser(input=token_stream)

    def _generate_parse_tree(self, input: TextIOBase) -> ParseTreeNode:
        parser: SharpyParser = self._create_parser(input=input)

        # Parse the input, starting with the 'module' rule
        return parser.module()

    def _postprocess_parse_tree(self, parse_tree: ParseTreeNode) -> ParseTreeNode | None:
        parse_tree_opt: ParseTreeNode | None = self._prune_empty_nodes(node=parse_tree)

        return self._simplify_direct_lineages(node=parse_tree_opt) if parse_tree_opt else None

    def _prune_empty_nodes(self, node: ParseTreeNode) -> ParseTreeNode | None:
        # Ignore empty nodes
        if not node.getText().strip():
            return None

        num_children: int = node.getChildCount()

        if num_children > 0:
            pruned_children: MutableSequence[ParseTreeNode] = []

            for child in node.getChildren():
                pruned_child_opt: ParseTreeNode | None = self._prune_empty_nodes(node=child)

                # Keep a child if it was retained
                if pruned_child_opt:
                    pruned_children.append(pruned_child_opt)

            # Update children to only include pruned children
            node.children = pruned_children

        return node

    def _eliminate_leaf_for_terminal_rule_nodes(self, node: ParseTreeNode):
        if isinstance(node, ParserRuleContext):
            num_children: int = node.getChildCount()

            if num_children > 1:
                for child in node.getChildren():
                    self._eliminate_leaf_for_terminal_rule_nodes(node=child)
            elif num_children == 1:
                child: ParseTreeNode = node.getChild(0)

                # If the child is a terminal node with no rules, remove it
                if child.getChildCount() == 0:
                    print(f"Eliminating leaf node: {child.getText()}")
                    node.children = []
                    return

                # Otherwise, recursively eliminate leaf nodes for the child
                self._eliminate_leaf_for_terminal_rule_nodes(node=child)  # type: ignore

    def _simplify_direct_lineages(self, node: ParseTreeNode) -> ParseTreeNode:
        num_children: int = node.getChildCount()

        if num_children == 0:
            # Always return terminal nodes
            return node

        if num_children == 1:
            child: ParseTreeNode = node.getChild(0)

            # Prune the child node, but don't keep it unless it is the parent of
            # a terminal node
            pruned_child: ParseTreeNode = self._simplify_direct_lineages(node=child)
            assert pruned_child

            node.children = [pruned_child]
        else:
            pruned_children: MutableSequence[ParseTreeNode] = []

            # Recursively prune each child node
            for child in node.getChildren():
                # Reset keep first single child for new lineages of nodes
                pruned_child: ParseTreeNode = self._simplify_direct_lineages(node=child)

                # Keep a child if it was retained
                if pruned_child:
                    pruned_children.append(pruned_child)

            # Update children to only include pruned children
            node.children = pruned_children

        # Update count after pruning
        num_children: int = node.getChildCount()
        assert num_children

        if num_children == 1:
            child: ParseTreeNode = node.getChild(0)

            # If this node's child is terminal keep it
            if child.getChildCount() == 0:
                return node
            else:
                # Otherwise get the child directly
                return child
        # Else
        return node
