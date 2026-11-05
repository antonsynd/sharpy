x = 1
class H:
    def m(self):
        x += 1
    def r(self):
        return x
def main():
    H().m()
    print(x, H().r())
main()
