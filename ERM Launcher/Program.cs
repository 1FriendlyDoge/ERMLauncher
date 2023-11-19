using Avalonia;
using System;
using System.Diagnostics;
using Avalonia.Svg.Skia;
using Sentry;

namespace ERM_Launcher;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    [DebuggerStepThrough]
    public static void Main(string[] args)
    {
        SentrySdk.Init(o =>
        {
            o.Dsn = "https://4ba6c01215c74866b823883bbcf18442@o968027.ingest.sentry.io/5919400";
            o.Debug = false;
            o.EnableTracing = true;
            o.IsGlobalModeEnabled = true;
        });
        
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
    
    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception ?? new Exception("Unknown exception");

        SentrySdk.CaptureException(ex);
        
        throw ex;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);
        GC.KeepAlive(typeof(Avalonia.Svg.Skia.Svg).Assembly);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}