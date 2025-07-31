import uuid
import xml.etree.ElementTree as ET
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


def render_parse_tree_as_xml(
    parse_tree: ParseTreeNode, parser: SharpyParser, output_path: Path
) -> None:
    """
    Render a ParseTreeNode and its children as XML and write to the specified path.

    Args:
        parse_tree: The root ParseTreeNode to render
        parser: The SharpyParser instance (for rule name lookup)
        output_path: Path where the XML file will be written
    """
    root_element = parse_tree_to_xml(parse_tree, parser)

    # Create the XML tree and write to file
    tree = ET.ElementTree(root_element)
    ET.indent(tree, space="  ", level=0)  # Pretty formatting

    logger.debug(f"Writing parse tree XML to {output_path}")
    tree.write(output_path, encoding="utf-8", xml_declaration=True)


def parse_tree_to_xml(
    tree: ParseTreeNode, parser: SharpyParser, parent_element: ET.Element | None = None
) -> ET.Element:
    """
    Convert a ParseTreeNode to an XML Element recursively.

    Args:
        tree: The ParseTreeNode to convert
        parser: The SharpyParser instance (for rule name lookup)
        parent_element: The parent XML element (None for root)

    Returns:
        The XML Element representing this ParseTreeNode
    """
    # Determine the element name and type
    if isinstance(tree, ParserRuleContext):
        rule_index = tree.getRuleIndex()
        rule_name = parser.ruleNames[rule_index] if rule_index >= 0 else "unknown_rule"
        element_name = "parse_rule"
        node_type = "rule"
    else:
        # Terminal node
        element_name = "terminal"
        node_type = "terminal"
        rule_name = None

    # Create the XML element
    element = ET.Element(element_name)

    # Add attributes
    element.set("id", str(id(tree)))
    element.set("type", node_type)

    if rule_name:
        element.set("rule_name", rule_name)

    # Add text content
    text_content = tree.getText()
    if text_content:
        element.set("text", text_content)

    # Add child count for debugging
    child_count = tree.getChildCount()
    element.set("child_count", str(child_count))

    # Recursively add children
    if child_count > 0:
        children_element = ET.SubElement(element, "children")
        for child in tree.getChildren():
            child_element = parse_tree_to_xml(child, parser)
            children_element.append(child_element)

    logger.debug(
        f"Created XML element for node {id(tree)}: {element_name} ({rule_name if rule_name else 'terminal'})"
    )

    return element


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

    if is_node:
        node_id = str(id(node))
    else:
        # Literal values in Python are shared, so for readability, we append a
        # UUID to ensure unique node IDs for the same value.
        node_id = f"value_{id(node)}_{uuid.uuid4().hex}"

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
    children: MutableSequence[tuple[str | None, Any]] = []

    if is_node:
        for key, value in node.__dict__.items():
            logger.debug(
                f"[{node.__class__.__name__}] Processing field '{key}' with value: {value}"
            )

            if key in {"_source"}:
                # Don't emit source information
                continue

            key = key.lstrip("_")

            if isinstance(value, list):
                num_subvalues: int = len(value)

                for i, v in enumerate(value):
                    if not hasattr(v, "__class__"):
                        continue

                    if isinstance(node, (List, Tuple, Set)):
                        new_key = f"[{i}]"
                    elif num_subvalues > 1:
                        new_key = f"{key}[{i}]"
                    else:
                        new_key = key

                    logger.debug(f"{new_key}: adding child node: {value}")
                    children.append((new_key, v))
            elif hasattr(value, "__class__"):
                logger.debug(f"{key}: adding child node: {value}")
                children.append((key.lstrip("_"), value))

    logger.debug(f"Node {node_id} has children: {children}")

    for child in children:
        ast_to_dot(child[1], dot=dot, parent_id=node_id, label_prefix=child[0])

    return dot
