using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;

internal class StartupHook {
    private static string DumpDir = "/tmp";
    private static string DumpExecutable { get; set; }
    private static int DumpNumber = 0;

    private static string HostName { get; set; }

    public static void Initialize() {
        DumpDir = Environment.GetEnvironmentVariable("DT_CRASH_DUMP_DIR") ?? DumpDir;
        DumpExecutable = Environment.GetEnvironmentVariable("DT_DUMP_EXEC") ?? "createdump";
        
        Console.Out.WriteLine($"Exception Grabber active - wrtiting dumps to {DumpDir}");

        HostName = Environment.MachineName;
        Directory.CreateDirectory($"{DumpDir}/{HostName}");
        
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        AppDomain.CurrentDomain.FirstChanceException +=CurrentDomainOnFirstChanceException;
    }


    private static void CurrentDomainOnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e) {
        if (e.Exception is System.IO.IOException && (e.Exception?.Message?.StartsWith("The process cannot access the file") ?? false)) {
            CreateDump("firstchance", e.Exception);
        }
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        CreateDump("unhandled", e.ExceptionObject as Exception);
    }

    private static void CreateDump(string name, Exception ex) {
        try {
            Process.Start(DumpExecutable, $"--full --name {DumpDir}/{HostName}/{name}-{((ex != null) ? ex.GetType().Name : "null")}-%p-%t({DumpNumber++}).core {Process.GetCurrentProcess().Id}")?.WaitForExit(30_000);
        } 
        catch (Exception e) {
            Console.Out.WriteLine($"Failed to get dump number {DumpNumber}. Reason: {e}");
        }
    }
}