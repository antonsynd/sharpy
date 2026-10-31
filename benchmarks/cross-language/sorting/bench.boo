import System
import System.Collections.Generic

def quicksort(arr as List[of long]) as List[of long]:
    if len(arr) <= 1:
        return arr
    pivot = arr[len(arr) / 2]
    left = List[of long](x for x in arr if x < pivot)
    middle = List[of long](x for x in arr if x == pivot)
    right = List[of long](x for x in arr if x > pivot)
    result = quicksort(left)
    result.AddRange(middle)
    result.AddRange(quicksort(right))
    return result

def main():
    rng = Random(42)
    count = 0
    for i in range(100):
        data = List[of long](cast(long, rng.Next(10001)) for _ in range(1000))
        sorted_data = quicksort(data)
        count = i + 1
    print "sorted ${count} arrays"

main()
