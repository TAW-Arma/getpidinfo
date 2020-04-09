using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace getpidinfo
{
    public class ProcessInfoManager
    {
        private static int countLogicalProcessors = 0;
        private readonly int numberOfSamplesToAverage;
        private readonly int secondsToKeepWatchingProcessWithNoRequests;

        public ProcessInfoManager(int numberOfSamplesToAverage, int secondsToKeepWatchingProcessWithNoRequests)
        {
            this.numberOfSamplesToAverage = numberOfSamplesToAverage;
            this.secondsToKeepWatchingProcessWithNoRequests = secondsToKeepWatchingProcessWithNoRequests;

            using (var searcher = new ManagementObjectSearcher("select MaxClockSpeed,NumberOfLogicalProcessors from Win32_Processor"))
            {
                foreach (var item in searcher.Get())
                {
                    var processors = int.Parse(item["NumberOfLogicalProcessors"].ToString());
                    countLogicalProcessors += processors;
                }
            }

        }

        public struct CpuMemoryUsageData
        {
            public double cpuUsage; // from 0 to 1, as 0 to 100% 
            public long memoryUsageBytes;
        }

        private readonly ConcurrentDictionary<int, DateTime> pidRequestToLastTime = new ConcurrentDictionary<int, DateTime>();
        private readonly ConcurrentQueue<int> addprocessToWatch = new ConcurrentQueue<int>();
        public CpuMemoryUsageData GetCpuMemoryUsageForPid(int pid)
        {
            pidRequestToLastTime[pid] = DateTime.Now;
            ProcessData pd;
            if (!pidToProcessData.TryGetValue(pid, out pd))
            {
                addprocessToWatch.Enqueue(pid);
                return new CpuMemoryUsageData() { cpuUsage = 0, memoryUsageBytes = 0 };
            }
            return new CpuMemoryUsageData() { cpuUsage = pd.cpuUsage, memoryUsageBytes = pd.memoryUsageBytes };
        }

        private readonly Dictionary<int, ProcessData> pidToProcessData = new Dictionary<int, ProcessData>();

        private class ProcessData
        {
            public double cpuUsage { get; private set; }
            public long memoryUsageBytes { get; private set; }

            private readonly Queue<double> cpuUsageHistory = new Queue<double>();
            private readonly Queue<long> memoryUsageBytesHistory = new Queue<long>();
            private TimeSpan lastTotalProcessorTime;
            private readonly Process process;
            public static ProcessData CreateProcessDataForPid(int pid)
            {
                var process = TimeCachedGetProcessByPid.Singleton.GetProcessForPid(pid);
                if (process == null) return null;
                if (process.HasExited) return null;
                else return new ProcessData(process);
            }
            private ProcessData(Process process)
            {
                this.process = process;

                memoryUsageBytesHistory.Enqueue(process.WorkingSet64);

                lastTotalProcessorTime = process.TotalProcessorTime;
            }
            public void Tick(double elapsedMiliseconds, int maxHistoryLen)
            {
                try
                {
                    // check if running
                    if (process != null && process.Id != 0 && !process.HasExited)
                    {
                        var timeUsedMiliseconds = (process.TotalProcessorTime - lastTotalProcessorTime).TotalMilliseconds; // total miliseconds of using any processor
                        var cpuUsageLast = timeUsedMiliseconds / (elapsedMiliseconds * countLogicalProcessors); // normalize it to per time unit per one logical processor

                        cpuUsageHistory.Enqueue(cpuUsageLast);
                        memoryUsageBytesHistory.Enqueue(process.WorkingSet64);

                        while (cpuUsageHistory.Count > maxHistoryLen) cpuUsageHistory.Dequeue();
                        while (memoryUsageBytesHistory.Count > maxHistoryLen) memoryUsageBytesHistory.Dequeue();

                        cpuUsage = cpuUsageHistory.Average();
                        memoryUsageBytes = (long)Math.Round(memoryUsageBytesHistory.Average());

                        lastTotalProcessorTime = process.TotalProcessorTime;
                    }
                    else
                    {
                        cpuUsage = 0;
                        memoryUsageBytes = 0;
                        lastTotalProcessorTime = new TimeSpan(0);
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    Debug.WriteLine("Error: Could not read " + process.Id);
                    cpuUsage = 0;
                    memoryUsageBytes = 0;
                    lastTotalProcessorTime = new TimeSpan(0);
                }
            }
        }

        private DateTime lastTickWasAt;
        public void Tick()
        {
            var elapsedMiliseconds = (DateTime.Now - lastTickWasAt).TotalMilliseconds;
            lastTickWasAt = DateTime.Now;

            // remove dead processes
            var timeUnderWhichRemovePidRequests = DateTime.Now - new TimeSpan(0, 0, secondsToKeepWatchingProcessWithNoRequests);
            foreach (var kvp in pidRequestToLastTime)
            {
                if (kvp.Value < timeUnderWhichRemovePidRequests)
                {
                    // Process has no requests
                    pidToProcessData.Remove(kvp.Key);
                }
            }
            pidRequestToLastTime.Clear();


            while (addprocessToWatch.Count > 0)
            {
                int pid;
                if (!addprocessToWatch.TryDequeue(out pid)) break; // no processes to add to watch
                var pd = ProcessData.CreateProcessDataForPid(pid);
                if (pd != null)
                {
                    // process is alive!
                    pidToProcessData[pid] = pd;
                }
                else
                {
                    // process is not running. Do not process
                }
            }

            // process data for processes
            foreach (var kvp in pidToProcessData)
            {
                var pd = kvp.Value;
                pd.Tick(elapsedMiliseconds, numberOfSamplesToAverage);
            }


        }

        public static bool IsRunning(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                if (proc != null && proc.Id != 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
