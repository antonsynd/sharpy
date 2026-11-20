"""Split a Bash tool command into the simple commands bash would run, for the PreToolUse hooks.

Used by guard-shared-tree.sh and enforce-dotnet-serialized.sh (imported from this directory).
``walk(src, cwd, visit, opaque=...)`` calls ``visit(argv, cwd, env, assigns)`` for every simple
command, in order, with:

- here-documents turned into ``<<<`` here-strings first, at any depth (inside ``"$(cat <<'EOF'
  ... EOF)"`` too): a body is data to ``cat``, a script to ``bash <<EOF``, and, with an unquoted
  delimiter, text whose ``$(...)`` runs; its quotes and command-like text are never code;
- separators split: ``;`` ``&&`` ``||`` ``|`` ``|&`` ``&`` and newlines; ``#`` comments dropped;
- ``( ... )`` subshells and ``$( ... )`` / backtick substitutions walked as nested commands, their
  ``cd`` and assignments not leaking out; ``bash|sh|zsh|dash|ksh -c '<script>'``, a shell's
  here-string script, ``eval``, ``env -S``, ``flock -c``, ``watch`` and ``parallel`` walked as
  scripts;
- quotes removed, ``\\`` escapes and line continuations applied, redirections removed from argv;
- leading ``VAR=value`` assignments (passed to visit as ``assigns``) and the launchers in
  PREFIXES stripped with their options, so argv[0] is the program run; ``env -C``/``sudo -D``
  set its cwd; a program named by a known variable (``G=git; $G reset``) expanded; ``find -exec/
  -execdir/-ok/-okdir ... ;|+`` visited as commands (``{}`` read as ``<start path>/{}``) and
  ``find -delete`` as ``rm <start path>/{}``;
- ``cd`` / ``cd -`` / ``pushd`` / ``popd`` and plain assignments (``X=v``, ``export X=v``)
  tracked: ``cwd`` is the directory the command runs in and ``env`` the variables the command text
  set, as far as the text says. ``None`` means unknown (``cd "$X"``, a loop variable, a pop
  after ``pushd -n``). ``$(mktemp [-d])`` is a fresh path in $TMPDIR and ``$(git rev-parse
  --show-toplevel)`` the cwd's toplevel.

A script it cannot see (``eval "$X"``, ``bash -c "$X"``, ``$X args``, ``... | bash``, ``sh -s``,
``bash < file``, ``bash <(...)``) goes to ``opaque(cwd, what)``, else raises ParseError.
It does not evaluate the shell: a variable not set in the text resolves from the hook's own
environment, else it is unknown; globs stay unexpanded; functions, aliases, ``source`` and
script files run by path are not followed; control keywords (``if``/``then``/``do``/``{``...)
are skipped as argv[0], not parsed. Launchers not in PREFIXES (ssh, su -c, script -c, docker
exec, chroot, make, npm run ...) hide their command. An unterminated quote or substitution
raises ParseError; ``bash_accepts`` tells whether bash itself would refuse the text.
"""

import os
import re
import subprocess

