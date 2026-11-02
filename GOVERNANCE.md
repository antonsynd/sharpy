# Governance

Sharpy is governed under a **Benevolent Dictator For Life (BDFL)** model. This document explains what that means in practice, so contributors and users know what to expect.

## Model

[@antonsynd](https://github.com/antonsynd), the creator and maintainer, is the sole decision-maker for the project. This applies at least until the 1.0 release. There is no commitment that governance changes at 1.0, or on any particular schedule. If it changes, this document will be updated first.

## What the maintainer decides

The maintainer alone decides the **direction of the language and its ecosystem**, including:

- Language syntax and semantics, and the language specification
- The standard library: what is in it, what it looks like, and what is left out
- Compiler, tooling, and editor-integration design
- Which features are accepted, deferred, or declined
- Release timing, versioning, and deprecations
- Project infrastructure, licensing, and governance itself

There is no steering committee, RFC vote, or consensus process. Discussion is welcome and often useful, but it is input, not a ballot.

## What this does *not* mean

A BDFL model does not mean outside contributions are unwelcome. **Bug fixes are welcome**, and so are:

- Bug reports with minimal reproductions
- Fixes for compiler, runtime, standard library, tooling, and documentation defects
- Test coverage improvements
- Spec clarifications that match the intended behavior

A bug fix does not need prior approval, though opening an issue first for anything non-trivial can save you effort. Being a good bug fix does not guarantee it will be merged: it still has to fit the language spec and the project's conventions (see [CONTRIBUTING.md](CONTRIBUTING.md)).

Changes that alter the language's direction (new syntax, new semantics, new standard library surface, new tooling scope) are the maintainer's call. You may propose them in an issue. They may be declined, even if well made, and the maintainer may not give a detailed rationale.

## Roadmap

There is no published roadmap and no feature schedule. The only standing goals are:

1. **Complete and stable syntax and semantics** for the language.
2. **Mostly bug-free compilation and runtime behavior.**

Everything else is secondary and may happen, change, or not happen at all. Please do not plan around features, dates, or priorities that are not written in the language specification or in a tagged release.

## Forking

You are free to fork Sharpy. If you want a different direction, a faster pace, a larger feature set, or a different governance model, forking is a legitimate and encouraged answer. Sharpy is dual licensed under [Apache-2.0](LICENSE-APACHE) or [MIT](LICENSE-MIT), which permits this. No ill will is intended or taken.

## Changes to this document

The maintainer may change this document at any time. Changes are made through the repository like any other file, so the history is public.
