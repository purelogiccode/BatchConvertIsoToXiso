namespace XboxIsoStudio.Interfaces;

public interface IFileMover
{
    Task MoveTestedFileAsync(string sourceFile, string destinationFolder, string moveReason, CancellationToken token);
}