# Dogfooding Summary Report

**Generated:** 2026-02-11T23:44:47.455809

## Overall Statistics

- **Total Iterations:** 10
- **Successful:** 1 (10.0%)
- **Failed:** 2 (20.0%)
- **Skipped:** 7 (70.0%)

## Issues by Type

- **compilation_failed:** 2
- **skipped:** 7

## Recent Failures

- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260211_234317_compilation_failed_0000) - raise_exception/simple - 9.3s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260211_234424_compilation_failed_0001) - optional_unwrap/simple - 8.6s

## Recent Skips

- module_imports/complex - Sharpy compiler error in data_models.spy: Compilation errors:

error[SPY0102]: Expected newline, got Dedent
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp3lb3grd_/dogfood_test.spy:46:18
    |
 46 |     DELIVERED = 4
    |                  ^
    |

error[SPY0104]: Expected Dedent, got Eof
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp3lb3grd_/dogfood_test.spy:46:18
    |
 46 |     DELIVERED = 4
    |                  ^
    |


- module_utils/medium - Sharpy compiler error in math_utils.spy: Compilation errors:

error[SPY0403]: Entry point file requires a 'main()' function
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp1wew0j_k/dogfood_test.spy:3:1
    |
  3 | def is_prime(n: int) -> bool:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- cross_module_classes/complex - Sharpy compiler error in shapes.spy: Compilation errors:

error[SPY0403]: Entry point file requires a 'main()' function
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp5v2l2h4h/dogfood_test.spy:3:1
    |
  3 | @abstract
    | ^^^^^^^^^
    |


- module_imports/medium - Sharpy compiler error in math_utils.spy: Compilation errors:

error[SPY0403]: Entry point file requires a 'main()' function
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmp1e4_7khg/dogfood_test.spy:3:1
    |
  3 | def factorial(n: int) -> int:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- module_imports/complex - Sharpy compiler error in geometry.spy: Compilation errors:

error[SPY0403]: Entry point file requires a 'main()' function
  --> /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/tmpxn7iiub4/dogfood_test.spy:3:1
    |
  3 | interface IDrawable:
    | ^^^^^^^^^^^^^^^^^^^^
    |


