# CPython oracle yield report — `test_bisect`

- CPython source: Lib/test/test_bisect.py
- CPython version pin: 3.12
- Test methods: 23
- Portable: 16 (69.6%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 16 | 69.6% |
| NEEDS-REWRITE | 7 | 30.4% |
| DIVERGENT | 0 | 0.0% |
| DYNAMIC | 0 | 0.0% |
| IMPL-DETAIL | 0 | 0.0% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `TestBisect.test_precomputed` | PORTABLE | _generated-loop_ |
| `TestBisect.test_negative_lo` | PORTABLE | — |
| `TestBisect.test_large_range` | PORTABLE | — |
| `TestBisect.test_large_pyrange` | NEEDS-REWRITE | `dunder-fixture` |
| `TestBisect.test_random` | PORTABLE | _generated-loop_ |
| `TestBisect.test_optionalSlicing` | PORTABLE | _generated-loop_ |
| `TestBisect.test_backcompatibility` | PORTABLE | — |
| `TestBisect.test_keyword_args` | PORTABLE | — |
| `TestBisect.test_lookups_with_key_function` | PORTABLE | _generated-loop_ |
| `TestBisect.test_insort` | PORTABLE | _generated-loop_ |
| `TestBisect.test_insort_keynotNone` | PORTABLE | _generated-loop-unroll_ |
| `TestBisect.test_lt_returns_non_bool` | NEEDS-REWRITE | `local-class` |
| `TestBisect.test_lt_returns_notimplemented` | NEEDS-REWRITE | `local-class` |
| `TestInsort.test_vsBuiltinSort` | PORTABLE | _generated-loop-unroll_ |
| `TestInsort.test_backcompatibility` | PORTABLE | — |
| `TestInsort.test_listDerived` | NEEDS-REWRITE | `local-class` |
| `TestErrorHandling.test_non_sequence` | PORTABLE | _generated-loop-unroll_ |
| `TestErrorHandling.test_len_only` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_get_only` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_cmp_err` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_arg_parsing` | PORTABLE | _generated-loop-unroll_ |
| `TestDocExample.test_grades` | PORTABLE | — |
| `TestDocExample.test_colors` | PORTABLE | — |
