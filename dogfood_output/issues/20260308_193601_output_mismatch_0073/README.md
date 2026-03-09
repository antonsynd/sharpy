# Issue Report: output_mismatch

**Timestamp:** 2026-03-08T19:30:27.265668
**Type:** output_mismatch
**Feature Focus:** collection_methods
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class Gradebook:
    grades: dict[str, list[float]]
    
    def __init__(self):
        self.grades = {}
    
    def add_course(self, course_name: str) -> None:
        if course_name not in self.grades:
            self.grades[course_name] = []
    
    def add_grade(self, course_name: str, grade: float) -> None:
        # Use get() to safely access with default
        existing: list[float] = self.grades.get(course_name, [])
        existing.append(grade)
        self.grades[course_name] = existing
    
    def get_grades(self, course_name: str) -> list[float]:
        return self.grades.get(course_name, [])
    
    def get_average(self, course_name: str) -> float:
        grades: list[float] = self.get_grades(course_name)
        if len(grades) == 0:
            return 0.0
        total: float = 0.0
        for g in grades:
            total = total + g
        return total / len(grades)
    
    def remove_lowest(self, course_name: str) -> None:
        grades: list[float] = self.grades.get(course_name, [])
        if len(grades) > 0:
            grades.sort()
            removed: float = grades.pop(0)
            self.grades[course_name] = grades
            print(f"Removed lowest: {removed}")
    
    def list_courses(self) -> list[str]:
        # Get keys and convert to list
        result: list[str] = []
        for name in self.grades.keys():
            result.append(name)
        return result
    
    def show_all(self) -> None:
        print("--- All Grades ---")
        for course, grades in self.grades.items():
            # Build the output line as a single string
            line: str = f"{course}: "
            for g in grades:
                line = line + str(g) + " "
            print(line)

def main():
    gb: Gradebook = Gradebook()
    gb.add_course("Math")
    gb.add_course("Science")
    gb.add_grade("Math", 85.0)
    gb.add_grade("Math", 92.0)
    gb.add_grade("Math", 78.0)
    gb.add_grade("Science", 90.0)
    gb.add_grade("Science", 88.0)
    # Test get and len
    print(f"Math grades: {gb.get_grades('Math')}")
    print(f"Math count: {len(gb.get_grades('Math'))}")
    # Test values
    math_avg: float = gb.get_average("Math")
    print(f"Math average: {math_avg}")
    gb.remove_lowest("Math")
    # Check dict membership
    print(f"Has Math: {'Math' in gb.grades}")
    gb.show_all()
    # Test sorted course names
    courses: list[str] = gb.list_courses()
    courses.sort()
    print(f"Courses: {courses}")

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
Math grades: [85.0, 92.0, 78.0]
Math count: 3
Math average: 85.0
Removed lowest: 78.0
Has Math: True
--- All Grades ---
Math: 92.0 85.0 
Science: 90.0 88.0 
Courses: ['Math', 'Science']

```

### Actual
```
Math grades: [85, 92, 78]
Math count: 3
Math average: 85.0
Removed lowest: 78.0
Has Math: True
--- All Grades ---
Math: 85.0 92.0 
Science: 90.0 88.0 
Courses: [Math, Science]
```

## Timing

- Generation: 232.24s
- Execution: 5.18s
