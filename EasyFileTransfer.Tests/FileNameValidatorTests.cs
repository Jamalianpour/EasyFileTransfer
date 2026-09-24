using EasyFileTransfer.Internal;

namespace EasyFileTransfer.Tests;

public class FileNameValidatorTests
{
    [Theory]
    [InlineData("report.pdf")]
    [InlineData("archive.tar.gz")]
    [InlineData("no-extension")]
    [InlineData(".hidden")]
    [InlineData("name with spaces.txt")]
    [InlineData("\u0641\u0627\u06CC\u0644.txt")]
    [InlineData("console.log")]
    [InlineData("CONNECT.txt")]
    public void Accepts_ordinary_names(string name) => Assert.True(FileNameValidator.IsValid(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("sub/file.txt")]
    [InlineData("sub\\file.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("C:evil.txt")]
    [InlineData("file.txt:stream")]
    [InlineData("\\\\server\\share\\x")]
    [InlineData("trailing-dot.")]
    [InlineData("trailing-space ")]
    [InlineData("nul\0byte")]
    [InlineData("new\nline")]
    [InlineData("wild*card")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("NUL.tar.gz")]
    [InlineData("COM1")]
    [InlineData("lpt9.log")]
    [InlineData("COM\u00B9")]
    public void Rejects_dangerous_names(string name) => Assert.False(FileNameValidator.IsValid(name));

    [Fact]
    public void Rejects_names_longer_than_255_characters()
    {
        Assert.True(FileNameValidator.IsValid(new string('a', 255)));
        Assert.False(FileNameValidator.IsValid(new string('a', 256)));
    }
}
