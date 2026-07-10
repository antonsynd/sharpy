# CPython oracle yield report — `test_heapq`

- CPython source: Lib/test/test_heapq.py
- CPython version pin: 3.12
- Test methods: 26
- Portable: 15 (57.7%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 15 | 57.7% |
| NEEDS-REWRITE | 11 | 42.3% |
| DIVERGENT | 0 | 0.0% |
| DYNAMIC | 0 | 0.0% |
| IMPL-DETAIL | 0 | 0.0% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `TestModules.test_py_functions` | NEEDS-REWRITE | `reflection` |
| `TestModules.test_c_functions` | NEEDS-REWRITE | `reflection` |
| `TestHeap.test_push_pop` | PORTABLE | — |
| `TestHeap.test_heapify` | PORTABLE | — |
| `TestHeap.test_naive_nbest` | PORTABLE | — |
| `TestHeap.test_nbest` | PORTABLE | — |
| `TestHeap.test_nbest_with_pushpop` | PORTABLE | — |
| `TestHeap.test_heappushpop` | PORTABLE | — |
| `TestHeap.test_heappop_max` | PORTABLE | — |
| `TestHeap.test_heapsort` | PORTABLE | _generated-loop_ |
| `TestHeap.test_merge` | PORTABLE | _generated-loop-unroll_ |
| `TestHeap.test_empty_merges` | PORTABLE | — |
| `TestHeap.test_merge_does_not_suppress_index_error` | PORTABLE | — |
| `TestHeap.test_merge_stability` | NEEDS-REWRITE | `local-class` |
| `TestHeap.test_nsmallest` | PORTABLE | _generated-loop-unroll_ |
| `TestHeap.test_nlargest` | PORTABLE | _generated-loop-unroll_ |
| `TestHeap.test_comparison_operator` | NEEDS-REWRITE | `local-class` |
| `TestErrorHandling.test_non_sequence` | PORTABLE | _generated-loop-unroll_ |
| `TestErrorHandling.test_len_only` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_cmp_err` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_arg_parsing` | PORTABLE | _generated-loop-unroll_ |
| `TestErrorHandling.test_iterable_args` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_heappush_mutating_heap` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_heappop_mutating_heap` | NEEDS-REWRITE | `dunder-fixture` |
| `TestErrorHandling.test_comparison_operator_modifiying_heap` | NEEDS-REWRITE | `local-class` |
| `TestErrorHandling.test_comparison_operator_modifiying_heap_two_heaps` | NEEDS-REWRITE | `local-class` |
