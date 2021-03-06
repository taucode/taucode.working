﻿using System;
using System.Runtime.Serialization;

namespace TauCode.Working.Exceptions
{
    [Serializable]
    public class WorkingException : Exception
    {
        public WorkingException()
        {
        }

        public WorkingException(string message)
            : base(message)
        {
        }

        public WorkingException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected WorkingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
