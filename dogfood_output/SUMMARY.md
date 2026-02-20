# Dogfooding Summary Report

**Generated:** 2026-02-19T21:26:22.737294

## Overall Statistics

- **Total Iterations:** 100
- **Successful:** 41 (41.0%)
- **Failed:** 42 (42.0%)
- **Skipped:** 17 (17.0%)

## Issues by Type

- **compilation_failed:** 23
- **generation_failed:** 2
- **output_mismatch:** 17
- **skipped:** 17

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_062533_compilation_failed_0032) - dotnet_type_usage/medium - 322.7s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_063253_output_mismatch_0033) - cross_module_classes/complex - 440.2s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_064929_compilation_failed_0034) - class_inheritance/simple - 791.0s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_065755_compilation_failed_0035) - cross_module_classes/medium - 505.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_075652_compilation_failed_0036) - module_utils/medium - 162.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_080553_compilation_failed_0037) - module_utils/complex - 540.5s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_081856_compilation_failed_0038) - module_imports/medium - 97.0s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_082130_compilation_failed_0039) - module_imports/complex - 153.9s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_083043_compilation_failed_0040) - dunder_str/complex - 299.9s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260219_085300_output_mismatch_0041) - module_imports/complex - 418.0s

## Recent Skips

- cross_module_classes/medium - Failed to parse multi-file response after 3 attempts
- module_utils/medium - Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'utils' has no exported symbol 'UserId' (in main.spy)
  --> /tmp/tmp9prhkt4d/main.spy:2:86
    |
  2 | from utils import DataTransformer, filter_positive, calculate_average, format_value, UserId
    |                                                                                      ^^^^^^
    |

Type errors:
error[SPY0202]: Type 'UserId' not found
  --> /tmp/tmp9prhkt4d/main.spy:26:14
    |
 26 |     user_id: UserId = 42
    |              ^^^^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'interfaces' has no exported symbol 'IDrawable' (in data_structures.spy)
  --> /tmp/tmp3ewqf2nn/data_structures.spy:2:13
    |
  2 | from data_structures import Rectangle, Circle, Point, ShapeType
    |             ^^^^^^^^^
    |

error[SPY0301]: Module 'interfaces' has no exported symbol 'IDrawable' (in main.spy)
  --> /tmp/tmp3ewqf2nn/main.spy:4:24
    |
  4 | from interfaces import IDrawable
    |                        ^^^^^^^^^
    |

Type errors:
error[SPY0220]: Cannot pass argument of type 'Rectangle' to parameter of type 'IDrawable'
  --> /tmp/tmp3ewqf2nn/main.spy:21:27
    |
 21 |     print(renderer.render(rect))
    |                           ^^^^
    |

error[SPY0220]: Cannot pass argument of type 'Circle' to parameter of type 'IDrawable'
  --> /tmp/tmp3ewqf2nn/main.spy:22:27
    |
 22 |     print(renderer.render(circle))
    |                           ^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'Rectangle' to parameter of type 'IDrawable'
  --> /tmp/tmp3ewqf2nn/main.spy:25:48
    |
 25 |     larger: IDrawable = renderer.compare_items(rect, circle)
    |                                                ^^^^
    |

error[SPY0220]: Cannot pass argument of type 'Circle' to parameter of type 'IDrawable'
  --> /tmp/tmp3ewqf2nn/main.spy:25:54
    |
 25 |     larger: IDrawable = renderer.compare_items(rect, circle)
    |                                                      ^^^^^^
    |

error[SPY0202]: Type 'IDrawable' not found
  --> /tmp/tmp3ewqf2nn/main.spy:25:13
    |
 25 |     larger: IDrawable = renderer.compare_items(rect, circle)
    |             ^^^^^^^^^
    |


- interface_definition/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmpj9grb3a5/dogfood_test.spy:1:9
    |
  1 | Request timed out.
    |         ^^^^^
    |


