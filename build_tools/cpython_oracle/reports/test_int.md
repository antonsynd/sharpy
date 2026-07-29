# CPython oracle yield report — `test_int`

- CPython source: Lib/test/test_int.py
- CPython version pin: 3.12
- Test methods: 35
- Portable: 15 (42.9%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 15 | 42.9% |
| NEEDS-REWRITE | 9 | 25.7% |
| DIVERGENT | 7 | 20.0% |
| DYNAMIC | 1 | 2.9% |
| IMPL-DETAIL | 3 | 8.6% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `IntTestCases.test_basic` | PORTABLE | _generated-loop_; _generated-loop-unroll_ |
| `IntTestCases.test_invalid_signs` | PORTABLE | — |
| `IntTestCases.test_unicode` | DIVERGENT | `huge-int` (int-overflow-checked) |
| `IntTestCases.test_underscores` | DYNAMIC | `exec-eval` |
| `IntTestCases.test_small_ints` | IMPL-DETAIL | `impl-detail-decorator` |
| `IntTestCases.test_no_args` | PORTABLE | — |
| `IntTestCases.test_keyword_args` | PORTABLE | — |
| `IntTestCases.test_int_base_limits` | DIVERGENT | `huge-int` (int-overflow-checked) |
| `IntTestCases.test_int_base_bad_types` | PORTABLE | — |
| `IntTestCases.test_int_base_indexable` | DIVERGENT | `local-class`; `huge-int` (int-overflow-checked) |
| `IntTestCases.test_non_numeric_input_types` | NEEDS-REWRITE | `local-class` |
| `IntTestCases.test_int_memoryview` | PORTABLE | — |
| `IntTestCases.test_string_float` | PORTABLE | — |
| `IntTestCases.test_intconversion` | NEEDS-REWRITE | `local-class` |
| `IntTestCases.test_int_subclass_with_index` | NEEDS-REWRITE | `local-class` |
| `IntTestCases.test_int_subclass_with_int` | NEEDS-REWRITE | `local-class` |
| `IntTestCases.test_int_returns_int_subclass` | NEEDS-REWRITE | `local-class`; `builtin-subclass` |
| `IntTestCases.test_error_message` | PORTABLE | — |
| `IntTestCases.test_issue31619` | PORTABLE | — |
| `IntStrDigitLimitsTests.test_disabled_limit` | NEEDS-REWRITE | `test-support` |
| `IntStrDigitLimitsTests.test_max_str_digits_edge_cases` | PORTABLE | — |
| `IntStrDigitLimitsTests.test_max_str_digits` | PORTABLE | — |
| `IntStrDigitLimitsTests.test_denial_of_service_prevented_int_to_str` | NEEDS-REWRITE | `test-support` |
| `IntStrDigitLimitsTests.test_denial_of_service_prevented_str_to_int` | NEEDS-REWRITE | `test-support` |
| `IntStrDigitLimitsTests.test_power_of_two_bases_unlimited` | PORTABLE | _generated-loop-unroll_ |
| `IntStrDigitLimitsTests.test_underscores_ignored` | PORTABLE | — |
| `IntStrDigitLimitsTests.test_sign_not_counted` | PORTABLE | — |
| `IntStrDigitLimitsTests.test_int_from_other_bases` | PORTABLE | — |
| `IntStrDigitLimitsTests.test_int_max_str_digits_is_per_interpreter` | NEEDS-REWRITE | `test-support` |
| `PyLongModuleTests.test_pylong_int_to_decimal` | DIVERGENT | `huge-int` (int-overflow-checked) |
| `PyLongModuleTests.test_pylong_int_to_decimal_2` | DIVERGENT | `huge-int` (int-overflow-checked) |
| `PyLongModuleTests.test_pylong_int_divmod` | DIVERGENT | `huge-int` (int-overflow-checked) |
| `PyLongModuleTests.test_pylong_str_to_int` | DIVERGENT | `huge-int` (int-overflow-checked) |
| `PyLongModuleTests.test_pylong_misbehavior_error_path_to_str` | IMPL-DETAIL | `impl-detail-decorator`; `test-support` |
| `PyLongModuleTests.test_pylong_misbehavior_error_path_from_str` | IMPL-DETAIL | `impl-detail-decorator`; `test-support` |
