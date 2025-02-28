using System.Drawing;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace phdt;
public static class Structs
{
    public struct ConsoleColourScheme
    {
        public ConsoleColourSet Init { get; set; }
        public ConsoleColourSet Warning { get; set; }
        public ConsoleColourSet Status { get; set; }
        public ConsoleColourSet Fatal { get; set; }
        public ConsoleColourSet Verbose { get; set; }
        public ConsoleColourSet Finish { get; set; }
    }
    // I'll come back to this.
    public struct ColourSchemeFile
    {
        public Color InitPrefix { get; set; }
        public Color InitMessage { get; set; }
        public Color WarningPrefix { get; set; }
        public Color WarningMessage { get; set; }
        public Color StatusPrefix { get; set; }
        public Color StatusMessage { get; set; }
        public Color FatalPrefix { get; set; }
        public Color FatalMessage { get; set; }
        public Color VerbosePrefix { get; set; }
        public Color VerboseMessage { get; set; }
        public Color FinishPrefix { get; set; }
        public Color FinishMessage { get; set; }
    }
    public struct ConsoleColourSet
    {
        public Color Prefix { get; init; }
        public Color Message { get; init; }
    }
    public struct CompareResult
    {
        public bool HasFailedCompare { get; init; }
        public int Count { get; init; }
        public int Iteration { get; init; }
        public string FileName { get; init; }
    }

    public struct CreateResult
    {
        public string File { get; init; }
        public bool Success { get; init; }
    }

    public struct RevalidationParameters
    {
        public int DummySize { get; init; }
        public int Size { get; init; }
    }
}