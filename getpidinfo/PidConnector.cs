using System;
using System.Collections.Generic;
using System.Threading;

namespace getpidinfo
{
    public class PidConnector
    {
        private const int updateProcessInfoEveryMiliseconds = 200;
        private const int numberOfProcessInfoSamplesToAverage = 5;
        private const int updateNetworkInfoEveryMiliseconds = 200;
        private const int secondsToKeepNetworkSamples = 2;
        private const int secondsToKeepWatchingProcessWithNoRequests = 10;
        private const int secondsToClosePIDPortsWithNoRequests = 60;
        private const int secondsToUpdatePidToPortsTable = 20;

        private readonly ProcessInfoManager processInfoManager;
        private readonly PortStatisticsManager portStatisticsManager;

        private readonly Thread portStatisticsManagerThread;
        private readonly Thread processInfoThread;

        // spawn threads
        private void PortStatisticsManagerThread()
        {
            while (Thread.CurrentThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(updateNetworkInfoEveryMiliseconds);
                portStatisticsManager.Tick();
            }
        }
        private void ProcessInfoThread()
        {
            while (Thread.CurrentThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(updateProcessInfoEveryMiliseconds);
                processInfoManager.Tick();
            }
        }

        public PidConnector()
        {
            portStatisticsManager = new PortStatisticsManager(secondsToKeepNetworkSamples, secondsToClosePIDPortsWithNoRequests, secondsToUpdatePidToPortsTable);
            portStatisticsManagerThread = new Thread(PortStatisticsManagerThread)
            {
                Name = "portStatisticsManagerThread",
                IsBackground = true,
                CurrentCulture = System.Globalization.CultureInfo.InvariantCulture
            };
            portStatisticsManagerThread.Start();

            processInfoManager = new ProcessInfoManager(numberOfProcessInfoSamplesToAverage, secondsToKeepWatchingProcessWithNoRequests);
            processInfoThread = new Thread(ProcessInfoThread)
            {
                Name = "processInfoThread",
                IsBackground = true,
                CurrentCulture = System.Globalization.CultureInfo.InvariantCulture
            };
            processInfoThread.Start();

        }

        public void Stop()
        {
            if (processInfoThread != null) processInfoThread.Abort();

            if (portStatisticsManagerThread != null) portStatisticsManagerThread.Abort();
            if (portStatisticsManager != null) portStatisticsManager.Stop();
        }

        // returns pid info from pid array stored in class
        public PidInfoData[] QueryMultipleProcesses(List<int> pids)
        {
            if (pids is null)
            {
                throw new ArgumentNullException(nameof(pids));
            }

            var data = new List<PidInfoData>();
            for (int i = 0; i < pids.Count; i++)
            {
                int pid = pids[i];
                var cpuMemory = processInfoManager.GetCpuMemoryUsageForPid(pid);
                var network = portStatisticsManager.GetBytesSentLastSecondForPid(pid);
                data.Add(new PidInfoData(cpuMemory, network));
            };

            return data.ToArray();
        }

        public PidInfoData QueryProcess(int pid)
        {
            if (!ProcessInfoManager.IsRunning(pid))
            {
                // PID not running
                return new PidInfoData();
            }
            else
            {
                var cpuMemory = processInfoManager.GetCpuMemoryUsageForPid(pid);
                var network = portStatisticsManager.GetBytesSentLastSecondForPid(pid);
                return new PidInfoData(cpuMemory, network);
            }
        }

    }

    // json structured data sent to backend
    public class PidInfoData
    {
        public class Mem
        {
            public decimal percentage { get; set; } = 0;
            public long bytes { get; set; } = 0;
        }
        public class Cpu
        {
            public int percentage { get; set; } = 0;
        }
        public Cpu cpu { get; set; } = new Cpu();
        public Mem mem { get; set; } = new Mem();
        public long net { get; set; } = 0;
        public PidInfoData()
        {
            cpu = new Cpu();
            mem = new Mem();
        }
        public PidInfoData(ProcessInfoManager.CpuMemoryUsageData cpuMemory, long network)
        {
            cpu = new Cpu() { percentage = (int)Math.Round(cpuMemory.cpuUsage * 100.0) };
            mem = new Mem() { bytes = cpuMemory.memoryUsageBytes };
            this.net = network;
        }
    }
}
