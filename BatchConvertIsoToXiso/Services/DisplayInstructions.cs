using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace BatchConvertIsoToXiso.Services;

public static class DisplayInstructions
{
    private static volatile ILogger? _logger;

    public static void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    public static void DisplayInitialInstructions()
    {
        var logger = _logger;
        if (logger != null)
        {
            logger.Information("Welcome to 'Batch Convert ISO to XISO'.");
            logger.Information("");
            logger.Information("This application provides three main functions, available in the tabs above:");
            logger.Information(
                "1. Convert: Converts standard Xbox ISO files to the optimized XISO format. Supports archives (.zip, .7z, .rar) and CUE/BIN files.");
            logger.Information("2. Test Integrity: Verifies the XDVDFS file system structure and sector readability.");
            logger.Information(
                "   NOTE: This test checks if the ISO is structurally valid and readable. It does NOT perform data checksum (MD5/SHA) verification.");
            logger.Information("3. Explorer: Explore the content of .iso files.");
            logger.Information("");
            logger.Information("IMPORTANT: This tool ONLY works with Xbox and Xbox 360 ISO files.");
            logger.Information("It cannot convert or test ISOs from PlayStation, PlayStation 2, or other consoles.");
            logger.Information("");

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

            if (isArm64)
            {
                logger.Information(
                    "WARNING: Running on ARM64. CUE/BIN conversion is disabled because the required tool (bchunk.exe) is not compatible with ARM64.");
            }
            else
            {
                var bchunkPath = Path.Combine(appDirectory, "bchunk.exe");
                if (File.Exists(bchunkPath))
                {
                    logger.Information("INFO: bchunk.exe found. CUE/BIN conversion is enabled.");
                }
                else
                {
                    logger.Information("WARNING: bchunk.exe not found. CUE/BIN conversion will fail.");
                }
            }

            // XISO conversion is performed in-process by the XISOSharp library
            logger.Information("INFO: XISOSharp conversion engine loaded. XISO conversion is enabled.");

            logger.Information("");

            logger.Information("--- Ready ---");
        }
    }
}