# launcher -> (options that take a separate argument, positional args before the command, mode)
# mode: "cmd" (the rest is argv), "script" (the rest, joined, is a shell script)
PREFIXES = {
    "env": ({"-u", "--unset", "-C", "--chdir"}, 0, "cmd"),
    "sudo": ({"-u", "-g", "-C", "-D", "-h", "-p", "-r", "-t", "-U", "-T"}, 0, "cmd"),
    "nice": ({"-n", "--adjustment"}, 0, "cmd"),
    "timeout": ({"-s", "--signal", "-k", "--kill-after"}, 1, "cmd"),
    "stdbuf": ({"-i", "-o", "-e", "--input", "--output", "--error"}, 0, "cmd"),
    "xargs": ({"-I", "-n", "-L", "-P", "-s", "-d", "-E", "-a", "--arg-file", "--delimiter",
               "--max-args", "--max-lines", "--max-procs", "--max-chars", "--eof", "--replace"},
              0, "cmd"),
    "exec": ({"-a"}, 0, "cmd"),
    "command": (set(), 0, "cmd"),
    "nohup": (set(), 0, "cmd"),
    "setsid": (set(), 0, "cmd"),
    "time": ({"-f", "--format", "-o", "--output"}, 0, "cmd"),
    "ionice": ({"-c", "--class", "-n", "--classdata", "-p", "--pid", "-P", "--pgid", "-u",
                "--uid"}, 0, "cmd"),
    "taskset": (set(), 1, "cmd"),
    "chrt": (set(), 1, "cmd"),
    "flock": ({"-w", "--wait", "--timeout", "-E", "--conflict-exit-code"}, 1, "cmd"),
    "strace": ({"-e", "-o", "-p", "-s", "-u", "-E", "-P", "-a", "-b", "-I", "-O", "-S", "-X",
                "-U"}, 0, "cmd"),
    "watch": ({"-n", "--interval", "-q", "--equexit"}, 0, "script"),
    "parallel": ({"-j", "--jobs", "-S", "--sshlogin", "-a", "--arg-file", "--delay", "--timeout",
                  "-n", "--max-args", "-N", "--colsep", "-C", "-d", "--delimiter", "-I", "-E",
                  "--env", "--joblog", "--results", "--tmpdir", "--workdir", "--wd", "-P",
                  "--max-procs", "--halt", "--retries"}, 0, "script"),
}
SHELLS = {"bash", "sh", "zsh", "dash", "ksh"}
KEYWORDS = {"if", "then", "else", "elif", "fi", "do", "done", "while", "until", "{", "}", "!",
            "coproc"}
DECLARERS = {"export", "declare", "local", "readonly", "typeset"}
ASSIGN = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)(\[[^]]*\])?\+?=(.*)$", re.S)
FIND_EXEC = {"-exec", "-execdir", "-ok", "-okdir"}


class ParseError(Exception):
    pass


def bash_accepts(src):
    """True when `bash -n` parses src (it runs nothing); also True if bash cannot be run.
    The text goes on stdin: an argv string is capped at 128 KiB."""
    try:
        r = subprocess.run(["bash", "-n"], input=src.encode("utf-8", "surrogateescape"),
                           capture_output=True, timeout=3)
    except Exception:
        return True
    return r.returncode == 0


# ---------------------------------------------------------------------------------------------
# Pre-pass: here-document bodies out, at every nesting depth.

_DELIM_END = set(" \t\n;&|<>()")


def _herestring(body, quoted, kind):
    """The ``<<<`` word bash would read for a here-document body: single-quoted when the
    delimiter was quoted (no expansion), else double-quoted with its substitutions intact."""
    if kind == "bt" and "`" in body:
        raise ParseError("a here-document holding a backtick inside backticks")
    if quoted:
        return "<<< '" + body.replace("'", "'\\''") + "' "
    out, k, n = [], 0, len(body)
    while k < n:
        ch = body[k]
        if ch == "\\":
            pair = body[k:k + 2]
            out.append(pair if pair[1:] in ("$", "`", "\\", "\n") else "\\\\")
            k += 2 if pair[1:] in ("$", "`", "\\", "\n") else 1
        elif body.startswith("$(", k) or body.startswith("${", k) or ch == "`":
            # copied whole: a substitution's own quotes are code, not the body's text
            e = (_skip_backtick(body, k + 1) if ch == "`" else _match_brace(body, k + 2)
                 if body[k + 1] == "{" else _match_close(body, k + 2, "(", ")"))
            out.append(body[k:e])
            k = e
        else:
            out.append('\\"' if ch == '"' else ch)
            k += 1
    return '<<< "' + "".join(out) + '" '


