using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Util.Duckstation
{
    internal class DuckstationMemoryHelper
    {
        private IntPtr processHandle;

        // Define constants needed for module enumeration
        private const int MAX_PATH = 260;
        private const uint LIST_MODULES_ALL = 0x03;

        // Define the structures needed for module enumeration
        [StructLayout(LayoutKind.Sequential)]
        public struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szExePath;
        }

        // Import the required functions
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, int th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll", SetLastError = true)]
        static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        public DuckstationMemoryHelper()
        {
            processHandle = Memory.GetCurrentProcess().Handle;
        }

        public IntPtr FindEEromAddress()
        {
            // Try to find the PCSX2 module
            IntPtr duckstationModule = FindModuleInProcess("duckstation-qt-x64-ReleaseLTCG");
            if (duckstationModule == IntPtr.Zero)
            {
                Console.WriteLine("Failed to find Duckstation module");
                return IntPtr.Zero;
            }

            // Find the export table in the PE header
            IntPtr exportTableAddress = FindExportTable(duckstationModule);
            if (exportTableAddress == IntPtr.Zero)
            {
                Console.WriteLine("Failed to find export table");
                return IntPtr.Zero;
            }

            // Find the EEmem export
            IntPtr EEmemPtr = FindExportByName(duckstationModule, exportTableAddress, "RAM");
            if (EEmemPtr == IntPtr.Zero)
            {
                Console.WriteLine("Failed to find EEmem export");
                return IntPtr.Zero;
            }

            // Read the pointer value at the EEmem address
            byte[] buffer = new byte[IntPtr.Size];
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)EEmemPtr, buffer, buffer.Length, out IntPtr bytesRead))
            {
                Console.WriteLine("Failed to read EEmem pointer value");
                return IntPtr.Zero;
            }

            // Convert buffer to pointer
            IntPtr EEmemBaseAddress = (IntPtr)BitConverter.ToInt64(buffer, 0);

            return EEmemBaseAddress;
        }

        private IntPtr FindModuleInProcess(string moduleName)
        {
            // Create a snapshot of all modules in the process
            IntPtr snapshot = CreateToolhelp32Snapshot(0x00000008, Memory.GetProcIdFromExe("duckstation-qt-x64-ReleaseLTCG"));
            if (snapshot == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create snapshot");
                return IntPtr.Zero;
            }

            // Enumerate the modules
            MODULEENTRY32 moduleEntry = new MODULEENTRY32();
            moduleEntry.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));

            if (Module32First(snapshot, ref moduleEntry))
            {
                do
                {
                    if (moduleEntry.szModule.ToLower().Contains(moduleName.ToLower()))
                    {
                        Memory.PlatformImpl.CloseHandle(snapshot);
                        return moduleEntry.modBaseAddr;
                    }
                } while (Module32Next(snapshot, ref moduleEntry));
            }

            Memory.PlatformImpl.CloseHandle(snapshot);
            return IntPtr.Zero;
        }

        private IntPtr FindExportTable(IntPtr moduleBaseAddress)
        {
            // Read the DOS header
            byte[] dosHeaderBuffer = new byte[64];  // DOS header size
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress, dosHeaderBuffer, dosHeaderBuffer.Length, out IntPtr bytesRead))
            {
                Console.WriteLine("Failed to read DOS header");
                return IntPtr.Zero;
            }

            // Check for MZ signature
            if (dosHeaderBuffer[0] != 'M' || dosHeaderBuffer[1] != 'Z')
            {
                Console.WriteLine("Invalid DOS header signature");
                return IntPtr.Zero;
            }

            // Get e_lfanew field to find the PE header
            int e_lfanew = BitConverter.ToInt32(dosHeaderBuffer, 0x3C);

            // Read the NT header signature
            byte[] ntSignatureBuffer = new byte[4];
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + (ulong)e_lfanew, ntSignatureBuffer, ntSignatureBuffer.Length, out bytesRead))
            {
                Console.WriteLine("Failed to read NT signature");
                return IntPtr.Zero;
            }

            // Check for PE signature
            if (ntSignatureBuffer[0] != 'P' || ntSignatureBuffer[1] != 'E' || ntSignatureBuffer[2] != 0 || ntSignatureBuffer[3] != 0)
            {
                Console.WriteLine("Invalid PE signature");
                return IntPtr.Zero;
            }

            // Read the File Header and determine if 32-bit or 64-bit
            byte[] machineBuffer = new byte[2];
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + (ulong)e_lfanew + 4, machineBuffer, machineBuffer.Length, out bytesRead))
            {
                Console.WriteLine("Failed to read machine type");
                return IntPtr.Zero;
            }

            bool is32Bit = (BitConverter.ToUInt16(machineBuffer, 0) & 0x0100) != 0;
            int optionalHeaderOffset = e_lfanew + 4 + 20; // 4 for PE signature, 20 for File Header
            int dataDirectoryOffset;

            if (is32Bit)
            {
                dataDirectoryOffset = optionalHeaderOffset + 96; // 96 is the offset to the data directory in the 32-bit optional header
            }
            else
            {
                dataDirectoryOffset = optionalHeaderOffset + 112; // 112 is the offset to the data directory in the 64-bit optional header
            }

            // Read the Export Directory RVA and Size
            byte[] exportDirectoryBuffer = new byte[8]; // 4 bytes for RVA, 4 bytes for Size
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + (ulong)dataDirectoryOffset, exportDirectoryBuffer, exportDirectoryBuffer.Length, out bytesRead))
            {
                Console.WriteLine("Failed to read export directory");
                return IntPtr.Zero;
            }

            uint exportDirectoryRVA = BitConverter.ToUInt32(exportDirectoryBuffer, 0);

            return (IntPtr)((ulong)moduleBaseAddress + exportDirectoryRVA);
        }

        private IntPtr FindExportByName(IntPtr moduleBaseAddress, IntPtr exportTableAddress, string exportName)
        {
            // Read the export directory
            byte[] exportDirectoryBuffer = new byte[40]; // Size of IMAGE_EXPORT_DIRECTORY
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)exportTableAddress, exportDirectoryBuffer, exportDirectoryBuffer.Length, out IntPtr bytesRead))
            {
                Console.WriteLine("Failed to read export directory");
                return IntPtr.Zero;
            }

            // Extract important fields from the export directory
            uint numberOfNames = BitConverter.ToUInt32(exportDirectoryBuffer, 24);
            uint addressOfNames = BitConverter.ToUInt32(exportDirectoryBuffer, 32);
            uint addressOfNameOrdinals = BitConverter.ToUInt32(exportDirectoryBuffer, 36);
            uint addressOfFunctions = BitConverter.ToUInt32(exportDirectoryBuffer, 28);

            // Read the names RVA array
            byte[] namesBuffer = new byte[numberOfNames * 4]; // Each entry is a 4-byte RVA
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + addressOfNames, namesBuffer, namesBuffer.Length, out bytesRead))
            {
                Console.WriteLine("Failed to read export names");
                return IntPtr.Zero;
            }

            // Read the ordinals array
            byte[] ordinalsBuffer = new byte[numberOfNames * 2]; // Each entry is a 2-byte ordinal
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + addressOfNameOrdinals, ordinalsBuffer, ordinalsBuffer.Length, out bytesRead))
            {
                Console.WriteLine("Failed to read export ordinals");
                return IntPtr.Zero;
            }

            // Read the functions RVA array
            byte[] functionsBuffer = new byte[numberOfNames * 4]; // Each entry is a 4-byte RVA
            if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + addressOfFunctions, functionsBuffer, functionsBuffer.Length, out bytesRead))
            {
                Console.WriteLine("Failed to read export functions");
                return IntPtr.Zero;
            }

            // Find the export name
            for (uint i = 0; i < numberOfNames; i++)
            {
                uint nameRVA = BitConverter.ToUInt32(namesBuffer, (int)(i * 4));

                // Read the export name
                byte[] nameBuffer = new byte[256]; // Assume max name length
                if (!Memory.PlatformImpl.ReadProcessMemory(processHandle, (ulong)moduleBaseAddress + nameRVA, nameBuffer, nameBuffer.Length, out bytesRead))
                {
                    continue;
                }

                // Convert to string (null-terminated)
                string currentExportName = Encoding.ASCII.GetString(nameBuffer);
                int nullTerminator = currentExportName.IndexOf('\0');
                if (nullTerminator != -1)
                {
                    currentExportName = currentExportName.Substring(0, nullTerminator);
                }

                if (currentExportName == exportName)
                {
                    // Get the ordinal for this name
                    ushort ordinal = BitConverter.ToUInt16(ordinalsBuffer, (int)(i * 2));

                    // Get the function RVA for this ordinal
                    uint functionRVA = BitConverter.ToUInt32(functionsBuffer, ordinal * 4);

                    // Return the actual address of the function
                    return (IntPtr)((ulong)moduleBaseAddress + functionRVA);
                }
            }

            return IntPtr.Zero;
        }
    }
}

