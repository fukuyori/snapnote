using System.Windows.Input;

namespace SnapNoteStudio.Services;

public static class ShortcutUtilities
{
    public static bool TryParse(string? shortcut, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        foreach (var part in shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "ALT":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "SHIFT":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModifierKeys.Windows;
                    break;
                default:
                    if (Enum.TryParse<Key>(part, true, out var parsedKey))
                        key = parsedKey;
                    break;
            }
        }

        return key != Key.None;
    }

    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
