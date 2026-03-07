# Skipped Dogfood Run

**Timestamp:** 2026-03-06T23:38:04.000156
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0100]: Unexpected token: Newline
  --> /tmp/tmp4uej5j2r/dogfood_test.spy:21:24
    |
 21 |             case >= 90:
    |                        ^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp4uej5j2r/dogfood_test.spy:23:13
    |
 23 |             case >= 80:
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp4uej5j2r/dogfood_test.spy:25:13
    |
 25 |             case >= 70:
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp4uej5j2r/dogfood_test.spy:27:13
    |
 27 |             case >= 60:
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Case
  --> /tmp/tmp4uej5j2r/dogfood_test.spy:29:13
    |
 29 |             case _:
    |             ^^^^
    |

error[SPY0100]: Unexpected token: Dedent
  --> /tmp/tmp4uej5j2r/dogfood_test.spy:36:1
    |
 36 | class CurvedScale(GradingScale):
    | ^
    |


**Feature Focus:** match_relational_pattern
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Match relational patterns with class hierarchy and type narrowing
# This tests relational patterns: >, >=, <, <= combined with virtual dispatch

from abc import ABC

@abstract
class GradingScale:
    @abstract
    def points_to_grade(self, points: int) -> str:
        pass
    
    @abstract
    def is_passing(self, points: int) -> bool:
        pass

class StandardScale(GradingScale):
    @override
    def points_to_grade(self, points: int) -> str:
        # Relational patterns in match expression
        return match points:
            case >= 90:
                "A"
            case >= 80:
                "B"
            case >= 70:
                "C"
            case >= 60:
                "D"
            case _:
                "F"
    
    @override
    def is_passing(self, points: int) -> bool:
        return points >= 60

class CurvedScale(GradingScale):
    curve: int
    
    def __init__(self, curve: int):
        self.curve = curve
    
    @override
    def points_to_grade(self, points: int) -> str:
        adjusted: int = points + self.curve
        return match adjusted:
            case >= 85:
                "A"
            case >= 70:
                "B"
            case >= 55:
                "C"
            case >= 40:
                "D"
            case _:
                "F"
    
    @override
    def is_passing(self, points: int) -> bool:
        return points + self.curve >= 40

enum CourseType:
    MATH = 1
    SCIENCE = 2
    ENGLISH = 3

def evaluate_student(name: str, score: int, scale: GradingScale, course: CourseType) -> None:
    grade: str = scale.points_to_grade(score)
    passing: bool = scale.is_passing(score)
    
    # Relational patterns with match on score directly
    performance: str = match score:
        case >= 95:
            "Exceptional"
        case >= 85:
            "Strong"
        case >= 75:
            "Adequate"
        case >= 65:
            "Developing"
        case _:
            "Needs Support"
    
    print(f"{name}")
    print(f"Score: {score}")
    print(f"Grade: {grade}")
    print(f"Performance: {performance}")
    print(f"Passing: {passing}")

def test_standard_scale() -> None:
    print("Standard Scale Results:")
    scores: list[int] = [95, 87, 72, 58, 45]
    for score in scores:
        evaluate_student(f"Student{score}", score, StandardScale(), CourseType.MATH)

def test_curved_scale() -> None:
    print("")
    print("Curved Scale Results (curve=10):")
    scores: list[int] = [88, 78, 55, 35]
    for score in scores:
        evaluate_student(f"Student{score}", score, CurvedScale(10), CourseType.SCIENCE)

def test_guard_clause() -> None:
    print("")
    target: int = 75
    result: str = match target:
        case >= 70 if target < 100:
            "In range 70-99"
        case _:
            "Out of expected range"
    print(result)

def main():
    test_standard_scale()
    test_curved_scale()
    test_guard_clause()

```

## Timing

- Generation: 484.75s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
