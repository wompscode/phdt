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
    private static readonly Stopwatch TotalStopwatch = new ();
    private static readonly Stopwatch CreateStopwatch = new ();
    private static readonly Stopwatch CompareStopwatch = new ();
     
    private static readonly bool Debug = false;
    private static readonly string? Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    
    private static bool _verbose;
    public static bool MonochromeOutput;
    private static bool NewLines;

    private static SemaphoreSlim? _semaphore;
    static readonly List<Task<CompareResult>> ValidateTasks = new ();
    private static int _validateCount;
    private static bool _validateFailed;
    static readonly List<Task<CreateResult>> CreateTasks = new ();
    private static int _createCount;
    private static int _semaphoreCount;
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            new Option<string>(["--location", "-l"], "Directory to handle all testing in."),
            new Option<int>(["--size", "-s"], "Total size to test in MB."),
            new Option<int>(["--dummy", "-d"], "Size of dummy file in MB (max 32mb)."),
            new Option<bool>(["--revalidate", "-rv"], "Re-validate files in directory specified in location (first file becomes dummy)."),
            new Option<bool>(["--verbose", "-v"], "Verbose mode."),
            new Option<bool>(["--monochrome", "-m"], "Disables colour output."),
            new Option<bool>(["--newlines", "-n"], "Always output on newline, never overwrite output."),
            new Option<int>(["--semaphores", "-t"], "Maximum concurrent tasks. (DO NOT SET TOO HIGH)")
        };
        
        rootCommand.Description = "Test drive capacity by copying files to a directory on the drive, and validate these files to a dummy file.";
        
        rootCommand.Handler = CommandHandler.Create<string, int, int, bool, bool, bool, bool, int>((location, size, dummy, verbose, revalidate, monochrome, newlines, semaphores) =>
        {
            TotalStopwatch.Start();
            MonochromeOutput = monochrome;
            NewLines = newlines;
            _verbose = verbose;
            Log($"Phoebe's Disk Tester {Version}", "init", ConsoleColor.Blue, ConsoleColor.DarkBlue);

            if (semaphores == 0)
            {
                Log($"Semaphore count was not specified, setting to 1. This may be slow.", "warning", ConsoleColor.Yellow, ConsoleColor.DarkYellow);
                semaphores = 1;
            }
            _semaphore = new SemaphoreSlim(0, semaphores);
            _semaphoreCount = semaphores;

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
                for (int i = 2; i < 32; i++)
                {
                    if ((size % i) == 0)
                    {
                        dummy = i;
                        break;
                    }
                }
                Log($"Dummy file size set to {dummy}.", "warning", ConsoleColor.Yellow, ConsoleColor.DarkYellow);
            }

            if (dummy > 32)
            {
                Log($"Dummy file size above 32mb, forcing to 32mb.", "warning", ConsoleColor.Yellow, ConsoleColor.DarkYellow);
                dummy = 32;
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
        try
        {
            await _semaphore?.WaitAsync();
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
        finally
        {
            _semaphore?.Release();
        }
    }
    private static async void StartTest(string location, int sizeToTest, int dummySize)
    {
        string dummy = "";
        
        int count = 0;
        int times = sizeToTest / dummySize;
        CreateStopwatch.Start();
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
            CreateTasks.Add(Task.Run(async () => await CreateTask(location, __)));
            if(_verbose) Log($"Queued {__} to be created.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                Log($"Queued {__} to be created.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }

        _semaphore?.Release(_semaphoreCount);
        // wait for all CreateTasks to be done
        Task.WaitAll(CreateTasks.ToArray<Task>());
        
        foreach (var item in CreateTasks)
        {
            CreateResult result = await item;

            if (result.Success == false) break;
            if(_verbose) Log($"Created {result.File}.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                Log($"Created {result.File}.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }

            _createCount += dummySize;
        }

        CreateStopwatch.Stop();
        if(_createCount == sizeToTest)
        {
            if(_verbose) Log($"{_createCount} == {sizeToTest}, continuing to validation. (took {ElapsedTime(CreateStopwatch.Elapsed)}, (~)write: {Math.Round(sizeToTest / CreateStopwatch.Elapsed.TotalSeconds)}MB/s)", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                Log($"{_createCount} matches {sizeToTest}, continuing to validation. (took {ElapsedTime(CreateStopwatch.Elapsed)}, (~)write: {Math.Round(sizeToTest / CreateStopwatch.Elapsed.TotalSeconds)}MB/s)", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
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
        try
        {
            await _semaphore?.WaitAsync();
            bool outcome = false;
            try
            {
                if (!_validateFailed)
                {
                    outcome = await DummyCompare(Dummy, fileTwo);
                    if (outcome == false) _validateFailed = true;
                }
            }
            catch (Exception exc)
            {
                Log($"{exc.Message}", "error", ConsoleColor.Red, ConsoleColor.DarkRed);
            }
            return new CompareResult { Count = count, HasFailedCompare = outcome, Iteration = i, FileName = fileTwo};
        }
        finally
        {
            _semaphore?.Release();
        }
    }

    private static async void Validate(string location, string dummy, int dummySize, int times, int sizeToTest)
    {
        _semaphore = new SemaphoreSlim(0, _semaphoreCount);
        int count = 0;
        
        if(_verbose) Log($"Validation reached.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);

        CompareStopwatch.Start();
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
            ValidateTasks.Add(Task.Run(async () => await CompareTask(__, _, Path.Combine(location, $"file{_}.phdt"))));
            if(_verbose) Log($"Queued file{_}.phdt to be compared.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                Log($"Queued file{_}.phdt to be compared.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }

        _semaphore?.Release(_semaphoreCount);
        // wait until every task in ValidateTasks is done
        Task.WaitAll(ValidateTasks.ToArray<Task>());
        
        foreach (Task<CompareResult> item in ValidateTasks)
        {
            CompareResult compareResult = await item;
            if(_verbose) Log($"Comparing file{compareResult.Iteration}.phdt to dummy.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);

            bool outcome = compareResult.HasFailedCompare;
            
            if (outcome == false)
            {
                Log($"File {compareResult.FileName} does not match the dummy file: failed at {compareResult.Count} megabytes.", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
                break;
            }
            _validateCount += dummySize;
            
            if(_verbose) Log($"{compareResult.FileName} and dummy match, continuing.", "verbose", ConsoleColor.Cyan, ConsoleColor.DarkCyan);
            else
            {
                Log($"{compareResult.FileName} and dummy match, continuing.", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta, true);
            }
        }

        CompareStopwatch.Stop();
        if (_validateCount == sizeToTest)
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed. (took {ElapsedTime(CompareStopwatch.Elapsed)}, (~)read: {Math.Round(sizeToTest / CompareStopwatch.Elapsed.TotalSeconds)}MB/s)", "status", ConsoleColor.Magenta, ConsoleColor.DarkMagenta);
        }
        else
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed: mismatch?", "fatal", ConsoleColor.Red, ConsoleColor.DarkRed);
        }

        TotalStopwatch.Stop();
        
        Log($"Completed in {ElapsedTime(CompareStopwatch.Elapsed + CreateStopwatch.Elapsed)} (full total: {ElapsedTime(TotalStopwatch.Elapsed)}).", "finish", ConsoleColor.Green, ConsoleColor.DarkGreen);
        Log($"You can clear the files in \"{location}\" if you want to, or keep them to re-validate (phdt -l {location} -s {sizeToTest} -d {dummySize} -rv).", "finish", ConsoleColor.Green, ConsoleColor.DarkGreen);
    }

    private static string ElapsedTime(TimeSpan ts) {
        return String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
    }
}