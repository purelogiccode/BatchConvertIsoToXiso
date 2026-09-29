using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

// XISOSharp keeps the $SystemUpdate filter and the process working directory as global
// state (it chdir's during extract/pack), so tests that exercise the conversion pipeline
// must not run in parallel with each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace XISOStudio.Tests;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}