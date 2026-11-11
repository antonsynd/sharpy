# Comment templates — one file per issue under `<scratchpad>/comments/<issue>.md`, posted with `gh issue comment N --body-file`

Header (every comment):

```
**Batching <YYYY-MM-DD> @ <sha>** — charter `docs/process/rounds/remediation-round-<date>-batching.md` §<3.x>, **P<n> — <group name>** (order <k>[, one commit, no plan]).
```

## Fold-in comment (the home issue absorbing another issue's cell)

```
<header> **#<other> cell <k> folded in:** <the cell, its measured verdict @ sha, why it belongs to this class>.
```

## Cell comment (a cell posted on its class tracker; the cell issue, if any, closes as folded)

```
<header> **Cell:** <axis values> — <program, ≤5 lines> → observed `<…>` @ <sha>; expected `<…>` (<python3 | spec §>). Lane <lane>, reach <program|probe>. Found by <round / lane / prober>.
```

## Closure comments (`gh issue close N --reason <r> --comment "…"`)

- Duplicate (`--reason "not planned"`): `**Closed as a duplicate of #<home>** (batching <date> @ <sha>): identical cell (<one line>). <What carried over>; placement and ruling: the charter.`
- Fold (`--reason "not planned"`): `**Folded into #<home>** (batching <date> @ <sha>): <which cell of which class>.`
- Multi-cell split (`--reason "not planned"`): `**Folded into #<a> and #<b>**: cell 1 (<…>) is posted on #<a> under <ruling>; cell 2 (<…>) on #<b>. Closed so the class has one issue per cell.`
- Resolved design note (`--reason completed`): `**Closed as resolved** (batching <date> @ <sha>): <where the ruling and the verified behaviour are recorded>. <Optional cleanup> is not required; file it when wanted. Adjacent open cell: #<n> (<group>).`

## Tracking-issue body (filed by a ruling)

```
## Class
<contract; measured cells with @ sha; the seam>

## Ruling R-<xx> (owner, <date>)
<chosen mechanism>. Rejected: <alternatives>.

## Cells
| axis values | program | observed @ sha | expected | lane | reach | found by |
|---|---|---|---|---|---|---|

## Acceptance
- <guard / matrix / mutation record>
- <allowlist or direction record>

Labels: `class-tracker`, `lane:<lane>`, `reach:<reach>`.

Charter: `docs/process/rounds/remediation-round-<date>-batching.md` §<3.x> — <group>[ phase <k>]. Sibling: #<n>.
```
