from pathlib import Path
from tempfile import NamedTemporaryFile
from typing import Any, Callable, MutableSequence

from antlr4 import ParserRuleContext
from graphviz import Digraph
from SharpyParser import SharpyParser

from sharpy.compiler_toolchain.antlr import ParseTreeNode
from sharpy.compiler_toolchain.ast import Node as ASTNode


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

    print(f"Processing node: {label_opt} with {num_children} children")
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

    print(f"Adding node {id(tree)} with label: {label}")
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
    label_fn: Callable | None = None,
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

    node_id = str(id(node))
    # Default label: class name and (optionally) id/name field
    if label_fn:
        label = label_fn(node)
    else:
        label = node.__class__.__name__
        # Try to add 'id' or 'name' or 'value' if present
        for attr in ("id", "name", "value"):
            if hasattr(node, attr):
                label += f"\n{attr}: {getattr(node, attr)}"
                break

    dot.node(node_id, label=label)
    if parent_id:
        dot.edge(parent_id, node_id)

    # Recursively add children
    # Try .fields (AST), or __dict__ for custom nodes
    children: MutableSequence[Any] = []

    if hasattr(node, "__dict__"):
        for _, value in node.__dict__.items():
            if isinstance(value, list):
                children.extend([v for v in value if hasattr(v, "__class__")])
            elif hasattr(value, "__class__"):
                children.append(value)

    for child in children:
        ast_to_dot(child, dot=dot, parent_id=node_id, label_fn=label_fn)

    return dot
