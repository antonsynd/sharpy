---
name: dogfood-analyze
description: Analyze dogfood results and classify failures by root cause
argument-hint: "[directory_name]"
---

Analyze dogfood results in `dogfood_output/` and classify each failure into root cause categories (C1-C5).

Use the `dogfood-analyst` agent via the Task tool to perform the analysis:

```
Task tool:
  subagent_type: dogfood-analyst
  prompt: (see below)
```

If `$ARGUMENTS` is empty (no specific directory given), analyze all results:

```
Analyze all dogfood results in dogfood_output/. Read SUMMARY.md and runs.json first, then investigate each issue and skip directory. Classify every failure using categories C1-C5 and produce a structured report.
```

If `$ARGUMENTS` specifies a directory name, analyze only that item:

```
Analyze a single dogfood result. Look for the directory matching "$ARGUMENTS" under dogfood_output/issues/ or dogfood_output/skips/. Read its metadata.json, error.txt or skip_reason.txt, and source files. Classify using categories C1-C5 and produce a focused report for this single item.
```

Present the agent's report directly to the user.
