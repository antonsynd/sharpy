"""Split a Bash tool command into the simple commands bash would run, for the PreToolUse hooks.

Used by guard-shared-tree.sh and enforce-dotnet-serialized.sh (imported from this directory).
``walk(src, cwd, visit)`` calls ``visit(argv, cwd)`` for every simple command, in order, with:

- separators split: ``;`` ``&&`` ``||`` ``|`` ``|&`` ``&`` and newlines;
- ``( ... )`` subshells and ``$( ... )`` / backtick substitutions walked as nested commands, their
  ``cd`` not leaking out; ``bash|sh|zsh|dash -c '<script>'`` and ``eval <words>`` walked as scripts;
- quotes removed, ``\\`` escapes and line continuations applied, ``#`` comments and here-document
  bodies dropped, redirections (``>f``, ``2>&1``, ``<<EOF``, ``<<<w``) removed from argv;
- leading ``VAR=value`` assignments and the prefix commands ``env sudo nohup time command exec
  nice timeout stdbuf xargs`` (with their options) stripped, so argv[0] is the program run;
- ``cd`` / ``pushd`` tracked: ``cwd`` is the directory the command runs in, as far as the text says.

It does not evaluate the shell: variables expand from the hook's own environment (an unset one to
""), a ``$(...)`` inside a word stays literal text, globs stay unexpanded, functions and aliases are
unknown, and control keywords (``if``/``then``/``do``/``{``...) are skipped as argv[0], not parsed.
An unterminated quote or substitution raises ParseError: bash refuses that command too.
"""

import os
import re

PREFIX_OPTS_WITH_ARG = {
    "env": {"-u", "--unset", "-C", "--chdir", "-S", "--split-string"},
    "sudo": {"-u", "-g", "-C", "-D", "-h", "-p", "-r", "-t", "-U", "-T"},
    "nice": {"-n", "--adjustment"},
    "timeout": {"-s", "--signal", "-k", "--kill-after"},
    "stdbuf": {"-i", "-o", "-e", "--input", "--output", "--error"},
    "xargs": {"-I", "-n", "-L", "-P", "-s", "-d", "-E", "-a", "--arg-file", "--delimiter",
              "--max-args", "--max-lines", "--max-procs", "--max-chars", "--eof", "--replace"},
    "exec": {"-a"},
    "command": set(),
    "nohup": set(),
    "time": set(),
}
SHELLS = {"bash", "sh", "zsh", "dash", "ksh"}
KEYWORDS = {"if", "then", "else", "elif", "fi", "do", "done", "while", "until", "{", "}", "!",
            "time", "coproc"}
ASSIGN = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*(\[[^]]*\])?\+?=")


class ParseError(Exception):
    pass


def _match_close(src, i, open_ch, close_ch):
    """Index just past the ``close_ch`` matching the ``open_ch`` already consumed before ``i``."""
    depth, n = 1, len(src)
    while i < n:
        c = src[i]
        if c == "\\":
            i += 2
            continue
        if c == "'":
            j = src.find("'", i + 1)
            if j < 0:
                raise ParseError("unterminated '")
            i = j + 1
            continue
        if c == '"':
            i = _skip_dquote(src, i + 1)
            continue
        if c == "`":
            i = _skip_backtick(src, i + 1)
            continue
        if c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise ParseError("unterminated " + open_ch)


def _skip_dquote(src, i):
    n = len(src)
    while i < n:
        c = src[i]
        if c == "\\":
            i += 2
            continue
        if c == '"':
            return i + 1
        if src.startswith("$(", i):
            i = _match_close(src, i + 2, "(", ")")
            continue
        if c == "`":
            i = _skip_backtick(src, i + 1)
            continue
        i += 1
    raise ParseError('unterminated "')


def _skip_backtick(src, i):
    n = len(src)
    while i < n:
        if src[i] == "\\":
            i += 2
            continue
        if src[i] == "`":
            return i + 1
        i += 1
    raise ParseError("unterminated `")


