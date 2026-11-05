x = 1
v = 99
class H:
    v = 1
    def m(self):
        v = 5
        x = 6
def main():
    H().m()
    print(v, x)
main()
