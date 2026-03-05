# Skipped Dogfood Run

**Timestamp:** 2026-03-04T14:42:17.856720
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0373]: Event 'on_message' type 'GenericType { IsNullable = False, IsValueType = False, ClrType = , DeclaringSymbol = , Name = EventHandler, TypeArguments = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.SemanticType], GenericDefinition = TypeSymbol { Name = EventHandler, Kind = Type, AccessLevel = Public, DeclarationLine = 2, DeclarationColumn = 1, DeclarationSpan = , DeclaringFilePath = , IsReExport = True, OriginalModule = system, IsErrorRecovery = False, CodeGenInfo = , TypeKind = Class, ClrType = System.EventHandler`2[TSender,TEventArgs], IsAbstract = False, IsCovariant = False, DefiningModule = system, DefiningFilePath = , TypeParameters = System.Collections.Generic.List`1[Sharpy.Compiler.Parser.Ast.TypeParameterDef], IsGeneric = True, Fields = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.VariableSymbol], Methods = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol], Properties = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.PropertySymbol], Events = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.EventSymbol], OperatorMethods = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], ProtocolMethods = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], MethodOverloads = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], Constructors = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol], UnionCases = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.TypeSymbol], BaseType = TypeSymbol { Name = MulticastDelegate, Kind = Type, AccessLevel = Public, DeclarationLine = , DeclarationColumn = , DeclarationSpan = , DeclaringFilePath = , IsReExport = False, OriginalModule = , IsErrorRecovery = False, CodeGenInfo = , TypeKind = Class, ClrType = System.MulticastDelegate, IsAbstract = True, IsCovariant = False, DefiningModule = , DefiningFilePath = , TypeParameters = System.Collections.Generic.List`1[Sharpy.Compiler.Parser.Ast.TypeParameterDef], IsGeneric = False, Fields = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.VariableSymbol], Methods = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol], Properties = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.PropertySymbol], Events = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.EventSymbol], OperatorMethods = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], ProtocolMethods = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], MethodOverloads = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], Constructors = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol], UnionCases = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.TypeSymbol], BaseType = TypeSymbol { Name = Delegate, Kind = Type, AccessLevel = Public, DeclarationLine = , DeclarationColumn = , DeclarationSpan = , DeclaringFilePath = , IsReExport = False, OriginalModule = , IsErrorRecovery = False, CodeGenInfo = , TypeKind = Class, ClrType = System.Delegate, IsAbstract = True, IsCovariant = False, DefiningModule = , DefiningFilePath = , TypeParameters = System.Collections.Generic.List`1[Sharpy.Compiler.Parser.Ast.TypeParameterDef], IsGeneric = False, Fields = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.VariableSymbol], Methods = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol], Properties = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.PropertySymbol], Events = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.EventSymbol], OperatorMethods = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], ProtocolMethods = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], MethodOverloads = System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol]], Constructors = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.FunctionSymbol], UnionCases = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.TypeSymbol], BaseType = , Interfaces = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.InterfaceReference], UnresolvedBaseName = , UnresolvedInterfaceNames = System.Collections.Generic.List`1[System.String] }, Interfaces = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.InterfaceReference], UnresolvedBaseName = , UnresolvedInterfaceNames = System.Collections.Generic.List`1[System.String] }, Interfaces = System.Collections.Generic.List`1[Sharpy.Compiler.Semantic.InterfaceReference], UnresolvedBaseName = , UnresolvedInterfaceNames = System.Collections.Generic.List`1[System.String] } }' is not a delegate type
  --> /tmp/tmp7lk5actl/dogfood_test.spy:12:5
    |
 12 |     event on_message: EventHandler[EventSource, MessageEventArgs]
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0287]: super() cannot be used in regular methods; only in __init__, @override, or dunder methods
  --> /tmp/tmp7lk5actl/dogfood_test.spy:38:9
    |
 38 |         super().trigger(msg)
    |         ^^^^^^^
    |


**Feature Focus:** event_with_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test events with inheritance
from system import EventArgs, EventHandler