def tokenize(src):
    """Tokens: ('w', text, [substitution sources]), ('op', text) or ('redir', text)."""
    toks = []
    word, subs = None, []
    heredocs = []  # (delimiter, strip_tabs) awaiting the next newline
    i, n = 0, len(src)

    def flush():
        nonlocal word, subs
        if word is not None:
            toks.append(("w", "".join(word), subs))
        word, subs = None, []

    def add(text):
        nonlocal word
        if word is None:
            word = []
        word.append(text)

    while i < n:
        c = src[i]
        if c == "\n":
            flush()
            toks.append(("op", ";"))
            i += 1
            for delim, strip_tabs in heredocs:
                while i < n:
                    j = src.find("\n", i)
                    line = src[i:] if j < 0 else src[i:j]
                    i = n if j < 0 else j + 1
                    if (line.lstrip("\t") if strip_tabs else line) == delim:
                        break
            heredocs = []
            continue
        if c in " \t\r":
            flush()
            i += 1
            continue
        if c == "#" and word is None:
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if c == "\\":
            if i + 1 < n and src[i + 1] == "\n":
                i += 2
                continue
            add(src[i + 1] if i + 1 < n else "")
            i += 2
            continue
        if c == "'":
            j = src.find("'", i + 1)
            if j < 0:
                raise ParseError("unterminated '")
            add(src[i + 1:j])
            i = j + 1
            continue
        if src.startswith("$'", i):
            j = i + 2
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            if j >= n:
                raise ParseError("unterminated $'")
            add(src[i + 2:j])
            i = j + 1
            continue
        if c == '"':
            j, buf = i + 1, []
            while True:
                if j >= n:
                    raise ParseError('unterminated "')
                d = src[j]
                if d == '"':
                    break
                if d == "\\" and j + 1 < n and src[j + 1] in '$`"\\\n':
                    if src[j + 1] != "\n":
                        buf.append(src[j + 1])
                    j += 2
                    continue
                if src.startswith("$(", j):
                    k = _match_close(src, j + 2, "(", ")")
                    subs.append(src[j + 2:k - 1])
                    buf.append(src[j:k])
                    j = k
                    continue
                if d == "`":
                    k = _skip_backtick(src, j + 1)
                    subs.append(src[j + 1:k - 1])
                    buf.append(src[j:k])
                    j = k
                    continue
                buf.append(d)
                j += 1
            add("".join(buf))
            i = j + 1
            continue
        if src.startswith("$((", i):
            k = _match_close(src, i + 3, "(", ")")  # arithmetic: no commands inside
            if k < n and src[k] == ")":
                k += 1
            add(src[i:k])
            i = k
            continue
        if src.startswith("$(", i):
            k = _match_close(src, i + 2, "(", ")")
            subs.append(src[i + 2:k - 1])
            add(src[i:k])
            i = k
            continue
        if src.startswith("${", i):
            k = _match_close(src, i + 2, "{", "}")
            add(src[i:k])
            i = k
            continue
        if c == "`":
            k = _skip_backtick(src, i + 1)
            subs.append(src[i + 1:k - 1])
            add(src[i:k])
            i = k
            continue
        if c in "<>" or (c == "&" and i + 1 < n and src[i + 1] == ">"):
            if word is not None and "".join(word).isdigit():
                word, subs = None, []  # an fd number: 2>&1
            flush()
            m = re.compile(r"&>>|&>|<<<|<<-|<<|<>|>>|>&|<&|>\||<|>").match(src, i)
            op = m.group(0)
            i = m.end()
            toks.append(("redir", op))
            if op in ("<<", "<<-"):
                m2 = re.compile(r"[ \t]*(['\"]?)([^\s'\";&|<>()]+)\1").match(src, i)
                if m2:
                    heredocs.append((m2.group(2), op == "<<-"))
                    i = m2.end()
                    toks.append(("w", m2.group(2), []))
            continue
        if c in ";&|()":
            flush()
            two = src[i:i + 2]
            if two in ("&&", "||", ";;", "|&"):
                toks.append(("op", two))
                i += 2
            else:
                toks.append(("op", c))
                i += 1
            continue
        add(c)
        i += 1
    flush()
    return toks


