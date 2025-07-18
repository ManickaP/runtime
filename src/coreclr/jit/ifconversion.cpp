// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

/*XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XX                                                                           XX
XX                              OptIfConversion                              XX
XX                                                                           XX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
*/

#include "jitpch.h"
#ifdef _MSC_VER
#pragma hdrstop
#endif

//-----------------------------------------------------------------------------
// OptIfConversionDsc:     Descriptor used for If conversion
//
class OptIfConversionDsc
{
public:
    OptIfConversionDsc(Compiler* comp, BasicBlock* startBlock)
    {
        m_comp       = comp;
        m_startBlock = startBlock;
    }

private:
    Compiler* m_comp; // The Compiler instance.

    BasicBlock* m_startBlock;           // First block in the If Conversion.
    BasicBlock* m_finalBlock = nullptr; // Block where the flows merge. In a return case, this can be nullptr.

    // The node, statement and block of an operation.
    struct IfConvertOperation
    {
        BasicBlock* block = nullptr;
        Statement*  stmt  = nullptr;
        GenTree*    node  = nullptr;
    };

    GenTree*           m_cond;          // The condition in the conversion
    IfConvertOperation m_thenOperation; // The single operation in the Then case.
    IfConvertOperation m_elseOperation; // The single operation in the Else case.

    int m_checkLimit = 4; // Max number of chained blocks to allow in both the True and Else cases.

    genTreeOps m_mainOper         = GT_COUNT; // The main oper of the if conversion.
    bool       m_doElseConversion = false;    // Does the If conversion have an else statement.
    bool       m_flowFound        = false;    // Has a valid flow been found.

    bool IfConvertCheckInnerBlockFlow(BasicBlock* block);
    bool IfConvertCheckThenFlow();
    void IfConvertFindFlow();
    bool IfConvertCheckStmts(BasicBlock* fromBlock, IfConvertOperation* foundOperation);
    void IfConvertJoinStmts(BasicBlock* fromBlock);

    GenTree* TryTransformSelectOperOrLocal(GenTree* oper, GenTree* lcl);
    GenTree* TryTransformSelectOperOrZero(GenTree* oper, GenTree* lcl);
    GenTree* TryTransformSelectToOrdinaryOps(GenTree* trueInput, GenTree* falseInput);
#ifdef DEBUG
    void IfConvertDump();
#endif

public:
    bool optIfConvert();
};

//-----------------------------------------------------------------------------
// IfConvertCheckInnerBlockFlow
//
// Check if the flow of a block is valid for use as an inner block (either a Then or Else block)
// in an If Conversion.
//
// Assumptions:
//   m_startBlock and m_doElseConversion are set.
//
// Arguments:
//   block -- Block to check.
//
// Returns:
//   True if Checks are ok, else false.
//
bool OptIfConversionDsc::IfConvertCheckInnerBlockFlow(BasicBlock* block)
{
    // Block should have a single successor or be a return.
    if (!(block->GetUniqueSucc() != nullptr || (m_doElseConversion && (block->KindIs(BBJ_RETURN)))))
    {
        return false;
    }

    // Check that we have linear flow and are still in the same EH region

    if (block->GetUniquePred(m_comp) == nullptr)
    {
        return false;
    }

    if (!BasicBlock::sameEHRegion(block, m_startBlock))
    {
        return false;
    }

    return true;
}

//-----------------------------------------------------------------------------
// IfConvertCheckThenFlow
//
// Check all the Then blocks between m_startBlock and m_finalBlock are valid.
//
// Assumptions:
//   m_startBlock, m_finalBlock and m_doElseConversion are set.
//
// Returns:
//   If a conversion is found, then set m_flowFound and return true.
//   If a conversion is not found, and it's ok to keep searching, return true.
//   Otherwise, return false.
//
// Notes:
//   Sets m_flowFound and m_mainOper.
//
bool OptIfConversionDsc::IfConvertCheckThenFlow()
{
    m_flowFound           = false;
    BasicBlock* thenBlock = m_startBlock->GetFalseTarget();

    for (int thenLimit = 0; thenLimit < m_checkLimit; thenLimit++)
    {
        if (!IfConvertCheckInnerBlockFlow(thenBlock))
        {
            // Then block is not in a valid flow.
            return true;
        }
        BasicBlock* thenBlockNext = thenBlock->GetUniqueSucc();

        if (thenBlockNext == m_finalBlock)
        {
            // All the Then blocks up to m_finalBlock are in a valid flow.
            m_flowFound = true;
            if (thenBlock->KindIs(BBJ_RETURN))
            {
                assert(m_finalBlock == nullptr);
                m_mainOper = GT_RETURN;
            }
            else
            {
                m_mainOper = GT_STORE_LCL_VAR;
            }
            return true;
        }

        if (thenBlockNext == nullptr)
        {
            // Invalid Then and Else combination.
            return false;
        }

        thenBlock = thenBlockNext;
    }

    // Nothing found. Still valid to continue.
    return true;
}

