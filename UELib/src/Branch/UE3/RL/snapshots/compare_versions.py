#!/usr/bin/env python3
"""
Cross-version GNatives comparison for Rocket League rebuilds.

When a new RL patch ships, the engine binary's opcode dispatch table
(funcs_7FF6CD28592F = 0x7FF6CF2AA580 in v868) gets rebuilt with the same
112 handler addresses but reshuffled to different byte indices. Reverse-
engineering each handler from scratch is wasteful — the handlers haven't
changed, only their byte assignments have rotated. This script reads the
v868 snapshot table and a fresh dump of the new build's GNatives table,
matches handler addresses (with ASLR-offset compensation), and prints a
diff that tells you which bytes rotated.

Workflow:

  1. Open the new build's binary in IDA. Locate the new GNatives base
     (search for the dispatcher pattern `funcs_X[v3]` in `UStruct::SerializeExpr`,
     or grep the strings for "Unknown code token %02X" and follow xrefs to
     find the error handler — its address appears in many GNatives slots).

  2. Dump the table:

         from mcp_ida_pro_mcp import get_bytes
         result = get_bytes(regions=[{"addr": "0x<NEW_BASE>", "size": 896}])
         # 896 = 112 entries × 8 bytes per qword

     Save the 112 qword values (handler addresses) into a JSON file with
     this shape:

         {
           "imagebase": "0x7FF6CCFB0000",   # the new build's IDA imagebase
           "base_addr": "0x<NEW_BASE>",     # new GNatives table address
           "handlers": [
             "0x7FF6CD31ACB0",  # byte 0x00
             "0x7FF6CD2F0FB0",  # byte 0x01
             ...
           ]
         }

     Use the path UELib/src/Branch/UE3/RL/snapshots/v<NEW_VERSION>_dump.json
     so the file lives next to this script.

  3. Run this script:

         python compare_versions.py v<NEW_VERSION>_dump.json

     Output is a markdown table with one line per byte that rotated, a
     suggested updated `BuildTokenMap` block, and a list of bytes whose
     handler addresses don't appear anywhere in the v868 snapshot — those
     are NEW handlers that need fresh binary RE.

  4. Apply the suggested mapping to `EngineBranch.RL.cs` `BuildTokenMap`
     and run the sentinel-function regression set per CLAUDE.md.

This script intentionally has no dependencies beyond stdlib so it runs
under any Python ≥ 3.8.
"""

import json
import re
import sys
from pathlib import Path

V868_IMAGEBASE = 0x7FF6CCFB0000

