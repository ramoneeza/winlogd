using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;


namespace winlogd
{
    class Program
    {
        public static readonly string Version=string.Join(".",Assembly.GetExecutingAssembly().GetName().Version.ToString().Split('.').Take(3));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:No pasar cadenas literal como parámetros localizados", Justification = "<pendiente>")]
        static async Task<int> Main(string[] args)
        {
            args = args ?? Array.Empty<string>();
            if (args.Length == 0)
            {
                Do_Help();
                return 0;
            }

            int r;
            if (HasUnique(args, "-h", "--help", Do_Help, out r)) return r;
            if (HasUnique(args, "-V", "--version", Do_Version, out r)) return r;

            var daemon = Has(args, "-d", "--daemon");

            if (daemon == false)
            {
                Do_Help("No -d argument found");
                return 1;
            }

            var ip =Dns.GetHostAddresses(HasStr(args, "-s", "--server") ?? "localhost").FirstOrDefault(a=>a.AddressFamily== AddressFamily.InterNetwork);

            var configuracion = new DaemonConfiguration()
            {
                Test = Has(args, "-t", "--test"),
                Server = ip,
                Port =int.Parse(HasStr(args, "-p", "--port") ?? "514", CultureInfo.InvariantCulture),
                Level = HasStr(args, "-l", "--level"),
                Verbose = Has(args, "-v", "--verbose")
            };
            if (string.IsNullOrEmpty(configuracion.Level)) configuracion.Level = "Error";
            if (configuracion.Verbose)
            {
                Do_Version();
                Console.WriteLine("Configuration:");
                Console.WriteLine($"Test:\t{configuracion.Test}");
                Console.WriteLine($"Server:\t{configuracion.Server}");
                Console.WriteLine($"Port:\t{configuracion.Port}");
                Console.WriteLine($"Level:\t{configuracion.Level}");
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                Console.WriteLine($"Verbose:\t{configuracion.Verbose}");
            }

            using (_service = new Winlogd(configuracion))
            {
                Console.CancelKeyPress += DoCancel;
                CreateConsoleKeyTask();
                Console.WriteLine("CTRL+C to interrupt the daemon.");
                Console.WriteLine("T to test the syslog destiny.");
                Console.WriteLine("E to test the eventlog.");
                int exitresult = await _service.Run().ConfigureAwait(false);
                Console.WriteLine("Closing the daemon");
                _cancelKey.Cancel();
                _keyTask.Wait(200);
                return exitresult;
            }
        }

        private static Winlogd _service;
        private static Task _keyTask;
        private static CancellationToken _cancelTokenKey;
        private static CancellationTokenSource _cancelKey;
        private static void CreateConsoleKeyTask()
        {
            _cancelKey = new CancellationTokenSource();
            _cancelTokenKey = _cancelKey.Token;
            _keyTask = Task.Run(ConsoleKeys, _cancelTokenKey);
        }

        private static void DoCancel(object sender, ConsoleCancelEventArgs e)
        {
            _service.Exit();
        }
        private static bool HasUnique(string[] args, string v1, string v2, Action action, out int r)
        {
            r = 0;
            if (!Has(args, v1, v2)) return false;
            if (args.Length != 1)
            {
                r = 1;
                Do_Help("Bad Arguments");
                return true;
            }
            action();
            return true;
        }
        public static void Do_Version() => Console.WriteLine($"winlogd V:{Version}. EventLog to Syslog Forwarder Daemon.");

        private static string HasStr(string[] args, params string[] v)
        {
            var s = args.SkipWhile(a => !v.Contains(a)).Take(2).ToArray();
            if (s.Length != 2) return null;
            if (s[1].StartsWith("-",StringComparison.Ordinal)) return null;
            return s[1];
        }

        private static bool Has(string[] args, params string[] v)=>args.Any(v.Contains);

        private static void Do_Help() => Do_Help(null);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:No pasar cadenas literal como parámetros localizados", Justification = "<pendiente>")]
        private static void Do_Help(string error)
        {
            Do_Version();
            Console.WriteLine();
            if (error!=null) Console.WriteLine($"Error: {error}");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h --help\tShow this help.");
            Console.WriteLine("  -v --verbose\tVerbose mode.");
            Console.WriteLine("  -d --daemon\tRun program without exit.");
            Console.WriteLine("  -t --test\tTest mode, doesn't forward events.");
            Console.WriteLine("  -V --version\tShow program version");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine("  -s --server   [ip|name]   Syslog server address to forward.");
            Console.WriteLine("  -p --port     [int]       Syslog server port to forward.");
            Console.WriteLine("  -l --level    [level,...] Levels to forward:");
            Console.WriteLine("                                Info     -> Informational Event");
            Console.WriteLine("                                Warning  -> Warning Message");
            Console.WriteLine("                                Error    -> Error Message");
            Console.WriteLine("                                AuditOk  -> Success Audit");
            Console.WriteLine("                                AuditFail-> Failure Audit Event");
            Console.WriteLine("                                All      -> Any Event. Must be alone");
        }

        private static void ConsoleKeys()
        {
            while (!_cancelTokenKey.IsCancellationRequested)
            {
                _cancelTokenKey.ThrowIfCancellationRequested();
                if (!Console.KeyAvailable) Thread.Sleep(100);
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.T)
                {
                    SendTestLog();
                }
                if (k.Key == ConsoleKey.E)
                {
                    CreateEventLog();
                }
            }
        }

        private static void SendTestLog()
        {
            _service.SendTestLog();
        }
        private static void CreateEventLog()
        {
            _service.CreateEventLog();
        }
    }
}
