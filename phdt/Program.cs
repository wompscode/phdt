using static phdt.Logging;
using static phdt.FileOperations;
using static phdt.Structs;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Drawing;
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

    private static bool Verbose;
    public static bool MonochromeOutput;

    private static SemaphoreSlim? _semaphore;
    static readonly List<Task<CompareResult>> ValidateTasks = new ();
    private static int _validateCount;
    private static bool _validateFailed;
    static readonly List<Task<CreateResult>> CreateTasks = new ();
    private static int _createCount;
    private static int _semaphoreCount;

    private static readonly ConsoleColourScheme _init = new()
    {
        Prefix = Color.DarkSlateBlue,
        Message = Color.SlateBlue
    };
    private static readonly ConsoleColourScheme _warning = new()
    {
        Prefix = Color.DarkOrange,
        Message = Color.Orange
    };
    private static readonly ConsoleColourScheme _status = new()
    {
        Prefix = Color.DarkViolet,
        Message = Color.BlueViolet
    };
    private static readonly ConsoleColourScheme _fatal = new()
    {
        Prefix = Color.DarkRed,
        Message = Color.Red
    };
    private static readonly ConsoleColourScheme _verbose = new()
    {
        Prefix = Color.DarkCyan,
        Message = Color.Cyan
    };
    private static readonly ConsoleColourScheme _finish = new()
    {
        Prefix = Color.DarkGreen,
        Message = Color.Green
    };
    
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
            new Option<int>(["--maxtasks", "-t"], "Maximum concurrent tasks. (DO NOT SET TOO HIGH)")
        };
        
        rootCommand.Description = "Test drive capacity by copying files to a directory on the drive, and validate these files to a dummy file.";
        
        rootCommand.Handler = CommandHandler.Create<string, int, int, bool, bool, bool, int>((location, size, dummy, verbose, revalidate, monochrome, maxtasks) =>
        {
            TotalStopwatch.Start();
            Verbose = verbose;
            MonochromeOutput = monochrome;
            Log($"Phoebe's Disk Tester {Version}", "init", _init);

            if (maxtasks == 0)
            {
                Log($"Semaphore count was not specified, setting to 1. This may be slow.", "warning", _warning);
                maxtasks = 1;
            }
            _semaphore = new SemaphoreSlim(0, maxtasks);
            _semaphoreCount = maxtasks;

            if (Debug)
            {
                location = "test";
                size = 512;
                dummy = 4;
            }
            
            if (string.IsNullOrEmpty(location))
            {
                Log($"Directory was not specified.", "fatal", _fatal);
                return;
            }

            if (size == 0)
            {
                Log($"Size to test was not specified.", "fatal", _fatal);
                return;
            }

            if (dummy == 0)
            {
                dummy = 1;
                Log($"Dummy file size not specified, set to 1. This will be dramatically slower at larger speeds. Consider setting a value (eg. 16, 32).", "warning", _warning);
            }

            if (dummy > 64)
            {
                Log($"Dummy file size above 64mb, forcing to 64mb.", "warning", _warning);
                dummy = 64;
            }
            
            string path = location;
            if (!Directory.Exists(path))
            {
                Log($"Directory does not exist.", "fatal", _fatal);
                return;
            }
 
            if(Verbose) Log($"location: {path}, dummy: {dummy}, size: {size}, verbose: {verbose}, revalidate: {revalidate}, maxtasks: {maxtasks}", "verbose", _verbose);
            
            if (Directory.GetFileSystemEntries(path).Length > 0)
            {
                if (revalidate)
                {
                    if(File.Exists(Path.Combine(path, "file0.phdt")))
                    {
                        Log($"Revalidation started.", "revalidation", _status);
                        Validate(path, "file0.phdt", dummy, size / dummy, size);
                        return;
                    } else
                    {
                        Log($"Directory is missing file0.phdt.", "fatal", _fatal);
                        return;
                    }
                }
                else
                {
                    Log($"Directory is not empty.", "fatal", _fatal);
                    return;
                }
            }
            
            bool fits = size % dummy == 0;
            if (fits)
            {
                if(Verbose) Log($"{dummy} fits into {size} with no remainders. Continuing.", "verbose", _verbose);
                StartTest(location, size, dummy);
            }
            else
            {
                Log($"Dummy file size does not fit into size to test with no remainder, setting dummy size to 2.", "warning", _warning);
                StartTest(location, size, 2);
            }
        });

        return rootCommand.Invoke(args);
    }

    private static async Task<CreateResult> CreateTask(string location, string file, int count, int times)
    {
        try
        {
            if(_semaphore == null) return new CreateResult()
            {
                Success = true,
                File = file
            };
            await _semaphore.WaitAsync();
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
                Log($"Created {file} ({count}/{times}).", "status", _status);
                await File.WriteAllBytesAsync(Path.Combine(location, file), Dummy.Data);
                return new CreateResult()
                {
                    Success = true,
                    File = file
                };
            }
            catch (Exception exception)
            {
                Log($"{exception.Message}", "fatal", _fatal);
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
            if(Verbose) Log($"file creation task {count} released from Semaphore.", "verbose", _verbose);
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
            if(Verbose) Log($"Incrementing count by {dummySize}, now {count}.", "verbose", _verbose);
            if (Dummy.IsSet == false)
            {
                if(Verbose) Log($"No dummy file, creating: file{i}.phdt.", "verbose", _verbose);
                GenerateDummyFile(Path.Combine(location, $"file{i}.phdt"), dummySize);
                dummy = $"file{i}.phdt";
                SetDummyFile(dummy, location);
                _createCount += dummySize;
                continue;
            }

            string __ = $"file{i}.phdt";
            int _i = i;
            CreateTasks.Add(Task.Run(async () => await CreateTask(location, __, _i, times)));
            if(Verbose) Log($"Queued {__} to be created.", "verbose", _verbose);
        }

        _semaphore?.Release(_semaphoreCount);
        // wait for all CreateTasks to be done
        Task.WaitAll(CreateTasks.ToArray<Task>());
        
        foreach (var item in CreateTasks)
        {
            CreateResult result = await item;

            if (result.Success == false) break;

            _createCount += dummySize;
        }

        CreateStopwatch.Stop();
        if(_createCount == sizeToTest)
        {
            Log($"{_createCount} matches {sizeToTest}, continuing to validation. (took {ElapsedTime(CreateStopwatch.Elapsed)}: (~)write: {Math.Round(sizeToTest / CreateStopwatch.Elapsed.TotalSeconds)}MB/s)", "status", _status);
            Validate(location, dummy, dummySize, times, sizeToTest);
        }
        else
        {
            Log($"{_createCount} and {sizeToTest} do not match, something went wrong..", "fatal", _fatal);
        }
    }

    private static async Task<CompareResult> CompareTask(int count, int i, string fileTwo)
    {
        try
        {
            if (_semaphore == null)
                return new CompareResult()
                {
                    Count = count, HasFailedCompare = false, Iteration = i, FileName = fileTwo
                };
            await _semaphore.WaitAsync();
            bool outcome = false;
            try
            {
                Log($"Validating {fileTwo} against dummy in memory..", "status", _status);
                if (!_validateFailed)
                {
                    outcome = await DummyCompare(Dummy, fileTwo);
                    if (outcome == false)
                    {
                        _validateFailed = true;
                        Log($"File {fileTwo} does not match the dummy in memory: failed at {count} megabytes.", "fatal", _fatal);
                    }
                    else
                    {
                        Log($"File {fileTwo} matched the dummy in memory, continuing..", "status", _status);

                    }
                }
            }
            catch (Exception exc)
            {
                Log($"{exc.Message}", "fatal", _fatal);
            }
            return new CompareResult { Count = count, HasFailedCompare = outcome, Iteration = i, FileName = fileTwo};
        }
        finally
        {
            _semaphore?.Release();
            if(Verbose) Log($"compare task {count} released from Semaphore.", "verbose", _verbose);
        }
    }

    private static async void Validate(string location, string dummy, int dummySize, int times, int sizeToTest)
    {
        _semaphore = new SemaphoreSlim(0, _semaphoreCount);
        int count = 0;
        
        if(Verbose) Log($"Validation reached.", "verbose", _verbose);

        CompareStopwatch.Start();
        for (int i = 0; i < times; i++)
        {
            count += dummySize;
            if(Verbose) Log($"Incremented count by {dummySize}, now {count}.", "verbose", _verbose);
            if(!Dummy.IsSet) SetDummyFile(dummy, location);
            if ($"file{i}.phdt" == Dummy.FileName)
            {
                if(Verbose) Log($"File is dummy, continuing.", "verbose", _verbose);

                bool inMemory = await DummyCompare(Dummy, Path.Combine(location, dummy));
                if (inMemory == false)
                {
                    Log("Dummy file on disk does not match dummy file stored in memory.", "fatal", _fatal);
                    break;
                }
                
                _validateCount += dummySize;
                continue;
            }

            var __ = count;
            var _ = i;
            ValidateTasks.Add(Task.Run(async () => await CompareTask(__, _, Path.Combine(location, $"file{_}.phdt"))));
            if(Verbose) Log($"Queued file{_}.phdt to be compared.", "verbose", _verbose);
        }

        _semaphore.Release(_semaphoreCount);
        // wait until every task in ValidateTasks is done
        Task.WaitAll(ValidateTasks.ToArray<Task>());
        
        foreach (Task<CompareResult> item in ValidateTasks)
        {
            CompareResult compareResult = await item;
            bool outcome = compareResult.HasFailedCompare;
            
            if (outcome == false)
            {
                break;
            }
            _validateCount += dummySize;
            
            if(Verbose) Log($"Incrementing _validateCount by {dummySize}, now {_validateCount}.", "verbose", _verbose);
        }

        CompareStopwatch.Stop();
        if (_validateCount == sizeToTest)
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed. (took {ElapsedTime(CompareStopwatch.Elapsed)}, (~)read: {Math.Round(sizeToTest / CompareStopwatch.Elapsed.TotalSeconds)}MB/s)", "status", _status);
        }
        else
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed: mismatch?", "fatal", _fatal);
        }

        TotalStopwatch.Stop();
        
        Log($"Completed in {ElapsedTime(CompareStopwatch.Elapsed + CreateStopwatch.Elapsed)} (full total: {ElapsedTime(TotalStopwatch.Elapsed)}).", "finish", _finish);
        Log($"You can clear the files in \"{location}\" if you want to, or keep them to re-validate (phdt -l {location} -s {sizeToTest} -d {dummySize} -rv).", "finish", _finish);
    }

    private static string ElapsedTime(TimeSpan ts) {
        return String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
    }
}