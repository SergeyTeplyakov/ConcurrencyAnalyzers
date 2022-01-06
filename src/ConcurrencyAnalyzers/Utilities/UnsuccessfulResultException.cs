using System;

namespace ConcurrencyAnalyzers.Utilities
{
    public class UnsuccessfulResultException : Exception
    {
        public UnsuccessfulResultException(string? message) : base(message)
        {
        }
    }
}