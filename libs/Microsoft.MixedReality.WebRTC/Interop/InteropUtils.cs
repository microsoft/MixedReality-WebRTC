// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Tracing;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Interop boolean.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    internal struct mrsBool
    {
        public static readonly mrsBool True = new mrsBool(true);
        public static readonly mrsBool False = new mrsBool(false);
        private int _value;
        public mrsBool(bool value) { _value = (value ? -1 : 0); }
        public static explicit operator mrsBool(bool b) { return (b ? True : False); }
        public static explicit operator bool(mrsBool b) { return (b._value != 0); }
    }

    /// <summary>
    /// Interop optional boolean, conceptually equivalent to <c>bool?</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    internal struct mrsOptBool
    {
        public static readonly mrsOptBool Unset = new mrsOptBool { _value = kUnsetValue };
        public static readonly mrsOptBool True = new mrsOptBool { _value = -1 };
        public static readonly mrsOptBool False = new mrsOptBool { _value = 0 };

        private sbyte _value;

        private const sbyte kUnsetValue = 0b01010101;

        public bool HasValue => (_value != kUnsetValue);
        public bool Value
        {
            get
            {
                if (!HasValue)
                {
                    throw new InvalidOperationException();
                }
                return (bool)this;
            }
        }

        public static explicit operator mrsOptBool(bool b) { return (b ? True : False); }
        public static explicit operator mrsOptBool(bool? b)
        {
            if (b.HasValue)
            {
                return (b.Value ? True : False);
            }
            return Unset;
        }
        public static explicit operator bool(mrsOptBool b)
        {
            if (!b.HasValue)
            {
                throw new InvalidOperationException();
            }
            return (b._value != 0);
        }
        public static explicit operator bool?(mrsOptBool b)
        {
            if (b.HasValue)
            {
                return (b._value != 0);
            }
            return null;
        }
    }

    /// <summary>
    /// Attribute to decorate managed delegates used as native callbacks (reverse P/Invoke).
    /// Required by Mono in Ahead-Of-Time (AOT) compiling, and Unity with the IL2CPP backend.
    /// </summary>
    ///
    /// This attribute is required by Mono AOT and Unity IL2CPP, but not by .NET Core or Framework.
    /// The implementation was copied from the Mono source code (https://github.com/mono/mono).
    /// The type argument does not seem to be used anywhere in the code, and a stub implementation
    /// like this seems to be enough for IL2CPP to be able to marshal the delegate (untested on Mono).
    [AttributeUsage(AttributeTargets.Method)]
    sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type t) { }
    }

    internal static class Utils
    {
        // Note that on Windows due to a "bug" in LoadLibraryEx() this filename must not contain any '.'.
        // See https://github.com/dotnet/runtime/issues/7223
        internal const string dllPath = "mrwebrtc";

        // Error codes returned by the interop API -- see result.h
        internal const uint MRS_SUCCESS = 0u;
        internal const uint MRS_E_UNKNOWN = 0x80000000u;
        internal const uint MRS_E_INVALID_PARAMETER = 0x80000001u;
        internal const uint MRS_E_INVALID_OPERATION = 0x80000002u;
        internal const uint MRS_E_WRONG_THREAD = 0x80000003u;
        internal const uint MRS_E_NOTFOUND = 0x80000004u;
        internal const uint MRS_E_INVALID_NATIVE_HANDLE = 0x80000005u;
        internal const uint MRS_E_NOT_INITIALIZED = 0x80000006u;
        internal const uint MRS_E_UNSUPPORTED = 0x80000007u;
        internal const uint MRS_E_OUT_OF_RANGE = 0x80000008u;
        internal const uint MRS_E_BUFFER_TOO_SMALL = 0x80000009u;
        internal const uint MRS_E_PEER_CONNECTION_CLOSED = 0x80000101u;
        internal const uint MRS_E_SCTP_NOT_NEGOTIATED = 0x80000301u;
        internal const uint MRS_E_INVALID_DATA_CHANNEL_ID = 0x80000302u;
        internal const uint MRS_E_INVALID_MEDIA_KIND = 0x80000401u;
        internal const uint MRS_E_AUDIO_RESAMPLING_NOT_SUPPORTED = 0x80000402u;

        public static IntPtr MakeWrapperRef(object wrapper)
        {
            var handle = GCHandle.Alloc(wrapper, GCHandleType.Normal);
            var wrapperRef = GCHandle.ToIntPtr(handle);
            return wrapperRef;
        }

        public static T ToWrapper<T>(IntPtr wrapperRef) where T : class
        {
            var handle = GCHandle.FromIntPtr(wrapperRef);
            var wrapper = (handle.Target as T);
            return wrapper;
        }

        public static void ReleaseWrapperRef(IntPtr wrapperRef)
        {
            var handle = GCHandle.FromIntPtr(wrapperRef);
            handle.Free();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct SdpFilter
        {
            public string CodecName;
            public string ExtraParams;
        }

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsReportLiveObjects")]
        public static unsafe extern uint LibraryReportLiveObjects();

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLibraryUseAudioDeviceModule")]
        public static unsafe extern uint LibraryUseAudioDeviceModule(AudioDeviceModule adm);

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLibraryGetAudioDeviceModule")]
        public static unsafe extern AudioDeviceModule LibraryGetAudioDeviceModule();

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsGetShutdownOptions")]
        public static unsafe extern Library.ShutdownOptionsFlags LibraryGetShutdownOptions();

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsSetShutdownOptions")]
        public static unsafe extern void LibrarySetShutdownOptions(Library.ShutdownOptionsFlags options);

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsForceShutdown")]
        public static unsafe extern void LibraryForceShutdown();

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsSdpForceCodecs")]
        public static unsafe extern uint SdpForceCodecs(string message, SdpFilter audioFilter, SdpFilter videoFilter,
            StringBuilder messageOut, ref ulong messageOutLength);


        /// <summary>
        /// Unsafe utility to copy a contiguous block of memory.
        /// This is equivalent to the C function <c>memcpy()</c>, and is provided for optimization purpose only.
        /// </summary>
        /// <param name="dst">Pointer to the beginning of the destination buffer data is copied to.</param>
        /// <param name="src">Pointer to the beginning of the source buffer data is copied from.</param>
        /// <param name="size">Size of the memory block, in bytes.</param>
        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, EntryPoint = "mrsMemCpy")]
        public static unsafe extern void MemCpy(void* dst, void* src, ulong size);

        /// <summary>
        /// Unsafe utility to copy a memory block with stride.
        ///
        /// This utility loops over the rows of the input memory block, and copy them to the output
        /// memory block, then increment the read and write pointers by the source and destination
        /// strides, respectively. For each row, exactly <paramref name="elem_size"/> bytes are copied,
        /// even if the row stride is higher. The extra bytes in the destination buffer past the row
        /// size until the row stride are left untouched.
        ///
        /// This is equivalent to the following pseudo-code:
        /// <code>
        /// for (int row = 0; row &lt; elem_count; ++row) {
        ///   memcpy(dst, src, elem_size);
        ///   dst += dst_stride;
        ///   src += src_stride;
        /// }
        /// </code>
        /// </summary>
        /// <param name="dst">Pointer to the beginning of the destination buffer data is copied to.</param>
        /// <param name="dst_stride">Stride in bytes of the destination rows. This must be greater than
        /// or equal to the row size <paramref name="elem_size"/>.</param>
        /// <param name="src">Pointer to the beginning of the source buffer data is copied from.</param>
        /// <param name="src_stride">Stride in bytes of the source rows. This must be greater than
        /// or equal to the row size <paramref name="elem_size"/>.</param>
        /// <param name="elem_size">Size of each row, in bytes.</param>
        /// <param name="elem_count">Total number of rows to copy.</param>
        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, EntryPoint = "mrsMemCpyStride")]
        public static unsafe extern void MemCpyStride(void* dst, int dst_stride, void* src, int src_stride,
            int elem_size, int elem_count);

        /// <summary>
        /// Helper to get an exception based on an error code.
        /// </summary>
        /// <param name="res">The error code to turn into an exception.</param>
        /// <returns>The exception corresponding to error code <paramref name="res"/>, or <c>null</c> if
        /// <paramref name="res"/> was <c>MRS_SUCCESS</c>.</returns>
        public static Exception GetExceptionForErrorCode(uint res)
        {
            switch (res)
            {
            case MRS_SUCCESS:
                return null;

            case MRS_E_UNKNOWN:
            default:
                return new Exception();

            case MRS_E_INVALID_PARAMETER:
                return new ArgumentException();

            case MRS_E_INVALID_OPERATION:
                return new InvalidOperationException();

            case MRS_E_WRONG_THREAD:
                return new InvalidOperationException("This method cannot be called on that thread.");

            case MRS_E_NOTFOUND:
                return new Exception("Object not found.");

            case MRS_E_INVALID_NATIVE_HANDLE:
                return new InvalidInteropNativeHandleException();

            case MRS_E_NOT_INITIALIZED:
                return new InvalidOperationException("Object not initialized.");

            case MRS_E_UNSUPPORTED:
                return new NotSupportedException();

            case MRS_E_OUT_OF_RANGE:
                return new ArgumentOutOfRangeException();

            case MRS_E_BUFFER_TOO_SMALL:
                return new BufferTooSmallException();

            case MRS_E_SCTP_NOT_NEGOTIATED:
                return new SctpNotNegotiatedException();

            case MRS_E_PEER_CONNECTION_CLOSED:
                return new InvalidOperationException("The operation cannot complete because the peer connection was closed.");

            case MRS_E_INVALID_DATA_CHANNEL_ID:
                return new ArgumentOutOfRangeException("Invalid ID passed to AddDataChannelAsync().");

            case MRS_E_INVALID_MEDIA_KIND:
                return new InvalidOperationException("Some audio-only function was called on a video-only object or vice-versa.");

            case MRS_E_AUDIO_RESAMPLING_NOT_SUPPORTED:
                return new NotSupportedException("Audio resampling for the given input/output frequencies is not supported. Try requesting a different output frequency.");
            }
        }

        /// <summary>
        /// Helper to throw an exception based on an error code.
        /// </summary>
        /// <param name="res">The error code to turn into an exception, if not zero (MRS_SUCCESS).</param>
        public static void ThrowOnErrorCode(uint res)
        {
            if (res == MRS_SUCCESS)
            {
                return;
            }
            MainEventSource.Log.NativeError(res);
            throw GetExceptionForErrorCode(res);
        }

        /// <summary>
        /// See <see cref="PeerConnection.SetFrameHeightRoundMode(PeerConnection.FrameHeightRoundMode)"/>.
        /// </summary>
        /// <param name="value"></param>
        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, EntryPoint = "mrsSetFrameHeightRoundMode")]
        public static unsafe extern void SetFrameHeightRoundMode(PeerConnection.FrameHeightRoundMode value);

        public static string EncodeTransceiverStreamIDs(List<string> streamIDs)
        {
            if ((streamIDs == null) || (streamIDs.Count == 0))
            {
                return string.Empty;
            }
            return string.Join(";", streamIDs.ToArray());
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct H264Config
        {
            internal int Profile;
            internal int RcMode;
            internal int MaxQp;
            internal int Quality;

            internal H264Config(PeerConnection.H264Config config)
            {
                Profile = (int)config.Profile;
                RcMode = config.RcMode.HasValue ? (int)config.RcMode : -1;
                MaxQp = config.MaxQp.GetValueOrDefault(-1);
                Quality = config.Quality.GetValueOrDefault(-1);
            }
        };

        [DllImport(dllPath, CallingConvention = CallingConvention.StdCall, EntryPoint = "mrsSetH264Config")]
        internal static unsafe extern uint SetH264Config(in H264Config value);
    }
}
