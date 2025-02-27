namespace phdt;

public static class Structs
{
    public struct Result
    {
        public bool HasFailedCompare { get; init; }
        public int Count { get; init; }
        public int Iteration { get; init; }
    }
}