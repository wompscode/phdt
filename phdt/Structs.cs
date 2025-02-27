namespace phdt;

public static class Structs
{
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
}