//-----------------------------------------------------------------------------
// IfConvertFindFlow
//
// Find a valid if conversion flow from m_startBlock to a final block.
// There might be multiple Then and Else blocks in the flow - use m_checkLimit to limit this.
//
// Notes:
//   Sets m_flowFound, m_finalBlock, m_doElseConversion and m_mainOper.
//
void OptIfConversionDsc::IfConvertFindFlow()
{
    // First check for flow with no else case. The final block is the destination of the jump.
    m_doElseConversion = false;
    m_finalBlock       = m_startBlock->GetTrueTarget();
    assert(m_finalBlock != nullptr);
    if (!IfConvertCheckThenFlow() || m_flowFound)
    {
        // Either the flow is invalid, or a flow was found.
        return;
    }

    // Look for flows with else blocks. The final block is the block after the else block.
    m_doElseConversion = true;
    for (int elseLimit = 0; elseLimit < m_checkLimit; elseLimit++)
    {
        BasicBlock* elseBlock = m_finalBlock;
        if (elseBlock == nullptr || !IfConvertCheckInnerBlockFlow(elseBlock))
        {
            // Need a valid else block in a valid flow .
            return;
        }

        m_finalBlock = elseBlock->GetUniqueSucc();

        if (!IfConvertCheckThenFlow() || m_flowFound)
        {
            // Either the flow is invalid, or a flow was found.
            return;
        }
    }
}

