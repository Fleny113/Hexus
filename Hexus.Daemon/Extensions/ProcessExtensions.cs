using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;
using Win32 = Windows.Win32;

namespace Hexus.Daemon.Extensions;

internal static class ProcessExtensions
{
    internal record CpuStatistics
    {
        public TimeSpan LastTotalProcessorTime { get; set; } = TimeSpan.Zero;
        public DateTimeOffset LastTime { get; set; } = DateTimeOffset.UtcNow;
    }

    extension(Process process)
    {
        public double GetProcessCpuUsage(CpuStatistics cpuStatistics)
        {
            var currentTime = DateTimeOffset.UtcNow;
            var deltaTime = currentTime - cpuStatistics.LastTime;

            var totalProcessTime = process.TotalProcessorTime;
            var deltaProcessTime = totalProcessTime - cpuStatistics.LastTotalProcessorTime;

            var cpuUsage = deltaProcessTime / Environment.ProcessorCount / deltaTime;

            cpuStatistics.LastTotalProcessorTime = totalProcessTime;
            cpuStatistics.LastTime = currentTime;

            return cpuUsage * 100;
        }

        public bool SendSignal(WindowsCtrlType windowsSignal, PosixSignal posixSignal)
        {
            // process.SafeHandle.Signal on windows can only send SIGKILL, so we use our custom interop code
            if (!OperatingSystem.IsWindows()) return process.SafeHandle.Signal(posixSignal);

            // We only support sending signals on Windows 7 and later, as the CtrlRoutine procedure is not available on earlier versions.
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                return false;

            if (Win32.CtrlRoutine.ProcedureAddress.IsNull)
                return false;

            using var safeHandle = Win32.PInvoke.OpenProcess_SafeHandle(
                dwDesiredAccess: PROCESS_ACCESS_RIGHTS.PROCESS_CREATE_THREAD,
                bInheritHandle: false,
                dwProcessId: (uint)process.Id);

            if (safeHandle.IsInvalid)
                return false;

            // Microsoft.Windows.CsWin32 creates CreateRemoteThread with pointer parameters, so we need to use unsafe
            unsafe
            {
                var lpStartRoutine = Win32.CtrlRoutine.ProcedureAddress.CreateDelegate<LPTHREAD_START_ROUTINE>();
                var lpParameter = (void*)(uint)windowsSignal;

                using var remoteThread = Win32.PInvoke.CreateRemoteThread(
                    hProcess: safeHandle,
                    lpThreadAttributes: null,
                    dwStackSize: 0,
                    lpStartAddress: lpStartRoutine,
                    lpParameter: lpParameter,
                    dwCreationFlags: 0,
                    lpThreadId: out _);

                if (remoteThread.IsInvalid)
                    return false;
            }

            return true;
        }
    }
}
