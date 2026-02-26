# Dogfooding Summary Report

**Generated:** 2026-02-26T11:02:04.605469

## Overall Statistics

- **Total Iterations:** 100
- **Successful:** 43 (43.0%)
- **Failed:** 35 (35.0%)
- **Skipped:** 22 (22.0%)

## Issues by Type

- **compilation_failed:** 18
- **generation_failed:** 6
- **output_mismatch:** 11
- **skipped:** 22

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_073601_compilation_failed_0025) - result_type/medium - 62.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_073756_compilation_failed_0026) - cross_module_classes/medium - 114.3s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_074934_output_mismatch_0027) - module_utils/medium - 177.1s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_083350_compilation_failed_0028) - tuple_unpacking_nested/complex - 507.4s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_083856_compilation_failed_0029) - dunder_reversed/medium - 306.7s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_085240_output_mismatch_0030) - class_with_loop/medium - 200.2s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_085551_output_mismatch_0031) - module_imports/medium - 190.8s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_092307_compilation_failed_0032) - cross_module_classes/medium - 165.9s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_094318_compilation_failed_0033) - module_utils/medium - 454.6s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260226_101412_compilation_failed_0034) - generator_yield_from/complex - 164.9s

## Recent Skips

- module_imports/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'contracts' has no exported symbol 'ITransformable' (in main.spy)
  --> /tmp/tmpnhlrgmbv/main.spy:3:46
    |
  3 | from contracts import IEntity, IValidatable, ITransformable
    |                                              ^^^^^^^^^^^^^^
    |

Type errors:
error[SPY0202]: Type 'ITransformable' not found
  --> /tmp/tmpnhlrgmbv/main.spy:41:20
    |
 41 |     transformable: ITransformable = circle
    |                    ^^^^^^^^^^^^^^
    |


- dunder_iter/complex - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0229]: Cannot assign 'None' to 'TreeNode?'. 'None' is the C# null literal. Did you mean 'None()' to construct an empty Optional?
  --> /tmp/tmpmw82hwwl/dogfood_test.spy:10:22
    |
 10 |         self._left = None
    |                      ^^^^
    |

error[SPY0229]: Cannot assign 'None' to 'TreeNode?'. 'None' is the C# null literal. Did you mean 'None()' to construct an empty Optional?
  --> /tmp/tmpmw82hwwl/dogfood_test.spy:11:23
    |
 11 |         self._right = None
    |                       ^^^^
    |


- cross_module_classes/medium - Repeated identical compiler error (likely compiler bug): Compilation errors:

error[SPY0202]: Base type 'Shape' not found
  --> /tmp/tmpxioeq3mm/main.spy:5:1
    |
  5 | class Rectangle(Shape, IDrawable):
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0202]: Base type 'Shape' not found
  --> /tmp/tmpxioeq3mm/main.spy:26:1
    |
 26 | class Circle(Shape, IDrawable):
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'types' has no exported symbol 'ShapeType' (in main.spy)
  --> /tmp/tmp3qhssy7f/main.spy:3:19
    |
  3 | from types import ShapeType, IShape
    |                   ^^^^^^^^^
    |

error[SPY0301]: Module 'types' has no exported symbol 'ShapeType' (in shapes.spy)
  --> /tmp/tmp3qhssy7f/shapes.spy:3:13
    |
  3 | from types import ShapeType, IShape
    |             ^^^^^^^^^
    |

Type errors:
error[SPY0200]: Undefined identifier 'ShapeType'
  --> /tmp/tmp3qhssy7f/main.spy:25:38
    |
 25 |     is_rect: bool = r1.shape_type == ShapeType.RECTANGLE
    |                                      ^^^^^^^^^
    |


- module_utils/complex - Sharpy compiler error after 3 attempts: Compilation errors:

Import resolution errors:
error[SPY0301]: Module 'geometry' has no exported symbol 'Color' (in main.spy)
  --> /tmp/tmpopj6yspw/main.spy:2:29
    |
  2 | from geometry import Point, Color
    |                             ^^^^^
    |

error[SPY0301]: Module 'geometry' has no exported symbol 'Color' (in shapes.spy)
  --> /tmp/tmpopj6yspw/shapes.spy:3:32
    |
  3 | from shapes import Circle, Rectangle
    |                                ^^^^^
    |

error[SPY0301]: Module 'geometry' has no exported symbol 'Color' (in utils.spy)
  --> /tmp/tmpopj6yspw/utils.spy:2:22
    |
  2 | from geometry import Point, Color
    |                      ^^^^^
    |

Type errors:
error[SPY0200]: Undefined identifier 'Color'
  --> /tmp/tmpopj6yspw/main.spy:41:51
    |
 41 |     circle: Circle = Circle(Point(0.0, 0.0), 5.0, Color.RED)
    |                                                   ^^^^^
    |

error[SPY0200]: Undefined identifier 'Color'
  --> /tmp/tmpopj6yspw/main.spy:42:64
    |
 42 |     rect: Rectangle = Rectangle(Point(10.0, 10.0), 20.0, 30.0, Color.GREEN)
    |                                                                ^^^^^
    |

error[SPY0200]: Undefined identifier 'Color'
  --> /tmp/tmpopj6yspw/main.spy:58:33
    |
 58 |     print(f"Red hex: {hex_color(Color.RED)}")
    |                                 ^^^^^
    |

error[SPY0200]: Undefined identifier 'Color'
  --> /tmp/tmpopj6yspw/main.spy:61:11
    |
 61 |     print(Color.BLUE)
    |           ^^^^^
    |


