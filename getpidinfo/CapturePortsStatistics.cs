using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;
using SharpPcap.WinPcap;
using PacketDotNet;

namespace getpidinfo
{
    public class CapturePortsStatistics
    {

        static int nextGlobalId = 0;

        struct Data
        {
            public long bytes;
            public DateTime dateTime;
        }
        ConcurrentQueue<Data> receviedBytes = new ConcurrentQueue<Data>();

        WinPcapDeviceList devices;
        List<ushort> listeningToPorts = new List<ushort>();
        bool hasStarted = false;
        int myId;

        public CapturePortsStatistics()
        {
            myId = nextGlobalId;
            nextGlobalId++;
        }

        public void CaptureThesePorts(ushort[] ports)
        {
            if (ports == null) return;

            int portsNumChanged = 0;

            // add ports we are not listening to, but want to listen to
            foreach (var port in ports)
            {
                if (listeningToPorts.Contains(port) == false)
                {
                    listeningToPorts.Add(port);
                    portsNumChanged++;
                }
            }

            // remov extra port we are listening to but dont want to listen to
            List<ushort> portsToRemove = new List<ushort>();
            foreach (var port in listeningToPorts)
            {
                if (ports.Contains(port) == false)
                {
                    portsToRemove.Add(port);
                    portsNumChanged++;
                }
            }
            for (int i = 0; i < portsToRemove.Count; i++)
            {
                listeningToPorts.Remove(portsToRemove[i]);
            }

            if (portsNumChanged > 0)
            {
                if (listeningToPorts.Count > 0)
                {
                    if (hasStarted) Restart();
                    else Start();
                }
                else
                {
                    Stop();
                }
            }
        }

        public long GetAverageOfReceviedBytes()
        {
            if (receviedBytes.Count == 0) return 0;
            return (long)Math.Round(receviedBytes.Select(r => r.bytes).Average());
        }
        public void ClampData(DateTime startDataAtThisTime)
        {
            if (receviedBytes.Count == 0) return;
            while (receviedBytes.Count > 0)
            {
                Data data;
                if (!receviedBytes.TryPeek(out data)) break;
                if (data.dateTime > startDataAtThisTime) break;
                if (!receviedBytes.TryDequeue(out data)) break;
            }
        }

        void OnPcapStatistics(object sender, StatisticsModeEventArgs e)
        {
            // Console.WriteLine(port + " " + e.Statistics.RecievedBytes);
            receviedBytes.Enqueue(
                new Data()
                {
                    bytes = e.Statistics.RecievedBytes,
                    dateTime = DateTime.Now,
                }
            );
        }

        string GetStringPorts()
        {
            return string.Join(",", listeningToPorts.ToArray());
        }

        string GetPortsFilter()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < listeningToPorts.Count; i++)
            {
                if (i > 0) sb.Append(" or ");
                sb.Append("port " + listeningToPorts[i]);
            }
            return sb.ToString();
        }


        void Restart()
        {
            var ports = GetStringPorts();
            Console.WriteLine("Restarting listener id:" + myId + " on ports:" + ports + " ...");
            foreach (WinPcapDevice dev in devices)
            {
                dev.StopCapture();
                dev.Filter = GetPortsFilter();
                dev.StartCapture();
            }
            Console.WriteLine("Restarted listener id:" + myId + " on ports:" + ports);
        }

        void Start()
        {
            if (listeningToPorts.Count <= 0) return;
            if (devices == null) devices = WinPcapDeviceList.New();

            var ports = GetStringPorts();
            Console.WriteLine("Starting listener id:" + myId + " on ports:" + ports + " ...");
            foreach (WinPcapDevice dev in devices)
            {
                dev.Open(DeviceMode.Normal);
                dev.Mode = CaptureMode.Statistics;
                dev.Filter = GetPortsFilter();
                dev.OnPcapStatistics += OnPcapStatistics;
                dev.StartCapture();
            }
            Console.WriteLine("Started listener id:" + myId + " on ports:" + ports);
            hasStarted = true;
        }

        public void Stop()
        {
            if (devices == null) return;

            var ports = GetStringPorts();
            Console.WriteLine("Stopping listener id:" + myId + " on ports:" + ports + " ...");
            foreach (WinPcapDevice dev in devices)
            {
                dev.OnPcapStatistics -= OnPcapStatistics;
                dev.StopCapture();
                dev.Close();
            }
            Console.WriteLine("Stopped listener id:" + myId + " on ports:" + ports);
            hasStarted = false;
        }

    }

}