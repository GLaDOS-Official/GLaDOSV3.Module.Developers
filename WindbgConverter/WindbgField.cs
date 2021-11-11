using System;
using System.Collections.Generic;
using System.Linq;

namespace GLaDOSV3.Module.Developers.WindbgConverter
{
    class WindbgField
    {
        public string Name;
        public string Type;
        public UIntPtr Offset;
        public virtual bool IsArray() => false;
        public virtual bool IsOnion() => false;
        public virtual bool IsBitfield() => false;
        public virtual bool IsBitfield_pack() => false;
        public virtual string AsString(int tabcount = 0) => "";


        public WindbgField(string name, string type, UIntPtr offset)
        {
            this.Name = name;
            this.Type = type;
            this.Offset = offset;
        }
    }

    class WindbgSimple : WindbgField
    {
        public WindbgSimple(string name, string type, UIntPtr offset) : base(name, type, offset) { }
        public override string AsString(int tabcount = 0) => $"{new string(' ', tabcount * 4)}{this.Type} {this.Name};";
    }

    class WindbgArray : WindbgField
    {
        public WindbgArray(string name, string type, UIntPtr offset, UIntPtr len) : base(name, type, offset) => this.Length = len;
        public override bool IsArray() => true;
        public UIntPtr Length;

        public override string AsString(int tabcount = 0) =>
            $"{new string(' ', tabcount * 4)}{this.Type} {this.Name}[{this.Length}];";
    }
    class WindbgUnion : WindbgField
    {
        public WindbgUnion(UIntPtr offset) : base("UNNAMED_UNION", "UNION", offset) { }
        public override bool              IsOnion() => true;
        public          List<WindbgField> Members = new List<WindbgField>();

        public override string AsString(int tabcount = 0)
        {
            var s = new string(' ', tabcount * 4) + "union {\n";
            s += this.Members.Aggregate(s, (current, field) => current + (field.AsString(tabcount + 1) + "\n"));
            s += new string(' ', tabcount * 4) + "};";
            return s;
        }

    }

    class WindbgBitfield : WindbgField
    {
        public WindbgBitfield(string name, string type, UIntPtr offset, UIntPtr position, UIntPtr length) : base(name, type, offset)
        {
            this.Length = length;
            this.Position = position;
        }

        private UIntPtr Position;
        private UIntPtr Length;
        public override bool IsBitfield() => true;
        public override string AsString(int tabcount = 0) => $"{new string(' ', tabcount * 4)}{this.Type} {this.Name} : {this.Length};";
    }

    class WindbgBitfield_pack : WindbgField
    {
        public override bool              IsBitfield_pack() => true;
        public          List<WindbgField> Members = new List<WindbgField>();
        public WindbgBitfield_pack(UIntPtr offset) : base("UNNAMED_PACK", "PACK", offset) { }
        public override string AsString(int tabcount = 0)
        {
            var s = $"{new string(' ', tabcount * 4)}struct {{\n";
            s += this.Members.Aggregate(s, (current, field) => current + (field.AsString(tabcount + 1) + "\n"));
            s += $"{new string(' ', tabcount * 4)}}};";
            return s;
        }
    }


}
