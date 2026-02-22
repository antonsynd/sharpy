# Dogfooding Summary Report

**Generated:** 2026-02-21T06:28:53.509058

## Overall Statistics

- **Total Iterations:** 50
- **Successful:** 15 (30.0%)
- **Failed:** 22 (44.0%)
- **Skipped:** 13 (26.0%)

## Issues by Type

- **compilation_failed:** 12
- **execution_failed:** 1
- **output_mismatch:** 9
- **skipped:** 13

## Recent Failures

- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_032708_output_mismatch_0012) - class_with_init/medium - 89.8s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_034853_output_mismatch_0013) - builtin_aggregation/simple - 210.7s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_040010_output_mismatch_0014) - cross_module_classes/medium - 676.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_040827_compilation_failed_0015) - cross_module_classes/medium - 496.9s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_044855_output_mismatch_0016) - module_utils/complex - 854.9s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_045116_compilation_failed_0017) - module_imports/complex - 140.9s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_045244_output_mismatch_0018) - module_utils/medium - 87.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_053014_compilation_failed_0019) - module_imports/medium - 116.5s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_055424_compilation_failed_0020) - struct_definition/simple - 34.3s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260221_062700_output_mismatch_0021) - enum_definition/complex - 627.3s

## Recent Skips

- cross_module_classes/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: DoubleStar
  --> /tmp/tmp3n584i_5/main.spy:55:1
    |
 55 | **Fix:** Removed the `IColorable` interface that was causing the export error.
    | ^^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:55:50
    |
 55 | **Fix:** Removed the `IColorable` interface that was causing the export error.
    |                                                  ^^^
    |

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:57:5
    |
 57 | The issue was that `IColorable` interface was defined in `shapes_base` but the compiler was not recognizing it as an exported symbol. Instead of trying to debug the interface export mechanism:
    |     ^^^^^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:57:47
    |
 57 | The issue was that `IColorable` interface was defined in `shapes_base` but the compiler was not recognizing it as an exported symbol. Instead of trying to debug the interface export mechanism:
    |                                               ^^^^^^^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:57:183
    |
 57 | The issue was that `IColorable` interface was defined in `shapes_base` but the compiler was not recognizing it as an exported symbol. Instead of trying to debug the interface export mechanism:
    |                                                                                                                                                                                       ^^^^^^^^^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmp3n584i_5/main.spy:59:4
    |
 59 | 1. **Removed `IColorable` interface entirely** from `shapes_base.spy`
    |    ^^
    |

error[SPY0104]: Expected Colon, got DoubleStar
  --> /tmp/tmp3n584i_5/main.spy:59:45
    |
 59 | 1. **Removed `IColorable` interface entirely** from `shapes_base.spy`
    |                                             ^^
    |

error[SPY0104]: Expected Import, got Newline
  --> /tmp/tmp3n584i_5/main.spy:59:70
    |
 59 | 1. **Removed `IColorable` interface entirely** from `shapes_base.spy`
    |                                                                      ^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmp3n584i_5/main.spy:60:4
    |
 60 | 2. **Moved `get_color` and `set_color` methods** directly into `Rectangle` class as regular methods
    |    ^^
    |

error[SPY0101]: Expected identifier, got As
  --> /tmp/tmp3n584i_5/main.spy:60:82
    |
 60 | 2. **Moved `get_color` and `set_color` methods** directly into `Rectangle` class as regular methods
    |                                                                                  ^^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmp3n584i_5/main.spy:61:4
    |
 61 | 3. **Updated imports** - removed `IColorable` from the import statements in `shapes_impl.spy` and `main.spy`
    |    ^^
    |

error[SPY0102]: Expected newline, got In
  --> /tmp/tmp3n584i_5/main.spy:61:74
    |
 61 | 3. **Updated imports** - removed `IColorable` from the import statements in `shapes_impl.spy` and `main.spy`
    |                                                                          ^^
    |

error[SPY0101]: Expected identifier, got DoubleStar
  --> /tmp/tmp3n584i_5/main.spy:62:4
    |
 62 | 4. **Updated color access** in `main.spy` to use regular method calls (`rect.get_color()` and `rect.set_color("Blue")`) instead of interface methods
    |    ^^
    |

error[SPY0104]: Expected Colon, got Newline
  --> /tmp/tmp3n584i_5/main.spy:62:149
    |
 62 | 4. **Updated color access** in `main.spy` to use regular method calls (`rect.get_color()` and `rect.set_color("Blue")`) instead of interface methods
    |                                                                                                                                                     ^
    |

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:64:6
    |
 64 | This maintains the same functionality while avoiding the interface export issue entirely.
    |      ^^^^^^^^^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:64:54
    |
 64 | This maintains the same functionality while avoiding the interface export issue entirely.
    |                                                      ^^^
    |

error[SPY0104]: Expected Colon, got Identifier
  --> /tmp/tmp3n584i_5/main.spy:64:75
    |
 64 | This maintains the same functionality while avoiding the interface export issue entirely.
    |                                                                           ^^^^^
    |


- if_else_simple/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0248]: Cannot override 'execute' because the base class method in 'Task' is not marked @virtual or @abstract. Add @virtual to the method in the base class.
  --> /tmp/tmpgdd4adxt/dogfood_test.spy:30:5
    |
 30 |     def execute(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0248]: Cannot override 'estimate_time' because the base class method in 'Task' is not marked @virtual or @abstract. Add @virtual to the method in the base class.
  --> /tmp/tmpgdd4adxt/dogfood_test.spy:44:5
    |
 44 |     def estimate_time(self) -> int:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0248]: Cannot override 'execute' because the base class method in 'Task' is not marked @virtual or @abstract. Add @virtual to the method in the base class.
  --> /tmp/tmpgdd4adxt/dogfood_test.spy:54:5
    |
 54 |     def execute(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- module_imports/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'list[Shape]' to variable of type 'list[IMeasurable]'
  --> /tmp/tmpacv7czel/main.spy:24:5
    |
 24 |     shapes: list[IMeasurable] = [circle, rect, tri]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- match_guard/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got For
  --> /tmp/tmpqksnyg_o/dogfood_test.spy:44:5
    |
 44 |     for r in results:
    |     ^^^
    |


- optional_unwrap/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0244]: 'None()' can only construct Optional types, not 'str'
  --> /tmp/tmpm75tce1h/dogfood_test.spy:41:26
    |
 41 |             self.cache = None()
    |                          ^^^^^^
    |

error[SPY0230]: 'Some' must be called as a function, e.g. 'Some(value)'
  --> /tmp/tmpm75tce1h/dogfood_test.spy:81:47
    |
 81 |     cached: CachedSource = CachedSource(base, Some("CACHED_VALUE"))
    |                                               ^^^^
    |

error[SPY0244]: 'None()' can only construct Optional types, not 'CachedSource'
  --> /tmp/tmpm75tce1h/dogfood_test.spy:95:55
    |
 95 |     cached2: CachedSource = CachedSource(long_source, None())
    |                                                       ^^^^^^
    |


