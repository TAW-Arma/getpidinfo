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
using System.Net;

namespace getpidinfo
{
    public class TimeCachedGetProcessByPid
    {
        static TimeCachedGetProcessByPid singleton;
        public static TimeCachedGetProcessByPid Singleton
        {
            get
            {
                if(singleton == null)
                {
                    singleton = new TimeCachedGetProcessByPid(new TimeSpan(0, 0, 2));
                }
                return singleton;
            }
        }

        struct Data
        {
            public Process process;
        }

        Dictionary<int, DateTime> pidToLastTime = new Dictionary<int, DateTime>();
        Dictionary<int, Data> pidToLastProcessInstance = new Dictionary<int, Data>();
        TimeSpan timespanToRefrash;

        public TimeCachedGetProcessByPid(TimeSpan timespanToRefrash)
        {
            this.timespanToRefrash = timespanToRefrash;
        }
        public Process GetProcessForPid(int pid)
        {
            DateTime lastTime;
            if (pidToLastTime.TryGetValue(pid, out lastTime))
            {
                if (lastTime + timespanToRefrash > DateTime.Now)
                {
                    return pidToLastProcessInstance[pid].process;
                }
            }

            Process process = null;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch
            {

            }

            pidToLastTime[pid] = DateTime.Now;
            pidToLastProcessInstance[pid] = new Data() { process = process };

            return process;
        }
    }

}
