using SmtpProxyServer;
using SmtpProxyServer.Config;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

internal class Program
{
    private const string AppGuid = "6611d02c-4e11-49ed-ba56-160fba027479";
    private const int AcquireMutexTimeout = 1000;
    private static Mutex mutex;
    private static bool mutexActive;

    private static async Task<int> Main(string[] args)
    {
        if (!IsOnlyInstance())
        {
            Console.WriteLine("Error: Program is already running");
            await Task.Delay(5000);
            return -1;
        }

        string appData = InitAppData();
        if (!ConfigFile.TryLoad(Path.Combine(appData, "config.toml"), out ConfigFile config))
            return 1;

        AccountValidator validator = new AccountValidator(config.Smtp.EmailDomainFilter, config.UserAccount);

        ExchangeEmailService emailService = new ExchangeEmailService(config.Exchange, validator);

        SmtpService smtpServer = new SmtpService(config.Smtp, validator, emailService);
        await smtpServer.Start();

        await Task.Delay(60000);

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

    private static string InitAppData()
    {
        AssemblyName mainAssemblyName = typeof(Program).Assembly.GetName();
        string appname = mainAssemblyName.Name;
        string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("No /AppData/Local/ folder exists!");
        path = Path.Combine(path, appname);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        Log.Init(Path.Combine(path, "Logs", appname + ".log"));
        if (mainAssemblyName.Version != null)
            Log.Info("Started application - v" + mainAssemblyName.Version);
        else
            Log.Info("Started application");

        return path;
    }
}