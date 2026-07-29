# CPython oracle yield report — `test_list`

- CPython source: Lib/test/test_list.py
- CPython version pin: 3.12
- Test methods: 18
- Portable: 11 (61.1%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 11 | 61.1% |
| NEEDS-REWRITE | 6 | 33.3% |
| DIVERGENT | 0 | 0.0% |
| DYNAMIC | 0 | 0.0% |
| IMPL-DETAIL | 1 | 5.6% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `ListTest.test_basic` | PORTABLE | — |
| `ListTest.test_keyword_args` | PORTABLE | — |
| `ListTest.test_keywords_in_subclass` | NEEDS-REWRITE | `local-class` |
| `ListTest.test_truth` | PORTABLE | — |
| `ListTest.test_identity` | PORTABLE | — |
| `ListTest.test_len` | PORTABLE | — |
| `ListTest.test_overflow` | PORTABLE | — |
| `ListTest.test_list_resize_overflow` | PORTABLE | — |
| `ListTest.test_repr_large` | PORTABLE | — |
| `ListTest.test_iterator_pickle` | PORTABLE | _generated-loop_ |
| `ListTest.test_reversed_pickle` | PORTABLE | _generated-loop_ |
| `ListTest.test_step_overflow` | PORTABLE | — |
| `ListTest.test_no_comdat_folding` | NEEDS-REWRITE | `local-class` |
| `ListTest.test_equal_operator_modifying_operand` | NEEDS-REWRITE | `local-class` |
| `ListTest.test_lt_operator_modifying_operand` | NEEDS-REWRITE | `local-class` |
| `ListTest.test_list_index_modifing_operand` | NEEDS-REWRITE | `local-class` |
| `ListTest.test_preallocation` | IMPL-DETAIL | `impl-detail-decorator`; `impl-detail-sys` |
| `ListTest.test_count_index_remove_crashes` | NEEDS-REWRITE | `local-class` |
