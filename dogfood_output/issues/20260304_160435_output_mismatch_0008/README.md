# Issue Report: output_mismatch

**Timestamp:** 2026-03-04T15:57:49.022126
**Type:** output_mismatch
**Feature Focus:** if_else_simple
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex if/else test: Polymorphic grading system with nested conditionals
type Score = int
type Percentage = float

@abstract
class Evaluator:
    threshold: int

    @abstract
    def evaluate(self, score: Score) -> str: ...

    @virtual
    def is_passing(self, score: Score) -> bool:
        return score >= self.threshold

class StandardEvaluator(Evaluator):
    def __init__(self):
        self.threshold = 60

    @override
    def evaluate(self, score: Score) -> str:
        if score >= 90:
            return "A"
        elif score >= 80:
            return "B"
        elif score >= 70:
            return "C"
        elif score >= 60:
            return "D"
        else:
            return "F"

class StrictEvaluator(Evaluator):
    bonus_threshold: int

    def __init__(self):
        self.threshold = 70
        self.bonus_threshold = 95

    @override
    def evaluate(self, score: Score) -> str:
        if score >= self.bonus_threshold:
            return "A+"
        elif score >= 85:
            return "A"
        elif score >= self.threshold:
            return "B"
        else:
            return "C"

    @override
    def is_passing(self, score: Score) -> bool:
        adjusted: int = score + 5
        return adjusted >= self.threshold

def calculate_average(scores: list[Score]) -> Percentage:
    total: int = 0
    count: int = 0
    for s in scores:
        total += s
        count += 1
    if count == 0:
        return 0.0
    return total / count

def main():
    standard: StandardEvaluator = StandardEvaluator()
    strict: StrictEvaluator = StrictEvaluator()
    scores: list[Score] = [45, 62, 78, 88, 94]
    evaluators: list[Evaluator] = [standard, strict]
    
    for evaluator in evaluators:
        eval_type: str = "Standard" if isinstance(evaluator, StandardEvaluator) else "Strict"
        print(eval_type)
        
        for s in scores:
            grade: str = evaluator.evaluate(s)
            status: str = "PASS" if evaluator.is_passing(s) else "FAIL"
            
            if grade in ["A", "A+"]:
                if status == "PASS":
                    print(grade)
                else:
                    print("ERROR")
            elif grade in ["B", "C"]:
                print(grade)
            else:
                result: Score = s
                if result > 0:
                    print(grade)
        
        avg: Percentage = calculate_average(scores)
        if avg > 70.0:
            print(avg)
        elif avg > 50.0 and avg <= 70.0:
            print("MED")
        else:
            print("LOW")
        
        if eval_type == "Strict":
            print("END")

```

## Error

```
AI verification backend failure
```

## Output Comparison

### Expected
```
Standard
F
D
C
B
A
A
73.4
Strict
C
B
B
A
A+
73.4
END

```

### Actual
```
Standard
F
D
C
B
A
73.4
Strict
C
C
B
A
A
73.4
END
```

## Timing

- Generation: 89.79s
- Execution: 5.07s
