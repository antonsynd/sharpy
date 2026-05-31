def fib_recursive(n: int) -> int:
    if n <= 1:
        return n
    return fib_recursive(n - 1) + fib_recursive(n - 2)

def fib_iterative(n: int) -> int:
    if n <= 1:
        return n
    a = 0
    b = 1
    for _ in range(2, n + 1):
        a, b = b, a + b
    return b

def main():
    # Recursive (expensive)
    result = fib_recursive(30)
    print(result)
    # Iterative (fast)
    for _ in range(100_000):
        fib_iterative(30)
    print("done")

if __name__ == "__main__":
    main()
