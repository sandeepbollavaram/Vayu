namespace Vayu.Memory.Tests;

public class MemoryOptionsTests
{
    [Fact]
    public void Defaults_AreSafeForLocalUse()
    {
        var options = new MemoryOptions();

        Assert.Equal(@"%LOCALAPPDATA%\Vayu\vayu.db", options.DatabasePath);
        Assert.True(options.EnableAuditLog);
        Assert.Equal(100, options.MaxRecentItems);
    }

    [Fact]
    public void Record_Supports_WithExpression()
    {
        var options = new MemoryOptions();
        var custom = options with { DatabasePath = @"C:\Temp\vayu.db", MaxRecentItems = 50 };

        Assert.Equal(@"C:\Temp\vayu.db", custom.DatabasePath);
        Assert.Equal(50, custom.MaxRecentItems);
        Assert.True(custom.EnableAuditLog);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new MemoryOptions { DatabasePath = "a", MaxRecentItems = 10 };
        var b = new MemoryOptions { DatabasePath = "a", MaxRecentItems = 10 };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
