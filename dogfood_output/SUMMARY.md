# Dogfooding Summary Report

**Generated:** 2026-03-05T02:45:20.784903

## Overall Statistics

- **Total Iterations:** 200
- **Successful:** 28 (14.0%)
- **Failed:** 19 (9.5%)
- **Skipped:** 153 (76.5%)

## Issues by Type

- **compilation_failed:** 10
- **internal_compiler_error:** 1
- **output_mismatch:** 8
- **skipped:** 153

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_170048_compilation_failed_0009) - module_utils/complex - 358.7s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_180005_output_mismatch_0010) - module_imports/complex - 409.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_180241_compilation_failed_0011) - module_utils/medium - 156.1s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_180537_compilation_failed_0012) - cross_module_classes/complex - 176.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_181413_compilation_failed_0013) - module_utils/complex - 515.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_184126_compilation_failed_0014) - class_instance_methods/medium - 157.6s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_191638_output_mismatch_0015) - set_literal/complex - 757.0s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_193532_compilation_failed_0016) - cross_module_classes/complex - 1134.2s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_194644_output_mismatch_0017) - module_imports/medium - 217.4s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260304_202256_output_mismatch_0018) - float_variables/medium - 132.7s

## Recent Skips

- class_instance_methods/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmplv7nuz6s/dogfood_test.spy:1:9
    |
  1 | Request timed out. (request_id=req_95f18856c8d6)
    |         ^^^^^
    |


- spread_call/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpvwavhyvm/dogfood_test.spy:1:9
    |
  1 | Request timed out. (request_id=req_b8da5248ae16)
    |         ^^^^^
    |


- simple_function/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmphyfhc312/dogfood_test.spy:1:9
    |
  1 | Request timed out. (request_id=req_fde868303b8d)
    |         ^^^^^
    |


- module_imports/complex - Failed to parse multi-file response after 3 attempts
- match_expression/simple - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpl2t4o2ph/dogfood_test.spy:1:9
    |
  1 | Request timed out. (request_id=req_a4c5c8f2cf11)
    |         ^^^^^
    |


