# Dogfooding Summary Report

**Generated:** 2026-01-26T23:54:57.685817

## Overall Statistics

- **Total Iterations:** 10
- **Successful:** 5 (50.0%)
- **Failed:** 3 (30.0%)
- **Skipped:** 2 (20.0%)

## Issues by Type

- **compilation_failed:** 2
- **output_mismatch:** 1
- **skipped:** 2

## Recent Failures

- [output_mismatch](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260126_235156_output_mismatch_0000) - for_range_single/medium - 29.0s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260126_235257_compilation_failed_0001) - module_utils/medium - 43.0s
- [compilation_failed](/Users/anton/Documents/github/sharpy/dogfood_output/issues/20260126_235314_compilation_failed_0002) - collection_methods/simple - 17.2s

## Recent Skips

- cross_module_classes/medium - Unsupported feature in models.spy: Line 25: ternary expression (not fully supported) - 'status: str = "Available" if self.available else "...'
- cross_module_classes/complex - Unsupported feature in game_entities.spy: Line 59: ternary expression (not fully supported) - 'status: str = "collected" if self.is_collected els...'
