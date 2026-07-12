import numpy as np


def main():
    # Numeric-path matrix multiply: numpy's @ (native BLAS) is the idiomatic fast
    # route, the counterpart to the pure-list matrix_multiply benchmark.
    size = 256
    a = np.array([[float((i + j) % 7) for j in range(size)] for i in range(size)])
    b = np.array([[float((i * j + 1) % 11) for j in range(size)] for i in range(size)])

    result = a @ b
    n = 1
    while n < 200:
        result = a @ b
        n = n + 1
    print(f"done: {int(result[0, 0])}")


if __name__ == "__main__":
    main()
