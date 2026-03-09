# Dogfooding Summary Report

**Generated:** 2026-03-08T21:53:14.856169

## Overall Statistics

- **Total Iterations:** 200
- **Successful:** 91 (45.5%)
- **Failed:** 79 (39.5%)
- **Skipped:** 30 (15.0%)

## Issues by Type

- **compilation_failed:** 47
- **execution_failed:** 1
- **internal_compiler_error:** 2
- **output_mismatch:** 29
- **skipped:** 30

## Recent Failures

- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_175645_output_mismatch_0069) - function_calling_function/medium - 175.9s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_175905_output_mismatch_0070) - try_except_finally/medium - 139.7s
- [internal_compiler_error](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_182738_internal_compiler_error_0071) - lambda_multiarg/complex - 348.5s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_185706_compilation_failed_0072) - try_except_finally/complex - 447.1s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_193601_output_mismatch_0073) - collection_methods/medium - 334.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_194012_compilation_failed_0074) - cross_module_classes/medium - 250.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_202015_compilation_failed_0075) - result_unwrap/complex - 483.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_210430_compilation_failed_0076) - optional_unwrap/complex - 344.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_213304_compilation_failed_0077) - module_utils/complex - 567.4s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260308_215314_compilation_failed_0078) - module_utils/medium - 102.4s

## Recent Skips

- containment_test/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'object' to variable of type 'Item'
  --> /tmp/tmpdwo3uagq/dogfood_test.spy:23:9
    |
 23 |         other_item: Item = other
    |         ^^^^^^^^^^^^^^^^^^^^^^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'Service' has no member 'total_cost'
  --> /tmp/tmpxc0u3262/main.spy:41:11
    |
 41 |     print(s.total_cost)
    |           ^^^^^^^^^^^^
    |


- cross_module_classes/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'utils' has no exported symbol 'StringValue' (in main.spy)
  --> /tmp/tmphumloxfn/main.spy:3:45
    |
  3 | from utils import Color, Point, NamedValue, StringValue, FloatValue, Logger, Formatter, FormatFunc
    |                                             ^^^^^^^^^^^
    |

error[SPY0301]: Module 'utils' has no exported symbol 'FloatValue' (in main.spy)
  --> /tmp/tmphumloxfn/main.spy:3:58
    |
  3 | from utils import Color, Point, NamedValue, StringValue, FloatValue, Logger, Formatter, FormatFunc
    |                                                          ^^^^^^^^^^
    |

error[SPY0301]: Module 'utils' has no exported symbol 'FormatFunc' (in main.spy)
  --> /tmp/tmphumloxfn/main.spy:3:89
    |
  3 | from utils import Color, Point, NamedValue, StringValue, FloatValue, Logger, Formatter, FormatFunc
    |                                                                                         ^^^^^^^^^^
    |

Type errors:
error[SPY0202]: Type 'StringValue' not found
  --> /tmp/tmphumloxfn/main.spy:18:15
    |
 18 |     name_val: StringValue = NamedValue[str]("version", "1.0.0")
    |               ^^^^^^^^^^^
    |

error[SPY0202]: Type 'FloatValue' not found
  --> /tmp/tmphumloxfn/main.spy:19:16
    |
 19 |     float_val: FloatValue = NamedValue[float]("pi", 3.14159)
    |                ^^^^^^^^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot pass argument of type 'str' to parameter of type 'T'
  --> /tmp/tmpx1gkfbwc/main.spy:24:31
    |
 24 |     id_result: str = identity("test")
    |                               ^^^^
    |

error[SPY0220]: Cannot assign type 'T' to variable of type 'str'
  --> /tmp/tmpx1gkfbwc/main.spy:24:5
    |
 24 |     id_result: str = identity("test")
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- match_union_exhaustive/simple - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Newline
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:8:29
    |
  8 |         case Status.Pending:
    |                             ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:10:9
    |
 10 |         case Status.Running:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:12:9
    |
 12 |         case Status.Completed:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmp5_75zzm_/dogfood_test.spy:15:1
    |
 15 | def main():
    | ^
    |


