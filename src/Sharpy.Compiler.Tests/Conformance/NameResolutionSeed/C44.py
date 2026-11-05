class H:
    f = lambda: H.s
    s = 2
def main():
    print(H.f())
main()
