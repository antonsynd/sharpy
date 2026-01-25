# Dogfooding Summary Report

**Generated:** 2026-01-24T19:32:58.090516

## Overall Statistics

- **Total Iterations:** 20
- **Successful:** 1 (5.0%)
- **Failed:** 12 (60.0%)
- **Skipped:** 7 (35.0%)

## Issues by Type

- **compilation_failed:** 4
- **execution_failed:** 7
- **output_mismatch:** 1
- **skipped:** 7

## Recent Failures

- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183411_execution_failed_0002) - if_else_simple/simple - 23.8s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183432_compilation_failed_0003) - type_alias/complex - 21.0s
- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183505_execution_failed_0004) - if_else_simple/complex - 32.6s
- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183542_execution_failed_0005) - module_imports/medium - 37.2s
- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183712_execution_failed_0006) - cross_module_classes/medium - 43.4s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183741_compilation_failed_0007) - type_narrowing/simple - 28.9s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183754_compilation_failed_0008) - type_narrowing/simple - 13.3s
- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_183834_execution_failed_0009) - generic_function/medium - 39.8s
- [output_mismatch](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_193144_output_mismatch_0010) - enum_usage/simple - 3170.8s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260124_193238_compilation_failed_0011) - type_narrowing/simple - 18.0s

## Recent Skips

- logical_operators/simple - Invalid expected output after 3 attempts (Python says: )
- module_imports/complex - analyzer.spy invalid per spec
- generic_function/simple - Invalid expected output after 3 attempts (Python says: )
- logical_operators/medium - Invalid expected output after 3 attempts (Python says: )
- module_imports/medium - Unsupported feature in shapes.spy: Line 46: list type annotation (v0.1.11) - 'def calculate_total_area(shapes: list[Shape]) -> f...'
