#!/usr/bin/env python3
import argparse
import logging
import sys
from io import StringIO
from pathlib import Path

from sharpy.compiler_toolchain.antlr import ParseTreeNode
from sharpy.compiler_toolchain.antlr.ast_builder import AntlrASTBuilder
from sharpy.compiler_toolchain.antlr.parser import AntlrParser
from sharpy.compiler_toolchain.antlr.rendering import render_ast_as_png, render_parse_tree_as_png
from sharpy.compiler_toolchain.ast import Node as ASTNode
from sharpy.compiler_toolchain.logging import formatter, logger


def main():
    parser = argparse.ArgumentParser(
        description="Parse a Sharpy file and render its parse tree or AST as a PNG."
    )
    parser.add_argument("--input", type=Path, required=True, help="Input Sharpy source file")
    parser.add_argument(
        "--output-dir", type=Path, default=Path("."), help="Output directory for PNG files"
    )
    parser.add_argument(
        "--ast", action="store_true", help="Render AST (default: render parse tree)"
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
        "--log",
        type=Path,
        default=None,
        help="Path to a log file to additionally write logs to. If not specified, logs to stdout and stderr.",
    )

    args: argparse.Namespace = parser.parse_args()

    input_path: Path = args.input
    output_dir: Path = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)

    debug: bool = args.debug
    log_path: Path | None = args.log

    if log_path:
        # Set up file logging
        file_handler = logging.FileHandler(log_path, mode="w", encoding="utf-8")
        file_handler.setFormatter(formatter)
        file_handler.setLevel(logging.DEBUG if debug else logging.INFO)
        logger.addHandler(file_handler)

    if debug:
        logger.setLevel(logging.DEBUG)

    basename: str = args.basename or input_path.stem

    # ANTLR parse
    buffer = StringIO(input_path.read_text(encoding="utf-8"))
    parser = AntlrParser()
    raw_parser = parser._create_parser(input=buffer)
    parse_tree: ParseTreeNode | None = raw_parser.file_input()

    if not parse_tree:
        logger.error("Failed to parse the input file.")
        sys.exit(1)

    parser._postprocess_parse_tree(parse_tree=parse_tree)

    if not parse_tree:
        logger.error("Failed to postprocess the parse tree file.")
        sys.exit(1)

    if args.ast:
        # AST build
        builder = AntlrASTBuilder()
        ast: ASTNode = builder._generate_ast(parse_tree)
        out_png: Path = output_dir / f"{basename}_ast.png"
        render_ast_as_png(ast, out_png)

        print(f"AST PNG written to {out_png}")
    else:
        out_png: Path = output_dir / f"{basename}_parse_tree.png"
        render_parse_tree_as_png(parse_tree, raw_parser, out_png)

        print(f"Parse tree PNG written to {out_png}")


if __name__ == "__main__":
    main()
