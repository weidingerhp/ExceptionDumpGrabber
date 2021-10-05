using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;

internal class StartupHook {
    private static readonly char[] Delimiters = { ',', ';', '-', '|', ' ' };

    private static string DumpDir = "/tmp";
    private static string DumpExecutable { get; set; }
    private static int DumpNumber = 0;
    private static string[] FirstChanceExceptionFilter;

    private static string HostName { get; set; }

    public static bool IsReadOnly { get; set; }

    public static void Initialize() {
        IsReadOnly = false;
        DumpDir = Environment.GetEnvironmentVariable("DT_CRASH_DUMP_DIR") ?? DumpDir;
        DumpExecutable = Environment.GetEnvironmentVariable("DT_DUMP_EXEC") ?? "createdump";
        ReadFirstChanceExceptionFilter();

        Console.Out.WriteLine($"Exception Grabber active - wrtiting dumps to {DumpDir}");

        HostName = Environment.MachineName;
        try {
            IsReadOnly = (new DirectoryInfo($"{DumpDir}").Attributes & FileAttributes.ReadOnly) != 0;

            if (!IsReadOnly) {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
                AppDomain.CurrentDomain.FirstChanceException +=CurrentDomainOnFirstChanceException;
            }
            else {
                Console.Out.WriteLine($"Directory {DumpDir} is readonly - cannot write dumps. Aborting");
            }
        }
        catch (Exception ex) {
            Console.Out.WriteLine($"Exception initializing ExceptionGrabber for {DumpDir} - {ex.Message}");
        }
    }

    private static void ReadFirstChanceExceptionFilter() {
        var envVarValue = Environment.GetEnvironmentVariable("DT_FIRST_CHANCE_EXCEPTIONS") ?? string.Empty;
        var exceptions = envVarValue.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries);
        if (exceptions == null) {
            FirstChanceExceptionFilter = new string[0];
            Console.Out.WriteLine("Warning: no exception filter patterns");
            return;
        }
        FirstChanceExceptionFilter = exceptions.Select(e => e.Trim()).ToArray();
        Console.Out.WriteLine($"Exception patterns: {string.Join(", ", FirstChanceExceptionFilter)}");
    }

    private static void CurrentDomainOnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e) {
        foreach (var pattern in FirstChanceExceptionFilter) {
            if (e.Exception.GetType().FullName.Contains(pattern, StringComparison.OrdinalIgnoreCase)) {
                CreateDump("firstchance", e.Exception);
                return;
            }
        }
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        CreateDump("unhandled", e.ExceptionObject as Exception);
    }

    private static void CreateDump(string name, Exception ex) {
        try {
            var logFileName = DetermineLogFileName();

            using (var logFileStream = File.OpenWrite(logFileName)) {
                try {
                    WriteLog(logFileStream, $"Writing Dump for {ex.GetType().FullName}");
                    WriteLog(logFileStream, ex.ToString());

                    var dumpFileName =
                        $"{DumpDir}/{HostName}/{name}-{((ex != null) ? ex.GetType().Name : "null")}-{DumpNumber++}.core";

                    WriteLog(logFileStream, $"Logfile name: {dumpFileName}");
                    Process.Start(DumpExecutable,
                            $"--full --name {dumpFileName} {Process.GetCurrentProcess().Id}")
                        ?.WaitForExit(30_000);
                    WriteLog(logFileStream, $"Finished writing dump to {dumpFileName}");
                }
                catch (Exception dumpException) {
                    WriteLog(logFileStream, $"Error while writing Dump: {dumpException.ToString()}");
                }
            }

        }

        catch (Exception e) {
            Console.Out.WriteLine($"Failed to get dump number {DumpNumber}. Reason: {e}");
        }
    }

    private static void WriteLog(Stream logStream, string log) {
        foreach(var line in log.Split('\n'))
        {
            logStream.Write(Encoding.UTF8.GetBytes($"{DateTime.Now:yyyy-dd-M--HH-mm-ss} - {line}\n"));
        }
    }

    private static string DetermineLogFileName() {
        string baseLogFileName = $"{DumpDir}/{HostName}/crashget-{DateTime.Now:yyyy-dd-M--HH-mm-ss}";
        string logFileName     = $"{baseLogFileName}.log";
        int    n               = 0;

        while (File.Exists(logFileName)) {
            logFileName = $"{baseLogFileName}-{++n}.log";
        }

        if (!Directory.Exists($"{DumpDir}/{HostName}")) {
            Directory.CreateDirectory($"{DumpDir}/{HostName}");
        }

        return logFileName;
    }
}