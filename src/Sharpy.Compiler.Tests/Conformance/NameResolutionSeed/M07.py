def get():
    return lambda: x
x = 5
def main():
    print(get()())
main()
