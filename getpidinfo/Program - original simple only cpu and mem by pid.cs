using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Management;
using System.Windows.Forms;
using System.Threading;
using SharpPcap;
using SharpPcap.AirPcap;
using SharpPcap.LibPcap;
using SharpPcap.WinPcap;
using PacketDotNet;
using PacketDotNet.Tcp;
using PacketDotNet.Utils;

namespace GetProcessInfoByPID
{
    class Program
    {
        class ProcessData
        {
            public string PercentProcessorTime;
            public string WorkingSet { get { return workingSet.ToString(); } } // memory usage in bytes, same is shown by task mgr

            public Process process;
            public TimeSpan lastTotalProcessorTime;
            public long workingSet;
        }
        
        static void Main(string[] args)
        {

                        //args = new[] { "10284" };
            if (args.Length <= 0)
            {
                Console.WriteLine("err,number of requested PIDs in arguments is zero");
                return;
            }
            args = args[0].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length <= 0)
            {
                Console.WriteLine("err,number of requested PIDs in arguments is zero");
                return;
            }
            try
            {

                //float maxCpuSpeedMhz = 0;
                int countLogicalProcessors = 0;
                var searcher = new ManagementObjectSearcher("select MaxClockSpeed,NumberOfLogicalProcessors from Win32_Processor");
                foreach (var item in searcher.Get())
                {
                    var processors = int.Parse(item["NumberOfLogicalProcessors"].ToString());
                    countLogicalProcessors += processors;
                    //maxCpuSpeedMhz += float.Parse(item["MaxClockSpeed"].ToString()) * processors;
                }

                var data = new Dictionary<string, ProcessData>();

                foreach (var PID in args)
                {
                    try
                    {
                        int intPID;
                        if (!int.TryParse(PID.Trim(), out intPID)) continue;
                        var process = Process.GetProcessById(intPID);
                        var pd = new ProcessData()
                        {
                            process = process,
                            lastTotalProcessorTime = process.TotalProcessorTime,
                            workingSet = process.WorkingSet64,
                        };
                        data[PID] = pd;
                    }
                    catch
                    {

                    }
                }

                var sleepMiliseconds = 200;
                Thread.Sleep(sleepMiliseconds);
                
                foreach (var kvp in data)
                {
                    var pd = kvp.Value;
                    var timeUsedMiliseconds = (pd.process.TotalProcessorTime - pd.lastTotalProcessorTime).TotalMilliseconds;
                    //var cpuCanProcessThisMiliseconds = (1.0 / (maxCpuSpeedMhz * 1000.0 * 1000.0)) * sleepMiliseconds;
                    var cpuUsage = timeUsedMiliseconds / (sleepMiliseconds * countLogicalProcessors);
                    pd.PercentProcessorTime = Math.Round(cpuUsage * 100.0).ToString();
                }

                foreach (var PID in args)
                {
                    ProcessData processData;
                    if (data.TryGetValue(PID, out processData))
                    {
                        Console.WriteLine("{0},{1},{2}", PID, processData.PercentProcessorTime, processData.WorkingSet);
                    }
                    else
                    {
                        Console.WriteLine("{0},0,0", PID);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("err," + e.ToString());
            }
                
        }
        
    }
}