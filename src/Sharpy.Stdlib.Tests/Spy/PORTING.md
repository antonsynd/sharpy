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
| DatetimeTests.cs | 69 | Spy/datetime/datetime_tests.spy | 68 | ported (1 omitted: DatetimeModule_ExposesExpectedTypes — typeof()/Type reflection, no Sharpy surface. tzinfo identity via `is`; ArgumentOutOfRangeException via `from system import`; total_seconds is a property; date_component/time_component) |
| DatetimeDateTests.cs | 20 | Spy/datetime/datetime_date_tests.spy | 20 | ported |
| DatetimeDateTimeTests.cs | 19 | Spy/datetime/datetime_datetime_tests.spy | 19 | ported (tzinfo identity via `is`/`is None`) |
| DatetimeTimeTests.cs | 25 | Spy/datetime/datetime_time_tests.spy | 25 | ported |
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
| LoggingModuleTests.cs | 14 | Spy/logging/logging_module_tests.spy | 1 | ported (13 omitted: 2 reference identity — GetLogger BeSameAs/NotBeSameAs; 11 StringWriter/Console.SetError stderr capture — no Sharpy surface for stderr interception) |
| LoggingCompleteTests.cs | 22 | Spy/logging/logging_complete_tests.spy | 4 | ported (18 omitted: 3 reference identity — GetLogger BeSameAs/NotBeSameAs; 15 StringWriter/Console.SetError stderr capture — no Sharpy surface for stderr interception) |
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
| OsModuleTests.cs | 33 | Spy/os/os_module_tests.spy | 33 | ported (constants use C# field names os.Sep/os.Linesep/os.Name/os.Environ — static module fields aren't snake_case-mapped in project compilation, SPY0203 / #940; mirrors C# OsModule.Sep) |
| OsModuleAdditionalTests.cs | 16 | Spy/os/os_module_additional_tests.spy | 16 | ported (os.Altsep/os.Pathsep/os.Environ as above; list/dict membership via `contains` helper / bool local to avoid xUnit2009) |
| OsPathTests.cs | 25 | Spy/os/os_path_tests.spy | 25 | ported (os.path via `from os.path import ...`; POSIX separator literals) |
| OsPathAdditionalTests.cs | 24 | Spy/os/os_path_additional_tests.spy | 24 | ported |
| PathlibTests.cs | 61 | Spy/pathlib/pathlib_tests.spy | 60 | ported (1 omitted: ReadBytes/WriteBytes round-trip — CLR byte[] params/returns not bridgeable from .spy, #941. `/` operator works; kwargs passed positionally since codegen mangles snake_case kwarg names, #942) |
| PathlibAdditionalTests.cs | 32 | Spy/pathlib/pathlib_additional_tests.spy | 29 | ported (3 omitted: Constructor null→non-nullable Axiom 3; `Path? == None` rejected SPY0222 — Optional uses `is None`; WriteBytes empty byte[] #941) |
| PprintTests.cs | 32 | Spy/pprint/pprint_tests.spy | 32 | ported (re-enabled #888) |
| RandomTests.cs | 16 | Spy/random/random_tests.spy | 11 | ported (5 omitted: 4 choice — `random.choice(list)` ambiguous overload SPY0353 #954; 1 Shuffle_NullList null-arg Axiom 3) |
| RandomAdditionalTests.cs | 22 | Spy/random/random_additional_tests.spy | 15 | ported (7 omitted: 2 weighted Choices — `choices` has no weights param in random_module.spy; 4 Sys_* tests belong to sys module not random, covered by sys port) |
| RandomAdditionalTests2.cs | 23 | Spy/random/random_additional2_tests.spy | 20 | ported (3 omitted: 2 choice ambiguous overload #954; 1 weighted Choices cumulative-zero — no weights param in spy) |
| ReModuleTests.cs | 56 | Spy/re/re_module_tests.spy | 56 | ported |
| ReOperationTests.cs | 24 | Spy/re/re_operation_tests.spy | 24 | ported |
| RePatternTests.cs | 74 | Spy/re/re_pattern_tests.spy | 67 | ported (7 omitted: 4 re.error type-not-nameable + 3 MatchResult indexer __getitem__ — spy-sourced module member-type gap, #918) |
| RequestsModuleTests.cs | 20 | Spy/requests/requests_module_tests.spy | 0 | ported (all 20 omitted [14 methods, 6 Theory expansion]: all tests drive internal Requests.Send with mocked HttpMessageHandler — not reachable from .spy; 1 null arg Axiom 3; 1 live DNS) |
| RequestsResponseTests.cs | 38 | Spy/requests/requests_response_tests.spy | 0 | ported (all 38 omitted [26 methods, 12 Theory expansion]: all tests construct Response via internal ctor from HttpResponseMessage — not reachable from .spy; 1 null arg Axiom 3) |
| RequestsSessionTests.cs | 37 | Spy/requests/requests_session_tests.spy | 20 | ported (17 omitted [11 methods, 6 Theory expansion]: 2 null arg Axiom 3; 1 IDisposable/Dispose; 1 ArgumentOutOfRangeException not catchable; 7 methods internal Requests.Send incl. 3 Theories ×3 InlineData each) |
| RequestsAdvancedTests.cs | 34 | Spy/requests/requests_advanced_tests.spy | 0 | ported (all 34 omitted [32 methods, 2 Theory expansion]: file upload/streaming via internal Requests.Send+mock; session config tests already ported in session_tests.spy) |
| SecretsTests.cs | 21 | Spy/secrets/secrets_tests.spy | 21 | ported |
| ShlexModuleTests.cs | 34 | Spy/shlex/shlex_module_tests.spy | 31 | ported (3 omitted: Split/Quote/Join_Null_ThrowsTypeError — null→non-nullable rejected at compile time, Axiom 3) |
| ShutilTests.cs | 15 | Spy/shutil/shutil_tests.spy | 15 | ported (Copy2_PreservesTimestamps verifies dst mtime == src mtime via os.stat().st_mtime — os.utime not exposed, so the custom-time setup is dropped; os.path helpers via `from os.path import ...`) |
| ShutilAdditionalTests.cs | 14 | Spy/shutil/shutil_additional_tests.spy | 14 | ported (2 which tests' Windows skip dropped — POSIX runners, as in glob) |
| SocketModuleTests.cs | 46 | Spy/socket/socket_module_tests.spy | 40 | ported (6 omitted: 3 disabled pending #945 — socket.error/socket.timeout resolve to wrong module's type; 1 InnerException identity; 2 other reflection/construction) |
| Sqlite3ConnectionTests.cs | 23 | Spy/sqlite3/sqlite3_connection_tests.spy | 20 | ported (3 omitted: 2 BeOfType reflection; 1 IDisposable/using — covered by close()-then-use test) |
| Sqlite3CursorTests.cs | 46 | Spy/sqlite3/sqlite3_cursor_tests.spy | 43 | ported (3 omitted: LINQ Cast/Select; IDisposable Dispose; TypeMapping_Null collapsed into Execute_NullParameter) |
| Sqlite3ErrorTests.cs | 33 | Spy/sqlite3/sqlite3_error_tests.spy | 4 | ported (29 omitted: 12 BeAssignableTo reflection; 6 MessagePropagation exception ctor; 6 InnerException ctor; 6 DefaultConstructor — all exception construction/reflection with no Sharpy surface; replaced by 4 behavioral catch-hierarchy tests) |
| Sqlite3RowTests.cs | 24 | Spy/sqlite3/sqlite3_row_tests.spy | 21 | ported (3 omitted: Keys NotBeSameAs identity; ISized interface cast; BeOfType reflection) |
| StatisticsTests.cs | 31 | Spy/statistics/statistics_tests.spy | 31 | ported |
| StatisticsAdditionalTests.cs | 19 | Spy/statistics/statistics_additional_tests.spy | 19 | ported |
| StringModuleTests.cs | 12 | Spy/string/string_module_tests.spy | 12 | ported |
| SubprocessModuleTests.cs | 36 | Spy/subprocess/subprocess_module_tests.spy | 36 | ported (all POSIX-portable; CompletedProcess/Popen/CalledProcessError/TimeoutExpired surface verified) |
| SysModuleTests.cs | 21 | Spy/sys/sys_module_tests.spy | 14 | ported (7 omitted: 4 reference identity BeSameAs/NotBeSameAs Console.Out/Error/argv/path copies; 2 StringWriter/Console.SetOut/SetError redirect; 1 null arg getsizeof(null) Axiom 3) |
| TarfileTests.cs | 24 | Spy/tarfile/tarfile_tests.spy | 21 | ported (3 omitted: TarInfo_DefaultProperties/TypeChecks/ToString — `tarfile.TarInfo()` ctor is internal, not constructible from the test assembly, #943. `with tarfile.open(...)`, extractfile().decode(), module-qualified exceptions/constants, isinstance hierarchy all work; `.add(path, arcname)` positional) |
| TempfileTests.cs | 11 | Spy/tempfile/tempfile_tests.spy | 11 | ported (os.path helpers via `from os.path import ...` — attribute access `os.path.basename` is shadowed by the top-level `os` binding, SPY0203; plain `import os` still needed for rmdir/remove) |
| TempfileCompleteTests.cs | 11 | Spy/tempfile/tempfile_complete_tests.spy | 11 | ported (GetExtension mirrored with os.path.splitext) |
| TextwrapTests.cs | 27 | Spy/textwrap/textwrap_tests.spy | 27 | ported |
| ThreadingModuleTests.cs | 43 | Spy/threading/threading_module_tests.spy | 43 | ported (Thread/Lock/RLock/Event/Condition/Semaphore/BoundedSemaphore/Barrier/Timer all verified; closures via list[int] cells for cross-thread mutable state) |
| TimeModuleTests.cs | 34 | Spy/time/time_module_tests.spy | 19 | ported (15 omitted: all ConvertFormat tests — `ConvertFormat` is `internal`, no `time.*` Python surface; 12 [Theory] code-mapping rows + DoublePercent/CompoundFormat/EscapesLiteralLetters. Behavior exercised indirectly via strftime. StructTime.Wday tests re-expressed via time.gmtime(secs) for the same UTC instants; struct_time ctor + str() work) |
| TomlModuleTests.cs | 46 | Spy/toml/toml_module_tests.spy | 41 | ported (5 omitted: 5 null-arg/Axiom 3) |
| TomlTypedDeserializationTests.cs | 6 | Spy/toml/toml_typed_deserialization_tests.spy | 5 | ported (2 omitted: null-arg/Axiom 3; +1 table array test) |
| UuidModuleTests.cs | 13 | Spy/uuid/uuid_module_tests.spy | 13 | ported (re-enabled #886) |
| XmlModuleTests.cs | 84 | | | pending |
| YamlModuleTests.cs | 60 | Spy/yaml/yaml_module_tests.spy | 55 | ported (5 omitted: null-arg TypeError guards — None→non-nullable param rejected at compile time, Axiom 3) |
| YamlRoundtripTests.cs | 29 | Spy/yaml/yaml_roundtrip_tests.spy | 23 | ported (6 omitted: 1 null-arg/Axiom 3, 1 TryGetValue/`out`-param interop, 4 CommentInfo construction — yaml surface gaps #919) |
| YamlTypedDeserializationTests.cs | 6 | Spy/yaml/yaml_typed_deserialization_tests.spy | 5 | ported (1 omitted: null-arg TypeError, Axiom 3) |
| ZoneinfoTests.cs | 28 | Spy/zoneinfo/zoneinfo_tests.spy | 29 | ported (re-enabled #901; +1 invalid-zone test) |

## Phase 3 (I/O & System) — COMPLETE (2026-06-16)

All 7 modules ported; 14 C# test files removed and replaced by `.spy`. **319/326 tests ported, 7 documented omissions, 0 silent drops.**

| Module | C# files | Ported/Total | Omitted |
|--------|----------|--------------|---------|
| glob | 1 | 18/18 | 0 |
| tempfile | 2 | 22/22 | 0 |
| io | 2 | 42/42 | 0 |
| shutil | 2 | 29/29 | 0 |
| os | 4 | 98/98 | 0 |
| pathlib | 2 | 89/93 | 4 |
| tarfile | 1 | 21/24 | 3 |
| **Total** | **14** | **319/326** | **7** |

Omissions: pathlib ×4 — CLR `byte[]` not bridgeable (#941, ×2: read/write round-trip, empty write), null→non-nullable ctor (Axiom 3), `Path? == None` (SPY0222, Optional uses `is None`); tarfile ×3 — `tarfile.TarInfo()` ctor is `internal`/not constructible (#943). Each omitted behavior is otherwise covered (e.g. TarInfo via `getmembers()`).

Compiler/stdlib gaps filed during Phase 3 (none block a module): **#940** (module static-field name mapping differs run vs project mode), **#941** (CLR `byte[]` params/returns not bridgeable from `.spy`), **#942** (snake_case keyword-arg names mangled to camelCase → CS1739), **#943** (`tarfile.TarInfo` internal ctor).

## Phase 4 (System & Network) — COMPLETE (2026-06-17)

All 7 modules ported; 14 C# test files removed and replaced by `.spy`. **246/437 tests ported, 191 documented omissions, 0 silent drops.** (Counts use expanded test cases — each `[Theory]`/`[InlineData]` row = 1 case.)

| Module | C# files | Ported/Total | Omitted |
|--------|----------|--------------|---------|
| sys | 1 | 14/21 | 7 |
| logging | 2 | 5/36 | 31 |
| subprocess | 1 | 36/36 | 0 |
| threading | 1 | 43/43 | 0 |
| socket | 1 | 40/46 | 6 |
| sqlite3 | 4 | 88/126 | 38 |
| requests | 4 | 20/129 | 109 |
| **Total** | **14** | **246/437** | **191** |

Omission breakdown by category:
- **requests ×109**: internal `Requests.Send` + mocked `HttpMessageHandler` with no Sharpy surface (79 expanded); internal `Response(HttpResponseMessage)` constructor (26); null arg Axiom 3 (3); live DNS (1). The 4 C# files have `[Theory]` methods that expand to 26 additional test cases beyond method count.
- **sqlite3 error ×29**: exception construction + reflection (BeAssignableTo, MessagePropagation, InnerException, DefaultConstructor) — no Sharpy surface for direct exception construction/inspection
- **logging ×31**: StringWriter/Console.SetError stderr capture (26); reference identity GetLogger BeSameAs/NotBeSameAs (5) — no Sharpy surface for stderr interception
- **sys ×7**: reference identity BeSameAs/NotBeSameAs (4); StringWriter redirect (2); null arg Axiom 3 (1)
- **socket ×6**: reflection/exception hierarchy (3); InnerException identity (1); other reflection (2)
- **sqlite3 conn/cursor/row ×9**: BeOfType/ISized reflection (5); IDisposable/using (2); LINQ (1); collapsed duplicate (1)

Compiler fix applied during Phase 4: CLR nested-type names (`SocketModule+Socket` → `SocketModule.Socket`) via `ClrNameHelper.ToCSharpQualifiedName`.

Compiler/stdlib gaps filed during Phase 4: **#945** (`socket.error`/`socket.timeout` resolve to wrong module's type — name collision with `re.Error`/stray `Timeout`; 3 socket tests disabled), **#946** (threading Lock/RLock/Semaphore `with` context manager emits Enter/Exit — types lack those methods), **#947** (`int?` == `int` rejected by SPY0222 — nullable comparison), **#948** (no Sharpy surface for stderr capture — blocks logging test porting), **#949** (`bytes([1,2,3])` emits invalid `Sharpy.List<int>` instead of `Bytes`), **#950** (`[None]` infers `Sharpy.List<void>` — should require annotation), **#951** (CLR `object?[]` not indexable/matchable from .spy — sqlite3 fetchone default), **#952** (sqlite3 error types not reachable as module members — #918 class).

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
