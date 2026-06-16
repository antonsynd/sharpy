# Stdlib Test Porting Tracker (#843)

This file tracks the progress of porting C# xUnit stdlib tests to `.spy` test files.

## Regeneration

```bash
bash build_tools/regenerate_spy_tests.sh          # Regenerate all
bash build_tools/regenerate_spy_tests.sh --check   # CI staleness check
bash build_tools/regenerate_spy_tests.sh --dry-run  # Preview
```

**Never hand-edit files in `Spy/generated/`** — they are overwritten by the regeneration script.

## Porting Status

| C# File | Tests | .spy File | Tests | Status |
|---------|-------|-----------|-------|--------|
| ArgparseTests.cs | 42 | Spy/argparse/argparse_tests.spy | 42 | ported |
| ArgparseAdditionalTests.cs | 19 | Spy/argparse/argparse_additional_tests.spy | 18 | ported (1 omitted: SetOutput/StringWriter) |
| Base64Tests.cs | 13 | Spy/base64/base64_tests.spy | 13 | ported |
| BisectTests.cs | 19 | Spy/bisect/bisect_tests.spy | 19 | ported |
| BisectAdditionalTests.cs | 15 | Spy/bisect/bisect_additional_tests.spy | 15 | ported |
| CalendarTests.cs | 28 | Spy/calendar/calendar_tests.spy | 25 | ported (re-enabled #886-#892) |
| CollectionsModuleTests.cs | 57 | Spy/collections/collections_module_tests.spy | 54 | ported (3 omitted: typeof() reflection, List factory lambda, ToDictionary raw indexing) |
| CollectionsAdditionalTests.cs | 33 | Spy/collections/collections_additional_tests.spy | 33 | ported |
| ColorsysTests.cs | 32 | Spy/colorsys/colorsys_tests.spy | 32 | ported |
| ConfigparserTests.cs | 66 | Spy/configparser/configparser_tests.spy | 66 | ported |
| CounterCompleteTests.cs | 20 | Spy/collections/counter_complete_tests.spy | 20 | ported |
| CsvModuleTests.cs | 9 | Spy/csv/csv_module_tests.spy | 9 | ported |
| CsvDictTests.cs | 19 | Spy/csv/csv_dict_tests.spy | 16 | ported (3 omitted: null-arg TypeError guards — None→non-nullable param rejected at compile time, Axiom 3) |
| CsvReaderWriterTests.cs | 18 | Spy/csv/csv_reader_writer_tests.spy | 16 | ported (2 omitted: null-arg TypeError guards — None→non-nullable param rejected at compile time, Axiom 3) |
| DatetimeTests.cs | 69 | | | pending |
| DatetimeDateTests.cs | 20 | | | pending |
| DatetimeDateTimeTests.cs | 19 | | | pending |
| DatetimeTimeTests.cs | 25 | | | pending |
| DefaultDictTests.cs | 13 | Spy/collections/default_dict_tests.spy | 11 | ported (2 omitted: mutable closure capture, List factory lambda) |
| DequeChainMapTests.cs | 16 | Spy/collections/deque_chainmap_tests.spy | 15 | ported (1 omitted: reference identity assertion) |
| DifflibModuleTests.cs | 34 | Spy/difflib/difflib_module_tests.spy | 34 | ported |
| EmailTests.cs | 34 | Spy/email/email_tests.spy | 34 | ported (re-enabled #903) |
| FnmatchTests.cs | 27 | Spy/fnmatch/fnmatch_tests.spy | 27 | ported |
| FractionsTests.cs | 66 | Spy/fractions/fractions_tests.spy | 66 | ported |
| FunctoolsTests.cs | 21 | Spy/functools/functools_tests.spy | 21 | ported (re-enabled #904; cmp_to_key lambdas annotated) |
| GlobModuleTests.cs | 18 | Spy/glob/glob_tests.spy | 18 | ported (paths built via "/" concat — `import os` for makedirs shadows the `os.path` submodule) |
| GraphemeTests.cs | 27 | Spy/grapheme/grapheme_tests.spy | 27 | ported |
| HashlibTests.cs | 12 | Spy/hashlib/hashlib_tests.spy | 12 | ported |
| HashlibCompleteTests.cs | 22 | Spy/hashlib/hashlib_complete_tests.spy | 22 | ported |
| HeapqTests.cs | 31 | Spy/heapq/heapq_tests.spy | 31 | ported (re-enabled #889) |
| HeapqAdditionalTests.cs | 12 | Spy/heapq/heapq_additional_tests.spy | 12 | ported |
| HmacTests.cs | 13 | Spy/hmac/hmac_tests.spy | 13 | ported (re-enabled #890) |
| HtmlModuleTests.cs | 44 | Spy/html/html_module_tests.spy | 44 | ported (re-enabled #902; 4 convert_charrefs=False tests restored #906) |
| HttpTests.cs | 35 | Spy/http/http_tests.spy | 26 | ported (9 omitted: 7 HTTPResponse internal ctor, 1 reflection, 1 StringWriter) |
| IoModuleTests.cs | 15 | Spy/io/io_module_tests.spy | 15 | ported (io.StringIO only; Dispose_ClosesStream ported via close() — IDisposable.Dispose has no Python surface) |
| IoStringIOTests.cs | 27 | Spy/io/io_stringio_tests.spy | 27 | ported |
| IpaddressTests.cs | 114 | Spy/ipaddress/ipaddress_tests.spy | 112 | ported (2 omitted: BigInteger, Bytes ctor) |
| ItertoolsTests.cs | 20 | Spy/itertools/itertools_tests.spy | 20 | ported (inline collection literals bound to typed locals to work around codegen type-inference bug #917) |
| ItertoolsAdditionalTests.cs | 34 | Spy/itertools/itertools_additional_tests.spy | 34 | ported |
| ItertoolsCombinatoricsTests.cs | 11 | Spy/itertools/itertools_combinatorics_tests.spy | 11 | ported |
| ItertoolsFilterTests.cs | 16 | Spy/itertools/itertools_filter_tests.spy | 16 | ported |
| ItertoolsGroupingTests.cs | 15 | Spy/itertools/itertools_grouping_tests.spy | 15 | ported |
| ItertoolsInfiniteTests.cs | 10 | Spy/itertools/itertools_infinite_tests.spy | 10 | ported |
| JsonModuleTests.cs | 109 | Spy/json/json_module_tests.spy | 108 | ported (1 omitted: loads_null_throws_type_error passes null! to non-nullable — not expressible in type-safe Sharpy, Axiom 3) |
| JsonModuleAdditionalTests.cs | 13 | Spy/json/json_additional_tests.spy | 13 | ported |
| JsonEncoderDecoderTests.cs | 16 | Spy/json/json_encoder_decoder_tests.spy | 14 | ported (2 omitted: need subclassing json.JSONEncoder, #914; re-enabled 5 object_hook tests after #915) |
| JsonTypedDeserializationTests.cs | 16 | Spy/json/json_typed_deserialization_tests.spy | 14 | ported (2 omitted: type-unsafe null input, Axiom 3; required stdlib fix: IncludeFields + Optional converter for Sharpy field-lowered classes) |
| LoggingModuleTests.cs | 14 | | | pending |
| LoggingCompleteTests.cs | 22 | | | pending |
| LruCacheTests.cs | 13 | Spy/functools/lru_cache_tests.spy | 13 | ported |
| MathAdditionalTests.cs | 48 | Spy/math/math_additional_tests.spy | 48 | ported |
| MathAdditionalTests2.cs | 72 | Spy/math/math_additional2_tests.spy | 72 | ported |
| ModuleIntegrationTests.cs | 7 | | | pending |
| NdArrayTests.cs | 20 | | | pending |
| NdArrayIndexingTests.cs | 14 | | | pending |
| NdArrayOperatorTests.cs | 19 | | | pending |
| NdArrayReshapeTests.cs | 20 | | | pending |
| NdArraySlicingTests.cs | 22 | | | pending |
| NdArrayAdvancedTests.cs | 32 | | | pending |
| NumpyCreationTests.cs | 31 | | | pending |
| NumpyFftTests.cs | 16 | | | pending |
| NumpyLinalgTests.cs | 37 | | | pending |
| NumpyManipulationTests.cs | 22 | | | pending |
| NumpyMathTests.cs | 37 | | | pending |
| NumpyRandomTests.cs | 17 | | | pending |
| OrderedDictTests.cs | 13 | Spy/collections/ordered_dict_tests.spy | 13 | ported |
| OsModuleTests.cs | 33 | Spy/os/os_module_tests.spy | 33 | ported (constants use C# field names os.Sep/os.Linesep/os.Name/os.Environ — static module fields aren't snake_case-mapped in project compilation, SPY0203; mirrors C# OsModule.Sep) |
| OsModuleAdditionalTests.cs | 16 | Spy/os/os_module_additional_tests.spy | 16 | ported (os.Altsep/os.Pathsep/os.Environ as above; list/dict membership via `contains` helper / bool local to avoid xUnit2009) |
| OsPathTests.cs | 25 | Spy/os/os_path_tests.spy | 25 | ported (os.path via `from os.path import ...`; POSIX separator literals) |
| OsPathAdditionalTests.cs | 24 | Spy/os/os_path_additional_tests.spy | 24 | ported |
| PathlibTests.cs | 61 | | | pending |
| PathlibAdditionalTests.cs | 32 | | | pending |
| PprintTests.cs | 32 | Spy/pprint/pprint_tests.spy | 32 | ported (re-enabled #888) |
| RandomTests.cs | 16 | | | pending |
| RandomAdditionalTests.cs | 22 | | | pending |
| RandomAdditionalTests2.cs | 23 | | | pending |
| ReModuleTests.cs | 56 | Spy/re/re_module_tests.spy | 56 | ported |
| ReOperationTests.cs | 24 | Spy/re/re_operation_tests.spy | 24 | ported |
| RePatternTests.cs | 74 | Spy/re/re_pattern_tests.spy | 67 | ported (7 omitted: 4 re.error type-not-nameable + 3 MatchResult indexer __getitem__ — spy-sourced module member-type gap, #918) |
| RequestsModuleTests.cs | 20 | | | pending |
| RequestsResponseTests.cs | 38 | | | pending |
| RequestsSessionTests.cs | 37 | | | pending |
| RequestsAdvancedTests.cs | 34 | | | pending |
| SecretsTests.cs | 21 | Spy/secrets/secrets_tests.spy | 21 | ported |
| ShlexModuleTests.cs | 34 | Spy/shlex/shlex_module_tests.spy | 31 | ported (3 omitted: Split/Quote/Join_Null_ThrowsTypeError — null→non-nullable rejected at compile time, Axiom 3) |
| ShutilTests.cs | 15 | Spy/shutil/shutil_tests.spy | 15 | ported (Copy2_PreservesTimestamps verifies dst mtime == src mtime via os.stat().st_mtime — os.utime not exposed, so the custom-time setup is dropped; os.path helpers via `from os.path import ...`) |
| ShutilAdditionalTests.cs | 14 | Spy/shutil/shutil_additional_tests.spy | 14 | ported (2 which tests' Windows skip dropped — POSIX runners, as in glob) |
| SocketModuleTests.cs | 46 | | | pending |
| Sqlite3ConnectionTests.cs | 23 | | | pending |
| Sqlite3CursorTests.cs | 46 | | | pending |
| Sqlite3ErrorTests.cs | 33 | | | pending |
| Sqlite3RowTests.cs | 24 | | | pending |
| StatisticsTests.cs | 31 | Spy/statistics/statistics_tests.spy | 31 | ported |
| StatisticsAdditionalTests.cs | 19 | Spy/statistics/statistics_additional_tests.spy | 19 | ported |
| StringModuleTests.cs | 12 | Spy/string/string_module_tests.spy | 12 | ported |
| SubprocessModuleTests.cs | 36 | | | pending |
| SysModuleTests.cs | 21 | | | pending |
| TarfileTests.cs | 24 | | | pending |
| TempfileTests.cs | 11 | Spy/tempfile/tempfile_tests.spy | 11 | ported (os.path helpers via `from os.path import ...` — attribute access `os.path.basename` is shadowed by the top-level `os` binding, SPY0203; plain `import os` still needed for rmdir/remove) |
| TempfileCompleteTests.cs | 11 | Spy/tempfile/tempfile_complete_tests.spy | 11 | ported (GetExtension mirrored with os.path.splitext) |
| TextwrapTests.cs | 27 | Spy/textwrap/textwrap_tests.spy | 27 | ported |
| ThreadingModuleTests.cs | 43 | | | pending |
| TimeModuleTests.cs | 34 | | | pending |
| TomlModuleTests.cs | 46 | Spy/toml/toml_module_tests.spy | 41 | ported (5 omitted: 5 null-arg/Axiom 3) |
| TomlTypedDeserializationTests.cs | 6 | Spy/toml/toml_typed_deserialization_tests.spy | 5 | ported (2 omitted: null-arg/Axiom 3; +1 table array test) |
| UuidModuleTests.cs | 13 | Spy/uuid/uuid_module_tests.spy | 13 | ported (re-enabled #886) |
| XmlModuleTests.cs | 84 | | | pending |
| YamlModuleTests.cs | 60 | Spy/yaml/yaml_module_tests.spy | 55 | ported (5 omitted: null-arg TypeError guards — None→non-nullable param rejected at compile time, Axiom 3) |
| YamlRoundtripTests.cs | 29 | Spy/yaml/yaml_roundtrip_tests.spy | 23 | ported (6 omitted: 1 null-arg/Axiom 3, 1 TryGetValue/`out`-param interop, 4 CommentInfo construction — yaml surface gaps #919) |
| YamlTypedDeserializationTests.cs | 6 | Spy/yaml/yaml_typed_deserialization_tests.spy | 5 | ported (1 omitted: null-arg TypeError, Axiom 3) |
| ZoneinfoTests.cs | 28 | Spy/zoneinfo/zoneinfo_tests.spy | 29 | ported (re-enabled #901; +1 invalid-zone test) |

## Out of Scope (stay C#)

| C# File | Tests | Reason |
|---------|-------|--------|
| UnittestMarkerTests.cs | 4 | Tests C# runtime helper (compiler rewrites these calls) |
| UnittestCapturedOutputTests.cs | 8 | Tests C# runtime helper |
| UnittestTmpPathTests.cs | 6 | Tests C# runtime helper |

## Residuals (blocked by compiler issues)

Re-enabling the 15 modules that were excluded for #879–#883 surfaced a **second
layer** of pre-existing compiler / codegen / inference gaps (previously masked
behind #880/#881). After #879–#883 + #18 landed, **argparse** and **shlex**
re-enable cleanly (513 spy tests pass, up from 454). The remaining modules stay
excluded in `tests.spyproj` pending the gap fixes below. Where a test-code fix
was necessary but not sufficient (a codegen/stdlib gap also blocks the module),
the `.spy` source has already been fixed and is noted as "test-code fixed".

The #886–#893 fixes (plan f40f84) landed and **10 of these 13 modules now re-enable
and pass** (calendar, uuid, http, fractions, pprint, heapq, hmac, ipaddress,
difflib, html — 907 spy tests green). The remaining **3** are blocked by a *third*
layer of out-of-scope gaps discovered during re-enablement and stay excluded in
`tests.spyproj` pending those new issues.

| Module (.spy) | Original gap | Status |
|---------------|--------------|--------|
| calendar | #886 (tuple `==`/`!=`) | ✅ re-enabled |
| uuid | #886 (`uuid.UUID ==`/`!=`) | ✅ re-enabled |
| http | #886 + #897 (`http.HTTPStatus` access) | ✅ re-enabled |
| fractions | #887 (`BigInteger == int`, reflected ops) | ✅ re-enabled — `floor_div` annotated `long` (Axiom 1); 1 test stays omitted (`test_fraction_large_numerator_denominator`): its `10 ** 50` exceeds `long`, and since Sharpy integers are fixed-width (Axiom 1) that constant now fails loudly at compile time with **SPY0328** rather than saturating — arbitrary precision is out of scope (**#905**) |
| pprint | #888 (`(1,)` 1-tuple) | ✅ re-enabled |
| heapq (`heapq_tests.spy`) | #889 (`sort(key=lambda)`) | ✅ re-enabled |
| hmac | #890 (`hmac.new` overload) | ✅ re-enabled |
| ipaddress | #891 (module alias) + #898 | ✅ re-enabled |
| difflib | #892 (tuple `.ItemN`) | ✅ re-enabled |
| html | #892 + #902 (f-string tuple index, fixed by narrowing-key commit) | ✅ re-enabled — the 4 `convert_charrefs=False` parser tests are restored; root cause was codegen dropping kwargs in `super().__init__` (**#906**), not the stdlib HTMLParser |
| zoneinfo | #886 | ✅ re-enabled — `==`/`!= None` on a CLR reference type now lowers to a null check (**#901**) |
| email | #891 | ✅ re-enabled — `isinstance(x, mod.Type)` now lowers to a type test (**#903**) |
| functools (`functools_tests.spy`) | #889 | ✅ re-enabled — `cmp_to_key` comparator lambda parameters must be annotated (`lambda a: int, b: int: ...`) so the generic type argument is inferable; unannotated comparators now get SPY0237 instead of leaking CS0411 (**#904**) |
