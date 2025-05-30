/**
 * \file
 * Atomic operations
 *
 * Author:
 *	Dick Porter (dick@ximian.com)
 *
 * (C) 2002 Ximian, Inc.
 * Copyright 2012 Xamarin Inc
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

#ifndef _WAPI_ATOMIC_H_
#define _WAPI_ATOMIC_H_

#include "config.h"
#include <glib.h>
#include <mono/utils/mono-memory-model.h>
#include <mono/utils/mono-compiler.h>

/*
The current Nexus 7 arm-v7a fails with:
F/MonoDroid( 1568): shared runtime initialization error: Cannot load library: reloc_library[1285]:    37 cannot locate '__sync_val_compare_and_swap_8'

Apple targets have historically being problematic, xcode 4.6 would miscompile the intrinsic.
*/

/* For each platform, decide what atomic implementation to use.
 *
 * Generally, we can enable C11 atomics if the header is available and if all the primitive types we
 * care about (int, long, void*, long long) are lock-free.
 *
 * Note that we generally don't want the compiler's locking implementation because it may take a
 * global lock, in which case if the atomic is used by both the GC implementation and runtime
 * internals we may have deadlocks during GC suspend.
 *
 * It might be possible to use some Mono specific implementation for specific types (e.g. long long)
 * on some platforms if the standard atomics for some type are not lock-free (for example: long
 * long).  We might be able to use a GC-aware lock, for example.
 *
 */
#undef MONO_USE_C11_ATOMIC
#undef MONO_USE_WIN32_ATOMIC
#undef MONO_USE_GCC_ATOMIC
#undef MONO_USE_EMULATED_ATOMIC

#if defined(MONO_GENERATING_OFFSETS)
  /*
   * Hack: for the offsets tool, define MONO_USE_EMULATED_ATOMIC since it doesn't actually need to see
   * the impementation, and the stdatomic ones cause problems on some Linux configurations where
   * libclang sees the platform header, not the clang one.
   */
#  define MONO_USE_EMULATED_ATOMIC 1
#elif defined(HOST_WIN32)
  /*
   * we need two things to switch to C11 atomics on Windows:
   *
   * 1. MSVC atomics support is not experimental, or we pass /experimental:c11atomics
   *
   * 2. We build our C++ code with C++23 or later (otherwise MSVC will complain about including
   * stdatomic.h)
   *
   */
#  if defined(_MSC_VER)
#    define MONO_USE_WIN32_ATOMIC 1
#  else
#    error FIXME: Implement atomics for MinGW and/or clang
#  endif
#elif defined(HOST_IOS) || defined(HOST_OSX) || defined(HOST_WATCHOS) || defined(HOST_TVOS)
#  define MONO_USE_C11_ATOMIC 1
#elif defined(HOST_ANDROID)
  /* on Android-x86 ATOMIC_LONG_LONG_LOCK_FREE == 1, not 2 like we want. */
  /* on Andriod-x64 ATOMIC_LONG_LOCK_FREE == 1, not 2 */
  /* on Android-armv7 ATOMIC_INT_LOCK_FREE == 1, not 2 */
#  if defined(HOST_ARM64)
#    define MONO_USE_C11_ATOMIC 1
#  elif defined(USE_GCC_ATOMIC_OPS)
#    define MONO_USE_GCC_ATOMIC 1
#  else
#    define MONO_USE_EMULATED_ATOMIC 1
#  endif
#elif defined(HOST_LINUX)
  /* FIXME: probably need arch checks */
#  define MONO_USE_C11_ATOMIC 1
#elif defined(HOST_WASI) || defined(HOST_BROWSER)
#  define MONO_USE_C11_ATOMIC 1
#elif defined(USE_GCC_ATOMIC_OPS)
/* Prefer GCC atomic ops if the target supports it (see configure.ac). */
#  define MONO_USE_GCC_ATOMIC 1
#else
#  define MONO_USE_EMULATED_ATOMIC 1
#endif

#if defined(MONO_USE_C11_ATOMIC)

#include <stdatomic.h>

#if defined(HOST_ARM64)
// C11 atomics on ARM64 offers a weaker version of sequential consistent, not expected by mono atomics operations.
// C11 seq_cst on ARM64 corresponds to acquire/release semantics, but mono expects these functions to emit a full memory
// barrier preventing any kind of reordering around the atomic operation. GCC atomics on ARM64 had similar limitations,
// see comments on GCC atomics below and mono injected full memory barriers around GCC atomic functions to mitigate this.
// Since mono GCC atomics implementation ended up even stronger (full memory barrier before/after), the C11 atomics
// implementation is still a little weaker, but should correspond to the exact same semantics as implemented by JIT
// compiler for sequential consistent atomic load/store/add/exchange/cas op codes on ARM64.
#define C11_MEMORY_ORDER_SEQ_CST() atomic_thread_fence (memory_order_seq_cst)
#else
#define C11_MEMORY_ORDER_SEQ_CST()
#endif