def inline_heredocs(src):
    """``src`` with each ``<<[-]WORD`` and its body turned into a ``<<<`` here-string at the
    operator, so a body is data (``cat``), a script (``bash <<EOF``), or, unquoted, text whose
    ``$(...)`` still runs."""
    out, pending = [], []
    stack = [["sh", 0, False]]  # kind, paren depth, is a $( frame
    i, n, prev = 0, len(src), "\n"
    while i < n:
        c = src[i]
        kind = stack[-1][0]
        if c == "\n":
            out.append(c)
            i += 1
            prev = c
            if pending and kind in ("sh", "bt"):
                for slot, delim, strip_tabs, quoted in pending:
                    lines = []
                    while i < n:
                        j = src.find("\n", i)
                        line = src[i:] if j < 0 else src[i:j]
                        i = n if j < 0 else j + 1
                        line = line.lstrip("\t") if strip_tabs else line
                        if line == delim:
                            break
                        lines.append(line)
                    out[slot] = _herestring("\n".join(lines), quoted, kind)
                pending = []
            continue
        if kind == "dq":
            if c == "\\":
                out.append(src[i:i + 2])
                i += 2
            elif c == '"':
                stack.pop()
                out.append(c)
                i += 1
            elif src.startswith("${", i):
                stack.append(["br", 0, True])
                out.append("${")
                i += 2
            elif src.startswith("$((", i):
                stack.append(["ar", 0, False])
                out.append("$((")
                i += 3
            elif src.startswith("$(", i):
                stack.append(["sh", 0, True])
                out.append("$(")
                i += 2
            elif c == "`":
                stack.append(["bt", 0, False])
                out.append(c)
                i += 1
            else:
                out.append(c)
                i += 1
            prev = c
            continue
        if kind == "ar":
            if c == "(":
                stack[-1][1] += 1
            elif c == ")":
                if stack[-1][1] > 0:
                    stack[-1][1] -= 1
                elif src.startswith("))", i):
                    stack.pop()
                    out.append("))")
                    i += 2
                    prev = ")"
                    continue
            out.append(c)
            i += 1
            prev = c
            continue
        if kind == "br":  # ${...}: no here-documents or comments inside; True = opened in "..."
            if c == "\\":
                out.append(src[i:i + 2])
                i += 2
                continue
            if src.startswith("$'", i) and not stack[-1][2]:
                j = i + 2
                while j < n and src[j] != "'":
                    j += 2 if src[j] == "\\" else 1
                out.append(src[i:j + 1])
                i = j + 1
                continue
            if c == "'" and not stack[-1][2]:
                j = src.find("'", i + 1)
                j = n - 1 if j < 0 else j
                out.append(src[i:j + 1])
                i = j + 1
                continue
            if src.startswith("${", i) or src.startswith("$(", i):
                stack.append(["br", 0, stack[-1][2]] if src[i + 1] == "{" else ["sh", 0, True])
                out.append(src[i:i + 2])
                i += 2
                continue
            if c == '"':
                stack.append(["dq", 0, False])
            elif c == "}":  # a bare { does not nest: `${x:-{}` is complete (bash prints `{`)
                stack.pop()
            out.append(c)
            i += 1
            prev = c
            continue
        # shell code: "sh" (top level or a $( frame) or "bt" (inside backticks)
        if c == "\\":
            out.append(src[i:i + 2])
            i += 2
            prev = "a"
            continue
        if c == "'":
            j = src.find("'", i + 1)
            j = n - 1 if j < 0 else j
            out.append(src[i:j + 1])
            i = j + 1
            prev = "'"
            continue
        if src.startswith("$'", i):
            j = i + 2
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            out.append(src[i:j + 1])
            i = j + 1
            prev = "'"
            continue
        if c == "#" and prev in " \t\n;&|(":
            j = src.find("\n", i)
            j = n if j < 0 else j
            out.append(src[i:j])
            i = j
            continue
        if c == '"':
            stack.append(["dq", 0, False])
        elif c == "`":
            if kind == "bt":
                stack.pop()
            else:
                stack.append(["bt", 0, False])
        elif src.startswith("${", i):
            stack.append(["br", 0, False])
            out.append("${")
            i += 2
            prev = "{"
            continue
        elif src.startswith("$((", i) or (src.startswith("((", i) and prev in " \t\n;&|("):
            k = 3 if c == "$" else 2  # $(( )) expansion, or the (( )) / for (( )) command
            stack.append(["ar", 0, False])
            out.append(src[i:i + k])
            i += k
            prev = "("
            continue
        elif src.startswith("$(", i):
            stack.append(["sh", 0, True])
            out.append("$(")
            i += 2
            prev = "("
            continue
        elif c == "(":
            stack[-1][1] += 1
        elif c == ")":
            if stack[-1][1] > 0:
                stack[-1][1] -= 1
            elif stack[-1][2]:
                stack.pop()
        elif src.startswith("<<<", i):  # a here-string: its second `<` opens no here-document
            out.append("<<<")
            i += 3
            prev = "<"
            continue
        elif src.startswith("<<", i):
            j = i + 2
            strip_tabs = j < n and src[j] == "-"
            j += 1 if strip_tabs else 0
            while j < n and src[j] in " \t":
                j += 1
            word, quoted = [], False
            while j < n and src[j] not in _DELIM_END:
                d = src[j]
                if d in "'\"":
                    k = src.find(d, j + 1)
                    k = n if k < 0 else k
                    word.append(src[j + 1:k])
                    j = k + 1
                    quoted = True
                elif d == "\\":
                    word.append(src[j + 1:j + 2])
                    j += 2
                    quoted = True
                else:
                    word.append(d)
                    j += 1
            if word:
                pending.append((len(out), "".join(word), strip_tabs, quoted))
                out.append("<<< '' ")  # filled when the body is read; empty if it never is
                i = j
                prev = " "
                continue
        out.append(c)
        i += 1
        prev = c
    return "".join(out)


