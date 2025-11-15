namespace Sharpy.Core;

public readonly struct Bytes(byte[] data)
{
    private readonly byte[] _data = data;
}
