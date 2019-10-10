using System;
using System.Linq;
using System.Threading;

namespace winlogdcore
{
    class Program
    {
        public const string Version="0.0";

        static int Main(string[] args)
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

            var configuracion = new Configuration()
            {
                Test = Has(args, "-t", "--test"),
                Server = HasStr(args, "-s", "--server") ?? "localhost",
                Port = HasStr(args, "-p", "--port") ?? "514",
                External = Has(args, "-e", "--external"),
                Level = HasStr(args, "-l", "--level"),
                Verbose = Has(args, "-v", "--verbose")
            };
            if (string.IsNullOrEmpty(configuracion.Level)) configuracion.Level = "Error";
            if (configuracion.Verbose)
            {
                Console.WriteLine("Configuration:");
                Console.WriteLine($"Test:\t{configuracion.Test}");
                Console.WriteLine($"Server:\t{configuracion.Server}");
                Console.WriteLine($"Port:\t{configuracion.Port}");
                Console.WriteLine($"External:\t{configuracion.External}");
                Console.WriteLine($"Level:\t{configuracion.Level}");
                Console.WriteLine($"Verbose:\t{configuracion.Verbose}");
            }

            var service = new Winlogd(configuracion);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(DoCancel);
            Console.WriteLine("CTRL+C to interrupt the daemon:");
            ExitEvent = new AutoResetEvent(false);
            var task = new Thread(() => service.Start());
            task.Start();
            while (service.Started)
            {
                Thread.Sleep(0);
            }
            
        Console.WriteLine("Closing the daemon");
            service.Stop();
            return ExitResult;
        }

        private static void DoCancel(object sender, ConsoleCancelEventArgs e)
        {
            ExitEvent.Set();
        }

        public static AutoResetEvent ExitEvent { get; set; }
        public static int ExitResult { get; set; } = 0;
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
        private static void Do_Version() => Console.WriteLine($"winlogdcore V:{Version}. Windows Log Forward Daemon.");

        private static string HasStr(string[] args, params string[] v)
        {
            var s = args.SkipWhile(a => !v.Contains(a)).Take(2).ToArray();
            if (s.Length != 2) return null;
            if (s[1].StartsWith("-")) return null;
            return s[1];
        }

        private static bool Has(string[] args, params string[] v)=>args.Any(v.Contains);

        private static void Do_Help() => Do_Help(null);
        private static void Do_Help(string error)
        {
            Do_Version();
            Console.WriteLine();
            if (error!=null) Console.WriteLine($"Error: {error}");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -h --help\tShow this help.");
            Console.WriteLine("  -V --verbose\tVerbose mode.");
            Console.WriteLine("  -d --daemon\tRun program without exit.");
            Console.WriteLine("  -t --test\tTest mode, doesn't forward events.");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine("  -s --server   [ip|name]   Syslog server address to forward.");
            Console.WriteLine("  -p --port     [int]       Syslog server port to forward.");
            Console.WriteLine("  -e --external [name]      Allow External Events. (It Adds computer name to facility)");
            Console.WriteLine("  -l --level    [level,...] Levels to forward:");
            Console.WriteLine("                                Info    -> Informational Event");
            Console.WriteLine("                                Warning -> Warning Message");
            Console.WriteLine("                                Error   -> Error Message");
            Console.WriteLine("                                Success -> Success Audit");
            Console.WriteLine("                                Failure -> Failure Audit Event");
            Console.WriteLine("                                All -> Any Event. Must be alone");
        }

    }
}
