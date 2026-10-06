# The python twin of timeout_matrix_threading.spy: the same program with python's spellings.
# Its output is timeout_matrix_threading.expected; regenerate it from this directory with
#     python3 -I timeout_matrix_threading.py > timeout_matrix_threading.expected
# (python3 3.12.13 on macOS, 2026-10-06; no cell touches the network). #2263.

import threading


def lock_cell(state: str, blocking: bool, v: float) -> str:
    lk = threading.Lock()
    if state == "held":
        lk.acquire()
    try:
        return str(lk.acquire(blocking, v))
    except ValueError:
        return "ValueError"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def hold_rlock(lk: threading.RLock, ready: threading.Event, hold: threading.Event) -> None:
    lk.acquire()
    ready.set()
    hold.wait()
    lk.release()


def try_rlock(lk: threading.RLock, blocking: bool, v: float) -> str:
    try:
        return str(lk.acquire(blocking, v))
    except ValueError:
        return "ValueError"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def rlock_cell(state: str, blocking: bool, v: float) -> str:
    lk = threading.RLock()
    ready = threading.Event()
    hold = threading.Event()
    w = threading.Thread(target=lambda: hold_rlock(lk, ready, hold))
    if state == "owned":
        lk.acquire()
    if state == "held":
        w.start()
        ready.wait()
    r = try_rlock(lk, blocking, v)
    hold.set()
    if state == "held":
        w.join()
    return r


def sem_cell(kind: str, state: str, blocking: bool, v: float) -> str:
    try:
        if kind == "Semaphore":
            s = threading.Semaphore(1 if state == "free" else 0)
            return str(s.acquire(blocking, v))
        b = threading.BoundedSemaphore(1)
        if state == "held":
            b.acquire()
        return str(b.acquire(blocking, v))
    except ValueError:
        return "ValueError"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def event_cell(state: str, v: float) -> str:
    e = threading.Event()
    if state == "set":
        e.set()
    try:
        return str(e.wait(v))
    except ValueError:
        return "ValueError"
    except OverflowError:
        return "OverflowError"
    except Exception as ex:
        return "other " + type(ex).__name__


def try_barrier(b: threading.Barrier, v: float) -> str:
    try:
        b.wait(v)
        return "returned"
    except threading.BrokenBarrierError:
        return "BrokenBarrierError"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def barrier_cell(parties: int, v: float) -> str:
    b = threading.Barrier(parties)
    r = try_barrier(b, v)
    return r + " broken=" + str(b.broken)


def try_join(t: threading.Thread, v: float) -> str:
    try:
        t.join(v)
        return "None"
    except OverflowError:
        return "OverflowError"
    except Exception as e:
        return "other " + type(e).__name__


def join_cell(state: str, v: float) -> str:
    hold = threading.Event()
    t = threading.Thread(target=lambda: hold.wait())
    t.start()
    if state != "alive":
        hold.set()
    if state == "joined":
        t.join()
    r = try_join(t, v)
    if state == "alive" and r == "None":
        r = r + " alive=" + str(t.is_alive())
    hold.set()
    t.join()
    return r


def timer_cell(v: float) -> str:
    calls: list[int] = []
    t = threading.Timer(v, lambda: calls.append(1))
    t.start()
    if v > 1.0:
        t.cancel()
    t.join()
    return "ran=" + str(len(calls))


def main() -> None:
    labels = ["-1", "-1e10", "-2.0", "-0.5", "-0.001", "-0.0005", "0", "0.01", "3e6", "1e10"]
    values = [-1.0, -1e10, -2.0, -0.5, -0.001, -0.0005, 0.0, 0.01, 3e6, 1e10]
    print("TIMEOUT_MAX " + str(threading.TIMEOUT_MAX))
    for i in range(len(values)):
        lab = labels[i]
        v = values[i]
        forever = lab == "-1" or lab == "3e6"
        for st in ["free", "held"]:
            for blocking in [True, False]:
                if not (st == "held" and blocking and forever):
                    print(f"Lock {st} blocking={blocking} {lab} -> {lock_cell(st, blocking, v)}")
        for st in ["free", "owned", "held"]:
            for blocking in [True, False]:
                if not (st == "held" and blocking and forever):
                    print(f"RLock {st} blocking={blocking} {lab} -> {rlock_cell(st, blocking, v)}")
        for kind in ["Semaphore", "BoundedSemaphore"]:
            for st in ["free", "held"]:
                for blocking in [True, False]:
                    if not (st == "held" and blocking and lab == "3e6"):
                        print(f"{kind} {st} blocking={blocking} {lab} -> {sem_cell(kind, st, blocking, v)}")
        for st in ["unset", "set"]:
            if not (st == "unset" and lab == "3e6"):
                print(f"Event {st} {lab} -> {event_cell(st, v)}")
        for parties in [2, 1]:
            if not (parties == 2 and lab == "3e6"):
                print(f"Barrier({parties}) {lab} -> {barrier_cell(parties, v)}")
        for st in ["alive", "released", "joined"]:
            if not (st == "alive" and lab == "3e6"):
                print(f"join {st} {lab} -> {join_cell(st, v)}")
        if lab != "1e10":
            print(f"Timer {lab} -> {timer_cell(v)}")


main()
