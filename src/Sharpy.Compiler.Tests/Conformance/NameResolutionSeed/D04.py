def f():
    return 1
class H:
    a = f()
    @staticmethod
    def f():
        return 2
def main():
    print(H.a)
main()
