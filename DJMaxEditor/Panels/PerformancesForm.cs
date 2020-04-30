using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using static DJMaxEditor.Panels.FModForm;

namespace DJMaxEditor.Panels
{
    public partial class PerformancesForm : Form
    {
        public BindingList<DebugInfo> debugInfo = new BindingList<DebugInfo>();

        private static PerformanceCounter cpuCounter;

        private static PerformanceCounter ramCounter;

        private static DebugInfo _cpu;

        private static DebugInfo _memory;

        public PerformancesForm()
        {
            InitializeComponent();

            string processName = Process.GetCurrentProcess().ProcessName;
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", processName);
            ramCounter = new PerformanceCounter("Memory", "Available MBytes", processName);
            _cpu = new DebugInfo() { Key = "CPU", Value = "0" };
            _memory = new DebugInfo() { Key = "Memory", Value = "0" };

            ThreadPool.QueueUserWorkItem(WatchItThreadProc);

            debugInfo.Add(_cpu);
            debugInfo.Add(_memory);
            DebugInfoList.DataSource = debugInfo;
        }

        private static void WatchItThreadProc(object obj)
        {
            while (true)
            {
                Thread.Sleep(1000);
                _cpu.Value = String.Format("{0} %", cpuCounter.NextValue());
                _memory.Value = String.Format("{0} MB", ramCounter.NextValue());

            }
        }

    }
}
