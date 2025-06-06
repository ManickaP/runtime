// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __StackFrameIterator_h__
#define __StackFrameIterator_h__

#include "CommonMacros.h"
#include "ICodeManager.h"
#include "Pal.h" // NATIVE_CONTEXT
#include "regdisplay.h"

#include "forward_declarations.h"

struct ExInfo;
typedef DPTR(ExInfo) PTR_ExInfo;
typedef VPTR(ICodeManager) PTR_ICodeManager;

enum ExKind : uint8_t
{
    EK_HardwareFault = 2,
    EK_SupersededFlag  = 8,
};

struct EHEnum
{
    ICodeManager * m_pCodeManager;
    EHEnumState m_state;
};

class StackFrameIterator;

struct PInvokeTransitionFrame;
typedef DPTR(PInvokeTransitionFrame) PTR_PInvokeTransitionFrame;
typedef DPTR(PAL_LIMITED_CONTEXT) PTR_PAL_LIMITED_CONTEXT;

class StackFrameIterator
{
    friend class AsmOffsets;

public:
    StackFrameIterator() {}
    StackFrameIterator(Thread * pThreadToWalk, PInvokeTransitionFrame* pInitialTransitionFrame);
    StackFrameIterator(Thread* pThreadToWalk, NATIVE_CONTEXT* pCtx);
    StackFrameIterator(Thread * pThreadToWalk, PTR_PAL_LIMITED_CONTEXT pCtx);

    bool             IsValid();
    void             CalculateCurrentMethodState();
    void             Next();
    PTR_VOID         GetEffectiveSafePointAddress();
    REGDISPLAY *     GetRegisterSet();
    PTR_ICodeManager GetCodeManager();
    MethodInfo *     GetMethodInfo();
    bool             IsActiveStackFrame();
#ifdef TARGET_X86
    bool             GetHijackedReturnValueLocation(PTR_OBJECTREF * pLocation, GCRefKind * pKind);
#endif
    void             SetControlPC(PTR_VOID controlPC);
    PTR_VOID         GetControlPC() { return m_ControlPC; }

    static bool     IsValidReturnAddress(PTR_VOID pvAddress);

    // Support for conservatively reporting GC references in a stack range. This is used when managed methods
    // with an unknown signature potentially including GC references call into the runtime and we need to let
    // a GC proceed (typically because we call out into managed code again). Instead of storing signature
    // metadata for every possible managed method that might make such a call we identify a small range of the
    // stack that might contain outgoing arguments. We then report every pointer that looks like it might
    // refer to the GC heap as a fixed interior reference.
    bool HasStackRangeToReportConservatively();
    void GetStackRangeToReportConservatively(PTR_OBJECTREF * ppLowerBound, PTR_OBJECTREF * ppUpperBound);

    // Implementations of RhpSfiInit and RhpSfiNext called from managed code
    bool             Init(PAL_LIMITED_CONTEXT* pStackwalkCtx, bool instructionFault);
    bool             Next(uint32_t* puExCollideClauseIdx, bool* pfUnwoundReversePInvoke);

private:
    // The invoke of a funclet is a bit special and requires an assembly thunk, but we don't want to break the
    // stackwalk due to this.  So this routine will unwind through the assembly thunks used to invoke funclets.
    // It's also used to disambiguate exceptionally- and non-exceptionally-invoked funclets.
    void UnwindFuncletInvokeThunk();
    void UnwindThrowSiteThunk();

    // If our control PC indicates that we're in the universal transition thunk that we use to generically
    // dispatch arbitrary managed calls, then handle the stack walk specially.
    // NOTE: This function always publishes a non-NULL conservative stack range lower bound.
    void UnwindUniversalTransitionThunk();

    void EnterInitialInvalidState(Thread * pThreadToWalk);

    void InternalInit(Thread * pThreadToWalk, PTR_PInvokeTransitionFrame pFrame, uint32_t dwFlags); // GC stackwalk
    void InternalInit(Thread * pThreadToWalk, PTR_PAL_LIMITED_CONTEXT pCtx, uint32_t dwFlags);  // EH and hijack stackwalk, and collided unwind
    void InternalInit(Thread * pThreadToWalk, NATIVE_CONTEXT* pCtx, uint32_t dwFlags);  // GC stackwalk of redirected thread
    void EnsureInitializedToManagedFrame();

    void InternalInitForEH(Thread * pThreadToWalk, PAL_LIMITED_CONTEXT * pCtx, bool instructionFault); // EH stackwalk
    void InternalInitForStackTrace();  // Environment.StackTrace

    PTR_VOID HandleExCollide(PTR_ExInfo pExInfo);
    void NextInternal();

    // This will walk m_pNextExInfo from its current value until it finds the next ExInfo at a higher address
    // than the SP reference value passed in.  This is useful when 'restarting' the stackwalk from a
    // particular PInvokeTransitionFrame or after we have a 'collided unwind' that may skip over ExInfos.
    void ResetNextExInfoForSP(uintptr_t SP);

    void UpdateFromExceptionDispatch(PTR_StackFrameIterator pSourceIterator);

    // helpers to ApplyReturnAddressAdjustment
    PTR_VOID AdjustReturnAddressForward(PTR_VOID controlPC);
    PTR_VOID AdjustReturnAddressBackward(PTR_VOID controlPC);

    void UnwindNonEHThunkSequence();
    void PrepareToYieldFrame();

    enum ReturnAddressCategory
    {
        InManagedCode,
        InThrowSiteThunk,
        InFuncletInvokeThunk,
        InFilterFuncletInvokeThunk,
        InUniversalTransitionThunk,
    };

    static ReturnAddressCategory CategorizeUnadjustedReturnAddress(PTR_VOID returnAddress);
    static bool IsNonEHThunk(ReturnAddressCategory category);

