namespace XISOStudio.Interfaces;

/// <summary>
/// Moves tested image files to a destination folder.
/// </summary>
public interface IFileMover
{
    /// <summary>
    /// Moves a tested file into <paramref name="destinationFolder" />, creating the folder
    /// when needed and retrying transient failures. Returns normally without moving when the
    /// source is missing or the destination file already exists; throws when the move fails.
    /// </summary>
    /// <param name="sourceFile">Full path to the file to move.</param>
    /// <param name="destinationFolder">Folder the file is moved into.</param>
    /// <param name="moveReason">Reason for the move, used in the log (for example "successfully tested").</param>
    /// <param name="token">Token used to cancel the move.</param>
    /// <exception cref="IOException">When the file cannot be moved (after retrying transient errors).</exception>
    Task MoveTestedFileAsync(string sourceFile, string destinationFolder, string moveReason, CancellationToken token);
}