using BatchConvertIsoToXiso.Models.XisoDefinitions;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Models;

public class XisoFsFileAttributesTests
{
    [Fact]
    public void EnumValuesAreCorrect()
    {
        Assert.Equal(0x01, (byte)XisoFsFileAttributes.ReadOnly);
        Assert.Equal(0x02, (byte)XisoFsFileAttributes.Hidden);
        Assert.Equal(0x04, (byte)XisoFsFileAttributes.System);
        Assert.Equal(0x10, (byte)XisoFsFileAttributes.Directory);
        Assert.Equal(0x20, (byte)XisoFsFileAttributes.Archive);
        Assert.Equal(0x80, (byte)XisoFsFileAttributes.Normal);
    }

    [Fact]
    public void FlagsCanBeCombined()
    {
        const XisoFsFileAttributes combined = XisoFsFileAttributes.ReadOnly | XisoFsFileAttributes.Hidden;
        Assert.Equal(0x03, (byte)combined);
    }

    [Fact]
    public void DirectoryFlagCanBeTested()
    {
        const XisoFsFileAttributes attr = XisoFsFileAttributes.Directory | XisoFsFileAttributes.ReadOnly;
        Assert.NotEqual((byte)0, (byte)(attr & XisoFsFileAttributes.Directory));
        Assert.NotEqual((byte)0, (byte)(attr & XisoFsFileAttributes.ReadOnly));
        Assert.Equal((byte)0, (byte)(attr & XisoFsFileAttributes.Hidden));
    }
}