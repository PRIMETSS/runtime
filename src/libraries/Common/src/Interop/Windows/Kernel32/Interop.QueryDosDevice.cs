// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [DllImport(Libraries.Kernel32, SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern uint QueryDosDeviceW(string lpDeviceName, char[] lpTargetPath, int ucchMax);
    }
}
