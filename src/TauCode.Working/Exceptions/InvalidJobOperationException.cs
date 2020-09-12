﻿using System;
using System.Runtime.Serialization;

namespace TauCode.Working.Exceptions
{
    [Serializable]
    public class InvalidJobOperationException : JobException
    {
        public InvalidJobOperationException()
        {
        }

        public InvalidJobOperationException(string message)
            : base(message)
        {
        }

        public InvalidJobOperationException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected InvalidJobOperationException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
