def f():
    return 1
class H:
    @staticmethod
    def f():
        return 2
    a = f()
def main():
    print(H.a)
main()
