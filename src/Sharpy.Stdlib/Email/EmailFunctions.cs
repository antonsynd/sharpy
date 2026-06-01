namespace Sharpy
{
    public static partial class EmailModule
    {
        public static EmailMessage MessageFromString(string text)
        {
            return EmailParser.ParseString(text);
        }

        public static EmailMessage MessageFromBytes(Bytes data)
        {
            return EmailParser.ParseBytes(data);
        }
    }
}
