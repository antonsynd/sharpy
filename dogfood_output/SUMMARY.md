# Dogfooding Summary Report

**Generated:** 2026-02-17T22:56:20.760257

## Overall Statistics

- **Total Iterations:** 50
- **Successful:** 19 (38.0%)
- **Failed:** 21 (42.0%)
- **Skipped:** 10 (20.0%)

## Issues by Type

- **compilation_failed:** 7
- **execution_failed:** 5
- **generation_failed:** 2
- **internal_compiler_error:** 4
- **output_mismatch:** 3
- **skipped:** 10

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_210000_compilation_failed_0009) - module_imports/complex - 210.1s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_211853_compilation_failed_0010) - module_utils/medium - 143.2s
- [internal_compiler_error](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_212313_internal_compiler_error_0011) - cross_module_classes/complex - 194.3s
- [generation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_213520_generation_failed_0012) - module_imports/complex - 540.0s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_213851_output_mismatch_0013) - virtual_override/medium - 211.0s
- [internal_compiler_error](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_215615_internal_compiler_error_0014) - dict_comprehension/complex - 686.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260217_221111_compilation_failed_0015) - module_utils/complex - 248.2s
- [execution_failed](N/A) - cross_module_classes/medium - 140.3s
- [execution_failed](N/A) - module_imports/medium - 138.8s
- [execution_failed](N/A) - cross_module_classes/complex - 82.2s

## Recent Skips

- module_imports/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0018]: Unterminated literal name (backtick-delimited identifier)
  --> /tmp/tmps72gdtiy/main.spy:37:4
    |
 37 | ```
    |    ^
    |


- nullable_types/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'list[object]' to variable of type 'list[int?]'
  --> /tmp/tmp3iepqx0a/dogfood_test.spy:46:5
    |
 46 |     readings: list[int?] = [10, None, 30, None, 50]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- dunder_comparison/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0456]: Class 'Point' defines '__hash__' but not '__eq__(self, other: object)'. The .NET equality contract requires both. Define '__eq__(self, other: object) -> bool'.
  --> /tmp/tmp7iww925u/dogfood_test.spy:35:5
    |
 35 |     def __hash__(self) -> int:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0018]: Unterminated literal name (backtick-delimited identifier)
  --> /tmp/tmpxcrfz8ck/main.spy:95:4
    |
 95 | ```
    |    ^
    |


- module_utils/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0018]: Unterminated literal name (backtick-delimited identifier)
  --> /tmp/tmppra4ueqh/main.spy:59:4
    |
 59 | ```
    |    ^
    |


