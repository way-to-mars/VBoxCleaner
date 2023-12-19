using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VBoxCleaner.IO
{
    internal class FileUsage
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr _lopen(string lpPathName, int iReadWrite);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int OF_READWRITE = 2;
        private const int OF_SHARE_DENY_NONE = 0x40;

        public enum State { NONE, BUSY, FREE }

        public static State CheckState(string vFileName)
        {

            if (!File.Exists(vFileName)) return State.NONE;

            IntPtr HFILE_ERROR = new IntPtr(-1);
            IntPtr vHandle = _lopen(vFileName, OF_READWRITE | OF_SHARE_DENY_NONE);
            if (vHandle == HFILE_ERROR) return State.BUSY;

            CloseHandle(vHandle);
            return State.FREE;
        }
    }
}