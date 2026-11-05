from functools import lru_cache
class H:
    N = 4
    @lru_cache(maxsize=N)
    def f(self, x):
        return x
def main():
    print(H().f(3))
main()
