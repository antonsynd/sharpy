# Dogfooding Summary Report

**Generated:** 2026-03-03T11:17:30.747956

## Overall Statistics

- **Total Iterations:** 100
- **Successful:** 41 (41.0%)
- **Failed:** 40 (40.0%)
- **Skipped:** 19 (19.0%)

## Issues by Type

- **compilation_failed:** 24
- **output_mismatch:** 16
- **skipped:** 19

## Recent Failures

- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_074724_output_mismatch_0030) - access_modifiers/medium - 496.6s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_080133_output_mismatch_0031) - cross_module_classes/medium - 189.4s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_082352_output_mismatch_0032) - generator_basic/medium - 227.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_082611_compilation_failed_0033) - match_type_pattern/medium - 139.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_084959_compilation_failed_0034) - module_utils/medium - 172.4s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_090345_output_mismatch_0035) - result_type/complex - 401.4s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_095915_output_mismatch_0036) - cross_module_classes/complex - 894.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_103851_compilation_failed_0037) - cross_module_classes/medium - 644.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_105525_compilation_failed_0038) - cross_module_classes/medium - 994.4s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260303_111730_compilation_failed_0039) - nullable_types/complex - 448.7s

## Recent Skips

- match_literal/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'int' to variable of type 'int'
  --> /tmp/tmpwneyhkdx/dogfood_test.spy:19:13
    |
 19 |             n: int = raw_value
    |             ^^^^^^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'str' to variable of type 'str'
  --> /tmp/tmpwneyhkdx/dogfood_test.spy:29:13
    |
 29 |             s: str = raw_value
    |             ^^^^^^^^^^^^^^^^^^
    |


- result_type/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0200]: Undefined identifier 'val'. Did you mean 'all'?
  --> /tmp/tmpv5yt4qo4/dogfood_test.spy:13:8
    |
 13 |     if val > 0:
    |        ^^^
    |

error[SPY0200]: Undefined identifier 'val'. Did you mean 'all'?
  --> /tmp/tmpv5yt4qo4/dogfood_test.spy:14:19
    |
 14 |         return Ok(val)
    |                   ^^^
    |

error[SPY0227]: Cannot infer type for 'Ok()' without a type annotation. Add a type annotation like 'x: int !str = Ok(value)'
  --> /tmp/tmpv5yt4qo4/dogfood_test.spy:41:15
    |
 41 |     chained = Ok(5).map(lambda x: x + 1).map(lambda x: x * 2)
    |               ^^^^^
    |


- module_imports/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'list[Shape]' to variable of type 'list[IMeasurable]'
  --> /tmp/tmpfu4l4os4/main.spy:25:5
    |
 25 |     all_shapes: list[IMeasurable] = [red_circle, blue_rect]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0203]: Type 'Circle' has no member 'area'
  --> /tmp/tmpfu4l4os4/main.spy:30:11
    |
 30 |     print(red_circle.area)
    |           ^^^^^^^^^^^^^^^
    |

error[SPY0203]: Type 'Rectangle' has no member 'area'
  --> /tmp/tmpfu4l4os4/main.spy:31:11
    |
 31 |     print(blue_rect.area)
    |           ^^^^^^^^^^^^^^
    |


- module_utils/complex - Repeated identical compiler error (likely compiler bug): Compilation errors:

error[SPY0203]: Type 'PositionedShape' has no member 'x'
  --> /tmp/tmp6osktqww/main.spy:41:11
    |
 41 |     print(positioned.x)
    |           ^^^^^^^^^^^^
    |

error[SPY0203]: Type 'PositionedShape' has no member 'y'
  --> /tmp/tmp6osktqww/main.spy:42:11
    |
 42 |     print(positioned.y)
    |           ^^^^^^^^^^^^
    |

error[SPY0203]: Type 'PositionedShape' has no member 'x'
  --> /tmp/tmp6osktqww/main.spy:46:11
    |
 46 |     print(positioned.x)
    |           ^^^^^^^^^^^^
    |

error[SPY0203]: Type 'PositionedShape' has no member 'y'
  --> /tmp/tmp6osktqww/main.spy:47:11
    |
 47 |     print(positioned.y)
    |           ^^^^^^^^^^^^
    |


- cross_module_classes/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'Inventory' has no member 'location'
  --> /tmp/tmp0z0ji3iw/main.spy:22:5
    |
 22 |     inv.location = Point(10.0, 20.0)
    |     ^^^^^^^^^^^^
    |


