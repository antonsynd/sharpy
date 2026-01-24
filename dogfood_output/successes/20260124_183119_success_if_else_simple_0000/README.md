# Successful Dogfood Run

**Timestamp:** 2026-01-24T18:30:57.510465
**Feature Focus:** if_else_simple
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test if/else with nullable type and comparison operations
# Tests: if/else, nullable types, comparison operators, type narrowing

class ScoreEvaluator:
    passing_grade: int
    
    def __init__(self, passing: int):
        self.passing_grade = passing
    
    def evaluate(self, score: int?) -> str:
        if score is None:
            return "No score recorded"
        else:
            if score >= self.passing_grade:
                return "Pass"
            else:
                return "Fail"
    
    def get_status(self, score: int?) -> int:
        if score is None:
            return 0
        else:
            if score >= self.passing_grade:
                return 1
            else:
                return 2

def main():
    evaluator = ScoreEvaluator(60)
    
    test_score_1: int? = 75
    result_1 = evaluator.evaluate(test_score_1)
    print(result_1)
    status_1 = evaluator.get_status(test_score_1)
    print(status_1)
    
    test_score_2: int? = 45
    result_2 = evaluator.evaluate(test_score_2)
    print(result_2)
    status_2 = evaluator.get_status(test_score_2)
    print(status_2)
    
    test_score_3: int? = None
    result_3 = evaluator.evaluate(test_score_3)
    print(result_3)
    status_3 = evaluator.get_status(test_score_3)
    print(status_3)

# EXPECTED OUTPUT:
# Pass
# 1
# Fail
# 2
# No score recorded
# 0
```

## Output

```
Pass
1
Fail
2
No score recorded
0
```

## Timing

- Generation: 8.34s
- Execution: 1.62s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
