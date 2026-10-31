def fib_recursive(n as long) as long:
    if n <= 1:
        return n
    return fib_recursive(n - 1) + fib_recursive(n - 2)

def fib_iterative(n as long) as long:
    if n <= 1:
        return n
    a as long = 0
    b as long = 1
    i as long = 2
    while i <= n:
        temp = a + b
        a = b
        b = temp
        i = i + 1
    return b

def main():
    # Recursive (expensive)
    result = fib_recursive(30)
    print result
    # Iterative (fast)
    for _ in range(100000):
        fib_iterative(30)
    print "done"

main()
