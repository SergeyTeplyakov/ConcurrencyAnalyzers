using System;

namespace ConcurrencyAnalyzers;

public class UnsuccessfulResultException : Exception
{
    public UnsuccessfulResultException(string? message) : base(message)
    {
    }
}