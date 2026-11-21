#!/bin/bash
# PreToolUse(Bash) hook: the shared-tree contract (docs/design/verification-contract.md §9) and the
# force-push rule (CLAUDE.md › Git & Release), enforced instead of asked for. Denies (exit 2):
#
#   1. In the repository's MAIN worktree (`git rev-parse --git-dir` = `--git-common-dir`), the one
#      a session's agents share (a linked worktree — .claude/worktrees/*, a scratchpad `git
#      worktree add` — is its owner's own and is never judged here):
#      a. a SUBAGENT (hook input has `agent_id`) may not run the verbs §9 names — `git checkout|
#         restore|clean|stash|reset` — in any form but `stash list|show`;
#      b. the LEAD, and a subagent's `git switch`, may move and create branches (`switch <b>`,
#         `switch -c <new> [start]`, `switch --detach`, `checkout <b>`, `checkout -b <new>
#         [start]`; git refuses them when they would overwrite local changes) and unstage
#         (`restore --staged|-S` without `--worktree|-W`; `reset [-q] [<tree-ish>] [--] <paths>`),
#         but not the forms that destroy uncommitted work: other `restore`; `clean` without
#         -n/--dry-run; `stash` but list/show; `reset` without paths, but --soft; checkout/switch
#         with -f/--force/--discard-changes/-m/--merge/--ours/--theirs/-p/--patch; `checkout` of a
#         pathspec (`--`, `.`, or an argument neither a commit by `rev-parse --verify <arg>^{commit}`
#         nor a refs/remotes/*/<arg>).
#      The docs (code.claude.com/docs/en/hooks, "Hooks in subagents"): "When a subagent calls a
#      tool, tool events such as `PreToolUse` and `PostToolUse` fire the same configured hooks as
#      in the main conversation, and the input carries the `agent_id` and `agent_type` common
#      input fields that identify the subagent."
#   2. `rm` (and `find -delete`, `find -exec rm`) of the main worktree itself or any directory
#      above it, for anyone; and of a path inside the main worktree, except under .claude/tmp/, a
#      .NET build output (`bin/` or `obj/` beside a *.*proj; build_tools/bin/ is source), and a
#      directory under .claude/worktrees/ that `git worktree list` does not register. The path's
#      parent decides, so removing a registered linked worktree's root is denied: `git worktree
#      remove`. A target the text cannot resolve (a loop variable, an unset variable, a `$(cmd)`
#      other than mktemp) is skipped. `$PWD`/`$OLDPWD` are the command's own (input cwd, then
#      each cd), never the hook process's. The top/ancestor check reads a target without a
#      trailing `/` through its parent's realpath, so `rm -f <symlink to the repo>` is allowed.
#      Known false deny: `find src -name bin -type d -exec rm -rf {} +` (find's `{}` is read as
#      `src/{}`); name the output directory instead: `find src/X/bin -name '*.dll' -delete`.
#   An UNKNOWN directory fails closed: when the text loses the cwd (`cd "$X"`, a `cd` into a
#   substitution, a rotation or pop after `pushd -n +N`), or names a -C, GIT_DIR/--git-dir or
#   GIT_WORK_TREE/--work-tree it cannot resolve, the git command is judged as in the main worktree.
#   Known false deny: `cd "$X" && git reset --hard` from a linked worktree; name the directory
#   instead: `git -C <path> reset --hard`, or `cd <literal path>`. `$(mktemp [-d])` is a fresh
#   path in $TMPDIR, `$(git rev-parse --show-toplevel)` the cwd's toplevel, and a directory that
#   `git init <dir>` / `git clone <url> <dir>` creates earlier in the text (one that does not
#   exist yet, or is empty) is a new repository, not this one, even if that step fails at run
#   time. A symlink the same text makes (`ln -s <main> x && git -C x reset --hard`) does not
#   exist yet when the hook runs, so the command is judged in the directory holding the link.
#   3. Staging without per-file pathspecs, in any worktree of this repository: `git add -A|--all|
#      -u|--update|.|./|:/` and `git commit -a|--all`. -u and commit -a are the same hazard as -A:
#      they stage every tracked file's changes, a peer's uncommitted hunks included.
#   Long options are read as git reads them: a unique prefix (`--force-w`, `--mirr`, `--forc`,
#   `--discard`, `--work`, `--al`) is the option it abbreviates, per subcommand (LONG_OPTS).
#   4. A forced `git push` (`--force`, `-f`, `--force-with-lease`, `+refspec`, `--mirror`) unless it
#      carries both `--force-with-lease` and `--force-if-includes` and every pushed ref is
#      `claude/*` (so never dev or mainline). `--mirror`, `--all`, `--branches`, `--tags` never.
#
# The worktree is resolved from the command itself: `git -C <dir>` (cumulative), `--work-tree`,
# `--git-dir`, `GIT_DIR=`/`GIT_WORK_TREE=` (prefixed or set earlier in the text), `env -C`/`sudo
# -D`, a preceding `cd`/`cd -`/`pushd`/`popd`, else the hook input's `cwd`. "This repository" is
# the one whose common dir matches that of $CLAUDE_PROJECT_DIR (else `cwd`), so a scratch repo is
# not guarded. Commands are found by .claude/hooks/shell_commands.py (its docstring lists what it
# follows).
#
# Out of scope (not followed, and not pattern-checked): a script run by its own path (`./x.sh`),
# a function or alias, a python/node subprocess, `xargs rm` input, `git rm`, `unlink`/`mv`, an
# rm target the text cannot resolve, a `git` alias, launchers outside shell_commands.PREFIXES
# (ssh, su -c, script -c, docker exec, make, npm run).
# PATTERN-CHECKED instead of walked: unparseable text (or substitutions nested more than 8
# deep), allowed when `bash -n` refuses it too (bash will not run it); and a script the text
# runs but does not show: `eval "$X"`, `bash -c "$X"`, `$X args`, `... | bash`, `sh -s`, `bash
# < file`, `bash <(...)`, and a script file (`bash x.sh`, `source x.sh`; perhaps written
# earlier in the same text). A literal `bash -c '...'`, here-string or here-document body is
# walked. The whole text is then searched for rules 1-2's verbs (where the opaque script runs in
# the main worktree, or anywhere unknown) and rules 3-4's (add -A/./-u, commit -a, a forced push
# without both lease flags or to dev/mainline; in every worktree of this repository).
# Known false deny: a guarded verb anywhere in such a text, e.g. `cat x.sh | bash; git stash
# list` in the main worktree; run the two commands separately.
#
# FAILS CLOSED when it cannot read the input or run: unreadable input (not JSON, no string
# command) is denied; an internal error pattern-checks the whole text for every rule, in any
# worktree; a python3 that fails to start or dies is denied by the shell wrapper (so with no
# python3 every Bash command is denied). Text it cannot follow is pattern-checked as above, not
# denied: `eval "$X"` with nothing guarded visible in the text is allowed. The input is read
# from stdin by python (no size limit). A hook timeout (settings.json, 5 s) is Claude Code's to
# handle; a 200 KB command, or 2,000 `git add x` on one line, takes well under one second.
#
# Hook input is JSON on stdin (tool_input.command, cwd, agent_id). Falsified by
# .claude/hooks/test-hooks.sh.
read -r -d '' PROG <<'PY'
import glob, json, os, re, subprocess, sys
sys.dont_write_bytecode = True

