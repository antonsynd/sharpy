# Sharpy skills

AI agent skills for working with [Sharpy](https://github.com/antonsynd/sharpy),
a statically-typed Pythonic language for .NET. They follow the
[Agent Skills](https://agentskills.io/specification) format, so any AI coding
agent can use them to write Sharpy or set up a Sharpy project.

Sharpy looks like Python, and coding agents will write plain Python by reflex —
these skills exist to correct that. Every code example and command in them has
been compiled and executed against the Sharpy compiler at the revision that
last touched this directory.

## Skills

- [`/sharpy-syntax`](sharpy-syntax/SKILL.md): Corrects pretrained Python
  assumptions so your agent writes valid Sharpy. Use it whenever an agent
  writes `.spy` code.
- [`/new-sharpy-project`](new-sharpy-project/SKILL.md): Creates a new Sharpy
  project — toolchain setup, single-file programs, and `.spyproj` projects.

## Install

### Claude Code

From a checkout of this repository:

```text
/plugin marketplace add /path/to/sharpy/skills
/plugin install skills@sharpy
```

Or copy (or symlink) the individual skill directories into your agent's skills
directory — for Claude Code, `~/.claude/skills/`:

```bash
cp -r skills/sharpy-syntax skills/new-sharpy-project ~/.claude/skills/
```

### Other agents

Copy the skill directories into wherever your agent discovers skills; each
skill is a single self-contained `SKILL.md`.

## Contributing

Keep the skills terse — they are correction layers loaded into an agent's
context window, not documentation. Only add facts a Python-fluent model gets
wrong. Verify every example against the compiler before committing
(`sharpyc run` each positive example; confirm each claimed diagnostic).
