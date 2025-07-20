from pathlib import Path
from tempfile import NamedTemporaryFile
from typing import Any, MutableSequence, Tuple

from antlr4 import ParserRuleContext
from graphviz import Digraph
from SharpyParser import SharpyParser

from sharpy.compiler_toolchain.antlr import ParseTreeNode
from sharpy.compiler_toolchain.ast import Dict, List
from sharpy.compiler_toolchain.ast import Node as ASTNode
from sharpy.compiler_toolchain.ast import Set, Tuple
from sharpy.compiler_toolchain.logging import logger


def render_parse_tree_as_png(
    parse_tree: ParseTreeNode, parser: SharpyParser, output_path: Path, keep_temp: bool = False
) -> Path | None:
    dot: Digraph = parse_tree_to_dot(tree=parse_tree, parser=parser)

    with NamedTemporaryFile(suffix=".gv", mode="w+", delete=not keep_temp) as temp_file:
        logger.debug(f"Rendering parse tree to {temp_file.name}...")
        dot.render(filename=temp_file.name, outfile=output_path, format="png")

        if keep_temp:
            return Path(temp_file.name)
        else:
            return None


def render_ast_as_png(ast: ASTNode, output_path: Path, keep_temp: bool = False) -> Path | None:
    dot: Digraph = ast_to_dot(node=ast)
    with NamedTemporaryFile(suffix=".gv", mode="w+", delete=not keep_temp) as temp_file:
        logger.debug(f"Rendering parse tree to {temp_file.name}...")
        dot.render(filename=temp_file.name, outfile=output_path, format="png")

        if keep_temp:
            return Path(temp_file.name)
        else:
            return None


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


_node_list: MutableSequence[str] = list()


def ast_to_dot(
    node: ASTNode,
    dot: Digraph | None = None,
    parent_id: str | None = None,
    label_prefix: str | None = None,
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

    node_id = str(id(node))

    if is_node:
        label: str = node.__class__.__name__
    else:
        label: str = str(node)

    if label_prefix:
        label = f"{label_prefix}: {label}"

    logger.debug(f"Processing node: {label} with ID: {node_id}")
    _node_list.append(f"{node_id}: {label}")

    dot.node(node_id, label=label)

    if parent_id:
        dot.edge(parent_id, node_id)

    if not is_node:
        return dot

    # Recursively add children
    children: MutableSequence[Tuple[str | None, Any]] = []

    if is_node:
        for key, value in node.__dict__.items():
            logger.debug(
                f"[{node.__class__.__name__}] Processing field '{key}' with value: {value}"
            )

            if key in {"_source"}:
                # Don't emit source information
                continue

            new_key: str | None = None

            if key in {"_body"}:
                new_key = None
            else:
                new_key = key

            if isinstance(value, list):
                for i, v in enumerate(value):
                    if not hasattr(v, "__class__"):
                        continue

                    new_key: str | None = (
                        new_key if not isinstance(node, (List, Tuple, Set, Dict)) else f"[{i}]"
                    )

                    logger.debug(f"{new_key}: adding child node: {value}")
                    children.append((new_key, v))
            elif hasattr(value, "__class__"):
                logger.debug(f"{key}: adding child node: {value}")
                children.append((key.lstrip("_"), value))

    logger.debug(f"Node {node_id} has children: {children}")

    for child in children:
        ast_to_dot(child[1], dot=dot, parent_id=node_id, label_prefix=child[0])

    return dot
