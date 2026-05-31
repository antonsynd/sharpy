def matrix_multiply(a: list[list[int]], b: list[list[int]]) -> list[list[int]]:
    rows_a = len(a)
    cols_a = len(a[0])
    cols_b = len(b[0])
    result = [[0] * cols_b for _ in range(rows_a)]
    for i in range(rows_a):
        for j in range(cols_b):
            for k in range(cols_a):
                result[i][j] += a[i][k] * b[k][j]
    return result

def main():
    size = 100
    a = [[(i + j) % 7 for j in range(size)] for i in range(size)]
    b = [[(i * j + 1) % 11 for j in range(size)] for i in range(size)]

    for _ in range(10):
        result = matrix_multiply(a, b)
    print(f"done: {result[0][0]}")

if __name__ == "__main__":
    main()
