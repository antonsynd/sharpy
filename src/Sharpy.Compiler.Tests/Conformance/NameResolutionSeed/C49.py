class H:
    @staticmethod
    def read():
        return H.sp
    a = H.read()
    sp = 4
def main():
    print(H.a)
main()