//-----------------------------------------------------------------------------
// IfConvertCheckStmts
//
// From the given block to the final block, check all the statements and nodes are
// valid for an If conversion. Chain of blocks must contain only a single local
// store and no other operations.
//
// Arguments:
//   fromBlock      - Block inside the if statement to start from (Either Then or Else path).
//   foundOperation - Returns the found operation.
//
// Returns:
//   If everything is valid, then set foundOperation to the store and return true.
//   Otherwise return false.
//
bool OptIfConversionDsc::IfConvertCheckStmts(BasicBlock* fromBlock, IfConvertOperation* foundOperation)
{
    bool found = false;

    for (BasicBlock* block = fromBlock; block != m_finalBlock; block = block->GetUniqueSucc())
    {
        assert(block != nullptr);

        // Can all the nodes within the block be made to conditionally execute?
        for (Statement* const stmt : block->Statements())
        {
            GenTree* tree = stmt->GetRootNode();
            switch (tree->OperGet())
            {
                case GT_STORE_LCL_VAR:
                {
                    // Only one per operation per block can be conditionally executed.
                    if (found)
                    {
                        return false;
                    }

                    // Ensure the local has integer type.
                    if (!varTypeIsIntegralOrI(tree))
                    {
                        return false;
                    }

#ifndef TARGET_64BIT
                    // Disallow 64-bit operands on 32-bit targets as the backend currently cannot
                    // handle contained relops efficiently after decomposition.
                    if (varTypeIsLong(tree))
                    {
                        return false;
                    }
#endif
                    GenTree* op1 = tree->AsLclVar()->Data();

                    // Ensure it won't cause any additional side effects.
                    if ((op1->gtFlags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                    {
                        return false;
                    }

                    // Ensure the source isn't a phi.
                    if (op1->OperIs(GT_PHI))
                    {
                        return false;
                    }

                    // Evaluating unconditionally effectively has the same effect as reordering
                    // with the condition (for example, the condition could be an explicit bounds
                    // check and the operand could read an array element). Disallow this except
                    // for some common cases that we know are always side effect free.
                    if (((m_cond->gtFlags & GTF_ORDER_SIDEEFF) != 0) && !op1->IsInvariant() && !op1->OperIsLocal())
                    {
                        return false;
                    }

                    found                 = true;
                    foundOperation->block = block;
                    foundOperation->stmt  = stmt;
                    foundOperation->node  = tree;
                    break;
                }

                case GT_RETURN:
                {
                    // GT_SWIFT_ERROR_RET not supported
                    GenTree* const retVal = tree->gtGetOp1();

                    // Only allow RETURNs if else conversion is being used.
                    if (!m_doElseConversion)
                    {
                        return false;
                    }

                    // Only one per operation per block can be conditionally executed.
                    if (found || retVal == nullptr)
                    {
                        return false;
                    }

                    // Ensure the operation has integer type.
                    if (!varTypeIsIntegralOrI(tree))
                    {
                        return false;
                    }

#ifndef TARGET_64BIT
                    // Disallow 64-bit operands on 32-bit targets as the backend currently cannot
                    // handle contained relops efficiently after decomposition.
                    if (varTypeIsLong(tree))
                    {
                        return false;
                    }
#endif

                    // Ensure it won't cause any additional side effects.
                    if ((retVal->gtFlags & (GTF_SIDE_EFFECT | GTF_ORDER_SIDEEFF)) != 0)
                    {
                        return false;
                    }

                    // Evaluating unconditionally effectively has the same effect as reordering
                    // with the condition (for example, the condition could be an explicit bounds
                    // check and the operand could read an array element). Disallow this except
                    // for some common cases that we know are always side effect free.
                    if (((m_cond->gtFlags & GTF_ORDER_SIDEEFF) != 0) && !retVal->IsInvariant() &&
                        !retVal->OperIsLocal())
                    {
                        return false;
                    }

                    found                 = true;
                    foundOperation->block = block;
                    foundOperation->stmt  = stmt;
                    foundOperation->node  = tree;
                    break;
                }

                // These do not need conditional execution.
                case GT_NOP:
                    break;

                // Cannot optimise this block.
                default:
                    return false;
            }
        }
    }
    return found;
}

//-----------------------------------------------------------------------------
// IfConvertJoinStmts
//
// Move all the statements from a block onto the end of the start block.
//
// Arguments:
//   fromBlock  -- Source block
//
void OptIfConversionDsc::IfConvertJoinStmts(BasicBlock* fromBlock)
{
    Statement* stmtList1 = m_startBlock->firstStmt();
    Statement* stmtList2 = fromBlock->firstStmt();
    Statement* stmtLast1 = m_startBlock->lastStmt();
    Statement* stmtLast2 = fromBlock->lastStmt();
    stmtLast1->SetNextStmt(stmtList2);
    stmtList2->SetPrevStmt(stmtLast1);
    stmtList1->SetPrevStmt(stmtLast2);
    fromBlock->bbStmtList = nullptr;
}

//-----------------------------------------------------------------------------
// IfConvertDump
//
// Dump all the blocks in the If Conversion.
//
#ifdef DEBUG
void OptIfConversionDsc::IfConvertDump()
{
    assert(m_startBlock != nullptr);
    m_comp->fgDumpBlock(m_startBlock);
    BasicBlock* dumpBlock = m_startBlock->KindIs(BBJ_COND) ? m_startBlock->GetFalseTarget() : m_startBlock->GetTarget();
    for (; dumpBlock != m_finalBlock; dumpBlock = dumpBlock->GetUniqueSucc())
    {
        m_comp->fgDumpBlock(dumpBlock);
    }
    if (m_doElseConversion)
    {
        dumpBlock = m_startBlock->KindIs(BBJ_COND) ? m_startBlock->GetTrueTarget() : m_startBlock->GetTarget();
        for (; dumpBlock != m_finalBlock; dumpBlock = dumpBlock->GetUniqueSucc())
        {
            m_comp->fgDumpBlock(dumpBlock);
        }
    }
}
#endif

//-----------------------------------------------------------------------------
// optIfConvert
//
// Find blocks representing simple if statements represented by conditional jumps
// over another block. Try to replace the jumps by use of SELECT nodes.
//
// Returns:
//   true if any IR changes possibly made.
//
// Notes:
//
// Example of simple if conversion:
//
// This is optimising a simple if statement. There is a single condition being
// tested, and a single assignment inside the body. There must be no else
// statement. For example:
// if (x < 7) { a = 5; }
//
// This is represented in IR by two basic blocks. The first block (block) ends with
// a JTRUE statement which conditionally jumps to the second block (thenBlock).
// The second block just contains a single store statement. Both blocks then jump
// to the same destination (finalBlock).  Note that the first block may contain
// additional statements prior to the JTRUE statement.
//
// For example:
//
// ------------ BB03 [009..00D) -> BB05 (cond), preds={BB02} succs={BB04,BB05}
// STMT00004
//   *  JTRUE     void   $VN.Void
//   \--*  GE        int    $102
//      +--*  LCL_VAR   int    V02
//      \--*  CNS_INT   int    7 $46
//
// ------------ BB04 [00D..010), preds={BB03} succs={BB05}
// STMT00005
//   *  STORE_LCL_VAR   int    V00 arg0
//   \--*  CNS_INT   int    5 $47
//
//
// This is optimised by conditionally executing the store and removing the conditional
// jumps. First the JTRUE is replaced with a NOP. The store is updated so that the
// source of the store is a SELECT node with the condition set to the inverse of the
// original JTRUE condition. If the condition passes the original store happens,
// otherwise the existing source value is used.
//
// In the example above, local var 0 is set to 5 if the LT returns true, otherwise
// the existing value of local var 0 is used:
//
// ------------ BB03 [009..00D) -> BB05 (always), preds={BB02} succs={BB05}
// STMT00004
//   *  NOP       void
//
// STMT00005
//   *  STORE_LCL_VAR   int    V00 arg0
//   \--*  SELECT    int
//      +--*  LT        int    $102
//      |  +--*  LCL_VAR   int    V02
//      |  \--*  CNS_INT   int    7 $46
//      +--*  CNS_INT   int    5 $47
//      \--*  LCL_VAR   int    V00
//
// ------------ BB04 [00D..010), preds={} succs={BB05}
//
//
// Example of simple if conversion with an else condition
//
// This is similar to the simple if conversion above, but with an else statement
// that assigns to the same variable as the then statement. For example:
// if (x < 7) { a = 5; } else { a = 9; }
//
// ------------ BB03 [009..00D) -> BB05 (cond), preds={BB02} succs={BB04,BB05}
// STMT00004
//   *  JTRUE     void   $VN.Void
//   \--*  GE        int    $102
//      +--*  LCL_VAR   int    V02
//      \--*  CNS_INT   int    7 $46
//
// ------------ BB04 [00D..010), preds={BB03} succs={BB06}
// STMT00005
//   *  STORE_LCL_VAR   int    V00 arg0
//   \--*  CNS_INT   int    5 $47
//
// ------------ BB05 [00D..010), preds={BB03} succs={BB06}
// STMT00006
//   *  STORE_LCL_VAR   int    V00 arg0
//   \--*  CNS_INT   int    9 $48
//
// Again this is squashed into a single block, with the SELECT node handling both cases.
//
// ------------ BB03 [009..00D) -> BB05 (always), preds={BB02} succs={BB05}
// STMT00004
//   *  NOP       void
//
// STMT00005
//   *  STORE_LCL_VAR   int    V00 arg0
//   \--*  SELECT    int
//      +--*  LT        int    $102
//      |  +--*  LCL_VAR   int    V02
//      |  \--*  CNS_INT   int    7 $46
//      +--*  CNS_INT   int    5 $47
//      +--*  CNS_INT   int    9 $48
//
// STMT00006
//   *  NOP       void
//
// ------------ BB04 [00D..010), preds={} succs={BB06}
// ------------ BB05 [00D..010), preds={} succs={BB06}
//
// Alternatively, an if conversion with an else condition may use RETURNs.
// return (x < 7) ? 5 : 9;
//
// ------------ BB03 [009..00D) -> BB05 (cond), preds={BB02} succs={BB04,BB05}
// STMT00004
//   *  JTRUE     void   $VN.Void
//   \--*  GE        int    $102
//      +--*  LCL_VAR   int    V02
//      \--*  CNS_INT   int    7 $46
//
// ------------ BB04 [00D..010), preds={BB03} succs={BB06}
// STMT00005
//   *  RETURN    int    $VN.Void
// +--*  CNS_INT   int    5 $41
//
// ------------ BB05 [00D..010), preds={BB03} succs={BB06}
// STMT00006
//   *  RETURN    int    $VN.Void
// +--*  CNS_INT   int    9 $43
//
// becomes:
//
// ------------ BB03 [009..00D) -> BB05 (always), preds={BB02} succs={BB05}
// STMT00004
//   *  NOP       void
//
// STMT00005
//   *  RETURN    int    $VN.Void
//   \--*  SELECT    int
//      +--*  LT        int    $102
//      |  +--*  LCL_VAR   int    V02
//      |  \--*  CNS_INT   int    7 $46
//      +--*  CNS_INT   int    5 $41
//      +--*  CNS_INT   int    9 $43
//
// STMT00006
//   *  NOP       void
//
// ------------ BB04 [00D..010), preds={} succs={BB06}
// ------------ BB05 [00D..010), preds={} succs={BB06}
//
bool OptIfConversionDsc::optIfConvert()
{
    // Does the block end by branching via a JTRUE after a compare?
    if (!m_startBlock->KindIs(BBJ_COND) || (m_startBlock->NumSucc() != 2))
    {
        return false;
    }

    // Verify the test block ends with a condition that we can manipulate.
    GenTree* last = m_startBlock->lastStmt()->GetRootNode();
    noway_assert(last->OperIs(GT_JTRUE));
    m_cond = last->gtGetOp1();
    if (!m_cond->OperIsCompare())
    {
        return false;
    }

    // Look for valid flow of Then and Else blocks.
    IfConvertFindFlow();
    if (!m_flowFound)
    {
        return false;
    }

    // Check the Then and Else blocks have a single operation each.
    if (!IfConvertCheckStmts(m_startBlock->GetFalseTarget(), &m_thenOperation))
    {
        return false;
    }
    assert(m_thenOperation.node->OperIs(GT_STORE_LCL_VAR, GT_RETURN));
    if (m_doElseConversion)
    {
        if (!IfConvertCheckStmts(m_startBlock->GetTrueTarget(), &m_elseOperation))
        {
            return false;
        }

        // Both operations must be the same node type.
        if (m_thenOperation.node->OperGet() != m_elseOperation.node->OperGet())
        {
            return false;
        }

        // Currently can only support Else Store Blocks that have the same destination as the Then block.
        if (m_thenOperation.node->OperIs(GT_STORE_LCL_VAR))
        {
            unsigned lclNumThen = m_thenOperation.node->AsLclVarCommon()->GetLclNum();
            unsigned lclNumElse = m_elseOperation.node->AsLclVarCommon()->GetLclNum();
            if (lclNumThen != lclNumElse)
            {
                return false;
            }
        }
    }

#ifdef DEBUG
    if (m_comp->verbose)
    {
        JITDUMP("\nConditionally executing " FMT_BB, m_thenOperation.block->bbNum);
        if (m_doElseConversion)
        {
            JITDUMP(" and " FMT_BB, m_elseOperation.block->bbNum);
        }
        JITDUMP(" inside " FMT_BB "\n", m_startBlock->bbNum);
        IfConvertDump();
    }
#endif

    // Using SELECT nodes means that both Then and Else operations are fully evaluated.
    // Put a limit on the original source and destinations.
    if (!m_comp->compStressCompile(Compiler::STRESS_IF_CONVERSION_COST, 25))
    {
        int thenCost = 0;
        int elseCost = 0;

        if (m_mainOper == GT_STORE_LCL_VAR)
        {
            thenCost = m_thenOperation.node->AsLclVar()->Data()->GetCostEx() +
                       (m_comp->gtIsLikelyRegVar(m_thenOperation.node) ? 0 : 2);
            if (m_doElseConversion)
            {
                elseCost = m_elseOperation.node->AsLclVar()->Data()->GetCostEx() +
                           (m_comp->gtIsLikelyRegVar(m_elseOperation.node) ? 0 : 2);
            }
        }
        else
        {
            assert(m_mainOper == GT_RETURN);
            thenCost = m_thenOperation.node->AsOp()->GetReturnValue()->GetCostEx();
            if (m_doElseConversion)
            {
                elseCost = m_elseOperation.node->AsOp()->GetReturnValue()->GetCostEx();
            }
        }

        // Cost to allow for "x = cond ? a + b : c + d".
        if (thenCost > 7 || elseCost > 7)
        {
            JITDUMP("Skipping if-conversion that will evaluate RHS unconditionally at costs %d,%d\n", thenCost,
                    elseCost);
            return false;
        }
    }

    if (!m_comp->compStressCompile(Compiler::STRESS_IF_CONVERSION_INNER_LOOPS, 25))
    {
        // Don't optimise the block if it is inside a loop. Loop-carried
        // dependencies can cause significant stalls if if-converted.
        // Detect via the block weight as that will be high when inside a loop.

        if (m_startBlock->getBBWeight(m_comp) > BB_UNITY_WEIGHT * 1.05)
        {
            JITDUMP("Skipping if-conversion inside loop (via weight)\n");
            return false;
        }

        // We may be inside an unnatural loop, so do the expensive check.
        if (m_comp->optReachable(m_finalBlock, m_startBlock, nullptr))
        {
            JITDUMP("Skipping if-conversion inside loop (via FG walk)\n");
            return false;
        }
    }

    // Get the select node inputs.
    var_types selectType;
    GenTree*  selectTrueInput;
    GenTree*  selectFalseInput;
    if (m_mainOper == GT_STORE_LCL_VAR)
    {
        selectFalseInput = m_thenOperation.node->AsLclVar()->Data();
        selectTrueInput  = m_doElseConversion ? m_elseOperation.node->AsLclVar()->Data() : nullptr;

        // Pick the type as the type of the local, which should always be compatible even for implicit coercions.
        selectType = genActualType(m_thenOperation.node);
    }
    else
    {
        assert(m_mainOper == GT_RETURN);
        assert(m_doElseConversion);
        assert(m_thenOperation.node->TypeGet() == m_elseOperation.node->TypeGet());

        selectTrueInput  = m_elseOperation.node->AsOp()->GetReturnValue();
        selectFalseInput = m_thenOperation.node->AsOp()->GetReturnValue();
        selectType       = genActualType(m_thenOperation.node);
    }

    GenTree* select = TryTransformSelectToOrdinaryOps(selectTrueInput, selectFalseInput);
    if (select == nullptr)
    {
#ifdef TARGET_RISCV64
        JITDUMP("Skipping if-conversion that cannot be transformed to ordinary operations\n");
        return false;
#endif
        if (selectTrueInput == nullptr)
        {
            // Duplicate the destination of the Then store.
            assert(m_mainOper == GT_STORE_LCL_VAR && !m_doElseConversion);
            GenTreeLclVar* store = m_thenOperation.node->AsLclVar();
            selectTrueInput      = m_comp->gtNewLclVarNode(store->GetLclNum(), store->TypeGet());
        }
        // Create a select node
        select = m_comp->gtNewConditionalNode(GT_SELECT, m_cond, selectTrueInput, selectFalseInput, selectType);
    }

    m_thenOperation.node->AddAllEffectsFlags(select);

    // Use the select as the source of the Then operation.
    if (m_mainOper == GT_STORE_LCL_VAR)
    {
        m_thenOperation.node->AsLclVar()->Data() = select;
    }
    else
    {
        m_thenOperation.node->AsOp()->SetReturnValue(select);
    }
    m_comp->gtSetEvalOrder(m_thenOperation.node);
    m_comp->fgSetStmtSeq(m_thenOperation.stmt);

    // Remove statements.
    last->gtBashToNOP();
    m_comp->gtSetEvalOrder(last);
    m_comp->fgSetStmtSeq(m_startBlock->lastStmt());
    if (m_doElseConversion)
    {
        m_elseOperation.node->gtBashToNOP();
        m_comp->gtSetEvalOrder(m_elseOperation.node);
        m_comp->fgSetStmtSeq(m_elseOperation.stmt);
    }

    // Merge all the blocks.
    IfConvertJoinStmts(m_thenOperation.block);
    if (m_doElseConversion)
    {
        IfConvertJoinStmts(m_elseOperation.block);
    }

    // Update the flow from the original block.
    FlowEdge* const removedEdge  = m_comp->fgRemoveAllRefPreds(m_startBlock->GetFalseTarget(), m_startBlock);
    FlowEdge* const retainedEdge = m_startBlock->GetTrueEdge();
    m_startBlock->SetKindAndTargetEdge(BBJ_ALWAYS, retainedEdge);
    m_comp->fgRepairProfileCondToUncond(m_startBlock, retainedEdge, removedEdge);

#ifdef DEBUG
    if (m_comp->verbose)
    {
        JITDUMP("\nAfter if conversion\n");
        IfConvertDump();
    }
#endif

    return true;
}

struct IntConstSelectOper
{
    genTreeOps oper;
    var_types  type;
    unsigned   bitIndex;

    bool isMatched() const
    {
        return oper != GT_NONE;
    }
};

//-----------------------------------------------------------------------------
// MatchIntConstSelectValues: Matches an operation so that `trueVal` can be calculated as:
//     oper(type, falseVal, condition)
//
// Notes:
//     A non-zero bitIndex (log2(trueVal)) differentiates (condition << bitIndex) from (falseVal << condition).
//
// Return Value:
//     The matched operation (if any).
//
static IntConstSelectOper MatchIntConstSelectValues(int64_t trueVal, int64_t falseVal)
{
    if (trueVal == falseVal + 1)
        return {GT_ADD, TYP_LONG};

    if (trueVal == int64_t(int32_t(falseVal) + 1))
        return {GT_ADD, TYP_INT};

    if (falseVal == 0)
    {
        unsigned bitIndex = BitOperations::Log2((uint64_t)trueVal);
        assert(bitIndex > 0);
        if (trueVal == (int64_t(1) << bitIndex))
            return {GT_LSH, TYP_LONG, bitIndex};

        bitIndex = BitOperations::Log2((uint32_t)trueVal);
        assert(bitIndex > 0);
        if (trueVal == int64_t(int32_t(int32_t(1) << bitIndex)))
            return {GT_LSH, TYP_INT, bitIndex};
    }

    if (trueVal == falseVal << 1)
        return {GT_LSH, TYP_LONG};

    if (trueVal == int64_t(int32_t(falseVal) << 1))
        return {GT_LSH, TYP_INT};

    if (trueVal == falseVal >> 1)
        return {GT_RSH, TYP_LONG};

    if (trueVal == int64_t(int32_t(falseVal) >> 1))
        return {GT_RSH, TYP_INT};

    if (trueVal == int64_t(uint64_t(falseVal) >> 1))
        return {GT_RSZ, TYP_LONG};

    if (trueVal == int64_t(uint32_t(falseVal) >> 1))
        return {GT_RSZ, TYP_INT};

    return {GT_NONE};
}

//-----------------------------------------------------------------------------
// TryTransformSelectOperOrLocal: Try to trasform "cond ? oper(lcl, (-)1) : lcl" into "oper(')(lcl, cond)"
//
// Arguments:
//     trueInput  - expression to be evaluated when m_cond is true
//     falseInput - expression to be evaluated when m_cond is false
//
// Return Value:
//     The transformed expression, or null if no transformation took place
//
GenTree* OptIfConversionDsc::TryTransformSelectOperOrLocal(GenTree* trueInput, GenTree* falseInput)
{
    GenTree* oper = trueInput;
    GenTree* lcl  = falseInput;

    bool isCondReversed = !lcl->OperIsAnyLocal();
    if (isCondReversed)
        std::swap(oper, lcl);

    if (lcl->OperIsAnyLocal() && (oper->OperIs(GT_ADD, GT_OR, GT_XOR) || oper->OperIsShift()))
    {
        GenTree* lcl2 = oper->gtGetOp1();
        GenTree* one  = oper->gtGetOp2();
        if (oper->OperIsCommutative() && !one->IsIntegralConst())
            std::swap(lcl2, one);

        bool isDecrement = oper->OperIs(GT_ADD) && one->IsIntegralConst(-1);
        if (one->IsIntegralConst(1) || isDecrement)
        {
            unsigned lclNum = lcl->AsLclVarCommon()->GetLclNum();
            if (lcl2->OperIs(GT_LCL_VAR) && (lcl2->AsLclVar()->GetLclNum() == lclNum))
            {
                oper->AsOp()->gtOp1 = lcl2;
                oper->AsOp()->gtOp2 = isCondReversed ? m_comp->gtReverseCond(m_cond) : m_cond;
                if (isDecrement)
                    oper->ChangeOper(GT_SUB);

                oper->gtFlags |= m_cond->gtFlags & GTF_ALL_EFFECT;
                return oper;
            }
        }
    }
    return nullptr;
}

//-----------------------------------------------------------------------------
// TryTransformSelectOperOrZero: Try to trasform "cond ? oper(1, expr) : 0" into "oper(cond, expr)"
//
// Arguments:
//     trueInput  - expression to be evaluated when m_cond is true
//     falseInput - expression to be evaluated when m_cond is false
//
// Return Value:
//     The transformed expression, or null if no transformation took place
//
GenTree* OptIfConversionDsc::TryTransformSelectOperOrZero(GenTree* trueInput, GenTree* falseInput)
{
    GenTree* oper = trueInput;
    GenTree* zero = falseInput;

    bool isCondReversed = !zero->IsIntegralConst();
    if (isCondReversed)
        std::swap(oper, zero);

    if (zero->IsIntegralConst(0) && oper->OperIs(GT_AND, GT_LSH))
    {
        GenTree* one  = oper->gtGetOp1();
        GenTree* expr = oper->gtGetOp2();
        if (oper->OperIsCommutative() && !one->IsIntegralConst())
            std::swap(one, expr);

        if (one->IsIntegralConst(1))
        {
            oper->AsOp()->gtOp1 = isCondReversed ? m_comp->gtReverseCond(m_cond) : m_cond;
            oper->AsOp()->gtOp2 = expr;

            oper->gtFlags |= m_cond->gtFlags & GTF_ALL_EFFECT;
            return oper;
        }
    }
    return nullptr;
}

//-----------------------------------------------------------------------------
// TryTransformSelectToOrdinaryOps: Try transforming the identified if-else expressions to a single expression
//
// This is meant mostly for RISC-V where the condition (1 or 0) is stored in a regular general-purpose register
// which can be fed as an argument to standard operations, e.g.
//     * (cond ? 6 : 5) becomes (5 + cond)
//     * (cond ? -25 : -13) becomes (-25 >> cond)
//     * if (cond) a++; becomes (a + cond)
//     * (cond ? 1 << a : 0) becomes (cond << a)
//
// Arguments:
//     trueInput  - expression to be evaluated when m_cond is true, or null if there is no else expression
//     falseInput - expression to be evaluated when m_cond is false
//
// Return Value:
//     The transformed single expression equivalent to the if-else expressions, or null if no transformation took place
//
GenTree* OptIfConversionDsc::TryTransformSelectToOrdinaryOps(GenTree* trueInput, GenTree* falseInput)
{
    assert(falseInput != nullptr);

    if ((trueInput != nullptr && trueInput->IsIntegralConst()) && falseInput->IsIntegralConst())
    {
        int64_t trueVal  = trueInput->AsIntConCommon()->IntegralValue();
        int64_t falseVal = falseInput->AsIntConCommon()->IntegralValue();
        if (trueInput->TypeIs(TYP_INT) && falseInput->TypeIs(TYP_INT))
        {
            if (trueVal == 1 && falseVal == 0)
            {
                // compare ? true : false  -->  compare
                return m_cond;
            }
            else if (trueVal == 0 && falseVal == 1)
            {
                // compare ? false : true  -->  reversed_compare
                return m_comp->gtReverseCond(m_cond);
            }
        }
#ifdef TARGET_RISCV64
        bool               isCondReversed = false;
        IntConstSelectOper selectOper     = MatchIntConstSelectValues(trueVal, falseVal);
        if (!selectOper.isMatched())
        {
            isCondReversed = true;
            selectOper     = MatchIntConstSelectValues(falseVal, trueVal);
        }
        if (selectOper.isMatched())
        {
            GenTree* left  = isCondReversed ? trueInput : falseInput;
            GenTree* right = isCondReversed ? m_comp->gtReverseCond(m_cond) : m_cond;
            if (selectOper.bitIndex > 0)
            {
                assert(selectOper.oper == GT_LSH);
                left->AsIntConCommon()->SetIntegralValue(selectOper.bitIndex);
                std::swap(left, right);
            }
            return m_comp->gtNewOperNode(selectOper.oper, selectOper.type, left, right);
        }
        return nullptr;
#endif // TARGET_RISCV64
    }
#ifdef TARGET_RISCV64
    else
    {
        if (trueInput == nullptr)
        {
            assert(m_mainOper == GT_STORE_LCL_VAR && !m_doElseConversion);
            trueInput = m_thenOperation.node;
        }

        GenTree* transformed = TryTransformSelectOperOrLocal(trueInput, falseInput);
        if (transformed != nullptr)
            return transformed;

        transformed = TryTransformSelectOperOrZero(trueInput, falseInput);
        if (transformed != nullptr)
            return transformed;
    }
#endif // TARGET_RISCV64
    return nullptr;
}

//-----------------------------------------------------------------------------
// optIfConversion: If conversion
//
// Returns:
//   suitable phase status
//
PhaseStatus Compiler::optIfConversion()
{
    if (!opts.OptimizationEnabled())
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }

#if defined(DEBUG)
    if (JitConfig.JitDoIfConversion() == 0)
    {
        return PhaseStatus::MODIFIED_NOTHING;
    }
#endif

    bool madeChanges = false;

    // This phase does not respect SSA: local stores are deleted/moved.
    assert(!fgSsaValid);
    optReachableBitVecTraits = nullptr;

#if defined(TARGET_ARM64) || defined(TARGET_XARCH) || defined(TARGET_RISCV64)
    // Reverse iterate through the blocks.
    BasicBlock* block = fgLastBB;
    while (block != nullptr)
    {
        OptIfConversionDsc optIfConversionDsc(this, block);
        madeChanges |= optIfConversionDsc.optIfConvert();
        block = block->Prev();
    }
#endif

    return madeChanges ? PhaseStatus::MODIFIED_EVERYTHING : PhaseStatus::MODIFIED_NOTHING;
}
