// from http://www.pinvoke.net/default.aspx/iphlpapi.getextendedtcptable

using System;
using System.Runtime.InteropServices;

namespace getpidinfo
{
    public static partial class GetAllConnections
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPROW_OWNER_PID
        {
            // DWORD is System.UInt32 in C#
            private readonly uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            private readonly byte[] localPort;
            private readonly uint owningPid;

            public uint PID
            {
                get
                {
                    return owningPid;
                }
            }

            public System.Net.IPAddress LocalAddress
            {
                get
                {
                    return new System.Net.IPAddress(localAddr);
                }
            }

            public ushort LocalPort
            {
                get
                {
                    return BitConverter.ToUInt16(
                    new byte[2] { localPort[1], localPort[0] }, 0);
                }
            }

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            private readonly MIB_UDPROW_OWNER_PID table;
        }

        public enum UDP_TABLE_CLASS
        {
            UDP_TABLE_BASIC,
            UDP_TABLE_OWNER_PID,
            UDP_TABLE_OWNER_MODULE
        }

        private static DateTime getAllUdpConnections_cached_lastTime;
        private static MIB_UDPROW_OWNER_PID[] getAllUdpConnections_cached;
        //public TcpRow[] GetAllTcpConnections()        
        public static MIB_UDPROW_OWNER_PID[] GetAllUdpConnections()
        {
            if (getAllUdpConnections_cached == null || getAllUdpConnections_cached_lastTime + new TimeSpan(0, 0, 1) < DateTime.Now)
            {

                MIB_UDPROW_OWNER_PID[] tTable;
                int AF_INET = 2;    // IP_v4
                int buffSize = 0;

                // how much memory do we need?
                uint ret = NativeMethods.GetExtendedUdpTable(IntPtr.Zero, ref buffSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
                IntPtr buffTable = Marshal.AllocHGlobal(buffSize);

                try
                {
                    ret = NativeMethods.GetExtendedUdpTable(buffTable, ref buffSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
                    if (ret != 0)
                    {
                        return new MIB_UDPROW_OWNER_PID[0];
                    }

                    // get the number of entries in the table
                    //MibTcpTable tab = (MibTcpTable)Marshal.PtrToStructure(buffTable, typeof(MibTcpTable));
                    MIB_UDPTABLE_OWNER_PID tab = (MIB_UDPTABLE_OWNER_PID)Marshal.PtrToStructure(buffTable, typeof(MIB_UDPTABLE_OWNER_PID));
                    //IntPtr rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.numberOfEntries) );
                    IntPtr rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.dwNumEntries));
                    // buffer we will be returning
                    //tTable = new TcpRow[tab.numberOfEntries];
                    tTable = new MIB_UDPROW_OWNER_PID[tab.dwNumEntries];

                    //for (int i = 0; i < tab.numberOfEntries; i++)        
                    for (int i = 0; i < tab.dwNumEntries; i++)
                    {
                        //MibTcpRow_Owner_Pid tcpRow = (MibTcpRow_Owner_Pid)Marshal.PtrToStructure(rowPtr, typeof(MibTcpRow_Owner_Pid));
                        MIB_UDPROW_OWNER_PID tcpRow = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDPROW_OWNER_PID));
                        //tTable[i] = new TcpRow(tcpRow);
                        tTable[i] = tcpRow;
                        rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(tcpRow));   // next entry
                    }

                }
                finally
                {
                    // Free the Memory
                    Marshal.FreeHGlobal(buffTable);
                }


                getAllUdpConnections_cached = tTable;
                getAllUdpConnections_cached_lastTime = DateTime.Now;
            }

            return getAllUdpConnections_cached;
        }

        // Usage:
        // MIB_TCPROW_OWNER_PID[] tcpState = GetAllTcpConnections();
        // Console.WriteLine("Local\tRemote\tState\tPID");
        // foreach (MIB_TCPROW_OWNER_PID mib in tcpState)
        // {
        //   Console.WriteLine("{0}:{1}\t{2}:{3}\t{4}\t{5}",mib.LocalAddress,mib.LocalPort,mib.RemoteAddress,mib.RemotePort,mib.state,mib.owningPid);
        // }
    }
}
