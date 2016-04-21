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

        static Dictionary<ushort, long> portToTraffic = new Dictionary<ushort, long>();
        static void AddTraffic(ushort port, long bytesNum)
        {
            long t = 0;
            portToTraffic.TryGetValue(port, out t);
            portToTraffic[port] = t + bytesNum;
        }

        static void OnPacketArrival(object sender, CaptureEventArgs e)
        {
            var d = new ByteArraySegment(e.Packet.Data);
            var udp = new UdpPacket(d);
            if (udp != null)
            {
                AddTraffic(udp.SourcePort, e.Packet.Data.LongLength);
                AddTraffic(udp.DestinationPort, e.Packet.Data.LongLength);
                /*
                DateTime time = e.Packet.Timeval.Date;
                int len = e.Packet.Data.Length;

                string srcIp = udp.SourcePort.ToString();
                string dstIp = udp.DestinationPort.ToString();

                Console.WriteLine("{0}:{1}  {2}->{3}  Len={4}",
                    time.Minute, time.Second,
                    srcIp, dstIp, len);*/
                //Console.WriteLine(e.Packet.ToString());
            }

            /*DateTime time = e.Packet.Timeval.Date;
            int len = e.Packet.Data.Length;
            Console.WriteLine("{0}    {1}:{2}:{3},{4} Len={5}",
                e.Device.Name, time.Hour, time.Minute, time.Second, time.Millisecond, len);*/
            
        }
        static void Main(string[] args)
        {

            var devices = CaptureDeviceList.Instance;

            foreach (ICaptureDevice dev in devices)
            {
                Console.WriteLine("{0}\n", dev.ToString());
                dev.Open(DeviceMode.Normal);
                dev.Filter = "udp or tcp";
                dev.OnPacketArrival += OnPacketArrival;
                dev.StartCapture();
            }


            Console.WriteLine("START");
            Console.ReadKey();
            Console.WriteLine("stopping");

            var pcapStop = new Stopwatch();
            pcapStop.Start();
            foreach (ICaptureDevice dev in devices)
            {
                dev.StopCapture();
                dev.Close();
            }
            pcapStop.Stop();

            Console.WriteLine("Pcap stopped in " + pcapStop.ElapsedMilliseconds+ " ms");

            foreach(var kvp in portToTraffic)
            {
                Console.WriteLine("port {0} got traffic of {1} bytes", kvp.Key, kvp.Value);
            }

            Console.WriteLine("stopped");
            Console.ReadKey();

            return;

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