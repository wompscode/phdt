using static phdt.Logging;
using static phdt.FileOperations;
using static phdt.Structs;
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
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    
    private static readonly bool Debug = false;
    private static readonly string? Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    
    private static bool _verbose;
    public static bool MonochromeOutput;
    public static bool NewLines;
    
    static readonly List<Task<CompareResult>> ValidateTasks = new ();
    private static int _validateCount;
    static readonly List<Task<CreateResult>> CreateTasks = new ();
    private static int _createCount;
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
            new Option<bool>(["--newlines", "-n"], "Always output on newline, never overwrite output.")
        };
        
        rootCommand.Description = "Test drive capacity by copying files to a directory on the drive, and validate these files to a dummy file.";
        
        rootCommand.Handler = CommandHandler.Create<string, int, int, bool, bool, bool, bool>((location, size, dummy, verbose, revalidate, monochrome, newlines) =>
        {
            Stopwatch.Start();
            MonochromeOutput = monochrome;
            NewLines = newlines;
            _verbose = verbose;
            
            Log($"PHoebe's Disk Tester {Version}", "init", ConsoleColor.Blue, ConsoleColor.DarkBlue);

            if (Debug)
            {
                location = "test";
                size = 512;
                dummy = 4;
            }
            
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
            
            bool fits = size % dummy == 0;
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

    private static async Task<CreateResult> CreateTask(string location, string file)
    {
        if (Dummy.Data.Length == 0)
        {
            return new CreateResult()
            {
                Success = false,
                File = ""
            };
        }

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(location, file), Dummy.Data);
            return new CreateResult()
            {
                Success = true,
                File = file
            };
        }
        catch (Exception exception)
        {
            Log($"{exception.Message}", "error", ConsoleColor.Red, ConsoleColor.DarkRed);
            return new CreateResult()
            {
                Success = false,
                File = ""
            };
        }
    }
    private static async void StartTest(string location, int sizeToTest, int dummySize)
    {
        bool temp = false;
        
        string dummy = "";
        
        int count = 0;
        int times = sizeToTest / dummySize;
        
        for (int i = 0; i < times; i++)
        {
            count += dummySize;
            if(_verbose) Log($"{count} reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            if (Dummy.IsSet == false)
            {
                if(_verbose) Log($"No dummy file, creating: file{i}.phdt.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
                GenerateDummyFile(Path.Combine(location, $"file{i}.phdt"), dummySize);
                dummy = $"file{i}.phdt";
                SetDummyFile(dummy, location);
                _createCount += dummySize;
                continue;
            }

            string __ = $"file{i}.phdt";
            CreateTasks.Add(Task.Run(() => CreateTask(location, __)));
            if(_verbose) Log($"Queued {__} to be created.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !NewLines)
                {
                    Console.WriteLine("");
                    temp = true;
                }
                Log($"Queued {__} to be created.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }
    
        // wait for all CreateTasks to be done
        Task.WaitAll(CreateTasks.ToArray<Task>());
        temp = false;
        
        foreach (var item in CreateTasks)
        {
            CreateResult result = await item;

            if (result.Success == false) break;
            if(_verbose) Log($"Created {result.File}.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !NewLines)
                {
                    Console.WriteLine("");
                    temp = true;
                }
                Log($"Created {result.File}.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }

            _createCount += dummySize;
        }
        
        temp = false;
        if(_createCount == sizeToTest)
        {
            if(_verbose) Log($"{_createCount} == {sizeToTest}, continuing to validation. (took {ElapsedTime(Stopwatch.Elapsed)})", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !NewLines)
                {
                    Console.WriteLine("");
                }
                Log($"{_createCount} matches {sizeToTest}, continuing to validation. (took {ElapsedTime(Stopwatch.Elapsed)})", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
            Validate(location, dummy, dummySize, times, sizeToTest);
        }
        else
        {
            Log($"{_createCount} and {sizeToTest} do not match, something went wrong..", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
        }
    }

    private static async Task<CompareResult> CompareTask(int count, int i, string fileTwo)
    {
        bool outcome = false;
        try
        {
            outcome = await DummyCompare(Dummy, fileTwo);
        }
        catch (Exception exc)
        {
            Log($"{exc.Message}", "error", ConsoleColor.Red, ConsoleColor.DarkRed);
        }
        return new CompareResult { Count = count, HasFailedCompare = outcome, Iteration = i, FileName = fileTwo};
    }

    private static async void Validate(string location, string dummy, int dummySize, int times, int sizeToTest)
    {
        bool temp = false;
        int count = 0;
        
        if(_verbose) Log($"Validation reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
        
        for (int i = 0; i < times; i++)
        {
            count += dummySize;
            if(_verbose) Log($"{count} reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            if(!Dummy.IsSet) SetDummyFile(dummy, location);
            if ($"file{i}.phdt" == Dummy.FileName)
            {
                if(_verbose) Log($"File is dummy, continuing.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);

                bool inMemory = await DummyCompare(Dummy, Path.Combine(location, dummy));
                if (inMemory == false)
                {
                    Log("Dummy file on disk does not match dummy file stored in memory.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                    break;
                }
                
                _validateCount += dummySize;
                continue;
            }

            var __ = count;
            var _ = i;
            ValidateTasks.Add(Task.Run(() => CompareTask(__, _, Path.Combine(location, $"file{_}.phdt"))));
            if(_verbose) Log($"Queued file{_}.phdt to be compared.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !NewLines)
                {
                    Console.WriteLine("");
                    temp = true;
                }
                Log($"Queued file{_}.phdt to be compared.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }
        
        // wait until every task in ValidateTasks is done
        Task.WaitAll(ValidateTasks.ToArray<Task>());
        temp = false;
        
        foreach (Task<CompareResult> item in ValidateTasks)
        {
            CompareResult compareResult = await item;
            if(_verbose) Log($"Comparing file{compareResult.Iteration}.phdt to {dummy}.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);

            bool outcome = compareResult.HasFailedCompare;
            
            if (outcome == false)
            {
                Log($"File {compareResult.FileName} does not match the dummy file: failed at {compareResult.Count} bytes.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                break;
            }
            _validateCount += dummySize;
            
            if(_verbose) Log($"{compareResult.FileName} and {dummy} match, continuing.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                if (temp == false && !NewLines)
                {
                    Console.WriteLine("");
                    temp = true;
                }
                Log($"{compareResult.FileName} and {dummy} match, continuing.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }
        if (_validateCount == sizeToTest)
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed. (took {ElapsedTime(Stopwatch.Elapsed)})", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta);
        }
        else
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed: mismatch?", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
        }

        Stopwatch.Stop();
        
        Log($"Completed in {ElapsedTime(Stopwatch.Elapsed)}.", "finish", ConsoleColor.Green, ConsoleColor.DarkGreen);
        Log($"You can clear the files in \"{location}\" if you want to, or keep them to re-validate (phdt -l {location} -s {sizeToTest} -d {dummySize} -rv).", "finish", ConsoleColor.Green, ConsoleColor.DarkGreen);
    }

    private static string ElapsedTime(TimeSpan ts) {
        return String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
    }
}