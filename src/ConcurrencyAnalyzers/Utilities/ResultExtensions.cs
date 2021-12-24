using System.Diagnostics;

namespace ConcurrencyAnalyzers;

public static class ResultExtensions
{
    public static void ThrowIfFailure<TResult>(this TResult result) where TResult : IResult
    {
        if (!result.Success)
        {
            throw new UnsuccessfulResultException(result.ErrorMessage);
        }
    }

    public static T GetValueOrThrow<T>(this Result<T> result)
    {
        result.ThrowIfFailure();
        
        Debug.Assert(result.Success);
        return result.Value;
    }
}