using SmtpProxyServer;
using SmtpProxyServer.Config;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
    private const string AppGuid = "6611d02c-4e11-49ed-ba56-160fba027479";
    private const int AcquireMutexTimeout = 1000;
    private static Mutex mutex;
    private static bool mutexActive;
    private static CancellationTokenSource processCancelToken = new CancellationTokenSource();
    private static SmtpService smtpServer;

    private static async Task<int> Main(string[] args)
    {
        if (!IsOnlyInstance())
        {
            Console.WriteLine("Error: Program is already running");
            await Task.Delay(5000);
            return -1;
        }

        Assembly mainAssembly = Assembly.GetExecutingAssembly();
        AssemblyName mainAssemblyName = mainAssembly.GetName();
        string mainAssemblyPath = Path.GetDirectoryName(Path.GetFullPath(mainAssembly.Location));

        if (!ConfigFile.TryLoad(Path.Combine(mainAssemblyPath, "config.toml"), out ConfigFile config))
            return 1;

        Log.Init(Path.Combine(mainAssemblyPath, "logs", mainAssemblyName.Name + ".log"));

        if (mainAssemblyName.Version != null)
            Log.Info("Started application - v" + mainAssemblyName.Version);
        else
            Log.Info("Started application");

        AppDomain.CurrentDomain.ProcessExit += ProcessExiting;

        AccountValidator validator = new AccountValidator(config.Smtp.EmailDomainFilter, config.UserAccount);
        ExchangeEmailService emailService = new ExchangeEmailService(config.Exchange, validator);
        smtpServer = new SmtpService(config.Smtp, validator, emailService);

        await smtpServer.Start(processCancelToken.Token);

        if (mutexActive)
        {
            try
            {
                // This might throw an exception because of the async method changing the thread
                mutex.ReleaseMutex();
            }
            catch (ApplicationException) { }

        }

        return 0;
    }

    private static bool IsOnlyInstance()
    {
        mutex = new Mutex(true, AppGuid, out mutexActive);
        if (!mutexActive)
        {
            try
            {
                mutexActive = mutex.WaitOne(AcquireMutexTimeout);
                if (!mutexActive)
                    return false;
            }
            catch (AbandonedMutexException)
            { } // Abandoned probably means that the process was killed or crashed
        }

        return true;
    }

    private static async void ProcessExiting(object sender, EventArgs e)
    {
        processCancelToken.Cancel();
        if (smtpServer != null)
            await smtpServer.WaitForExit();
        Log.Info("Application closed");
    }
}