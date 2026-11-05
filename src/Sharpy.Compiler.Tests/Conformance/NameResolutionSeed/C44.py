class H:
    f = staticmethod(lambda: H.s)
    s = 2
def main():
    print(H.f())
main()
