// Adapted from topspin4-decomp (MIT, Copyright (c) 2026 Top Spin 4 decompilation contributors),
// tools/ghidra/FixXbox360SaveHelpers.java at 553f783: the helper address is a script argument, so
// the repair runs on all three TU1 Modules (scripts/ghidra-project.ps1 passes each one's).
// Repair the shared Xenon register-save helpers misidentified as non-returning.
// @category TopSpin4

import ghidra.app.cmd.function.CreateFunctionCmd;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.FlowOverride;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.SourceType;

import java.util.Set;
import java.util.TreeSet;

public class FixXbox360SaveHelpers extends GhidraScript {
    private static final int FIRST_REGISTER = 14;
    private static final int LAST_SEPARATE_ENTRY = 29;

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 1) {
            throw new IllegalArgumentException("expected the __savegprlr_14 address, e.g. 0x8856ac40");
        }

        Address first = toAddr(Long.decode(args[0]));
        for (int register = FIRST_REGISTER; register <= 31; register++) {
            Address address = first.add((register - FIRST_REGISTER) * 4L);
            Instruction instruction = getInstructionAt(address);
            String expectedPrefix = "std r" + register + ",";
            if (instruction == null || !instruction.toString().startsWith(expectedPrefix)) {
                throw new IllegalStateException(
                    "unexpected save-helper instruction at " + address + ": " + instruction
                );
            }
        }
        Instruction saveLinkRegister = getInstructionAt(first.add(18 * 4L));
        Instruction returnInstruction = getInstructionAt(first.add(19 * 4L));
        if (saveLinkRegister == null || !saveLinkRegister.toString().equals("stw r12,-0x8(r1)") ||
            returnInstruction == null || !returnInstruction.toString().equals("blr")) {
            throw new IllegalStateException("unexpected save-helper tail at " + saveLinkRegister);
        }

        for (int register = FIRST_REGISTER; register <= LAST_SEPARATE_ENTRY; register++) {
            Address address = first.add((register - FIRST_REGISTER) * 4L);
            Function helper = getFunctionAt(address);
            if (helper == null) {
                throw new IllegalStateException("missing save-helper function at " + address);
            }
            helper.setNoReturn(false);
            helper.setName("_savegprlr_" + register, SourceType.USER_DEFINED);
        }

        int previousCallCount = -1;
        for (int pass = 0; pass < 8; pass++) {
            Set<Address> calls = collectCalls(first);
            for (Address callAddress : calls) {
                Instruction call = getInstructionAt(callAddress);
                if (call == null) {
                    continue;
                }
                if (call.getFlowOverride() != FlowOverride.NONE) {
                    call.setFlowOverride(FlowOverride.NONE);
                }
                Address fallThrough = callAddress.add(call.getDefaultFallThroughOffset());
                if (getInstructionAt(fallThrough) == null) {
                    disassemble(fallThrough);
                }
                Function caller = getFunctionContaining(callAddress);
                if (caller != null) {
                    CreateFunctionCmd.fixupFunctionBody(currentProgram, caller, monitor);
                }
            }
            println("Pass " + (pass + 1) + ": repaired " + calls.size() + " save-helper calls");
            if (calls.size() == previousCallCount) {
                break;
            }
            previousCallCount = calls.size();
        }
    }

    private Set<Address> collectCalls(Address first) {
        Set<Address> calls = new TreeSet<>();
        for (int register = FIRST_REGISTER; register <= LAST_SEPARATE_ENTRY; register++) {
            Address helperAddress = first.add((register - FIRST_REGISTER) * 4L);
            ReferenceIterator references = currentProgram.getReferenceManager()
                .getReferencesTo(helperAddress);
            while (references.hasNext()) {
                Reference reference = references.next();
                if (reference.getReferenceType().isCall()) {
                    calls.add(reference.getFromAddress());
                }
            }
        }
        return calls;
    }
}
