using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Archipelago.Core.Util.Enums;

namespace Archipelago.Core.Util
{

    public class Memory
    {
        #region Platform Implementation
        private static readonly IMemory PlatformImpl;

        static Memory()
        {
            PlatformImpl = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? new LinuxMemory()
                : new WindowsMemory();
        }
        #endregion

        #region Constants
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_SUSPEND_RESUME = 0x0800;

        public const uint PAGE_READONLY = 0x02;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        public const uint MEM_RELEASE = 0x00008000;
        public const uint MEM_COMMIT = 0x00001000;
        #endregion

        #region Process Management
        public static int CurrentProcId { get; set; }

        internal static IntPtr GetProcessH(int proc)
        {
            return PlatformImpl.OpenProcess(PROCESS_VM_OPERATION | PROCESS_SUSPEND_RESUME | PROCESS_VM_READ | PROCESS_VM_WRITE, false, proc);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processID);

        public static int GetProcessID(string procName)
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
                PlatformImpl.CloseHandle(GetProcessH(CurrentProcId));
                return 0;
            }
        }

        public static Process GetProcessById(int id)
        {
            return Process.GetProcessById(id);
        }

        public static Process GetCurrentProcess()
        {
            return GetProcessById(CurrentProcId);
        }

        public static ulong GetBaseAddress(string modName)
        {
            var process = Process.GetProcessById(CurrentProcId);
            return (ulong)(process.Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(x => x.ModuleName.Contains(modName, StringComparison.OrdinalIgnoreCase))
                ?.BaseAddress ?? IntPtr.Zero);
        }

        public static string GetLastErrorMessage()
        {
            return PlatformImpl.GetLastErrorMessage();
        }
        #endregion

        #region Read Operations
        public static byte ReadByte(ulong address)
        {
            byte[] buffer = new byte[1];
            PlatformImpl.ReadProcessMemory(GetProcessH(CurrentProcId), address, buffer, buffer.Length, out _);
            return buffer[0];
        }

        public static byte[] ReadByteArray(ulong address, int length, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = new byte[length];
            PlatformImpl.ReadProcessMemory(GetProcessH(CurrentProcId), address, buffer, buffer.Length, out _);
            if (endianness == Endianness.Big && BitConverter.IsLittleEndian ||
                endianness == Endianness.Little && !BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            return buffer;
        }

        public static bool ReadBit(ulong address, int bitNumber, Endianness endianness = Endianness.Little)
        {
            if (bitNumber < 0 || bitNumber > 7)
                throw new ArgumentOutOfRangeException(nameof(bitNumber), "Bit number must be between 0-7");

            byte b = ReadByte(address);

            if (endianness == Endianness.Big)
            {
                bitNumber = 7 - bitNumber;
            }

            return (b & (1 << bitNumber)) != 0;
        }

        public static ushort ReadUShort(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(ushort));
            return BitConverter.ToUInt16(HandleEndianness(buffer, endianness), 0);
        }

        public static short ReadShort(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(short));
            return BitConverter.ToInt16(HandleEndianness(buffer, endianness), 0);
        }

        public static uint ReadUInt(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(uint));
            return BitConverter.ToUInt32(HandleEndianness(buffer, endianness), 0);
        }

        public static int ReadInt(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(int));
            return BitConverter.ToInt32(HandleEndianness(buffer, endianness), 0);
        }

        public static long ReadLong(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(long));
            return BitConverter.ToInt64(HandleEndianness(buffer, endianness), 0);
        }

        public static ulong ReadULong(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(ulong));
            return BitConverter.ToUInt64(HandleEndianness(buffer, endianness), 0);
        }

        public static float ReadFloat(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(float));
            return BitConverter.ToSingle(HandleEndianness(buffer, endianness), 0);
        }

        public static double ReadDouble(ulong address, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = ReadByteArray(address, sizeof(double));
            return BitConverter.ToDouble(HandleEndianness(buffer, endianness), 0);
        }

        public static string ReadString(ulong address, int length, Endianness endianness = Endianness.Little, Encoding encoding = null)
        {
            byte[] dataBuffer = ReadByteArray(address, length, endianness);
            encoding ??= Encoding.UTF8;
            return encoding.GetString(dataBuffer);
        }
        #endregion

        #region Write Operations
        public static bool Write(ulong address, byte[] value)
        {
            return PlatformImpl.WriteProcessMemory(GetProcessH(CurrentProcId), address, value, value.Length, out _);
        }

        public static bool WriteString(ulong address, string value, Endianness endianness = Endianness.Little, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            byte[] bytes = encoding.GetBytes(value);
            WriteByteArray(address, bytes, endianness);
            return true;
        }

        public static bool WriteByte(ulong address, byte value)
        {
            return Write(address, new[] { value });
        }

        public static void WriteByteArray(ulong address, byte[] data, Endianness endianness = Endianness.Little)
        {
            if (endianness == Endianness.Big && BitConverter.IsLittleEndian ||
                endianness == Endianness.Little && !BitConverter.IsLittleEndian)
            {
                data = data.ToArray(); // Create a copy before reversing
                Array.Reverse(data);
            }
            Write(address, data);
        }

        public static bool Write(ulong address, ushort value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, short value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, uint value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, int value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, float value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, double value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, long value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool Write(ulong address, ulong value, Endianness endianness = Endianness.Little)
        {
            byte[] buffer = HandleEndianness(BitConverter.GetBytes(value), endianness);
            return Write(address, buffer);
        }

        public static bool WriteBit(ulong address, int bitNumber, bool value, Endianness endianness = Endianness.Little)
        {
            if (bitNumber < 0 || bitNumber > 7)
                throw new ArgumentOutOfRangeException(nameof(bitNumber), "Bit number must be between 0-7");

            if (endianness == Endianness.Big)
            {
                bitNumber = 7 - bitNumber;
            }

            byte currentByte = ReadByte(address);

            if (value)
                currentByte = (byte)(currentByte | (1 << bitNumber));
            else
                currentByte = (byte)(currentByte & ~(1 << bitNumber));

            return WriteByte(address, currentByte);
        }
        #endregion

        #region Memory Operations
        public static bool FreezeAddress(ulong address, int length)
        {
            uint oldProtect;
            return PlatformImpl.VirtualProtectEx(GetProcessH(CurrentProcId), (IntPtr)address, (IntPtr)length, PAGE_READONLY, out oldProtect);
        }

        public static bool UnfreezeAddress(ulong address, int length)
        {
            uint oldProtect;
            return PlatformImpl.VirtualProtectEx(GetProcessH(CurrentProcId), (IntPtr)address, (IntPtr)length, PAGE_READWRITE, out oldProtect);
        }

        public static IntPtr Allocate(uint size, uint flProtect = PAGE_READWRITE)
        {
            return PlatformImpl.VirtualAllocEx(GetProcessH(CurrentProcId), IntPtr.Zero, (IntPtr)size, MEM_COMMIT, flProtect);
        }

        public static bool FreeMemory(IntPtr address)
        {
            return PlatformImpl.VirtualFreeEx(GetProcessH(CurrentProcId), address, IntPtr.Zero, MEM_RELEASE);
        }
        #endregion

        #region Pattern Scanning
        public static IntPtr FindSignature(IntPtr start, int size, byte[] pattern, string mask)
        {
            byte[] buffer = new byte[size];
            IntPtr bytesRead;

            PlatformImpl.ReadProcessMemory(GetProcessH(CurrentProcId), (ulong)start, buffer, size, out bytesRead);

            for (int i = 0; i < size - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (mask[j] != '?' && buffer[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return start + i;
            }

            return IntPtr.Zero;
        }
        #endregion

        #region Remote Execution
        private static uint Execute(IntPtr address, uint timeoutSeconds = 0xFFFFFFFF)
        {
            return PlatformImpl.Execute(GetProcessH(CurrentProcId), address, timeoutSeconds);
        }

        public static uint ExecuteCommand(byte[] bytes, uint timeoutSeconds = 0xFFFFFFFF)
        {
            return PlatformImpl.ExecuteCommand(GetProcessH(CurrentProcId), bytes, timeoutSeconds);
        }
        #endregion

        #region Module Information
        public static MODULEINFO GetModuleInfo(string moduleName)
        {
            return PlatformImpl.GetModuleInfo(GetProcessH(CurrentProcId), moduleName);
        }
        #endregion

        #region Common Process IDs
        public static int BIZHAWK_PROCESSID => GetProcessID("EmuHawk");
        public static int EPSXE_PROCESSID => GetProcessID("ePSXe");
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
        public static int XENIA_PROCESSID => GetProcessID("Xenia");

        public static int GetProcIdFromExe(string exe) => GetProcessID(exe);
        #endregion

        #region Utilities
        private static byte[] HandleEndianness(byte[] data, Endianness endianness)
        {
            if (endianness == Endianness.Big && BitConverter.IsLittleEndian ||
                endianness == Endianness.Little && !BitConverter.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return data;
        }
        #endregion
    }
}
