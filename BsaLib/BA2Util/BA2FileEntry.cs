using System.IO;
using BsaLib.Utils;

namespace BsaLib.BA2Util
{
    public class BA2FileEntry : ArchiveEntry
    {
        public uint Flags { get; set; }
        public uint Align { get; set; }

        public override bool Compressed => this.Size != 0;
        public override uint DisplaySize => this.RealSize;

        public override ulong GetSizeInArchive(SharedExtractParams extractParams) => this.Compressed ? this.Size : this.RealSize;

        public BA2FileEntry(Archive ba2) : base(ba2)
        {
            this.NameHash = ba2.BinaryReader.ReadUInt32();
            this.Extension = new string(ba2.BinaryReader.ReadChars(4));
            this.DirHash = ba2.BinaryReader.ReadUInt32();

            this.FullPath = this.DirHash > 0 ? $"{this.DirHash:X}_" : string.Empty;
            this.FullPath += $"{this.NameHash:X}.{this.Extension.TrimEnd('\0')}";
            this.FullPathOriginal = this.FullPath;

            this.Flags = ba2.BinaryReader.ReadUInt32();
            this.Offset = ba2.BinaryReader.ReadUInt64();
            this.Size = ba2.BinaryReader.ReadUInt32();
            this.RealSize = ba2.BinaryReader.ReadUInt32();
            this.Align = ba2.BinaryReader.ReadUInt32();
        }

        public override string GetToolTipText()
        {
            return $"Name hash:\t {this.NameHash:X}\n" +
                $"Directory hash:\t {this.DirHash:X}\n" +
                $"Flags:\t\t {this.Flags:X}\n" +
                $"Offset:\t\t {this.Offset}\n" +
                $"Size:\t\t {this.Size}\n" +
                $"Real Size:\t {this.RealSize}\n" +
                $"Align:\t\t {this.Align:X}";
        }

        protected override void WriteDataToStream(Stream stream, SharedExtractParams extractParams, bool decompress)
        {
            var reader = extractParams.Reader;
            var len = this.Compressed ? this.Size : this.RealSize;
            _ = reader.BaseStream.Seek((long)this.Offset, SeekOrigin.Begin);
            // Reset at start since value might still be in used for a bit after
            this.BytesWritten = 0;

            if (!decompress || !this.Compressed)
            {
                StreamUtils.WriteSectionToStream(reader.BaseStream,
                    len,
                    stream,
                    bytesWritten => this.BytesWritten = bytesWritten);
            }
            else
            {
                CompressionUtils.Decompress(reader.BaseStream,
                    len,
                    stream,
                    bytesWritten => this.BytesWritten = bytesWritten,
                    extractParams);
            }
        }
    }
}
