using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;


namespace ClrMdHarness
{
    class Timers
    {
        public class TimerInfo
        {
            public ulong TimerQueueTimerAddress { get; set; }
            public uint DueTime { get; set; }
            public uint Period { get; set; }
            public bool Cancelled { get; set; }
            public ulong StateAddress { get; set; }
            public string StateTypeName { get; set; }
            public ulong ThisAddress { get; set; }
            public string MethodName { get; set; }
            public ulong MethodAddress { get; set; }
        }

        // from threadpool.cs in https://github.com/Microsoft/clrmd/tree/master/src/Microsoft.Diagnostics.Runtime/Desktop
        public ClrModule GetMscorlib(ClrRuntime runtime)
        {
            foreach (ClrModule module in runtime.Modules)
                if (module.AssemblyName.Contains("mscorlib.dll"))
                    return module;

            // Uh oh, this shouldn't have happened.  Let's look more carefully (slowly).
            foreach (ClrModule module in runtime.Modules)
                if (module.AssemblyName.ToLower().Contains("mscorlib"))
                    return module;

            // Ok...not sure why we couldn't find it.
            return null;
        }

        private object GetFieldValue(ClrHeap heap, ulong address, string fieldName)
        {
            var type = heap.GetObjectType(address);
            ClrInstanceField field = type.GetFieldByName(fieldName);
            if (field == null)
                return null;

            return field.GetValue(address);
        }

        private string BuildTimerCallbackMethodName(ClrRuntime runtime, ulong timerCallbackRef, string methodPtrString)
        {
            var heap = runtime.Heap;
            var methodPtr = GetFieldValue(heap, timerCallbackRef, methodPtrString);
            if (methodPtr != null)
            {
                ClrMethod method = runtime.GetMethodByAddress((ulong)(long)methodPtr);
                if (method != null)
                {
                    // look for "this" to figure out the real callback implementor type
                    string thisTypeName = "?";
                    var thisPtr = GetFieldValue(heap, timerCallbackRef, "_target");
                    if ((thisPtr != null) && ((ulong)thisPtr) != 0)
                    {
                        ulong thisRef = (ulong)thisPtr;
                        var thisType = heap.GetObjectType(thisRef);
                        if (thisType != null)
                        {
                            thisTypeName = thisType.Name;
                        }
                    }
                    else
                    {
                        thisTypeName = (method.Type != null) ? method.Type.Name : "?";
                    }
                    return method.GetFullSignature();
                    return string.Format("{0}.{1}", thisTypeName, method.Name);
                }
                else
                {
                    return "";
                }
            }
            else
            {
                return "";
            }
        }

        public IEnumerable<TimerInfo> EnumerateTimers(ClrRuntime runtime)
        {
            ClrHeap heap = runtime.Heap;
            if (!heap.CanWalkHeap)
                yield break;

            var timerQueueType = GetMscorlib(runtime).GetTypeByName("System.Threading.TimerQueue");
            if (timerQueueType == null)
                yield break;

            ClrStaticField staticField = timerQueueType.GetStaticFieldByName("s_queue");
            if (staticField == null)
                yield break;

            foreach (ClrAppDomain domain in runtime.AppDomains)
            {
                ulong? timerQueue = (ulong?)staticField.GetValue(domain);
                if (!timerQueue.HasValue || timerQueue.Value == 0)
                    continue;

                // m_timers is the start of the list of TimerQueueTimer
                var currentPointer = GetFieldValue(heap, timerQueue.Value, "m_timers");

                while ((currentPointer != null) && (((ulong)currentPointer) != 0))
                {
                    // currentPointer points to a TimerQueueTimer instance
                    ulong currentTimerQueueTimerRef = (ulong)currentPointer;

                    TimerInfo ti = new TimerInfo()
                    {
                        TimerQueueTimerAddress = currentTimerQueueTimerRef
                    };

                    var val = GetFieldValue(heap, currentTimerQueueTimerRef, "m_dueTime");
                    ti.DueTime = (uint)val;
                    val = GetFieldValue(heap, currentTimerQueueTimerRef, "m_period");
                    ti.Period = (uint)val;
                    val = GetFieldValue(heap, currentTimerQueueTimerRef, "m_canceled");
                    ti.Cancelled = (bool)val;
                    val = GetFieldValue(heap, currentTimerQueueTimerRef, "m_state");
                    ti.StateTypeName = "";
                    if (val == null)
                    {
                        ti.StateAddress = 0;
                    }
                    else
                    {
                        ti.StateAddress = (ulong)val;
                        var stateType = heap.GetObjectType(ti.StateAddress);
                        if (stateType != null)
                        {
                            ti.StateTypeName = stateType.Name;
                        }
                    }

                    // decypher the callback details
                    val = GetFieldValue(heap, currentTimerQueueTimerRef, "m_timerCallback");
                    if (val != null)
                    {
                        ulong elementAddress = (ulong)val;
                        if (elementAddress == 0)
                            continue;

                        var elementType = heap.GetObjectType(elementAddress);
                        if (elementType != null)
                        {
                            if (elementType.Name == "System.Threading.TimerCallback")
                            {
                                ti.MethodAddress = (ulong)(long)GetFieldValue(heap, elementAddress, "_methodPtr");
                                ti.MethodName = BuildTimerCallbackMethodName(runtime, elementAddress, "_methodPtr");
                                if (ti.MethodName == "")
                                {
                                    ti.MethodAddress = (ulong)(long)GetFieldValue(heap, elementAddress, "_methodPtrAux");
                                    ti.MethodName = BuildTimerCallbackMethodName(runtime, elementAddress, "_methodPtrAux");
                                }
                            }
                            else
                            {
                                ti.MethodName = "<" + elementType.Name + ">";
                            }
                        }
                        else
                        {
                            ti.MethodName = "{no callback type?}";
                        }
                    }
                    else
                    {
                        ti.MethodName = "???";
                    }

                    yield return ti;

                    currentPointer = GetFieldValue(heap, currentTimerQueueTimerRef, "m_next");
                }
            }
        }
    }
}