def deny(msg):
    sys.stderr.write("BLOCKED by .claude/hooks/guard-shared-tree.sh: " + msg + "\n")
    sys.exit(2)

try:
    data = json.loads(sys.stdin.buffer.read())
    cmd = data["tool_input"]["command"]
    cwd = data.get("cwd") or os.getcwd()
    agent = bool(data.get("agent_id"))
    assert isinstance(cmd, str) and isinstance(cwd, str)
except Exception as e:
    deny("cannot read the hook input (%s: %s); the hook fails closed." % (type(e).__name__, e))
if not cmd:
    sys.exit(0)

# Whole-text patterns for text the walk cannot follow: rules 1 and 2 in the main worktree,
# rules 3 and 4 in every worktree of this repository. The text is split at newlines and ; & |
# first, and each segment is searched in steps from its first `git` (a verb after it, then a
# flag after the verb): no nested unbounded quantifiers, so a long line stays linear.
SEGMENT = re.compile(r"[\n;&|]")
GIT = re.compile(r"\bgit\b")
RM = re.compile(r"(^|[\s(`])rm\s")
MAIN_VERB = re.compile(r"\b(checkout|restore|clean|stash|reset|switch)\b")
STAGE_ALL = ((re.compile(r"\badd\b"), re.compile(r"\s(-A|--all|-u|--update|\.)(?=[\s)]|$)")),
             (re.compile(r"\bcommit\b"),
              re.compile(r"\s(-[A-Za-z]*a[A-Za-z]*|--all)(?=[\s)]|$)")))
