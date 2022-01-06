using System;
using System.Collections.Generic;
using System.Reflection;
using CommandLine;
using Xunit;

namespace ConcurrencyAnalyzers.Tests
{
    public class CommandLineOptionsAreCorrect
    {
        [Theory]
        [InlineData(typeof(AttachOptions))]
        [InlineData(typeof(ProcessDumpOptions))]
        public void CommandLineOptionsDoNotHaveDuplicateSwitches(Type type)
        {
            // Unfortunately, CommandLineOptions parser fails with 'InvalidOperationException' if
            // there are more then one flag specified in different command line options.
            // In order to avoid this, this test checks that all the short names are unique.
            var names = new Dictionary<string, string>();
            foreach (var property in type.GetProperties())
            {
                var optionAttribute = property.GetCustomAttribute<OptionAttribute>();
                if (optionAttribute is not null)
                {
                    var name = optionAttribute.ShortName;
                    if (!names.TryAdd(name, property.Name))
                    {
                        Assert.True(false, $"A short name '{name}' is already defined for property '{names[name]}'.");
                    }
                }
            }
        }

        [Theory]
        [InlineData(typeof(AttachOptions))]
        [InlineData(typeof(ProcessDumpOptions))]
        public void CommandLineOptionsDoNotHaveDuplicateParameters(Type type)
        {
            var names = new Dictionary<string, string>();
            foreach (var property in type.GetProperties())
            {
                var optionAttribute = property.GetCustomAttribute<OptionAttribute>();
                if (optionAttribute is not null)
                {
                    var name = optionAttribute.LongName;
                    if (!names.TryAdd(name, property.Name))
                    {
                        Assert.True(false, $"A short name '{name}' is already defined for property '{names[name]}'.");
                    }
                }
            }
        }
    }
}