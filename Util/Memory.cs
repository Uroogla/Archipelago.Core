using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
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

        private const uint PAGE_READONLY = 0x02;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        public const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
        public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

        public const uint MEM_RELEASE = 0x00008000;
        public const uint MEM_COMMIT = 0x00001000;

        [StructLayout(LayoutKind.Sequential)]
        public struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        internal static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);
        public static MODULEINFO GetModuleInfo(string moduleName)
        {
            MODULEINFO moduleInfo = new MODULEINFO();
            GetModuleInformation(GetProcessH(CurrentProcId), (nint)GetBaseAddress(moduleName), out moduleInfo, (uint)Marshal.SizeOf(typeof(MODULEINFO)));
            return moduleInfo;
        }
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll")]
        internal static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        [DllImport("kernel32.dll")]
        internal static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, IntPtr dwSize, uint dwFreeType);
        [DllImport("kernel32.dll")]
        internal static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
        [DllImport("kernel32.dll")]
        internal static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processID);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool ReadProcessMemory(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr processH, ulong lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr processH);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.ThisCall)]
        private static extern bool VirtualProtect(IntPtr processH, ulong lpAddress, int lpBuffer, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(IntPtr processH, ulong lpAddress, int lpBuffer, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr Arguments);

        internal static int GetProcessID(string procName)

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
        internal static Process GetProcessById(int id)
        {
            return Process.GetProcessById(id);
        }
        public static Process GetCurrentProcess()
        {
            return GetProcessById(CurrentProcId);
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
        public static string GetLastErrorMessage()
        {
            uint errorCode = GetLastError();
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
        internal static IntPtr GetProcessH(int proc)
        {
            return OpenProcess(PROCESS_VM_OPERATION | PROCESS_SUSPEND_RESUME | PROCESS_VM_READ | PROCESS_VM_WRITE, false, proc);
        }
        private static uint Execute(IntPtr address, uint timeoutSeconds = 0xFFFFFFFF)
        {
            IntPtr thread = CreateRemoteThread(GetProcessH(CurrentProcId), IntPtr.Zero, 0, address, IntPtr.Zero, 0, IntPtr.Zero);
            var fail = GetLastErrorMessage();
            if (thread == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to create remote thread. {GetLastErrorMessage()}");
                return 0;
            }
            uint result = WaitForSingleObject(thread, timeoutSeconds);

            Console.WriteLine($"WaitForSingleObject result: 0x{result:X}");
            CloseHandle(thread);
            return result;
        }
        public static uint ExecuteCommand(byte[] bytes, uint timeoutSeconds = 0xFFFFFFFF)
        {

            IntPtr address = Allocate((uint)bytes.Length, PAGE_EXECUTE_READWRITE);
            if (address == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to allocate memory. {GetLastErrorMessage()}");
                return 0;
            }


            if (!Write((ulong)address, bytes))
            {
                Console.WriteLine($"Failed to write bytes to memory. {GetLastErrorMessage()}");
                return 0;
            }
            var result = Execute(address, timeoutSeconds);
            FreeMemory(address);

            return result;

        }

        public static bool FreezeAddress(ulong address, int length)
        {
            uint oldProtect;
            bool result = VirtualProtectEx(GetProcessH(CurrentProcId), (IntPtr)address, (IntPtr)length, PAGE_READONLY, out oldProtect);

            if (!result)
            {
                Console.WriteLine($"Failed to freeze address. Error: {GetLastErrorMessage()}");
            }

            return result;
        }

        public static bool UnfreezeAddress(ulong address, int length)
        {
            uint oldProtect;
            bool result = VirtualProtectEx(GetProcessH(CurrentProcId), (IntPtr)address, (IntPtr)length, PAGE_READWRITE, out oldProtect);

            if (!result)
            {
                Console.WriteLine($"Failed to unfreeze address. Error: {GetLastErrorMessage()}");
            }

            return result;
        }

        public static IntPtr Allocate(uint size, uint flProtect = PAGE_READWRITE)
        {
            Console.WriteLine($"Allocating memory: Size - {size}, flProtect - {flProtect}");
            return VirtualAllocEx(GetProcessH(CurrentProcId), 0, (nint)size, MEM_COMMIT, flProtect);
        }
        public static bool FreeMemory(IntPtr address)
        {
            return VirtualFreeEx(GetProcessH(CurrentProcId), address, IntPtr.Zero, MEM_RELEASE);
        }
        internal static string GetSystemMessage(ulong errorCode)
        {
            return Marshal.PtrToStringAnsi(IntPtr.Zero);
        }
        public static ulong GetBaseAddress(string modName)
        {
            var process = Process.GetProcessById(CurrentProcId);
            return (ulong)(process.Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(x => x.ModuleName.Contains(modName, StringComparison.OrdinalIgnoreCase))
                ?.BaseAddress ?? IntPtr.Zero);
        }
        public static IntPtr FindSignature(IntPtr start, int size, byte[] pattern, string mask)
        {
            byte[] buffer = new byte[size];
            IntPtr bytesRead;

            ReadProcessMemory(GetProcessH(CurrentProcId), (ulong)start, buffer, size, out bytesRead);

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
        public static long ReadLong(ulong address)
        {
            byte[] dataBuffer = new byte[8];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToInt64(dataBuffer, 0);
        }
        public static ulong ReadULong(ulong address)
        {
            byte[] dataBuffer = new byte[8];
            ReadProcessMemory(GetProcessH(CurrentProcId), address, dataBuffer, dataBuffer.Length, out _);
            return BitConverter.ToUInt64(dataBuffer, 0);
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

        public static byte[] ReadFromPointer(ulong ptrAddress, int length, int depth)
        {
            var next = Memory.ReadByteArray(ptrAddress, length);
            if (--depth == 0)
                return next;
            return ReadFromPointer(BitConverter.ToUInt32(next), length, depth);
        }
        public static IEnumerable<ulong> ScanMemory<T>(T value, ulong startAddress = 0, ulong endAddress = ulong.MaxValue, int chunkSize = 4096)
        {
            byte[] valueBytes;
            if (value is byte[] bytes)
            {
                valueBytes = bytes;
            }
            else
            {
                valueBytes = GetBytes(value);
            }
            int valueSize = valueBytes.Length;

            for (ulong address = startAddress; address < endAddress; address += (ulong)chunkSize)
            {
                byte[] buffer = new byte[chunkSize];
                if (!ReadProcessMemory(GetProcessH(CurrentProcId), address, buffer, buffer.Length, out _))
                {
                    continue; // Skip if we can't read this memory chunk
                }

                for (int i = 0; i <= buffer.Length - valueSize; i++)
                {
                    if (CompareBytes(buffer, i, valueBytes))
                    {
                        yield return address + (ulong)i;
                    }
                }
            }
        }
        public static Task MonitorAddressForAction<T>(ulong address, Action action, Func<T, bool> criteria)
        {
            int size = GetSizeOfType(typeof(T));
            var initialValue = ConvertByteArrayToT<T>(Memory.ReadByteArray(address, size));
            return Task.Run(async() =>
            {
                var value = initialValue;
                while (!criteria(value))
                {
                    value = ConvertByteArrayToT<T>(Memory.ReadByteArray(address, size));
                    await Task.Delay(10);
                }
                action();
            });
        }
        private static byte[] GetBytes<T>(T value)
        {
            if (typeof(T) == typeof(byte)) return new[] { (byte)(object)value };
            if (typeof(T) == typeof(short)) return BitConverter.GetBytes((short)(object)value);
            if (typeof(T) == typeof(ushort)) return BitConverter.GetBytes((ushort)(object)value);
            if (typeof(T) == typeof(int)) return BitConverter.GetBytes((int)(object)value);
            if (typeof(T) == typeof(uint)) return BitConverter.GetBytes((uint)(object)value);
            if (typeof(T) == typeof(long)) return BitConverter.GetBytes((long)(object)value);
            if (typeof(T) == typeof(ulong)) return BitConverter.GetBytes((ulong)(object)value);
            if (typeof(T) == typeof(float)) return BitConverter.GetBytes((float)(object)value);
            if (typeof(T) == typeof(double)) return BitConverter.GetBytes((double)(object)value);
            if (typeof(T) == typeof(string)) return Encoding.UTF8.GetBytes((string)(object)value);
            throw new ArgumentException("Unsupported type");
        }
        private static T ConvertByteArrayToT<T>(byte[] bytes)
        {
            if (typeof(T) == typeof(byte)) return (T)(object)bytes[0];
            if (typeof(T) == typeof(short)) return (T)(object)BitConverter.ToInt16(bytes, 0);
            if (typeof(T) == typeof(ushort)) return (T)(object)BitConverter.ToUInt16(bytes, 0);
            if (typeof(T) == typeof(int)) return (T)(object)BitConverter.ToInt32(bytes, 0);
            if (typeof(T) == typeof(uint)) return (T)(object)BitConverter.ToUInt32(bytes, 0);
            if (typeof(T) == typeof(long)) return (T)(object)BitConverter.ToInt64(bytes, 0);
            if (typeof(T) == typeof(ulong)) return (T)(object)BitConverter.ToUInt64(bytes, 0);
            if (typeof(T) == typeof(float)) return (T)(object)BitConverter.ToSingle(bytes, 0);
            if (typeof(T) == typeof(double)) return (T)(object)BitConverter.ToDouble(bytes, 0);

            // For other types, you might need to use a more complex deserialization method
            throw new NotSupportedException($"Type {typeof(T)} is not supported for conversion from byte array.");
        }
        private static int GetSizeOfType(Type type)
        {
            if (type == typeof(byte)) return 1;
            if (type == typeof(short) || type == typeof(ushort)) return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;

            // For other types, you might need to use Marshal.SizeOf
            return System.Runtime.InteropServices.Marshal.SizeOf(type);
        }
        private static bool CompareBytes(byte[] buffer, int startIndex, byte[] valueBytes)
        {
            for (int i = 0; i < valueBytes.Length; i++)
            {
                if (buffer[startIndex + i] != valueBytes[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
