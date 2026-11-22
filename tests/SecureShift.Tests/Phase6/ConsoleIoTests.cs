using SecureShift.ConsoleApp.Menus;

namespace SecureShift.Tests.Phase6;

/// <summary>Bugs found in the Windows rehearsal (piping keystrokes from Windows PowerShell 5.1).</summary>
public class ConsoleIoTests
{
    [Theory]
    [InlineData("﻿1", "1")]                 // UTF-8 BOM read as UTF-8
    [InlineData("ï»¿1", "1")]     // UTF-8 BOM read with an old code page
    [InlineData("1", "1")]
    [InlineData("", "")]
    public void The_byte_order_mark_of_piped_input_is_removed(string line, string expected) =>
        Assert.Equal(expected, SystemConsoleIO.StripBom(line));

    [Fact]
    public void Null_means_end_of_input() => Assert.Null(SystemConsoleIO.StripBom(null));
}
