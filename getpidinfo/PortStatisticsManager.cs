using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace getpidinfo
{
    public class PortStatisticsManager
    {
        private class PidData
        {
            public long lastBytesSentPerSecond;
            public CapturePortsStatistics statistics;
        }

        private TimeSpan timespanToKeepNetworkSamples;
        private TimeSpan timespanToClosePIDPortsWithNoRequests;
        private TimeSpan timespanToUpdatePidToPortsTable;
        public PortStatisticsManager(int secondsToKeepNetworkSamples, int secondsToClosePIDPortsWithNoRequests, int secondsToUpdatePidToPortsTable)
        {
            timespanToKeepNetworkSamples = new TimeSpan(0, 0, secondsToKeepNetworkSamples);
            timespanToClosePIDPortsWithNoRequests = new TimeSpan(0, 0, secondsToClosePIDPortsWithNoRequests);
            timespanToUpdatePidToPortsTable = new TimeSpan(0, 0, secondsToUpdatePidToPortsTable);
        }


        public void Tick()
        {
            TryUpdatePIDToPorts();


            var startDataAtThisTime = DateTime.Now - timespanToKeepNetworkSamples;

            foreach (var kvp in pidRequestToLastTime)
            {
                var pid = kvp.Key;
                var time = kvp.Value;

                PidData data;
                pidToData.TryGetValue(pid, out data);

                if (time + timespanToClosePIDPortsWithNoRequests > DateTime.Now)
                {
                    // to watch

                    ushort[] ports = null;
                    List<ushort> portsList;
                    if (pidToPorts.TryGetValue(pid, out portsList)) ports = portsList.ToArray();


                    if (data == null)
                    {
                        var process = TimeCachedGetProcessByPid.Singleton.GetProcessForPid(pid);
                        if (process != null) // be sure the process is running
                        {
                            Debug.WriteLine("Creating ports listener for pid:" + pid);
                            var statistics = GetCapturePortsStatistics();
                            statistics.CaptureThesePorts(ports);

                            data = new PidData() { lastBytesSentPerSecond = 0, statistics = statistics };
                            pidToData[pid] = data;
                        }

                    }
                    else
                    {
                        data.statistics.ClampData(startDataAtThisTime);
                        data.lastBytesSentPerSecond = (long)Math.Round(data.statistics.GetAverageOfReceviedBytes() / timespanToKeepNetworkSamples.TotalSeconds);
                        data.statistics.CaptureThesePorts(ports);
                    }
                }
                else
                {
                    // to not watch

                    if (data != null)
                    {
                        Debug.WriteLine("Destroying ports listener for pid:" + pid);

                        StopCapturePortsStatistics(data.statistics);
                        pidToData.TryRemove(pid, out data);

                    }

                }


            }


        }

        private readonly Queue<CapturePortsStatistics> dormantCapturePortsStatistics = new Queue<CapturePortsStatistics>();

        private CapturePortsStatistics GetCapturePortsStatistics()
        {
            if (dormantCapturePortsStatistics.Count > 0) return dormantCapturePortsStatistics.Dequeue();
            return new CapturePortsStatistics();
        }

        private void StopCapturePortsStatistics(CapturePortsStatistics stats)
        {
            stats.Stop();
            dormantCapturePortsStatistics.Enqueue(stats);
        }


        public void Stop()
        {
            foreach (var kvp in pidToData)
            {
                var stat = kvp.Value.statistics;
                if (stat != null)
                {
                    StopCapturePortsStatistics(stat);
                }
            }
            pidToData.Clear();
        }

        private readonly ConcurrentDictionary<int, PidData> pidToData = new ConcurrentDictionary<int, PidData>();
        private readonly ConcurrentDictionary<int, DateTime> pidRequestToLastTime = new ConcurrentDictionary<int, DateTime>();

        public long GetBytesSentLastSecondForPid(int pid)
        {
            pidRequestToLastTime[pid] = DateTime.Now;
            PidData data;
            if (pidToData.TryGetValue(pid, out data))
            {
                return data.lastBytesSentPerSecond;
            }
            else
            {
                return 0;
            }
        }

        private DateTime PIDtoPorts_lastTimeUpdated = DateTime.Now;
        private readonly Dictionary<int, List<ushort>> pidToPorts = new Dictionary<int, List<ushort>>();

        private void TryUpdatePIDToPorts()
        {
            if (PIDtoPorts_lastTimeUpdated + timespanToUpdatePidToPortsTable < DateTime.Now)
            {
                PIDtoPorts_lastTimeUpdated = DateTime.Now;

                pidToPorts.Clear();
                foreach (var row in GetAllConnections.GetAllTcpConnections())
                {
                    var PID = (int)row.PID;
                    List<ushort> ports;
                    if (!pidToPorts.TryGetValue(PID, out ports))
                    {
                        ports = new List<ushort>();
                        pidToPorts[PID] = ports;
                    }
                    if (!ports.Contains(row.LocalPort)) ports.Add(row.LocalPort);
                }
                foreach (var row in GetAllConnections.GetAllUdpConnections())
                {
                    var PID = (int)row.PID;
                    List<ushort> ports;
                    if (!pidToPorts.TryGetValue(PID, out ports))
                    {
                        ports = new List<ushort>();
                        pidToPorts[PID] = ports;
                    }
                    if (!ports.Contains(row.LocalPort)) ports.Add(row.LocalPort);
                }
            }
        }

    }
}