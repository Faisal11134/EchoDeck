using EchoDeck.App.Models;

namespace EchoDeck.Tests.Models;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("F8", false, false, false, false, "F8")]
    [InlineData("Ctrl + F8", true, false, false, false, "Ctrl + F8")]
    [InlineData("Ctrl+Alt+F8", true, true, false, false, "Ctrl + Alt + F8")]
    [InlineData("  Ctrl  +  Shift  +  F1  ", true, false, true, false, "Ctrl + Shift + F1")]
    [InlineData("Ctrl + Alt + Shift + Win + F24", true, true, true, true, "Ctrl + Alt + Shift + Win + F24")]
    [InlineData("", false, false, false, false, "")]
    [InlineData(null, false, false, false, false, "")]
    public void Parse_ValidInput_ReturnsCorrectGesture(string? input, bool ctrl, bool alt, bool shift, bool win, string expectedDisplay)
    {
        var gesture = HotkeyGesture.Parse(input);

        Assert.Equal(ctrl, gesture.Ctrl);
        Assert.Equal(alt, gesture.Alt);
        Assert.Equal(shift, gesture.Shift);
        Assert.Equal(win, gesture.Win);
        Assert.Equal(expectedDisplay, gesture.DisplayText);
    }

    [Fact]
    public void Parse_OnlyModifiers_NoKey()
    {
        var gesture = HotkeyGesture.Parse("Ctrl + Alt");
        Assert.True(gesture.Ctrl);
        Assert.True(gesture.Alt);
        Assert.Equal(string.Empty, gesture.Key);
    }

    [Fact]
    public void Parse_CaseInsensitive()
    {
        var gesture = HotkeyGesture.Parse("ctrl+alt+shift+win+f12");
        Assert.True(gesture.Ctrl);
        Assert.True(gesture.Alt);
        Assert.True(gesture.Shift);
        Assert.True(gesture.Win);
        Assert.Equal("f12", gesture.Key);
    }

    [Fact]
    public void ModifiersMask_None_ReturnsZero()
    {
        var gesture = new HotkeyGesture { Key = "F8" };
        Assert.Equal(0u, gesture.ModifiersMask);
    }

    [Fact]
    public void ModifiersMask_All_ReturnsCorrectMask()
    {
        var gesture = new HotkeyGesture { Ctrl = true, Alt = true, Shift = true, Win = true };
        Assert.Equal(0x000Fu, gesture.ModifiersMask);
    }

    [Fact]
    public void ModifiersMask_Alt_Returns1()
    {
        var gesture = new HotkeyGesture { Alt = true };
        Assert.Equal(0x0001u, gesture.ModifiersMask);
    }

    [Fact]
    public void ModifiersMask_Ctrl_Returns2()
    {
        var gesture = new HotkeyGesture { Ctrl = true };
        Assert.Equal(0x0002u, gesture.ModifiersMask);
    }

    [Fact]
    public void ModifiersMask_Shift_Returns4()
    {
        var gesture = new HotkeyGesture { Shift = true };
        Assert.Equal(0x0004u, gesture.ModifiersMask);
    }

    [Fact]
    public void ModifiersMask_Win_Returns8()
    {
        var gesture = new HotkeyGesture { Win = true };
        Assert.Equal(0x0008u, gesture.ModifiersMask);
    }

    [Fact]
    public void ToString_ReturnsDisplayText()
    {
        var gesture = new HotkeyGesture { Ctrl = true, Key = "F1" };
        Assert.Equal(gesture.DisplayText, gesture.ToString());
        Assert.Equal("Ctrl + F1", gesture.ToString());
    }

    [Fact]
    public void Parse_RoundTrip()
    {
        var original = "Ctrl + Alt + Shift + Win + F24";
        var gesture = HotkeyGesture.Parse(original);
        Assert.Equal(original, gesture.DisplayText);
    }
}
