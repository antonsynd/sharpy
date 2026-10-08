# The Sharpy Formatter

`sharpyc format` and the LSP's *Format Document* rewrite a file into one canonical layout. Both use the same formatter, so they produce the same text; *Format Selection* applies part of that text (see [In the editor](#in-the-editor-lsp)). This page states what formatting guarantees, and what it does when it cannot keep a guarantee.

## Usage

```bash
sharpyc format file.spy             # rewrite file.spy in place
sharpyc format src/                 # every .spy file under src/
sharpyc format file.spy -o out.spy  # write the result to out.spy; file.spy is untouched
sharpyc format src/ --check         # CI mode: exit 1 if any file would change; nothing is written
sharpyc format file.spy --diff      # print a unified diff; nothing is written
```

Without `-o`, `--check` or `--diff`, the file is rewritten **in place**. The output always uses `\n` line endings: a file with `\r\n` line endings comes back with `\n`. The program is unaffected, because a line break inside a string literal is `\n` in either spelling.

### Indentation is always 4 spaces

Sharpy indentation is exactly four spaces per level, and tabs are not allowed ([Indentation](../language_specification/indentation.md)). So the formatter always writes four spaces.

- `--indent 4` is accepted and changes nothing. `--indent N` with any other `N`, and `--tabs`, are usage errors: `sharpyc` exits with code 2 and touches no file.

  ```text
  $ sharpyc format indent.spy --indent 2
  Error: --indent 2 is not supported: Sharpy indentation is exactly 4 spaces per level (docs/language_specification/indentation.md).
  ```

- The LSP ignores the editor's `tabSize` and `insertSpaces` settings for Sharpy documents.

## What formatting guarantees

Formatting changes **layout only**. For every file it formats:

