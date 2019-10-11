using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace winlogd
{
    public struct Configuration
    {
        public bool Test;
        public string Server;
        public string Port;
        public bool Verbose;
        public EventLevel[] Levels { get; private set; }

        public string Level
        {
            get => string.Join(",", Levels??Array.Empty<EventLevel>());
            set
            {
                var v = value ?? "All";
                if (v.Contains("All")) 
                    Levels=(EventLevel[])Enum.GetValues(typeof(EventLevel));
                else
                    Levels=v.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries).Select(l=>(EventLevel)Enum.Parse(typeof(EventLevel),l)).ToArray();
            }
        }


        
    }
    public enum EventLevel
    {
        Critical = 2,
        Error = 3,
        Warning = 4,
        Notice = 5,
        Information = 6
    }
}
