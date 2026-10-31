import System.Collections.Generic

def main():
    total as long = 0
    for _ in range(500):
        # List comprehension with filter
        evens = List[of long](cast(long, x) * x for x in range(1000) if x % 2 == 0)
        # Nested comprehension
        pairs = List[of long](cast(long, a + b) for a in range(50) for b in range(50))
        total += len(evens) + len(pairs)
    print "total elements: ${total}"

main()
