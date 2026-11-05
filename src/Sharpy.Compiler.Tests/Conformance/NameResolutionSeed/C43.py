class H:
    s = 2
    f = staticmethod(lambda: s)
def main():
    print(H.f())
main()
