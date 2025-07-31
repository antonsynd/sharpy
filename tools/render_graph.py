#!/usr/bin/env python3
import argparse
import sys
from io import StringIO
from pathlib import Path

from sharpy.compiler_toolchain.antlr import ParseTreeNode
from sharpy.compiler_toolchain.antlr.ast_builder import AntlrASTBuilder
from sharpy.compiler_toolchain.antlr.parser import AntlrParser
from sharpy.compiler_toolchain.antlr.rendering import (
    _node_list,
    render_ast_as_png,
    render_parse_tree_as_png,
    render_parse_tree_as_xml,
)
from sharpy.compiler_toolchain.ast import Node as ASTNode
from sharpy.compiler_toolchain.logging import logger


def main():
    parser = argparse.ArgumentParser(
        description="Parse a Sharpy file and render its parse tree or AST as a PNG."
    )
    parser.add_argument("--input", type=Path, required=True, help="Input Sharpy source file")
    parser.add_argument(
        "--output-dir", type=Path, default=Path("."), help="Output directory for PNG files"
    )
    parser.add_argument(
        "--only-parse", action="store_true", help="Render only parse tree (default: render both)"
    )
    parser.add_argument(
        "--only-ast", action="store_true", help="Render only AST (default: render both)"
    )
    parser.add_argument(
        "--basename",
        type=str,
        default=None,
        help="Base name for output PNG file (default: input file stem)",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug output (default: False)",
    )
    parser.add_argument(
        "--log-path",
        type=Path,
        default=None,
        help="Path to a log file to additionally write logs to. If not specified, logs to stdout and stderr.",
    )
    parser.add_argument(
        "--keep-temp",
        action="store_true",
        help="Keep temporary files used for rendering (default: False)",
    )
    parser.add_argument(
        "--emit-xml",
        action="store_true",
        help="Emit XML representation of the parse tree (default: False)",
    )

    args: argparse.Namespace = parser.parse_args()

    input_path: Path = args.input
    output_dir: Path = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    keep_temp: bool = args.keep_temp
    only_parse: bool = args.only_parse
    only_ast: bool = args.only_ast
    emit_xml: bool = args.emit_xml

    if only_parse and only_ast:
        logger.error("Cannot specify both --only-parse and --only-ast. Choose one.")
        sys.exit(1)

    debug: bool = args.debug

    if debug:
        logger.enable_debug()

    log_path: Path | None = args.log_path

    if log_path:
        logger.add_file(file_path=log_path)

    basename: str = args.basename or input_path.stem

    # ANTLR parse
    buffer = StringIO(input_path.read_text(encoding="utf-8"))
    parser = AntlrParser()
    raw_parser = parser._create_parser(input=buffer)
    parse_tree: ParseTreeNode | None = raw_parser.module()

    if not parse_tree:
        logger.error("Failed to parse the input file.")
        sys.exit(1)

    parser._postprocess_parse_tree(parse_tree=parse_tree)

    if not parse_tree:
        logger.error("Failed to postprocess the parse tree file.")
        sys.exit(1)

    if not only_parse:
        # AST build
        builder = AntlrASTBuilder()
        ast: ASTNode = builder._generate_ast(parse_tree)
        out_png: Path = output_dir / f"{basename}_ast.png"
        temp_file: Path | None = render_ast_as_png(ast, out_png, keep_temp=keep_temp)

        for node in _node_list:
            logger.debug(f"Node: {node}")

        print(f"AST PNG written to {out_png}")

        if temp_file:
            print(f"Temporary file kept at {temp_file}")

    if not only_ast:
        out_png: Path = output_dir / f"{basename}_parse_tree.png"
        temp_file: Path | None = render_parse_tree_as_png(
            parse_tree, raw_parser, out_png, keep_temp=keep_temp
        )

        print(f"Parse tree PNG written to {out_png}")

        if temp_file:
            print(f"Temporary file kept at {temp_file}")

        if emit_xml:
            xml_output_path: Path = output_dir / f"{basename}_parse_tree.xml"
            render_parse_tree_as_xml(parse_tree, raw_parser, xml_output_path)
            print(f"Parse tree XML written to {xml_output_path}")


if __name__ == "__main__":
    main()
