// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
namespace phdt;
public static class FileOperations
{
    public static void GenerateDummyFile(string location, long size)
    {
        // https://stackoverflow.com/a/4432207
        const int blockSize = 1024 * 8; // 8k block size
        const int blocksPerMb = (1024 * 1024) / blockSize;
        byte[] data = new byte[blockSize];
        Random rng = new Random();
        using FileStream stream = File.OpenWrite(location);
        for (int i = 0; i < size * blocksPerMb; i++)
        {
            rng.NextBytes(data);
            stream.Write(data, 0, data.Length);
        }
    }

    // https://learn.microsoft.com/en-us/troubleshoot/developer/visualstudio/csharp/language-compilers/create-file-compare
    // rewritten to just move stuff around a bit
    public static bool Compare(string fileOne, string fileTwo)
    {
        if (fileOne == fileTwo) return true;

        int f1Byte;
        int f2Byte;
        var f1Stream = new FileStream(fileOne, FileMode.Open);
        var f2Stream = new FileStream(fileTwo, FileMode.Open);
        
        if(f1Stream.Length != f2Stream.Length)
        {
            f1Stream.Close();
            f2Stream.Close();
            return false;
        }

        do
        {
            f1Byte = f1Stream.ReadByte();
            f2Byte = f2Stream.ReadByte();
        } while ((f1Byte == f2Byte) && (f1Byte != -1));
        
        f1Stream.Close();
        f2Stream.Close();
        
        return ((f1Byte - f2Byte) == 0);
    }
}