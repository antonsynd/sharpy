# Comment templates — one file per issue under `<scratchpad>/comments/<issue>.md`, posted with `gh issue comment N --body-file`

Header (every comment):

```
**Batching <YYYY-MM-DD> @ <sha>** — charter `.claude/plans/remediation-round-<date>-batching.md` §<3.x>, **P<n> — <group name>** (order <k>[, one commit, no plan]).
```

## Placement comment (batched issue, no ruling)

```
<header>

<Contract in one sentence.> <Seam file:line or grep fact.> <Cure: one function / one carrier / one matrix; which existing matrix test it extends.> <Cells: axes × values.> <Allowlist rows / KnownRed / N/A cells that drain as acceptance.>
```

## Ruling comment (the decision's issue)

```
<header> **Ruling R-<xx> (owner, <date>): <chosen option, verbatim label>.**

<Internals in ≤3 lines: what the code does today, file:line.> <Mechanism of the chosen option.> <Guard: totality/matrix/mutation.> Rejected: <alternative> (<why; which direction is one-way>); <alternative> (<why>). [Sibling filed: #N.]
```

## Fold-in comment (the home issue absorbing another issue's cell)

```
<header> **#<other> cell <k> folded in:** <the cell, its measured verdict @ sha, why it belongs to this class>.
```

## Closure comments (`gh issue close N --reason <r> --comment "…"`)

- Duplicate (`--reason "not planned"`): `**Closed as a duplicate of #<home>** (batching <date> @ <sha>): identical cell (<one line>). The ruling (<R-xx>) and the placement (<group>) are recorded on #<home>; <what carried over>.`
- Fold (`--reason "not planned"`): `**Folded into #<home>** (batching <date> @ <sha>): <which cell of which class>. Root cause recorded there: <seam>. Placement: <group> (charter §<3.x>).`
- Multi-cell split (`--reason "not planned"`): `**Folded into #<a> and #<b>**: cell 1 (<…>) is posted on #<a> under <ruling>; cell 2 (<…>) on #<b>. Closed so the class has one issue per cell.`
- Resolved design note (`--reason completed`): `**Closed as resolved** (batching <date> @ <sha>): <where the ruling and the verified behaviour are recorded>. <Optional cleanup> is not required; file it when wanted. Adjacent open cell: #<n> (<group>).`

## Tracking-issue body (filed by a ruling)

```
## Class
<contract; measured cells with @ sha; the seam>

## Ruling R-<xx> (owner, <date>)
<chosen mechanism>. Rejected: <alternatives>.

## Acceptance
- <guard / matrix / mutation record>
- <allowlist or direction record>

Charter: `.claude/plans/remediation-round-<date>-batching.md` §<3.x> — <group>[ phase <k>]. Sibling: #<n>.
```
