using System.Runtime.Versioning;
using Windows.Win32.Foundation;

// ReSharper disable once CheckNamespace
// It does make sense for this stuff to be alongside the rest of the Win32 interop code defined by Microsoft.Windows.CsWin32
namespace Windows.Win32
{
    [SupportedOSPlatform("windows6.1")]
    internal static class CtrlRoutine
    {
        internal static FARPROC ProcedureAddress => produceAddress.Value;

        private static readonly Lazy<FARPROC> produceAddress = new(() => GetProcedureAddress("kernel32", "CtrlRoutine"));
        private static FARPROC GetProcedureAddress(string module, string proc)
        {
            var modulePtr = PInvoke.GetModuleHandle(module);
            return !modulePtr.IsInvalid ? PInvoke.GetProcAddress(modulePtr, proc) : FARPROC.Null;
        }
    }

    namespace System.Console
    {
        // https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent#parameters
        // https://learn.microsoft.com/en-us/windows/console/handlerroutine#parameters
        internal enum WindowsCtrlType : uint
        {
            /// <summary>
            /// Ctrl+C signal, typically used to interrupt a process.
            /// </summary>
            CtrlC = 0,
            /// <summary>
            /// Ctrl+Break signal, typically used to break a process.
            /// </summary>
            CtrlBreak = 1,
            /// <summary>
            /// A signal that the system sends to all processes attached to a console when the user closes the console
            /// (either by clicking Close on the console window's window menu, or by clicking the End Task button command from Task Manager).
            /// </summary>
            /// <remarks>
            /// This is not supported by GenerateConsoleCtrlEvent, but it is allowed by the HandlerRoutine callback.
            /// </remarks>
            CtrlClose = 2,
            /// <summary>
            /// A signal that the system sends to all console processes when a user is logging off.
            /// This signal does not indicate which user is logging off, so no assumptions can be made.
            ///
            /// Note that this signal is received only by services. Interactive applications are terminated at logoff,
            /// so they are not present when the system sends this signal.
            /// </summary>
            /// <remarks>
            /// This is not supported by GenerateConsoleCtrlEvent, but it is allowed by the HandlerRoutine callback.
            /// </remarks>
            CtrlLogoff = 5,
            /// <summary>
            /// A signal that the system sends when the system is shutting down.
            /// Interactive applications are not present by the time the system sends this signal,
            /// therefore it can be received only be services in this situation.
            /// Services also have their own notification mechanism for shutdown events.
            /// </summary>
            /// <remarks>
            /// This is not supported by GenerateConsoleCtrlEvent, but it is allowed by the HandlerRoutine callback.
            /// </remarks>
            CtrlShutdown = 6,
        }
    }
}
