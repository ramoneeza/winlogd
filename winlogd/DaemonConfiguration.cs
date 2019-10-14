using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;

namespace winlogd
{
    [SuppressMessage("Globalization", "CA1303:No pasar cadenas literal como parámetros localizados", Justification = "<pendiente>")]
    [SuppressMessage("Performance", "CA1815:Reemplazar Equals y el operador Equals en los tipos de valor", Justification = "<pendiente>")]
    [SuppressMessage("Design", "CA1051:No declarar campos de instancia visibles", Justification = "<pendiente>")]
    public struct DaemonConfiguration
    {
        public bool Test;
        public IPAddress Server;
        public int Port;
        public bool Verbose;

        [SuppressMessage("Performance", "CA1819:Las propiedades no deben devolver matrices", Justification = "<pendiente>")]
        public EventLogEntryType[] Levels { get; private set; }

        public string Level
        {
            get => string.Join(",", Levels ?? Array.Empty<EventLogEntryType>());
            set
            {
                var v = value ?? "All";
                if (StringComparer.OrdinalIgnoreCase.Compare(v, "All") == 0)
                    Levels =(EventLogEntryType[])Enum.GetValues(typeof(EventLogEntryType));
                else
                {
                    var preLevels = v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                    Levels = preLevels.Select(s => (EventLogEntryType)Enum.Parse(typeof(EventLogEntryType), s, true)).ToArray();
                }
            }
        }
    }
}
