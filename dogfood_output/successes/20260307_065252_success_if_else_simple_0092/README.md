# Successful Dogfood Run

**Timestamp:** 2026-03-07T06:49:48.435251
**Feature Focus:** if_else_simple
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Smart traffic signal controller with complex decision logic
# Tests nested if/else with class inheritance and multiple conditions

enum Direction:
    NORTH = 0
    EAST = 1
    SOUTH = 2
    WEST = 3

enum LightState:
    RED = 0
    YELLOW = 1
    GREEN = 2

class Vehicle:
    speed: int
    direction: Direction
    
    def __init__(self, speed: int, direction: Direction):
        self.speed = speed
        self.direction = direction
    
    @virtual
    def priority(self) -> int:
        return 0

class EmergencyVehicle(Vehicle):
    emergency_type: str
    
    def __init__(self, speed: int, direction: Direction, emergency_type: str):
        super().__init__(speed, direction)
        self.emergency_type = emergency_type
    
    @override
    def priority(self) -> int:
        if self.emergency_type == "fire":
            return 3
        elif self.emergency_type == "ambulance":
            return 2
        else:
            return 1

class TrafficSignal:
    current_state: LightState
    timer: int
    
    def __init__(self):
        self.current_state = LightState.RED
        self.timer = 0
    
    def evaluate(self, vehicles: list[Vehicle], weather_severity: int) -> str:
        emergency_count: int = 0
        for v in vehicles:
            if isinstance(v, EmergencyVehicle):
                emergency_count = emergency_count + 1
        
        decision: str = "hold"
        
        if emergency_count > 0:
            if weather_severity > 5:
                decision = "emergency_caution"
            else:
                decision = "emergency"
        elif len(vehicles) > 10:
            if self.current_state == LightState.RED:
                if weather_severity > 3:
                    decision = "caution"
                else:
                    decision = "proceed"
            else:
                decision = "queue"
        elif len(vehicles) == 0:
            if self.timer > 60:
                decision = "standby"
            else:
                decision = "maintain"
        else:
            if weather_severity > 7:
                decision = "extra_caution"
            else:
                decision = "standard"
        
        return decision

def create_vehicles() -> list[Vehicle]:
    result: list[Vehicle] = []
    result.append(Vehicle(30, Direction.NORTH))
    result.append(Vehicle(25, Direction.EAST))
    result.append(EmergencyVehicle(60, Direction.SOUTH, "ambulance"))
    result.append(Vehicle(20, Direction.WEST))
    return result

def create_heavy_traffic() -> list[Vehicle]:
    result: list[Vehicle] = []
    i: int = 0
    while i < 12:
        result.append(Vehicle(30, Direction.NORTH))
        i = i + 1
    return result

def main():
    signal: TrafficSignal = TrafficSignal()
    cars: list[Vehicle] = create_vehicles()
    
    print(signal.evaluate(cars, 0))
    print(signal.evaluate(cars, 6))
    
    empty: list[Vehicle] = []
    print(signal.evaluate(empty, 0))
    
    signal.timer = 70
    print(signal.evaluate(empty, 0))
    
    signal.timer = 0
    signal.current_state = LightState.RED
    heavy: list[Vehicle] = create_heavy_traffic()
    print(signal.evaluate(heavy, 0))
    print(signal.evaluate(heavy, 4))

```

## Output

```
emergency
emergency_caution
maintain
standby
proceed
caution
```

## Timing

- Generation: 173.77s
- Execution: 4.71s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
