using Discord.Commands;
using Iced.Intel;
using Keystone;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace GLaDOSV3.Module.Developers
{
    public class AssemblyModule : ModuleBase<ShardedCommandContext>
    {
        private readonly ILogger<AssemblyModule> _logger;
        public AssemblyModule(ILogger<AssemblyModule> logger) => this._logger = logger;

        private const ulong CodeRip = 0x400000;
#if true
        [Command("asm", RunMode = RunMode.Async)]
        [Remarks("asm <mode> <hex codes>")]
        [Alias("assemble")]
        [Summary("Assemble opcodes into asm with ease!")]
        public async Task Asm(string mode, [Remainder] string asm)
        {
            await Context.Channel.TriggerTypingAsync();
            Mode bitness;

            switch (mode)
            {
                case "x16":
                case "16":
                    bitness = Mode.X16;
                    break;
                case "x64":
                case "64":
                    bitness = Mode.X64;
                    break;
                case "x86":
                case "86":
                case "32":
                case "x32":
                    bitness = Mode.X32;
                    break;
                default:
                    await Context.Channel.SendMessageAsync("Mode chosen is not x86, x64 or x16!");
                    return;
            }
            try
            {
                using Engine keystone = new Engine(Architecture.X86, bitness) { ThrowOnError = true };

                EncodedData enc = keystone.Assemble(asm, CodeRip);
                await this.ReplyAsync($"Here's your opcodes chef!\n```\n{BitConverter.ToString(enc.Buffer).Replace("-", " ")}\n```");

            }
            catch (Exception ex) { this._logger.LogCritical(ex, ex.Message); }
        }
#endif
        [Command("disasm", RunMode = RunMode.Async)]
        [Remarks("disasm <mode> <hex codes>")]
        [Alias("disassemble")]
        [Summary("Disassemble opcodes into asm with ease!")]
        public async Task Disasm(string mode, [Remainder] string opcodes)
        {
            await Context.Channel.TriggerTypingAsync();
            int bitness;
            switch (mode)
            {
                case "x16":
                case "16":
                    bitness = 16;
                    break;
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
                    await Context.Channel.SendMessageAsync("Mode chosen is not x86, x64 or x16!");
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
                    if (!int.TryParse(@byte, NumberStyles.HexNumber, null, out var result)) { await Context.Channel.SendMessageAsync("Invalid opcodes found!"); return; }
                    bytes.Add((byte)result);
                }
                var codeBytes = bytes.ToArray();
                var codeReader = new ByteArrayCodeReader(codeBytes);
                var decoder = Decoder.Create(bitness, codeReader);
                decoder.IP = CodeRip;
                var endRip = decoder.IP + (uint)codeBytes.Length;

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
                var returnOutput = "";
                foreach (var instr in instructions)
                {
                    // Don't use instr.ToString(), it allocates more, uses masm syntax and default options
                    formatter.Format(instr, output);
                    returnOutput += instr.IP.ToString("X16");
                    returnOutput += " ";
                    var instrLen = instr.Length;
                    var byteBaseIndex = (int)(instr.IP - CodeRip);
                    for (var i = 0; i < instrLen; i++)
                        returnOutput += codeBytes[byteBaseIndex + i].ToString("X2");
                    var missingBytes = 10 - instrLen;
                    for (var i = 0; i < missingBytes; i++)
                        returnOutput += "  ";
                    returnOutput += " ";
                    returnOutput += output.ToStringAndReset() + "\n";
                }
                await Context.Channel.SendMessageAsync($"You're welcome\n```x86asm\n{returnOutput}\n```");
            }
            catch { }
        }
    }
}
