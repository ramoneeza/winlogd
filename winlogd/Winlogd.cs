using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net.Sockets;
using System.Security;
using System.Security.Permissions;

namespace winlogdcore
{
    
    public class Winlogd
    {
        private static readonly string[] _months = new string[]{"NUL","Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };
        public Configuration Configuration { get; }
        public bool Started { get; private set; } = false;

        public Winlogd(Configuration configuration)
        {
            Configuration = configuration;
        }

        public UdpClient SyslogServer { get; private set; }
        
        public void Start()
        {
            if (Started) return;
            SyslogServer = new UdpClient(int.Parse(Configuration.Port));
            // Attach Listener to each EventLog

            var p = new EventLogPermission(EventLogPermissionAccess.Audit,".");
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
            Started = true;
        }
        
        private void EventHook(object sender, EntryWrittenEventArgs e)
        {
            if (Configuration.Verbose)
            {
                Console.WriteLine($"New Event: {e.Entry.EntryType} {e.Entry.TimeGenerated:t} {e.Entry.MachineName} {e.Entry.Message}");
            }
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

        public void Stop()
        {
            if (!Started) return;
            var mlogs = EventLog.GetEventLogs();
            foreach (var el in mlogs)
            {
                el.EnableRaisingEvents = false;
                el.EntryWritten -= EventHook;
            }
            Started = false;
            SyslogServer.Dispose();
        }
        
    }
}
