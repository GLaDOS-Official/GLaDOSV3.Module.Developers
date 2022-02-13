using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GLaDOSV3.Module.Developers.WindbgConverter
{
    internal class WindbgStructure
    {
        private static readonly Dictionary<string, string> KnownTypes = new Dictionary<string, string>()
        {
            { "void", "VOID" },
            { "Void", "VOID" },
            { "Char", "CHAR" },
            { "Int2B", "SHORT" },
            { "Int4B", "LONG" },
            { "Int8B", "LONGLONG" },
            { "UChar", "UCHAR" },
            { "Uint2B", "USHORT" },
            { "Uint4B", "ULONG" },
            { "Uint8B", "ULONGLONG" }
        };
        private string Name;
        private List<WindbgField> Fields = new List<WindbgField>();

        private static bool IsOnionOrBitfield(string line, string nextLine)
        {
            try { return ParseFieldOffset(line) == ParseFieldOffset(nextLine); }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }


        private static UIntPtr ParseFieldOffset(string line)
        {
            var temp = line[3..line.IndexOf(' ')];
            return UIntPtr.Parse(temp[2..], NumberStyles.HexNumber);
        }

        private static string ParseFieldName(string line)
        {
            int nameStart = line.IndexOf(' ') + 1;
            int nameEnd = line.IndexOf(':');
            return line[nameStart..nameEnd].Trim();
        }

        private static WindbgField ParseField(string line)
        {
            bool isArray = false;
            int arraylen = 0;
            int pointerCount = 0;
            string offsetString = line[3..line.IndexOf(' ')];
            int offset = Convert.ToInt32(offsetString, 16);
            int nameStart = line.IndexOf(' ') + 1;
            int nameEnd = line.IndexOf(' ', nameStart);
            string nameString = line[nameStart..nameEnd].Trim();
            int typeStart = line.IndexOf(':') + 1;
            string typeString = line[typeStart..].Trim();
            if (typeString.Contains("Pos"))
            {
                var    separator   = typeString.IndexOf(',');
                var    pos         = typeString[4..separator];
                var    len         = typeString[(separator + 2)..];
                var    bitfieldPos = Convert.ToInt32(pos, 10);
                var    bitfieldLen = Convert.ToInt32(new string(len.Where(c=>(char.IsDigit(c))).ToArray()), 10);
                string type        = "UCHAR";

                if (bitfieldLen > 32)
                    type = "ULONGLONG";
                else if (bitfieldLen > 16)
                    type = "ULONG";
                else if (bitfieldLen > 8)
                    type = "USHORT";
                return new WindbgBitfield(nameString, type, (UIntPtr)offset, (UIntPtr)bitfieldPos, (UIntPtr)bitfieldLen);
            }

            if (typeString[0] == '[')
            {
                isArray = true;
                int arrayEnd = typeString.IndexOf(']');
                string subscript = typeString[1..arrayEnd];
                arraylen = Convert.ToInt32(subscript, 10);

                typeString = typeString[(arrayEnd + 2)..];
            }

            while (typeString.IndexOf("Ptr64") != -1)
            {
                pointerCount++;
                typeString = typeString[6..];
            }

            typeString = typeString.Trim();
            if (KnownTypes.TryGetValue(typeString, out var sec)) typeString = sec;
            else if (typeString[0] == '_') typeString = typeString[1..];
            switch (pointerCount)
            {
                case > 1:
                    {
                        typeString = $"P{typeString}";
                        while (--pointerCount != 0)
                        {
                            typeString += "*";
                        }
                        break;
                    }
                case 1:
                    typeString = $"P{typeString}";
                    break;
            }

            if (isArray) return new WindbgArray(nameString, typeString, (UIntPtr)offset, (UIntPtr)arraylen);
            else return new WindbgSimple(nameString, typeString, (UIntPtr)offset);
        }
        public WindbgStructure(string text)
        {
            string[] lines = text.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                lines[index] = lines[index].Trim();
            }

            for (var index = 0; index < lines.Length; index++)
            {
                var line     = lines[index];
                var nextLine = index + 1 == lines.Length ? "" : lines[index + 1];
                if (line.Contains('!'))
                {
                    this.Name = line[(line.IndexOf('!') + 1)..];
                    if (this.Name[0] == '_') this.Name = this.Name[1..];
                }
                else if (IsOnionOrBitfield(line, nextLine))
                {
                    List<WindbgField> union_fields = new List<WindbgField>();
                    do
                    {
                        union_fields.Add(ParseField(line));
                        index++;
                        line     = lines[index];
                        nextLine = index + 1 == lines.Length ? "" : lines[index + 1];
                    } while (IsOnionOrBitfield(line, nextLine));
                    union_fields.Add(ParseField(line));
                    int bitCount = union_fields.Count(f => f.IsBitfield());
                    if (bitCount != union_fields.Count && bitCount != 0)
                    {
                        WindbgUnion field = new WindbgUnion(ParseFieldOffset(line));
                        WindbgBitfield_pack pack = new WindbgBitfield_pack(ParseFieldOffset(line));
                        foreach (var f in union_fields)
                        {
                            if (f.IsBitfield()) pack.Members.Add(f);
                            else field.Members.Add(f);
                        }
                        field.Members.Add(pack);
                        this.Fields.Add(field);
                    }
                    else if (bitCount == 0)
                    {
                        WindbgUnion field = new WindbgUnion(ParseFieldOffset(line));
                        foreach (var f in union_fields)
                        {
                            field.Members.Add(f);
                        }
                        this.Fields.Add(field);
                    }
                    else
                    {
                        var pack = new WindbgBitfield_pack(ParseFieldOffset(line));
                        foreach (var f in union_fields)
                        {
                            pack.Members.Add(f);
                        }
                        this.Fields.Add(pack);
                    }
                }
                else
                {
                    this.Fields.Add(ParseField(line));
                }
            }
        }

        public string AsString(int tabcount)
        {
            var outStr = $"{new string(' ', tabcount * 4)}typedef struct _{this.Name}\n{{\n";
            outStr =  this.Fields.Aggregate(outStr, (current, field) => current + (field.AsString(tabcount + 1) + "\n"));
            outStr += $"{new string(' ', tabcount * 4)}}} {this.Name}, *P{this.Name};\n";
            var lines = outStr.Split('\n').ToList();

            // Iterate through the lines and remove any containing the search string
            int remove = 0;
            for (int i = lines.Count() - 1; i >= 0; i--)
            {
                if (remove == 0 && (lines[i].Contains("struct {") || lines[i].Contains("union {"))) {remove = i;continue;}
                if (remove != i + 1 || !(lines[i].Contains("struct {") || lines[i].Contains("union {"))) continue;
                lines.RemoveAt(i);
                remove = 0;
            }

            //  Join the remaining lines back into a single string
            string textCleaned = string.Join('\n', lines);
            return textCleaned;
        }
    }
}
