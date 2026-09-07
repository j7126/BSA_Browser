using System.IO;

namespace BsaLib.BSAUtil
{
    public struct BSAHeader(BinaryReader reader)
    {
        public uint Version { get; private set; } = reader.ReadUInt32();
        public uint FolderRecordOffset { get; private set; } = reader.ReadUInt32();
        public uint ArchiveFlags { get; private set; } = reader.ReadUInt32();
        public uint FolderCount { get; private set; } = reader.ReadUInt32();
        public uint FileCount { get; private set; } = reader.ReadUInt32();
        public uint FolderNameLength { get; private set; } = reader.ReadUInt32();
        public uint FileNameLength { get; private set; } = reader.ReadUInt32();
        public uint FileFlags { get; private set; } = reader.ReadUInt32();
    }
}
