using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using winlogdcore;

namespace winlogd
{
    
    public class Winlogd:IDisposable
    {
        private static readonly string[] _months = new string[]{"NUL","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };
        public Configuration Configuration { get; }
        public bool Started { get; private set; } = false;

        public Winlogd(Configuration configuration)
        {
            Configuration = configuration;
            _exitevent= new AutoResetEvent(false);
            SyslogServer = new UdpClient(int.Parse(Configuration.Port));
        }

        public UdpClient SyslogServer { get; private set; }

        private readonly AutoResetEvent _exitevent;

        public async Task<int> Run()
        {

            Start();
            try
            {
                //var res=await Task.Run(Loop);
                await Task.Run(()=>_exitevent.WaitOne(-1));
                Stop();
                return 0;
            }
            catch
            {
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
        
        private void EventHook(object sender, EntryWrittenEventArgs e)
        {
            
            var msg = new StringBuilder();
            // Windows Only Has Error, Warning and Notice, FailureAudit and SuccessAudit

            var level = EventLevel.Critical;
            switch (e.Entry.EntryType)
            {
                case EventLogEntryType.Error: level = EventLevel.Error;
                    break;
                case EventLogEntryType.Warning:
                    level = EventLevel.Warning;
                    break;
                case EventLogEntryType.Information:
                    level = EventLevel.Information;
                    break;
            }

            if (!Configuration.Levels.Contains(level))
            {
                if (Configuration.Verbose)
                    Console.WriteLine($"New Event with level {level} omitted");
                return;
            }

            if (Configuration.Verbose)
            {
                Console.WriteLine($"New Event: {e.Entry.EntryType} {e.Entry.TimeGenerated:t} {e.Entry.MachineName} {e.Entry.Message}");
            }

            // PRI
            var priority = 16 * 8 + (int) level;
            msg.Append($"<{priority}>");

            // HEADER::TIMESTAMP
            var t = e.Entry.TimeGenerated;
            msg.Append($"{_months[t.Month]} {t.Day:00} {t.Hour:00}:{t.Minute:00}:{t.Second:00} ");
            
            // HEADER::HOSTNAME
            msg.Append(e.Entry.MachineName.ToLower().Replace(" ","_"));
            msg.Append(" ");
    
            // MSG::TAG
            msg.Append($"{e.Entry.Source.Replace(' ', '_')}[{e.Entry.InstanceId}]: ");
            
            // MSG::CONTENT
            // Have to clean out \r and \t, syslog will replace \n with " "
            msg.Append(e.Entry.Message.Replace("\r", "").Replace("\t", ""));
            
            // Send To Server
            var pkt = System.Text.Encoding.ASCII.GetBytes(msg.ToString());

            if (Configuration.Verbose)
            {
                Console.WriteLine($"Event to Send: {msg}");
            }

            if (Configuration.Test)
            {
               if (Configuration.Verbose) Console.WriteLine("Test Mode. No send.");
               return;
            }
            SyslogServer.Send(pkt, pkt.Length);
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
            SyslogServer.Dispose();
        }

        public void Exit()
        {
            _exitevent.Set();
            if (Started) Thread.Sleep(100);
            Console.WriteLine("Service ended.");
        }
    }
}
