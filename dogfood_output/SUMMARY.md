# Dogfooding Summary Report

**Generated:** 2026-02-25T13:17:38.240887

## Overall Statistics

- **Total Iterations:** 100
- **Successful:** 48 (48.0%)
- **Failed:** 31 (31.0%)
- **Skipped:** 21 (21.0%)

## Issues by Type

- **compilation_failed:** 15
- **generation_failed:** 5
- **output_mismatch:** 11
- **skipped:** 21

## Recent Failures

- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_101030_compilation_failed_0021) - module_imports/complex - 849.1s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_102629_output_mismatch_0022) - module_utils/medium - 223.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_111946_compilation_failed_0023) - module_imports/complex - 1499.8s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_120547_output_mismatch_0024) - spread_call/medium - 294.1s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_120917_compilation_failed_0025) - module_utils/medium - 107.3s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_121233_compilation_failed_0026) - module_imports/complex - 195.7s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_122338_compilation_failed_0027) - null_conditional/medium - 435.1s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_123148_output_mismatch_0028) - cross_module_classes/complex - 400.2s
- [compilation_failed](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_125444_compilation_failed_0029) - module_imports/medium - 251.3s
- [output_mismatch](/home/anton/Documents/github/sharpy/dogfood_output/issues/20260225_125757_output_mismatch_0030) - class_inheritance/medium - 193.6s

## Recent Skips

- module_utils/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0203]: Type 'DataSource' has no member 'name'
  --> /tmp/tmpxc_0_4x7/main.spy:7:32
    |
  7 |     report: str = "Source: " + source.name + "\n"
    |                                ^^^^^^^^^^^
    |

error[SPY0203]: Type 'DataSource' has no member 'size'
  --> /tmp/tmpxc_0_4x7/main.spy:8:38
    |
  8 |     report = report + "Size: " + str(source.size) + "\n"
    |                                      ^^^^^^^^^^^
    |

error[SPY0203]: Type 'FileSource' has no member 'name'
  --> /tmp/tmpxc_0_4x7/main.spy:21:33
    |
 21 |         print("Source name: " + source.name)
    |                                 ^^^^^^^^^^^
    |

error[SPY0203]: Type 'FileSource' has no member 'size'
  --> /tmp/tmpxc_0_4x7/main.spy:22:37
    |
 22 |         print("Source size: " + str(source.size))
    |                                     ^^^^^^^^^^^
    |


- float_variables/complex - Non-code response after 3 attempts: Response too short (19 chars, minimum 20)
- while_loop/complex - Generation timed out after 900.0s
- type_narrowing/medium - Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot assign type 'float?' to variable of type 'float'
  --> /tmp/tmp0bakozxt/dogfood_test.spy:14:5
    |
 14 |     v: float = value.numeric_value
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


- cross_module_classes/complex - Non-code response after 3 attempts: Response too short (15 chars, minimum 20)
