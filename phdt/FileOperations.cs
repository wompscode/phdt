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

    public struct DummyFile
    {
        public string FileName { get; set; }
        public long Length { get; set; }
        public byte[] Data { get; set; }
        public bool IsSet { get; set; }
    }

    public static DummyFile Dummy;

    public static void SetDummyFile(string file, string location)
    {
        if (Dummy.IsSet) return;
        Dummy.FileName = file;
        var f1Stream = new FileStream(Path.Combine(location,file), FileMode.Open);
        Dummy.Length = f1Stream.Length;
        Dummy.Data = new byte[f1Stream.Length];
        var _ = f1Stream.Read(Dummy.Data, 0, Dummy.Data.Length);
        f1Stream.Close();
        Dummy.IsSet = true;
    }
    public static Task<bool> DummyCompare(DummyFile dummy, string fileOne)
    {
        if (dummy.FileName == fileOne) return Task.FromResult(true);

        var f1Stream = new FileStream(fileOne, FileMode.Open);
        byte[] f1Bytes = new byte[f1Stream.Length];
        
        if(f1Stream.Length != dummy.Length)
        {
            f1Stream.Close();
            return Task.FromResult(false);
        }

        var _ = f1Stream.Read(f1Bytes, 0, f1Bytes.Length);
        bool ot = dummy.Data.SequenceEqual(f1Bytes);
        
        return Task.FromResult(ot);
    }
    
    // https://learn.microsoft.com/en-us/troubleshoot/developer/visualstudio/csharp/language-compilers/create-file-compare
    // rewritten to just move stuff around a bit
    public static Task<bool> Compare(string fileOne, string fileTwo)
    {
        if (fileOne == fileTwo) return Task.FromResult(true);

        int f1Byte;
        int f2Byte;
        var f1Stream = new FileStream(fileOne, FileMode.Open);
        var f2Stream = new FileStream(fileTwo, FileMode.Open);
        
        if(f1Stream.Length != f2Stream.Length)
        {
            f1Stream.Close();
            f2Stream.Close();
            return Task.FromResult(false);
        }

        do
        {
            f1Byte = f1Stream.ReadByte();
            f2Byte = f2Stream.ReadByte();
        } while ((f1Byte == f2Byte) && (f1Byte != -1));
        
        f1Stream.Close();
        f2Stream.Close();
        
        return Task.FromResult((f1Byte - f2Byte) == 0);
    }
}