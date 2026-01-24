# Issue Report: execution_failed

**Timestamp:** 2026-01-24T18:31:19.295840
**Type:** execution_failed
**Feature Focus:** if_elif_else
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test if/elif/else with weather recommendation system
class WeatherAdvisor:
    temperature: int
    is_raining: bool
    wind_speed: int

    def __init__(self, temp: int, rain: bool, wind: int):
        self.temperature = temp
        self.is_raining = rain
        self.wind_speed = wind

    def get_activity_recommendation(self) -> str:
        if self.temperature < 0:
            return "Stay inside"
        elif self.temperature < 10:
            if self.is_raining:
                return "Warm clothing needed"
            else:
                return "Bundle up"
        elif self.temperature < 20:
            return "Light jacket recommended"
        elif self.temperature < 30:
            if self.wind_speed > 20:
                return "Nice but windy"
            else:
                return "Perfect outdoor weather"
        else:
            return "Very hot"

    def should_bring_umbrella(self) -> bool:
        if self.is_raining:
            return True
        elif self.temperature > 25 and self.wind_speed < 10:
            return False
        else:
            return False

def main():
    advisor1: WeatherAdvisor = WeatherAdvisor(5, True, 15)
    print(advisor1.get_activity_recommendation())
    
    advisor2: WeatherAdvisor = WeatherAdvisor(25, False, 5)
    print(advisor2.get_activity_recommendation())
    
    advisor3: WeatherAdvisor = WeatherAdvisor(35, False, 10)
    print(advisor3.get_activity_recommendation())
    
    advisor4: WeatherAdvisor = WeatherAdvisor(15, False, 8)
    print(advisor4.get_activity_recommendation())

main()

# EXPECTED OUTPUT:
# Warm clothing needed
# Perfect outdoor weather
# Very hot
# Light jacket recommended
```

## Error

```
Compilation failed:
  Semantic error at line 51, column 1: Executable statements are not allowed at module level

```

## Timing

- Generation: 17.65s
- Execution: 0.87s
