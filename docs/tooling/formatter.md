# The Sharpy Formatter

`sharpyc format` and the LSP's *Format Document* / *Format Selection* rewrite a file into one canonical layout. Both use the same formatter, so they produce the same text. This page states what formatting guarantees, and what it does when it cannot keep a guarantee.

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
Error: declined.spy: SPY0912: formatting declined: the output would drop the backtick escape on '_' at line 4; the file was left unchanged
```

`sharpyc format` exits with code 2, and the file is byte-for-byte unchanged. A declined file has not been damaged.

SPY0912 always means a formatter bug: your file is valid and its meaning is safe, but the formatter could not lay it out without changing it. Please report it (see below). Known cases:

- a backtick-escaped contextual keyword such as `` case `_`: `` is declined, because the parser reads the escaped spelling as the keyword itself (#2166);
- a string containing a `\r` escape, a generic constraint intersection `[T: A & B]`, and a `with … as (a, *rest):` target are declined, because the formatter cannot yet write them back unchanged (#2169).

## In the editor (LSP)

- **Format Document** uses the same formatter as `sharpyc format` and produces the same text.
- **Format Selection** and **format on type** do not yet carry these guarantees (#2168). Format Selection runs the same formatter, but it maps the result back onto the selection line by line. When formatting adds or removes a line (for example the two blank lines between top-level definitions), it can duplicate or drop lines. Format on type can re-indent a line inside a multi-line string. Until #2168 is fixed, use Format Document.
- If the formatter declines (SPY0912), the editor receives **no edits** and the document is unchanged, as with the CLI.
- If the document **does not parse**, for example in the middle of typing, the server falls back to an indentation-only pass. It re-indents lines to four-space levels and leaves everything else alone, including the inside of strings. This fallback is used only for documents that fail to parse, never for a document the formatter declined.

## Reporting a problem

If formatting declines a file (SPY0912), changes what a program prints, or moves or drops a comment or an escape, please open an issue at [github.com/antonsynd/sharpy](https://github.com/antonsynd/sharpy/issues) with:

1. the smallest input that shows it (the file, or a few lines from it),
2. the `sharpyc format` output (the SPY0912 message, or the `-o` result),
3. `sharpyc --version`.

Open formatter issues are tracked on [#2169](https://github.com/antonsynd/sharpy/issues/2169) (constructs the formatter declines) and [#2168](https://github.com/antonsynd/sharpy/issues/2168) (editor formatting routes). The guarantees on this page were established by [#2062](https://github.com/antonsynd/sharpy/issues/2062).
