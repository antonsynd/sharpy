#!/usr/bin/env python3
from sharpy.compiler_toolchain.python import code_generator
import argparse
from pathlib import Path


def main() -> None:
    args = parse_args()
    input: Path = args.input

    translator = code_generator.PythonToCSharp()

    csharp_code: str = translator.generate_csharp(input.read_text())

    print(csharp_code)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="")
    parser.add_argument("-i", "--input", type=Path, required=True)

    return parser.parse_args()


if __name__ == "__main__":
    main()
