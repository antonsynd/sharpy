"""The oracle child's environment, and the one thing it exists to guarantee.

The differential oracle runs each program as ``python3 -B -S``. ``-S`` skips site-packages,
which is deliberate — a developer's unrelated installs must not be able to change an oracle
verdict. It also means a non-stdlib module the oracle claims to compare (PyYAML) is NOT
importable in the child, so every one of its cells scored a name-missing SKIP while the
fixture counts rose. The widening looked successful and compared nothing.

These pin the remedy: exactly the declared third-party modules are put back, by path, and a
child can actually import them.
"""

from __future__ import annotations

import os

import pytest

from build_tools.differential_exec.run_programs import (
    ORACLE_THIRD_PARTY,
    _child_env,
    _third_party_paths,
    run_one,
)


def test_hermetic_defaults_are_still_set():
    env = _child_env()
    assert env["PYTHONHASHSEED"] == "0"
    assert env["PYTHONDONTWRITEBYTECODE"] == "1"
    assert env["PYTHONIOENCODING"] == "utf-8"


def test_env_does_not_inherit_the_parents_pythonpath():
    # The child's path is CONSTRUCTED, never inherited: whatever the developer has exported
    # must not reach the oracle, or a local install becomes part of the verdict.
    os.environ["PYTHONPATH"] = "/definitely/not/wanted"
    try:
        value = _child_env().get("PYTHONPATH", "")
        assert "/definitely/not/wanted" not in value
    finally:
        del os.environ["PYTHONPATH"]


def test_declared_third_party_modules_resolve_to_real_directories():
    for path in _third_party_paths():
        assert os.path.isdir(path), f"{path} is on the child's PYTHONPATH but is not a directory"


@pytest.mark.skipif("yaml" not in ORACLE_THIRD_PARTY, reason="yaml is not a declared oracle module")
def test_a_child_can_import_the_declared_module():
    # The property the whole change exists for, asserted end to end through an actual child
    # rather than by inspecting the env dict: under -S this failed with ModuleNotFoundError.
    pytest.importorskip("yaml")

    result = run_one("import yaml; print(yaml.safe_load('1e-7'))")

    assert result["ok"], f"child could not run: {result['stderr'][:200]}"
    # PyYAML resolves an exponent without a dot as a STRING (YAML 1.1), which is the very
    # divergence the oracle is meant to see. Asserting the value keeps this from passing on
    # a stub that merely imports.
    assert result["stdout"].strip() == "1e-7"


def test_an_undeclared_module_stays_unimportable_in_the_child():
    # The negative control: the injection is scoped to the declared list, not a door to all of
    # site-packages. `pytest` itself is installed here and must remain invisible to the child.
    assert "pytest" not in ORACLE_THIRD_PARTY

    result = run_one("import pytest; print('reachable')")

    assert not result["ok"]
    assert "ModuleNotFoundError" in result["stderr"] or "ImportError" in result["stderr"]
