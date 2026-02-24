# Skipped Dogfood Run

**Timestamp:** 2026-02-24T05:16:49.776626
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got For
  --> /tmp/tmp4g16429p/dogfood_test.spy:45:5
    |
 45 |     for i in range(len(appliances)):
    |     ^^^
    |


**Feature Focus:** property_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Property inheritance with abstract properties, virtual computed properties, and init-only properties
@abstract
class Appliance:
    @abstract
    property get power_rating(self) -> float:
        ...

    @abstract
    property get brand(self) -> str:
        ...

    property get efficiency(self) -> str:
        rating: float = self.power_rating
        if rating < 100.0:
            return "efficient"
        elif rating < 500.0:
            return "moderate"
        else:
            return "high consumption"

class KitchenAppliance(Appliance):
    warranty_years: int
    property init brand: str

    def __init__(self, brand: str, warranty: int = 1):
        self.brand = brand
        self.warranty_years = warranty

class Refrigerator(KitchenAppliance):
    volume_liters: float

    def __init__(self, brand: str, volume: float):
        super().__init__(brand, 2)
        self.volume_liters = volume

    @override
    property get power_rating(self) -> float:
        return self.volume_liters * 0.8

def main():
    appliances: list[Appliance] = [
        Refrigerator("CoolBrand", 300.0),
        Refrigerator("EcoBrand", 100.0)
    ]
    for i in range(len(appliances)):
        a: Appliance = appliances[i]
        print(a.brand)
        print(a.power_rating)
        print(a.efficiency)

# EXPECTED OUTPUT:
# CoolBrand
# 240.0
# moderate
# EcoBrand
# 80.0
# efficient
```

## Timing

- Generation: 1063.78s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
