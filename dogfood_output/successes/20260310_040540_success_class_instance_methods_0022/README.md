# Successful Dogfood Run

**Timestamp:** 2026-03-10T04:02:37.439055
**Feature Focus:** class_instance_methods
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex class instance methods with abstract base, virtual/override, properties, and conditional logic
# A student assessment system with different grading methods

type Score = float

@abstract
class Assessment:
    _raw_score: Score
    _max_points: Score

    def __init__(self, raw: Score, max_pts: Score):
        self._raw_score = raw
        self._max_points = max_pts

    @virtual
    def get_weighted_score(self) -> Score:
        if self._max_points > 0.0:
            return (self._raw_score / self._max_points) * 100.0
        return 0.0

    def is_passing(self) -> bool:
        return self.get_weighted_score() >= 60.0

    property get raw(self) -> Score:
        return self._raw_score

class Exam(Assessment):
    _difficulty: Score

    def __init__(self, raw: Score, max_pts: Score, difficulty: Score):
        super().__init__(raw, max_pts)
        self._difficulty = difficulty

    @override
    def get_weighted_score(self) -> Score:
        base: Score = super().get_weighted_score()
        return base * self._difficulty

class Project(Assessment):
    _bonus: Score

    def __init__(self, raw: Score, max_pts: Score, bonus: Score):
        super().__init__(raw, max_pts)
        self._bonus = bonus

    @override
    def get_weighted_score(self) -> Score:
        base: Score = super().get_weighted_score()
        adjusted: Score = base + self._bonus
        if adjusted > 100.0:
            return 100.0
        return adjusted

    def get_grade_tier(self) -> str:
        score: Score = self.get_weighted_score()
        if score >= 90.0:
            return "A"
        elif score >= 80.0:
            return "B"
        elif score >= 70.0:
            return "C"
        elif score >= 60.0:
            return "D"
        return "F"

def main():
    exam: Exam = Exam(75.0, 100.0, 1.1)
    project: Project = Project(85.0, 100.0, 5.0)

    print(exam.raw)
    print(exam.get_weighted_score())
    print(exam.is_passing())

    print(project.raw)
    print(project.get_weighted_score())
    print(project.is_passing())
    print(project.get_grade_tier())

    project2: Project = Project(50.0, 100.0, 5.0)
    print(project2.is_passing())

```

## Output

```
75.0
82.5
True
85.0
90.0
True
A
False
```

## Timing

- Generation: 171.82s
- Execution: 5.30s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