static inline guint8
mono_atomic_cas_u8 (volatile guint8 *dest, guint8 exch, guint8 comp)
{
	g_static_assert (sizeof (atomic_char) == sizeof (*dest) && ATOMIC_CHAR_LOCK_FREE == 2);
	(void)atomic_compare_exchange_strong ((volatile atomic_char *)dest, (char*)&comp, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return comp;
}

static inline guint16
mono_atomic_cas_u16 (volatile guint16 *dest, guint16 exch, guint16 comp)
{
	g_static_assert (sizeof (atomic_short) == sizeof (*dest) && ATOMIC_SHORT_LOCK_FREE == 2);
	(void)atomic_compare_exchange_strong ((volatile atomic_short *)dest, (short*)&comp, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return comp;
}

static inline gint32
mono_atomic_cas_i32 (volatile gint32 *dest, gint32 exch, gint32 comp)
{
	g_static_assert (sizeof (atomic_int) == sizeof (*dest) && ATOMIC_INT_LOCK_FREE == 2);
	(void)atomic_compare_exchange_strong ((volatile atomic_int *)dest, &comp, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return comp;
}

static inline gint64
mono_atomic_cas_i64 (volatile gint64 *dest, gint64 exch, gint64 comp)
{
#if SIZEOF_LONG == 8
	g_static_assert (sizeof (atomic_long) == sizeof (*dest) && ATOMIC_LONG_LOCK_FREE == 2);
	(void)atomic_compare_exchange_strong ((volatile atomic_long *)dest, (long*)&comp, exch);
#elif SIZEOF_LONG_LONG == 8
	g_static_assert (sizeof (atomic_llong) == sizeof (*dest) && ATOMIC_LLONG_LOCK_FREE == 2);
	(void)atomic_compare_exchange_strong ((volatile atomic_llong *)dest, (long long*)&comp, exch);
#else
#error "gint64 not same size atomic_llong or atomic_long, don't define MONO_USE_STDATOMIC"
#endif
	C11_MEMORY_ORDER_SEQ_CST ();
	return comp;
}

static inline gpointer
mono_atomic_cas_ptr (volatile gpointer *dest, gpointer exch, gpointer comp)
{
	g_static_assert(ATOMIC_POINTER_LOCK_FREE == 2);
	(void)atomic_compare_exchange_strong ((volatile _Atomic(gpointer) *)dest, &comp, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return comp;
}

static inline gint32
mono_atomic_fetch_add_i32 (volatile gint32 *dest, gint32 add);
static inline gint64
mono_atomic_fetch_add_i64 (volatile gint64 *dest, gint64 add);

static inline gint32
mono_atomic_add_i32 (volatile gint32 *dest, gint32 add)
{
	// mono_atomic_add_ is supposed to return the value that is stored.
	// the atomic_add intrinsic returns the previous value instead.
	// so we return prev+add which should be the new value
	return mono_atomic_fetch_add_i32 (dest, add) + add;
}

static inline gint64
mono_atomic_add_i64 (volatile gint64 *dest, gint64 add)
{
	return mono_atomic_fetch_add_i64 (dest, add) + add;
}

static inline gint32
mono_atomic_inc_i32 (volatile gint32 *dest)
{
	return mono_atomic_add_i32 (dest, 1);
}

static inline gint64
mono_atomic_inc_i64 (volatile gint64 *dest)
{
	return mono_atomic_add_i64 (dest, 1);
}

static inline gint32
mono_atomic_dec_i32 (volatile gint32 *dest)
{
	return mono_atomic_add_i32 (dest, -1);
}

static inline gint64
mono_atomic_dec_i64 (volatile gint64 *dest)
{
	return mono_atomic_add_i64 (dest, -1);
}

static inline guint8
mono_atomic_xchg_u8 (volatile guint8 *dest, guint8 exch)
{
	g_static_assert (sizeof (atomic_char) == sizeof (*dest) && ATOMIC_CHAR_LOCK_FREE == 2);
	guint8 old = atomic_exchange ((volatile atomic_char *)dest, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline guint16
mono_atomic_xchg_u16 (volatile guint16 *dest, guint16 exch)
{
	g_static_assert (sizeof (atomic_short) == sizeof (*dest) && ATOMIC_SHORT_LOCK_FREE == 2);
	guint16 old = atomic_exchange ((volatile atomic_short *)dest, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline gint32
mono_atomic_xchg_i32 (volatile gint32 *dest, gint32 exch)
{
	g_static_assert (sizeof (atomic_int) == sizeof (*dest) && ATOMIC_INT_LOCK_FREE == 2);
	gint32 old = atomic_exchange ((volatile atomic_int *)dest, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline gint64
mono_atomic_xchg_i64 (volatile gint64 *dest, gint64 exch)
{
#if SIZEOF_LONG == 8
	g_static_assert (sizeof (atomic_long) == sizeof (*dest) && ATOMIC_LONG_LOCK_FREE == 2);
	gint64 old = atomic_exchange ((volatile atomic_long *)dest, exch);
#elif SIZEOF_LONG_LONG == 8
	g_static_assert (sizeof (atomic_llong) == sizeof (*dest) && ATOMIC_LLONG_LOCK_FREE == 2);
	gint64 old = atomic_exchange ((volatile atomic_llong *)dest, exch);
#else
#error "gint64 not same size atomic_llong or atomic_long, don't define MONO_USE_STDATOMIC"
#endif
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline gpointer
mono_atomic_xchg_ptr (volatile gpointer *dest, gpointer exch)
{
	g_static_assert (ATOMIC_POINTER_LOCK_FREE == 2);
	gpointer old = atomic_exchange ((volatile _Atomic(gpointer) *)dest, exch);
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline gint32
mono_atomic_fetch_add_i32 (volatile gint32 *dest, gint32 add)
{
	g_static_assert (sizeof (atomic_int) == sizeof (*dest) && ATOMIC_INT_LOCK_FREE == 2);
	gint32 old = atomic_fetch_add ((volatile atomic_int *)dest, add);
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline gint64
mono_atomic_fetch_add_i64 (volatile gint64 *dest, gint64 add)
{
#if SIZEOF_LONG == 8
	g_static_assert (sizeof (atomic_long) == sizeof (*dest) && ATOMIC_LONG_LOCK_FREE == 2);
	gint64 old = atomic_fetch_add ((volatile atomic_long *)dest, add);
#elif SIZEOF_LONG_LONG == 8
	g_static_assert (sizeof (atomic_llong) == sizeof (*dest) && ATOMIC_LLONG_LOCK_FREE == 2);
	gint64 old = atomic_fetch_add ((volatile atomic_llong *)dest, add);
#else
#error "gint64 not same size atomic_llong or atomic_long, don't define MONO_USE_STDATOMIC"
#endif
	C11_MEMORY_ORDER_SEQ_CST ();
	return old;
}

static inline gint8
mono_atomic_load_i8 (volatile gint8 *src)
{
	g_static_assert (sizeof (atomic_char) == sizeof (*src) && ATOMIC_CHAR_LOCK_FREE == 2);
	C11_MEMORY_ORDER_SEQ_CST ();
	gint8 val = atomic_load ((volatile atomic_char *)src);
	return val;
}

static inline gint16
mono_atomic_load_i16 (volatile gint16 *src)
{
	g_static_assert (sizeof (atomic_short) == sizeof (*src) && ATOMIC_SHORT_LOCK_FREE == 2);
	C11_MEMORY_ORDER_SEQ_CST ();
	gint16 val = atomic_load ((volatile atomic_short *)src);
	return val;
}

static inline gint32 mono_atomic_load_i32 (volatile gint32 *src)
{
	g_static_assert (sizeof (atomic_int) == sizeof (*src) && ATOMIC_INT_LOCK_FREE == 2);
	C11_MEMORY_ORDER_SEQ_CST ();
	gint32 val = atomic_load ((volatile atomic_int *)src);
	return val;
}

static inline gint64
mono_atomic_load_i64 (volatile gint64 *src)
{
#if SIZEOF_LONG == 8
	g_static_assert (sizeof (atomic_long) == sizeof (*src) && ATOMIC_LONG_LOCK_FREE == 2);
	C11_MEMORY_ORDER_SEQ_CST ();
	gint64 val = atomic_load ((volatile atomic_long *)src);
#elif SIZEOF_LONG_LONG == 8
	g_static_assert (sizeof (atomic_llong) == sizeof (*src) && ATOMIC_LLONG_LOCK_FREE == 2);
	C11_MEMORY_ORDER_SEQ_CST ();
	gint64 val = atomic_load ((volatile atomic_llong *)src);
#else
#error "gint64 not same size atomic_llong or atomic_long, don't define MONO_USE_STDATOMIC"
#endif
	return val;
}

static inline gpointer
mono_atomic_load_ptr (volatile gpointer *src)
{
	g_static_assert (ATOMIC_POINTER_LOCK_FREE == 2);
	C11_MEMORY_ORDER_SEQ_CST ();
	gpointer val = atomic_load ((volatile _Atomic(gpointer) *)src);
	return val;
}

static inline void
mono_atomic_store_i8 (volatile gint8 *dst, gint8 val)
{
	g_static_assert (sizeof (atomic_char) == sizeof (*dst) && ATOMIC_CHAR_LOCK_FREE == 2);
	atomic_store ((volatile atomic_char *)dst, val);
	C11_MEMORY_ORDER_SEQ_CST ();
}

static inline void
mono_atomic_store_i16 (volatile gint16 *dst, gint16 val)
{
	g_static_assert (sizeof (atomic_short) == sizeof (*dst) && ATOMIC_SHORT_LOCK_FREE == 2);
	atomic_store ((volatile atomic_short *)dst, val);
	C11_MEMORY_ORDER_SEQ_CST ();
}

static inline void
mono_atomic_store_i32 (volatile gint32 *dst, gint32 val)
{
	g_static_assert (sizeof (atomic_int) == sizeof (*dst) && ATOMIC_INT_LOCK_FREE == 2);
	atomic_store ((atomic_int *)dst, val);
	C11_MEMORY_ORDER_SEQ_CST ();
}

static inline void
mono_atomic_store_i64 (volatile gint64 *dst, gint64 val)
{
#if SIZEOF_LONG == 8
	g_static_assert (sizeof (atomic_long) == sizeof (*dst) && ATOMIC_LONG_LOCK_FREE == 2);
	atomic_store ((volatile atomic_long *)dst, val);
#elif SIZEOF_LONG_LONG == 8
	g_static_assert (sizeof (atomic_llong) == sizeof (*dst) && ATOMIC_LLONG_LOCK_FREE == 2);
	atomic_store ((volatile atomic_llong *)dst, val);
#else
#error "gint64 not same size atomic_llong or atomic_long, don't define MONO_USE_STDATOMIC"
#endif
	C11_MEMORY_ORDER_SEQ_CST ();
}

static inline void
mono_atomic_store_ptr (volatile gpointer *dst, gpointer val)
{
	g_static_assert (ATOMIC_POINTER_LOCK_FREE == 2);
	atomic_store ((volatile _Atomic(gpointer) *)dst, val);
	C11_MEMORY_ORDER_SEQ_CST ();
}

#elif defined(MONO_USE_WIN32_ATOMIC)

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <intrin.h>

static inline guint8
mono_atomic_cas_u8 (volatile guint8 *dest, guint8 exch, guint8 comp)
{
	return _InterlockedCompareExchange8 ((char volatile *)dest, (char)exch, (char)comp);
}

static inline guint16
mono_atomic_cas_u16 (volatile guint16 *dest, guint16 exch, guint16 comp)
{
	return _InterlockedCompareExchange16 ((short volatile *)dest, (short)exch, (short)comp);
}

static inline gint32
mono_atomic_cas_i32 (volatile gint32 *dest, gint32 exch, gint32 comp)
{
	return _InterlockedCompareExchange ((LONG volatile *)dest, (LONG)exch, (LONG)comp);
}

static inline gint64
mono_atomic_cas_i64 (volatile gint64 *dest, gint64 exch, gint64 comp)
{
	return InterlockedCompareExchange64 ((LONG64 volatile *)dest, (LONG64)exch, (LONG64)comp);
}

static inline gpointer
mono_atomic_cas_ptr (volatile gpointer *dest, gpointer exch, gpointer comp)
{
	return InterlockedCompareExchangePointer ((PVOID volatile *)dest, (PVOID)exch, (PVOID)comp);
}

static inline gint32
mono_atomic_add_i32 (volatile gint32 *dest, gint32 add)
{
	return InterlockedAdd ((LONG volatile *)dest, (LONG)add);
}

static inline gint64
mono_atomic_add_i64 (volatile gint64 *dest, gint64 add)
{
	return InterlockedAdd64 ((LONG64 volatile *)dest, (LONG64)add);
}

static inline gint32
mono_atomic_inc_i32 (volatile gint32 *dest)
{
	return InterlockedIncrement ((LONG volatile *)dest);
}

static inline gint64
mono_atomic_inc_i64 (volatile gint64 *dest)
{
	return InterlockedIncrement64 ((LONG64 volatile *)dest);
}

static inline gint32
mono_atomic_dec_i32 (volatile gint32 *dest)
{
	return InterlockedDecrement ((LONG volatile *)dest);
}

static inline gint64
mono_atomic_dec_i64 (volatile gint64 *dest)
{
	return InterlockedDecrement64 ((LONG64 volatile *)dest);
}

static inline guint8
mono_atomic_xchg_u8 (volatile guint8 *dest, guint8 exch)
{
	return _InterlockedExchange8 ((char volatile *)dest, (char)exch);
}

static inline guint16
mono_atomic_xchg_u16 (volatile guint16 *dest, guint16 exch)
{
	return _InterlockedExchange16 ((short volatile *)dest, (short)exch);
}

static inline gint32
mono_atomic_xchg_i32 (volatile gint32 *dest, gint32 exch)
{
	return _InterlockedExchange ((LONG volatile *)dest, (LONG)exch);
}

static inline gint64
mono_atomic_xchg_i64 (volatile gint64 *dest, gint64 exch)
{
	return InterlockedExchange64 ((LONG64 volatile *)dest, (LONG64)exch);
}

static inline gpointer
mono_atomic_xchg_ptr (volatile gpointer *dest, gpointer exch)
{
	return InterlockedExchangePointer ((PVOID volatile *)dest, (PVOID)exch);
}

static inline gint32
mono_atomic_fetch_add_i32 (volatile gint32 *dest, gint32 add)
{
	return InterlockedExchangeAdd ((LONG volatile *)dest, (LONG)add);
}

static inline gint64
mono_atomic_fetch_add_i64 (volatile gint64 *dest, gint64 add)
{
	return InterlockedExchangeAdd64 ((LONG64 volatile *)dest, (LONG64)add);
}

static inline gint8
mono_atomic_load_i8 (volatile gint8 *src)
{
	gint8 loaded_value = *src;
	_ReadWriteBarrier ();

	return loaded_value;
}

static inline gint16
mono_atomic_load_i16 (volatile gint16 *src)
{
	gint16 loaded_value = *src;
	_ReadWriteBarrier ();

	return loaded_value;
}

static inline gint32 mono_atomic_load_i32 (volatile gint32 *src)
{
	gint32 loaded_value = *src;
	_ReadWriteBarrier ();

	return loaded_value;
}

static inline gint64
mono_atomic_load_i64 (volatile gint64 *src)
{
#if defined(TARGET_AMD64)
	gint64 loaded_value = *src;
	_ReadWriteBarrier ();

	return loaded_value;
#else
	return InterlockedCompareExchange64 ((LONG64 volatile *)src, 0, 0);
#endif
}

static inline gpointer
mono_atomic_load_ptr (volatile gpointer *src)
{
	gpointer loaded_value = *src;
	_ReadWriteBarrier ();

	return loaded_value;
}

static inline void
mono_atomic_store_i8 (volatile gint8 *dst, gint8 val)
{
	_InterlockedExchange8 ((CHAR volatile *)dst, (CHAR)val);
}

static inline void
mono_atomic_store_i16 (volatile gint16 *dst, gint16 val)
{
	_InterlockedExchange16 ((SHORT volatile *)dst, (SHORT)val);
}

static inline void
mono_atomic_store_i32 (volatile gint32 *dst, gint32 val)
{
	_InterlockedExchange ((LONG volatile *)dst, (LONG)val);
}

static inline void
mono_atomic_store_i64 (volatile gint64 *dst, gint64 val)
{
	InterlockedExchange64 ((LONG64 volatile *)dst, (LONG64)val);
}

static inline void
mono_atomic_store_ptr (volatile gpointer *dst, gpointer val)
{
	InterlockedExchangePointer ((PVOID volatile *)dst, (PVOID)val);
}

#elif defined(MONO_USE_GCC_ATOMIC)

/*
 * As of this comment (August 2016), all current Clang versions get atomic
 * intrinsics on ARM64 wrong. All GCC versions prior to 5.3.0 do, too. The bug
 * is the same: The compiler developers thought that the acq + rel barriers
 * that ARM64 load/store instructions can impose are sufficient to provide
 * sequential consistency semantics. This is not the case:
 *
 *     http://lists.infradead.org/pipermail/linux-arm-kernel/2014-February/229588.html
 *
 * We work around this bug by inserting full barriers around each atomic
 * intrinsic if we detect that we're built with a buggy compiler.
 */

#if defined (HOST_ARM64) && (defined (__clang__) || MONO_GNUC_VERSION < 50300)
#define WRAP_ATOMIC_INTRINSIC(INTRIN) \
	({ \
		mono_memory_barrier (); \
		__typeof__ (INTRIN) atomic_ret__ = (INTRIN); \
		mono_memory_barrier (); \
		atomic_ret__; \
	})

#define gcc_sync_val_compare_and_swap(a, b, c) WRAP_ATOMIC_INTRINSIC (__sync_val_compare_and_swap (a, b, c))
#define gcc_sync_add_and_fetch(a, b) WRAP_ATOMIC_INTRINSIC (__sync_add_and_fetch (a, b))
#define gcc_sync_sub_and_fetch(a, b) WRAP_ATOMIC_INTRINSIC (__sync_sub_and_fetch (a, b))
#define gcc_sync_fetch_and_add(a, b) WRAP_ATOMIC_INTRINSIC (__sync_fetch_and_add (a, b))
#else
#define gcc_sync_val_compare_and_swap(a, b, c) __sync_val_compare_and_swap (a, b, c)
#define gcc_sync_add_and_fetch(a, b) __sync_add_and_fetch (a, b)
#define gcc_sync_sub_and_fetch(a, b) __sync_sub_and_fetch (a, b)
#define gcc_sync_fetch_and_add(a, b) __sync_fetch_and_add (a, b)
#endif

static inline guint8 mono_atomic_cas_u8(volatile guint8 *dest,
						guint8 exch, guint8 comp)
{
	return gcc_sync_val_compare_and_swap (dest, comp, exch);
}

static inline guint16 mono_atomic_cas_u16(volatile guint16 *dest,
						guint16 exch, guint16 comp)
{
	return gcc_sync_val_compare_and_swap (dest, comp, exch);
}

static inline gint32 mono_atomic_cas_i32(volatile gint32 *dest,
						gint32 exch, gint32 comp)
{
	return gcc_sync_val_compare_and_swap (dest, comp, exch);
}

static inline gpointer mono_atomic_cas_ptr(volatile gpointer *dest, gpointer exch, gpointer comp)
{
	return gcc_sync_val_compare_and_swap (dest, comp, exch);
}

static inline gint32 mono_atomic_add_i32(volatile gint32 *dest, gint32 add)
{
	return gcc_sync_add_and_fetch (dest, add);
}

static inline gint32 mono_atomic_inc_i32(volatile gint32 *val)
{
	return gcc_sync_add_and_fetch (val, 1);
}

static inline gint32 mono_atomic_dec_i32(volatile gint32 *val)
{
	return gcc_sync_sub_and_fetch (val, 1);
}

static inline guint8 mono_atomic_xchg_u8(volatile guint8 *val, guint8 new_val)
{
	guint8 old_val;
	do {
		old_val = *val;
	} while (gcc_sync_val_compare_and_swap (val, old_val, new_val) != old_val);
	return old_val;
}

static inline guint16 mono_atomic_xchg_u16(volatile guint16 *val, guint16 new_val)
{
	guint16 old_val;
	do {
		old_val = *val;
	} while (gcc_sync_val_compare_and_swap (val, old_val, new_val) != old_val);
	return old_val;
}

static inline gint32 mono_atomic_xchg_i32(volatile gint32 *val, gint32 new_val)
{
	gint32 old_val;
	do {
		old_val = *val;
	} while (gcc_sync_val_compare_and_swap (val, old_val, new_val) != old_val);
	return old_val;
}

static inline gpointer mono_atomic_xchg_ptr(volatile gpointer *val,
						  gpointer new_val)
{
	gpointer old_val;
	do {
		old_val = *val;
	} while (gcc_sync_val_compare_and_swap (val, old_val, new_val) != old_val);
	return old_val;
}

static inline gint32 mono_atomic_fetch_add_i32(volatile gint32 *val, gint32 add)
{
	return gcc_sync_fetch_and_add (val, add);
}

static inline gint8 mono_atomic_load_i8(volatile gint8 *src)
{
	/* Kind of a hack, but GCC doesn't give us anything better, and it's
	 * certainly not as bad as using a CAS loop. */
	return gcc_sync_fetch_and_add (src, 0);
}

static inline gint16 mono_atomic_load_i16(volatile gint16 *src)
{
	return gcc_sync_fetch_and_add (src, 0);
}

static inline gint32 mono_atomic_load_i32(volatile gint32 *src)
{
	return gcc_sync_fetch_and_add (src, 0);
}

static inline void mono_atomic_store_i8(volatile gint8 *dst, gint8 val)
{
	/* Nothing useful from GCC at all, so fall back to CAS. */
	gint8 old_val;
	do {
		old_val = *dst;
	} while (gcc_sync_val_compare_and_swap (dst, old_val, val) != old_val);
}

static inline void mono_atomic_store_i16(volatile gint16 *dst, gint16 val)
{
	gint16 old_val;
	do {
		old_val = *dst;
	} while (gcc_sync_val_compare_and_swap (dst, old_val, val) != old_val);
}

static inline void mono_atomic_store_i32(volatile gint32 *dst, gint32 val)
{
	/* Nothing useful from GCC at all, so fall back to CAS. */
	gint32 old_val;
	do {
		old_val = *dst;
	} while (gcc_sync_val_compare_and_swap (dst, old_val, val) != old_val);
}

#if defined (TARGET_OSX) || defined (__arm__) || (defined (__powerpc__) && !defined (__powerpc64__))
#define BROKEN_64BIT_ATOMICS_INTRINSIC 1
#endif

#if !defined (BROKEN_64BIT_ATOMICS_INTRINSIC)

static inline gint64 mono_atomic_cas_i64(volatile gint64 *dest, gint64 exch, gint64 comp)
{
	return gcc_sync_val_compare_and_swap (dest, comp, exch);
}

static inline gint64 mono_atomic_add_i64(volatile gint64 *dest, gint64 add)
{
	return gcc_sync_add_and_fetch (dest, add);
}

static inline gint64 mono_atomic_inc_i64(volatile gint64 *val)
{
	return gcc_sync_add_and_fetch (val, 1);
}

static inline gint64 mono_atomic_dec_i64(volatile gint64 *val)
{
	return gcc_sync_sub_and_fetch (val, 1);
}

static inline gint64 mono_atomic_fetch_add_i64(volatile gint64 *val, gint64 add)
{
	return gcc_sync_fetch_and_add (val, add);
}

static inline gint64 mono_atomic_load_i64(volatile gint64 *src)
{
	/* Kind of a hack, but GCC doesn't give us anything better. */
	return gcc_sync_fetch_and_add (src, 0);
}

#else

/* Implement 64-bit cas by hand or emulate it. */
MONO_COMPONENT_API gint64 mono_atomic_cas_i64(volatile gint64 *dest, gint64 exch, gint64 comp);

/* Implement all other 64-bit atomics in terms of a specialized CAS
 * in this case, since chances are that the other 64-bit atomic
 * intrinsics are broken too.
 */

static inline gint64 mono_atomic_fetch_add_i64(volatile gint64 *dest, gint64 add)
{
	gint64 old_val;
	do {
		old_val = *dest;
	} while (mono_atomic_cas_i64 (dest, old_val + add, old_val) != old_val);
	return old_val;
}

static inline gint64 mono_atomic_inc_i64(volatile gint64 *val)
{
	gint64 get, set;
	do {
		get = *val;
		set = get + 1;
	} while (mono_atomic_cas_i64 (val, set, get) != get);
	return set;
}

static inline gint64 mono_atomic_dec_i64(volatile gint64 *val)
{
	gint64 get, set;
	do {
		get = *val;
		set = get - 1;
	} while (mono_atomic_cas_i64 (val, set, get) != get);
	return set;
}

static inline gint64 mono_atomic_add_i64(volatile gint64 *dest, gint64 add)
{
	gint64 get, set;
	do {
		get = *dest;
		set = get + add;
	} while (mono_atomic_cas_i64 (dest, set, get) != get);
	return set;
}

static inline gint64 mono_atomic_load_i64(volatile gint64 *src)
{
	return mono_atomic_cas_i64 (src, 0, 0);
}

#endif

static inline gpointer mono_atomic_load_ptr(volatile gpointer *src)
{
	return mono_atomic_cas_ptr (src, NULL, NULL);
}

static inline void mono_atomic_store_ptr(volatile gpointer *dst, gpointer val)
{
	mono_atomic_xchg_ptr (dst, val);
}

/* We always implement this in terms of a 64-bit cas since
 * GCC doesn't have an intrisic to model it anyway. */
static inline gint64 mono_atomic_xchg_i64(volatile gint64 *val, gint64 new_val)
{
	gint64 old_val;
	do {
		old_val = *val;
	} while (mono_atomic_cas_i64 (val, new_val, old_val) != old_val);
	return old_val;
}

static inline void mono_atomic_store_i64(volatile gint64 *dst, gint64 val)
{
	/* Nothing useful from GCC at all, so fall back to CAS. */
	mono_atomic_xchg_i64 (dst, val);
}

#elif defined(MONO_USE_EMULATED_ATOMIC)

#define WAPI_NO_ATOMIC_ASM

/* Fallbacks seem to not be used anymore, they should be removed. */
/* extern guint8 mono_atomic_cas_u8(volatile guint8 *dest, guint8 exch, guint8 comp); */
/* extern guint16 mono_atomic_cas_u16(volatile guint16 *dest, guint16 exch, guint16 comp); */
extern gint32 mono_atomic_cas_i32(volatile gint32 *dest, gint32 exch, gint32 comp);
extern gint64 mono_atomic_cas_i64(volatile gint64 *dest, gint64 exch, gint64 comp);
extern gpointer mono_atomic_cas_ptr(volatile gpointer *dest, gpointer exch, gpointer comp);
extern gint32 mono_atomic_add_i32(volatile gint32 *dest, gint32 add);
extern gint64 mono_atomic_add_i64(volatile gint64 *dest, gint64 add);
extern gint32 mono_atomic_inc_i32(volatile gint32 *dest);
extern gint64 mono_atomic_inc_i64(volatile gint64 *dest);
extern gint32 mono_atomic_dec_i32(volatile gint32 *dest);
extern gint64 mono_atomic_dec_i64(volatile gint64 *dest);
/*extern guint8 mono_atomic_xchg_u8(volatile guint8 *dest, guint8 exch); */
/*extern guint16 mono_atomic_xchg_u16(volatile guint16 *dest, guint16 exch); */
extern gint32 mono_atomic_xchg_i32(volatile gint32 *dest, gint32 exch);
extern gint64 mono_atomic_xchg_i64(volatile gint64 *dest, gint64 exch);
extern gpointer mono_atomic_xchg_ptr(volatile gpointer *dest, gpointer exch);
extern gint32 mono_atomic_fetch_add_i32(volatile gint32 *dest, gint32 add);
extern gint64 mono_atomic_fetch_add_i64(volatile gint64 *dest, gint64 add);
extern gint8 mono_atomic_load_i8(volatile gint8 *src);
extern gint16 mono_atomic_load_i16(volatile gint16 *src);
extern gint32 mono_atomic_load_i32(volatile gint32 *src);
extern gint64 mono_atomic_load_i64(volatile gint64 *src);
extern gpointer mono_atomic_load_ptr(volatile gpointer *src);
extern void mono_atomic_store_i8(volatile gint8 *dst, gint8 val);
extern void mono_atomic_store_i16(volatile gint16 *dst, gint16 val);
extern void mono_atomic_store_i32(volatile gint32 *dst, gint32 val);
extern void mono_atomic_store_i64(volatile gint64 *dst, gint64 val);
extern void mono_atomic_store_ptr(volatile gpointer *dst, gpointer val);

#else
#error one of MONO_USE_C11_ATOMIC, MONO_USE_WIN32_ATOMIC, MONO_USE_GCC_ATOMIC or MONO_USE_EMULATED_ATOMIC must be defined
#endif

#if SIZEOF_VOID_P == 4
#define mono_atomic_fetch_add_word(p,add) mono_atomic_fetch_add_i32 ((volatile gint32*)p, (gint32)add)
#else
#define mono_atomic_fetch_add_word(p,add) mono_atomic_fetch_add_i64 ((volatile gint64*)p, (gint64)add)
#endif

/* The following functions cannot be found on any platform, and thus they can be declared without further existence checks */

static inline void
mono_atomic_store_bool (volatile gboolean *dest, gboolean val)
{
	/* both, gboolean and gint32, are int32_t; the purpose of these casts is to make things explicit */
	mono_atomic_store_i32 ((volatile gint32 *)dest, (gint32)val);
}

#if defined (WAPI_NO_ATOMIC_ASM)
#define MONO_ATOMIC_USES_LOCK
#elif defined(BROKEN_64BIT_ATOMICS_INTRINSIC)
#if !defined(TARGET_OSX) && !(defined (__arm__) && defined (HAVE_ARMV7) && (defined(TARGET_IOS) || defined(TARGET_TVOS) || defined(TARGET_WATCHOS) || defined(TARGET_ANDROID)))
#define MONO_ATOMIC_USES_LOCK
#endif
#endif

#endif /* _WAPI_ATOMIC_H_ */