# Hand-curated v868 mapping: byte → (handler_addr, token_class, wire_format_summary)
# Sourced from GNATIVES_SNAPSHOT_v868.md — keep in sync if that file changes.
# Errors handlers (sub_7FF6CD31ACB0) are deduplicated; multiple bytes legitimately
# share the error handler in the v868 binary (intentionally-unmapped opcodes).
V868_TABLE = {
    0x00: (0x7FF6CD31ACB0, "ContextAwareReturnTokenRL", "ERROR (handled by RL token)"),
    0x01: (0x7FF6CD2F0FB0, "EventSubscribeToken",       "2 sub-exprs + delegate subscribe"),
    0x05: (0x7FF6CD2F6800, "ArrayElementToken",         "2 sub-exprs + dispatch (alias 0x16)"),
    0x06: (0x7FF6CD2F0020, "BoolVariableToken",         "1 sub + 8-byte peek"),
    0x07: (0x7FF6CD2F7360, "FieldWrappedExpressionTokenRL", "UField/UObject ref + wrapped sub-expression"),
    0x09: (0x7FF6CD2F5930, "DiscardKeepTokenRL",        "optional 0x20 + 2 sub-exprs"),
    0x0B: (0x7FF6CD2F6DC0, "IntConstToken",             "4-byte INT"),
    0x0C: (0x7FF6CD2F0AA0, "LetBoolTokenRL",            "2 sub + bit-clear (LetBool-shape)"),
    0x0E: (0x7FF6CD2F63E0, "StructCmpEqToken",          "UStruct + 2 sub + struct-cmp"),
    0x0F: (0x7FF6CD2F5F00, "FinalFunctionTokenRL",      "8-byte UFunction* + dispatch"),
    0x10: (0x7FF6CD2F00C0, "NothingToken",              "Execution-beyond-end sentinel"),
    0x11: (0x7FF6CD2ED370, "OutVariableToken",          "8-byte qword + walk OutParms list"),
    0x12: (0x7FF6CD2F5740, "VariadicReturnValueTokenRL", "variadic body until 0x3E + trailing dispatch"),
    0x13: (0x7FF6CD2ED3E0, "StateVariableToken",        "state-frame/property storage access"),
    0x15: (0x7FF6CD2F59D0, "StringLengthTokenRL",       "1 sub FString length"),
    0x16: (0x7FF6CD2F6800, "DynamicArrayElementToken",  "alias 0x05"),
    0x17: (0x7FF6CD2F1580, "EventUnsubscribeToken",     "2 sub + delegate-list clear"),
    0x19: (0x7FF6CD2F1750, "ExtendedNativeFunctionToken", "1 byte + dispatch"),
    0x1A: (0x7FF6CD2F70C0, "DynamicCastToken",          "8-byte UClass* + 1 sub"),
    0x1B: (0x7FF6CD2F6AE0, "MetaClassCastToken",        "2 sub + 1-byte skip + optional 0x20 (alias 0x54)"),
    0x1C: (0x7FF6CD2F7030, "IntZeroToken",              "write 4-byte 0 (alias 0x27)"),
    0x1D: (0x7FF6CD21D420, "NothingToken",              "empty stub (alias 0x2E)"),
    0x1E: (0x7FF6CD2ED550, "ArrayElementToken",         "2 sub + bounds-check"),
    0x1F: (0x7FF6CD2F5C60, "SelfToken",                 "*a3 = a1"),
    0x20: (0x7FF6CD3027A0, "ReturnToken",               "HANDLE_OPTIONAL_DEBUG_INFO macro"),
    0x21: (0x7FF6CD2F5A40, "StringCastTokenRL",         "1 sub + property-export to FString"),
    0x22: (0x7FF6CD2F0610, "JumpToken",                 "u16 + absolute jump"),
    0x23: (0x7FF6CD2F7050, "NoObjectToken",             "write 8-byte 0"),
    0x25: (0x7FF6CD2F0590, "CaseToken",                 "u16 + conditional sub-expr (switch case)"),
    0x27: (0x7FF6CD2F7030, "IntZeroToken",              "alias 0x1C"),
    0x28: (0x7FF6CD2F5CB0, "ContextToken",              "sub + 2 bytes + UField + type + sub"),
    0x29: (0x7FF6CD2F0630, "JumpIfNotToken",            "u16 + sub"),
    0x2A: (0x7FF6CD2F70A0, "ByteConstToken",            "u8 read"),
    0x2B: (0x7FF6CD2F7010, "IntConstByteToken",         "i8 read"),
    0x2C: (0x7FF6CD308010, "StatementWrapperTokenRL",   "1 sub + 1 byte skip + optional 0x20"),
    0x2D: (0x7FF6CD2F06A0, "AssertExpressionTokenRL",   "u16 + byte + 3 sub-exprs + assert log"),
    0x2E: (0x7FF6CD21D420, "NothingToken",              "alias 0x1D"),
    0x2F: (0x7FF6CD2F7040, "IntOneToken",               "write 4-byte 1 (alias 0x3A)"),
    0x30: (0x7FF6CD2F9C50, "VectorConstToken",          "12 bytes (3 INTs)"),
    0x31: (0x7FF6CD2ED4A0, "OptionalArgSkipTokenRL",    "u16 + variadic body until 0x4F"),
    0x32: (0x7FF6CD2F6180, "InstanceDelegateTokenRL",   "8-byte UObject* + 8-byte FName"),
    0x33: (0x7FF6CD2F1770, "DynArrayResultTokenRL",     "2 sub + dynarray-result handling"),
    0x36: (0x7FF6CD2F7250, "PropertySetterDiscardTokenRL", "8-byte UProperty* + sub + zero-result"),
    0x37: (0x7FF6CD2F5810, "FloatConstToken",           "2 sub + u16 + conditional variadic"),
    0x38: (0x7FF6CD308710, "ClassContextToken",         "sub + 2 bytes + UField + type + sub"),
    0x39: (0x7FF6CD2F6FA0, "NameConstToken",            "8-byte qword (alias 0x3B, 0x43, 0x5A)"),
    0x3A: (0x7FF6CD2F7040, "TrueToken",                 "alias 0x2F"),
    0x3B: (0x7FF6CD2F6FA0, "NameConstToken",            "FName leaf (alias 0x39 handler)"),
    0x3E: (0x7FF6CD2F00B0, "EndFunctionParmsToken",     "qword=0; --Code (variadic terminator)"),
    0x40: (0x7FF6CD2F5F90, "DelegateFunctionToken",     "1 byte + UProperty* + FName"),
    0x41: (0x7FF6CD2F5E90, "VirtualFunctionToken",      "FName + state-aware lookup"),
    0x43: (0x7FF6CD2F6FA0, "ObjectConstToken",          "alias 0x39"),
    0x46: (0x7FF6CD2F12A0, "LetBoolToken",              "2 sub-exprs (LetBool-shape)"),
    0x47: (0x7FF6CD2F0360, "EmptyParmToken",            "1 byte (EmptyParmValue)"),
    0x48: (0x7FF6CD30D7C0, "FilterEditorOnlyTokenRL",   "u16 + FName-like qword + byte"),
    0x49: (0x7FF6CD2F0C60, "LetDelegateTokenRL",        "2 sub + delegate cleanup"),
    0x4A: (0x7FF6CD2F6590, "StructMemberToken",         "UProperty + UStruct + 2 bytes + sub"),
    0x4C: (0x7FF6CD2F08B0, "LetTokenRL",                "2 sub + assign-through-None log"),
    0x4D: (0x7FF6CD2F5B80, "StructValueTokenRL",        "8-byte UStruct + 1 sub"),
    0x50: (0x7FF6CD2F6E00, "StringConstToken",          "C-string until null"),
    0x51: (0x7FF6CD2F6EA0, "UnicodeStringConstToken",   "UTF-16 until null word"),
    0x52: (0x7FF6CD2ED450, "EatReturnValueToken",       "1 sub + write 0"),
    0x53: (0x7FF6CD2F6240, "StructCmpEqToken",          "UStruct + 2 sub + struct-cmp"),
    0x54: (0x7FF6CD2F6AE0, "EatReturnValueToken",       "alias 0x1B"),
    0x55: (0x7FF6CD2ED270, "InstanceVariableToken",     "8-byte UProperty* + this-relative addr"),
    0x56: (0x7FF6CD2F5B00, "NameConstToken",            "8-byte FName + state-fn-call log"),
    0x57: (0x7FF6CD2F0390, "SwitchToken",               "9 bytes (UField + type) + sub + case loop"),
    0x58: (0x7FF6CD2ED2D0, "DefaultVariableToken",      "8-byte UProperty* + object-flag check"),
    0x59: (0x7FF6CD2F5F20, "GlobalFunctionToken",       "FName + state-skip lookup"),
    0x5A: (0x7FF6CD2F6FA0, "LocalVariableToken",        "alias 0x39"),
    0x5B: (0x7FF6CD308170, "StructDefaultParameterTokenRL", "UStruct + sub + u16 + sub (none-coalescing)"),
    0x5C: (0x7FF6CD2F07F0, "GotoLabelToken",            "1 sub + FindLabel"),
    0x5D: (0x7FF6CD30D7B0, "JumpToken",                 "Code += 2 (no-op jump)"),
    0x5E: (0x7FF6CD309510, "LocalVariableToken",        "8-byte UProperty + locals/out-param accessor"),
    0x60: (0x7FF6CD2F9C90, "VectorConstToken",          "12 bytes (3 INTs)"),
    0x61: (0x7FF6CD3082F0, "NewExpressionTokenRL",      "5 sub-exprs (Outer, Name, Flags, Class, Template)"),
    0x62: (0x7FF6CD2F6FC0, "DelegateFunctionRefTokenRL", "8-byte FName leaf"),
    0x63: (0x7FF6CD2F03B0, "IntConstByteToken",         "signed byte"),
    0x64: (0x7FF6CD2F6DE0, "FloatConstToken",           "4-byte literal"),
    0x65: (0x7FF6CD2ED210, "LocalVariableToken",        "8-byte UProperty* + Locals[offset]"),
    0x66: (0x7FF6CD2F5C70, "BoolVariableToken",         "1 sub + flag-clear"),
    0x69: (0x7FF6CD2F0290, "IteratorPopToken",          "1 byte + Unexpected-iterator-pop log"),
    0x6A: (0x7FF6CD2F7060, "EmptyDelegateToken",        "zeroes 24 bytes + empty delegate"),
    0x6B: (0x7FF6CD2F7340, "PrimitiveCastToken",        "1 byte + cast sub-table dispatch"),
    0x6C: (0x7FF6CD2F0210, "ReturnNothingToken",        "8-byte UProperty + Control-reached log"),
}

