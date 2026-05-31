def main():
    total = 0
    for _ in range(500):
        # List comprehension with filter
        evens = [x * x for x in range(1000) if x % 2 == 0]
        # Nested comprehension
        pairs = [a + b for a in range(50) for b in range(50)]
        total += len(evens) + len(pairs)
    print(f"total elements: {total}")

if __name__ == "__main__":
    main()
