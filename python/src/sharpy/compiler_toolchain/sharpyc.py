#!/usr/bin/env python3
import argparse
import sys
from io import StringIO
from pathlib import Path
from tempfile import TemporaryDirectory
from typing import Sequence

from sharpy.compiler_toolchain.antlr import ParseTreeNode
from sharpy.compiler_toolchain.antlr.ast_builder import AntlrASTBuilder
from sharpy.compiler_toolchain.antlr.parser import AntlrParser
from sharpy.compiler_toolchain.ast import Node as ASTNode
from sharpy.compiler_toolchain.logging import logger


def main() -> None:
    args: argparse.Namespace = parse_args()
    inputs: Sequence[Path] = args.input
    output: Path = args.output
    is_library: bool = output.suffix[1:] == "dll"

    with TemporaryDirectory() as temp_dir:
        temp_dir_path: Path = Path(temp_dir)

        for input_path in inputs:
            if not input_path.exists():
                logger.error(f"Input file {input_path} does not exist.")
                sys.exit(1)

            if not input_path.is_file():
                logger.error(f"Input path {input_path} is not a file.")
                sys.exit(1)

            output_path: Path = temp_dir_path / input_path.with_suffix(".cs").name
            transpile(input_path=input_path, output_path=output_path)

    # csc -target:library -out:output.dll abc.cs xyz.cs
    # omit -target:library if executable is desired, don't add suffix
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="The Sharpy compiler.")
    parser.add_argument("-i", "--input", type=Path, action="append", required=True)
    parser.add_argument("-o", "--output", type=Path, required=True)
    parser.add_argument("-v", "--verbose", action="store_true", help="Enable verbose output.")
    parser.add_argument("--version", action="store_true", help="Show version information.")

    return parser.parse_args()


def transpile(input_path: Path, output_path: Path) -> None:
    # Parse with ANTLR
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

    # Build AST
    builder = AntlrASTBuilder()
    ast: ASTNode = builder._generate_ast(parse_tree)

    # TODO: Type analysis

    # Transpilation
    pass


if __name__ == "__main__":
    main()
