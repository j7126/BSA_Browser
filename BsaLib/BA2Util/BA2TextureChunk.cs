using System.IO;

namespace BsaLib.BA2Util
{
    public struct BA2TextureChunk(BinaryReader br)
    {
        public ulong offset = br.ReadUInt64();
        public uint packSz = br.ReadUInt32();
        public uint fullSz = br.ReadUInt32();
        public ushort startMip = br.ReadUInt16();
        public ushort endMip = br.ReadUInt16();
        public uint align = br.ReadUInt32();
    }
}
