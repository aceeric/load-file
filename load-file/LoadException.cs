using System;

namespace load_file
{
    class LoadException : Exception
    {
        public LoadException(string Message) : base(Message) { }
    }
}
