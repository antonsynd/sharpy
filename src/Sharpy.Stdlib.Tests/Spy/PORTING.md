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
| Base64Tests.cs | 13 | | | pending |
| BisectTests.cs | 19 | | | pending |
| BisectAdditionalTests.cs | 15 | | | pending |
| CalendarTests.cs | 28 | Spy/calendar/calendar_tests.spy | 25 | ported (re-enabled #886-#892) |
| CollectionsModuleTests.cs | 57 | | | pending |
| CollectionsAdditionalTests.cs | 33 | | | pending |
| ColorsysTests.cs | 32 | Spy/colorsys/colorsys_tests.spy | 32 | ported |
| ConfigparserTests.cs | 66 | | | pending |
| CounterCompleteTests.cs | 20 | | | pending |
| CsvModuleTests.cs | 9 | | | pending |
| CsvDictTests.cs | 19 | | | pending |
| CsvReaderWriterTests.cs | 18 | | | pending |
| DatetimeTests.cs | 69 | | | pending |
| DatetimeDateTests.cs | 20 | | | pending |
| DatetimeDateTimeTests.cs | 19 | | | pending |
| DatetimeTimeTests.cs | 25 | | | pending |
| DefaultDictTests.cs | 13 | | | pending |
| DequeChainMapTests.cs | 16 | | | pending |
| DifflibModuleTests.cs | 34 | Spy/difflib/difflib_module_tests.spy | 34 | ported |
| EmailTests.cs | 34 | | | pending |
| FnmatchTests.cs | 27 | Spy/fnmatch/fnmatch_tests.spy | 27 | ported |
| FractionsTests.cs | 66 | Spy/fractions/fractions_tests.spy | 66 | ported |
| FunctoolsTests.cs | 21 | | | pending |
| GlobModuleTests.cs | 18 | | | pending |
| GraphemeTests.cs | 27 | Spy/grapheme/grapheme_tests.spy | 27 | ported |
| HashlibTests.cs | 12 | Spy/hashlib/hashlib_tests.spy | 12 | ported |
| HashlibCompleteTests.cs | 22 | Spy/hashlib/hashlib_complete_tests.spy | 22 | ported |
| HeapqTests.cs | 31 | Spy/heapq/heapq_tests.spy | 31 | ported (re-enabled #889) |
| HeapqAdditionalTests.cs | 12 | | | pending |
| HmacTests.cs | 13 | Spy/hmac/hmac_tests.spy | 13 | ported (re-enabled #890) |
| HtmlModuleTests.cs | 44 | Spy/html/html_module_tests.spy | 40 | ported (re-enabled #902; 4 omitted: convert_charrefs=False, #906) |
| HttpTests.cs | 35 | Spy/http/http_tests.spy | 26 | ported (9 omitted: 7 HTTPResponse internal ctor, 1 reflection, 1 StringWriter) |
| IoModuleTests.cs | 15 | | | pending |
| IoStringIOTests.cs | 27 | | | pending |
| IpaddressTests.cs | 114 | Spy/ipaddress/ipaddress_tests.spy | 112 | ported (2 omitted: BigInteger, Bytes ctor) |
| ItertoolsTests.cs | 20 | | | pending |
| ItertoolsAdditionalTests.cs | 34 | | | pending |
| ItertoolsCombinatoricsTests.cs | 11 | | | pending |
| ItertoolsFilterTests.cs | 16 | | | pending |
| ItertoolsGroupingTests.cs | 15 | | | pending |
| ItertoolsInfiniteTests.cs | 10 | | | pending |
| JsonModuleTests.cs | 109 | | | pending |
| JsonModuleAdditionalTests.cs | 13 | | | pending |
| JsonEncoderDecoderTests.cs | 16 | | | pending |
| JsonTypedDeserializationTests.cs | 16 | | | pending |
| LoggingModuleTests.cs | 14 | | | pending |
| LoggingCompleteTests.cs | 22 | | | pending |
| LruCacheTests.cs | 13 | | | pending |
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
| OrderedDictTests.cs | 13 | | | pending |
| OsModuleTests.cs | 33 | | | pending |
| OsModuleAdditionalTests.cs | 16 | | | pending |
| OsPathTests.cs | 25 | | | pending |
| OsPathAdditionalTests.cs | 24 | | | pending |
| PathlibTests.cs | 61 | | | pending |
| PathlibAdditionalTests.cs | 32 | | | pending |
| PprintTests.cs | 32 | Spy/pprint/pprint_tests.spy | 32 | ported (re-enabled #888) |
| RandomTests.cs | 16 | | | pending |
| RandomAdditionalTests.cs | 22 | | | pending |
| RandomAdditionalTests2.cs | 23 | | | pending |
| ReModuleTests.cs | 56 | | | pending |
| ReOperationTests.cs | 24 | | | pending |
| RePatternTests.cs | 74 | | | pending |
| RequestsModuleTests.cs | 20 | | | pending |
| RequestsResponseTests.cs | 38 | | | pending |
| RequestsSessionTests.cs | 37 | | | pending |
| RequestsAdvancedTests.cs | 34 | | | pending |
| SecretsTests.cs | 21 | | | pending |
| ShlexModuleTests.cs | 34 | | | pending |
| ShutilTests.cs | 15 | | | pending |
| ShutilAdditionalTests.cs | 14 | | | pending |
| SocketModuleTests.cs | 46 | | | pending |
| Sqlite3ConnectionTests.cs | 23 | | | pending |
| Sqlite3CursorTests.cs | 46 | | | pending |
| Sqlite3ErrorTests.cs | 33 | | | pending |
| Sqlite3RowTests.cs | 24 | | | pending |
| StatisticsTests.cs | 31 | | | pending |
| StatisticsAdditionalTests.cs | 19 | | | pending |
| StringModuleTests.cs | 12 | | | pending |
| SubprocessModuleTests.cs | 36 | | | pending |
| SysModuleTests.cs | 21 | | | pending |
| TarfileTests.cs | 24 | | | pending |
| TempfileTests.cs | 11 | | | pending |
| TempfileCompleteTests.cs | 11 | | | pending |
| TextwrapTests.cs | 27 | | | pending |
| ThreadingModuleTests.cs | 43 | | | pending |
| TimeModuleTests.cs | 34 | | | pending |
| TomlModuleTests.cs | 46 | | | pending |
| TomlTypedDeserializationTests.cs | 6 | | | pending |
| UuidModuleTests.cs | 13 | Spy/uuid/uuid_module_tests.spy | 13 | ported (re-enabled #886) |
| XmlModuleTests.cs | 84 | | | pending |
| YamlModuleTests.cs | 60 | | | pending |
| YamlRoundtripTests.cs | 29 | | | pending |
| YamlTypedDeserializationTests.cs | 6 | | | pending |
| ZoneinfoTests.cs | 28 | | | pending |

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
| fractions | #887 (`BigInteger == int`, reflected ops) | ✅ re-enabled — `floor_div` annotated `long` (Axiom 1); 1 test omitted (`test_fraction_large_numerator_denominator`, needs arbitrary-precision `10 ** 50`, **#905**) |
| pprint | #888 (`(1,)` 1-tuple) | ✅ re-enabled |
| heapq (`heapq_tests.spy`) | #889 (`sort(key=lambda)`) | ✅ re-enabled |
| hmac | #890 (`hmac.new` overload) | ✅ re-enabled |
| ipaddress | #891 (module alias) + #898 | ✅ re-enabled |
| difflib | #892 (tuple `.ItemN`) | ✅ re-enabled |
| html | #892 + #902 (f-string tuple index, fixed by narrowing-key commit) | ✅ re-enabled — 4 `convert_charrefs=False` parser tests omitted (separate stdlib bug, **#906**) |
| zoneinfo | #886 | ✅ re-enabled — `==`/`!= None` on a CLR reference type now lowers to a null check (**#901**) |
| email | #891 | ✅ re-enabled — `isinstance(x, mod.Type)` now lowers to a type test (**#903**) |
| functools (`functools_tests.spy`) | #889 | ✅ re-enabled — `cmp_to_key` comparator lambda parameters must be annotated (`lambda a: int, b: int: ...`) so the generic type argument is inferable; unannotated comparators now get SPY0237 instead of leaking CS0411 (**#904**) |
