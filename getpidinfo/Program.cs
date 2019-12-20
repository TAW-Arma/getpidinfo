using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Management;
using System.Windows.Forms;
using System.Threading;
using SharpPcap;
using SharpPcap.WinPcap;
using PacketDotNet;
using System.Net;
using System.Runtime.InteropServices;

namespace getpidinfo
{
    class Program
    {

        static void Main(string[] args)
        {
            new Program(args);
        }


        int serverPort = 2020;
        int updateProcessInfoEveryMiliseconds = 200;
        int numberOfProcessInfoSamplesToAverage = 5;
        int updateNetworkInfoEveryMiliseconds = 200;
        int secondsToKeepNetworkSamples = 2;
        int secondsToKeepWatchingProcessWithNoRequests = 10;
        int secondsToClosePIDPortsWithNoRequests = 60;
        int secondsToUpdatePidToPortsTable = 20;

        string[] args;
        public Program(string[] args)
        {

            var myExePath = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
            var myExeFileName = Path.GetFileName(myExePath);
            var cfgFile = myExeFileName + ".config";
            //var cfg = new XMLConfig(cfgFile);


            try
            {
                this.args = args;
                if (args.Length >= 1 && (args[0] == "help" || args[0] == "?")) printHelp = true;

                Param("serverPort");
                Param("updateProcessInfoEveryMiliseconds");
                Param("numberOfProcessInfoSamplesToAverage");
                Param("updateNetworkInfoEveryMiliseconds");
                Param("secondsToKeepNetworkInfoSamples");
                Param("secondsToKeepWatchingProcessWithNoRequests");
                Param("secondsToClosePIDPortsWithNoRequests");
                Param("secondsToUpdatePidToPortsTable");
                

                if (printHelp)
                {
                    PrintGeneralHelp();
                }
                if (!printHelp && allPassed)
                {
                    Console.WriteLine("Starting ...");
                    Start();
                    Console.WriteLine("Started");

                    AppDomain.CurrentDomain.ProcessExit += (sender, a) =>
                    {
                        Stop();
                    };

                    var handler = new ConsoleEventDelegate((int eventType) =>
                    {
                        if (eventType == 2)
                        {
                            Stop();
                        }
                        return true;
                    });
                    SetConsoleCtrlHandler(handler, true);

                    //Console.WriteLine("Press enter to quit ...");
                    while (Thread.CurrentThread.ThreadState == ThreadState.Running) Thread.Sleep(100);

                    Console.WriteLine("Stopping, this may take a while ...");
                    Stop();
                    Console.WriteLine("Stopped");

                }
            }
            catch(Exception e)
            {
                PrintGeneralHelp();
                Console.WriteLine();
                Console.WriteLine(e);
            }

        }


        // its pain in the ass to detect close in C#
        //http://stackoverflow.com/questions/4646827/on-exit-for-a-console-application
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);


        void PrintGeneralHelp()
        {
            Console.WriteLine("Start with help to get arguments description");
            Console.WriteLine("This application must run as administrator");
            Console.WriteLine("Request can be any http method");
            Console.WriteLine("You need to provide both pid and port you want stats for");
            Console.WriteLine("Request format is: localhost:port/?pid1,pid,pid3,....");
        }


        bool allPassed = true;
        bool printHelp = false;
        int paramIndex = 0;
        void Param(string name, bool required = false)
        {
            var f = this.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            var def = (int)f.GetValue(this);
            if (printHelp)
            {
                allPassed = false;
                Console.WriteLine("[" + name + "=" + def + "]");
            }
            else
            {
                if (paramIndex < args.Length)
                {
                    var val = int.Parse(args[paramIndex]);
                    Console.WriteLine(name + " = " + val);
                    f.SetValue(this, val);
                }
                else if (required)
                {
                    allPassed = false;
                    Console.WriteLine("missing required argument: " + name + " default value = " + def);
                    PrintGeneralHelp();
                }
                else
                {
                    Console.WriteLine(name + " = " + def);
                }
            }
            paramIndex++;
        }

        


        void Start()
        {
            portStatisticsManagerThread = new Thread(PortStatisticsManagerThread);
            portStatisticsManagerThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            portStatisticsManagerThread.Start();

            processInfoThread = new Thread(ProcessInfoThread);
            processInfoThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            processInfoThread.Start();

            serverThread = new Thread(HttpServerThread);
            serverThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            serverThread.Start();
        }

