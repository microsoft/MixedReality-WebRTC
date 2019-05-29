# Overall design

The library is designed to provide easy wrapping in target languages and platforms. At the time of writing, this means essentially C++ and C# for Desktop PC, UWP platforms (HoloLens), and Android.

## Code standard and style

The library takes a dependency on `webrtc.lib`, which already depends on numerous third-party utilities, therefore tries to reuse as much as possible those. This means:
- Reference counting is done with `rtc::scoped_refptr` _et al._, see **Lifetime and ownership** section for details.
- Asynchronous calls, promises, multi-threading events are done with [the `zsLib` library](https://github.com/robin-raymond/zsLib). The WebRTC UWP SDK project plans to deprecate this library in favor of `SW` but at the time of writing `zsLib` is still the only option. Documentation is sparse however.

The library uses `clang-format`. This is the reference and only accepted code formatting.

## API rules

The library provides a set of C functions which can be invoked directly from both C++ and C# (P-invoke). This means that the following restrictions should apply:

- The API uses a set of C functions which can be directly P-invoked. No C++ object or method in the API.
- Functions are exported prefixed with `mrtk_net_webrtc_` to avoid symbol clash; this prefix is omitted below for brievety.
- In general exported function names shall start with the object they work with for discoverability, _e.g._ `PeerConnectionCreate()` instead of `CreatePeerConnection()`.
- Calling convention is C (_i.e._ `__declspec(cdecl)` in MSVC).
- Strings are passed as UTF-8 null-terminated. See the **Strings** section for more details.
- Arguments and return values use simple types and strings which can easily be marshalled.
- Callbacks take an opaque `void* user_data` argument to allow the language wrapper to convert from static function to object method. See the **Callbacks** section more details.
- Functions are designed in such a way as to avoid as much as possible calls during performance intensive operations. For example, try to pass as much data as possible in a single call instead of performing multiple calls. This reduces the overhead of language transition, which can be high in some cases. See _e.g._ the **Enumeration** section.

Those rules apply to the API surface only (`api.h`). Within the library itself, C++ constructs can be used as needed.

## Lifetime and ownership

The library keeps a global collection of all `PeerConnection` objects allocated. All other objects are kept alive via an ownership tree with a `PeerConnection` object at its root.

All objects kept alive by the library itself, that is the collection of `PeerConnection` objects and some extra global implementation objects, are referenced by `rtc::scoped_refptr` such that unloading the library shall release all references and destroy all objects, unless some of them are explicitly kept alive by a language wrapper itself, for example because an asynchronous call did not complete yet.

Note that the WebRTC library uses a partially-intrusive pattern for ref-counting which allow only augmenting the class instance with a reference count variable when needed:
```cpp
class C {}; // nothing special here

// On use, allocate a RefCountedObject<C>, not C itself:
rtc::scoped_refptr<C> obj = new rtc::RefCountedObject<C>();

// Or use C if refcounting not needed:
C* ptr = new C();
```

## Strings

To simplify interop with C#, the intent is that strings be passed in C# as `DllImport(CharSet.Ansi)`, meaning UTF-8 in practice. So conversion happens from the native UTF-16 C# `string` type.

This means that strings passed as arguments are passed as null-terminated `const char*`.

Returning strings from native side back to wrapper side is more problematic. In general try to use a buffer `char*` allocated by the wrapper, with an explicit capacity `const int`, and return the used size `int*`.
```cpp
int GetString(char* buffer, const int capacity);
```

## Callbacks

Callbacks registered with the native plugin always feature an opaque `void* user_data` allowing the language wrapper to implement object method calls or delegates.

On the native side, the library makes use of the `Callback<>` utility class to pack those two together for convenience:
```cpp
Callback<int> cb { callback_ptr, user_data };
cb(-42); // == (*callback_ptr)(user_data, -42)
```

The `Callback<>` utility offers the extra advantage to silently ignore calls on a `nullptr` callback.
```cpp
void (*fnptr)(int) = nullptr;
Callback<int> cb { fnptr, nullptr };
cb(-42);                // safe, no-op
(*fnptr)(nullptr, -42); // access violation
``` 

## Enumeration

The library should try to batch enumerations to avoid either a call per enumerated item, or having to pre-allocate overly large buffers when the number of item to enumerate can vary widely.

The WinMD library solves this problem by using a caller-provided buffer and an enumerator handle to continue enumeration (_e.g._ [`IMetaDataImport::EnumMembers()` here](https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/metadata/imetadataimport-enummembers-method)):
```cpp
HCORENUM handle = nullptr;
mdTypeDef dummy = ...;
mdToken buffer[10];
ULONG usedCount;
while (mdi->EnumMembers(&handle, dummy, buffer, 10, &usedCount) == S_OK) {
    for (int i = 0; i < usedCount; ++i) {
        mdToken next_token = buffer[i];
        // use next_token
    }
}
mdi->CloseEnum(handle);
```

This allows the caller to adjust the capacity of the buffer (`10` in the example above) depending on the expected number of items to enumerate.

This library uses a simmilar pattern with the opaque `Enumerator` and its handle `EnumHandle` (Note: `mrtk_net_webrtc_` prefix removed for brievety):
```cpp
// First call to EnumT() will allocate an enumerator object,
// which keeps track of the next item(s) to yield on next call
EnumHandle handle = nullptr; 
T buffer[10];
int num_T;
while (EnumT(&handle, buffer, 10, &num_T) == 0) {
    for (int i = 0; i < num_T; ++i) {
        T& obj = buffer[i];
        // use |obj|
    }
}
CloseEnum(handle); // deallocates the enumerator impl
```

# Network session

The `WebRTCNetworkSession` class implements `INetworkSession` in terms of a [WebRTC peer connection](https://w3c.github.io/webrtc-pc/#rtcpeerconnection-interface).

WebRTC requires negotiating streams while opening a connection. That is, a connection cannot be established without any stream (this is partially untrue with [warm-up](https://w3c.github.io/webrtc-pc/#advanced-peer-to-peer-example-with-warm-up), but is really not a core feature of WebRTC). Conversely, the `INetworkSession` interface assumes a model where the user joins the session, then can add/remove streams as needed. A WebRTC peer is also uniquely and solely identified by the external signaling mechanism, which currently only uses a unique peer identifier (see `ISignaler.LocalId`), whereas `INetworkSession` assumes some basic properties of a participant like a friendly user name. 

In order to integrate those two approaches, we introduce a concept of _control channel_. The control channel is a WebRTC data channel automatically opened by the implementation and hidden from the user, with two roles:
- Materialize the session connection: because the `INetworkSession` interface does not assume any channel open while connected, or said otherwise allows session connection without any channel, we use the hidden control channel to force at least one WebRTC channel and allow WebRTC to establish a peer connection, while maintaining the illusion that there is no visible channel in use until requested by the user.
- Provide support for intrinsic properties: because the `INetworkSession` interface assumes some intrinsic properties of participants that are not available from the WebRTC protocol itself, like the friendly peer name, the control channel is used to share those intrinsic properties automatically without user intervention. (TODO: not used at the time of writing)