using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GBZ80AsmMetrics.Core
{
    /// <summary>
    /// Database of all Game Boy Z80 opcodes with lookup functionality
    /// </summary>
    public class OpcodeDatabase
    {
        private static OpcodeDatabase _instance;
        public static OpcodeDatabase Instance => _instance ?? (_instance = new OpcodeDatabase());

        private readonly Dictionary<string, OpcodeInfo> _unprefixedOpcodes;
        private readonly Dictionary<string, OpcodeInfo> _cbPrefixedOpcodes;

        private static readonly string[] CbInstructions = { "RLC", "RRC", "RL", "RR", "SLA", "SRA", "SWAP", "SRL", "BIT", "RES", "SET" };
        private static readonly string[] Registers = { "A", "B", "C", "D", "E", "H", "L", "AF", "BC", "DE", "HL", "SP", "PC" };
        private static readonly string[] Conditions = { "Z", "NZ", "C", "NC" };

        private OpcodeDatabase()
        {
            _unprefixedOpcodes = new Dictionary<string, OpcodeInfo>(StringComparer.OrdinalIgnoreCase);
            _cbPrefixedOpcodes = new Dictionary<string, OpcodeInfo>(StringComparer.OrdinalIgnoreCase);
            LoadOpcodes();
        }

        private void LoadOpcodes()
        {
            // Load embedded JSON resources or use hardcoded data
            LoadUnprefixedOpcodes();
            LoadCbPrefixedOpcodes();
        }

        private void LoadUnprefixedOpcodes()
        {
            // Hardcoded opcode data for unprefixed instructions
            var opcodes = new[]
            {
                // 0x00 - 0x0F
                new OpcodeInfo { Opcode = 0x00, Mnemonic = "NOP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x01, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "BC", "d16" } },
                new OpcodeInfo { Opcode = 0x02, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[BC]", "A" } },
                new OpcodeInfo { Opcode = 0x03, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "BC" } },
                new OpcodeInfo { Opcode = 0x04, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0x05, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0x06, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "d8" } },
                new OpcodeInfo { Opcode = 0x07, Mnemonic = "RLCA", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("0", "0", "0", "C"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x08, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 20 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[a16]", "SP" } },
                new OpcodeInfo { Opcode = 0x09, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "0", "H", "C"), Operands = new[] { "HL", "BC" } },
                new OpcodeInfo { Opcode = 0x0A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[BC]" } },
                new OpcodeInfo { Opcode = 0x0B, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "BC" } },
                new OpcodeInfo { Opcode = 0x0C, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0x0D, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0x0E, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "d8" } },
                new OpcodeInfo { Opcode = 0x0F, Mnemonic = "RRCA", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("0", "0", "0", "C"), Operands = new string[0] },

                // 0x10 - 0x1F
                new OpcodeInfo { Opcode = 0x10, Mnemonic = "STOP", Bytes = 2, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x11, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "DE", "d16" } },
                new OpcodeInfo { Opcode = 0x12, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[DE]", "A" } },
                new OpcodeInfo { Opcode = 0x13, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "DE" } },
                new OpcodeInfo { Opcode = 0x14, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0x15, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0x16, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "d8" } },
                new OpcodeInfo { Opcode = 0x17, Mnemonic = "RLA", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("0", "0", "0", "C"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x18, Mnemonic = "JR", Bytes = 2, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "r8" } },
                new OpcodeInfo { Opcode = 0x19, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "0", "H", "C"), Operands = new[] { "HL", "DE" } },
                new OpcodeInfo { Opcode = 0x1A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[DE]" } },
                new OpcodeInfo { Opcode = 0x1B, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "DE" } },
                new OpcodeInfo { Opcode = 0x1C, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0x1D, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0x1E, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "d8" } },
                new OpcodeInfo { Opcode = 0x1F, Mnemonic = "RRA", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("0", "0", "0", "C"), Operands = new string[0] },

                // 0x20 - 0x2F
                new OpcodeInfo { Opcode = 0x20, Mnemonic = "JR", Bytes = 2, Cycles = new[] { 12, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NZ", "r8" } },
                new OpcodeInfo { Opcode = 0x21, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "HL", "d16" } },
                new OpcodeInfo { Opcode = 0x22, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL+]", "A" } },
                new OpcodeInfo { Opcode = 0x23, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "HL" } },
                new OpcodeInfo { Opcode = 0x24, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0x25, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0x26, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "d8" } },
                new OpcodeInfo { Opcode = 0x27, Mnemonic = "DAA", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "-", "0", "C"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x28, Mnemonic = "JR", Bytes = 2, Cycles = new[] { 12, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "Z", "r8" } },
                new OpcodeInfo { Opcode = 0x29, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "0", "H", "C"), Operands = new[] { "HL", "HL" } },
                new OpcodeInfo { Opcode = 0x2A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[HL+]" } },
                new OpcodeInfo { Opcode = 0x2B, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "HL" } },
                new OpcodeInfo { Opcode = 0x2C, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0x2D, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0x2E, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "d8" } },
                new OpcodeInfo { Opcode = 0x2F, Mnemonic = "CPL", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "1", "1", "-"), Operands = new string[0] },

                // 0x30 - 0x3F
                new OpcodeInfo { Opcode = 0x30, Mnemonic = "JR", Bytes = 2, Cycles = new[] { 12, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NC", "r8" } },
                new OpcodeInfo { Opcode = 0x31, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "SP", "d16" } },
                new OpcodeInfo { Opcode = 0x32, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL-]", "A" } },
                new OpcodeInfo { Opcode = 0x33, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "SP" } },
                new OpcodeInfo { Opcode = 0x34, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 12 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0x35, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 12 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0x36, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "d8" } },
                new OpcodeInfo { Opcode = 0x37, Mnemonic = "SCF", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "0", "0", "1"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x38, Mnemonic = "JR", Bytes = 2, Cycles = new[] { 12, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "r8" } },
                new OpcodeInfo { Opcode = 0x39, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "0", "H", "C"), Operands = new[] { "HL", "SP" } },
                new OpcodeInfo { Opcode = 0x3A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[HL-]" } },
                new OpcodeInfo { Opcode = 0x3B, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "SP" } },
                new OpcodeInfo { Opcode = 0x3C, Mnemonic = "INC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "-"), Operands = new[] { "A" } },
                new OpcodeInfo { Opcode = 0x3D, Mnemonic = "DEC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "-"), Operands = new[] { "A" } },
                new OpcodeInfo { Opcode = 0x3E, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "d8" } },
                new OpcodeInfo { Opcode = 0x3F, Mnemonic = "CCF", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "0", "0", "C"), Operands = new string[0] },

                // 0x40 - 0x7F: LD r,r and LD r,[HL] and LD [HL],r (with HALT at 0x76)
                new OpcodeInfo { Opcode = 0x40, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "B" } },
                new OpcodeInfo { Opcode = 0x41, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "C" } },
                new OpcodeInfo { Opcode = 0x42, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "D" } },
                new OpcodeInfo { Opcode = 0x43, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "E" } },
                new OpcodeInfo { Opcode = 0x44, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "H" } },
                new OpcodeInfo { Opcode = 0x45, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "L" } },
                new OpcodeInfo { Opcode = 0x46, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "[HL]" } },
                new OpcodeInfo { Opcode = 0x47, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "B", "A" } },
                new OpcodeInfo { Opcode = 0x48, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "B" } },
                new OpcodeInfo { Opcode = 0x49, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "C" } },
                new OpcodeInfo { Opcode = 0x4A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "D" } },
                new OpcodeInfo { Opcode = 0x4B, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "E" } },
                new OpcodeInfo { Opcode = 0x4C, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "H" } },
                new OpcodeInfo { Opcode = 0x4D, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "L" } },
                new OpcodeInfo { Opcode = 0x4E, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "[HL]" } },
                new OpcodeInfo { Opcode = 0x4F, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "A" } },
                new OpcodeInfo { Opcode = 0x50, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "B" } },
                new OpcodeInfo { Opcode = 0x51, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "C" } },
                new OpcodeInfo { Opcode = 0x52, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "D" } },
                new OpcodeInfo { Opcode = 0x53, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "E" } },
                new OpcodeInfo { Opcode = 0x54, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "H" } },
                new OpcodeInfo { Opcode = 0x55, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "L" } },
                new OpcodeInfo { Opcode = 0x56, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "[HL]" } },
                new OpcodeInfo { Opcode = 0x57, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "D", "A" } },
                new OpcodeInfo { Opcode = 0x58, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "B" } },
                new OpcodeInfo { Opcode = 0x59, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "C" } },
                new OpcodeInfo { Opcode = 0x5A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "D" } },
                new OpcodeInfo { Opcode = 0x5B, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "E" } },
                new OpcodeInfo { Opcode = 0x5C, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "H" } },
                new OpcodeInfo { Opcode = 0x5D, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "L" } },
                new OpcodeInfo { Opcode = 0x5E, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "[HL]" } },
                new OpcodeInfo { Opcode = 0x5F, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "E", "A" } },
                new OpcodeInfo { Opcode = 0x60, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "B" } },
                new OpcodeInfo { Opcode = 0x61, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "C" } },
                new OpcodeInfo { Opcode = 0x62, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "D" } },
                new OpcodeInfo { Opcode = 0x63, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "E" } },
                new OpcodeInfo { Opcode = 0x64, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "H" } },
                new OpcodeInfo { Opcode = 0x65, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "L" } },
                new OpcodeInfo { Opcode = 0x66, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "[HL]" } },
                new OpcodeInfo { Opcode = 0x67, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "H", "A" } },
                new OpcodeInfo { Opcode = 0x68, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "B" } },
                new OpcodeInfo { Opcode = 0x69, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "C" } },
                new OpcodeInfo { Opcode = 0x6A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "D" } },
                new OpcodeInfo { Opcode = 0x6B, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "E" } },
                new OpcodeInfo { Opcode = 0x6C, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "H" } },
                new OpcodeInfo { Opcode = 0x6D, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "L" } },
                new OpcodeInfo { Opcode = 0x6E, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "[HL]" } },
                new OpcodeInfo { Opcode = 0x6F, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "L", "A" } },
                new OpcodeInfo { Opcode = 0x70, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "B" } },
                new OpcodeInfo { Opcode = 0x71, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "C" } },
                new OpcodeInfo { Opcode = 0x72, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "D" } },
                new OpcodeInfo { Opcode = 0x73, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "E" } },
                new OpcodeInfo { Opcode = 0x74, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "H" } },
                new OpcodeInfo { Opcode = 0x75, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "L" } },
                new OpcodeInfo { Opcode = 0x76, Mnemonic = "HALT", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0x77, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[HL]", "A" } },
                new OpcodeInfo { Opcode = 0x78, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "B" } },
                new OpcodeInfo { Opcode = 0x79, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "C" } },
                new OpcodeInfo { Opcode = 0x7A, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "D" } },
                new OpcodeInfo { Opcode = 0x7B, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "E" } },
                new OpcodeInfo { Opcode = 0x7C, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "H" } },
                new OpcodeInfo { Opcode = 0x7D, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "L" } },
                new OpcodeInfo { Opcode = 0x7E, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[HL]" } },
                new OpcodeInfo { Opcode = 0x7F, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "A" } },

                // 0x80 - 0x8F: ADD A,r and ADC A,r
                new OpcodeInfo { Opcode = 0x80, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "B" } },
                new OpcodeInfo { Opcode = 0x81, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "C" } },
                new OpcodeInfo { Opcode = 0x82, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "D" } },
                new OpcodeInfo { Opcode = 0x83, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "E" } },
                new OpcodeInfo { Opcode = 0x84, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "H" } },
                new OpcodeInfo { Opcode = 0x85, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "L" } },
                new OpcodeInfo { Opcode = 0x86, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "[HL]" } },
                new OpcodeInfo { Opcode = 0x87, Mnemonic = "ADD", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "A" } },
                new OpcodeInfo { Opcode = 0x88, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "B" } },
                new OpcodeInfo { Opcode = 0x89, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "C" } },
                new OpcodeInfo { Opcode = 0x8A, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "D" } },
                new OpcodeInfo { Opcode = 0x8B, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "E" } },
                new OpcodeInfo { Opcode = 0x8C, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "H" } },
                new OpcodeInfo { Opcode = 0x8D, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "L" } },
                new OpcodeInfo { Opcode = 0x8E, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "[HL]" } },
                new OpcodeInfo { Opcode = 0x8F, Mnemonic = "ADC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "A" } },

                // 0x90 - 0x9F: SUB r and SBC A,r
                new OpcodeInfo { Opcode = 0x90, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0x91, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0x92, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0x93, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0x94, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0x95, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0x96, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0x97, Mnemonic = "SUB", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A" } },
                new OpcodeInfo { Opcode = 0x98, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "B" } },
                new OpcodeInfo { Opcode = 0x99, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "C" } },
                new OpcodeInfo { Opcode = 0x9A, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "D" } },
                new OpcodeInfo { Opcode = 0x9B, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "E" } },
                new OpcodeInfo { Opcode = 0x9C, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "H" } },
                new OpcodeInfo { Opcode = 0x9D, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "L" } },
                new OpcodeInfo { Opcode = 0x9E, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "[HL]" } },
                new OpcodeInfo { Opcode = 0x9F, Mnemonic = "SBC", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "A" } },

                // 0xA0 - 0xAF: AND r and XOR r
                new OpcodeInfo { Opcode = 0xA0, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0xA1, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0xA2, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0xA3, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0xA4, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0xA5, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0xA6, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0xA7, Mnemonic = "AND", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "A" } },
                new OpcodeInfo { Opcode = 0xA8, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0xA9, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0xAA, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0xAB, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0xAC, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0xAD, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0xAE, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0xAF, Mnemonic = "XOR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "A" } },

                // 0xB0 - 0xBF: OR r and CP r
                new OpcodeInfo { Opcode = 0xB0, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0xB1, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0xB2, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0xB3, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0xB4, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0xB5, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0xB6, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0xB7, Mnemonic = "OR", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "A" } },
                new OpcodeInfo { Opcode = 0xB8, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "B" } },
                new OpcodeInfo { Opcode = 0xB9, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0xBA, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "D" } },
                new OpcodeInfo { Opcode = 0xBB, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "E" } },
                new OpcodeInfo { Opcode = 0xBC, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "H" } },
                new OpcodeInfo { Opcode = 0xBD, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "L" } },
                new OpcodeInfo { Opcode = 0xBE, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "[HL]" } },
                new OpcodeInfo { Opcode = 0xBF, Mnemonic = "CP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A" } },

                // 0xC0 - 0xCF
                new OpcodeInfo { Opcode = 0xC0, Mnemonic = "RET", Bytes = 1, Cycles = new[] { 20, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NZ" } },
                new OpcodeInfo { Opcode = 0xC1, Mnemonic = "POP", Bytes = 1, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "BC" } },
                new OpcodeInfo { Opcode = 0xC2, Mnemonic = "JP", Bytes = 3, Cycles = new[] { 16, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NZ", "a16" } },
                new OpcodeInfo { Opcode = 0xC3, Mnemonic = "JP", Bytes = 3, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "a16" } },
                new OpcodeInfo { Opcode = 0xC4, Mnemonic = "CALL", Bytes = 3, Cycles = new[] { 24, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NZ", "a16" } },
                new OpcodeInfo { Opcode = 0xC5, Mnemonic = "PUSH", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "BC" } },
                new OpcodeInfo { Opcode = 0xC6, Mnemonic = "ADD", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "d8" } },
                new OpcodeInfo { Opcode = 0xC7, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "00H" } },
                new OpcodeInfo { Opcode = 0xC8, Mnemonic = "RET", Bytes = 1, Cycles = new[] { 20, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "Z" } },
                new OpcodeInfo { Opcode = 0xC9, Mnemonic = "RET", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0xCA, Mnemonic = "JP", Bytes = 3, Cycles = new[] { 16, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "Z", "a16" } },
                new OpcodeInfo { Opcode = 0xCB, Mnemonic = "PREFIX", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "CB" } },
                new OpcodeInfo { Opcode = 0xCC, Mnemonic = "CALL", Bytes = 3, Cycles = new[] { 24, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "Z", "a16" } },
                new OpcodeInfo { Opcode = 0xCD, Mnemonic = "CALL", Bytes = 3, Cycles = new[] { 24 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "a16" } },
                new OpcodeInfo { Opcode = 0xCE, Mnemonic = "ADC", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "H", "C"), Operands = new[] { "A", "d8" } },
                new OpcodeInfo { Opcode = 0xCF, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "08H" } },

                // 0xD0 - 0xDF
                new OpcodeInfo { Opcode = 0xD0, Mnemonic = "RET", Bytes = 1, Cycles = new[] { 20, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NC" } },
                new OpcodeInfo { Opcode = 0xD1, Mnemonic = "POP", Bytes = 1, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "DE" } },
                new OpcodeInfo { Opcode = 0xD2, Mnemonic = "JP", Bytes = 3, Cycles = new[] { 16, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NC", "a16" } },
                new OpcodeInfo { Opcode = 0xD4, Mnemonic = "CALL", Bytes = 3, Cycles = new[] { 24, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "NC", "a16" } },
                new OpcodeInfo { Opcode = 0xD5, Mnemonic = "PUSH", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "DE" } },
                new OpcodeInfo { Opcode = 0xD6, Mnemonic = "SUB", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "d8" } },
                new OpcodeInfo { Opcode = 0xD7, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "10H" } },
                new OpcodeInfo { Opcode = 0xD8, Mnemonic = "RET", Bytes = 1, Cycles = new[] { 20, 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C" } },
                new OpcodeInfo { Opcode = 0xD9, Mnemonic = "RETI", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0xDA, Mnemonic = "JP", Bytes = 3, Cycles = new[] { 16, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "a16" } },
                new OpcodeInfo { Opcode = 0xDC, Mnemonic = "CALL", Bytes = 3, Cycles = new[] { 24, 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "C", "a16" } },
                new OpcodeInfo { Opcode = 0xDE, Mnemonic = "SBC", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "A", "d8" } },
                new OpcodeInfo { Opcode = 0xDF, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "18H" } },

                // 0xE0 - 0xEF
                new OpcodeInfo { Opcode = 0xE0, Mnemonic = "LDH", Bytes = 2, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[a8]", "A" } },
                new OpcodeInfo { Opcode = 0xE1, Mnemonic = "POP", Bytes = 1, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "HL" } },
                new OpcodeInfo { Opcode = 0xE2, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[C]", "A" } },
                new OpcodeInfo { Opcode = 0xE5, Mnemonic = "PUSH", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "HL" } },
                new OpcodeInfo { Opcode = 0xE6, Mnemonic = "AND", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "1", "0"), Operands = new[] { "d8" } },
                new OpcodeInfo { Opcode = 0xE7, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "20H" } },
                new OpcodeInfo { Opcode = 0xE8, Mnemonic = "ADD", Bytes = 2, Cycles = new[] { 16 }, Flags = new FlagEffect("0", "0", "H", "C"), Operands = new[] { "SP", "r8" } },
                new OpcodeInfo { Opcode = 0xE9, Mnemonic = "JP", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "HL" } },
                new OpcodeInfo { Opcode = 0xEA, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "[a16]", "A" } },
                new OpcodeInfo { Opcode = 0xEE, Mnemonic = "XOR", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "d8" } },
                new OpcodeInfo { Opcode = 0xEF, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "28H" } },

                // 0xF0 - 0xFF
                new OpcodeInfo { Opcode = 0xF0, Mnemonic = "LDH", Bytes = 2, Cycles = new[] { 12 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[a8]" } },
                new OpcodeInfo { Opcode = 0xF1, Mnemonic = "POP", Bytes = 1, Cycles = new[] { 12 }, Flags = new FlagEffect("Z", "N", "H", "C"), Operands = new[] { "AF" } },
                new OpcodeInfo { Opcode = 0xF2, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[C]" } },
                new OpcodeInfo { Opcode = 0xF3, Mnemonic = "DI", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0xF5, Mnemonic = "PUSH", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "AF" } },
                new OpcodeInfo { Opcode = 0xF6, Mnemonic = "OR", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "0", "0", "0"), Operands = new[] { "d8" } },
                new OpcodeInfo { Opcode = 0xF7, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "30H" } },
                new OpcodeInfo { Opcode = 0xF8, Mnemonic = "LD", Bytes = 2, Cycles = new[] { 12 }, Flags = new FlagEffect("0", "0", "H", "C"), Operands = new[] { "HL", "SP+r8" } },
                new OpcodeInfo { Opcode = 0xF9, Mnemonic = "LD", Bytes = 1, Cycles = new[] { 8 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "SP", "HL" } },
                new OpcodeInfo { Opcode = 0xFA, Mnemonic = "LD", Bytes = 3, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "A", "[a16]" } },
                new OpcodeInfo { Opcode = 0xFB, Mnemonic = "EI", Bytes = 1, Cycles = new[] { 4 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new string[0] },
                new OpcodeInfo { Opcode = 0xFE, Mnemonic = "CP", Bytes = 2, Cycles = new[] { 8 }, Flags = new FlagEffect("Z", "1", "H", "C"), Operands = new[] { "d8" } },
                new OpcodeInfo { Opcode = 0xFF, Mnemonic = "RST", Bytes = 1, Cycles = new[] { 16 }, Flags = new FlagEffect("-", "-", "-", "-"), Operands = new[] { "38H" } },
            };

            foreach (var op in opcodes)
            {
                string key = BuildLookupKey(op.Mnemonic, op.Operands);
                _unprefixedOpcodes[key] = op;
            }
        }

        private void LoadCbPrefixedOpcodes()
        {
            // CB-prefixed opcodes
            var regList = new[] { "B", "C", "D", "E", "H", "L", "[HL]", "A" };

            // RLC, RRC, RL, RR, SLA, SRA, SWAP, SRL (0x00-0x3F)
            var shiftOps = new[] { "RLC", "RRC", "RL", "RR", "SLA", "SRA", "SWAP", "SRL" };
            for (int opIdx = 0; opIdx < shiftOps.Length; opIdx++)
            {
                for (int regIdx = 0; regIdx < regList.Length; regIdx++)
                {
                    int opcode = opIdx * 8 + regIdx;
                    int cycles = regIdx == 6 ? 16 : 8; // [HL] takes more cycles
                    var flags = shiftOps[opIdx] == "SWAP"
                        ? new FlagEffect("Z", "0", "0", "0")
                        : new FlagEffect("Z", "0", "0", "C");

                    var info = new OpcodeInfo
                    {
                        Opcode = opcode,
                        Mnemonic = shiftOps[opIdx],
                        Bytes = 2,
                        Cycles = new[] { cycles },
                        Flags = flags,
                        Operands = new[] { regList[regIdx] }
                    };

                    string key = BuildLookupKey(info.Mnemonic, info.Operands);
                    _cbPrefixedOpcodes[key] = info;
                }
            }

            // BIT, RES, SET (0x40-0xFF)
            var bitOps = new[] { "BIT", "RES", "SET" };
            for (int opType = 0; opType < bitOps.Length; opType++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    for (int regIdx = 0; regIdx < regList.Length; regIdx++)
                    {
                        int opcode = 0x40 + opType * 0x40 + bit * 8 + regIdx;
                        int cycles = regIdx == 6 ? (bitOps[opType] == "BIT" ? 12 : 16) : 8;
                        var flags = bitOps[opType] == "BIT"
                            ? new FlagEffect("Z", "0", "1", "-")
                            : new FlagEffect("-", "-", "-", "-");

                        var info = new OpcodeInfo
                        {
                            Opcode = opcode,
                            Mnemonic = bitOps[opType],
                            Bytes = 2,
                            Cycles = new[] { cycles },
                            Flags = flags,
                            Operands = new[] { bit.ToString(), regList[regIdx] }
                        };

                        string key = BuildLookupKey(info.Mnemonic, info.Operands);
                        _cbPrefixedOpcodes[key] = info;
                    }
                }
            }
        }

        private string BuildLookupKey(string mnemonic, string[] operands)
        {
            string normalizedMnemonic = mnemonic.ToUpperInvariant();
            string normalizedOperands = string.Join(",", operands.Select(NormalizeOperand));
            return string.IsNullOrEmpty(normalizedOperands) ? normalizedMnemonic : $"{normalizedMnemonic} {normalizedOperands}";
        }

        private string NormalizeOperand(string operand)
        {
            return operand.ToUpperInvariant()
                .Replace(" ", "")
                .Replace("(HL+)", "[HL+]")
                .Replace("(HL-)", "[HL-]")
                .Replace("(HLI)", "[HL+]")
                .Replace("(HLD)", "[HL-]");
        }

        public OpcodeInfo Lookup(string mnemonic, string[] operands)
        {
            string normalizedMnemonic = mnemonic.ToUpperInvariant();

            // Check for CB-prefixed instructions
            if (CbInstructions.Contains(normalizedMnemonic))
            {
                return LookupCB(normalizedMnemonic, operands);
            }

            // Try direct lookup first
            string key = BuildSearchKey(normalizedMnemonic, operands);
            if (_unprefixedOpcodes.TryGetValue(key, out var result))
            {
                return result;
            }

            // Try with wildcard patterns
            return FindWithWildcard(normalizedMnemonic, operands);
        }

        private OpcodeInfo LookupCB(string mnemonic, string[] operands)
        {
            string key = BuildSearchKey(mnemonic, operands);
            if (_cbPrefixedOpcodes.TryGetValue(key, out var result))
            {
                return result;
            }

            // For BIT/SET/RES with constant bit numbers, try all bit positions
            if (new[] { "BIT", "SET", "RES" }.Contains(mnemonic) && operands.Length == 2)
            {
                string register = NormalizeSearchOperand(operands[1]);
                for (int bit = 0; bit <= 7; bit++)
                {
                    string tryKey = $"{mnemonic} {bit},{register}";
                    if (_cbPrefixedOpcodes.TryGetValue(tryKey, out result))
                    {
                        return result;
                    }
                }
            }

            // For single operand CB instructions
            if (operands.Length == 1)
            {
                string register = NormalizeSearchOperand(operands[0]);
                string tryKey = $"{mnemonic} {register}";
                if (_cbPrefixedOpcodes.TryGetValue(tryKey, out result))
                {
                    return result;
                }
            }

            return null;
        }

        private string BuildSearchKey(string mnemonic, string[] operands)
        {
            string normalizedOperands = string.Join(",", operands.Select(NormalizeSearchOperand));
            return string.IsNullOrEmpty(normalizedOperands) ? mnemonic : $"{mnemonic} {normalizedOperands}";
        }

        private string NormalizeSearchOperand(string operand)
        {
            string normalized = operand.ToUpperInvariant().Trim();

            // Handle RGBDS functions
            if (Regex.IsMatch(normalized, @"^(BANK|HIGH|LOW|SIZEOF|STARTOF)\s*\("))
            {
                return "D8";
            }

            // Handle memory addressing variations
            normalized = normalized
                .Replace(" ", "")
                .Replace("(HL+)", "[HL+]")
                .Replace("(HL-)", "[HL-]")
                .Replace("(HLI)", "[HL+]")
                .Replace("(HLD)", "[HL-]")
                .Replace("[HLI]", "[HL+]")
                .Replace("[HLD]", "[HL-]");

            // Convert parentheses to brackets
            normalized = Regex.Replace(normalized, @"\(([^)]+)\)", "[$1]");

            // Check if it's an immediate value
            if (IsImmediateValue(normalized))
            {
                if (Is16BitValue(operand))
                    return "D16";
                return "D8";
            }

            // Check for memory address
            if (normalized.StartsWith("[") && normalized.EndsWith("]"))
            {
                string inner = normalized.Substring(1, normalized.Length - 2);
                var registerIndirect = new[] { "HL", "BC", "DE", "C", "HL+", "HL-" };
                if (registerIndirect.Contains(inner))
                {
                    return normalized;
                }

                if (IsImmediateValue(inner))
                {
                    if (inner.StartsWith("$FF") || inner.StartsWith("FF"))
                        return "[A8]";
                    return "[A16]";
                }

                // Label or constant in brackets
                return "[A16]";
            }

            // Handle relative addresses for JR
            if (IsRelativeAddress(operand))
            {
                return "R8";
            }

            return normalized;
        }

        private bool IsImmediateValue(string operand)
        {
            return Regex.IsMatch(operand, @"^(\$[0-9A-Fa-f]+|%[01]+|&[0-7]+|\d+|"".""|[\w]+)$")
                && !IsRegister(operand)
                && !IsCondition(operand);
        }

        private bool IsRegister(string operand)
        {
            return Registers.Contains(operand.ToUpperInvariant());
        }

        private bool IsCondition(string operand)
        {
            return Conditions.Contains(operand.ToUpperInvariant());
        }

        private bool Is16BitValue(string operand)
        {
            var hexMatch = Regex.Match(operand, @"\$([0-9A-Fa-f]+)");
            if (hexMatch.Success && hexMatch.Groups[1].Value.Length > 2)
                return true;

            if (Regex.IsMatch(operand, @"^[A-Za-z_]\w*$") && !IsRegister(operand) && !IsCondition(operand))
                return true;

            return false;
        }

        private bool IsRelativeAddress(string operand)
        {
            return Regex.IsMatch(operand, @"^\.?\w+$") && !IsRegister(operand) && !IsCondition(operand);
        }

        private OpcodeInfo FindWithWildcard(string mnemonic, string[] operands)
        {
            var patterns = GenerateSearchPatterns(mnemonic, operands);

            foreach (var pattern in patterns)
            {
                if (_unprefixedOpcodes.TryGetValue(pattern, out var result))
                {
                    return result;
                }
            }

            return null;
        }

        private List<string> GenerateSearchPatterns(string mnemonic, string[] operands)
        {
            var patterns = new List<string>();

            if (operands.Length == 0)
            {
                patterns.Add(mnemonic);
            }
            else if (operands.Length == 1)
            {
                var op = NormalizeSearchOperand(operands[0]);
                patterns.Add($"{mnemonic} {op}");
                patterns.Add($"{mnemonic} D8");
                patterns.Add($"{mnemonic} D16");
                patterns.Add($"{mnemonic} A16");
                patterns.Add($"{mnemonic} R8");
                patterns.Add($"{mnemonic} [A8]");
                patterns.Add($"{mnemonic} [A16]");
                patterns.Add($"{mnemonic} [HL]");
            }
            else if (operands.Length == 2)
            {
                var op1 = NormalizeSearchOperand(operands[0]);
                var op2 = NormalizeSearchOperand(operands[1]);
                patterns.Add($"{mnemonic} {op1},{op2}");
                patterns.Add($"{mnemonic} {op1},D8");
                patterns.Add($"{mnemonic} {op1},D16");
                patterns.Add($"{mnemonic} {op1},A16");
                patterns.Add($"{mnemonic} {op1},R8");
                patterns.Add($"{mnemonic} {op1},[A8]");
                patterns.Add($"{mnemonic} {op1},[A16]");
                patterns.Add($"{mnemonic} [A8],{op2}");
                patterns.Add($"{mnemonic} [A16],{op2}");
                patterns.Add($"{mnemonic} [HL],D8");

                // Special case for SP+r8
                if (op1 == "HL" && op2.Contains("SP"))
                {
                    patterns.Add($"{mnemonic} HL,SP+R8");
                }
            }

            return patterns;
        }

        public string GetOpcodeHex(OpcodeInfo info, bool isCBPrefixed)
        {
            if (isCBPrefixed)
            {
                return $"CB {info.Opcode:X2}";
            }
            return $"{info.Opcode:X2}";
        }

        public bool IsCBPrefixedInstruction(string mnemonic)
        {
            return CbInstructions.Contains(mnemonic.ToUpperInvariant());
        }
    }

    /// <summary>
    /// Information about a single opcode
    /// </summary>
    public class OpcodeInfo
    {
        public int Opcode { get; set; }
        public string Mnemonic { get; set; }
        public int Bytes { get; set; }
        public int[] Cycles { get; set; }
        public FlagEffect Flags { get; set; }
        public string[] Operands { get; set; }
    }

    /// <summary>
    /// CPU flag effects for an instruction
    /// </summary>
    public class FlagEffect
    {
        public string Z { get; set; }
        public string N { get; set; }
        public string H { get; set; }
        public string C { get; set; }

        public FlagEffect() { }

        public FlagEffect(string z, string n, string h, string c)
        {
            Z = z;
            N = n;
            H = h;
            C = c;
        }
    }
}
