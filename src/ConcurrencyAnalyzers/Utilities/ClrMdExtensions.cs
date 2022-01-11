using System;
using Microsoft.Diagnostics.Runtime;

namespace ConcurrencyAnalyzers.Utilities
{
    internal static class ClrMdExtensions
    {
        public static T? TryGetValue<T>(this ClrStaticField? field, ClrRuntime runtime) where T : unmanaged
        {
            return field.TryRead(runtime, static (field, domain) => field.Read<T>(domain));
        }

        public static ClrValueType? TryReadStruct(this ClrStaticField? field, ClrRuntime runtime)
        {
            return field.TryRead(runtime, static (field, domain) => field.ReadStruct(domain));
        }

        public static ClrObject? TryReadObject(this ClrStaticField? field, ClrRuntime runtime)
        {
            return field.TryRead(runtime, static (field, domain) => field.ReadObject(domain));
        }

        public static T? TryRead<T>(this ClrStaticField? field, ClrRuntime runtime, Func<ClrStaticField, ClrAppDomain, T> reader)
        {
            if (field is null)
            {
                return default;
            }

            foreach (var domain in runtime.AppDomains)
            {
                if (field.IsInitialized(domain))
                {
                    return reader(field, domain);
                }
            }

            return default;
        }
    }
}