PUSH = re.compile(r"\bpush\b")
FORCE = re.compile(r"\s(--force(?![-\w])|-[A-Za-z]*f[A-Za-z]*|--mirror|\+\S)")
DEV = re.compile(r"\b(dev|mainline)\b")

def forced_push_text(push):
    """`push`: a segment from its `git`, holding `push` after it."""
    if FORCE.search(push):
        return True
    return "--force-with-lease" in push and ("--force-if-includes" not in push
                                             or bool(DEV.search(push)))

def matches(text, main, any_tree):
    for seg in SEGMENT.split(text):
        if main and RM.search(seg):
            return True
        g = GIT.search(seg) if "git" in seg else None
        if not g:
            continue
        if main and MAIN_VERB.search(seg, g.end()):
            return True
        if any_tree:
            for verb, flag in STAGE_ALL:
                v = verb.search(seg, g.end())
                if v and flag.search(seg, v.end()):
                    return True
            if PUSH.search(seg, g.end()) and forced_push_text(seg[g.start():]):
                return True
    return False

def failed(e):
    """An internal error: every pattern, in any worktree."""
    if matches(cmd, True, True):
        deny("the hook failed (%s: %s) and the command names a guarded git verb, rm, a "
             "stage-everything add/commit or a forced push; the hook fails closed." % (
                 type(e).__name__, e))
    sys.exit(0)

try:
    sys.path.insert(0, os.environ["HOOKS_DIR"])
    import shell_commands as sc
except Exception as e:
    failed(e)

def git(*args):
    return subprocess.run(["git"] + list(args), capture_output=True, text=True)

_cache = {}
NEW_REPOS = []  # directories `git init`/`git clone` creates earlier in the text
def repo_of(path):
    """(toplevel, git_dir, common_dir) of the worktree holding path, or None."""
    real = os.path.realpath(path)
    if any(real == d or real.startswith(d + "/") for d in NEW_REPOS):
        return None
    p = path
    while p and not os.path.isdir(p):
        p = os.path.dirname(p)
    if not p:
        return None
    if p not in _cache:
        r = git("-C", p, "rev-parse", "--path-format=absolute", "--show-toplevel", "--git-dir",
                "--git-common-dir")
        lines = r.stdout.split("\n")
        _cache[p] = (tuple(os.path.realpath(x) for x in lines[:3])
                     if r.returncode == 0 and len(lines) >= 3 and all(lines[:3]) else None)
    return _cache[p]

def repo_of_gitdir(gitdir):
    r = git("--git-dir=" + gitdir, "rev-parse", "--path-format=absolute", "--git-dir",
            "--git-common-dir")
    lines = r.stdout.split("\n")
    if r.returncode or len(lines) < 2 or not all(lines[:2]):
        return None
    gd, common = (os.path.realpath(x) for x in lines[:2])
    return (os.path.dirname(common), gd, common)

project = None  # set by run()
def ours(info):
    return bool(info and project and info[2] == project[2])
def main_tree(info):
    return ours(info) and info[1] == info[2]

_wt = []
def worktrees():
    """Registered worktree roots of this repository, the main one first."""
    if not _wt and project:
        out = git("-C", project[0], "worktree", "list", "--porcelain").stdout
        _wt.extend(os.path.realpath(l[9:]) for l in out.split("\n") if l.startswith("worktree "))
    return _wt

def main_info():
    """The main worktree, for a command whose directory or repository the text lost."""
    return (worktrees()[0], project[2], project[2]) if project and worktrees() else None

SHARED = ("The main worktree is shared with this session's agents (contract §9): REPORT `git status`, "
          "do not make it clean; restore a mutation from your `cp` copy. To reset, stash or discard, "
          "work in your own worktree (`git worktree add <scratchpad>/wt <ref>`), where it is allowed.")
