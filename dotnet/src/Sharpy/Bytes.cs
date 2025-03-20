namespace Sharpy
{
    public struct Bytes(byte[] data)
    {
        private readonly byte[] _data = data;
    }
}
