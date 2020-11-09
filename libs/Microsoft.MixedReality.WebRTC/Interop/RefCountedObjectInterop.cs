// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native reference-counted object.
    /// </summary>
    internal abstract class RefCountedObjectHandle : ObjectHandle
    {
        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            RefCountedObjectInterop.RefCountedObject_RemoveRef(handle);
            return true;
        }
    }

    internal class RefCountedObjectInterop
    {
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRefCountedObjectAddRef")]
        public static unsafe extern void RefCountedObject_AddRef(RefCountedObjectHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use RefCountedObjectHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsRefCountedObjectRemoveRef")]
        public static unsafe extern void RefCountedObject_RemoveRef(IntPtr handle);
    }
}
