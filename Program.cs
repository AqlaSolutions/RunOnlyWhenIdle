using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RunOnlyWhenIdle
{
    class Program
    {
        static void Main(string[] args)
        {
            string exe = Path.GetFullPath(args[0]);
            string startArgs = "";
            if (args.Length > 2) startArgs = string.Join(" ", args.Skip(2).Select(EncodeParameterArgument));
            string startExe = args.Length > 1 ? args[1] : exe;

            Console.WriteLine("Waiting startup delay");

            Thread.Sleep(30000);

            bool wasStarted = FindProcesses(exe).Any();
            bool wasIdle = true;

            bool IsIdle() => GetTimeFromLastInput().TotalMinutes > 10;

            Console.WriteLine("Exe started: " + wasStarted);

            while (true)
            {
                bool isIdle = IsIdle();
                if (isIdle != wasIdle)
                {
                    wasIdle = isIdle;
                    if (isIdle)
                    {
                        Console.WriteLine("Now idle " + DateTime.UtcNow);
                        if (wasStarted)
                        {
                            if (!FindProcesses(exe).Any())
                            {
                                Process.Start(startExe, startArgs);
                                Thread.Sleep(10000);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Now active " + DateTime.UtcNow);

                        wasStarted = false;
                        foreach (var p in FindProcesses(exe))
                        {
                            wasStarted = true;
                            try
                            {
                                if (!p.CloseMainWindow() || !p.WaitForExit(20000))
                                    p.Kill();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                        }
                    }
                }

                Thread.Sleep(500);
            }
        }

        public static string EncodeParameterArgument(string original)
        {
            if (original == "")
                return "\"\"";
            string value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
            value = Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"");
            return value;
        }

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        internal struct LASTINPUTINFO 
        {
            public uint cbSize;

            public uint dwTime;
        }

        static TimeSpan GetTimeFromLastInput()
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint) Marshal.SizeOf(info);
            uint envTicks = (uint) Environment.TickCount;
            if (!GetLastInputInfo(ref info)) return TimeSpan.FromMilliseconds(envTicks);
            var ms = (long) envTicks - info.dwTime;
            return TimeSpan.FromMilliseconds(ms >= 0 ? ms : (long) uint.MaxValue - info.dwTime + envTicks);
        }

        // https://stackoverflow.com/questions/777548/how-do-i-determine-the-owner-of-a-process-in-c
        private static WindowsIdentity GetProcessUser(Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(process.Handle, 8, out processHandle)) return null;
                
                return new WindowsIdentity(processHandle);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }
        private static string GetProcessPath(Process process)
        {
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                processHandle = OpenProcess(0x1000, false, process.Id);
                if (processHandle == IntPtr.Zero) return null;
                
                const int lengthSb = 65535;

                var sb = new StringBuilder(lengthSb);

                string result = null;

                if (GetModuleFileNameEx(processHandle, IntPtr.Zero, sb, lengthSb) > 0)
                {
                    result = sb.ToString();
                }

                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    CloseHandle(processHandle);
                }
            }
        }

        static IEnumerable<Process> FindProcesses(string path)
        {
            var currentProcess = Process.GetCurrentProcess();
            var myIdentity = GetProcessUser(currentProcess);
            currentProcess.Dispose();
            Process[] processes = Process.GetProcesses();
            try
            {
                foreach (var p in processes)
                {
                    var s = GetProcessPath(p);
                    if (s == null) continue;

                    if (string.Equals(s, path, StringComparison.OrdinalIgnoreCase)
                        && GetProcessUser(p)?.User?.Value == myIdentity.User?.Value)
                    {
                        yield return p;
                    }
                }

            }
            finally
            {
                foreach (var p in processes)
                    p.Dispose();
            }
        }

        
        [DllImport("psapi.dll")]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]


        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);


    }
}
