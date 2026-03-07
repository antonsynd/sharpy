# Dogfooding Summary Report

**Generated:** 2026-03-07T08:14:46.178023

## Overall Statistics

- **Total Iterations:** 200
- **Successful:** 103 (51.5%)
- **Failed:** 63 (31.5%)
- **Skipped:** 34 (17.0%)

## Issues by Type

- **compilation_failed:** 35
- **execution_failed:** 2
- **internal_compiler_error:** 3
- **output_mismatch:** 23
- **skipped:** 34

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_041152_compilation_failed_0053) - maybe_expression/complex - 631.2s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_042018_output_mismatch_0054) - module_utils/medium - 505.4s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_045426_compilation_failed_0055) - module_utils/complex - 862.9s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_050116_compilation_failed_0056) - module_utils/complex - 123.7s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_060122_output_mismatch_0057) - nullable_types/medium - 182.8s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_062213_output_mismatch_0058) - cross_module_classes/complex - 247.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_064350_compilation_failed_0059) - cross_module_classes/complex - 210.4s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_064822_output_mismatch_0060) - star_unpacking/complex - 272.1s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_071014_output_mismatch_0061) - spread_with_comprehension/complex - 532.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260307_074244_compilation_failed_0062) - cross_module_classes/complex - 148.8s

## Recent Skips

- bool_variables/complex - Malformed XML after 3 attempts: unclosed <code> tag
- cross_module_classes/complex - Malformed XML after 3 attempts: unclosed <code> tag
- operator_section/simple - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0237]: Type parameter 'TOut' cannot be inferred; no arguments provide type information. Use explicit syntax: map[TIn, TOut](...)
  --> /tmp/tmps31vz1dd/dogfood_test.spy:5:14
    |
  5 |     result = map(lambda x: x / 10, values)
    |              ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- module_utils/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot pass argument of type 'Stack[int]' to parameter of type 'Stack[T]'
  --> /tmp/tmpurwv7hq6/main.spy:13:27
    |
 13 |     print(stack_to_string(int_stack))
    |                           ^^^^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'Stack[int]' to parameter of type 'Stack[T]'
  --> /tmp/tmpurwv7hq6/main.spy:18:27
    |
 18 |     print(stack_to_string(reversed_stack))
    |                           ^^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'Stack[ComparableItem]' to parameter of type 'Stack[T]'
  --> /tmp/tmpurwv7hq6/main.spy:40:27
    |
 40 |     print(stack_to_string(item_stack))
    |                           ^^^^^^^^^^
    |


- module_utils/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'Counter' has no member 'value'. Did you mean '_value'?
  --> /tmp/tmpy73jcfjj/main.spy:23:17
    |
 23 |     val1: int = counter.value
    |                 ^^^^^^^^^^^^^
    |

error[SPY0203]: Type 'Counter' has no member 'value'. Did you mean '_value'?
  --> /tmp/tmpy73jcfjj/main.spy:29:17
    |
 29 |     val2: int = counter.value
    |                 ^^^^^^^^^^^^^
    |


