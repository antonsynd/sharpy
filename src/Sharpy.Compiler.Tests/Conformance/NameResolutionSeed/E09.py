def main():
    n = 1
    f = lambda: (n := 5)
    f()
    print(n)
main()
