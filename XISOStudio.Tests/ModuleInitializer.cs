using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

// XISOSharp keeps the $SystemUpdate filter and the process working directory as global
// state (it chdir's during extract/pack), so tests that exercise the conversion pipeline
// must not run in parallel with each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace XISOStudio.Tests;

/// <summary>Registers the required code page encoding provider before any test in the assembly runs.</summary>
internal static class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}