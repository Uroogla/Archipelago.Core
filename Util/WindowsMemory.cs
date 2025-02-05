using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Archipelago.Core.Util.Enums;

namespace Archipelago.Core.Util
{
    public class WindowsMemory : IMemory
    {
        #region Constants
        private const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
        private const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint MEM_COMMIT = 0x00001000;
        private const uint MEM_RELEASE = 0x00008000;
        #endregion

        #region Native Methods
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "ReadProcessMemory")]
        private static extern bool ReadProcessMemory_Win32(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "WriteProcessMemory")]
        private static extern bool WriteProcessMemory_Win32(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "OpenProcess")]
        private static extern IntPtr OpenProcess_Win32(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "VirtualProtectEx")]
        private static extern bool VirtualProtectEx_Win32(IntPtr processH, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", EntryPoint = "VirtualAllocEx")]
        private static extern IntPtr VirtualAllocEx_Win32(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", EntryPoint = "VirtualFreeEx")]
        private static extern bool VirtualFreeEx_Win32(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetLastError")]
        private static extern uint GetLastError_Win32();

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CloseHandle")]
        private static extern bool CloseHandle_Win32(IntPtr handle);

        [DllImport("kernel32.dll", EntryPoint = "CreateRemoteThread")]
        private static extern IntPtr CreateRemoteThread_Win32(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", EntryPoint = "WaitForSingleObject")]
        private static extern uint WaitForSingleObject_Win32(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetModuleHandle")]
        private static extern IntPtr GetModuleHandle_Win32(string lpModuleName);

        [DllImport("psapi.dll", SetLastError = true, EntryPoint = "GetModuleInformation")]
        private static extern bool GetModuleInformation_Win32(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr Arguments);
        #endregion

        #region Memory Operations
        public bool ReadProcessMemory(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead)
        {
            return ReadProcessMemory_Win32(processH, lpBaseAddress, lpBuffer, dwSize, out lpNumberOfBytesRead);
        }

        public bool WriteProcessMemory(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten)
        {
            return WriteProcessMemory_Win32(processH, lpBaseAddress, lpBuffer, dwSize, out lpNumberOfBytesWritten);
        }

        public IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId)
        {
            return OpenProcess_Win32(dwDesiredAccess, bInheritHandle, dwProcessId);
        }

        public bool VirtualProtectEx(IntPtr processH, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect)
        {
            return VirtualProtectEx_Win32(processH, lpAddress, dwSize, flNewProtect, out lpflOldProtect);
        }

        public IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect)
        {
            return VirtualAllocEx_Win32(hProcess, lpAddress, dwSize, flAllocationType, flProtect);
        }

        public bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint dwFreeType)
        {
            return VirtualFreeEx_Win32(hProcess, lpAddress, dwSize, dwFreeType);
        }

        public bool CloseHandle(IntPtr handle)
        {
            return CloseHandle_Win32(handle);
        }
        #endregion

        #region Error Handling
        public string GetLastErrorMessage()
        {
            uint errorCode = GetLastError_Win32();
            IntPtr lpMsgBuf = IntPtr.Zero;
            FormatMessage(
                FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero,
                errorCode,
                0,
                ref lpMsgBuf,
                0,
                IntPtr.Zero);
            string errorMessage = Marshal.PtrToStringAnsi(lpMsgBuf);
            Marshal.FreeHGlobal(lpMsgBuf);
            return $"Error {errorCode}: {errorMessage}";
        }
        public uint GetLastError()
        {
            return GetLastError_Win32();
        }
        #endregion

        #region Module Information
        public MODULEINFO GetModuleInfo(IntPtr processHandle, string moduleName)
        {
            MODULEINFO moduleInfo = new MODULEINFO();
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            GetModuleInformation_Win32(processHandle, moduleHandle, out moduleInfo, (uint)Marshal.SizeOf(typeof(MODULEINFO)));
            return moduleInfo;
        }

        public IntPtr GetModuleHandle(string moduleName)
        {
            return GetModuleHandle_Win32(moduleName);
        }
        #endregion

        #region Remote Execution
        public uint Execute(IntPtr processHandle, IntPtr address, uint timeoutSeconds = 0xFFFFFFFF)
        {
            IntPtr thread = CreateRemoteThread_Win32(processHandle, IntPtr.Zero, 0, address, IntPtr.Zero, 0, IntPtr.Zero);
            if (thread == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to create remote thread: {GetLastErrorMessage()}");
                return 0;
            }

            uint result = WaitForSingleObject_Win32(thread, timeoutSeconds);
            CloseHandle(thread);
            return result;
        }

        public uint ExecuteCommand(IntPtr processHandle, byte[] bytes, uint timeoutSeconds = 0xFFFFFFFF)
        {
            IntPtr address = VirtualAllocEx(processHandle, IntPtr.Zero, (IntPtr)bytes.Length, MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            if (address == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to allocate memory: {GetLastErrorMessage()}");
                return 0;
            }

            try
            {
                IntPtr bytesWritten;
                if (!WriteProcessMemory(processHandle, (ulong)address, bytes, bytes.Length, out bytesWritten))
                {
                    Console.WriteLine($"Failed to write bytes to memory: {GetLastErrorMessage()}");
                    VirtualFreeEx(processHandle, address, IntPtr.Zero, MEM_RELEASE);
                    return 0;
                }

                uint result = Execute(processHandle, address, timeoutSeconds);
                VirtualFreeEx(processHandle, address, IntPtr.Zero, MEM_RELEASE);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command: {ex.Message}");
                VirtualFreeEx(processHandle, address, IntPtr.Zero, MEM_RELEASE);
                return 0;
            }
        }
        #endregion
    }
}
