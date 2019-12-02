// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Helper base class for managed objects wrapping a native object.
    /// </summary>
    public abstract class WrapperBase : IDisposable
    {
        internal IntPtr _nativeHandle;

        /// <summary>
        /// Construct a new wrapper object associated with a given native handle.
        /// </summary>
        /// <param name="nativeHandle">Handle to the native object wrapped by this managed object.</param>
        protected WrapperBase(IntPtr nativeHandle)
        {
            this._nativeHandle = nativeHandle;
        }

        /// <summary>
        /// Finalize destruction of the object by disposing of native resources if not already done.
        /// </summary>
        ~WrapperBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose of native resources and optionally of managed ones.
        /// </summary>
        /// <param name="disposing"><c>true</c> if this is called from <see xref="IDisposable.Dispose()"/> and managed
        /// resources also need to be diposed, or <c>false</c> if called from the finalizer and onyl native
        /// resources can safely be accessed to be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_nativeHandle != IntPtr.Zero)
            {
                RemoveRef();
                _nativeHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Dispose of native resources associated with this managed wrapper object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Increment the reference count of the underlying native implementation object.
        /// </summary>
        protected abstract void AddRef();

        /// <summary>
        /// Decrement the reference count of the underlying native implementation object.
        /// </summary>
        protected abstract void RemoveRef();
    }
}