AGENT = ("Agents never run contract §9's checkout/restore/clean/stash/reset in the shared tree "
         "(`git switch` to move branches is allowed); work in your own worktree "
         "(`git worktree add <scratchpad>/wt <ref>`) if you need one.")
S9 = {"checkout", "restore", "clean", "stash", "reset"}
GIT_OPTS_WITH_ARG = {"-C", "-c", "--git-dir", "--work-tree", "--namespace", "--config-env",
                     "--exec-path", "--super-prefix"}
FORCE_LONG = {"--force", "--discard-changes", "--merge", "--ours", "--theirs", "--patch"}
CO_NAME_OPTS = {"-b", "-B", "--orphan", "-c", "-C", "--create", "--force-create"}
# Each judged subcommand's long options (git 2.43 docs), for git's unique-prefix abbreviation;
# every option's --no- form counts too. An ambiguous prefix is left as typed: git refuses it.
LONG_OPTS = {k: set(v.split()) for k, v in {
    "push": "verbose quiet repo all branches mirror delete tags dry-run porcelain force "
            "force-with-lease force-if-includes recurse-submodules receive-pack exec set-upstream "
            "progress prune verify follow-tags signed atomic push-option ipv4 ipv6 thin",
    "checkout": "quiet progress force merge conflict detach track guess orphan "
                "ignore-skip-worktree-bits ignore-other-worktrees ours theirs patch overlay "
                "recurse-submodules pathspec-from-file pathspec-file-nul",
    "switch": "create force-create detach guess discard-changes force merge conflict quiet "
              "progress track orphan ignore-other-worktrees recurse-submodules",
    "restore": "source patch worktree staged quiet progress ours theirs merge conflict "
               "ignore-unmerged ignore-skip-worktree-bits recurse-submodules overlay "
               "pathspec-from-file pathspec-file-nul",
    "reset": "quiet refresh soft mixed hard merge keep recurse-submodules patch intent-to-add "
             "pathspec-from-file pathspec-file-nul",
    "clean": "quiet dry-run force interactive exclude",
    "add": "dry-run verbose force interactive patch edit all ignore-removal update sparse "
           "intent-to-add refresh ignore-errors ignore-missing chmod pathspec-from-file "
           "pathspec-file-nul renormalize",
    "commit": "all patch reuse-message reedit-message fixup squash reset-author short branch "
              "porcelain long null file author date message template signoff trailer verify "
              "allow-empty allow-empty-message cleanup edit amend post-rewrite include only "
              "pathspec-from-file pathspec-file-nul untracked-files verbose quiet dry-run status "
              "gpg-sign",
}.items()}
for _v in LONG_OPTS.values():
    _v |= {"no-" + o for o in list(_v)}

def canon(verb, rest):
    """rest with each `--prefix[=v]` (before `--`) spelled as the long option it abbreviates."""
    names, out = LONG_OPTS.get(verb), []
    for k, a in enumerate(rest):
        if a == "--":
            return out + rest[k:]
        if names and a.startswith("--") and len(a) > 2:
            name, eq, val = a[2:].partition("=")
            if name not in names:
                hits = [o for o in names if o.startswith(name)]
                if len(hits) == 1:
                    a = "--" + hits[0] + eq + val
        out.append(a)
    return out

def is_commit(arg, where):
    if git("-C", where, "rev-parse", "--verify", "--quiet", arg + "^{commit}").returncode == 0:
        return True
    r = git("-C", where, "for-each-ref", "--count=1", "--format=%(refname)", "refs/remotes/*/" + arg)
    return bool(r.stdout.strip())

def positionals(rest, name_opts=()):
    pos, j = [], 0
    while j < len(rest):
        a = rest[j]
        if a == "--":
            break
        if a in name_opts:
            j += 2
            continue
        if not a.startswith("-"):  # `-` alone is the previous branch: an option here
            pos.append(a)
        j += 1
    return pos

