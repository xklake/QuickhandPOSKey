using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace Quickhand.Licensing.Legacy
{
    internal static class HardwareQuery
    {
        // Return detached strings only. The synchronous WMI API creates
        // ManagementObjectCollection enumerators whose RCWs can be finalized
        // while another COM call is active (RaceOnRCWCleanup).
        internal static string[] ReadFirst(string query, params string[] properties)
        {
            // ManagerTool can call from an STA UI thread. Initiate WMI on MTA,
            // so waiting for completion never depends on that UI pumping COM.
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                return Task.Run(() => ReadFirstCore(query, properties)).GetAwaiter().GetResult();
            return ReadFirstCore(query, properties);
        }

        private static string[] ReadFirstCore(string query, string[] properties)
        {
            var gate = new object();
            var observer = new ManagementOperationObserver();
            string[] first = null;
            Exception failure = null;
            bool receivedFirst = false;
            bool completed = false;
            bool active = true;

            ObjectReadyEventHandler onObject = (sender, args) =>
            {
                // A callback already queued during cancellation must still release
                // its object. No ManagementBaseObject is retained by a caller.
                using (var item = args.NewObject)
                {
                    lock (gate)
                    {
                        if (!active || receivedFirst) return;
                        receivedFirst = true;
                        try
                        {
                            var values = new string[properties.Length];
                            for (int i = 0; i < values.Length; i++)
                                values[i] = item[properties[i]]?.ToString();
                            first = values;
                        }
                        catch (Exception ex) { failure = ex; }
                    }
                }
            };
            CompletedEventHandler onCompleted = (sender, args) =>
            {
                using (args.StatusObject)
                {
                    lock (gate)
                    {
                        if (!active) return;
                        if (args.Status != ManagementStatus.NoError && failure == null)
                            failure = new ManagementException("Hardware query failed: " + args.Status);
                        completed = true;
                        Monitor.PulseAll(gate);
                    }
                }
            };

            observer.ObjectReady += onObject;
            observer.Completed += onCompleted;
            using (var searcher = new ManagementObjectSearcher(query))
            {
                try
                {
                    searcher.Get(observer); // No collection, Count, Clone or enumerator.
                    var elapsed = Stopwatch.StartNew();
                    lock (gate)
                    {
                        while (!completed)
                        {
                            var remaining = TimeSpan.FromSeconds(30) - elapsed.Elapsed;
                            if (remaining <= TimeSpan.Zero)
                                throw new TimeoutException("Hardware query did not finish within 30 seconds.");
                            Monitor.Wait(gate, remaining);
                        }
                        if (failure != null) throw new InvalidOperationException("Cannot read hardware information.", failure);
                        return first;
                    }
                }
                finally
                {
                    bool cancel;
                    lock (gate)
                    {
                        active = false;
                        cancel = !completed;
                    }
                    // Never cancel while holding gate: cancellation may deliver a callback.
                    try { if (cancel) observer.Cancel(); }
                    finally
                    {
                        observer.ObjectReady -= onObject;
                        observer.Completed -= onCompleted;
                    }
                }
            }
        }
    }
}
