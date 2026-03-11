# Dogfooding Summary Report

**Generated:** 2026-03-10T20:30:13.350053

## Overall Statistics

- **Total Iterations:** 200
- **Successful:** 95 (47.5%)
- **Failed:** 71 (35.5%)
- **Skipped:** 34 (17.0%)

## Issues by Type

- **compilation_failed:** 45
- **internal_compiler_error:** 3
- **output_mismatch:** 23
- **skipped:** 34

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_190844_compilation_failed_0061) - module_imports/complex - 404.2s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_191544_compilation_failed_0062) - loop_in_function/complex - 78.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_192614_compilation_failed_0063) - event_with_inheritance/complex - 629.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_192958_compilation_failed_0064) - cross_module_classes/complex - 224.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_193807_compilation_failed_0065) - cross_module_classes/complex - 194.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_195404_compilation_failed_0066) - maybe_expression/complex - 405.4s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_200018_compilation_failed_0067) - module_utils/complex - 373.7s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_200557_output_mismatch_0068) - module_imports/complex - 339.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_201549_compilation_failed_0069) - module_imports/complex - 161.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260310_202240_compilation_failed_0070) - module_imports/complex - 295.3s

## Recent Skips

- auto_property/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0220]: Cannot pass argument of type 'Player' to parameter of type 'Entity'
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:150:22
     |
 150 |     show_entity_info(p)
     |                      ^
     |

Validation errors:
error[SPY0411]: Property 'display_name' in 'Player' is marked @override but no matching property exists in base class 'Unit'
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:68:5
    |
 68 |     property get display_name(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0411]: Property 'category' in 'Player' is marked @override but no matching property exists in base class 'Unit'
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:72:5
    |
 72 |     property get category(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0411]: Cannot override property 'max_health' because the base class property in 'Unit' is not marked @virtual or @abstract
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:76:5
    |
 76 |     property get max_health(self) -> int:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0411]: Property 'display_name' in 'Bot' is marked @override but no matching property exists in base class 'Unit'
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:98:5
    |
 98 |     property get display_name(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0411]: Property 'category' in 'Bot' is marked @override but no matching property exists in base class 'Unit'
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:102:5
     |
 102 |     property get category(self) -> str:
     |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
     |

error[SPY0411]: Cannot override property 'max_health' because the base class property in 'Unit' is not marked @virtual or @abstract
  --> /tmp/tmp0jcfs6qv/dogfood_test.spy:106:5
     |
 106 |     property get max_health(self) -> int:
     |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
     |


- module_utils/complex - Malformed XML after 3 attempts: unclosed <code> tag
- break_continue/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0220]: Cannot assign type 'EarlyExitSearcher[int]' to variable of type 'ITraverser[int]'
  --> /tmp/tmpt67y1ock/dogfood_test.spy:81:5
    |
 81 |     searcher: ITraverser[int] = EarlyExitSearcher[int]()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'CounterTraverser[int]' to variable of type 'ITraverser[int]'
  --> /tmp/tmpt67y1ock/dogfood_test.spy:85:5
    |
 85 |     counter: ITraverser[int] = CounterTraverser[int](3)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

Validation errors:
error[SPY0418]: Covariant type parameter 'T' cannot appear in contravariant position (parameter type)
  --> /tmp/tmpt67y1ock/dogfood_test.spy:7:36
    |
  7 |     def traverse(self, items: list[T], pred: NodePredicate[T]) -> T?
    |                                    ^
    |

error[SPY0418]: Covariant type parameter 'T' cannot appear in contravariant position (parameter type)
  --> /tmp/tmpt67y1ock/dogfood_test.spy:7:60
    |
  7 |     def traverse(self, items: list[T], pred: NodePredicate[T]) -> T?
    |                                                            ^
    |


- async_with/complex - Non-code response after 3 attempts: Response too short (2 chars, minimum 20)
- async_for/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Module 'asyncio' has no member 'run'
  --> /tmp/tmp142n2d2t/dogfood_test.spy:93:5
    |
 93 |     asyncio.run(do_async_work())
    |     ^^^^^^^^^^^
    |


