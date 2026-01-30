# Dogfooding Summary Report

**Generated:** 2026-01-29T20:18:51.644780

## Overall Statistics

- **Total Iterations:** 10
- **Successful:** 3 (30.0%)
- **Failed:** 2 (20.0%)
- **Skipped:** 5 (50.0%)

## Issues by Type

- **execution_failed:** 2
- **skipped:** 5

## Recent Failures

- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260129_201723_execution_failed_0000) - generic_function/simple - 19.4s
- [execution_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260129_201758_execution_failed_0001) - arithmetic_operators/simple - 14.1s

## Recent Skips

- module_imports/complex - Unsupported feature in geometry.spy: Line 27: with statement (not implemented) - 'return f"Circle with radius {self.radius}"...'
- cross_module_classes/complex - Unsupported feature in shapes.spy: Line 32: with statement (not implemented) - 'return f"A {self.color} circle with radius {self.r...'
- cross_module_classes/medium - Unsupported feature in shapes.spy: Line 31: with statement (not implemented) - 'return f"Circle '{self.name}' with radius {self.ra...'
- cross_module_classes/complex - main.spy invalid per spec
- f_string_expressions/medium - Invalid expected output after 3 attempts (Python says: Calculator initialized 7 and 3
Sum: 10
Product: 21
Expression: 7 + 3 = 10
Price per item: 19.99
Quantity: 5
Subtotal: 99.95
Tax (8%): 8.00
Total: 107.95
Padded number: 00042)
