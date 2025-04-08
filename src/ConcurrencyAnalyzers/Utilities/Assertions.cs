﻿using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.ContractsLight;
using System.Runtime.CompilerServices;

namespace ConcurrencyAnalyzers.Utilities
{
    public static class Assertions
    {
        public static T AssertNotNull<T>([NotNull] this T? instance,
            [CallerArgumentExpression("instance")] string instanceCreationExpression = "")
        {
            Contract.Assert(instance is not null, userMessage: instanceCreationExpression);
            return instance;
        }

        public static T AssertNotNull<T>([NotNull] this T? instance,
            [CallerArgumentExpression("instance")] string instanceCreationExpression = "") where T : struct
        {
            Contract.Assert(instance is not null, userMessage: instanceCreationExpression);
            return instance.Value;
        }

        public static T RequiresNotNull<T>([NotNull] this T? instance,
            [CallerArgumentExpression("instance")] string instanceCreationExpression = "")
        {
            Contract.Requires(instance is not null, userMessage: instanceCreationExpression);
            return instance;
        }
    }
}