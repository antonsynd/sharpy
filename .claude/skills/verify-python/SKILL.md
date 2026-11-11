---
name: verify-python
description: Run a Python 3 expression or snippet to verify behavior before implementing Sharpy semantics
disable-model-invocation: true
argument-hint: <expression or code>
---

Run the given expression or code in Python 3 and report the result.

```bash
python3 -c "$ARGUMENTS"
```

If the argument is multi-line or complex, use a heredoc instead:

```bash
python3 << 'EOF'
$ARGUMENTS
EOF
```

The oracle is CPython 3.15, the minor in the repo-root `.python-version` (CLAUDE.md rule 6). Run `python3 --version` first; when the host's `python3` is older and a `python3.15` is on `PATH`, use that instead, and state the minor you ran with the output.

After showing the output, note whether Sharpy can match this behavior exactly or if .NET semantics differ (Axiom 1 wins over Axiom 2 when they conflict).
