using System.IO;

namespace BsaLib.BSAUtil
{
    public struct BSAHeaderMW(BinaryReader br, uint version)
    {
        public const uint Size = 12;

        public uint Version { get; private set; } = version;
        public uint HashOffset { get; private set; } = br.ReadUInt32();
        public uint FileCount { get; private set; } = br.ReadUInt32();
    }
}