1. **String values are untouched.** Every string literal, f-string and t-string denotes exactly the same text after formatting: its trailing spaces, its indentation and the blank lines inside a triple-quoted string are all kept. The formatter may re-spell a string's quotes and escapes in a canonical form without changing what it denotes: `'''x'''` is written `"x"`, and `'\x41'` is written `"A"`. Replacement fields (`{x!r:>4}`) are written back exactly as written.
2. **Every comment survives, in its place.** The output's comments are the input's comments, by text and in order. Each one stays where it was written: at the end of its line, before the statement it preceded, on a decorator or a clause header (`elif`, `else`, `except`, `finally`, `case`), or inside the block it closes. An end-of-block comment stays inside that block, at the block's indentation, even when several blocks close at once. A comment-only line carries no indentation meaning, so it is written at the indentation of the code it belongs to. Trailing spaces after a comment are removed; they are not part of the comment.
3. **Every backtick escape survives.** A name written with backticks (`` `class` ``, `` `count` ``) is written back with them at every position: parameters, keyword arguments, imports and their aliases, `except … as`, patterns, member access, decorators and type names. This matters for meaning, not just spelling: `` obj.`count` `` and `obj.count` can bind different .NET members.
4. **The program is the same.** The output parses to the same syntax tree as the input, and compiles to the same C# apart from the `#line` directives, which record line numbers.
5. **Formatting is idempotent.** Formatting a formatted file changes nothing.

For example, this file

```python
def scale(`class`: int) -> int:
    return `class` * 2
def main():
    x   =   scale(`class`=3)  # escaped keyword argument
    if x > 5:
        print("big")
        # still inside the if body
    else:  # the else header
        print("small")
```

formats (`sharpyc format guarantees.spy -o formatted.spy`) to

```python
def scale(`class`: int) -> int:
    return `class` * 2


def main():
    x = scale(`class`=3)  # escaped keyword argument
    if x > 5:
        print("big")
        # still inside the if body
    else:  # the else header
        print("small")
```

and both files print `big`.

These guarantees are checked over the whole test corpus on every build: each fixture as written, each fixture with a comment added at every position, and each fixture with every name backtick-escaped (`FormatterMeaningPreservationSweepTests`, `FormatterEmitInvarianceSweepTests`).

## Constructs with comments inside are written as you wrote them

A statement, a clause header (`elif`, `except`, `case`) or a decorator that has a comment **inside** it — inside its brackets, or inside an f-string replacement field — is written **verbatim**: exactly as it appears in the source, re-indented as a unit. Its continuation lines move with its first line. A continuation line that starts inside a multi-line string or f-string is never moved, because its leading spaces belong to the string.

```python
def main():
        xs = [1,  # one
              2]
        print(xs)
```

formats (`sharpyc format verbatim.spy -o verbatim_out.spy`) to

```python
def main():
    xs = [1,  # one
          2]
    print(xs)
```

Both print `[1, 2]`.

Verbatim also means the layout inside the construct is kept: `[1,2]` stays `[1,2]` and is not rewritten to `[1, 2]`. The formatter has no comment-aware layout for bracketed code, so it does not re-lay out anything that carries a comment inside. A construct without inner comments is formatted normally, and its trailing comment stays on its line.

## Canonical spellings

Some constructs have one written form, so formatting may change their spelling without changing the program. For example, an `except` clause that catches several exception types is always written with parentheses: `except KeyError, IndexError:` becomes `except (KeyError, IndexError):`.

## When the formatter declines: SPY0912

Before it writes anything, the formatter re-reads its own output and checks guarantees 2–4. If the output would drop or move a comment, drop a backtick escape, fail to parse, or change the program's structure, the formatter **declines**:

- the file is left **exactly as it was**, and
- error **SPY0912** names the first problem.

```text
$ sharpyc format declined.spy
Error: declined.spy: SPY0912: formatting declined: the output would not re-parse (first error at formatted line 9: SPY0104 Expected RightBracket, got Colon); the file was left unchanged
```

`sharpyc format` exits with code 2, and the file is byte-for-byte unchanged. A declined file has not been damaged.

SPY0912 always means a formatter bug: your file is valid and its meaning is safe, but the formatter could not lay it out without changing it. Please report it (see below). Known cases: a string containing a `\r` escape, a generic constraint intersection `[T: A & B]`, and a `with … as (a, *rest):` target are declined, because the formatter cannot yet write them back unchanged (#2169).

## In the editor (LSP)

- **Format Document** uses the same formatter as `sharpyc format` and produces the same text.
- If the formatter declines (SPY0912), the editor receives **no edits** and the document is unchanged, as with the CLI.
- **Format Selection** formats the whole file and applies only the changes the selection touches. A change the selection touches is applied whole, so it can reach past the selection in either direction, to both ends of its run of changed lines, because part of a change can be wrong on its own. The text it would produce is checked the same way as the formatter's own output: if it would not parse, or would change the program, Format Selection applies **nothing**. The file keeps its line endings: a line outside a change keeps its own, the last line of a change keeps the ending it had, and every other line break a change writes is the file's first one — so a file with one kind of line ending keeps it, and a file that mixes them may see a changed line's ending become the file's first kind.
- **Format on type** aligns the first line of a statement to its block's indentation, and nothing else: never a line that continues a statement (inside brackets, or after `\`), a comment line, or a line inside a string. It changes how far the line is indented, never which block it is in, so it leaves an unexpected indent alone and does not dedent an `else:` typed at its body's indentation. It applies the new indentation only if the file stays the same program; while the file does not parse, the indentation-only check below decides instead.
- If the document **does not parse**, for example in the middle of typing, Format Document and Format Selection fall back to an indentation-only pass. It re-indents lines to four-space levels and changes nothing else. It applies its result only if every line keeps its text, every line inside a string is unchanged, every statement stays in the block it was in, and no new indentation error appears; otherwise it applies nothing. A line that belongs to no block, because it is indented with a tab or to a width no enclosing block uses, is the mistake the pass repairs: it moves to the level the editor guesses for it, which may not be the block you meant. This fallback is used only for documents that fail to parse, never for a document the formatter declined.
- While a triple-quoted string is unfinished, or cannot be read as a string, the editor cannot tell which lines are code. A string cannot be read as one when a stray `"""` above it has paired with its opening quotes, when its opening `"""` sits on a line the lexer could not read at all (a line indented with a tab, or to a width no enclosing block uses), or when an error earlier on its opening line (an unexpected character, an invalid escape, the f-string's own quote inside a format spec) made the lexer skip the rest of that line. Format on type and the indentation-only pass change nothing until the string can be read again.
- The editor reads the whole file, however many errors it has. The compiler stops at 25 lexer errors, but the indentation-only pass and format on type still read every later line and every string in it.
- A few re-paired strings look exactly like a string that is fine, so the editor cannot tell them apart: a stray `"""` whose partner sits inside a comment, two stray delimiters, or an opening `"""` inside an unfinished short string or backtick-delimited name. There the indentation-only pass and format on type may re-indent lines of the string. Fix the stray quote first, or check the edit.

Selecting the body of `main` (lines 4–5) in

<!-- editor-example: selection 4-5 -->
```python
def helper() -> int:
    return 2
def main():
    x   =   helper()
    print(x)
```

applies only the change inside the selection:

```python
def helper() -> int:
    return 2
def main():
    x = helper()
    print(x)
```

Format Document would also add two blank lines before `def main():`. They are outside the selection, so Format Selection does not add them.

In this file, formatting re-indents both statements of `main`. Selecting line 2 alone applies **nothing**: re-indenting `s = """` without `print(s)` would leave a file that does not parse, and the change `print(s)` needs lies past the string.

<!-- editor-example: selection 2-2; unchanged -->
```python
def main():
        s = """
abc
"""
        print(s)
```

Format on type on line 4 of

<!-- editor-example: on-type 4 -->
```python
def main():
    x = 1
    if x > 0:
            print(x)
```

re-indents that line:

```python
def main():
    x = 1
    if x > 0:
        print(x)
```

In this file, indented 8 spaces per level, format on type on line 4 applies **nothing**: re-indenting `print(2)` alone would move it out of the `if` block, and the program would print `2`.

<!-- editor-example: on-type 4; unchanged -->
```python
def main():
        if False:
                print(1)
                print(2)
```

Here a docstring has been opened in `f` but not closed yet. Its `"""` pairs with the one that opens `s` in `g`, so `key: value` is read as code. Format Document, Format Selection of line 6 and format on type on line 6 all apply **nothing**: the file stays exactly as written until the docstring is closed.

<!-- editor-example: document, selection 6-6, on-type 6; unchanged -->
```python
def f() -> int:
    """
    return 1
def g() -> str:
    s = """
        key: value
    """
    return s
```

Here a stray `"""` at the top of the file pairs with the one that opens `s`, so `key: value` is read as code, and the string's closing `"""` pairs with the quotes inside `'"""'`. Format Document, Format Selection of line 4 and format on type on line 4 all apply **nothing**:

<!-- editor-example: document, selection 4-4, on-type 4; unchanged -->
```python
"""
def main():
    s = """
      key: value
    """
    t = '"""'
    print(s, t)
```

Here the line that opens the string is indented two spaces, a width no block in the file uses, so the lexer skips the whole line and never sees its `"""`. Format Document, Format Selection of line 3 and format on type on line 3 all apply **nothing**, so `key: value` keeps its eight spaces:

<!-- editor-example: document, selection 3-3, on-type 3; unchanged -->
```python
def main():
  s = """
        key: value
  """
  print(s)
```

A file with 25 or more lexer errors is read to its end: past the compiler's limit, the editor re-indents lines and keeps every string it can read exactly as it does in a file with fewer errors. The compiler itself still stops at 25 errors; `sharpyc emit diagnostics --max-errors N` lists the errors past the first 25.

## Reporting a problem

If formatting declines a file (SPY0912), changes what a program prints, or moves or drops a comment or an escape, please open an issue at [github.com/antonsynd/sharpy](https://github.com/antonsynd/sharpy/issues) with:

1. the smallest input that shows it (the file, or a few lines from it),
2. the `sharpyc format` output (the SPY0912 message, or the `-o` result),
3. `sharpyc --version`.

Open formatter issues are tracked on [#2169](https://github.com/antonsynd/sharpy/issues/2169) (constructs the formatter declines). The guarantees on this page were established by [#2062](https://github.com/antonsynd/sharpy/issues/2062).
