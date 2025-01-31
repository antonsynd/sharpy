#!/usr/bin/env python3
from sharpy.compiler_toolchain.python import code_generator
import argparse
from pathlib import Path


def main() -> None:
    args: argparse.Namespace = parse_args()
    input: Path = args.input
    emit_line_metadata: bool = args.emit_line_metadata

    translator = code_generator.PythonToCSharp(emit_line_metadata=emit_line_metadata)

    csharp_code: str = translator.generate_csharp(buffer=input.read_text(), file_name=input.name)

    print(csharp_code)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="")
    parser.add_argument("-i", "--input", type=Path, required=True)
    parser.add_argument(
        "-e",
        "--emit-line-metadata",
        action="store_true",
        help="If present, emits #line directives serving as debugging source mapping.",
    )

    return parser.parse_args()


if __name__ == "__main__":
    main()
