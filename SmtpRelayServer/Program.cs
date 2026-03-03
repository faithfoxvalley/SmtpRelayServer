using SmtpRelayServer.Config;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SmtpRelayServer;

internal class Program
{
    private const string EnvPrefix = "RELAY_";
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

        ConfigFile config;
        bool docker = args?.Any(x => x.Equals("--docker", StringComparison.InvariantCultureIgnoreCase)) == true;
        if(docker)
        {
            Log.Init();
            config = ConfigFile.Load("/data/config.toml", EnvPrefix);
        }
        else
        {
            string dataPath = Path.GetDirectoryName(Path.GetFullPath(mainAssembly.Location));
            Log.Init(Path.Combine(dataPath, "logs", mainAssemblyName.Name + ".log"));
            config = ConfigFile.Load(Path.Combine(dataPath, "config.toml"), EnvPrefix);
        }
        
        if (mainAssemblyName.Version != null)
            Log.Info("Started application - v" + mainAssemblyName.Version.ToString(3));
        else
            Log.Info("Started application");

        HookProcessClosing();

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

        Log.Info("Application stopped");
        return 0;
    }

    private static bool IsOnlyInstance()
    {
        mutex = new Mutex(true, "Global\\" + AppGuid, out mutexActive);
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

    private static void HookProcessClosing()
    {
        PosixSignalRegistration.Create(PosixSignal.SIGINT, ExitRequested);
        PosixSignalRegistration.Create(PosixSignal.SIGHUP, ExitRequested);
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, ExitRequested);
    }

    private static async void ExitRequested(PosixSignalContext context)
    {
        Log.Info($"Application stop requested ({context?.Signal.ToString() ?? ""})");
        processCancelToken.Cancel();
        if (smtpServer != null)
            await smtpServer.WaitForExit();
    }
}