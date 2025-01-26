from pathlib import Path
from typing import Optional

from antlr4 import ParserRuleContext
from graphviz import Digraph
from PythonParser import PythonParser

from sharpy.compiler_toolchain.parser import ParseTreeNode


def render_as_png(parse_tree: ParseTreeNode, parser: PythonParser, output_path: Path):
    dot: Digraph = parse_tree_to_dot(tree=parse_tree, parser=parser)
    dot.render(outfile=output_path, format="png")


def parse_tree_to_dot(
    tree: ParseTreeNode,
    parser: PythonParser,
    dot: Optional[Digraph] = None,
) -> Digraph:
    if not dot:
        dot = Digraph()

    label: str = tree.getText()
    if not label:
        label = "<empty>"

    if isinstance(tree, ParserRuleContext):
        rule_index: int = tree.getRuleIndex()
        rule_name: str = parser.ruleNames[rule_index] if rule_index >= 0 else "<unknown_rule>"
        label += f"\nRule: {rule_name}"

    dot.node(name=str(id(tree)), label=label)

    if tree.getChildCount() > 0:
        for child in tree.getChildren():
            dot.edge(tail_name=str(id(tree)), head_name=str(id(child)))
            parse_tree_to_dot(tree=child, parser=parser, dot=dot)

    return dot