def destroys(verb, rest, where):
    """Why this verb can destroy uncommitted work in the main worktree, or None."""
    opts = rest[:rest.index("--")] if "--" in rest else rest  # after `--` come paths only
    shorts = "".join(a[1:] for a in opts if a.startswith("-") and not a.startswith("--"))
    if verb == "restore":
        staged = "--staged" in opts or "S" in shorts
        worktree = "--worktree" in opts or "W" in shorts
        return None if staged and not worktree else "`git restore` discards uncommitted changes"
    if verb == "clean":
        return None if "--dry-run" in opts or "n" in shorts else "`git clean` deletes untracked files"
    if verb == "stash":
        if rest and rest[0] in ("list", "show"):
            return None
        return "`git stash` moves uncommitted changes (a peer's included) off the tree"
    if verb == "reset":
        if {"--hard", "--merge", "--keep"} & set(opts):
            return "`git reset --hard|--merge|--keep` discards uncommitted changes"
        if "--soft" in opts:
            return None
        if "--" in rest and rest.index("--") < len(rest) - 1:
            return None  # a path reset: only the index moves
        if any(a.startswith("--pathspec-from-file") for a in opts):
            return None
        if any(not is_commit(a, where) for a in positionals(rest)):
            return None
        return "`git reset` without paths moves the branch and unstages every file"
    # checkout / switch
    for a in rest:
        if a == "--":
            break
        if a.split("=", 1)[0] in FORCE_LONG or a.startswith("--pathspec-from-file"):
            return "`git %s %s` overwrites uncommitted changes" % (verb, a)
        if a.startswith("-") and not a.startswith("--"):
            for ch in a[1:]:
                if ch in "bBcC":
                    break
                if ch in "fmp":
                    return "`git %s %s` overwrites uncommitted changes" % (verb, a)
    if verb != "checkout":
        return None
    if "--" in rest:
        return "`git checkout -- <path>` overwrites the path's uncommitted changes"
    for a in positionals(rest, CO_NAME_OPTS):
        if a == "." or not is_commit(a, where):
            return "`git checkout %s` names a path: it overwrites that path's uncommitted changes" % a
    return None

def from_env(name, cwd, env, assigns):
    """(given, path) for GIT_DIR / GIT_WORK_TREE: a prefix assignment, else one the text set
    earlier (env holds None for a value it lost)."""
    if name in assigns:
        return True, sc.resolve(assigns[name], cwd, env)
    if name in env and env[name] != "":
        return True, None if env[name] is None else sc.resolve(env[name], cwd, env)
    return False, None

INIT_OPTS = ("-b", "--initial-branch", "--separate-git-dir", "--template", "--object-format",
             "--ref-format")
CLONE_OPTS = ("-o", "--origin", "-b", "--branch", "-u", "--upload-pack", "--reference",
              "--reference-if-able", "--separate-git-dir", "--depth", "--shallow-since",
              "--shallow-exclude", "-c", "--config", "-j", "--jobs", "--template",
              "--server-option", "--filter", "--bundle-uri", "--ref-format")

def new_repo(verb, rest, where, env):
    """Record the directory `git init [dir]` / `git clone <url> [dir]` makes a new repository:
    only one that does not exist yet, or is empty (an existing tree stays this repository's)."""
    pos = positionals(rest, INIT_OPTS if verb == "init" else CLONE_OPTS)
    if verb == "init":
        target = sc.resolve(pos[0], where, env) if pos else where
    elif len(pos) >= 2:
        target = sc.resolve(pos[1], where, env)
    elif pos:
        name = re.sub(r"(/\.git|\.git)?/*$", "", pos[0]).rstrip("/")
        name = re.split(r"[/:]", name)[-1] if name else ""
        target = sc.resolve(name, where, env) if name else None
    else:
        target = None
    if target and (not os.path.lexists(target) or (
            os.path.isdir(target) and not os.path.islink(target) and not os.listdir(target))):
        NEW_REPOS.append(os.path.realpath(target))

