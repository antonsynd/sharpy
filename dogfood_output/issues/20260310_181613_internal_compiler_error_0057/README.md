# Issue Report: internal_compiler_error

**Timestamp:** 2026-03-10T18:10:09.705781
**Type:** internal_compiler_error
**Feature Focus:** event_with_delegate
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Events with custom delegates in inheritance hierarchy
# Demonstrates virtual/override events with custom delegate types

delegate DataHandler(value: int) -> None

class DataSource:
    _buffer: list[int]
    _handlers: list[DataHandler]

    def __init__(self):
        self._buffer = []
        self._handlers = []

    @virtual
    def add_data(self, value: int) -> None:
        processed = self.process_value(value)
        self._buffer.append(processed)
        self.notify_handlers(processed)

    def process_value(self, x: int) -> int:
        return x * 2

    def get_buffer(self) -> list[int]:
        return self._buffer

    def add_handler(self, handler: DataHandler) -> None:
        self._handlers.append(handler)

    def notify_handlers(self, value: int) -> None:
        for h in self._handlers:
            h(value)

delegate DataProcessor(value: int) -> int

class FilteredDataSource(DataSource):
    min_threshold: int

    def __init__(self, threshold: int):
        super().__init__()
        self.min_threshold = threshold

    @override
    def add_data(self, value: int) -> None:
        if value >= self.min_threshold:
            super().add_data(value)

class DataPipeline:
    _source: FilteredDataSource
    _handlers: list[DataProcessor]
    _results: list[int]

    def __init__(self, source: FilteredDataSource):
        self._source = source
        self._handlers = []
        self._results = []
        handler: DataHandler = lambda v: self.on_data_received(v)
        self._source.add_handler(handler)

    def add_handler(self, handler: DataProcessor) -> None:
        self._handlers.append(handler)

    def on_data_received(self, value: int) -> None:
        current = value
        for h in self._handlers:
            current = h(current)
        self._results.append(current)

    def run_pipeline(self, inputs: list[int]) -> list[int]:
        self._results = []
        for val in inputs:
            self._source.add_data(val)
        return self._results

def main():
    source = FilteredDataSource(5)
    pipeline = DataPipeline(source)

    pipeline.add_handler(lambda x: x + 10)
    pipeline.add_handler(lambda x: x * 3)

    inputs: list[int] = [3, 5, 7, 2, 10, 4]
    results = pipeline.run_pipeline(inputs)

    print(len(results))
    for r in results:
        print(r)

    buffer = source.get_buffer()
    print(len(buffer))
    for b in buffer:
        print(b)

```

## Error

```
Internal compiler error: Compilation errors:

error[SPY0907]: Internal: type inference produced UnknownType for 'FunctionCall' without a corresponding error diagnostic. This is a compiler bug.
  --> /tmp/tmp8uax2l5x/dogfood_test.spy:16:21
    |
 16 |         processed = self.process_value(value)
    |                     ^^^^^^^^^^^^^^^^^^^^^^^^^
    |


```

## Timing

- Generation: 345.85s