ERROR_HANDLER = 0x7FF6CD31ACB0


def load_dump(path: Path) -> dict:
    with path.open() as f:
        data = json.load(f)
    if "handlers" not in data or len(data["handlers"]) != 112:
        raise SystemExit(f"{path}: expected `handlers` array of length 112")
    return data


def parse_addr(s: str | int) -> int:
    if isinstance(s, int):
        return s
    return int(s, 16) if s.startswith(("0x", "0X")) else int(s)


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__, file=sys.stderr)
        return 2

    dump_path = Path(sys.argv[1])
    if not dump_path.is_absolute():
        dump_path = Path(__file__).parent / dump_path
    if not dump_path.exists():
        print(f"error: dump file {dump_path} not found", file=sys.stderr)
        return 1

    new = load_dump(dump_path)
    new_imagebase = parse_addr(new["imagebase"])
    new_handlers = [parse_addr(h) for h in new["handlers"]]

    # Slide the v868 addresses into the new image's address space — the
    # comparison succeeds when the SAME handler appears in both binaries
    # at addresses whose `addr - imagebase` (RVA) matches.
    rva_to_v868 = {addr - V868_IMAGEBASE: (byte, klass, shape)
                   for byte, (addr, klass, shape) in V868_TABLE.items()}

    rotations: list[tuple[int, int, str, str]] = []  # (new_byte, v868_byte, klass, shape)
    unknown: list[tuple[int, int]] = []  # (new_byte, handler_addr)
    error_bytes: list[int] = []

    for new_byte, addr in enumerate(new_handlers):
        rva = addr - new_imagebase
        if addr - new_imagebase == ERROR_HANDLER - V868_IMAGEBASE:
            error_bytes.append(new_byte)
            continue
        if rva in rva_to_v868:
            v868_byte, klass, shape = rva_to_v868[rva]
            if v868_byte != new_byte:
                rotations.append((new_byte, v868_byte, klass, shape))
        else:
            unknown.append((new_byte, addr))

    print("# Cross-version diff vs v868\n")
    print(f"Source dump: `{dump_path.name}`")
    print(f"v868 imagebase: `0x{V868_IMAGEBASE:X}`")
    print(f"new imagebase:  `0x{new_imagebase:X}`\n")

    if rotations:
        print("## Rotated bytes (handler appears at a new index)\n")
        print("| new byte | v868 byte | token class                  | wire format / shape |")
        print("|----------|-----------|------------------------------|---------------------|")
        for new_byte, v868_byte, klass, shape in sorted(rotations):
            print(f"| 0x{new_byte:02X}     | 0x{v868_byte:02X}      | `{klass}` | {shape} |")
        print()
    else:
        print("_No bytes rotated — token map is unchanged._\n")

    if unknown:
        print("## Bytes with no v868 match (need fresh RE)\n")
        for new_byte, addr in sorted(unknown):
            print(f"- 0x{new_byte:02X} → `0x{addr:X}` (RVA `0x{addr - new_imagebase:X}`)")
        print()

    print(f"## Error-handler bytes (intentionally unmapped): "
          f"{len(error_bytes)} entries\n")

    if rotations:
        print("## Suggested BuildTokenMap delta\n")
        print("Replace the corresponding entries in `EngineBranch.RL.cs`:\n")
        print("```csharp")
        for new_byte, v868_byte, klass, shape in sorted(rotations):
            print(f"{{ 0x{new_byte:02X}, typeof({klass}) }},  // was at 0x{v868_byte:02X} in v868")
        print("```\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
