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
| ArgparseTests.cs | 42 | | | pending |
| ArgparseAdditionalTests.cs | 19 | | | pending |
| Base64Tests.cs | 13 | | | pending |
| BisectTests.cs | 19 | | | pending |
| BisectAdditionalTests.cs | 15 | | | pending |
| CalendarTests.cs | 28 | | | pending |
| CollectionsModuleTests.cs | 57 | | | pending |
| CollectionsAdditionalTests.cs | 33 | | | pending |
| ColorsysTests.cs | 32 | | | pending |
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
| DifflibModuleTests.cs | 34 | | | pending |
| EmailTests.cs | 34 | | | pending |
| FnmatchTests.cs | 27 | | | pending |
| FractionsTests.cs | 66 | | | pending |
| FunctoolsTests.cs | 21 | | | pending |
| GlobModuleTests.cs | 18 | | | pending |
| GraphemeTests.cs | 27 | | | pending |
| HashlibTests.cs | 12 | | | pending |
| HashlibCompleteTests.cs | 22 | | | pending |
| HeapqTests.cs | 31 | | | pending |
| HeapqAdditionalTests.cs | 12 | | | pending |
| HmacTests.cs | 13 | | | pending |
| HtmlModuleTests.cs | 44 | | | pending |
| HttpTests.cs | 35 | | | pending |
| IoModuleTests.cs | 15 | | | pending |
| IoStringIOTests.cs | 27 | | | pending |
| IpaddressTests.cs | 114 | | | pending |
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
| PprintTests.cs | 32 | | | pending |
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
| UuidModuleTests.cs | 13 | | | pending |
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

| C# File | Test(s) | Blocking Issue | Status |
|---------|---------|----------------|--------|
| (none yet) | | | |
