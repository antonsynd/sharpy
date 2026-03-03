# Issue Report: compilation_failed

**Timestamp:** 2026-03-03T02:54:21.160524
**Type:** compilation_failed
**Feature Focus:** class_with_init
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Class hierarchy with __init__, inheritance, abstract methods, properties, and enums
# Combines: inheritance, virtual/override, properties, enums, explicit constructor initialization

enum DeviceStatus:
    OFFLINE = 0
    ONLINE = 1
    MAINTENANCE = 2

@abstract
class Device:
    _id: str
    _status: DeviceStatus

    property get status(self) -> DeviceStatus:
        return self._status

    def __init__(self, device_id: str):
        self._id = device_id
        self._status = DeviceStatus.OFFLINE

    @virtual
    def power_on(self) -> None:
        self._status = DeviceStatus.ONLINE

    @virtual
    def power_off(self) -> None:
        self._status = DeviceStatus.OFFLINE

    @virtual
    def get_info(self) -> str:
        return f"Device[{self._id}]: {self.status.name}"

class NetworkDevice(Device):
    _ip_address: str

    property ip_address(self) -> str:
        return self._ip_address

    def __init__(self, device_id: str, ip: str):
        self._id = device_id
        self._status = DeviceStatus.OFFLINE
        self._ip_address = ip

    @override
    def get_info(self) -> str:
        base_info = self._id
        return f"NetworkDevice[{base_info}@{self.ip_address}]: {self.status.name}"

class Server(NetworkDevice):
    _cpu_count: int

    property cpu_count(self) -> int:
        return self._cpu_count

    def __init__(self, device_id: str, ip: str, cpus: int):
        self._id = device_id
        self._status = DeviceStatus.OFFLINE
        self._ip_address = ip
        self._cpu_count = cpus

    @override
    def power_on(self) -> None:
        self._status = DeviceStatus.ONLINE

    @override
    def get_info(self) -> str:
        return f"Server[{self._id}@{self.ip_address}]: {self.status.name}, CPUs={self.cpu_count}"

def main():
    # Test NetworkDevice with explicit constructor initialization
    net: NetworkDevice = NetworkDevice("router-001", "192.168.1.1")
    print(net.get_info())

    # Test Server with full hierarchy
    server: Server = Server("srv-001", "10.0.0.5", 8)
    print(server.get_info())

    # Power on and check status
    server.power_on()
    print(server.status.name)
    print(server.status.value)

    # Test status enum iteration and comparison
    total: int = 0
    for s in DeviceStatus:
        total = total + s.value
    print(total)

    # Power off and verify
    server.power_off()
    print(server.status.name)

    # Verify ip_address property is accessible
    print(net.ip_address)

```

## Error

```
Assembly compilation failed:

error[CS7036]: There is no argument given that corresponds to the required parameter 'deviceId' of 'DogfoodTest.Device.Device(string)'
  --> /tmp/tmp_g_fe1u7/dogfood_test.spy:41:16
    |
 41 |         self._status = DeviceStatus.OFFLINE
    |                ^
    |

error[CS7036]: There is no argument given that corresponds to the required parameter 'deviceId' of 'DogfoodTest.NetworkDevice.NetworkDevice(string, string)'
  --> /tmp/tmp_g_fe1u7/dogfood_test.spy:57:16
    |
 57 |         self._status = DeviceStatus.OFFLINE
    |                ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmp_g_fe1u7/dogfood_test.cs

```

## Timing

- Generation: 252.57s
- Execution: 4.55s
