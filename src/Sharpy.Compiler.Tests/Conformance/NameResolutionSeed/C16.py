class H:
    a = H.f()
    @staticmethod
    def f():
        return 3
def main():
    print(H.a)
main()