# ---------------------------------------------------------------------------------------------
# Tokenizer.

def _match_close(src, i, open_ch, close_ch):
    """Index just past the ``close_ch`` matching the ``open_ch`` already consumed before ``i``."""
    depth, n = 1, len(src)
    while i < n:
        c = src[i]
        if c == "\\":
            i += 2
            continue
        if src.startswith("$'", i):
            i = _skip_ansi(src, i + 2)
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
        if c == "#" and i > 0 and src[i - 1] in " \t\n;&|(":
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise ParseError("unterminated " + open_ch)


def _match_brace(src, i):
    """Index just past the ``}`` closing a ``${`` consumed before ``i``. A nested ``${`` nests;
    a bare ``{`` does not (bash: ``echo ${x:-{a}b}`` prints ``{ab}``)."""
    depth, n = 1, len(src)
    while i < n:
        c = src[i]
        if c == "\\":
            i += 2
            continue
        if src.startswith("$'", i):
            i = _skip_ansi(src, i + 2)
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
        if src.startswith("$(", i):
            i = _match_close(src, i + 2, "(", ")")
            continue
        if src.startswith("${", i):
            depth += 1
            i += 2
            continue
        if c == "}":
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise ParseError("unterminated ${")


def _subs_in(text):
    """The command substitutions ($(...), backticks) and process substitutions (<(...),
    >(...)) inside a span of shell words."""
    found = [sub for t in tokenize(text) if t[0] == "w" for sub in t[2]]
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c == "\\":
            i += 2
        elif text.startswith("$'", i):
            i = _skip_ansi(text, i + 2)
        elif c == "'":
            j = text.find("'", i + 1)
            i = n if j < 0 else j + 1
        elif c == '"':
            i = _skip_dquote(text, i + 1)
        elif text.startswith("<(", i) or text.startswith(">(", i):
            k = _match_close(text, i + 2, "(", ")")
            found.append(text[i + 2:k - 1])
            i = k
        else:
            i += 1
    return found


def _skip_ansi(src, i):
    """Index just past the ``'`` closing a ``$'`` consumed before ``i`` (``\\'`` is escaped)."""
    n = len(src)
    while i < n:
        if src[i] == "\\":
            i += 2
            continue
        if src[i] == "'":
            return i + 1
        i += 1
    raise ParseError("unterminated $'")


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


_ARRAY_ASSIGN = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*(\[[^]]*\])?\+?=$")
_REDIR = re.compile(r"&>>|&>|<<<|<<-|<<|<>|>>|>&|<&|>\||<|>")


def tokenize(src):
    """Tokens: ('w', text, [substitution sources]), ('op', text) or ('redir', text).

    Expects here-documents already inlined (inline_heredocs)."""
    toks = []
    word, subs = None, []
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
                if src.startswith("$(", j) and not src.startswith("$((", j):
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
            k = _match_close(src, i + 3, "(", ")")  # arithmetic, which may hold $(...)
            if k < n and src[k] == ")":
                k += 1
            subs.extend(_subs_in(src[i + 3:k - 2]))
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
            k = _match_brace(src, i + 2)
            subs.extend(_subs_in(src[i + 2:k - 1]))  # ${x:-$(cmd)} runs cmd
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
            m = _REDIR.match(src, i)
            i = m.end()
            toks.append(("redir", m.group(0)))
            continue
        if c == "(" and word is not None and _ARRAY_ASSIGN.match("".join(word)):
            k = _match_close(src, i + 1, "(", ")")  # NAME=( ... ): words, not a subshell,
            subs.extend(_subs_in(src[i + 1:k - 1]))  # but their $(...) still run
            add(src[i:k])
            i = k
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


