# The python twin of timeout_matrix_http.spy: the same program with python's spellings (http.client for Sharpy's http module).
# Its output is timeout_matrix_http.expected; regenerate it from this directory with
#     python3 -I timeout_matrix_http.py > timeout_matrix_http.expected
# (python3 3.12.13 on macOS, 2026-10-06; no cell touches the network). #2263.

import http.client
import requests


def http_cell(v: float) -> str:
    try:
        c = http.client.HTTPConnection("127.0.0.1", 9, timeout=v)
        c.request("GET", "/")
        return "returned"
    except ValueError:
        return "ValueError"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def requests_cell(v: float) -> str:
    try:
        requests.get("http://127.0.0.1:9/", timeout=v)
        return "returned"
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
        lab = labels[i]
        if not (lab == "0" or lab == "0.01" or lab == "3e6"):
            print(f"HTTPConnection.request {lab} -> {http_cell(values[i])}")
        if not (lab == "0.01" or lab == "3e6"):
            print(f"requests.get {lab} -> {requests_cell(values[i])}")


main()
