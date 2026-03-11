# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-10T08:20:16.555453
**Type:** internal_compiler_error
**Feature Focus:** set_literal
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex set literal usage in an academic course system
# Combines: abstract classes, virtual/override, generics, set operations, type aliases

type CourseCode = str
type StudentId = int

@abstract
class Course:
    code: CourseCode
    credits: int

    def __init__(self, code: CourseCode, credits: int):
        self.code = code
        self.credits = credits

    @virtual
    def get_prerequisites(self) -> set[CourseCode]:
        return set()

    @virtual
    def get_conflicting_codes(self) -> set[CourseCode]:
        return set()

class LectureCourse(Course):
    department: str

    def __init__(self, code: CourseCode, credits: int, dept: str):
        super().__init__(code, credits)
        self.department = dept

    @override
    def get_prerequisites(self) -> set[CourseCode]:
        # Return specific set literal based on department
        if self.department == "CS":
            return {"CS101", "MATH200"}
        elif self.department == "MATH":
            return {"MATH101"}
        return set()

class LabCourse(Course):
    partner_lecture: CourseCode

    def __init__(self, code: CourseCode, credits: int, partner: CourseCode):
        super().__init__(code, credits)
        self.partner_lecture = partner

    @override
    def get_conflicting_codes(self) -> set[CourseCode]:
        # Labs conflict with other sections at same time
        return {"LAB001", "LAB002", "LAB003"}

class ScheduleAnalyzer:
    courses: list[Course]
    max_credits: int

    def __init__(self, max_credits: int):
        self.courses = list()
        self.max_credits = max_credits

    def add_course(self, course: Course) -> bool:
        current_credits = self._total_credits()
        if current_credits + course.credits > self.max_credits:
            return False
        self.courses.append(course)
        return True

    def _total_credits(self) -> int:
        total = 0
        for c in self.courses:
            total += c.credits
        return total

    def all_prerequisites(self) -> set[CourseCode]:
        all_reqs: set[CourseCode] = set()
        for course in self.courses:
            # Use set literal operations
            reqs = course.get_prerequisites()
            all_reqs = all_reqs | reqs
        return all_reqs

    def find_schedule_conflicts(self) -> set[CourseCode]:
        # Complex set operation using literal
        blocked: set[CourseCode] = {"BREAK001", "LUNCH002"}
        for course in self.courses:
            conflicts = course.get_conflicting_codes()
            blocked = blocked | conflicts
        return blocked

def main():
    # Create courses using various constructors
    cs300 = LectureCourse("CS300", 3, "CS")
    math250 = LectureCourse("MATH250", 4, "MATH")
    phys_lab = LabCourse("PHYS101L", 1, "PHYS101")

    # Build schedule
    analyzer = ScheduleAnalyzer(10)
    print(cs300.code)
    print(math250.code)

    # Set literals from prerequisites
    cs_reqs = cs300.get_prerequisites()
    print(len(cs_reqs))

    # Add courses and analyze
    analyzer.add_course(cs300)
    analyzer.add_course(math250)
    analyzer.add_course(phys_lab)
    all_reqs = analyzer.all_prerequisites()
    print(len(all_reqs))
    conflicts = analyzer.find_schedule_conflicts()
    print(len(conflicts))

    # Test set literal membership
    test_codes: set[CourseCode] = {"CS101", "CS200", "CS300", "MATH101"}
    common = all_reqs & test_codes
    print(len(common))

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp955qnmid/dogfood_test.spy:61:27
    |
 61 |         current_credits = self._total_credits()
    |                           ^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 211.67s
