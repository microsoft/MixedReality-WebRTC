// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

// P/Invoke uses stdcall by default. This can be changed, but Unity's IL2CPP
// does not understand the CallingConvention attribute and instead
// unconditionally forces stdcall. So use stdcall in the API to be compatible.
#if defined(MR_SHARING_WIN)
	#define MRS_API __declspec(dllexport)
	#define MRS_CALL __stdcall
#elif defined(MR_SHARING_ANDROID)
	#define MRS_API __attribute__((visibility("default")))
	#define MRS_CALL __attribute__((stdcall))
#endif
