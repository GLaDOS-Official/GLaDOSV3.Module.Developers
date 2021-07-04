using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GLaDOSV3.Helpers;
using Iced.Intel;

namespace GLaDOSV3.Module.Developers
{
    public class AssemblyModule : ModuleBase<SocketCommandContext>
    {
#if false
        [Command("asm", RunMode = RunMode.Async)]
        [Remarks("asm <mode> <hex codes>")]
        [Alias("assemble")]
        [Summary("Assemble opcodes into asm with ease!")]
        public async Task Asm(string mode, [Remainder] string opcodes)
        {
            var typing = Context.Channel.TriggerTypingAsync();
            int bitness = 0;
            const ulong codeRIP = 0x10000;
            switch (mode)
            {
                case "x64":
                case "64":
                    bitness = 64;
                    break;
                case "x86":
                case "86":
                case "32":
                case "x32":
                    bitness = 32;
                    break;
                default:
                    await Context.Channel.SendMessageAsync("Mode chosen is not x86 or x64!");
                    return;
            }
            try
            {
                var c = new Assembler(64);
                var d = Encoder.Create(64, new StreamCodeWriter(new MemoryStream()));
                Enum.Parse<Code>()
                d.Encode(Instruction.Create(Code))
            }
            finally { typing.Dispose(); }
        }
#endif
        [Command("disasm", RunMode = RunMode.Async)]
        [Remarks("disasm <mode> <hex codes>")]
        [Alias("disassemble")]
        [Summary("Disassemble opcodes into asm with ease!")]
        public async Task Disasm(string mode, [Remainder] string opcodes)
        {
            var typing = Context.Channel.TriggerTypingAsync();
            int bitness = 0;
            const ulong codeRIP = 0x10000;
            switch (mode)
            {
                case "x64":
                case "64":
                    bitness = 64;
                    break;
                case "x86":
                case "86":
                case "32":
                case "x32":
                    bitness = 32;
                    break;
                default:
                    await Context.Channel.SendMessageAsync("Mode chosen is not x86 or x64!");
                    return;
            }
            try
            {
                var opcodesConverted = opcodes.Split(' ');
                var bytes = new List<byte>();
                foreach (var _byte in opcodesConverted)
                {
                    var @byte = _byte;
                    if (@byte.EndsWith('h')) @byte = _byte[..(@byte.Length - 1)];
                    if (@byte.StartsWith("0x")) @byte = _byte[2..];
                    if (!int.TryParse(@byte, NumberStyles.HexNumber, null, out int result)) { await Context.Channel.SendMessageAsync("Invalid opcodes found!"); return; }
                    bytes.Add((byte)result);
                }
                byte[] codeBytes = bytes.ToArray();
                var codeReader = new ByteArrayCodeReader(codeBytes);
                var decoder = Decoder.Create(bitness, codeReader);
                decoder.IP = codeRIP;
                ulong endRip = decoder.IP + (uint)codeBytes.Length;

                var instructions = new List<Instruction>();
                while (decoder.IP < endRip)
                    instructions.Add(decoder.Decode());

                // Formatters: Masm*, Nasm*, Gas* (AT&T) and Intel* (XED).
                // There's also `FastFormatter` which is ~2x faster. Use it if formatting speed is more
                // important than being able to re-assemble formatted instructions.
                IntelFormatter formatter = new IntelFormatter();
                formatter.Options.BinaryPrefix = "0b";
                formatter.Options.AddLeadingZeroToHexNumbers = true;
                formatter.Options.AlwaysShowSegmentRegister = true;
                formatter.Options.BranchLeadingZeroes = false;
                formatter.Options.DecimalDigitGroupSize = 0;
                formatter.Options.HexDigitGroupSize = 0;
                formatter.Options.RipRelativeAddresses = true;
                formatter.Options.MasmAddDsPrefix32 = true;
                formatter.Options.ShowSymbolAddress = true;
                formatter.Options.SpaceAfterOperandSeparator = true;
                formatter.Options.FirstOperandCharIndex = 10;
                var output = new StringOutput();
                var returnOutput = "```x86asm\n";
                foreach (var instr in instructions)
                {
                    // Don't use instr.ToString(), it allocates more, uses masm syntax and default options
                    formatter.Format(instr, output);
                    returnOutput += instr.IP.ToString("X16");
                    returnOutput += " ";
                    int instrLen = instr.Length;
                    int byteBaseIndex = (int)(instr.IP - codeRIP);
                    for (int i = 0; i < instrLen; i++)
                        returnOutput += codeBytes[byteBaseIndex + i].ToString("X2");
                    int missingBytes = 10 - instrLen;
                    for (int i = 0; i < missingBytes; i++)
                        returnOutput += "  ";
                    returnOutput += " ";
                    returnOutput += output.ToStringAndReset() + "\n";
                }

                returnOutput += "```";
                await Context.Channel.SendMessageAsync(returnOutput);
            }
            finally { typing.Dispose(); }
        }
    }
}