class MessageEventArgs(EventArgs):
    message: str
    
    def __init__(self, msg: str):
        self.message = msg

class EventSource:
    # Base class with an event
    event on_message: EventHandler[EventSource, MessageEventArgs]
    count: int = 0
    
    @virtual
    def process(self, msg: str) -> bool:
        return len(msg) > 0
    
    def trigger(self, msg: str):
        if self.process(msg):
            self.count += 1
            self.on_message?.invoke(self, MessageEventArgs(msg))

class LoggingEventSource(EventSource):
    # Derived class that adds logging before event trigger
    logs: list[str]
    
    def __init__(self):
        self.logs = []
    
    @override
    def process(self, msg: str) -> bool:
        self.logs.append(f"Processing: {msg}")
        return super().process(msg)
    
    def trigger(self, msg: str):
        self.logs.append(f"Triggering: {msg}")
        super().trigger(msg)
    
    def get_logs(self) -> list[str]:
        return self.logs

class FilteredEventSource(LoggingEventSource):
    # Second-level derived class with filtering
    @override
    def process(self, msg: str) -> bool:
        # Filter out messages containing "block"
        if "block" in msg:
            self.logs.append(f"Blocked: {msg}")
            return False
        return super().process(msg)

class EventSubscriber:
    received: list[str]
    source_name: str
    
    def __init__(self, name: str):
        self.received = []
        self.source_name = name
    
    def on_event(self, sender: object, args: MessageEventArgs):
        msg = f"[{self.source_name}] Got: {args.message}"
        self.received.append(msg)

def main():
    # Test 1: Basic event subscription
    print("--- Test 1: Basic event ---")
    base = EventSource()
    sub1 = EventSubscriber("Sub1")
    base.on_message += sub1.on_event
    base.trigger("Hello")
    base.trigger("World")
    for msg in sub1.received:
        print(msg)
    print(f"Count: {base.count}")
    
    # Test 2: Logging event source
    print("")
    print("--- Test 2: Logging source ---")
    logging = LoggingEventSource()
    sub2 = EventSubscriber("Sub2")
    logging.on_message += sub2.on_event
    logging.trigger("Test")
    logging.trigger("Message")
    for msg in sub2.received:
        print(msg)
    for log in logging.get_logs():
        print(log)
    
    # Test 3: Filtered event source (multi-level inheritance)
    print("")
    print("--- Test 3: Filtered source ---")
    filtered = FilteredEventSource()
    sub3 = EventSubscriber("Sub3")
    filtered.on_message += sub3.on_event
    filtered.trigger("Allowed")
    filtered.trigger("This is blocked message")
    filtered.trigger("Also good")
    for msg in sub3.received:
        print(msg)
    for log in filtered.get_logs():
        if "Blocked" in log:
            print(log)
    
    # Test 4: Multiple subscribers (using prefix removal without slicing in f-string)
    print("")
    print("--- Test 4: Multiple subscribers ---")
    multi = EventSource()
    sub_a = EventSubscriber("A")
    sub_b = EventSubscriber("B")
    multi.on_message += sub_a.on_event
    multi.on_message += sub_b.on_event
    multi.trigger("Broadcast")
    for msg in sub_a.received:
        # Remove "[A] " prefix (4 chars) using removeprefix
        without_prefix = str(msg).removeprefix("[A] ")
        print(f"A: {without_prefix}")
    for msg in sub_b.received:
        # Remove "[B] " prefix (4 chars) using removeprefix
        without_prefix = str(msg).removeprefix("[B] ")
        print(f"B: {without_prefix}")
    
    # Test 5: Unsubscribe
    print("")
    print("--- Test 5: Unsubscribe ---")
    single = EventSource()
    listener = EventSubscriber("Listener")
    single.on_message += listener.on_event
    single.trigger("First")
    single.on_message -= listener.on_event
    single.trigger("Second")
    for msg in listener.received:
        print(msg)
    print(f"Only received: {len(listener.received)} message(s)")

```

## Timing

- Generation: 288.02s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
