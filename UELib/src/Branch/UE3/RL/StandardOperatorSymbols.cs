using System.Collections.Generic;

namespace UELib.Branch.UE3.RL
{
    /// <summary>
    /// Native function index → operator symbol + type, sourced from baseline UE3 Object.uc
    /// (Development/Src/Core/Classes/Object.uc).
    ///
    /// Cooked .upks strip the script-source FriendlyName, so UFunction.FriendlyName falls back
    /// to the function's Name (e.g. "Multiply_FloatFloat") instead of the symbol ("*"). This
    /// table re-supplies the symbols by native index — same indexes are stable across all
    /// UE3-based games for the standard operator set declared in Object.uc.
    /// </summary>
    public static class StandardOperatorSymbols
    {
        public readonly struct OperatorEntry
        {
            public readonly string Symbol;
            public readonly FunctionType Type;
            public readonly byte Precedence;

            public OperatorEntry(string symbol, FunctionType type, byte precedence)
            {
                Symbol = symbol;
                Type = type;
                Precedence = precedence;
            }
        }

        public static readonly Dictionary<ushort, OperatorEntry> Map = new()
        {
            // ====== Bool ======
            { 129, new("!",  FunctionType.PreOperator,   0) },  // !  (preoperator)
            { 130, new("&&", FunctionType.Operator,     30) },
            // 131 is NotEqual_BoolBool in RL (per RocketLeagueNativeNames), NOT XorXor as in
            // baseline UE3. RL repurposed this slot — see the RL-specific section below.
            { 132, new("||", FunctionType.Operator,     32) },
            { 242, new("==", FunctionType.Operator,     24) },
            { 243, new("!=", FunctionType.Operator,     26) },

            // ====== Byte (out byte op= ...) ======
            { 133, new("*=", FunctionType.Operator,     34) },
            { 134, new("/=", FunctionType.Operator,     34) },
            { 135, new("+=", FunctionType.Operator,     34) },
            { 136, new("-=", FunctionType.Operator,     34) },
            { 137, new("++", FunctionType.PreOperator,   0) },
            { 138, new("--", FunctionType.PreOperator,   0) },
            { 139, new("++", FunctionType.PostOperator,  0) },
            { 140, new("--", FunctionType.PostOperator,  0) },
            { 198, new("*=", FunctionType.Operator,     34) },  // byte *= float

            // ====== Int ======
            { 141, new("~",  FunctionType.PreOperator,   0) },
            { 143, new("-",  FunctionType.PreOperator,   0) },
            { 144, new("*",  FunctionType.Operator,     16) },
            { 145, new("/",  FunctionType.Operator,     16) },
            { 253, new("%",  FunctionType.Operator,     18) },
            { 146, new("+",  FunctionType.Operator,     20) },
            { 147, new("-",  FunctionType.Operator,     20) },
            { 148, new("<<", FunctionType.Operator,     22) },
            { 149, new(">>", FunctionType.Operator,     22) },
            { 196, new(">>>",FunctionType.Operator,     22) },
            { 150, new("<",  FunctionType.Operator,     24) },
            { 151, new(">",  FunctionType.Operator,     24) },
            { 152, new("<=", FunctionType.Operator,     24) },
            { 153, new(">=", FunctionType.Operator,     24) },
            { 154, new("==", FunctionType.Operator,     24) },
            { 155, new("!=", FunctionType.Operator,     26) },
            { 156, new("&",  FunctionType.Operator,     28) },
            { 157, new("^",  FunctionType.Operator,     28) },
            { 158, new("|",  FunctionType.Operator,     28) },
            { 159, new("*=", FunctionType.Operator,     34) },
            { 160, new("/=", FunctionType.Operator,     34) },
            { 161, new("+=", FunctionType.Operator,     34) },
            { 162, new("-=", FunctionType.Operator,     34) },
            { 163, new("++", FunctionType.PreOperator,   0) },
            { 164, new("--", FunctionType.PreOperator,   0) },
            { 165, new("++", FunctionType.PostOperator,  0) },
            { 166, new("--", FunctionType.PostOperator,  0) },

            // ====== Float ======
            { 169, new("-",  FunctionType.PreOperator,   0) },
            { 170, new("**", FunctionType.Operator,     12) },
            { 171, new("*",  FunctionType.Operator,     16) },
            { 172, new("/",  FunctionType.Operator,     16) },
            { 173, new("%",  FunctionType.Operator,     18) },
            { 174, new("+",  FunctionType.Operator,     20) },
            { 175, new("-",  FunctionType.Operator,     20) },
            { 176, new("<",  FunctionType.Operator,     24) },
            { 177, new(">",  FunctionType.Operator,     24) },
            { 178, new("<=", FunctionType.Operator,     24) },
            { 179, new(">=", FunctionType.Operator,     24) },
            { 180, new("==", FunctionType.Operator,     24) },
            { 210, new("~=", FunctionType.Operator,     24) },
            { 181, new("!=", FunctionType.Operator,     26) },
            { 182, new("*=", FunctionType.Operator,     34) },
            { 183, new("/=", FunctionType.Operator,     34) },
            { 184, new("+=", FunctionType.Operator,     34) },
            { 185, new("-=", FunctionType.Operator,     34) },

            // ====== String ======
            { 112, new("$",  FunctionType.Operator,     40) },
            { 168, new("@",  FunctionType.Operator,     40) },
            { 115, new("<",  FunctionType.Operator,     24) },
            { 116, new(">",  FunctionType.Operator,     24) },
            { 120, new("<=", FunctionType.Operator,     24) },
            { 121, new(">=", FunctionType.Operator,     24) },
            { 122, new("==", FunctionType.Operator,     24) },
            { 123, new("!=", FunctionType.Operator,     26) },
            { 124, new("~=", FunctionType.Operator,     24) },
            { 322, new("$=", FunctionType.Operator,     44) },
            { 323, new("@=", FunctionType.Operator,     44) },
            { 324, new("-=", FunctionType.Operator,     45) },

            // ====== Object ======
            { 114, new("==", FunctionType.Operator,     24) },
            { 119, new("!=", FunctionType.Operator,     26) },

            // ====== Name ======
            { 254, new("==", FunctionType.Operator,     24) },
            { 255, new("!=", FunctionType.Operator,     26) },

            // ====== Vector ======
            { 211, new("-",  FunctionType.PreOperator,   0) },
            { 212, new("*",  FunctionType.Operator,     16) },
            { 213, new("*",  FunctionType.Operator,     16) },
            { 296, new("*",  FunctionType.Operator,     16) },
            { 214, new("/",  FunctionType.Operator,     16) },
            { 215, new("+",  FunctionType.Operator,     20) },
            { 216, new("-",  FunctionType.Operator,     20) },
            { 275, new("<<", FunctionType.Operator,     22) },
            { 276, new(">>", FunctionType.Operator,     22) },
            { 217, new("==", FunctionType.Operator,     24) },
            { 218, new("!=", FunctionType.Operator,     26) },
            { 219, new("Dot",  FunctionType.Operator,   16) },
            { 220, new("Cross",FunctionType.Operator,   16) },
            { 221, new("*=", FunctionType.Operator,     34) },
            { 297, new("*=", FunctionType.Operator,     34) },
            { 222, new("/=", FunctionType.Operator,     34) },
            { 223, new("+=", FunctionType.Operator,     34) },
            { 224, new("-=", FunctionType.Operator,     34) },

            // ====== Rotator ======
            { 142, new("==", FunctionType.Operator,     24) },
            { 203, new("!=", FunctionType.Operator,     26) },
            { 287, new("*",  FunctionType.Operator,     16) },
            { 288, new("*",  FunctionType.Operator,     16) },
            { 289, new("/",  FunctionType.Operator,     16) },
            { 290, new("*=", FunctionType.Operator,     34) },
            { 291, new("/=", FunctionType.Operator,     34) },
            { 316, new("+",  FunctionType.Operator,     20) },
            { 317, new("-",  FunctionType.Operator,     20) },
            { 318, new("+=", FunctionType.Operator,     34) },
            { 319, new("-=", FunctionType.Operator,     34) },

            // ====== Quaternion ======
            { 270, new("+",  FunctionType.Operator,     16) },
            { 271, new("-",  FunctionType.Operator,     16) },

            // ====== RL-specific operator indexes (from RocketLeagueNativeNames) ======
            // These appear at additional native indexes besides the baseline UE3 set —
            // RL extends operator overloads via separate registrations.
            { 131, new("!=", FunctionType.Operator,     26) },  // NotEqual_BoolBool (RL alt of 243)
            { 191, new("~=", FunctionType.Operator,     24) },  // ComplementEqual_StrStr
            { 192, new("!=", FunctionType.Operator,     26) },  // NotEqual_StrStr
            { 204, new("==", FunctionType.Operator,     24) },  // EqualEqual_StrStr
            { 206, new("!=", FunctionType.Operator,     26) },  // NotEqual_NameName (RL alt of 255)
            { 207, new("==", FunctionType.Operator,     24) },  // EqualEqual_NameName (RL alt of 254)
            { 240, new(">",  FunctionType.Operator,     24) },  // Greater_StrStr
            { 241, new("<",  FunctionType.Operator,     24) },  // Less_StrStr
            { 248, new("$",  FunctionType.Operator,     40) },  // Concat_StrStr (RL alt of 112)
        };

