using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;
using System.Threading;
using SharpPcap;
using SharpPcap.WinPcap;
using PacketDotNet;
using System.Net;

namespace getpidinfo
{
    class ProcessInfoManager
    {


        static int countLogicalProcessors = 0;
        int numberOfSamplesToAverage;
        int secondsToKeepWatchingProcessWithNoRequests;

        public ProcessInfoManager(int numberOfSamplesToAverage, int secondsToKeepWatchingProcessWithNoRequests)
        {
            this.numberOfSamplesToAverage = numberOfSamplesToAverage;
            this.secondsToKeepWatchingProcessWithNoRequests = secondsToKeepWatchingProcessWithNoRequests;

            var searcher = new ManagementObjectSearcher("select MaxClockSpeed,NumberOfLogicalProcessors from Win32_Processor");
            foreach (var item in searcher.Get())
            {
                var processors = int.Parse(item["NumberOfLogicalProcessors"].ToString());
                countLogicalProcessors += processors;
            }

        }

        public struct CpuMemoryUsageData
        {
            public double cpuUsage; // from 0 to 1, as 0 to 100% 
            public long memoryUsageBytes;
        }

        ConcurrentDictionary<int, DateTime> pidRequestToLastTime = new ConcurrentDictionary<int, DateTime>();
        ConcurrentQueue<int> addprocessToWatch = new ConcurrentQueue<int>();
        public CpuMemoryUsageData GetCpuMemoryUsageForPid(int pid)
        {
            pidRequestToLastTime[pid] = DateTime.Now;
            ProcessData pd;
            if(!pidToProcessData.TryGetValue(pid, out pd))
            {
                addprocessToWatch.Enqueue(pid);
                return new CpuMemoryUsageData() { cpuUsage = 0, memoryUsageBytes = 0 };
            }
            return new CpuMemoryUsageData() { cpuUsage = pd.cpuUsage, memoryUsageBytes = pd.memoryUsageBytes };
        }

        Dictionary<int, ProcessData> pidToProcessData = new Dictionary<int, ProcessData>();



        class ProcessData
        {
            public double cpuUsage { get; private set; }
            public long memoryUsageBytes { get; private set; }

            Queue<double> cpuUsageHistory = new Queue<double>();
            Queue<long> memoryUsageBytesHistory = new Queue<long>();

            TimeSpan lastTotalProcessorTime;
            Process process;
            public static ProcessData CreateProcessDataForPid(int pid)
            {
                var process = TimeCachedGetProcessByPid.Singleton.GetProcessForPid(pid);
                if (process == null) return null;
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
        }



        DateTime lastTickWasAt;
        public void Tick()
        {
            var elapsedMiliseconds = (DateTime.Now - lastTickWasAt).TotalMilliseconds;
            lastTickWasAt = DateTime.Now;


            var timeUnderWhichRemovePidRequests = DateTime.Now - new TimeSpan(0, 0, secondsToKeepWatchingProcessWithNoRequests);
            foreach (var kvp in pidRequestToLastTime)
            {
                if(kvp.Value < timeUnderWhichRemovePidRequests)
                {
                    pidToProcessData.Remove(kvp.Key);
                }
            }
            pidRequestToLastTime.Clear();


            while (addprocessToWatch.Count > 0)
            {
                int pid;
                if (!addprocessToWatch.TryDequeue(out pid)) break;
                var pd = ProcessData.CreateProcessDataForPid(pid);
                if(pd != null)
                {
                    pidToProcessData[pid] = pd;
                }
                else
                {
                    //Console.WriteLine("Process with pid " + pid + " is not running");
                }
            }

            foreach (var kvp in pidToProcessData)
            {
                var pd = kvp.Value;
                pd.Tick(elapsedMiliseconds, numberOfSamplesToAverage);
            }


        }

    }
}
