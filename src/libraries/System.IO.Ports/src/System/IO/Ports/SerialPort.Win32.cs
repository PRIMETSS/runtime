// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.IO.Ports
{
    public partial class SerialPort : Component
    {
        // Windows SerialPort GUID Class ID
        private const string GuidDevInterfaceComPortID = "86e0d1e0-8089-11d0-9ce4-08003e301f73";

        public static string[] GetPortNames()
        {
            // Hitting the registry for this isn't the only way to get the ports.
            //
            // WMI: https://msdn.microsoft.com/en-us/library/aa394413.aspx
            // QueryDosDevice: https://msdn.microsoft.com/en-us/library/windows/desktop/aa365461.aspx
            //
            // QueryDosDevice involves finding any ports that map to \Device\Serialx (call with null to get all, then iterate to get the actual device name)
            //
            // GetPortNames will return serial port device names for both registry registered SerialComm devices as well as DosDevice names from Interop QueryDosDevice()
            // USB Serial Ports on Win10IoT are not initalised in registry key HKLM\HARDWARE\DEVICEMAP\SERIALCOMM correctly, placing garbage chars in this keys value
            // So to handle this, we are using Interop QueryDosDevice() call to obtain Serialports, not the registry

            if (System.Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "ARM")
            {
                return (string[]) QueryDosDeviceComPorts(GuidDevInterfaceComPortID).ToArray(typeof(string));
            }
            else
            {
                using (RegistryKey serialKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
                {
                    if (serialKey != null)
                    {
                        string[] result = serialKey.GetValueNames();
                        for (int i = 0; i < result.Length; i++)
                        {
                            // Replace the name in the array with its value.
                            result[i] = (string)serialKey.GetValue(result[i]);
                        }
                        return result;
                    }
                }
            }

            return Array.Empty<string>();
        }

        private static System.Collections.ArrayList QueryDosDeviceComPorts(string filterGuid)
        {
            // Build a list of all system Com Port device names.
            // memBuff starts with a small arbitary size and dynamically expands until there is enough room for data returned from Kernal32.QueryDosDeviceW().

            System.Collections.ArrayList returnList = new System.Collections.ArrayList();

            var buffPool = System.Buffers.ArrayPool<char>.Shared;
            int maxBuffSize = 1024;
            uint returnSize = 0;
            char[] memBuff = buffPool.Rent(maxBuffSize);

            while ((returnSize = Interop.Kernel32.QueryDosDeviceW(null, memBuff, memBuff.Length)) == 0)
            {
                int error = Marshal.GetLastWin32Error();
                switch (error)
                {
                    case Interop.Errors.ERROR_INSUFFICIENT_BUFFER:
                    case Interop.Errors.ERROR_MORE_DATA:
                        // Return and rent a larger char buffer
                        maxBuffSize += 1024;
                        buffPool.Return(memBuff);
                        memBuff = buffPool.Rent(maxBuffSize);
                        break;
                    default:
                        throw new Win32Exception(error);
                }
            }

            ReadOnlySpan<char> allPortNames = new Span<char>(memBuff);
            int head = 0;
            int skip = allPortNames.IndexOf('\0');

            // Build list of device names filtered by SerialPort Guid
            while (skip > 0)
            {
                var singlePortName = allPortNames.Slice(head, skip);

                // If device name contains the SerialPort GUID, add device name to returnList
                if (singlePortName.Contains(filterGuid.ToCharArray(), StringComparison.OrdinalIgnoreCase))
                {
                    returnList.Add(@"\\?\" + singlePortName.ToString());
                }

                head = head + skip + 1;
                skip = allPortNames.Slice(head, allPortNames.Length - head).IndexOf('\0');
            }

            return returnList;
        }
    }
}