- cross_module_classes/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: DoubleStar
  --> /tmp/tmpnqsfxfkd/main.spy:77:1
    |
 77 | **Summary of fixes made:**
    | ^^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmpnqsfxfkd/main.spy:79:4
    |
 79 | 1. **Added missing `__init__` to `Dimensions` struct** (`geometry.spy:30-36`): The struct was being called as `Dimensions(width, height)` in `Rectangle.__init__`, but structs require an explicit constructor.
    |    ^^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmpnqsfxfkd/main.spy:79:53
    |
 79 | 1. **Added missing `__init__` to `Dimensions` struct** (`geometry.spy:30-36`): The struct was being called as `Dimensions(width, height)` in `Rectangle.__init__`, but structs require an explicit constructor.
    |                                                     ^^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmpnqsfxfkd/main.spy:79:95
    |
 79 | 1. **Added missing `__init__` to `Dimensions` struct** (`geometry.spy:30-36`): The struct was being called as `Dimensions(width, height)` in `Rectangle.__init__`, but structs require an explicit constructor.
    |                                                                                               ^^^^^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmpnqsfxfkd/main.spy:81:4
    |
 81 | 2. **Fixed incorrect use of `@override` on interface implementations** (`shapes.spy:63` and `shapes.spy:103`): The `measure()` method comes from the `IMeasurable` interface, not a base class. Per the rules: "@override is ONLY for overriding @virtual or @abstract methods from base classes" - interface implementations should NOT use `@override`.
    |    ^^
    |

error[SPY0104]: Expected Colon, got DoubleStar
  --> /tmp/tmpnqsfxfkd/main.spy:81:69
    |
 81 | 2. **Fixed incorrect use of `@override` on interface implementations** (`shapes.spy:63` and `shapes.spy:103`): The `measure()` method comes from the `IMeasurable` interface, not a base class. Per the rules: "@override is ONLY for overriding @virtual or @abstract methods from base classes" - interface implementations should NOT use `@override`.
    |                                                                     ^^
    |

error[SPY0104]: Expected Import, got Identifier
  --> /tmp/tmpnqsfxfkd/main.spy:81:150
    |
 81 | 2. **Fixed incorrect use of `@override` on interface implementations** (`shapes.spy:63` and `shapes.spy:103`): The `measure()` method comes from the `IMeasurable` interface, not a base class. Per the rules: "@override is ONLY for overriding @virtual or @abstract methods from base classes" - interface implementations should NOT use `@override`.
    |                                                                                                                                                      ^^^^^^^^^^^
    |

error[SPY0101]: Expected identifier, got Comma
  --> /tmp/tmpnqsfxfkd/main.spy:81:173
    |
 81 | 2. **Fixed incorrect use of `@override` on interface implementations** (`shapes.spy:63` and `shapes.spy:103`): The `measure()` method comes from the `IMeasurable` interface, not a base class. Per the rules: "@override is ONLY for overriding @virtual or @abstract methods from base classes" - interface implementations should NOT use `@override`.
    |                                                                                                                                                                             ^
    |

error[SPY0101]: Expected identifier, got Dot
  --> /tmp/tmpnqsfxfkd/main.spy:81:191
    |
 81 | 2. **Fixed incorrect use of `@override` on interface implementations** (`shapes.spy:63` and `shapes.spy:103`): The `measure()` method comes from the `IMeasurable` interface, not a base class. Per the rules: "@override is ONLY for overriding @virtual or @abstract methods from base classes" - interface implementations should NOT use `@override`.
    |                                                                                                                                                                                               ^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmpnqsfxfkd/main.spy:81:319
    |
 81 | 2. **Fixed incorrect use of `@override` on interface implementations** (`shapes.spy:63` and `shapes.spy:103`): The `measure()` method comes from the `IMeasurable` interface, not a base class. Per the rules: "@override is ONLY for overriding @virtual or @abstract methods from base classes" - interface implementations should NOT use `@override`.
    |                                                                                                                                                                                                                                                                                                                               ^^^^^^
    |


