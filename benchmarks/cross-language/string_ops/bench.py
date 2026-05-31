def main():
    # String concatenation and manipulation
    result = ""
    for i in range(10000):
        result = result + str(i)

    # String methods
    count = 0
    for _ in range(1000):
        upper = result.upper()
        lower = result.lower()
        parts = result.split("5")
        count += len(parts)
    print(f"operations: {count}")

if __name__ == "__main__":
    main()
