# getpidinfo

Windows REST JSON service that returns CPU, memory and network usage of requested process IDs.

It's being internally used by our control panel backend to gather server status information.
Must be ran as administrator.

Once it's running you can request info of any number of process IDs.

Uses [sharppcap](https://github.com/chmorgan/sharppcap)

Requires installed [windows pcap driver](https://www.winpcap.org/install/)

# Example

Request
```http
http://localhost:2020/?10724,4,13768,678
```
Where after ? is a list of PID numbers glued with comma.

Response
```json
{
	"10724":{ "pid":10724, "cpu":5, "memory":58966016, "network":0 },
	"4":{ "pid":4, "cpu":1, "memory":1839005696, "network":0 },
	"13768":{ "pid":13768, "cpu":1, "memory":370495488, "network":0 },
	"678":{ "pid":678, "cpu":0, "memory":0, "network":0 }
}
```

If pid is not running or is not accessible it will return 0 for all stats.
CPU usage is in percents.
Memory usage is in bytes.
Network usage is bytes sent per second and is averaged over secondsToKeepNetworkSamples second.
