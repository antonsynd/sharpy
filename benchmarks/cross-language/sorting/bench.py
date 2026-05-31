import random

def quicksort(arr: list[int]) -> list[int]:
    if len(arr) <= 1:
        return arr
    pivot = arr[len(arr) // 2]
    left = [x for x in arr if x < pivot]
    middle = [x for x in arr if x == pivot]
    right = [x for x in arr if x > pivot]
    return quicksort(left) + middle + quicksort(right)

def main():
    random.seed(42)
    for i in range(100):
        data = [random.randint(0, 10000) for _ in range(1000)]
        sorted_data = quicksort(data)
    print(f"sorted {i + 1} arrays")

if __name__ == "__main__":
    main()
