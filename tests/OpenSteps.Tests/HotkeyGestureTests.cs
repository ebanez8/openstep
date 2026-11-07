using OpenSteps.Core.Models;

namespace OpenSteps.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+R", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "R")]
    [InlineData("alt + shift + F9", HotkeyModifiers.Alt | HotkeyModifiers.Shift, "F9")]
    [InlineData("Ctrl+Win+5", HotkeyModifiers.Ctrl | HotkeyModifiers.Win, "D5")]
    public void TryParse_ReadsModifiersAndKey(string text, HotkeyModifiers modifiers, string key)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture));
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("R")]
    [InlineData("Hyper+R")]
    public void TryParse_RejectsMissingOrUnknownModifiers(string? text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _));
    }

    [Fact]
    public void ToString_UsesFixedModifierOrderAndPlainDigits()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Ctrl, "D1");

        Assert.Equal("Ctrl+Alt+Shift+1", gesture.ToString());
        Assert.True(HotkeyGesture.TryParse(gesture.ToString(), out var roundTripped));
        Assert.Equal(gesture, roundTripped);
    }
}
