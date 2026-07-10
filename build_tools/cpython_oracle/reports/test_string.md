# CPython oracle yield report — `test_string`

- CPython source: Lib/test/test_string.py
- CPython version pin: 3.12
- Test methods: 38
- Portable: 21 (55.3%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 21 | 55.3% |
| NEEDS-REWRITE | 17 | 44.7% |
| DIVERGENT | 0 | 0.0% |
| DYNAMIC | 0 | 0.0% |
| IMPL-DETAIL | 0 | 0.0% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `ModuleTest.test_attrs` | PORTABLE | — |
| `ModuleTest.test_capwords` | PORTABLE | — |
| `ModuleTest.test_basic_formatter` | PORTABLE | — |
| `ModuleTest.test_format_keyword_arguments` | PORTABLE | — |
| `ModuleTest.test_auto_numbering` | PORTABLE | — |
| `ModuleTest.test_conversion_specifiers` | PORTABLE | — |
| `ModuleTest.test_name_lookup` | NEEDS-REWRITE | `local-class` |
| `ModuleTest.test_index_lookup` | PORTABLE | — |
| `ModuleTest.test_override_get_value` | NEEDS-REWRITE | `local-class` |
| `ModuleTest.test_override_format_field` | NEEDS-REWRITE | `local-class` |
| `ModuleTest.test_override_convert_field` | NEEDS-REWRITE | `local-class` |
| `ModuleTest.test_override_parse` | NEEDS-REWRITE | `local-class` |
| `ModuleTest.test_check_unused_args` | NEEDS-REWRITE | `local-class` |
| `ModuleTest.test_vformat_recursion_limit` | PORTABLE | — |
| `TestTemplate.test_regular_templates` | PORTABLE | — |
| `TestTemplate.test_regular_templates_with_braces` | PORTABLE | — |
| `TestTemplate.test_regular_templates_with_upper_case` | PORTABLE | — |
| `TestTemplate.test_regular_templates_with_non_letters` | PORTABLE | — |
| `TestTemplate.test_escapes` | PORTABLE | — |
| `TestTemplate.test_percents` | PORTABLE | — |
| `TestTemplate.test_stringification` | PORTABLE | — |
| `TestTemplate.test_tupleargs` | PORTABLE | — |
| `TestTemplate.test_SafeTemplate` | PORTABLE | — |
| `TestTemplate.test_invalid_placeholders` | PORTABLE | — |
| `TestTemplate.test_idpattern_override` | NEEDS-REWRITE | `local-class`; `dunder-fixture` |
| `TestTemplate.test_flags_override` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_idpattern_override_inside_outside` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_idpattern_override_inside_outside_invalid_unbraced` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_pattern_override` | NEEDS-REWRITE | `local-class`; `dunder-fixture` |
| `TestTemplate.test_braced_override` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_braced_override_safe` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_invalid_with_no_lines` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_unicode_values` | PORTABLE | — |
| `TestTemplate.test_keyword_arguments` | PORTABLE | — |
| `TestTemplate.test_keyword_arguments_safe` | PORTABLE | — |
| `TestTemplate.test_delimiter_override` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_is_valid` | NEEDS-REWRITE | `local-class` |
| `TestTemplate.test_get_identifiers` | NEEDS-REWRITE | `local-class` |