# ---------------------------------------------------------------------------------------------
# Words.

_VAR = re.compile(r"\$(?:\{([A-Za-z_][A-Za-z0-9_]*)\}|([A-Za-z_][A-Za-z0-9_]*))")


_SUB_HEAD = re.compile(r"^\$\(([^()`$'\"]*)\)")
_mktemps = [0]


def _known_sub(inner, env, cwd):
    """The value of two substitutions whose output the text fixes: ``mktemp [-d] [-q] [-t]
    [--suffix=S] [TEMPLATE]`` (a fresh name in $TMPDIR, else /tmp; with -p/--tmpdir=DIR or a
    template outside it, unknown) and ``git rev-parse --show-toplevel`` (run in ``cwd``)."""
    words = inner.split()
    if words == ["git", "rev-parse", "--show-toplevel"]:
        if cwd is None:
            return None
        try:
            r = subprocess.run(["git", "-C", cwd, "rev-parse", "--show-toplevel"],
                               capture_output=True, text=True, timeout=3)
        except Exception:
            return None
        return r.stdout.strip() or None if r.returncode == 0 else None
    if not words or words[0] != "mktemp":
        return None
    in_tmp, template = False, None
    for w in words[1:]:
        if w in ("-t", "--tmpdir"):
            in_tmp = True
        elif w in ("-d", "--directory", "-q", "--quiet", "-u", "--dry-run") or w.startswith(
                "--suffix="):
            continue
        elif w.startswith("-") or template is not None:
            return None  # -p DIR, --tmpdir=DIR, an option this does not know
        else:
            template = w
    if template is not None and ("/" in template or not in_tmp):
        return None  # mktemp TEMPLATE creates it in the current directory
    tmp = env["TMPDIR"] if "TMPDIR" in env else (os.environ.get("TMPDIR") or "/tmp")
    if tmp is None:
        return None
    _mktemps[0] += 1
    return os.path.join(tmp, "tmp.hook%d" % _mktemps[0])


def expand(word, env=None, cwd=None):
    """``word`` with ~ and $VAR / ${VAR} expanded, or None when it cannot be resolved: it holds a
    substitution (but a leading ``$(mktemp ...)`` or ``$(git rev-parse --show-toplevel)``), a
    special or unset variable, or one the command text made unknown."""
    env = {} if env is None else env
    m = _SUB_HEAD.match(word)
    if m:
        head = _known_sub(m.group(1), env, cwd)
        tail = expand(word[m.end():], env, cwd) if head is not None else None
        return None if tail is None else head + tail
    if "`" in word or "$(" in word:
        return None
    if word == "~" or word.startswith("~/"):
        home = env.get("HOME", os.environ.get("HOME"))
        if home is None:
            return None
        word = home + word[1:]
    out, pos = [], 0
    for m in _VAR.finditer(word):
        name = m.group(1) or m.group(2)
        val = env[name] if name in env else os.environ.get(name)
        if val is None:
            return None
        out.append(word[pos:m.start()])
        out.append(val)
        pos = m.end()
    rest = word[pos:]
    if "$" in rest:
        return None  # $1, $@, $$, ${x:-y} ...
    return "".join(out) + rest


def resolve(path, cwd, env=None):
    """Absolute normalized path for ``path`` run in ``cwd``, or None when unknown."""
    p = expand(path, env, cwd)
    if p is None or p == "":
        return None
    if not os.path.isabs(p):
        if cwd is None:
            return None
        p = os.path.join(cwd, p)
    return os.path.normpath(p)


def _skip_opts(argv, takes):
    while argv and argv[0].startswith("-") and argv[0] != "-":
        opt = argv.pop(0)
        if opt == "--":
            break
        if opt in takes and argv:
            argv.pop(0)


