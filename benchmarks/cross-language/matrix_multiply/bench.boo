import System.Collections.Generic

def matrix_multiply(a as List[of List[of long]], b as List[of List[of long]]) as List[of List[of long]]:
    rows_a = len(a)
    cols_a = len(a[0])
    cols_b = len(b[0])
    result = List[of List[of long]](List[of long](0L for _ in range(cols_b)) for _ in range(rows_a))
    for i in range(rows_a):
        for j in range(cols_b):
            for k in range(cols_a):
                result[i][j] = result[i][j] + a[i][k] * b[k][j]
    return result

def main():
    size = 100
    a = List[of List[of long]](List[of long](cast(long, (i + j) % 7) for j in range(size)) for i in range(size))
    b = List[of List[of long]](List[of long](cast(long, (i * j + 1) % 11) for j in range(size)) for i in range(size))

    result = matrix_multiply(a, b)
    for _ in range(1, 10):
        result = matrix_multiply(a, b)
    print "done: ${result[0][0]}"

main()
