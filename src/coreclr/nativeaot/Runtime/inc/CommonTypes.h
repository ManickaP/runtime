// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __COMMON_TYPES_H__
#define __COMMON_TYPES_H__

#include <cstddef>
#include <cstdint>
#include <stdlib.h>
#include <stdio.h>
#include <new>

#ifdef HOST_WINDOWS
#include <windows.h>
#endif // HOST_WINDOWS

#include <minipal/mutex.h>

// Implement pure virtual for Unix (for -p:LinkStandardCPlusPlusLibrary=false the default),
// to avoid linker requiring __cxa_pure_virtual.
#ifdef TARGET_WINDOWS
#define PURE_VIRTUAL = 0;
#else
// `while(true);` is to satisfy the missing `return` statement. It will be optimized away by the compiler.
#define PURE_VIRTUAL { assert(!"pure virtual function called"); while(true); }
#endif

using std::nothrow;
using std::size_t;
using std::uintptr_t;
using std::intptr_t;


#ifdef TARGET_WINDOWS
typedef wchar_t             WCHAR;
#define W(str) L##str
#else
typedef char16_t            WCHAR;
#define W(str) u##str
#endif
typedef void *              HANDLE;

typedef uint32_t            UInt32_BOOL;    // windows 4-byte BOOL, 0 -> false, everything else -> true
#define UInt32_FALSE        0
#define UInt32_TRUE         1

#if defined(FEATURE_EVENT_TRACE) && defined(TARGET_UNIX)
typedef int BOOL;
typedef void* LPVOID;
typedef uint32_t UINT;
typedef void* PVOID;
typedef uint64_t ULONGLONG;
typedef uintptr_t ULONG_PTR;
typedef uint32_t ULONG;
typedef int64_t LONGLONG;
typedef uint8_t BYTE;
typedef uint16_t UINT16;
#endif // FEATURE_EVENT_TRACE && TARGET_UNIX

// Hijack funcs are not called, they are "returned to". And when done, they return to the actual caller.
// Thus they cannot have any parameters or return anything.
typedef void HijackFunc();

#endif // __COMMON_TYPES_H__
