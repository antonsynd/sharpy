# The python twin of timeout_matrix_time.spy: the same program with python's spellings.
# Its output is timeout_matrix_time.expected; regenerate it from this directory with
#     python3 -I timeout_matrix_time.py > timeout_matrix_time.expected
# (python3 3.12.13 on macOS, 2026-10-06; no cell touches the network). #2263.

import time


def sleep_cell(v: float) -> str:
    try:
        time.sleep(v)
        return "None"
    except ValueError:
        return "ValueError"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def main() -> None:
    labels = ["-1", "-1e10", "-2.0", "-0.5", "-0.001", "-0.0005", "0", "0.01", "3e6", "1e10"]
    values = [-1.0, -1e10, -2.0, -0.5, -0.001, -0.0005, 0.0, 0.01, 3e6, 1e10]
    for i in range(len(values)):
        if labels[i] != "3e6":
            print(f"sleep {labels[i]} -> {sleep_cell(values[i])}")


main()
