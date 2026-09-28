using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using BatchConvertIsoToXiso.Interfaces;
using Serilog;

namespace BatchConvertIsoToXiso.Services;

public partial class ExternalToolService : IExternalToolService
{
    private readonly ILogger _logger;
    private readonly string _bchunkPath;

    public ExternalToolService(ILogger logger)
    {
        _logger = logger.ForContext<ExternalToolService>();
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _bchunkPath = Path.Combine(appDir, "bchunk.exe");
    }

    public async Task<string?> ConvertCueBinToIsoAsync(string cuePath, string tempOutputDir, CancellationToken token)
    {
        var cueFileName = Path.GetFileName(cuePath);
        _logger.Information("Converting CUE/BIN to ISO: '{CueFileName}'...", cueFileName);

        var binPath = await ParseCueForBinFileAsync(cuePath, token);
        if (string.IsNullOrEmpty(binPath))
        {
            _logger.Information("Could not find BIN file for CUE: '{CueFileName}'", cueFileName);
            return null;
        }

        var binFileName = Path.GetFileName(binPath);
        _logger.Information("Found BIN file: '{BinFileName}'", binFileName);

        var outputBaseName = Path.GetFileNameWithoutExtension(cuePath);
        var result = await RunProcessAsync(_bchunkPath, $"\"{binPath}\" \"{cuePath}\" \"{outputBaseName}\"",
            tempOutputDir, outputBaseName, token);

        if (result != 0)
        {
            _logger.Information(
                "Failed to convert CUE/BIN to ISO for '{CueFileName}'. bchunk.exe exited with code {ExitCode}.",
                cueFileName, result);
            return null;
        }

        var isoFile = Directory.GetFiles(tempOutputDir, "*.iso").FirstOrDefault();
        if (isoFile != null)
        {
            _logger.Information("Successfully converted CUE/BIN to ISO: '{IsoFileName}'", Path.GetFileName(isoFile));
        }
        else
        {
            _logger.Warning("CUE/BIN conversion completed but no ISO file was found for '{CueFileName}'.", cueFileName);
        }

        return isoFile;
    }

    private async Task<int?> RunProcessAsync(string fileName, string arguments, string? workingDir, string contextName,
        CancellationToken token)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDir ?? AppDomain.CurrentDomain.BaseDirectory
            };

            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(token);
            var stdErrTask = process.StandardError.ReadToEndAsync(token);

            await using (token.Register(state =>
                         {
                             var p = (Process?)state;
                             if (p != null)
                             {
                                 // Offload to background thread to avoid blocking UI thread
                                 // Use CancellationToken.None because 'token' is already cancelled at this point
                                 _ = Task.Run(() => ProcessTerminatorHelper.TerminateProcess(p, contextName, _logger),
                                     CancellationToken.None);
                             }
                         }, process))
            {
                await process.WaitForExitAsync(token);
            }

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            if (!string.IsNullOrWhiteSpace(stdOut))
                _logger.Information("{Message:l}", stdOut.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stdErr))
                _logger.Information("{Message:l}", stdErr.TrimEnd());

            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Process execution canceled ({ContextName}).", contextName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Process execution failed ({ContextName}): {Message}", contextName, ex.Message);
            return null;
        }
    }

    private async Task<string?> ParseCueForBinFileAsync(string cuePath, CancellationToken token)
    {
        var cueDir = Path.GetDirectoryName(cuePath);
        if (cueDir == null) return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(cuePath, token);
            foreach (var line in lines)
            {
                var match = CueRegex().Match(line.Trim());
                if (!match.Success) continue;

                var quoted = match.Groups["Quoted"].Value;
                var unquoted = match.Groups["Unquoted"].Value;
                var rawBinName = !string.IsNullOrEmpty(quoted) ? quoted : unquoted;

                // Combine with CUE directory while preserving relative paths (e.g. "data\file.bin")
                var binPath = Path.Combine(cueDir, rawBinName);
                if (File.Exists(binPath)) return binPath;
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to parse CUE file: {CuePath}", cuePath);
        }

        var fallback = Path.ChangeExtension(cuePath, ".bin");
        return File.Exists(fallback) ? fallback : null;
    }

    [GeneratedRegex("""^FILE\s+(?:"(?<Quoted>.+)"|(?<Unquoted>\S+))\s+\S+""", RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CueRegex();
}