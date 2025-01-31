#!/usr/bin/env python3
from sharpy.compiler_toolchain.python import code_generator
import argparse
from pathlib import Path
from typing import Optional


def main() -> None:
    args: argparse.Namespace = parse_args()
    input: Path = args.input
    output: Optional[Path] = args.output
    format: bool = args.format
    csharpier_path: Optional[Path] = args.csharpier_path
    emit_line_metadata: bool = args.emit_line_metadata

    if csharpier_path:
        csharpier_path = str(csharpier_path)
    else:
        csharpier_path = "dotnet-csharpier"

    translator = code_generator.PythonToCSharp(
        emit_line_metadata=emit_line_metadata, format=format, csharpier_path=csharpier_path
    )

    csharp_code: str = translator.generate_csharp(buffer=input.read_text(), file_name=input.name)

    if output:
        output.write_text(csharp_code)
    else:
        print(csharp_code)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="")
    parser.add_argument("-i", "--input", type=Path, required=True)
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
        help="Formats the generated code using csharpier",
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

    return parser.parse_args()


if __name__ == "__main__":
    main()
