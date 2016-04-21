// from http://www.pinvoke.net/default.aspx/iphlpapi.getextendedtcptable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace getpidinfo
{
    public static partial class GetAllConnections
    {

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, TCP_TABLE_CLASS tblClass, uint reserved = 0);


        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            // DWORD is System.UInt32 in C#
            System.UInt32 state;
            System.UInt32 localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            byte[] localPort;
            System.UInt32 remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            byte[] remotePort;
            System.UInt32 owningPid;

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

            public System.Net.IPAddress RemoteAddress
            {
                get
                {
                    return new System.Net.IPAddress(remoteAddr);
                }
            }

            public ushort RemotePort
            {
                get
                {
                    return BitConverter.ToUInt16(
                    new byte[2] { remotePort[1], remotePort[0] }, 0);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            MIB_TCPROW_OWNER_PID table;
        }

        enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }



        static DateTime getAllTcpConnections_cached_lastTime;
        static MIB_TCPROW_OWNER_PID[] getAllTcpConnections_cached;
        //public TcpRow[] GetAllTcpConnections()
        public static MIB_TCPROW_OWNER_PID[] GetAllTcpConnections()
        {
            if (getAllTcpConnections_cached == null || getAllTcpConnections_cached_lastTime + new TimeSpan(0, 0, 1) < DateTime.Now)
            {
                //  TcpRow is my own class to display returned rows in a nice manner.
                //    TcpRow[] tTable;
                MIB_TCPROW_OWNER_PID[] tTable;
                int AF_INET = 2;    // IP_v4
                int buffSize = 0;

                // how much memory do we need?
                uint ret = GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);
                IntPtr buffTable = Marshal.AllocHGlobal(buffSize);

                try
                {
                    ret = GetExtendedTcpTable(buffTable, ref buffSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);
                    if (ret != 0)
                    {
                        return new MIB_TCPROW_OWNER_PID[0];
                    }

                    // get the number of entries in the table
                    //MibTcpTable tab = (MibTcpTable)Marshal.PtrToStructure(buffTable, typeof(MibTcpTable));
                    MIB_TCPTABLE_OWNER_PID tab = (MIB_TCPTABLE_OWNER_PID)Marshal.PtrToStructure(buffTable, typeof(MIB_TCPTABLE_OWNER_PID));
                    //IntPtr rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.numberOfEntries) );
                    IntPtr rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.dwNumEntries));
                    // buffer we will be returning
                    //tTable = new TcpRow[tab.numberOfEntries];
                    tTable = new MIB_TCPROW_OWNER_PID[tab.dwNumEntries];

                    //for (int i = 0; i < tab.numberOfEntries; i++)        
                    for (int i = 0; i < tab.dwNumEntries; i++)
                    {
                        //MibTcpRow_Owner_Pid tcpRow = (MibTcpRow_Owner_Pid)Marshal.PtrToStructure(rowPtr, typeof(MibTcpRow_Owner_Pid));
                        MIB_TCPROW_OWNER_PID tcpRow = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
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


                getAllTcpConnections_cached = tTable;
                getAllTcpConnections_cached_lastTime = DateTime.Now;
            }

            return getAllTcpConnections_cached;
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
