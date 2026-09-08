using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BsaLib.Utils;

namespace BsaLib.BA2Util
{
    public class BA2GNFEntry : ArchiveEntry
    {
        private const int GNF_HEADER_MAGIC = 0x20464E47;
        private const int GNF_HEADER_CONTENT_SIZE = 248;
        private const int IntFirst14BitMask = (1 << 14) - 1;

        public List<BA2TextureChunk> Chunks { get; private set; } = [];

        /// <summary>
        /// Unknown.
        /// </summary>
        private ushort ChunkHdrLen { get; set; }
        /// <summary>
        /// Unknown. 00 00 00 00.
        /// </summary>
        private uint Unk2 { get; set; }
        private uint Align { get; set; }

        public readonly uint numChunks;
        public readonly uint format;
        public readonly uint numFormat;
        public readonly uint height;
        public readonly uint width;

        /// <summary>
        /// Part of the header that will be in GNF file.
        /// </summary>
        public byte[] GNFHeader { get; set; }

        public override uint DisplaySize
        {
            get
            {
                var size = this.RealSize;
                foreach (var chunk in this.Chunks)
                {
                    size += chunk.fullSz;
                }

                return size;
            }
        }

        public override ulong GetSizeInArchive(SharedExtractParams extractParams) => Math.Max(this.Size, this.RealSize);

        public BA2GNFEntry(Archive ba2) : base(ba2)
        {
            this.NameHash = ba2.BinaryReader.ReadUInt32();
            this.Extension = new string(ba2.BinaryReader.ReadChars(4));
            this.DirHash = ba2.BinaryReader.ReadUInt32();

            this.FullPath = this.DirHash > 0 ? $"{this.DirHash:X}_" : string.Empty;
            this.FullPath += $"{this.NameHash:X}.{this.Extension.TrimEnd('\0')}";
            this.FullPathOriginal = this.FullPath;

            _ = ba2.BinaryReader.ReadByte(); // Unknown
            this.numChunks = ba2.BinaryReader.ReadByte();
            this.ChunkHdrLen = ba2.BinaryReader.ReadUInt16();

            this.GNFHeader = ba2.BinaryReader.ReadBytes(32);

            var formatInfo = BitConverter.ToUInt32([.. this.GNFHeader.Skip(4).Take(4)], 0);
            this.format = (formatInfo >> 20) & ((1 << 6) - 1); // Skip first 20 bits then take 6 next bits
            this.numFormat = (formatInfo >> 26) & ((1 << 4) - 1); // Skip first 26 bits then take 4 next bits

            var size = BitConverter.ToUInt32([.. this.GNFHeader.Skip(8).Take(4)], 0);
            this.width = (size & IntFirst14BitMask) + 1; // Get first 14 bits
            this.height = ((size >> 14) & IntFirst14BitMask) + 1; // Shifts past first 14 bits then get first 14 bits again

            this.Offset = ba2.BinaryReader.ReadUInt64();
            this.Size = ba2.BinaryReader.ReadUInt32();
            this.RealSize = ba2.BinaryReader.ReadUInt32();
            this.Unk2 = ba2.BinaryReader.ReadUInt32();
            this.Align = ba2.BinaryReader.ReadUInt32();

            for (var i = 0; i < (this.numChunks - 1); i++)
            {
                this.Chunks.Add(new BA2TextureChunk(ba2.BinaryReader));
            }
        }

        public override string GetToolTipText()
        {
            var dxgi = Enum.GetName(typeof(DXGI_FORMAT_FULL), this.format);

            return $"Name hash:\t {this.NameHash:X}\n" +
                $"Directory hash:\t {this.DirHash:X}\n" +
                $"DXGI format:\t {dxgi} ({this.format})\n" +
                $"Resolution:\t {this.width}x{this.height}\n" +
                $"Chunks:\t\t {this.numChunks}\n" +
                $"Chunk header len:\t {this.ChunkHdrLen}\n" +
                $"Num format:\t {this.numFormat}\n" +
                $"Offset:\t\t {this.Offset}\n" +
                $"Size:\t\t {this.Size}\n" +
                $"Real Size:\t {this.RealSize}\n" +
                $"Align:\t\t {this.Align:X}\n\n" +
                $"{nameof(this.Unk2)}:\t\t {this.Unk2}";
        }

        protected override void WriteDataToStream(Stream stream, SharedExtractParams extractParams, bool decompress)
        {
            var reader = extractParams.Reader;
            _ = reader.BaseStream.Seek((long)this.Offset, SeekOrigin.Begin);
            // Reset at start since value might still be in used for a bit after
            this.BytesWritten = 0;

            if (!decompress)
            {
                StreamUtils.WriteSectionToStream(reader.BaseStream,
                    Math.Max(this.Size, this.RealSize), // Lazy hack, only one should be set when not compressed
                    stream,
                    bytesWritten => this.BytesWritten = bytesWritten);
            }
            else
            {
                this.WriteHeader(stream);

                try
                {
                    CompressionUtils.Decompress(reader.BaseStream,
                        this.Size,
                        stream,
                        bytesWritten => this.BytesWritten = bytesWritten);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Couldn't decompress zlib texture data. Size: {this.Size}, RealSize: {this.RealSize}", ex);
                }
            }

            this.WriteChunks(stream, extractParams, decompress);
        }

        private void WriteChunks(Stream stream, SharedExtractParams extractParams, bool decompress)
        {
            var reader = extractParams.Reader;

            for (var i = 0; i < (this.numChunks - 1); i++)
            {
                _ = reader.BaseStream.Seek((long)this.Chunks[i].offset, SeekOrigin.Begin);


                if (!decompress)
                {
                    var prev = this.BytesWritten;
                    StreamUtils.WriteSectionToStream(reader.BaseStream,
                        Math.Max(this.Chunks[i].packSz, this.Chunks[i].fullSz),  // Lazy hack, only one should be set when not compressed
                        stream,
                        bytesWritten => this.BytesWritten = prev + bytesWritten);
                }
                else
                {
                    var prev = this.BytesWritten;
                    CompressionUtils.Decompress(reader.BaseStream,
                        this.Chunks[i].packSz,
                        stream,
                        bytesWritten => this.BytesWritten = prev + bytesWritten);
                }
            }
        }

        private void WriteHeader(Stream stream)
        {
            var writer = new BinaryWriter(stream);

            writer.Write(GNF_HEADER_MAGIC); // 'GNF ' magic
            writer.Write(GNF_HEADER_CONTENT_SIZE); // Content-size. Seems to be either 4 or 8 bytes

            writer.Write((byte)0x2); // Version
            writer.Write((byte)0x1); // Texture Count
            writer.Write((byte)0x8); // Alignment
            writer.Write((byte)0x0); // Unused

            writer.Write(BitConverter.GetBytes(this.RealSize + 256).Reverse().ToArray()); // File size + header size
            writer.Write(this.GNFHeader);

            for (var i = 0; i < 208; i++)
            {
                writer.Write((byte)0x0); // Padding
            }
        }
    }
}
