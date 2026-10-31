def main():
    # String concatenation and manipulation
    result = ""
    for i in range(10000):
        result = result + i.ToString()

    # String methods
    count as long = 0
    for _ in range(1000):
        upper = result.ToUpper()
        lower = result.ToLower()
        parts = result.Split(char('5'))
        count += len(parts)
    print "operations: ${count}"

main()
