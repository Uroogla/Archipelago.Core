using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util
{
    public class Memory
    {
        #region kernel functions
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_SUSPEND_RESUME = 0x0800;

        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        public const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
        public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;


        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processID);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr processH);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.ThisCall)]
        public static extern bool VirtualProtect(IntPtr processH, ulong lpAddress, int lpBuffer, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualProtectEx(IntPtr processH, ulong lpAddress, int lpBuffer, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr Arguments);

        private static int GetProcessID(string procName)

        {
            Process[] Processes = Process.GetProcessesByName(procName);
            if (Processes.Any(x => x.MainWindowHandle > 0))
            {
                IntPtr hWnd = Processes.First(x => x.MainWindowHandle > 0).MainWindowHandle;
                GetWindowThreadProcessId(hWnd, out int PID);
                return PID;
            }
            else
            {
                //application is not running
                CloseHandle(GetProcessH(CurrentProcId));
                return 0;
            }
        }

        //Make PID available anywhere within the program.
        public static int BIZHAWK_PROCESSID
        {
            get
            {
                var pid = GetProcessID("EmuHawk");
                return pid;
            }
        }
        public static int EPSXE_PROCESSID
        {
            get
            {
                var pid = GetProcessID("ePSXe");
                return pid;
            }
        }
        public static int PCSX2_PROCESSID
        {
            get
            {
                var pid = GetProcessID("pcsx2");
                if (pid == 0)
                {
                    pid = GetProcessID("pcsx2-qt");
                }
                return pid;
            }
        }
        public static int DARKSOULS_PROCESSID
        {
            get
            {
                var pid = GetProcessID("DarkSoulsRemastered");
                return pid;
            }
        }
        public static int XENIA_PROCESSID
        {
            get
            {
                var pid = GetProcessID("Xenia");
                return pid;
            }
        }
        public static int GetProcIdFromExe(string exe)
        {
            var pid = GetProcessID(exe);
            return pid;
        }
        public static int CurrentProcId { get; set; }
        public static IntPtr GetProcessH(int proc)
        {
            return OpenProcess(PROCESS_VM_OPERATION | PROCESS_SUSPEND_RESUME | PROCESS_VM_READ | PROCESS_VM_WRITE, false, proc);
        }

        internal static string GetSystemMessage(ulong errorCode)
        {
            return Marshal.PtrToStringAnsi(IntPtr.Zero);
        }
        #endregion
        public static byte ReadByte(ulong address)
        {
            byte[] dataBuffer = new byte[1];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return dataBuffer[0];
        }

        public static byte[] ReadByteArray(ulong address, int numBytes)
        {
            byte[] dataBuffer = new byte[numBytes];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return dataBuffer;
        }

        public static ushort ReadUShort(ulong address)
        {
            byte[] dataBuffer = new byte[2];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToUInt16(dataBuffer, 0);
        }

        public static short ReadShort(ulong address)
        {
            byte[] dataBuffer = new byte[2];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToInt16(dataBuffer, 0);
        }

        public static uint ReadUInt(ulong address)
        {
            byte[] dataBuffer = new byte[4];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToUInt32(dataBuffer, 0);
        }

        public static int ReadInt(ulong address)
        {
            byte[] dataBuffer = new byte[4];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToInt32(dataBuffer, 0);
        }
        public static int ReadBigEndianInt(ulong address)
        {
            byte[] dataBuffer = new byte[4];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(dataBuffer);
            }

            return BitConverter.ToInt32(dataBuffer, 0);
        }
        public static float ReadFloat(ulong address)
        {
            byte[] dataBuffer = new byte[8];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToSingle(dataBuffer, 0);
        }

        public static double ReadDouble(ulong address)
        {
            byte[] dataBuffer = new byte[8];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToDouble(dataBuffer, 0);
        }

        public static string ReadString(ulong address, int length)
        {
            byte[] dataBuffer = new byte[length];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, length, out _);
            var converter = Encoding.GetEncoding(10000);
            var output = converter.GetString(dataBuffer);
            return output;
        }

        public static bool Write(ulong address, byte[] value)
        {
            return WriteProcessMemory(GetProcessH(CurrentProcId), address, value, value.Length, out _);
        }

        public static bool WriteString(ulong address, string stringToWrite)
        {
            byte[] dataBuffer = Encoding.GetEncoding(10000).GetBytes(stringToWrite);
            return WriteProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
        }

        public static bool WriteByte(ulong address, byte value)
        {
            return Write(address, [value]);
        }

        public static void WriteByteArray(ulong address, byte[] byteArray)
        {
            bool successful;
            successful = VirtualProtectEx(GetProcessH(CurrentProcId), address, byteArray.Length, PAGE_EXECUTE_READWRITE, out _);
            if (successful == false)
                Console.WriteLine(GetLastError() + " - " + GetSystemMessage(GetLastError()));
            successful = WriteProcessMemory(GetProcessH(CurrentProcId), address, byteArray, byteArray.Length, out _);
            if (successful == false)
                Console.WriteLine(GetLastError() + " - " + GetSystemMessage(GetLastError()));
        }

        public static bool Write(ulong address, ushort value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }
        public static bool Write(ulong address, int value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }
        public static bool Write(ulong address, short value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }
        public static bool Write(ulong address, uint value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }
        public static bool Write(ulong address, float value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }
        public static bool Write(ulong address, double value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }
        public static bool WriteBit(ulong address, int bitPosition, bool value)
        {
            if (bitPosition < 0 || bitPosition > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(bitPosition), "Bit position must be between 0 and 7.");
            }

            byte currentByte = ReadByte(address);

            if (value)
            {
                currentByte = (byte)(currentByte | (1 << bitPosition));
            }
            else
            {
                currentByte = (byte)(currentByte & ~(1 << bitPosition));
            }

            return WriteByte(address, currentByte);
        }
    }
}