        public static bool TryResolveByName(string name, out OperatorEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (TryResolveBinaryByPrefix(name, out entry))
            {
                return true;
            }

            if (name.Contains("_Pre"))
            {
                if (name.StartsWith("Not_", System.StringComparison.Ordinal))
                {
                    entry = new OperatorEntry("!", FunctionType.PreOperator, 0);
                    return true;
                }
                if (name.StartsWith("Subtract_", System.StringComparison.Ordinal))
                {
                    entry = new OperatorEntry("-", FunctionType.PreOperator, 0);
                    return true;
                }
                if (name.StartsWith("Complement_", System.StringComparison.Ordinal))
                {
                    entry = new OperatorEntry("~", FunctionType.PreOperator, 0);
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveBinaryByPrefix(string name, out OperatorEntry entry)
        {
            entry = default;
            (string Prefix, string Symbol, byte Precedence)[] operators =
            {
                ("ComplementEqual_", "~=", 24),
                ("GreaterEqual_", ">=", 24),
                ("LessEqual_", "<=", 24),
                ("EqualEqual_", "==", 24),
                ("NotEqual_", "!=", 26),
                ("Greater_", ">", 24),
                ("Less_", "<", 24),
                ("MultiplyEqual_", "*=", 34),
                ("DivideEqual_", "/=", 34),
                ("AddEqual_", "+=", 34),
                ("SubtractEqual_", "-=", 34),
                ("ConcatEqual_", "$=", 44),
                ("AtEqual_", "@=", 44),
                ("Multiply_", "*", 16),
                ("Divide_", "/", 16),
                ("Percent_", "%", 18),
                ("Add_", "+", 20),
                ("Subtract_", "-", 20),
                ("Concat_", "$", 40),
                ("At_", "@", 40),
            };

            foreach (var op in operators)
            {
                if (name.StartsWith(op.Prefix, System.StringComparison.Ordinal))
                {
                    entry = new OperatorEntry(op.Symbol, FunctionType.Operator, op.Precedence);
                    return true;
                }
            }

            return false;
        }
    }
}
