# CPython oracle yield report — `test_dict`

- CPython source: Lib/test/test_dict.py
- CPython version pin: 3.12
- Test methods: 84
- Portable: 36 (42.9%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 36 | 42.9% |
| NEEDS-REWRITE | 30 | 35.7% |
| DIVERGENT | 0 | 0.0% |
| DYNAMIC | 2 | 2.4% |
| IMPL-DETAIL | 16 | 19.0% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `DictTest.test_invalid_keyword_arguments` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_constructor` | PORTABLE | — |
| `DictTest.test_literal_constructor` | DYNAMIC | `exec-eval` |
| `DictTest.test_merge_operator` | PORTABLE | — |
| `DictTest.test_bool` | PORTABLE | — |
| `DictTest.test_keys` | PORTABLE | — |
| `DictTest.test_values` | PORTABLE | — |
| `DictTest.test_items` | PORTABLE | — |
| `DictTest.test_views_mapping` | NEEDS-REWRITE | `local-class`; `builtin-subclass` |
| `DictTest.test_contains` | PORTABLE | — |
| `DictTest.test_len` | PORTABLE | — |
| `DictTest.test_getitem` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_clear` | PORTABLE | — |
| `DictTest.test_update` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_fromkeys` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_copy` | PORTABLE | — |
| `DictTest.test_copy_fuzz` | PORTABLE | _generated-loop-unroll_ |
| `DictTest.test_copy_maintains_tracking` | IMPL-DETAIL | `local-class`; `impl-detail-gc` |
| `DictTest.test_copy_noncompact` | PORTABLE | — |
| `DictTest.test_get` | PORTABLE | — |
| `DictTest.test_setdefault` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_setdefault_atomic` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_setitem_atomic_at_resize` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_popitem` | PORTABLE | _generated-loop-unroll_; _generated-loop_ |
| `DictTest.test_pop` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_mutating_iteration` | PORTABLE | — |
| `DictTest.test_mutating_iteration_delete` | PORTABLE | — |
| `DictTest.test_mutating_iteration_delete_over_values` | PORTABLE | — |
| `DictTest.test_mutating_iteration_delete_over_items` | PORTABLE | — |
| `DictTest.test_mutating_lookup` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_repr` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_repr_deep` | PORTABLE | — |
| `DictTest.test_eq` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_keys_contained` | PORTABLE | — |
| `DictTest.test_errors_in_view_containment_check` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_dictview_set_operations_on_keys` | PORTABLE | — |
| `DictTest.test_dictview_set_operations_on_items` | PORTABLE | — |
| `DictTest.test_items_symmetric_difference` | PORTABLE | _generated-loop_ |
| `DictTest.test_dictview_mixed_set_operations` | NEEDS-REWRITE | `heterogeneous-literal` |
| `DictTest.test_missing` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_tuple_keyerror` | PORTABLE | — |
| `DictTest.test_bad_key` | DYNAMIC | `local-class`; `exec-eval`; `dynamic-namespace` |
| `DictTest.test_resize1` | PORTABLE | — |
| `DictTest.test_resize2` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_empty_presized_dict_in_freelist` | PORTABLE | — |
| `DictTest.test_container_iterator` | IMPL-DETAIL | `local-class`; `impl-detail-gc` |
| `DictTest.test_string_keys_can_track_values` | PORTABLE | — |
| `DictTest.test_track_literals` | IMPL-DETAIL | `impl-detail-decorator` |
| `DictTest.test_track_dynamic` | IMPL-DETAIL | `impl-detail-decorator`; `local-class` |
| `DictTest.test_track_subtypes` | IMPL-DETAIL | `impl-detail-decorator`; `local-class` |
| `DictTest.test_splittable_setdefault` | IMPL-DETAIL | `impl-detail-decorator`; `impl-detail-sys` |
| `DictTest.test_splittable_del` | IMPL-DETAIL | `impl-detail-decorator`; `impl-detail-sys` |
| `DictTest.test_splittable_pop` | IMPL-DETAIL | `impl-detail-decorator` |
| `DictTest.test_splittable_pop_pending` | IMPL-DETAIL | `impl-detail-decorator` |
| `DictTest.test_splittable_popitem` | IMPL-DETAIL | `impl-detail-decorator`; `impl-detail-sys` |
| `DictTest.test_splittable_update` | IMPL-DETAIL | `impl-detail-decorator`; `local-class` |
| `DictTest.test_splittable_to_generic_combinedtable` | IMPL-DETAIL | `impl-detail-decorator`; `local-class`; `impl-detail-sys`; `heterogeneous-literal` |
| `DictTest.test_iterator_pickling` | PORTABLE | _generated-loop_ |
| `DictTest.test_itemiterator_pickling` | PORTABLE | _generated-loop_ |
| `DictTest.test_valuesiterator_pickling` | PORTABLE | _generated-loop_ |
| `DictTest.test_reverseiterator_pickling` | PORTABLE | _generated-loop_ |
| `DictTest.test_reverseitemiterator_pickling` | PORTABLE | _generated-loop_ |
| `DictTest.test_reversevaluesiterator_pickling` | PORTABLE | _generated-loop_ |
| `DictTest.test_instance_dict_getattr_str_subclass` | NEEDS-REWRITE | `local-class`; `reflection` |
| `DictTest.test_object_set_item_single_instance_non_str_key` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_reentrant_insertion` | PORTABLE | — |
| `DictTest.test_merge_and_mutate` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_free_after_iterating` | NEEDS-REWRITE | `test-support` |
| `DictTest.test_equal_operator_modifying_operand` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_fromkeys_operator_modifying_dict_operand` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_fromkeys_operator_modifying_set_operand` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_dictitems_contains_use_after_free` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_dict_contain_use_after_free` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_init_use_after_free` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_oob_indexing_dictiter_iternextitem` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_reversed` | PORTABLE | — |
| `DictTest.test_reverse_iterator_for_empty_dict` | PORTABLE | — |
| `DictTest.test_reverse_iterator_for_shared_shared_dicts` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_dict_copy_order` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_dict_items_result_gc` | IMPL-DETAIL | `impl-detail-decorator`; `impl-detail-gc` |
| `DictTest.test_dict_items_result_gc_reversed` | IMPL-DETAIL | `impl-detail-decorator`; `impl-detail-gc` |
| `DictTest.test_store_evilattr` | NEEDS-REWRITE | `local-class` |
| `DictTest.test_str_nonstr` | IMPL-DETAIL | `local-class`; `impl-detail-support` |
| `CAPITest.test_getitem_knownhash` | IMPL-DETAIL | `impl-detail-decorator`; `test-support`; `local-class` |
