# Skipped Dogfood Run

**Timestamp:** 2026-03-08T10:56:09.120387
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0220]: Cannot pass argument of type 'float' to parameter of type 'T'
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:30:26
    |
 30 |         super().__init__(initial_price)
    |                          ^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'float' to parameter of type 'T'
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:35:27
    |
 35 |         super().set_value(new_price)
    |                           ^^^^^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'StockPriceMonitor' to parameter of type 'ObservableValue[float]'
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:69:27
    |
 69 |     portfolio.add_holding(apple, 1.05)
    |                           ^^^^^
    |

error[SPY0220]: Cannot pass argument of type 'StockPriceMonitor' to parameter of type 'ObservableValue[float]'
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:70:27
    |
 70 |     portfolio.add_holding(google, 1.10)
    |                           ^^^^^^
    |

error[SPY0200]: Undefined identifier 'increment_counter'
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:86:30
    |
 86 |     tech.on_value_changed += increment_counter
    |                              ^^^^^^^^^^^^^^^^^
    |

error[SPY0200]: Undefined identifier 'increment_counter'
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:89:30
    |
 89 |     tech.on_value_changed -= increment_counter
    |                              ^^^^^^^^^^^^^^^^^
    |

Validation errors:
error[SPY0283]: Cannot access protected member '_log_count' of 'PortfolioManager' from outside the class hierarchy
  --> /tmp/tmp7mpxnzxs/dogfood_test.spy:93:11
    |
 93 |     print(portfolio._log_count)
    |           ^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** event_subscribe_unsubscribe
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Event subscription/unsubscription with class inheritance and generics
# Features: Generic base class with event, inheritance, virtual/override,
# lambda subscriptions, event subscription tracking

delegate ValueChangedHandler[T](old_value: T, new_value: T) -> None

class ObservableValue[T]:
    _current: T
    
    def __init__(self, initial: T):
        self._current = initial
    
    def get_current(self) -> T:
        return self._current
    
    @virtual
    def set_value(self, new_value: T) -> None:
        if new_value != self._current:
            old = self._current
            self._current = new_value
            self.on_value_changed?.invoke(old, new_value)
    
    event on_value_changed: ValueChangedHandler[T]

class StockPriceMonitor(ObservableValue[float]):
    _symbol: str
    
    def __init__(self, symbol: str, initial_price: float):
        self._symbol = symbol
        super().__init__(initial_price)
    
    @override
    def set_value(self, new_price: float) -> None:
        print(self._symbol)
        super().set_value(new_price)

class PortfolioManager:
    _holdings: list[ObservableValue[float]]
    _log_count: int
    
    def __init__(self):
        self._holdings = []
        self._log_count = 0
    
    def add_holding(self, stock: ObservableValue[float], threshold: float) -> None:
        self._holdings.append(stock)
        handler: ValueChangedHandler[float] = lambda old_val, new_val: self._check_threshold(old_val, new_val, threshold)
        stock.on_value_changed += handler
        stock.on_value_changed += self._log_change
    
    def _log_change(self, old_val: float, new_val: float) -> None:
        self._log_count += 1
        print(self._log_count)
    
    def _check_threshold(self, old_val: float, new_val: float, threshold: float) -> None:
        if new_val > old_val * threshold:
            print(999)

class CounterHolder:
    value: int
    
    def __init__(self, initial: int):
        self.value = initial

def main():
    apple = StockPriceMonitor("AAPL", 150.0)
    google = StockPriceMonitor("GOOGL", 2800.0)
    portfolio = PortfolioManager()
    portfolio.add_holding(apple, 1.05)
    portfolio.add_holding(google, 1.10)
    
    print(111)
    apple.set_value(157.5)
    print(222)
    google.set_value(2900.0)
    print(333)
    google.set_value(3100.0)
    
    tech = StockPriceMonitor("TECH", 50.0)
    counter_holder = CounterHolder(0)
    
    def increment_counter(old: float, new: float) -> None:
        counter_holder.value += 1
        print(counter_holder.value)
    
    tech.on_value_changed += increment_counter
    print(444)
    tech.set_value(55.0)
    tech.on_value_changed -= increment_counter
    print(555)
    tech.set_value(60.0)
    print(666)
    print(portfolio._log_count)
    print(counter_holder.value)

```

## Timing

- Generation: 695.39s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
