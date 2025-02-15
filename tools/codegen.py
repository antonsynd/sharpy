#!/usr/bin/env python3
import argparse
import logging
import sys

from pathlib import Path
from typing import Optional

from sharpy.compiler_toolchain.python import code_generator
from sharpy.compiler_toolchain.python.code_formatter import CodeFormatter, CSharpierFormatter


def main() -> None:
    args: argparse.Namespace = parse_args()

    input: Path = args.input
    output: Optional[Path] = args.output
    format: bool = args.format
    csharpier_path: Optional[Path] = args.csharpier_path
    dotnet_path: Optional[Path] = None
    emit_line_metadata: bool = args.emit_line_metadata
    debug: bool = args.debug

    logger: logging.Logger = create_logger(debug=debug)

    if csharpier_path:
        csharpier_path = str(csharpier_path)
    else:
        csharpier_path = "dotnet-csharpier"

    formatter: Optional[CodeFormatter] = None

    if format:
        formatter = CSharpierFormatter(logger=logger, invocation=csharpier_path)

    translator = code_generator.PythonToCSharp(
        logger=logger, emit_line_metadata=emit_line_metadata, formatter=formatter
    )

    csharp_code: str = translator.generate_csharp(buffer=input.read_text(), file_name=input.name)

    if output:
        logger.info(f"Writing code to {output}...")
        output.write_text(csharp_code)
        logger.info(f"Done!")
    else:
        print(csharp_code)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Tool for testing code generation for Sharpy (Python) to C#"
    )
    parser.add_argument("-i", "--input", type=Path, required=True, help="The input file.")
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        default=None,
        help="The output file to generated code to. If omitted, then outputs to stdout.",
    )
    parser.add_argument(
        "-f",
        "--format",
        action="store_true",
        help="Formats the generated code using csharpier.",
    )
    parser.add_argument(
        "--csharpier-path",
        type=Path,
        default=None,
        help="Custom path to csharpier. If not provided, defaults to dotnet-csharpier on PATH.",
    )
    parser.add_argument(
        "-e",
        "--emit-line-metadata",
        action="store_true",
        help="Emits #line directives serving as debugging source mapping.",
    )
    parser.add_argument("--debug", action="store_true", help="Emit debug information.")

    return parser.parse_args()


def create_logger(debug: bool) -> logging.Logger:
    logger = logging.Logger("sharpyc")
    formatter = logging.Formatter("[%(name)s] [%(levelname)s] %(message)s")
    stdout_handler = logging.StreamHandler(stream=sys.stdout)
    stdout_handler.setFormatter(formatter)
    stdout_handler.setLevel(logging.DEBUG)

    stderr_handler = logging.StreamHandler(stream=sys.stderr)
    stderr_handler.setFormatter(formatter)
    stderr_handler.setLevel(logging.WARNING)

    logger.addHandler(stdout_handler)
    logger.addHandler(stderr_handler)

    if debug:
        logger.setLevel(logging.DEBUG)
    else:
        logger.setLevel(logging.INFO)

    return logger


if __name__ == "__main__":
    main()