        void Stop()
        {
            if (httpListener != null) httpListener.Stop();

            if (serverThread != null) serverThread.Abort();

            if (processInfoThread != null) processInfoThread.Abort();

            if (portStatisticsManagerThread != null) portStatisticsManagerThread.Abort();
            if (portStatisticsManager != null) portStatisticsManager.Stop();
        }


        Thread portStatisticsManagerThread;
        PortStatisticsManager portStatisticsManager;
        void PortStatisticsManagerThread()
        {
            portStatisticsManager = new PortStatisticsManager(secondsToKeepNetworkSamples, secondsToClosePIDPortsWithNoRequests, secondsToUpdatePidToPortsTable);
            while (Thread.CurrentThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(updateNetworkInfoEveryMiliseconds);
                portStatisticsManager.Tick();
            }
        }


        Thread processInfoThread;
        ProcessInfoManager processInfoManager;
        void ProcessInfoThread()
        {
            processInfoManager = new ProcessInfoManager(numberOfProcessInfoSamplesToAverage, secondsToKeepWatchingProcessWithNoRequests);
            while (Thread.CurrentThread.ThreadState == ThreadState.Running)
            {
                Thread.Sleep(updateProcessInfoEveryMiliseconds);
                processInfoManager.Tick();
            }
        }




        Thread serverThread;
        HttpListener httpListener;
        void HttpServerThread()
        {
            if (!HttpListener.IsSupported)
            {
                Console.WriteLine("HttpListener is not supported");
                return;
            }
            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://*:" + serverPort + "/");
                httpListener.Start();
            }
            catch (System.Net.HttpListenerException e)
            {
                if (e.ErrorCode == 5)
                {
                    Console.WriteLine("Failed to start server listener on: " + serverPort + " reason: This program requires administrator privileges. Please run as administrator.");
                } else
                {
                    Console.WriteLine("Failed to start server listener on: " + serverPort + " reason: " + e);
                }
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start server listener on: " + serverPort + " reason: " + e);
                return;
            }
            HttpListenerContext context;
            while (Thread.CurrentThread.ThreadState == ThreadState.Running)
            {
                context = null;
                try
                {
                    context = httpListener.GetContext();
                    if (ProcessContext(context))
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
                catch (Exception e)
                {
                    if (context != null) context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    Console.WriteLine(e);
                }
                finally
                {
                    if (context != null)
                    {
                        Console.WriteLine(context.Request.HttpMethod + " " + context.Request.RawUrl + " " + ((HttpStatusCode)context.Response.StatusCode).ToString());
                        context.Response.Close();
                        //Console.WriteLine("closed");
                    }
                }
            }
        }

        bool ProcessContext(HttpListenerContext context)
        {
            // localhost:port/?pid1=port1,pid2=port2,pid3=port3, ....
            var q = context.Request.RawUrl;

            if (q.StartsWith("/")) q = q.Substring(1);
            if (q.StartsWith("?")) q = q.Substring(1);            

            var parts = q.Split(',');

            var binaryStream = new MemoryStream();
            var o = new StreamWriter(binaryStream);
            
            int pid;
            int cpu;
            long memory;
            float network;

            o.WriteLine("{");
            for(int i=0; i<parts.Length; i++)
            {
                var part = parts[i];
                var pidPort = part.Split('=');
                if (!int.TryParse(pidPort[0], out pid)) return false;
                if (pid == 0)
                {
                    cpu = 0;
                    memory = 0;
                    network = 0;
                }
                else
                {
                    var cpuMemory = processInfoManager.GetCpuMemoryUsageForPid(pid);
                    cpu = (int)Math.Round(cpuMemory.cpuUsage * 100.0);
                    memory = cpuMemory.memoryUsageBytes;
                    network = portStatisticsManager.GetBytesSentLastSecondForPid(pid);
                }
                o.Write("\t\"{0}\":{{ \"pid\":{0}, \"cpu\":{1}, \"memory\":{2}, \"network\":{3} }}", 
                    pid,
                    cpu,
                    memory,
                    network
                );

                if (i < parts.Length - 1) o.Write(",");
                o.WriteLine();
            }
            o.Write("}");

            o.Flush();              

            context.Response.AddHeader("Date", DateTime.Now.ToString("r"));
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = binaryStream.Length;
            context.Response.OutputStream.Write(binaryStream.GetBuffer(), 0, (int)binaryStream.Length);
            context.Response.OutputStream.Close();

            return true;
        }









    }
}