s = 7
class H:
    a = H.s + 1
    s = 2
def main():
    print(H.a)
main()
