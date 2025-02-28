using System.Net.Mime;
using Pastel;

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
namespace phdt;

public static class Logging
{
    public static void Log(string message, string? prefix = null, Structs.ConsoleColourScheme? colourScheme = null)
    {
        if (Program.MonochromeOutput)
        {
            colourScheme = null;
        }
        DateTime now = DateTime.Now;
        string _ = $"{(prefix == null ? $"[unknown]" : $"[{prefix}]")} {now:HH:mm:ss}:";
        if (colourScheme.HasValue)
        {
            _ = _.Pastel(colourScheme.Value.Prefix);
        }
        string __ = $" {message}";
        if (colourScheme.HasValue)
        {
            __ = __.Pastel(colourScheme.Value.Message);
        }
        Console.WriteLine(_+__);
    }
}