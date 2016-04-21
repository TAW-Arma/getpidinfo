using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace getpidinfo
{
    public static class Console
    {
        public static void WriteLine()
        {
            System.Console.WriteLine();
        }
        public static void WriteLine(object msg)
        {
            System.Console.WriteLine("[" + TimeNow() + "] " + msg.ToString());
        }
        public static string TimeNow()
        {
            // https://msdn.microsoft.com/en-us/library/8kb3ddd4(v=vs.110).aspx
            return System.DateTime.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
