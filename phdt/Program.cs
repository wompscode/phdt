using static phdt.Logging;
using static phdt.FileOperations;
using static phdt.Structs;

using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text.Json;

// I want my variables to be named how I want rider. fuck off
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable MethodHasAsyncOverload

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
    private static int _semaphoreCount;
    
    private static SemaphoreSlim? _semaphore;
    static readonly List<Task<CompareResult>> ValidateTasks = new ();
    private static int _validateCount;
    private static bool _validateFailed;
    static readonly List<Task<CreateResult>> CreateTasks = new ();
    private static int _createCount;
    private static bool _createFailed;
    
    private static readonly ConsoleColourScheme ColourScheme = new()
    {
        Init = new()
        {
            Prefix = Color.DarkSlateBlue,
            Message = Color.SlateBlue
        },
        Warning = new()
        {
            Prefix = Color.DarkOrange,
            Message = Color.Orange
        },
        Status = new()
        {
            Prefix = Color.DarkViolet,
            Message = Color.BlueViolet
        },
        Fatal = new()
        {
            Prefix = Color.DarkRed,
            Message = Color.Red
        },
        Verbose = new()
        {
            Prefix = Color.DarkCyan,
            Message = Color.Cyan
        },
        Finish = new()
        {
            Prefix = Color.DarkGreen,
            Message = Color.Green
        }
    };
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            new Option<string>(["--location", "-l"], "Directory to handle all testing in."),
            new Option<int>(["--size", "-s"], "Total size to test in MB."),
            new Option<int>(["--dummy", "-d"], "Size of dummy file in MB (max 32mb)."),
            new Option<bool>(["--revalidate", "-rv"], "Re-validate files in directory specified in location (first file becomes dummy). You need to set the test size and dummy file size the same as the inital test was run."),
            new Option<bool>(["--verbose", "-v"], "Verbose mode."),
            new Option<bool>(["--monochrome", "-m"], "Disables colour output."),
            new Option<int>(["--maxtasks", "-t"], "Maximum concurrent tasks. (DO NOT SET TOO HIGH)"),
        };
        
        rootCommand.Description = "Test drive capacity by copying files to a directory on the drive, and validate these files to a dummy file.";
        
        rootCommand.Handler = CommandHandler.Create<string, int, int, bool, bool, bool, int>((location, size, dummy, verbose, revalidate, monochrome, maxtasks) =>
        {
            TotalStopwatch.Start();
            Verbose = verbose;
            MonochromeOutput = monochrome;

            Log($"Phoebe's Disk Tester {Version}", "init", ColourScheme.Init);

            if (maxtasks == 0)
            {
                Log($"Semaphore max task count was not specified, setting to 1. This may be slow.", "warning", ColourScheme.Warning);
                maxtasks = 1;
            } else if (maxtasks >= 128)
            {
                Log($"Semaphore max task count above 128. This can cause slowdown at large test sizes/dummy file sizes.", "warning", ColourScheme.Warning);
                Log($"Press Q to stop, otherwise press any key to continue.", "warning", ColourScheme.Warning);
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Q)
                {
                    return;
                }
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
                Log($"Directory was not specified.", "fatal", ColourScheme.Fatal);
                return;
            }

            if (size == 0 && revalidate == false)
            {
                Log($"Size to test was not specified.", "fatal", ColourScheme.Fatal);
                return;
            }

            if (dummy == 0 && revalidate == false)
            {
                dummy = 1;
                Log($"Dummy file size not specified, set to 1. This will be dramatically slower at larger speeds. Consider setting a value (eg. 16, 32).", "warning", ColourScheme.Warning);
            }


            
            string path = location;
            if (!Directory.Exists(path))
            {
                Log($"Directory does not exist.", "fatal", ColourScheme.Fatal);
                return;
            }
 
            if(Verbose) Log($"location: {path}, dummy: {dummy}, size: {size}, verbose: {verbose}, revalidate: {revalidate}, maxtasks: {maxtasks}", "verbose", ColourScheme.Verbose);
            
            if (Directory.GetFileSystemEntries(path).Length > 0)
            {
                if (revalidate)
                {
                    if(File.Exists(Path.Combine(path, "revalidation.json")) || size > 0 && dummy > 0)
                    {
                        if (File.Exists(Path.Combine(path, "file0.phdt")))
                        {
                            Log($"Revalidation started.", "revalidation", ColourScheme.Status);
                            RevalidationParameters parameters = JsonSerializer.Deserialize<RevalidationParameters>(File.ReadAllText(Path.Combine(path, "revalidation.json")));
                            if (size == 0)
                            {
                                if (parameters.Size == 0)
                                {
                                    Log($"Size to test was not specified (revalidation.json value was 0, and size was 0).", "fatal", ColourScheme.Fatal);
                                    return;
                                }
                                size = parameters.Size;
                            }
                            if (dummy == 0)
                            {
                                if (parameters.DummySize == 0)
                                {
                                    Log($"Dummy file size not specified (revalidation.json value was 0, and dummy was 0).", "fatal", ColourScheme.Fatal);
                                    return;
                                }
                                dummy = parameters.DummySize;
                            }
                        }
                        else
                        {
                            Log($"Directory is missing file0.phdt.", "fatal", ColourScheme.Fatal);
                            return;
                        }
                    } else
                    {
                        Log($"Directory is missing revalidation.json (and no size and dummy size values were specified).", "fatal", ColourScheme.Fatal);
                        return;
                    }
                }
                else
                {
                    Log($"Directory is not empty.", "fatal", ColourScheme.Fatal);
                    return;
                }
            }
            else
            {
                if (revalidate)
                {
                    Log($"Directory is empty.", "fatal", ColourScheme.Fatal);
                    return;
                }
            }
            
            if (dummy > 64)
            {
                Log($"Dummy file size above 64mb, this can cause problems with large max task counts and test sizes.", "warning", ColourScheme.Warning);
                Log($"Press Y to continue with value above 64mb, or press any key to set to 64mb.", "warning", ColourScheme.Warning);
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if (keyInfo.Key != ConsoleKey.Y)
                {
                    dummy = 64;
                }
            }
            bool fits = size % dummy == 0;
            if (fits)
            {
                if(Verbose) Log($"{dummy} fits into {size} with no remainders. Continuing.", "verbose", ColourScheme.Verbose);
                if (revalidate)
                {
                    Validate(path, "file0.phdt", dummy, size / dummy, size);
                    return;
                }
                StartTest(location, size, dummy);
            }
            else
            {
                Log($"Dummy file size does not fit into size to test with no remainder, setting dummy size to 2.", "warning", ColourScheme.Warning);
                if (revalidate)
                {
                    Validate(path, "file0.phdt", dummy, size / dummy, size);
                    return;
                }
                StartTest(location, size, 2);
            }
        });

        return rootCommand.Invoke(args);
    }

    private static async Task<CreateResult> CreateTask(string location, string file, int count, int times, int dummySize)
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
                if (_createFailed) return new CreateResult()
                {
                    Success = false,
                    File = ""
                };
                Log($"Created {file} ({count}/{times}).", "status", ColourScheme.Status);
                await File.WriteAllBytesAsync(Path.Combine(location, file), Dummy.Data);
                _createCount += dummySize;
                if(Verbose) Log($"{count}: Incrementing _createCount by {dummySize}, now {_validateCount}.", "verbose", ColourScheme.Verbose);
                return new CreateResult()
                {
                    Success = true,
                    File = file
                };
            }
            catch (Exception exception)
            {
                Log($"{exception.Message}", "fatal", ColourScheme.Fatal);
                _createFailed = true;
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
            if(Verbose) Log($"file creation task {count} released from Semaphore.", "verbose", ColourScheme.Verbose);
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
            if(Verbose) Log($"Incrementing count by {dummySize}, now {count}.", "verbose", ColourScheme.Verbose);
            if (Dummy.IsSet == false)
            {
                if(Verbose) Log($"No dummy file, creating: file{i}.phdt.", "verbose", ColourScheme.Verbose);
                GenerateDummyFile(Path.Combine(location, $"file{i}.phdt"), dummySize);
                dummy = $"file{i}.phdt";
                SetDummyFile(dummy, location);
                _createCount += dummySize;
                continue;
            }

            string __ = $"file{i}.phdt";
            int _i = i;
            CreateTasks.Add(Task.Run(async () => await CreateTask(location, __, _i, times, dummySize)));
            if(Verbose) Log($"Queued {__} to be created.", "verbose", ColourScheme.Verbose);
        }

        _semaphore?.Release(_semaphoreCount);
        // wait for all CreateTasks to be done
        Task.WaitAll(CreateTasks.ToArray<Task>());
        
        foreach (var item in CreateTasks)
        {
            CreateResult result = await item;

            if (result.Success == false) break;
        }

        CreateStopwatch.Stop();
        if(_createCount == sizeToTest)
        {
            Log($"{_createCount} matches {sizeToTest}, continuing to validation. (took {ElapsedTime(CreateStopwatch.Elapsed)}: (~)write: {Math.Round(sizeToTest / CreateStopwatch.Elapsed.TotalSeconds)}MB/s)", "status", ColourScheme.Status);
            Validate(location, dummy, dummySize, times, sizeToTest);
        }
        else
        {
            Log($"{_createCount} and {sizeToTest} do not match, something went wrong..", "fatal", ColourScheme.Fatal);
        }
    }

    private static async Task<CompareResult> CompareTask(int count, int i, string fileTwo, int dummySize)
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
                if (!_validateFailed)
                {
                    Log($"{i}: Validating {fileTwo} against dummy in memory..", "status", ColourScheme.Status);
                    if (!File.Exists(fileTwo))
                    {
                        _validateFailed = true;
                        Log($"{i}: File {fileTwo} missing!", "fatal", ColourScheme.Fatal);

                        return new CompareResult()
                        {
                            Count = count, HasFailedCompare = false, Iteration = i, FileName = fileTwo
                        };
                    }
                    outcome = await DummyCompare(Dummy, fileTwo);
                    if (outcome == false)
                    {
                        _validateFailed = true;
                        Log($"{i}: File {fileTwo} does not match the dummy in memory: failed at {count} megabytes.", "fatal", ColourScheme.Fatal);
                    }
                    else
                    {
                        Log($"{i}: File {fileTwo} matched the dummy in memory, continuing..", "status", ColourScheme.Status);
                        _validateCount += dummySize;
            
                        if(Verbose) Log($"{i}: Incrementing _validateCount by {dummySize}, now {_validateCount}.", "verbose", ColourScheme.Verbose);
                    }
                }
            }
            catch (Exception exc)
            {
                Log($"{exc.Message}", "fatal", ColourScheme.Fatal);
            }
            return new CompareResult { Count = count, HasFailedCompare = outcome, Iteration = i, FileName = fileTwo};
        }
        finally
        {
            _semaphore?.Release();
            if(Verbose) Log($"compare task {count} released from Semaphore.", "verbose", ColourScheme.Verbose);
        }
    }

    private static async void Validate(string location, string dummy, int dummySize, int times, int sizeToTest)
    {
        _semaphore = new SemaphoreSlim(0, _semaphoreCount);
        int count = 0;
        
        if(Verbose) Log($"Validation reached.", "verbose", ColourScheme.Verbose);

        CompareStopwatch.Start();
        for (int i = 0; i < times; i++)
        {
            count += dummySize;
            if(Verbose) Log($"Incremented count by {dummySize}, now {count}.", "verbose", ColourScheme.Verbose);
            if(!Dummy.IsSet) SetDummyFile(dummy, location);
            if ($"file{i}.phdt" == Dummy.FileName)
            {
                if(Verbose) Log($"File is dummy, continuing.", "verbose", ColourScheme.Verbose);

                bool inMemory = await DummyCompare(Dummy, Path.Combine(location, dummy));
                if (inMemory == false)
                {
                    Log("Dummy file on disk does not match dummy file stored in memory.", "fatal", ColourScheme.Fatal);
                    break;
                }
                
                _validateCount += dummySize;
                continue;
            }

            var __ = count;
            var _ = i;
            if (!File.Exists(Path.Combine(location, $"file{_}.phdt")))
            {
                _validateFailed = true;
                Log($"{i}: File {Path.Combine(location, $"file{_}.phdt")} missing!", "fatal", ColourScheme.Fatal);
                break;
            }
            ValidateTasks.Add(Task.Run(async () => await CompareTask(__, _, Path.Combine(location, $"file{_}.phdt"), dummySize)));
            if(Verbose) Log($"Queued file{_}.phdt to be compared.", "verbose", ColourScheme.Verbose);
        }

        _semaphore.Release(_semaphoreCount);
        // wait until every task in ValidateTasks is done
        Task.WaitAll(ValidateTasks.ToArray<Task>());
        
        foreach (Task<CompareResult> item in ValidateTasks)
        {
            CompareResult compareResult = await item;
            bool outcome = compareResult.HasFailedCompare;
            
            if (outcome == false) break;
        }

        CompareStopwatch.Stop();
        if (_validateCount == sizeToTest)
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed. (took {ElapsedTime(CompareStopwatch.Elapsed)}, (~)read: {Math.Round(sizeToTest / CompareStopwatch.Elapsed.TotalSeconds)}MB/s)", "status", ColourScheme.Status);

            TotalStopwatch.Stop();
            RevalidationParameters revalidation = new RevalidationParameters()
            {
                Size = sizeToTest,
                DummySize = dummySize
            };
            string revalidationJSON = JsonSerializer.Serialize(revalidation);
            if (File.Exists(Path.Combine(location, "revalidation.json")))
            {
                Log($"revalidation.json already exists in {location}, overwrite? (Y/N): ", "finish", ColourScheme.Finish);
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y)
                {
                    File.WriteAllText(Path.Combine(location, "revalidation.json"), revalidationJSON);
                }
            }
            Log($"Completed successfully in {ElapsedTime(CompareStopwatch.Elapsed + CreateStopwatch.Elapsed)} (full total: {ElapsedTime(TotalStopwatch.Elapsed)}).", "finish", ColourScheme.Finish);
            Log($"You can clear the files in \"{location}\" if you don't intend to run revalidation: phdt -l {location} -rv", "finish", ColourScheme.Finish);
        }
        else
        {
            Log($"{_validateCount} megabytes counted, {sizeToTest} megabytes needed: failed.", "fatal", ColourScheme.Fatal);
            TotalStopwatch.Stop();
            RevalidationParameters revalidation = new RevalidationParameters()
            {
                Size = sizeToTest,
                DummySize = dummySize
            };
            string revalidationJSON = JsonSerializer.Serialize(revalidation);
            if (File.Exists(Path.Combine(location, "revalidation.json")))
            {
                Log($"revalidation.json already exists in {location}, overwrite? (Y/N): ", "finish", ColourScheme.Finish);
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y)
                {
                    File.WriteAllText(Path.Combine(location, "revalidation.json"), revalidationJSON);
                }
            }
            Log($"Completed with errors in {ElapsedTime(CompareStopwatch.Elapsed + CreateStopwatch.Elapsed)} (full total: {ElapsedTime(TotalStopwatch.Elapsed)}).", "finish", ColourScheme.Finish);
            Log($"You can clear the files in \"{location}\" if you don't intend to run revalidation: phdt -l {location} -rv.", "finish", ColourScheme.Finish);
        }


    }

    private static string ElapsedTime(TimeSpan ts) {
        return String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
    }
}