def _find(argv):
    """The commands ``find`` runs: its -exec family, and -delete as an rm."""
    args = argv[1:]
    while args and args[0] in ("-H", "-L", "-P") or (args and args[0].startswith("-O")):
        args.pop(0)
    if args and args[0] == "-D" and len(args) > 1:
        args = args[2:]
    starts = []
    while args and not args[0].startswith("-") and args[0] not in ("(", "!", "\\("):
        starts.append(args.pop(0))
    base = (starts or ["."])[0]
    placeholder = base.rstrip("/") + "/{}"
    extras, k = [], 0
    while k < len(args):
        if args[k] in FIND_EXEC:
            cmd, k = [], k + 1
            while k < len(args) and args[k] not in (";", "+"):
                cmd.append(placeholder if args[k] == "{}" else args[k])
                k += 1
            if cmd:
                extras.append(cmd)
        elif args[k] == "-delete":
            extras.extend(["rm", s.rstrip("/") + "/{}"] for s in (starts or ["."]))
        k += 1
    return extras


def shell_args(argv):
    """(single-letter flags, index of the first operand) of a ``bash|sh|... [options]`` argv:
    with ``c`` the operand is the script; else with ``s``, none, or ``-``, stdin is the script;
    else the operand names a script file."""
    flags, k = set(), 1
    while k < len(argv):
        a = argv[k]
        if a in ("-o", "+o", "-O", "+O", "--rcfile", "--init-file"):
            k += 2
            continue
        if a in ("--", "-"):
            k += a == "--"
            break
        if a[:1] not in ("-", "+") or a == "+":
            break
        if not a.startswith("--"):
            flags.update(a[1:])
        k += 1
    return flags, k


_CHDIR = {"env": ("-C", "--chdir"), "sudo": ("-D", "--chdir")}


def _take_opts(name, argv, chdirs, scripts):
    """Pop env's or sudo's options off ``argv``, recording the last ``-C DIR`` /
    ``--chdir[=]DIR`` (env) or ``-D DIR`` / ``--chdir[=]DIR`` (sudo) in ``chdirs`` (a later one
    replaces an earlier one). True when ``env -S`` took a script."""
    short, long_ = _CHDIR[name]
    takes = PREFIXES[name][0]
    mine, took = [], False
    while argv and argv[0].startswith("-") and argv[0] != "-":
        opt = argv.pop(0)
        if opt == "--":
            break
        if name == "env" and opt in ("-S", "--split-string") and argv:
            scripts.append(argv.pop(0))
            took = True
            break
        if name == "env" and opt.startswith("--split-string="):
            scripts.append(opt.split("=", 1)[1])
            took = True
            break
        if opt in (short, long_):
            if argv:
                mine.append(argv.pop(0))
        elif opt.startswith(long_ + "="):
            mine.append(opt.split("=", 1)[1])
        elif opt.startswith(short) and not opt.startswith("--"):
            mine.append(opt[len(short):])
        elif opt in takes and argv:
            argv.pop(0)
    chdirs.extend(mine[-1:])
    return took


def unwrap(argv):
    """Strip assignments, keywords and launchers. Returns (argv, scripts, extras, assigns,
    chdirs): the program's argv, shell scripts it runs, further argvs it runs (find -exec), the
    VAR=value assignments that prefixed it, and the directories ``env -C`` / ``sudo -D`` run it
    in (each relative to the one before)."""
    argv, scripts, extras, assigns, chdirs = list(argv), [], [], {}, []

    def take_assigns():
        while argv and ASSIGN.match(argv[0]):
            m = ASSIGN.match(argv.pop(0))
            assigns[m.group(1)] = m.group(3)

    while argv:
        take_assigns()
        if not argv:
            break
        if argv[0] in KEYWORDS:
            argv.pop(0)
            continue
        name = os.path.basename(argv[0])
        if name in _CHDIR:
            argv.pop(0)
            if _take_opts(name, argv, chdirs, scripts):
                return [], scripts, extras, assigns, chdirs
            continue
        if name in PREFIXES:
            takes, npos, mode = PREFIXES[name]
            argv.pop(0)
            _skip_opts(argv, takes)
            if name == "flock":
                if argv:
                    argv.pop(0)  # the lock file or fd
                if argv and argv[0] in ("-c", "--command"):
                    return [], argv[1:2], extras, assigns, chdirs
                _skip_opts(argv, takes)
            else:
                for _ in range(npos):
                    if argv:
                        argv.pop(0)
            if mode == "script":
                if name == "parallel":
                    for k, a in enumerate(argv):
                        if a.startswith(":::"):
                            argv = argv[:k]
                            break
                if argv:
                    scripts.append(" ".join(argv))
                return [], scripts, extras, assigns, chdirs
            continue
        if name == "find":
            extras.extend(_find(argv))
            return argv, scripts, extras, assigns, chdirs
        if name in SHELLS:
            flags, k = shell_args(argv)
            if "c" in flags:
                return [], argv[k:k + 1], extras, assigns, chdirs
            return argv, scripts, extras, assigns, chdirs
        if name == "eval":
            return [], [" ".join(argv[1:])], extras, assigns, chdirs
        break
    return argv, scripts, extras, assigns, chdirs


