# Dogfooding Summary Report

**Generated:** 2026-01-24T19:32:58.090516
**Updated:** 2026-01-24 (5 of 7 skipped cases resolved)

## Overall Statistics

- **Total Iterations:** 20
- ~~Successful: 1 (5.0%)~~ ADDRESSED AND REMOVED
- ~~Failed: 12 (60.0%)~~ ADDRESSED AND REMOVED
- ~~**Skipped:** 7 (35.0%)~~ 5 MIGRATED TO TEST FIXTURES
- **Remaining Skips:** 2 (require feature work)

## Remaining Issues

- **module_imports/complex** - analyzer.spy invalid per spec (multi-file import resolution)
- **module_imports/medium** - Unsupported feature: `list[Shape]` type annotation (v0.1.11)

## Resolved Issues (migrated to test fixtures)

- ✅ class_field_access/simple → `classes/class_person_field_mutation.spy`
- ✅ logical_operators/simple → `control_flow/logical_operators_simple.spy`
- ✅ generic_function/simple → `functions/identity_functions.spy`
- ✅ logical_operators/medium → `classes/logic_gate_class.spy`
- ✅ function_keyword_args/complex → `functions/keyword_args_with_defaults.spy` (bug fix applied)