def expand(word, env=None):
    """Tilde and $VAR / ${VAR} expansion against ``env`` (default: this process's), unset -> ''."""
    env = os.environ if env is None else env
    if word == "~" or word.startswith("~/"):
        word = env.get("HOME", "~") + word[1:]

    def sub(m):
        name = m.group(1) or m.group(2)
        return env.get(name, "")

    return re.sub(r"\$\{([A-Za-z_][A-Za-z0-9_]*)\}|\$([A-Za-z_][A-Za-z0-9_]*)", sub, word)


def resolve(path, cwd):
    return os.path.normpath(os.path.join(cwd, expand(path)))


def unwrap(argv):
    """Strip assignments and prefix commands. Returns (argv, nested_scripts)."""
    argv = list(argv)
    while argv:
        while argv and ASSIGN.match(argv[0]):
            argv.pop(0)
        if not argv:
            break
        if argv[0] in KEYWORDS:
            argv.pop(0)
            continue
        name = os.path.basename(argv[0])
        if name in PREFIX_OPTS_WITH_ARG:
            takes = PREFIX_OPTS_WITH_ARG[name]
            argv.pop(0)
            while argv and argv[0].startswith("-") and argv[0] != "-":
                opt = argv.pop(0)
                if opt == "--":
                    break
                if opt in takes and argv:
                    argv.pop(0)
            if name == "env":
                while argv and ASSIGN.match(argv[0]):
                    argv.pop(0)
            if name == "timeout" and argv:
                argv.pop(0)  # the duration
            continue
        if name in SHELLS:
            for k, a in enumerate(argv[1:], 1):
                if not a.startswith("-"):
                    break
                if not a.startswith("--") and "c" in a[1:]:
                    rest = [x for x in argv[k + 1:]]
                    return [], rest[:1]
            return argv, []
        if name == "eval":
            return [], [" ".join(argv[1:])]
        break
    return argv, []


def _split(toks):
    """Group tokens into (kind, payload): ('cmd', [words]), ('open',), ('close',)."""
    out, cur, skip_next = [], [], False
    for t in toks:
        if t[0] == "redir":
            skip_next = True
            continue
        if t[0] == "w":
            if skip_next:
                skip_next = False
                out.append(("subs", t[2]))
                continue
            cur.append(t)
            continue
        skip_next = False
        if cur:
            out.append(("cmd", cur))
            cur = []
        if t[1] == "(":
            out.append(("open",))
        elif t[1] == ")":
            out.append(("close",))
    if cur:
        out.append(("cmd", cur))
    return out


def walk(src, cwd, visit, _depth=0):
    """Call visit(argv, cwd) for each simple command in ``src``. Returns the final cwd."""
    if _depth > 8:
        return cwd
    stack = []
    for item in _split(tokenize(src)):
        if item[0] == "open":
            stack.append(cwd)
            continue
        if item[0] == "close":
            cwd = stack.pop() if stack else cwd
            continue
        if item[0] == "subs":
            for s in item[1]:
                walk(s, cwd, visit, _depth + 1)
            continue
        words = item[1]
        for w in words:
            for s in w[2]:
                walk(s, cwd, visit, _depth + 1)
        argv, scripts = unwrap([w[1] for w in words])
        for s in scripts:
            walk(s, cwd, visit, _depth + 1)
        if not argv:
            continue
        name = argv[0]
        if name in ("cd", "pushd"):
            args = [a for a in argv[1:] if not a.startswith("-")]
            if not args:
                cwd = os.environ.get("HOME", cwd) if name == "cd" else cwd
            else:
                cwd = resolve(args[0], cwd)
            continue
        visit(argv, cwd)
    return cwd
