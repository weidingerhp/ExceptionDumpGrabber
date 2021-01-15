using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

internal class StartupHook {
    private static String DumpDir = "/tmp";
    private static int DumpNumber = 0;
    
    public static void Initialize() {
        DumpDir = Environment.GetEnvironmentVariable("DT_CRASH_DUMP_DIR") ?? DumpDir;
        
        Console.Out.WriteLine($"Exception Grabber active - wrtiting dumps to {DumpDir}");
        
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
            Process.Start("createdump", $"--full --name {DumpDir}/{name}-{((ex != null) ? ex.GetType().Name : "null")}-%p-%t({DumpNumber++}).core {Process.GetCurrentProcess().Id}")?.WaitForExit(30_000);
        } 
        catch (Exception e) {
            Console.Out.WriteLine($"Failed to get dump number {DumpNumber}. Reason: {e}");
        }
    }
}