# getpidinfo

C# library that returns CPU, memory and network usage of requested process IDs.

Once it's running you can request info of any number of process IDs.

Uses [sharppcap](https://github.com/chmorgan/sharppcap)

Requires [windows npcap driver](https://nmap.org/npcap/) installed.

# Example
Response
```c#
using getpidinfo;
...
PidConnector pidInfo = new PidConnector();
PidInfoData data = pidInfo.QueryProcess(pid);
```
# PidInfoData
If pid is not running or is not accessible it will return 0 for all stats.

CPU usage is in percent.

Memory usage is in bytes.

Network usage is bytes sent per second and is averaged over secondsToKeepNetworkSamples second.
