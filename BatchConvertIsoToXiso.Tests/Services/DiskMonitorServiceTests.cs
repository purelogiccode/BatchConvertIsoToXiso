using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Services;
using Moq;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class DiskMonitorServiceTests
{
    private static DiskMonitorService CreateService()
    {
        var mockLogger = new Mock<ILogger>();
        return new DiskMonitorService(mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void ConstructorInitializesWithValidParameters()
    {
        var service = CreateService();
        Assert.NotNull(service);
    }

    [Fact]
    public void ConstructorDriveLetterIsNull()
    {
        var service = CreateService();
        Assert.Null(service.CurrentDriveLetter);
    }

    [Fact]
    public void ConstructorStatusMessageIsNull()
    {
        var service = CreateService();
        Assert.Null(service.StatusMessage);
    }

    #endregion

    #region GetAvailableFreeSpace Tests

    [Fact]
    public void GetAvailableFreeSpaceNullPathReturnsZero()
    {
        var service = CreateService();
        var result = service.GetAvailableFreeSpace(null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetAvailableFreeSpaceEmptyPathReturnsZero()
    {
        var service = CreateService();
        var result = service.GetAvailableFreeSpace("");
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetAvailableFreeSpaceLocalDriveReturnsPositiveValue()
    {
        var service = CreateService();
        var result = service.GetAvailableFreeSpace("C:\\");
        Assert.True(result > 0, $"Expected positive free space, got {result}");
    }

    [Fact]
    public void GetAvailableFreeSpaceLocalDriveSubfolderReturnsPositiveValue()
    {
        var service = CreateService();
        var result = service.GetAvailableFreeSpace("C:\\Windows");
        Assert.True(result > 0, $"Expected positive free space, got {result}");
    }

    #endregion

    #region FindDriveWithFreeSpace Tests

    [Fact]
    public void FindDriveWithFreeSpaceZeroBytesReturnsDrive()
    {
        var service = CreateService();
        var result = service.FindDriveWithFreeSpace(0);
        Assert.NotNull(result);
    }

    [Fact]
    public void FindDriveWithFreeSpaceEnormousRequirementReturnsNull()
    {
        var service = CreateService();
        // 1 Exabyte - no drive should have this much space
        var result = service.FindDriveWithFreeSpace(long.MaxValue / 2);
        Assert.Null(result);
    }

    [Fact]
    public void FindDriveWithFreeSpaceExcludesSpecifiedDrive()
    {
        var service = CreateService();
        const string cDrive = "C:";

        var result = service.FindDriveWithFreeSpace(0, cDrive);

        if (result != null)
        {
            Assert.DoesNotContain("C:", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region StopMonitoring Tests

    [Fact]
    public void StopMonitoringClearsDriveLetter()
    {
        var service = CreateService();
        service.StopMonitoring();
        Assert.Null(service.CurrentDriveLetter);
    }

    [Fact]
    public void StopMonitoringClearsStatusMessage()
    {
        var service = CreateService();
        service.StopMonitoring();
        Assert.Null(service.StatusMessage);
    }

    [Fact]
    public void StopMonitoringCalledMultipleTimesDoesNotThrow()
    {
        var service = CreateService();
        service.StopMonitoring();
        var exception = Record.Exception(service.StopMonitoring);
        Assert.Null(exception);
    }

    #endregion

    #region Speed Format Tests

    [Fact]
    public void GetCurrentReadSpeedFormattedWithoutMonitoringReturnsNa()
    {
        var service = CreateService();
        var result = service.GetCurrentReadSpeedFormatted();
        Assert.Equal("N/A", result);
    }

    [Fact]
    public void GetCurrentWriteSpeedFormattedWithoutMonitoringReturnsNa()
    {
        var service = CreateService();
        var result = service.GetCurrentWriteSpeedFormatted();
        Assert.Equal("N/A", result);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void DisposeCalledMultipleTimesDoesNotThrow()
    {
        var service = CreateService();
        service.Dispose();
        var exception = Record.Exception(service.Dispose);
        Assert.Null(exception);
    }

    [Fact]
    public void DisposeAfterStopMonitoringDoesNotThrow()
    {
        var service = CreateService();
        service.StopMonitoring();
        var exception = Record.Exception(service.Dispose);
        Assert.Null(exception);
    }

    #endregion
}