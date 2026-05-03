using System.Collections.Generic;

namespace UELib.Branch.UE3.RL
{
    /// <summary>
    /// Set of native function indexes whose entry in the binary's GNatives table at
    /// <c>0x7FF6CF2AA580</c> is the default "Unknown code token" error handler — i.e. there is
    /// no real native at that index in this RL build. These cover the gaps in the assigned
    /// native namespace (3,982 entries across 256..4415).
    ///
    /// <c>ChainedNativeDispatcherTokenRL</c> consults this set: when the computed index is in
    /// the unknown set, it skips the token's variadic-arg deserialize step so we don't emit
    /// <c>__NFUN_NNN__(...)</c> ghost call sites for indexes the binary itself would error on.
    ///
    /// Stored as compressed (inclusive-start, inclusive-end) ranges and expanded into a
    /// <c>HashSet&lt;ushort&gt;</c> at static initialization. 33 ranges → 3982 indexes.
    /// </summary>
    public static class RocketLeagueUnknownNatives
    {
        private static readonly (ushort, ushort)[] s_ranges =
        {
            (257, 257),
            (259, 260),
            (263, 265),
            (268, 269),
            (273, 274),
            (278, 278),
            (285, 286),
            (292, 295),
            (301, 303),
            (308, 308),
            (310, 310),
            (314, 315),
            (331, 331),
            (334, 383),
            (386, 499),
            (504, 507),
            (510, 511),
            (513, 513),
            (515, 516),
            (519, 519),
            (522, 523),
            (529, 531),
            (534, 535),
            (538, 545),
            (549, 706),
            (708, 1499),
            (1502, 3968),
            (3972, 4149),
            (4187, 4191),
            (4202, 4208),
            (4217, 4351),
            (4368, 4383),
            (4406, 4415),
        };

        public static readonly HashSet<ushort> Set = BuildSet();

        private static HashSet<ushort> BuildSet()
        {
            var set = new HashSet<ushort>();
            foreach (var (start, end) in s_ranges)
            {
                for (int i = start; i <= end; i++)
                {
                    set.Add((ushort)i);
                }
            }
            return set;
        }
    }
}
