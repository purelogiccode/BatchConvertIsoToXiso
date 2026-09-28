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
                "1. Convert: Converts standard Xbox ISO files to optimized XISO, ZAR, or CSO format. Supports archives (.zip, .7z, .rar).");
            logger.Information(
                "2. Test Integrity: Verifies ISO/CSO (XDVDFS) and ZAR structure and readability.");
            logger.Information(
                "   NOTE: This test checks if the image is structurally valid and readable. It does NOT perform data checksum (MD5/SHA) verification.");
            logger.Information("3. Explorer: Explore the content of .iso, .cso, and .zar files.");
            logger.Information("");
            logger.Information("IMPORTANT: This tool ONLY works with Xbox and Xbox 360 ISO files.");
            logger.Information("It cannot convert or test ISOs from PlayStation, PlayStation 2, or other consoles.");
            logger.Information("");

            // XISO conversion is performed in-process by the XISOSharp library
            logger.Information("INFO: XISOSharp conversion engine loaded. XISO conversion is enabled.");

            logger.Information("");

            logger.Information("--- Ready ---");
        }
    }
}