# ---------------------------------------------------------------------------------------------
# The walk.

def _split(toks):
    """Group tokens into ('cmd', [words], io), ('open',), ('close',), ('subs', [sources]).
    ``io`` says where the command's stdin comes from: ``piped`` (after ``|``), ``file`` (a ``<``
    redirect), ``procsub`` (a ``<(...)`` operand), ``herestring`` (the ``<<<`` word's text)."""
    out, cur, io, redir, piped = [], [], {}, None, False
    for t in toks:
        if t[0] == "redir":
            redir = t[1]
            continue
        if t[0] == "w":
            if redir is not None:
                if redir == "<<<":
                    io["herestring"] = t[1]
                elif redir in ("<", "<>"):
                    io["file"] = True
                redir = None
                out.append(("subs", t[2]))
                continue
            if not cur and piped:
                io["piped"] = True
            cur.append(t)
            continue
        if redir == "<" and t[1] == "(":
            io["procsub"] = True  # <(...): its commands are walked as a subshell below
        redir = None
        if cur:
            out.append(("cmd", cur, io))
            cur, io = [], {}
        piped = t[1] in ("|", "|&") or (piped and t[1] == "(")
        if t[1] == "(":
            out.append(("open",))
        elif t[1] == ")":
            out.append(("close",))
    if cur:
        out.append(("cmd", cur, io))
    return out


class _State:
    def __init__(self, cwd, env, old=None, dirs=()):
        self.cwd, self.env, self.old = cwd, dict(env), old
        self.dirs = None if dirs is None else list(dirs)  # None: the stack is not followed

    def copy(self):
        return _State(self.cwd, self.env, self.old, self.dirs)


def walk(src, cwd, visit, env=None, opaque=None, _depth=0):
    """Call visit(argv, cwd, env, assigns) for each simple command in ``src``, and
    opaque(cwd, what) for each script the text runs but does not show: ``eval "$X"``,
    ``bash -c "$X"``, ``$X args``, ``... | bash``, ``bash < file``, ``sh -s``. Without
    ``opaque``, such a script raises ParseError."""
    env = dict(env or {})
    env["PWD"], env["OLDPWD"] = cwd, None  # the command's, never the hook process's

    def no_opaque(cwd, what):
        raise ParseError("runs a script the text does not show: " + what)

    try:
        _walk(src, _State(cwd, env), (visit, opaque or no_opaque), _depth)
    except RecursionError:
        raise ParseError("nested too deeply")


def _walk(src, st, cb, depth):
    if depth > 8:
        raise ParseError("substitutions nested more than 8 deep")
    stack = []
    for item in _split(tokenize(inline_heredocs(src))):
        if item[0] == "open":
            stack.append(st)
            st = st.copy()
            continue
        if item[0] == "close":
            st = stack.pop() if stack else st
            continue
        if item[0] == "subs":
            for s in item[1]:
                _walk(s, st.copy(), cb, depth + 1)
            continue
        words = item[1]
        for w in words:
            for s in w[2]:
                _walk(s, st.copy(), cb, depth + 1)
        _command([w[1] for w in words], st, cb, depth, item[2])


def _opaque_name(word):
    return "$" in word or "`" in word


