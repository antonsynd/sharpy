namespace Sharpy
{
    public abstract class Object : object
    {
        public string Str()
        {
            return Repr();
        }

        public override sealed string ToString()
        {
            return Str();
        }

        public abstract string Repr();
    }
}
