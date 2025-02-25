using System;

namespace Sharpy
{
    public class ValueError(string message) : Exception(message)
    {
    }
}
