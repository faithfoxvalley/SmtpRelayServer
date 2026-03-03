using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;
using System;

namespace SmtpRelayServer;

public static class Log
{
    private static Logger log;

    public static void Init(string file = null)
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;


        ExpressionTemplate format = new ExpressionTemplate("{@t:HH:mm:ss} [{@l:u3}] [{ThreadID}]{CustomPrefix} {@m} {@x}\n");
        LoggerConfiguration logConfig = new LoggerConfiguration()
            .Enrich.With(new ThreadIDEnricher())
#if DEBUG
            .WriteTo.Debug(format)
#endif
            .WriteTo.Console(format);

        if (file != null)
            logConfig = logConfig.WriteTo.File(format, file, rollingInterval: RollingInterval.Day);

        log = logConfig.CreateLogger();


    }

    private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            log.Fatal(ex, "An exception was thrown:");
        else
            log.Fatal("An unhandled exception occurred.");
    }

    public static void Info(string msg)
    {
        Write(LogEventLevel.Information, msg);
    }

    public static void Error(string msg)
    {
        Write(LogEventLevel.Error, msg);
    }

    public static void Warn(string msg)
    {
        Write(LogEventLevel.Warning, msg);
    }

    public static void Error(Exception ex)
    {
        log.Error(ex, "An exception was thrown:");
    }

    public static void Error(Exception ex, string msg)
    {
        log.Error(ex, msg);
    }

    private static void Write(LogEventLevel level, string msg)
    {
        log.Write(level, msg);
    }

    private class PrefixEnricher : ILogEventEnricher
    {
        private readonly string prefix;

        public PrefixEnricher(string prefix)
        {
            this.prefix = " " + prefix;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CustomPrefix", prefix));
        }
    }

    private class ThreadIDEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
              "ThreadID", Environment.CurrentManagedThreadId.ToString()));
        }
    }
}