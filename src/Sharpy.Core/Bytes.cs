namespace Sharpy.Core;

/// <summary>
/// Represents an immutable sequence of bytes (similar to Python's bytes type).
/// </summary>
public readonly struct Bytes
{
    private readonly byte[] _data;

    public Bytes(byte[] data)
    {
        _data = data ?? [];
    }

    public int Length => _data.Length;

    public byte this[int index] => _data[index];

    public byte[] ToArray() => _data;

    public override string ToString()
    {
        return $"b'{BitConverter.ToString(_data).Replace("-", " ")}'";
    }
}
