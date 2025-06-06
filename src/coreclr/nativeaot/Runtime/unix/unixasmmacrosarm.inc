// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// CONSTANTS -- INTEGER
//

#define TSF_Attached                    0x01
#define TSF_SuppressGcStress            0x08
#define TSF_DoNotTriggerGc              0x10

#define PTFF_SAVE_ALL_PRESERVED 0x0000007F  // NOTE: R11 is not included in this set!
#define PTFF_SAVE_R9 0x00000020
#define PTFF_SAVE_SP 0x00000100
#define PTFF_SAVE_R0 0x00000200
#define PTFF_THREAD_ABORT  0x00004000
#define PTFF_THREAD_HIJACK 0x00008000   // indicates that this is a frame for a hijacked call

#define DEFAULT_FRAME_SAVE_FLAGS (PTFF_SAVE_ALL_PRESERVED + PTFF_SAVE_SP)

// These must match the TrapThreadsFlags enum
#define TrapThreadsFlags_None            0
#define TrapThreadsFlags_AbortInProgress 1
#define TrapThreadsFlags_TrapThreads     2

// GC minimal sized object. We use this to switch between 4 and 8 byte alignment in the GC heap (see AllocFast.asm).
#define ASM_MIN_OBJECT_SIZE 12

.macro NESTED_ENTRY Name, Section, Handler
        LEAF_ENTRY \Name, \Section
        .ifnc \Handler, NoHandler
        .personality C_FUNC(\Handler)
        .endif
.endm

.macro NESTED_END Name, Section
        LEAF_END \Name, \Section
.endm

.macro PATCH_LABEL Name
        .thumb_func
        .global C_FUNC(\Name)
C_FUNC(\Name):
.endm

.macro ALTERNATE_ENTRY Name
        .global C_FUNC(\Name)
        .type \Name, %function
C_FUNC(\Name):
.endm

.macro GLOBAL_LABEL Name
        .global C_FUNC(\Name)
C_FUNC(\Name):
.endm

.macro LEAF_ENTRY Name, Section
        .thumb_func
        .global C_FUNC(\Name)
        .type \Name, %function
C_FUNC(\Name):
        .fnstart
.endm

.macro LEAF_END Name, Section
        .size \Name, .-\Name
        .fnend
.endm

.macro PREPARE_EXTERNAL_VAR Name, HelperReg
        movw \HelperReg, #:lower16:C_FUNC(\Name) - (. + 12)
        movt \HelperReg, #:upper16:C_FUNC(\Name) - (. + 8)
        add \HelperReg, pc
.endm

.macro PREPARE_EXTERNAL_VAR_INDIRECT Name, HelperReg
        movw \HelperReg, #:lower16:C_FUNC(\Name) - (. + 12)
        movt \HelperReg, #:upper16:C_FUNC(\Name) - (. + 8)
        add \HelperReg, pc
        ldr \HelperReg, [\HelperReg]
.endm

.macro push_nonvol_reg Register
        push \Register
        .save \Register
.endm

.macro pop_nonvol_reg Register
        pop \Register
.endm

.macro vpush_nonvol_reg Register
        vpush \Register
        .vsave \Register
.endm

.macro vpop_nonvol_reg Register
        vpop \Register
.endm

.macro alloc_stack Size
        sub sp, sp, (\Size)
        .pad #(\Size)
.endm

.macro free_stack Size
        add sp, sp, (\Size)
        .pad #-(\Size)
.endm

.macro POP_CALLEE_SAVED_REGISTERS
        pop_nonvol_reg "{r4-r11, lr}"
.endm

.macro PUSH_CALLEE_SAVED_REGISTERS
        push_nonvol_reg "{r4-r11, lr}"
.endm

.macro push_register Reg
        push \Reg
.endm

.macro push_argument_register Reg
        push_register \Reg
.endm

.macro PUSH_ARGUMENT_REGISTERS
        push {r0-r3}
.endm

.macro pop_register Reg
        pop \Reg
.endm

.macro pop_argument_register Reg
        pop_register \Reg
.endm

.macro POP_ARGUMENT_REGISTERS
        pop {r0-r3}
.endm

.macro EMIT_BREAKPOINT
        .inst.w 0xde01
.endm

.macro PROLOG_PUSH RegList
        push_nonvol_reg "\RegList"
.endm

.macro PROLOG_VPUSH RegList
        vpush_nonvol_reg "\RegList"
.endm

.macro PROLOG_STACK_SAVE Register
        .setfp \Register, sp
        mov \Register, sp
.endm

.macro EPILOG_STACK_RESTORE Register
        mov sp, \Register
.endm

.macro EPILOG_POP RegList
        pop_nonvol_reg "\RegList"
.endm

.macro EPILOG_VPOP RegList
        vpop_nonvol_reg "\RegList"
.endm

.macro PROLOG_STACK_ALLOC Size
        sub sp, sp, #\Size
        .pad #\Size