def check_git(argv, cwd, env, assigns):
    args, i, where = argv[1:], 0, cwd
    gd_given, gitdir = from_env("GIT_DIR", cwd, env, assigns)
    wt_given, worktree = from_env("GIT_WORK_TREE", cwd, env, assigns)
    while i < len(args) and args[i].startswith("-"):
        a = args[i]
        if a in GIT_OPTS_WITH_ARG and i + 1 < len(args):
            v = args[i + 1]
            if a == "-C":
                where = sc.resolve(v, where, env)
            elif a == "--work-tree":
                wt_given, worktree = True, sc.resolve(v, where, env)
            elif a == "--git-dir":
                gd_given, gitdir = True, sc.resolve(v, where, env)
            i += 2
            continue
        if a.startswith("--work-tree="):
            wt_given, worktree = True, sc.resolve(a.split("=", 1)[1], where, env)
        elif a.startswith("--git-dir="):
            gd_given, gitdir = True, sc.resolve(a.split("=", 1)[1], where, env)
        i += 1
    if i >= len(args):
        return
    verb, rest = args[i], canon(args[i], args[i + 1:])
    if verb in ("init", "clone"):
        new_repo(verb, rest, worktree or where, env)
        return
    if (gd_given and gitdir is None) or (wt_given and worktree is None) or where is None:
        # The text lost the directory or the repository (cd "$X", GIT_DIR="$X", a pop after
        # pushd -n): fail closed, judge it as the main worktree.
        info = main_info()
    elif gitdir:
        info = repo_of_gitdir(gitdir)
    else:
        info = repo_of(worktree or where)
    if verb in S9 or verb == "switch":
        if main_tree(info):
            if agent and verb in S9:
                if not (verb == "stash" and rest and rest[0] in ("list", "show")):
                    deny("`git %s` by a subagent in the main worktree %s. %s" % (verb, info[0], AGENT))
            else:
                why = destroys(verb, rest, info[0])
                if why:
                    deny("%s in the main worktree %s. %s" % (why, info[0], SHARED))
    elif verb == "add" and ours(info):
        for a in rest:
            if a == "--":
                break
            if a in ("-A", "--all", "-u", "--update", "--no-ignore-removal") or (
                    a.startswith("-") and not a.startswith("--") and set(a[1:]) & set("Au")):
                deny("`git add %s` stages every change, a peer's hunks included (contract §9). "
                     "Stage explicit per-file pathspecs (`git add path/to/file`) and check "
                     "`git diff --cached --stat`." % a)
        if any(p in (".", "./", ":/", ":", "*") for p in rest):
            deny("`git add .` stages every change, a peer's hunks included (contract §9). "
                 "Stage explicit per-file pathspecs and check `git diff --cached --stat`.")
    elif verb == "commit" and ours(info):
        j = 0
        while j < len(rest):
            a = rest[j]
            if a == "--":
                break
            if a == "--all":
                deny("`git commit --all` stages every tracked change (contract §9). Stage per-file, then commit.")
            if a in ("--message", "--file", "--author", "--date", "--template", "--reuse-message",
                     "--reedit-message", "--fixup", "--squash", "--trailer", "--cleanup"):
                j += 2
                continue
            if a.startswith("-") and not a.startswith("--"):
                for k, ch in enumerate(a[1:]):
                    if ch == "a":
                        deny("`git commit -a` stages every tracked change (contract §9). Stage per-file, then commit.")
                    if ch in "mFcCt":
                        if k == len(a) - 2:
                            j += 1
                        break
            j += 1
    elif verb == "push" and ours(info):
        check_push(rest, where)

def check_push(rest, where):
    flags, pos, j = set(), [], 0
    while j < len(rest):
        a = rest[j]
        if a == "--":
            pos += rest[j + 1:]
            break
        if a.startswith("--"):
            flags.add(a.split("=", 1)[0])
            if a in ("--repo", "--receive-pack", "--exec", "--push-option"):
                j += 1
        elif a.startswith("-") and len(a) > 1:
            for ch in a[1:]:
                flags.add("-" + ch)
            if a[-1] == "o":
                j += 1
        else:
            pos.append(a)
        j += 1
    refspecs = pos[1:]
    forced = bool(flags & {"--force", "-f", "--force-with-lease", "--mirror"}) or any(
        r.startswith("+") for r in refspecs)
    if not forced:
        return
    rule = ("Force-push only with both `--force-with-lease` and `--force-if-includes`, only to "
            "`claude/*` branches, never dev or mainline (CLAUDE.md › Git & Release).")
    if flags & {"--mirror", "--all", "--branches", "--tags"}:
        deny("a forced `git push` of every ref. " + rule)
    if "--force" in flags or "-f" in flags or not {"--force-with-lease", "--force-if-includes"} <= flags:
        deny("a forced `git push` without both lease flags. " + rule)
    dsts = []
    for r in refspecs or ["HEAD"]:
        r = r.lstrip("+")
        dst = r.split(":", 1)[1] if ":" in r else r
        if dst == "HEAD":
            dst = (git("-C", where, "symbolic-ref", "--short", "-q", "HEAD").stdout.strip()
                   if where else "") or "HEAD"  # an unknown directory: HEAD is not claude/*
        dsts.append(dst[len("refs/heads/"):] if dst.startswith("refs/heads/") else dst)
    for d in dsts:
        if not d.startswith("claude/"):
            deny("a forced `git push` to `%s`. %s" % (d, rule))

