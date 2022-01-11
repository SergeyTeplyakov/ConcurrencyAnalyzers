using System.Diagnostics.ContractsLight;
using System.Runtime.CompilerServices;

namespace ConcurrencyAnalyzers.Utilities
{
    public static class ResultExtensions
    {
        public static void ThrowIfFailure<TResult>(this TResult result, [CallerArgumentExpression("result")] string resultExpression = "") where TResult : IResult
        {
            if (!result.Success)
            {
                string message = GetFullErrorMessage(result, resultExpression);
                throw new UnsuccessfulResultException(message);
            }
        }

        private static string GetFullErrorMessage<TResult>(TResult result, string resultExpression) where TResult : IResult
        {
            return $"Expression '{resultExpression}' produced unsuccessful result: {result.ErrorMessage}";
        }

        public static T GetValueOrThrow<T>(this Result<T> result, [CallerArgumentExpression("result")] string resultExpression = "")
        {
            result.ThrowIfFailure(resultExpression);

            Contract.Assert(result.Success);
            return result.Value;
        }

        public static T AssertSuccess<T>(this Result<T> result,
            [CallerArgumentExpression("result")] string resultExpression = "")
        {
            if (!result.Success)
            {
                Contract.Assert(false, GetFullErrorMessage(result, resultExpression));
            }

            return result.Value;
        }
    }
}