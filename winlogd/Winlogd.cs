using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace winlogd
{
    [SuppressMessage("Globalization", "CA1303:No pasar cadenas literal como parámetros localizados", Justification = "<pendiente>")]
    public sealed class Winlogd:IDisposable
    {
        private static readonly string[] _months = new string[]{"NUL","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };
        public DaemonConfiguration Configuration { get; }
        
        public IPEndPoint EndPoint { get; }

        public bool Started { get; private set; } 
        
        public Winlogd(DaemonConfiguration configuration)
        {
            Configuration = configuration;
            _exitevent= new AutoResetEvent(false);
            EndPoint = new IPEndPoint(Configuration.Server, Configuration.Port);
        }

        public void SysLogSend(byte[] pkt)
        {
            using(Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,ProtocolType.Udp))
            {
                sock.SendTo(pkt , EndPoint);
            }
        }

        public void SysLogSend(string msg)
        {
            var pkt = Encoding.ASCII.GetBytes(msg);
            SysLogSend(pkt);
        }

        private readonly AutoResetEvent _exitevent;

        [SuppressMessage("Design", "CA1031:No capture tipos de excepción generales.", Justification = "<pendiente>")]
        public async Task<int> Run()
        {

            Start();
            try
            {
                await Task.Run(()=>_exitevent.WaitOne(-1)).ConfigureAwait(false);
                Stop();
                return 0;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exit due to error {ex.Message}");
                return 1;
            }
        }
        private void Start()
        {
            if (Started) return;
            // Attach Listener to each EventLog
            var p = new EventLogPermission(EventLogPermissionAccess.Administer,".");
            p.Demand();
            var mlogs = EventLog.GetEventLogs();
            foreach (var el in mlogs)
            {
                try
                {
                    el.EnableRaisingEvents = true;
                    el.EntryWritten += EventHook;
                }
                catch (InvalidOperationException ex)
                {
                    if (Configuration.Verbose) 
                        Console.WriteLine(ex.Message);
                }
                catch (SecurityException ex)
                {
                    if (Configuration.Verbose) 
                        Console.WriteLine(ex.Message);
                }
            }
            if (Configuration.Verbose) Console.WriteLine("Service Started...");
            Started = true;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private enum SyslogLevel
        { 
            Emergency=0,
            Alert=1,
            Critical=2,
            Error=3,
            Warning=4,
            Notice=5,
            Informational=6,
            Debug=7
        }
        private void EventHook(object sender, EntryWrittenEventArgs e)
        {
            
            
            // Windows Only Has Error, Warning and Notice, FailureAudit and SuccessAudit

            
            if (!Configuration.Levels.Contains(e.Entry.EntryType))
            {
                if (Configuration.Verbose)
                    Console.WriteLine($"New Event with level {e.Entry.EntryType} omitted");
                return;
            }

            var level = SyslogLevel.Critical;
            switch (e.Entry.EntryType)
            {
                case EventLogEntryType.Error:
                    level = SyslogLevel.Error;
                    break;
                case EventLogEntryType.Warning:
                    level = SyslogLevel.Warning;
                    break;
                case EventLogEntryType.Information:
                    level = SyslogLevel.Informational;
                    break;
                case EventLogEntryType.SuccessAudit:
                    level = SyslogLevel.Informational;
                    break;
                case EventLogEntryType.FailureAudit:
                    level = SyslogLevel.Informational;
                    break;
            }
            
            if (Configuration.Verbose)
            {
                Console.WriteLine($"New Event: {e.Entry.EntryType} {e.Entry.TimeGenerated:t} {e.Entry.MachineName} {e.Entry.Message}");
            }

            var msg=BuildMessage(e, level);

            
            if (Configuration.Verbose)
            {
                Console.WriteLine($"Event to Send: {msg}");
            }

            if (Configuration.Test)
            {
               if (Configuration.Verbose) Console.WriteLine("Test Mode. No send.");
               return;
            }
            SysLogSend(msg);
        }

        private static string BuildMessage(EntryWrittenEventArgs e, SyslogLevel level)
        {
            return BuildMessage(level, e.Entry.TimeGenerated, e.Entry.MachineName, e.Entry.Source, e.Entry.InstanceId,
                e.Entry.Message);
        }

        private static string BuildMessage(SyslogLevel level,DateTime t,string machine,string source,long instanceid,string message)
        {
            var msg=new StringBuilder();
// PRI
            var priority = 16 * 8 + (int) level;
            msg.Append($"<{priority}>");

            // HEADER::TIMESTAMP
            msg.Append($"{_months[t.Month]} {t.Day:00} {t.Hour:00}:{t.Minute:00}:{t.Second:00} ");

            // HEADER::HOSTNAME
            msg.Append(machine.ToLower(CultureInfo.CurrentCulture).Replace(" ", "_"));
            msg.Append(" ");

            // MSG::TAG
            msg.Append($"{source.Replace(' ', '_')}[{instanceid}]: ");

            // MSG::CONTENT
            // Have to clean out \r and \t, syslog will replace \n with " "
            msg.Append(message.Replace("\r", "").Replace("\t", ""));
            return msg.ToString();
        }

        private void Stop()
        {
            if (!Started) return;
            var mlogs = EventLog.GetEventLogs();
            foreach (var el in mlogs)
            {
                el.EnableRaisingEvents = false;
                el.EntryWritten -= EventHook;
            }
            Started = false;
        }

        public void Dispose()
        {
            Exit();
            _exitevent.Dispose();
           
        }

        public void Exit()
        {
            _exitevent.Set();
            if (Started) Thread.Sleep(100);
            Console.WriteLine("Service ended.");
        }

        public void SendTestLog()
        {
            var msg = BuildMessage(SyslogLevel.Informational, DateTime.Now, Environment.MachineName, "Application", 0,
                "WinLogD Testing");
            SysLogSend(msg);
            if (Configuration.Verbose)
            {
                Console.WriteLine("Test message sent to SysLog.");
            }
        }

        
        public void CreateEventLog()
        {
            using (EventLog eventLog = new EventLog("Application")) 
            {
                eventLog.Source = "Application"; 
                eventLog.WriteEntry("Log message example", EventLogEntryType.Information, 101, 1); 
                if (Configuration.Verbose)
                {
                    Console.WriteLine("Create test eventlog.");
                }
            }
        }

    }
}
