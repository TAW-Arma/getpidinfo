using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace getpidinfo
{
    public class TimeCachedGetProcessByPid
    {
        private static TimeCachedGetProcessByPid singleton;
        public static TimeCachedGetProcessByPid Singleton
        {
            get
            {
                if (singleton == null)
                {
                    singleton = new TimeCachedGetProcessByPid(new TimeSpan(0, 0, 2));
                }
                return singleton;
            }
        }

        private struct Data
        {
            public Process process;
        }

        private readonly Dictionary<int, DateTime> pidToLastTime = new Dictionary<int, DateTime>();
        private readonly Dictionary<int, Data> pidToLastProcessInstance = new Dictionary<int, Data>();
        private TimeSpan timespanToRefrash;

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
