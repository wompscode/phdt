using System.Net.Mime;
using System.Text.RegularExpressions;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
namespace phdt;

public static class Logging
{
    public static void Log(string message, string? prefix = null, ConsoleColor? prefixColour = null, ConsoleColor? messageColour = null, bool samePosition = false)
    {
        if(samePosition && !Program.NewLines) Console.SetCursorPosition(0, Console.CursorTop - 1);
        if (Program.MonochromeOutput)
        {
            prefixColour = null;
            messageColour = null;
        }
        DateTime now = DateTime.Now;
        //ConsoleColor prevColour = Console.ForegroundColor;
        if (prefixColour.HasValue)
        {
            Console.ForegroundColor = prefixColour.Value;
        }

        Console.Write(prefix == null ? $"[unknown]" : $"[{prefix}]");
        Console.Write($" {now:HH:mm:ss}:");
        
        Console.ResetColor();
        
        if (messageColour.HasValue)
        {
            Console.ForegroundColor = messageColour.Value;
        }
        Console.WriteLine($" {message}");
        Console.ResetColor();
    }

    public static void Log(string message, (string text, ConsoleColor colour)[] colours, string? prefix = null, ConsoleColor? prefixColour = null, ConsoleColor? messageColour = null, bool samePosition = false)
    {
        if(samePosition && !Program.NewLines) Console.SetCursorPosition(0, Console.CursorTop - 1); 
        if (Program.MonochromeOutput)
        {
            prefixColour = null;
            messageColour = null;
            colours = new (string text, ConsoleColor colour)[1];
        }
        DateTime now = DateTime.Now;
        // ReSharper disable once InconsistentNaming
        string _out;
        if (prefixColour.HasValue)
        {
            _out = $" {message}";
            Console.ForegroundColor = prefixColour.Value;
            Console.Write($"{(prefix == null ? $"[unknown]" : $"[{prefix}]")} {now:HH:mm:ss}:");
            Console.ResetColor();
        }
        else
        {
            _out = $"{(prefix == null ? $"[unknown]" : $"[{prefix}]")} {now:HH:mm:ss}: {message}";
        }
        var words = Regex.Split(_out, @"( )");

        foreach (var word in words)
        {
            (string text, ConsoleColor colour) colour = colours.FirstOrDefault(x => word.Contains(x.text));

            if (colour.text != null)
            {
                Console.ForegroundColor = colour.colour;
                Console.Write($"{colour.text}");
                Console.ResetColor();
                continue;
            }
            if (messageColour.HasValue)
            {
                Console.ForegroundColor = messageColour.Value;
            }
            Console.Write($"{word}");
            Console.ResetColor();
        }
        Console.WriteLine();
    }
}