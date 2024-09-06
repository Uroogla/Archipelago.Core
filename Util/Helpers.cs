using Archipelago.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Archipelago.Core.Util
{
    public static class Helpers
    {

        public static T Random<T>(this IEnumerable<T> list) where T : struct
        {
            return list.ToList()[new Random().Next(0, list.Count())];
        }
        public static string OpenEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                string jsonFile = reader.ReadToEnd();
                return jsonFile;
            }
        }

        public static async Task<bool> CheckLocation(Location location)
        {
            switch (location.CheckType)
            {
                case (LocationCheckType.Bit):
                    var currentAddressValue = Memory.ReadByte(location.Address);
                    bool currentBitValue = GetBitValue(currentAddressValue, location.AddressBit);
                    if (string.IsNullOrWhiteSpace(location.CheckValue))
                    {
                        return currentBitValue;
                    }
                    else { return !currentBitValue; }
                    break;
                case (LocationCheckType.Int):
                    var currentIntValue = Memory.ReadInt(location.Address);
                    if (location.CompareType == LocationCheckCompareType.Match)
                    {
                        return currentIntValue == Convert.ToByte(location.CheckValue);

                    }
                    else if (location.CompareType == LocationCheckCompareType.GreaterThan)
                    {
                        return currentIntValue >= Convert.ToByte(location.CheckValue);
                    }
                    else if (location.CompareType == LocationCheckCompareType.LessThan)
                    {
                        return currentIntValue <= Convert.ToByte(location.CheckValue);
                    }
                    else if (location.CompareType == LocationCheckCompareType.Range)
                    {
                        throw new NotImplementedException("Range checks are not supported yet");
                    }
                    break;
                case (LocationCheckType.UInt):
                    var currentUIntValue = Memory.ReadUInt(location.Address);
                    if (location.CompareType == LocationCheckCompareType.Match)
                    {
                        return currentUIntValue == Convert.ToByte(location.CheckValue);

                    }
                    else if (location.CompareType == LocationCheckCompareType.GreaterThan)
                    {
                        return currentUIntValue >= Convert.ToByte(location.CheckValue);
                    }
                    else if (location.CompareType == LocationCheckCompareType.LessThan)
                    {
                        return currentUIntValue <= Convert.ToByte(location.CheckValue);
                    }
                    else if (location.CompareType == LocationCheckCompareType.Range)
                    {
                        throw new NotImplementedException("Range checks are not supported yet");
                    }
                    break;
                case (LocationCheckType.Byte):
                    var currentByteValue = Memory.ReadByte(location.Address);
                    if (location.CompareType == LocationCheckCompareType.Match)
                    {
                        return currentByteValue == Convert.ToByte(location.CheckValue);

                    }
                    else if (location.CompareType == LocationCheckCompareType.GreaterThan)
                    {
                        return currentByteValue >= Convert.ToByte(location.CheckValue);
                    }
                    else if (location.CompareType == LocationCheckCompareType.LessThan)
                    {
                        return currentByteValue <= Convert.ToByte(location.CheckValue);
                    }
                    else if (location.CompareType == LocationCheckCompareType.Range)
                    {
                        throw new NotImplementedException("Range checks are not supported yet");
                    }
                    break;
                default:
                    return false;
            }
            return false;
        }
        private static bool GetBitValue(byte value, int bitIndex)
        {
            return (value & (1 << bitIndex)) != 0;
        }
        public static byte[] IntToLittleEndianBytes(int value, int numBytes = 4)
        {
            if (numBytes < 1 || numBytes > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(numBytes), "Number of bytes must be between 1 and 4.");
            }

            byte[] bytes = new byte[numBytes];
            for (int i = 0; i < numBytes; i++)
            {
                bytes[i] = (byte)(value >> (i * 8));
            }

            return bytes;
        }
        public static byte IntToLittleEndianByte(int value)
        {

            return (byte)(value >> (0 * 8));
        }
    }
}
