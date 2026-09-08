using System;
using System.IO;

namespace BsaLib.BA2Util
{
    public enum CompressionFormat
    {
        Zip,
        LZ4
    }

    public struct BA2Header
    {
        public BA2HeaderMagic Magic { get; private set; }
        public uint Version { get; private set; }
        public BA2HeaderType Type { get; private set; }
        public uint NumFiles { get; private set; }
        public ulong NameTableOffset { get; private set; }
        public uint Unknown1 { get; private set; }
        public uint Unknown2 { get; private set; }
        public uint Unknown3 { get; private set; }

        public CompressionFormat CompressionFormat { get; private set; }

        public BA2Header(BinaryReader br)
        {
            this.Magic = ParseMagic(br.ReadChars(4));
            this.Version = br.ReadUInt32();
            this.Type = ParseType(br.ReadChars(4));
            this.NumFiles = br.ReadUInt32();
            this.NameTableOffset = br.ReadUInt64();

            this.Unknown1 = 0;
            this.Unknown2 = 0;
            this.Unknown3 = 0;

            if (this.Version == 2)
            {
                this.Unknown1 = br.ReadUInt32();
                this.Unknown2 = br.ReadUInt32();
            }

            if (this.Version == 3)
            {
                this.Unknown1 = br.ReadUInt32();
                this.Unknown2 = br.ReadUInt32();
                this.Unknown3 = br.ReadUInt32();
            }

            // If version is 3, then Unknown1 means which compression format is used. TODO: Consider renaming Unknown1
            this.CompressionFormat = this.Version == 3 ? this.Unknown1 == 1 ? CompressionFormat.LZ4 : CompressionFormat.Zip : CompressionFormat.Zip;
        }

        private static BA2HeaderMagic ParseMagic(char[] chars)
        {
            string magic = new(chars);
            return Enum.TryParse(magic, true, out BA2HeaderMagic magicParsed)
                ? magicParsed
                : throw new Exception($"Unknown {nameof(BA2Header)}.{nameof(Magic)} value: ${magic}");
        }

        private static BA2HeaderType ParseType(char[] chars)
        {
            string type = new(chars);
            return Enum.TryParse(type, true, out BA2HeaderType typeParsed)
                ? typeParsed
                : throw new Exception($"Unknown {nameof(BA2Header)}.{nameof(Type)} value: ${type}");
        }

        public override readonly string ToString()
        {
            return $"Magic: {this.Magic} Version: {this.Version} Type: {this.Type} NumFiles: {this.NumFiles} NameTableOffset: {this.NameTableOffset}";
        }
    }
}
