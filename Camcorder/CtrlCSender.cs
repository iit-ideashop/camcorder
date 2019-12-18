using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Camcorder {
    public static class CtrlCSender {
        // https://stackoverflow.com/a/29274238
        public static bool GracefulExit(this Process p) {
            if (AttachConsole((uint)p.Id)) {
                SetConsoleCtrlHandler(null, true);
                try {
                    if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                        return false;
                    p.WaitForExit();
                } finally {
                    FreeConsole();
                    SetConsoleCtrlHandler(null, false);
                }

                return true;
            }

            return false;
        }

        #region DLL Imports for sending a Ctrl-C

        public const int CTRL_C_EVENT = 0;

        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);

        #endregion
    }
}
