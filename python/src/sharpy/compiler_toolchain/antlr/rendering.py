from io import StringIO
from pathlib import Path
from tempfile import NamedTemporaryFile
from typing import Any, MutableSequence

from antlr4 import ParserRuleContext
from graphviz import Digraph
from SharpyParser import SharpyParser

from sharpy.compiler_toolchain.antlr import ParseTreeNode
from sharpy.compiler_toolchain.ast import Node as ASTNode
from sharpy.compiler_toolchain.logging import logger


def render_parse_tree_as_png(parse_tree: ParseTreeNode, parser: SharpyParser, output_path: Path):
    dot: Digraph = parse_tree_to_dot(tree=parse_tree, parser=parser)

    with NamedTemporaryFile(suffix=".gv", mode="w+") as temp_file:
        dot.render(filename=temp_file.name, outfile=output_path, format="png")


def render_ast_as_png(ast: ASTNode, output_path: Path):
    dot: Digraph = ast_to_dot(node=ast)
    with NamedTemporaryFile(suffix=".gv", mode="w+") as temp_file:
        dot.render(filename=temp_file.name, outfile=output_path, format="png")


def parse_tree_to_dot(
    tree: ParseTreeNode,
    parser: SharpyParser,
    dot: Digraph | None = None,
) -> Digraph:
    if not dot:
        dot = Digraph()

    num_children: int = tree.getChildCount()

    label_opt: str | None = tree.getText()
    label: str = "<empty>"

    logger.debug(f"Processing node: {label_opt} with {num_children} children")
    if num_children > 1:
        label: str = ""
    elif label_opt:
        label: str = label_opt

    if isinstance(tree, ParserRuleContext):
        rule_index: int = tree.getRuleIndex()
        rule_name: str = parser.ruleNames[rule_index] if rule_index >= 0 else "<unknown_rule>"

        if label:
            label += f"\nRule: {rule_name}"
        else:
            label = f"Rule: {rule_name}"

    logger.debug(f"Adding node {id(tree)} with label: {label}")
    dot.node(name=str(id(tree)), label=label)

    if tree.getChildCount() > 0:
        for child in tree.getChildren():
            dot.edge(tail_name=str(id(tree)), head_name=str(id(child)))
            parse_tree_to_dot(tree=child, parser=parser, dot=dot)

    return dot


def ast_to_dot(
    node: ASTNode,
    dot: Digraph | None = None,
    parent_id: str | None = None,
) -> Digraph:
    """
    Recursively convert an AST node to a Graphviz Digraph.
    node: the AST node (should have .__class__.__name__ and fields/children)
    dot: existing Digraph or None
    parent_id: id of the parent node (for edge creation)
    label_fn: optional function to customize node labels
    """
    if dot is None:
        dot = Digraph()

    is_node: bool = isinstance(node, ASTNode)

    if is_node:
        node_id = str(id(node))
    else:
        node_id = StringIO(str(id(node))).getvalue()

    if is_node:
        label: str = node.__class__.__name__
    else:
        label: str = str(node)

    dot.node(node_id, label=label)

    if parent_id:
        dot.edge(parent_id, node_id)

    if not is_node:
        return dot

    # Recursively add children
    children: MutableSequence[Any] = []

    if is_node:
        for key, value in node.__dict__.items():
            logger.debug(f"[{node.__class__.__name__}] Processing field '{key}' with value: {value}")

            if isinstance(value, list):
                children.extend([v for v in value if hasattr(v, "__class__")])
            elif hasattr(value, "__class__"):
                children.append(value)

    for child in children:
        ast_to_dot(child, dot=dot, parent_id=node_id)

    return dot