def check_rm(argv, cwd, env):
    targets, opts = [], True
    for a in argv[1:]:
        if opts and a == "--":
            opts = False
            continue
        if opts and a.startswith("-") and a != "-":
            continue
        targets.append(a)
    wts = worktrees()
    top = wts[0] if wts else None
    for t in targets:
        path = sc.resolve(t, cwd, env)
        if path is None:
            continue  # cannot resolve: "$tmp", a loop variable, an unknown cwd
        if top:
            for c in [path] + (glob.glob(path) if glob.has_magic(path) else []):
                # without a trailing `/`, rm removes the name itself (a symlink, not its target)
                rp = os.path.realpath(c) if t.endswith("/") else os.path.join(
                    os.path.realpath(os.path.dirname(c)), os.path.basename(c))
                if rp == top or top.startswith(rp.rstrip("/") + "/"):
                    deny("`rm %s` removes the main worktree %s%s. Contract §9." % (
                        t, top, "" if rp == top else " (it is below " + rp + ")"))
        parent = os.path.dirname(path)
        info = repo_of(parent)
        if not main_tree(info):
            continue
        rel = os.path.relpath(os.path.join(os.path.realpath(parent), os.path.basename(path)), info[0])
        parts = rel.split(os.sep)
        if parts[:2] == [".claude", "tmp"] and len(parts) > 2:
            continue
        if parts[:2] == [".claude", "worktrees"] and len(parts) > 2 and os.path.realpath(
                os.path.join(info[0], ".claude", "worktrees", parts[2])) not in wts:
            continue  # a stale directory, no longer a registered worktree
        if any(p in ("bin", "obj") and glob.glob(os.path.join(info[0], *parts[:k], "*.*proj"))
               for k, p in enumerate(parts)):
            continue
        deny("`rm %s` inside the shared main worktree %s. Restore a mutation from the copy you made "
             "(`cp`); put scratch files in your scratchpad or .claude/tmp/ (bin/ and obj/ build "
             "outputs may be removed; a registered worktree goes with `git worktree remove`). "
             "Contract §9." % (t, info[0]))

def visit(argv, cwd, env, assigns):
    name = os.path.basename(argv[0])
    if name == "git":
        check_git(argv, cwd, env, assigns)
    elif name == "rm":
        check_rm(argv, cwd, env)

def run():
    global project
    project = repo_of(os.environ.get("CLAUDE_PROJECT_DIR") or cwd)
    opaque = []
    try:
        sc.walk(cmd, cwd, visit, opaque=lambda where, what: opaque.append((where, what)))
    except sc.ParseError:
        if not sc.bash_accepts(cmd):
            sys.exit(0)  # bash refuses it too: nothing runs
        here = repo_of(cwd)
        if matches(cmd, main_tree(here), ours(here)):
            deny("this command could not be parsed (bash can parse it) and names a guarded git "
                 "verb, rm, a stage-everything add/commit or a forced push. Split it into simpler "
                 "commands.")
    for where, what in opaque:
        info = repo_of(where) if where else main_info()
        if matches(cmd, main_tree(info), ours(info)):
            deny("this command runs a script the hook cannot read (%s) in %s, and its text names "
                 "a guarded git verb, rm, a stage-everything add/commit or a forced push. Run the "
                 "commands directly." % (what, where or "an unknown directory"))

try:
    run()
except Exception as e:
    failed(e)
sys.exit(0)
PY
HOOKS_DIR="$(cd "$(dirname "$0")" && pwd)" python3 -c "$PROG"
rc=$?
case $rc in
  0 | 2) exit "$rc" ;;
esac
echo "BLOCKED by .claude/hooks/guard-shared-tree.sh: the hook could not judge this command" \
  "(python3 exit $rc); it fails closed." >&2
exit 2
