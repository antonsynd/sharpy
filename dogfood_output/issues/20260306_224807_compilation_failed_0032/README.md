# Issue Report: compilation_failed

**Timestamp:** 2026-03-06T22:40:24.278800
**Type:** compilation_failed
**Feature Focus:** cross_module_classes
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Main entry point demonstrating cross-module class usage
from core import PluginManager
from data import DataPacket, DataType, Priority, PacketProcessor
from plugins import TextProcessor, NumberProcessor, BinaryProcessor, create_packet

def main():
    # Create plugin manager
    manager: PluginManager[IPlugin] = PluginManager[IPlugin]()
    
    # Register plugins
    text_plugin = TextProcessor(Priority.HIGH)
    num_plugin = NumberProcessor(100)
    bin_plugin = BinaryProcessor()
    
    manager.register(text_plugin)
    manager.register(num_plugin)
    manager.register(bin_plugin)
    
    # Create packets
    text_packet: DataPacket = create_packet(DataType.TEXT, 5, "text1")
    num_packet: DataPacket = create_packet(DataType.NUMBER, 50, "num1")
    bin_packet: DataPacket = create_packet(DataType.BINARY, 30, "bin1")
    
    # Test plugin names
    print(text_plugin.get_name())
    print(num_plugin.get_name())
    print(bin_plugin.get_name())
    
    # Test processing
    result1: int = manager.process_all(text_packet)
    print(result1)
    
    result2: int = manager.process_all(num_packet)
    print(result2)
    
    result3: int = manager.process_all(bin_packet)
    print(result3)
    
    # Test struct and data packet directly
    direct_packet: DataPacket = DataPacket(DataType.NUMBER, 25, "direct")
    print(direct_packet.payload)
    print(direct_packet.tag)
    
    # Test PacketProcessor
    processor: PacketProcessor = PacketProcessor(4)
    calc_result: int = processor.calculate(direct_packet)
    print(calc_result)
    
    # Test priority enum
    p: Priority = Priority.MEDIUM
    print(p.value)

```

## Error

```
Assembly compilation failed:

error[CS1061]: 'T' does not contain a definition for 'Prepare' and no accessible extension method 'Prepare' accepting a first argument of type 'T' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpgal9nvrd/core.spy:38:28
    |
 38 |     
    |     ^
    |

error[CS1061]: 'Data.Priority' does not contain a definition for 'Value' and no accessible extension method 'Value' accepting a first argument of type 'Data.Priority' could be found (are you missing a using directive or an assembly reference?)
  --> /tmp/tmpgal9nvrd/plugins.spy:17:54
    |
 17 |     manager.register(bin_plugin)
    |                                 ^
    |


```

## Timing

- Generation: 426.84s
- Execution: 5.54s