def _command(raw, st, cb, depth, io=None):
    visit, opaque = cb
    io = io or {}
    if raw and raw[0] in ("for", "select") and len(raw) > 1:
        st.env[raw[1]] = None
        return
    if raw and raw[0] == "read":
        for a in raw[1:]:
            if not a.startswith("-"):
                st.env[a] = None
        return
    if raw and all(ASSIGN.match(a) for a in raw):
        for a in raw:
            m = ASSIGN.match(a)
            st.env[m.group(1)] = expand(m.group(3), st.env, st.cwd)
        return
    argv, scripts, extras, assigns, chdirs = unwrap(raw)
    for _ in range(4):  # a program named by a variable: G=git; $G reset
        if not argv or not _opaque_name(argv[0]):
            break
        val = expand(argv[0], st.env, st.cwd)
        if val is None:
            break
        more = unwrap(val.split() + argv[1:])
        argv = more[0]
        scripts += more[1]
        extras += more[2]
        assigns.update(more[3])
        chdirs += more[4]
    cst = st
    if chdirs:
        cst = st.copy()
        for d in chdirs:
            cst.cwd = resolve(d, cst.cwd, cst.env)
    for s in scripts:
        _walk(s, cst.copy(), cb, depth + 1)
    if argv and _opaque_name(argv[0]):
        opaque(cst.cwd, argv[0])
        return
    if argv and argv[0] in DECLARERS:
        for a in argv[1:]:
            m = ASSIGN.match(a)
            if m:
                st.env[m.group(1)] = expand(m.group(3), st.env, st.cwd)
        return
    if argv:
        name = argv[0]
        if name in ("cd", "pushd", "popd"):
            _chdir(name, argv, st)
            return
        if os.path.basename(name) in SHELLS:
            flags, k = shell_args(argv)
            if "s" in flags or k >= len(argv) or argv[k] == "-":  # stdin is the script
                if "herestring" in io:
                    _walk(io["herestring"], cst.copy(), cb, depth + 1)
                elif io.get("piped") or io.get("file") or io.get("procsub"):
                    opaque(cst.cwd, name + " reading its script from stdin")
            elif io.get("procsub"):
                opaque(cst.cwd, name + " <(...)")
        visit(argv, cst.cwd, cst.env, assigns)
    for e in extras:
        _command(e, cst.copy(), cb, depth + 1)


def _chdir(name, argv, st):
    """cd / pushd / popd. OLDPWD moves only when bash changes directory (`pushd +0` does; a
    refused pushd/popd, or any -n, does not)."""
    prev, moved = st.cwd, True
    if name == "cd":
        args = [a for a in argv[1:] if not a.startswith("-") or a == "-"]
        if not args:
            st.cwd = resolve("~", None, st.env)
        elif args[0] == "-":
            st.cwd = st.old
        else:
            st.cwd = resolve(args[0], st.cwd, st.env)
    elif "-n" in argv[1:]:
        # -n edits the stack without a cd; bash's -n rotations and pops index it in ways this
        # does not model, so from here the stack below the top is unknown
        st.dirs, moved = None, False
    else:
        # bash's directory stack: index 0 is the cwd, then the most recently pushed. A refused
        # pushd/popd (empty stack, N out of range) changes nothing, as in bash.
        args = argv[1:]
        rot = args[0] if args and re.match(r"^[+-][0-9]+$", args[0]) else None
        if name == "pushd" and args and rot is None:
            st.cwd = resolve(args[0], st.cwd, st.env)
            if st.dirs is not None:
                st.dirs.append(prev)
        elif st.dirs is None:  # pops or rotates into the unknown part: unknown, maybe refused
            st.cwd = st.old = None
            st.env["PWD"] = st.env["OLDPWD"] = None
            return
        else:
            full = [st.cwd] + st.dirs[::-1]
            if rot is not None:  # +N|-N: rotate (pushd) or drop (popd) entry N
                k = int(rot[1:]) if rot[0] == "+" else len(full) - 1 - int(rot[1:])
                if not 0 <= k < len(full) or (name == "popd" and len(full) < 2):
                    moved = False
                elif name == "pushd":
                    full = full[k:] + full[:k]
                else:
                    moved = k == 0
                    del full[k]
            elif len(full) >= 2:  # bare pushd swaps the top two; popd drops the top
                full = [full[1], full[0]] + full[2:] if name == "pushd" else full[1:]
            else:
                moved = False
            st.cwd, st.dirs = full[0], full[1:][::-1]
    if moved:
        st.old = prev
        st.env["PWD"], st.env["OLDPWD"] = st.cwd, prev
