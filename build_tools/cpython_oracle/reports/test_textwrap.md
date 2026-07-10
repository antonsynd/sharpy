# CPython oracle yield report — `test_textwrap`

- CPython source: Lib/test/test_textwrap.py
- CPython version pin: 3.12
- Test methods: 66
- Portable: 66 (100.0%)

## Category counts

| Category | Count | % |
|---|---:|---:|
| PORTABLE | 66 | 100.0% |
| NEEDS-REWRITE | 0 | 0.0% |
| DIVERGENT | 0 | 0.0% |
| DYNAMIC | 0 | 0.0% |
| IMPL-DETAIL | 0 | 0.0% |

## Per-method classification

| Method | Category | Reasons |
|---|---|---|
| `WrapTestCase.test_simple` | PORTABLE | — |
| `WrapTestCase.test_empty_string` | PORTABLE | — |
| `WrapTestCase.test_empty_string_with_initial_indent` | PORTABLE | — |
| `WrapTestCase.test_whitespace` | PORTABLE | — |
| `WrapTestCase.test_fix_sentence_endings` | PORTABLE | — |
| `WrapTestCase.test_wrap_short` | PORTABLE | — |
| `WrapTestCase.test_wrap_short_1line` | PORTABLE | — |
| `WrapTestCase.test_hyphenated` | PORTABLE | — |
| `WrapTestCase.test_hyphenated_numbers` | PORTABLE | — |
| `WrapTestCase.test_em_dash` | PORTABLE | — |
| `WrapTestCase.test_unix_options` | PORTABLE | — |
| `WrapTestCase.test_funky_hyphens` | PORTABLE | — |
| `WrapTestCase.test_punct_hyphens` | PORTABLE | — |
| `WrapTestCase.test_funky_parens` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_false` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_false_whitespace_only` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_false_whitespace_only_with_indent` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_whitespace_only` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_leading_whitespace` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_whitespace_line` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_whitespace_only_with_indent` | PORTABLE | — |
| `WrapTestCase.test_drop_whitespace_whitespace_indent` | PORTABLE | — |
| `WrapTestCase.test_split` | PORTABLE | — |
| `WrapTestCase.test_break_on_hyphens` | PORTABLE | — |
| `WrapTestCase.test_bad_width` | PORTABLE | — |
| `WrapTestCase.test_no_split_at_umlaut` | PORTABLE | — |
| `WrapTestCase.test_umlaut_followed_by_dash` | PORTABLE | — |
| `WrapTestCase.test_non_breaking_space` | PORTABLE | — |
| `WrapTestCase.test_narrow_non_breaking_space` | PORTABLE | — |
| `MaxLinesTestCase.test_simple` | PORTABLE | — |
| `MaxLinesTestCase.test_spaces` | PORTABLE | — |
| `MaxLinesTestCase.test_placeholder` | PORTABLE | — |
| `MaxLinesTestCase.test_placeholder_backtrack` | PORTABLE | — |
| `LongWordTestCase.test_break_long` | PORTABLE | — |
| `LongWordTestCase.test_nobreak_long` | PORTABLE | — |
| `LongWordTestCase.test_max_lines_long` | PORTABLE | — |
| `LongWordWithHyphensTestCase.test_break_long_words_on_hyphen` | PORTABLE | — |
| `LongWordWithHyphensTestCase.test_break_long_words_not_on_hyphen` | PORTABLE | — |
| `LongWordWithHyphensTestCase.test_break_on_hyphen_but_not_long_words` | PORTABLE | — |
| `LongWordWithHyphensTestCase.test_do_not_break_long_words_or_on_hyphens` | PORTABLE | — |
| `IndentTestCases.test_fill` | PORTABLE | — |
| `IndentTestCases.test_initial_indent` | PORTABLE | — |
| `IndentTestCases.test_subsequent_indent` | PORTABLE | — |
| `DedentTestCase.test_dedent_nomargin` | PORTABLE | — |
| `DedentTestCase.test_dedent_even` | PORTABLE | — |
| `DedentTestCase.test_dedent_uneven` | PORTABLE | — |
| `DedentTestCase.test_dedent_declining` | PORTABLE | — |
| `DedentTestCase.test_dedent_preserve_internal_tabs` | PORTABLE | — |
| `DedentTestCase.test_dedent_preserve_margin_tabs` | PORTABLE | — |
| `IndentTestCase.test_indent_nomargin_default` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_nomargin_explicit_default` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_nomargin_all_lines` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_no_lines` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_roundtrip_spaces` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_roundtrip_tabs` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_roundtrip_mixed` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_default` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_explicit_default` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_all_lines` | PORTABLE | _generated-loop_ |
| `IndentTestCase.test_indent_empty_lines` | PORTABLE | _generated-loop_ |
| `ShortenTestCase.test_simple` | PORTABLE | — |
| `ShortenTestCase.test_placeholder` | PORTABLE | — |
| `ShortenTestCase.test_empty_string` | PORTABLE | — |
| `ShortenTestCase.test_whitespace` | PORTABLE | — |
| `ShortenTestCase.test_width_too_small_for_placeholder` | PORTABLE | — |
| `ShortenTestCase.test_first_word_too_long_but_placeholder_fits` | PORTABLE | — |
