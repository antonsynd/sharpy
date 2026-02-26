# Issue Report: output_mismatch

**Timestamp:** 2026-02-25T10:22:46.247162
**Type:** output_mismatch
**Feature Focus:** module_utils
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point - imports from both modules
from student_base import Student, calculate_average, format_score
from grade_utils import calculate_class_average, get_top_student, GradeReport

def main():
    # Create students
    alice: Student = Student("Alice", 20, 1001)
    bob: Student = Student("Bob", 21, 1002)
    charlie: Student = Student("Charlie", 19, 1003)
    
    # Add grades
    alice.add_grade(85)
    alice.add_grade(92)
    alice.add_grade(78)
    
    bob.add_grade(90)
    bob.add_grade(88)
    bob.add_grade(95)
    
    charlie.add_grade(75)
    charlie.add_grade(82)
    charlie.add_grade(79)
    
    # Calculate individual averages
    alice_avg: float = calculate_average(alice.grades)
    bob_avg: float = calculate_average(bob.grades)
    
    print(format_score(alice_avg))
    print(format_score(bob_avg))
    
    # Create list and calculate class average
    students: list[Student] = [alice, bob, charlie]
    class_avg: float = calculate_class_average(students)
    print(format_score(class_avg))
    
    # Use GradeReport utility class
    report: GradeReport = GradeReport("Final Grades")
    report.add_entry("Processed 3 students")
    print(report.get_summary())
    
    # Get top student using utility function
    top: Student = get_top_student(students)
    print(top.name)

# EXPECTED OUTPUT:
# 85.0
# 91.0
# 84.0
# Final Grades - 1 entries
# Bob
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
85.0
91.0
84.0
Final Grades - 1 entries
Bob

```

### Actual
```
85.0
91.0
84.9
Final Grades - 1 entries
Bob
```

## Timing

- Generation: 85.46s
- Execution: 4.63s
