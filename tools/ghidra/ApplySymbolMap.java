// Copied from topspin4-decomp (MIT, Copyright (c) 2026 Top Spin 4 decompilation contributors),
// tools/ghidra/ApplySymbolMap.java at 553f783. Changed: the evidence column may hold commas (it
// comes last, and csv.writer quotes it), and a name whose address Ghidra did not make a function
// creates one there instead of stopping the run.
// Apply reviewed function names from the repository's metadata-only symbol map.
// @category TopSpin4

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.SourceType;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;

public class ApplySymbolMap extends GhidraScript {
    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        if (args.length != 1) {
            throw new IllegalArgumentException("expected one symbol-map path");
        }

        Path mapPath = Paths.get(args[0]).toAbsolutePath();
        List<String> lines = Files.readAllLines(mapPath, StandardCharsets.UTF_8);
        if (lines.isEmpty() ||
            !lines.get(0).equals("module,address,name,confidence,evidence")) {
            throw new IllegalArgumentException("unexpected symbol-map header: " + mapPath);
        }

        int applied = 0;
        int created = 0;
        int missing = 0;
        for (int index = 1; index < lines.size(); index++) {
            String line = lines.get(index);
            if (line.isBlank()) {
                continue;
            }
            String[] fields = line.split(",", 5);
            if (fields.length != 5) {
                throw new IllegalArgumentException("invalid symbol-map row " + (index + 1));
            }
            if (!fields[0].equals(currentProgram.getName())) {
                continue;
            }

            Address address = currentProgram.getAddressFactory().getAddress(fields[1]);
            if (address == null) {
                throw new IllegalArgumentException("invalid address in symbol-map row " + (index + 1));
            }
            Function function = getFunctionAt(address);
            if (function == null) {
                function = createFunction(address, null);
                created++;
            }
            if (function == null) {
                printerr("no function could be made at " + fields[1] + " for " + fields[2]);
                missing++;
                continue;
            }
            function.setName(fields[2], SourceType.USER_DEFINED);
            applied++;
        }
        println("Applied " + applied + " function names from " + mapPath + " (" + created +
            " functions created, " + missing + " addresses without one)");
    }
}
