total = 0
class C:
    @staticmethod
    def add(k):
        total = total + k
def main():
    C.add(3)
    print(total)
main()
