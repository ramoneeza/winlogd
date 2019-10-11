# winlogd
Windows EventLogs to Syslog Forwarder

## Getting Started

a) Copy executable to a folder
b) Using Task Scheduler, make a new Task to start on system startup (like a service) 

### Prerequisites

.NET Framework 4.7.2

### Installing

a) Copy executable to a folder
b) Using Task Scheduler, make a new Task to start on system startup (like a service) 

Can be used as simple consola app.

winlogd -h

```
winlogd V:0.0. Windows Log Forward Daemon.


Options:
  -h --help     Show this help.
  -V --verbose  Verbose mode.
  -d --daemon   Run program without exit.
  -t --test     Test mode, doesn't forward events.

Parameters:
  -s --server   [ip|name]   Syslog server address to forward.
  -p --port     [int]       Syslog server port to forward.
  -l --level    [level,...] Levels to forward:
                                Info    -> Informational Event
                                Warning -> Warning Message
                                Error   -> Error Message
                                Success -> Success Audit
                                Failure -> Failure Audit Event
                                All -> Any Event. Must be alone

## Authors

* Ramón Ordiales Plaza

## License

This project is licensed under GNU 3.0 - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments and Notes

* Original winlogd was a windows service written on c++ ten years ago.  
Microsoft changed EventLog Access Permissions (starting with Windows Vista) 
So, a new program becomes necessary to achieve same results. 
* Other free software available have a GUI.  
With the wide availability of Windows Server CORE / NANO where no GUI is available, a simpler solution is necessary. 
* Porting to .NET Core is not possible, (as far I know) Security permission to audit Event Logs are not available in.NET CORE 3.0
