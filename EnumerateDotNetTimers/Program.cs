using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;


namespace ClrMdHarness
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine("ERROR - Invalid args");
            Console.WriteLine();
            Console.WriteLine("EnumerateDotNetTimers.exe --all | --non-microsoft-only");
            Console.WriteLine();
            Console.WriteLine("--all\t\t\tEnumerate all timers");
            Console.WriteLine("--non-microsoft-only\tEnumerate only timers with non-System and non-Microsoft namespaces");
        }
        static void Main(string[] args)
        {
            bool enumerateAll = true;

            if (args.Length < 1)
            {
                Usage();
                return;
            }
            else if (args[0] == "--all")
            {
                enumerateAll = true;
            }
            else if (args[0] == "--non-microsoft-only")
            {
                enumerateAll = false;
            }
            else
            {
                Usage();
                return;
            }

            Console.WriteLine("Timers\n============");

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    using (DataTarget target = DataTarget.AttachToProcess(process.Id, 1000, AttachFlag.Passive))
                    {
                        // First, loop through each Clr in the process (there may be multiple in the side-by-side scenario).
                        foreach (ClrInfo clrVersion in target.ClrVersions)
                        {
                            ClrRuntime runtime = clrVersion.CreateRuntime();
                            Timers t = new Timers();
                            var timers = t.EnumerateTimers(runtime);
                            foreach (var timer in timers)
                            {
                                if (!timer.MethodName.StartsWith("System") && !timer.MethodName.StartsWith("Microsoft"))
                                {
                                    Console.WriteLine("[NON-MICROSOFT] - {0}:{1} - {2}, {3}, {4:X}, {5:X}, {6}", process.ProcessName, process.Id, timer.DueTime.ToString(), timer.StateTypeName, timer.ThisAddress, timer.MethodAddress, timer.MethodName);
                                }
                                else if (enumerateAll)
                                {
                                    Console.WriteLine("[MICROSOFT] - {0}:{1} - {2}, {3}, {4:X}, {5:X}, {6}", process.ProcessName, process.Id, timer.DueTime.ToString(), timer.StateTypeName, timer.ThisAddress, timer.MethodAddress, timer.MethodName);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Console.WriteLine(e);
                }
            }
        }
    }
}
