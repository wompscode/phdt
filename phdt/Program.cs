using static phdt.Logging;
using static phdt.FileOperations;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Reflection;

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace phdt;
static class Program
{
    private static readonly string? Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    private static bool _verbose;
    public static bool MonochromeOutput;
    public static bool RawOutput;
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

    static int Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            new Option<string>(["--location", "-l"], "Directory to handle all testing in."),
            new Option<int>(["--size", "-s"], "Total size to test in MB."),
            new Option<int>(["--dummy", "-d"], "Size of dummy file in MB."),
            new Option<bool>(["--revalidate", "-rv"], "Re-validate files in directory specified in location (first file becomes dummy)."),
            new Option<bool>(["--verbose", "-v"], "Verbose mode."),
            new Option<bool>(["--monochrome", "-m"], "Disables colour output."),
            new Option<bool>(["--raw", "-r"], "Raw output.")
        };
        
        rootCommand.Description = "Test drive capacity by copying files to a directory on the drive, and validate these files to a dummy file.";
        
        rootCommand.Handler = CommandHandler.Create<string, int, int, bool, bool, bool, bool>((location, size, dummy, verbose, revalidate, monochrome, raw) =>
        {
            Stopwatch.Start();
            MonochromeOutput = raw || monochrome;
            RawOutput = raw;
            _verbose = verbose;
            Log($"PHoebe's Disk Tester {Version}", "init", ConsoleColor.Blue, ConsoleColor.DarkBlue);

            if (string.IsNullOrEmpty(location))
            {
                Log($"Directory was not specified.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                return;
            }

            if (size == 0)
            {
                Log($"Size to test was not specified.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                return;
            }

            if (dummy == 0)
            {
                Log($"Dummy file size was not specified, finding value that fits into size specified.", "warning", ConsoleColor.Yellow, ConsoleColor.DarkYellow);
                for (int i = 2; i < 128; i++)
                {
                    if ((size % i) == 0)
                    {
                        dummy = i;
                        break;
                    }
                }
                Log($"Dummy file size set to {dummy}.", "warning", ConsoleColor.Yellow, ConsoleColor.DarkYellow);
            }
            
            string path = location;
            if (!Directory.Exists(path))
            {
                Log($"Directory does not exist.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                return;
            }
 
            if (Directory.GetFileSystemEntries(path).Length > 0)
            {
                if (revalidate)
                {
                    if(File.Exists(Path.Combine(path, "file0.phdt")))
                    {
                        Log($"Revalidation started.", "revalidation", ConsoleColor.Green, ConsoleColor.DarkGreen);
                        Validate(path, "file0.phdt", dummy, size / dummy, size);
                        return;
                    } else
                    {
                        Log($"Directory is missing file0.phdt.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                        return;
                    }
                }
                else
                {
                    Log($"Directory is not empty.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                    return;
                }
            }
            
            bool fits = (size % dummy) == 0;
            if (fits)
            {
                if(_verbose) Log($"{dummy} fits into {size} with no remainders. Continuing.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
                StartTest(location, size, dummy);
            }
            else
            {
                Log($"Dummy file size does not fit into size to test with no remainder.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
            }
        });

        return rootCommand.Invoke(args);
    }

    private static void StartTest(string location, int sizeToTest, int dummySize)
    {
        int count = 0;
        bool temp = false;
        string dummy = "";
        int times = sizeToTest / dummySize;
        for (int i = 0; i < times; i++)
        {
            count += dummySize;
            if(_verbose) Log($"{count} reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            if (string.IsNullOrEmpty(dummy))
            {
                if(_verbose) Log($"No dummy file, creating: file{i}.phdt.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
                GenerateDummyFile(Path.Combine(location, $"file{i}.phdt"), dummySize);
                dummy = $"file{i}.phdt";
                continue;
            }
            File.Copy(Path.Combine(location, $"{dummy}"), Path.Combine(location, $"file{i}.phdt"));
            if(_verbose) Log($"Copying {dummy} to file{i}.phdt.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !RawOutput)
                {
                    Console.WriteLine("");
                    temp = true;
                }
                Log($"Copying {dummy} to file{i}.phdt.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }

        temp = false;
        if(count == sizeToTest)
        {
            if(_verbose) Log($"{count} == {sizeToTest}, continuing to validation. (took {ElapsedTime(Stopwatch.Elapsed)})", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !RawOutput)
                {
                    Console.WriteLine("");
                }
                Log($"{count} matches {sizeToTest}, continuing to validation. (took {ElapsedTime(Stopwatch.Elapsed)})", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);

            }
            Validate(location, dummy, dummySize, times, sizeToTest);
        }
        else
        {
            Log($"{count} and {sizeToTest} do not match, something went wrong..", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
        }
    }
    static List<Thread> _threads = new List<Thread>();
    public static Task<bool> Test()
    {
        
        return Task.FromResult(true);
    }
    private static async void Validate(string location, string dummy, int dummySize, int times, int sizeToTest)
    {
        if(_verbose) Log($"Validation reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);

        int count = 0;
        bool temp = false;
        for (int i = 0; i < times; i++)
        {
            count += dummySize;
            if(_verbose) Log($"{count} reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            if ($"file{i}.phdt" == dummy)
            {
                if(_verbose) Log($"File is dummy, continuing.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
                continue;
            }
            bool outcome = await Compare(Path.Combine(location, dummy), Path.Combine(location, $"file{i}.phdt"));
            if(_verbose) Log($"Comparing file{i}.phdt to {dummy}.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            if (outcome == false)
            {
                Log($"File file{i}.phdt does not match the dummy file: failed at {count} bytes.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                break;
            }
            if(_verbose) Log($"file{i}.phdt and {dummy} match, continuing.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !RawOutput)
                {
                    Console.WriteLine("");
                    temp = true;
                }
                Log($"file{i}.phdt and {dummy} match, continuing.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }
        if (count == sizeToTest)
        {
            Log($"{count} megabytes counted, {sizeToTest} megabytes needed. (took {ElapsedTime(Stopwatch.Elapsed)})", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta);
        }
        else
        {
            Log($"{count} megabytes counted, {sizeToTest} megabytes needed: mismatch?", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
        }

        Stopwatch.Stop();
        
        Log($"Completed in {ElapsedTime(Stopwatch.Elapsed)}.", "finish", ConsoleColor.Green, ConsoleColor.DarkGreen);
        Log($"You can clear the files in {location} if you want to, or keep them to re-validate (phdt -l {location} -s {sizeToTest} -d {dummySize} -rv).", "finish", ConsoleColor.Green, ConsoleColor.DarkGreen);
    }

    private static string ElapsedTime(TimeSpan ts) {
        return String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
    }
}