.endm

.macro EPILOG_STACK_FREE Size
        add sp, sp, #\Size
        .pad #-\Size
.endm

//-----------------------------------------------------------------------------
// Macro used to check (in debug builds only) whether the stack is 64-bit aligned (a requirement before calling
// out into C++/OS code). Invoke this directly after your prolog (if the stack frame size is fixed) or directly
// before a call (if you have a frame pointer and a dynamic stack). A breakpoint will be invoked if the stack
// is misaligned.
//
.macro CHECK_STACK_ALIGNMENT

#ifdef _DEBUG
        push {r0}
        add r0, sp, #4
        tst r0, #7
        pop {r0}
        beq 0f
        EMIT_BREAKPOINT
0:
#endif
.endm

// Loads a 32bit constant into destination register
.macro MOV32 DestReg, Constant
        movw \DestReg, #((\Constant) & 0xFFFF)
        movt \DestReg, #((\Constant) >> 16)
.endm

//
// Macro used from unmanaged helpers called from managed code where the helper does not transition immediately
// into pre-emptive mode but may cause a GC and thus requires the stack is crawlable. This is typically the
// case for helpers that meddle in GC state (e.g. allocation helpers) where the code must remain in
// cooperative mode since it handles object references and internal GC state directly but a garbage collection
// may be inevitable. In these cases we need to be able to transition to pre-meptive mode deep within the
// unmanaged code but still be able to initialize the stack iterator at the first stack frame which may hold
// interesting GC references. In all our helper cases this corresponds to the most recent managed frame (e.g.
// the helper's caller).
//
// This macro builds a frame describing the current state of managed code.
//
// INVARIANTS
// - The macro assumes it defines the method prolog, it should typically be the first code in a method and
//   certainly appear before any attempt to alter the stack pointer.
// - This macro uses trashReg (after its initial value has been saved in the frame) and upon exit trashReg
//   will contain the address of transition frame.
//
.macro PUSH_COOP_PINVOKE_FRAME trashReg

        PROLOG_STACK_ALLOC 4          // Save space for caller's SP
        PROLOG_PUSH "{r4-r10}"        // Save preserved registers
        PROLOG_STACK_ALLOC 8          // Save space for flags and Thread*
        PROLOG_PUSH "{r11,lr}"        // Save caller's frame pointer and PC

        // Compute SP value at entry to this method and save it in the last slot of the frame (slot #12).
        add         \trashReg, sp, #(12 * 4)
        str         \trashReg, [sp, #(11 * 4)]

        // Record the bitmask of saved registers in the frame (slot #3).
        mov         \trashReg, #DEFAULT_FRAME_SAVE_FLAGS
        str         \trashReg, [sp, #(3 * 4)]

        mov         \trashReg, sp
.endm

// Pop the frame and restore register state preserved by PUSH_COOP_PINVOKE_FRAME
.macro POP_COOP_PINVOKE_FRAME
        EPILOG_POP  "{r11,lr}"        // Restore caller's frame pointer and PC (return address)
        EPILOG_STACK_FREE 8           // Discard flags and Thread*
        EPILOG_POP  "{r4-r10}"        // Restore preserved registers
        EPILOG_STACK_FREE 4           // Discard caller's SP
.endm

// thumb with PIC version
.macro INLINE_GET_TLS_VAR Var
        ldr     r0, 2f
1:
        add     r0, pc, r0
        bl      __tls_get_addr
        b       3f

        // Inline data
        // LLVM assembler has no concept of subsections and this is not expressible as
        // cross-section relocation.
        .p2align 2
2:
        .extern \Var
        .type \Var, tls_object
        .long  \Var(TLSGD) + (2b - 1b - 4)
3:
.endm

.macro INLINE_GETTHREAD
#ifdef FEATURE_EMULATED_TLS
        bl C_FUNC(RhpGetThread)
#else
        // Inlined version of call C_FUNC(RhpGetThread)
        INLINE_GET_TLS_VAR tls_CurrentThread
#endif
.endm

.macro INLINE_GET_ALLOC_CONTEXT_BASE
        INLINE_GETTHREAD
.endm

.macro INLINE_THREAD_UNHIJACK threadReg, trashReg1, trashReg2
        //
        // Thread::Unhijack()
        //
        ldr         \trashReg1, [\threadReg, #OFFSETOF__Thread__m_pvHijackedReturnAddress]
        cbz         \trashReg1, 1f

        ldr         \trashReg2, [\threadReg, #OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation]
        str         \trashReg1, [\trashReg2]
        mov         \trashReg1, #0
        str         \trashReg1, [\threadReg, #OFFSETOF__Thread__m_ppvHijackedReturnAddressLocation]
        str         \trashReg1, [\threadReg, #OFFSETOF__Thread__m_pvHijackedReturnAddress]

1:
.endm

.macro EPILOG_BRANCH_REG reg

        bx          \reg

.endm
