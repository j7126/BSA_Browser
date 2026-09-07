using System.IO;

namespace BsaLib.BSAUtil
{
    public struct BSAFileInfo(BinaryReader reader)
    {
        public ulong Hash { get; private set; } = reader.ReadUInt64();
        public uint SizeFlags { get; private set; } = reader.ReadUInt32();
        public uint Offset { get; private set; } = reader.ReadUInt32();
    }
}
