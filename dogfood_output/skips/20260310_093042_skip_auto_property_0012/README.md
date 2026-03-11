# Skipped Dogfood Run

**Timestamp:** 2026-03-10T09:22:09.081891
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0248]: Cannot override 'get_resource_type' because the base class method in 'Resource' is not marked @virtual or @abstract. Add @virtual to the method in the base class.
  --> /tmp/tmpgo23a313/dogfood_test.spy:58:5
    |
 58 |     def get_resource_type(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0248]: Cannot override 'get_resource_type' because the base class method in 'Resource' is not marked @virtual or @abstract. Add @virtual to the method in the base class.
  --> /tmp/tmpgo23a313/dogfood_test.spy:78:5
    |
 78 |     def get_resource_type(self) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** auto_property
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex auto_property test: Resource management with property inheritance
# Tests: auto-property, get-property with inheritance, virtual/override, dispatch

type ResourceId = int

@abstract
class Resource:
    # Auto-property with default value (read-write)
    property name: str = "unnamed"
    
    # Read-only backing field for computed property
    _rid: ResourceId
    
    # Read-only computed property (virtual for override)
    @virtual
    property get capacity(self) -> int:
        return 0
    
    # Computed property based on other properties
    property get display_name(self) -> str:
        return f"{self.name}#{self._rid}"
    
    def __init__(self, rid: ResourceId, name: str):
        self._rid = rid
        self.name = name
    
    @abstract
    def allocate(self, amount: int) -> bool:
        ...
    
    def get_resource_type(self) -> str:
        return "base"

class MemoryResource(Resource):
    # Internal backing field for computed property
    _allocated: int
    
    # Override virtual property with computed implementation
    @override
    property get capacity(self) -> int:
        return 1024
    
    def __init__(self, rid: ResourceId, name: str):
        super().__init__(rid, name)
        self._allocated = 0
    
    @override
    def allocate(self, amount: int) -> bool:
        if self._allocated + amount <= self.capacity:
            self._allocated += amount
            return True
        return False
    
    property get utilization(self) -> float:
        return (self._allocated / self.capacity) * 100.0
    
    @override
    def get_resource_type(self) -> str:
        return "memory"

class ComputeResource(Resource):
    # Additional auto-property specific to subclass
    property cores: int = 4
    
    @override
    property get capacity(self) -> int:
        return self.cores * 100
    
    def __init__(self, rid: ResourceId, name: str, cores: int):
        super().__init__(rid, name)
        self.cores = cores
    
    @override
    def allocate(self, load: int) -> bool:
        return load <= self.capacity
    
    @override
    def get_resource_type(self) -> str:
        return "compute"

def analyze_memory_resource(mem: MemoryResource) -> str:
    return f"Memory: {mem.display_name}, util={mem.utilization:.1f}%"

def analyze_compute_resource(comp: ComputeResource) -> str:
    return f"Compute: {comp.display_name}, {comp.cores} cores"

def main():
    # Create resources with different property configurations
    mem: MemoryResource = MemoryResource(1, "RAM-Block-A")
    cpu: ComputeResource = ComputeResource(2, "CPU-Cluster-1", 8)
    
    # Test auto-properties
    print(mem.name)
    print(cpu.cores)
    
    # Modify auto-property
    mem.name = "RAM-Block-B"
    print(mem.display_name)
    
    # Test allocation and computed utilization
    print(mem.allocate(512))
    print(mem.utilization)
    
    # Test capacity properties
    print(cpu.capacity)
    print(mem.capacity)
    
    # Test allocation with memory resource
    print(analyze_memory_resource(mem))
    
    # Test compute resource properties
    print(analyze_compute_resource(cpu))
    
    # Test failed allocation and second allocation
    print(mem.allocate(600))
    print(mem.allocate(200))
    print(mem.utilization)
    
    # Test compute resource allocation
    print(cpu.allocate(799))
    print(cpu.allocate(1))
    
    # Test with different core counts
    cpu2: ComputeResource = ComputeResource(3, "Small-CPU", 2)
    print(cpu2.cores)
    print(cpu2.capacity)

```

## Timing

- Generation: 496.50s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
