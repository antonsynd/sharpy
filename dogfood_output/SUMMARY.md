# Dogfooding Summary Report

**Generated:** 2026-02-24T06:55:38.844049

## Overall Statistics

- **Total Iterations:** 50
- **Successful:** 20 (40.0%)
- **Failed:** 18 (36.0%)
- **Skipped:** 12 (24.0%)

## Issues by Type

- **compilation_failed:** 9
- **internal_compiler_error:** 2
- **output_mismatch:** 7
- **skipped:** 12

## Recent Failures

- [internal_compiler_error](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_033650_internal_compiler_error_0008) - generator_reversed_class/medium - 394.5s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_033907_compilation_failed_0009) - module_imports/medium - 137.0s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_034625_output_mismatch_0010) - module_utils/medium - 438.4s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_034807_compilation_failed_0011) - cross_module_classes/medium - 101.9s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_035852_output_mismatch_0012) - module_utils/medium - 446.9s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_044422_compilation_failed_0013) - virtual_override/complex - 912.1s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_051649_output_mismatch_0014) - try_expression/complex - 440.6s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_055649_output_mismatch_0015) - tuple_types/medium - 691.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_060512_compilation_failed_0016) - named_tuple/complex - 502.6s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260224_062436_output_mismatch_0017) - interface_implementation/medium - 310.1s

## Recent Skips

- match_type_binding/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0104]: Expected Colon, got LeftParen
  --> /tmp/tmpmpma71uk/dogfood_test.spy:65:20
    |
 65 |         case Circle() as c:
    |                    ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpmpma71uk/dogfood_test.spy:70:9
    |
 70 |         case Rectangle() as r:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmpmpma71uk/dogfood_test.spy:75:9
    |
 75 |         case _:
    |         ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmpmpma71uk/dogfood_test.spy:78:1
    |
 78 | def check_container(obj: Container) -> str:
    | ^
    |


- property_inheritance/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got For
  --> /tmp/tmp4g16429p/dogfood_test.spy:45:5
    |
 45 |     for i in range(len(appliances)):
    |     ^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'IColorable' has no member 'set_color'. Did you mean 'get_color'?
  --> /tmp/tmpqsnbh8yh/main.spy:10:5
    |
 10 |     c.set_color("red")
    |     ^^^^^^^^^^^
    |

error[SPY0203]: Type 'ShapeType' has no member 'RECTANGLE'
  --> /tmp/tmpqsnbh8yh/main.spy:42:14
    |
 42 |         case ShapeType.RECTANGLE:
    |              ^^^^^^^^^^^^^^^^^^^
    |

error[SPY0203]: Type 'ShapeType' has no member 'CIRCLE'
  --> /tmp/tmpqsnbh8yh/main.spy:44:14
    |
 44 |         case ShapeType.CIRCLE:
    |              ^^^^^^^^^^^^^^^^
    |

error[SPY0203]: Type 'ShapeType' has no member 'SQUARE'
  --> /tmp/tmpqsnbh8yh/main.spy:46:14
    |
 46 |         case ShapeType.SQUARE:
    |              ^^^^^^^^^^^^^^^^
    |


- try_except_basic/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0200]: Undefined identifier 'be'
  --> /tmp/tmpgy9m4zd1/dogfood_test.spy:79:34
    |
 79 |             print(f"Bank error: {be.message} (code: {be.code})")
    |                                  ^^
    |

error[SPY0200]: Undefined identifier 'be'
  --> /tmp/tmpgy9m4zd1/dogfood_test.spy:79:54
    |
 79 |             print(f"Bank error: {be.message} (code: {be.code})")
    |                                                      ^^
    |

error[SPY0200]: Undefined identifier 've'
  --> /tmp/tmpgy9m4zd1/dogfood_test.spy:82:40
    |
 82 |             print(f"Validation error: {ve}")
    |                                        ^^
    |


- module_imports/complex - Generation timed out after 900.0s