    enum Flags
    {
        // If this flag is set, each unwind will apply a -1 to the ControlPC.  This is used by EH to ensure
        // that the ControlPC of a callsite stays within the containing try region.
        ApplyReturnAddressAdjustment = 1,

        // Used by the GC stackwalk, this flag will ensure that multiple funclet frames for a given method
        // activation will be given only one callback.  The one callback is given for the most nested physical
        // stack frame of a given activation of a method.  (i.e. the leafmost funclet)
        CollapseFunclets             = 2,

        // This is a state returned by Next() which indicates that we just crossed an ExInfo in our unwind.
        ExCollide                    = 4,

        // If a hardware fault frame is encountered, report its control PC at the binder-inserted GC safe
        // point immediately after the prolog of the most nested enclosing try-region's handler.
        RemapHardwareFaultsToSafePoint = 8,

        MethodStateCalculated = 0x10,

        // This is a state returned by Next() which indicates that we just unwound a reverse pinvoke method
        UnwoundReversePInvoke = 0x20,

        // The thread was interrupted in the current frame at the current IP by a signal, SuspendThread or similar.
        ActiveStackFrame = 0x40,

        // When encountering a reverse P/Invoke, unwind directly to the P/Invoke frame using the saved transition frame.
        SkipNativeFrames = 0x80,

        // Set SP to an address that is valid for funclet resumption (x86 only)
        UpdateResumeSp = 0x100,

        GcStackWalkFlags = (CollapseFunclets | RemapHardwareFaultsToSafePoint | SkipNativeFrames),
        EHStackWalkFlags = (ApplyReturnAddressAdjustment | UpdateResumeSp),
        StackTraceStackWalkFlags = GcStackWalkFlags
    };

    struct PreservedRegPtrs
    {
#ifdef TARGET_ARM
        PTR_uintptr_t pR4;
        PTR_uintptr_t pR5;
        PTR_uintptr_t pR6;
        PTR_uintptr_t pR7;
        PTR_uintptr_t pR8;
        PTR_uintptr_t pR9;
        PTR_uintptr_t pR10;
        PTR_uintptr_t pR11;
#elif defined(TARGET_ARM64)
        PTR_uintptr_t pX19;
        PTR_uintptr_t pX20;
        PTR_uintptr_t pX21;
        PTR_uintptr_t pX22;
        PTR_uintptr_t pX23;
        PTR_uintptr_t pX24;
        PTR_uintptr_t pX25;
        PTR_uintptr_t pX26;
        PTR_uintptr_t pX27;
        PTR_uintptr_t pX28;
        PTR_uintptr_t pFP;
#elif defined(TARGET_LOONGARCH64)
        PTR_uintptr_t pR23;
        PTR_uintptr_t pR24;
        PTR_uintptr_t pR25;
        PTR_uintptr_t pR26;
        PTR_uintptr_t pR27;
        PTR_uintptr_t pR28;
        PTR_uintptr_t pR29;
        PTR_uintptr_t pR30;
        PTR_uintptr_t pR31;
        PTR_uintptr_t pFP;
#elif defined(TARGET_RISCV64)
        PTR_uintptr_t pS1;
        PTR_uintptr_t pS2;
        PTR_uintptr_t pS3;
        PTR_uintptr_t pS4;
        PTR_uintptr_t pS5;
        PTR_uintptr_t pS6;
        PTR_uintptr_t pS7;
        PTR_uintptr_t pS8;
        PTR_uintptr_t pS9;
        PTR_uintptr_t pS10;
        PTR_uintptr_t pS11;
        PTR_uintptr_t pFP;
#elif defined(UNIX_AMD64_ABI)
        PTR_uintptr_t pRbp;
        PTR_uintptr_t pRbx;
        PTR_uintptr_t pR12;
        PTR_uintptr_t pR13;
        PTR_uintptr_t pR14;
        PTR_uintptr_t pR15;
#else // TARGET_ARM
        PTR_uintptr_t pRbp;
        PTR_uintptr_t pRdi;
        PTR_uintptr_t pRsi;
        PTR_uintptr_t pRbx;
#ifdef TARGET_AMD64
        PTR_uintptr_t pR12;
        PTR_uintptr_t pR13;
        PTR_uintptr_t pR14;
        PTR_uintptr_t pR15;
#endif // TARGET_AMD64
#endif // TARGET_ARM
    };

protected:
    Thread *            m_pThread;
    RuntimeInstance *   m_pInstance;
    PTR_VOID            m_FramePointer;
    PTR_VOID            m_ControlPC;
    REGDISPLAY          m_RegDisplay;
    PTR_ICodeManager    m_pCodeManager;
    MethodInfo          m_methodInfo;
    PTR_VOID            m_effectiveSafePointAddress;
#ifdef TARGET_X86
    PTR_OBJECTREF       m_pHijackedReturnValue;
    GCRefKind           m_HijackedReturnValueKind;
#endif
    PTR_uintptr_t       m_pConservativeStackRangeLowerBound;
    PTR_uintptr_t       m_pConservativeStackRangeUpperBound;
    uint32_t            m_dwFlags;
    PTR_ExInfo          m_pNextExInfo;
    PTR_VOID            m_pendingFuncletFramePointer;
    PreservedRegPtrs    m_funcletPtrs;  // @TODO: Placing the 'scratch space' in the StackFrameIterator is not
                                        // preferred because not all StackFrameIterators require this storage
                                        // space.  However, the implementation simpler by doing it this way.
    PTR_VOID            m_OriginalControlPC;
    PTR_PInvokeTransitionFrame m_pPreviousTransitionFrame;
};

#endif // __StackFrameIterator_h__
