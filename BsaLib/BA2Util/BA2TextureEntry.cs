using BsaLib.Extensions;
using BsaLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BsaLib.BA2Util
{
    public class UnsupportedDDSException : Exception
    {
        public UnsupportedDDSException() : base() { }
        public UnsupportedDDSException(string message) : base(message) { }
        public UnsupportedDDSException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class BA2TextureEntry : ArchiveEntry
    {
        private const uint TILE_MODE_DEFAULT = 0x08;
        private const uint XBOX_BASE_ALIGNMENT = 256;
        private const uint XBOX_XDK_VERSION = 13202;

        /// <summary>
        /// Gets or sets whether to generate DDS header.
        /// </summary>
        public bool GenerateTextureHeader { get; set; } = true;
        public List<BA2TextureChunk> Chunks { get; private set; } = [];
        public long dataSizePosition = -1;

        public readonly byte unk1;
        public readonly byte numChunks;
        public readonly ushort chunkHdrLen;
        public readonly ushort height;
        public readonly ushort width;
        public readonly byte numMips;
        public readonly byte format;
        public readonly byte isCubemap;
        public readonly byte tileMode;

        // After testing it seems like ALL textures are compressed.
        public override bool Compressed => this.Chunks[0].packSz != 0;
        public override uint Size
        {
            get
            {
                var size = this.GetHeaderSize();
                var compressed = this.Chunks[0].packSz != 0;

                foreach (var chunk in this.Chunks)
                {
                    size += compressed ? chunk.packSz : chunk.fullSz;
                }

                return size;
            }
        }
        // Start of with the size of the DDS Magic + DDS header + if applicable DDS DXT10 header
        public override uint RealSize => this.GetHeaderSize() + (uint)this.Chunks.Sum(x => Math.Max(x.fullSz, x.packSz));
        public override uint DisplaySize => this.GetHeaderSize() + (uint)this.Chunks.Sum(x => x.fullSz);
        public override ulong Offset => this.Chunks[0].offset;

        public override ulong GetSizeInArchive(SharedExtractParams extractParams) => (ulong)this.Chunks.Sum(x => this.Compressed ? x.packSz : x.fullSz);

        public BA2TextureEntry(Archive ba2) : base(ba2)
        {
            this.NameHash = ba2.BinaryReader.ReadUInt32();
            this.Extension = new string(ba2.BinaryReader.ReadChars(4));
            this.DirHash = ba2.BinaryReader.ReadUInt32();

            this.FullPath = this.DirHash > 0 ? $"{this.DirHash:X}_" : string.Empty;
            this.FullPath += $"{this.NameHash:X}.{this.Extension.TrimEnd('\0')}";
            this.FullPathOriginal = this.FullPath;

            this.unk1 = ba2.BinaryReader.ReadByte();
            this.numChunks = ba2.BinaryReader.ReadByte();
            this.chunkHdrLen = ba2.BinaryReader.ReadUInt16();
            this.height = ba2.BinaryReader.ReadUInt16();
            this.width = ba2.BinaryReader.ReadUInt16();
            this.numMips = ba2.BinaryReader.ReadByte();
            this.format = ba2.BinaryReader.ReadByte();
            this.isCubemap = ba2.BinaryReader.ReadByte();
            this.tileMode = ba2.BinaryReader.ReadByte();

            for (var i = 0; i < this.numChunks; i++)
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
                $"Chunk header len:\t {this.chunkHdrLen}\n" +
                $"Mipmaps:\t {this.numMips}\n" +
                $"Cubemap:\t {Convert.ToBoolean(this.isCubemap)}\n" +
                $"Tile mode:\t {this.tileMode}\n\n" +
                $"{nameof(this.unk1)}:\t\t {this.unk1}";
        }

        public bool IsFormatSupported()
        {
            return Enum.IsDefined(typeof(DXGI_FORMAT), (int)this.format);
        }

        private uint GetHeaderSize()
        {
            uint size = 0;

            // DDS.DDS_MAGIC
            size += 4;
            size += DDS_HEADER.GetSize();

            // If DXT10 add that size too
            switch ((DXGI_FORMAT)this.format)
            {
                case DXGI_FORMAT.BC1_UNORM_SRGB:
                case DXGI_FORMAT.BC3_UNORM_SRGB:
                case DXGI_FORMAT.BC4_UNORM:
                case DXGI_FORMAT.BC5_SNORM:
                case DXGI_FORMAT.BC7_UNORM:
                case DXGI_FORMAT.BC7_UNORM_SRGB:
                    size += DDS_HEADER_DXT10.GetSize();
                    break;
            }
            return size;
        }

        private void WriteHeader(BinaryWriter bw)
        {
            var ddsHeader = new DDS_HEADER
            {
                dwSize = DDS_HEADER.GetSize(),
                dwHeaderFlags = DDS.DDS_HEADER_FLAGS_TEXTURE | DDS.DDS_HEADER_FLAGS_LINEARSIZE | DDS.DDS_HEADER_FLAGS_MIPMAP,
                dwHeight = this.height,
                dwWidth = this.width,
                dwDepth = 1,
                dwMipMapCount = this.numMips
            };
            ddsHeader.PixelFormat.dwSize = DDS_PIXELFORMAT.GetSize();
            ddsHeader.dwCubemapFlags = this.isCubemap == 1 ? (uint)(DDSCAPS2.CUBEMAP
                | DDSCAPS2.CUBEMAP_NEGATIVEX | DDSCAPS2.CUBEMAP_POSITIVEX
                | DDSCAPS2.CUBEMAP_NEGATIVEY | DDSCAPS2.CUBEMAP_POSITIVEY
                | DDSCAPS2.CUBEMAP_NEGATIVEZ | DDSCAPS2.CUBEMAP_POSITIVEZ
                | DDSCAPS2.CUBEMAP_ALLFACES) : 0u;

            switch ((DXGI_FORMAT)this.format)
            {
                case DXGI_FORMAT.BC1_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '1');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height / 2); // 4bpp
                    break;
                case DXGI_FORMAT.BC2_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '3');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height); // 8bpp
                    break;
                case DXGI_FORMAT.BC5_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('B', 'C', '5', 'U');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height); // 8bpp
                    break;
                case DXGI_FORMAT.BC1_UNORM_SRGB:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height / 2); // 4bpp
                    break;
                case DXGI_FORMAT.BC3_UNORM_SRGB:
                case DXGI_FORMAT.BC7_UNORM_SRGB:
                case DXGI_FORMAT.R32G32B32A32_FLOAT:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height); // 8bpp
                    break;
                case DXGI_FORMAT.R8G8B8A8_UNORM:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                    ddsHeader.PixelFormat.dwRGBBitCount = 32;
                    ddsHeader.PixelFormat.dwRBitMask = 0x000000FF;
                    ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                    ddsHeader.PixelFormat.dwBBitMask = 0x00FF0000;
                    ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * 4);
                    break;
                case DXGI_FORMAT.B5G6R5_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGB;
                    ddsHeader.PixelFormat.dwRGBBitCount = 16;
                    ddsHeader.PixelFormat.dwRBitMask = 0x0000f800;
                    ddsHeader.PixelFormat.dwGBitMask = 0x000007e0;
                    ddsHeader.PixelFormat.dwBBitMask = 0x0000001f;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height * 2); // 16bpp
                    break;
                case DXGI_FORMAT.B8G8R8X8_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                    ddsHeader.PixelFormat.dwRGBBitCount = 32;
                    ddsHeader.PixelFormat.dwRBitMask = 0x00FF0000;
                    ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                    ddsHeader.PixelFormat.dwBBitMask = 0x000000FF;
                    ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height * 4); // 32bpp
                    break;

                case DXGI_FORMAT.R16G16B16A16_FLOAT:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.dwDepth = 1;
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = 0x71;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * 8);
                    break;
                case DXGI_FORMAT.R16G16B16A16_UNORM:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.dwDepth = 1;
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = 0x24;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * 8);
                    break;
                case DXGI_FORMAT.R8G8B8A8_UNORM_SRGB:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * 4);
                    break;
                case DXGI_FORMAT.R8_UNORM:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.PixelFormat.dwFlags = 0x20000;
                    ddsHeader.PixelFormat.dwRGBBitCount = 8;
                    ddsHeader.PixelFormat.dwRBitMask = 0xFF;
                    ddsHeader.dwPitchOrLinearSize = this.width;
                    break;
                case DXGI_FORMAT.R8G8B8A8_SNORM:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.PixelFormat.dwFlags = 0x00080000;
                    ddsHeader.PixelFormat.dwRGBBitCount = 32;
                    ddsHeader.PixelFormat.dwRBitMask = 0x000000FF;
                    ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                    ddsHeader.PixelFormat.dwBBitMask = 0x00FF0000;
                    ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * 4);
                    break;
                case DXGI_FORMAT.BC3_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', 'T', '5');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height); // 8bpp
                    break;
                case DXGI_FORMAT.BC4_UNORM:
                    ddsHeader.dwHeaderFlags = 0xA1007;
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('B', 'C', '4', 'U');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width / 4 * (this.height / 4) * 8);
                    break;
                case DXGI_FORMAT.BC5_SNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('B', 'C', '5', 'S');
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * this.height); // 8bpp
                    break;
                case DXGI_FORMAT.B8G8R8A8_UNORM:
                    ddsHeader.dwHeaderFlags = 0x2100F;
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_RGBA;
                    ddsHeader.PixelFormat.dwRGBBitCount = 32;
                    ddsHeader.PixelFormat.dwRBitMask = 0x00FF0000;
                    ddsHeader.PixelFormat.dwGBitMask = 0x0000FF00;
                    ddsHeader.PixelFormat.dwBBitMask = 0x000000FF;
                    ddsHeader.PixelFormat.dwABitMask = 0xFF000000;
                    ddsHeader.dwPitchOrLinearSize = (uint)(this.width * 4);
                    break;
                case DXGI_FORMAT.BC6H_UF16:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(Math.Ceiling(this.width / 4m) * Math.Ceiling(this.height / 4m) * 16);
                    break;
                case DXGI_FORMAT.BC7_UNORM:
                    ddsHeader.PixelFormat.dwFlags = DDS.DDS_FOURCC;
                    ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('D', 'X', '1', '0');
                    ddsHeader.dwPitchOrLinearSize = (uint)(Math.Ceiling(this.width / 4m) * Math.Ceiling(this.height / 4m) * 16);
                    break;
                default:
                    throw new UnsupportedDDSException("Unsupported DDS header format. File: " + this.FullPath);
            }

            ddsHeader.dwSurfaceFlags = DDS.DDS_SURFACE_FLAGS_TEXTURE;

            if (this.numMips > 1)
            {
                ddsHeader.dwSurfaceFlags |= DDS.DDS_SURFACE_FLAGS_COMPLEX | DDS.DDS_SURFACE_FLAGS_MIPMAP;
            }
            else if (this.isCubemap == 1)
            {
                ddsHeader.dwSurfaceFlags |= DDS.DDS_SURFACE_FLAGS_COMPLEX;
            }

            // Version 7 has 0xFE00 added to surface flags when files have multiple mipmaps and are cubemaps.
            // Unknown what 0xFE00 means currently.
            if ((this.Archive as BA2).Header.Version == 7 && this.numMips > 1 && this.isCubemap == 1)
            {
                ddsHeader.dwSurfaceFlags |= 0xFE00;
                ddsHeader.dwCubemapFlags = 0x0; // This is also reset for some reason
            }

            // If tileMode is NOT TILE_MODE_DEFAULT assume Xbox format
            if (this.tileMode != TILE_MODE_DEFAULT)
            {
                switch ((DXGI_FORMAT)this.format)
                {
                    case DXGI_FORMAT.BC1_UNORM:
                    case DXGI_FORMAT.BC1_UNORM_SRGB:
                    case DXGI_FORMAT.BC2_UNORM:
                    case DXGI_FORMAT.BC3_UNORM:
                    case DXGI_FORMAT.BC3_UNORM_SRGB:
                    case DXGI_FORMAT.BC4_UNORM:
                    case DXGI_FORMAT.BC5_SNORM:
                    case DXGI_FORMAT.BC5_UNORM:
                    case DXGI_FORMAT.BC7_UNORM:
                    case DXGI_FORMAT.BC7_UNORM_SRGB:
                        ddsHeader.PixelFormat.dwFourCC = DDS.MAKEFOURCC('X', 'B', 'O', 'X');
                        break;
                }
            }

            bw.Write((uint)DDS.DDS_MAGIC);
            ddsHeader.Write(bw);

            switch ((DXGI_FORMAT)this.format)
            {
                case DXGI_FORMAT.BC1_UNORM_SRGB:
                case DXGI_FORMAT.BC3_UNORM_SRGB:
                case DXGI_FORMAT.BC6H_UF16:
                case DXGI_FORMAT.BC7_UNORM:
                case DXGI_FORMAT.BC7_UNORM_SRGB:
                case DXGI_FORMAT.R32G32B32A32_FLOAT:
                case DXGI_FORMAT.R8G8B8A8_UNORM_SRGB:
                    new DDS_HEADER_DXT10()
                    {
                        dxgiFormat = this.format,
                        resourceDimension = (uint)DXT10_RESOURCE_DIMENSION.DIMENSION_TEXTURE2D,
                        miscFlag = this.isCubemap == 1 ? DDS.DDS_RESOURCE_MISC_TEXTURECUBE : 0u,
                        arraySize = 1,
                        miscFlags2 = DDS.DDS_ALPHA_MODE_UNKNOWN
                    }.Write(bw);
                    break;
                default:
                    if (this.tileMode != TILE_MODE_DEFAULT)
                    {
                        new DDS_HEADER_DXT10()
                        {
                            dxgiFormat = this.format,
                            resourceDimension = (uint)DXT10_RESOURCE_DIMENSION.DIMENSION_TEXTURE2D,
                            miscFlag = this.isCubemap == 1 ? DDS.DDS_RESOURCE_MISC_TEXTURECUBE : 0u,
                            arraySize = 1,
                            miscFlags2 = DDS.DDS_ALPHA_MODE_UNKNOWN
                        }.Write(bw);
                    }
                    break;
            }

            // If tileMode is NOT TILE_MODE_DEFAULT assume Xbox format
            if (this.tileMode != TILE_MODE_DEFAULT)
            {
                bw.Write((uint)this.tileMode);
                bw.Write(XBOX_BASE_ALIGNMENT);
                this.dataSizePosition = bw.BaseStream.Position;
                bw.Write((uint)0);
                bw.Write(XBOX_XDK_VERSION);
            }
        }

        protected override void WriteDataToStream(Stream stream, SharedExtractParams extractParams, bool decompress)
        {
            var bw = new BinaryWriter(stream);
            var reader = extractParams.Reader;

            // Reset at start since value might still be in used for a bit after
            this.BytesWritten = 0;

            if (decompress && this.GenerateTextureHeader)
            {
                this.WriteHeader(bw);
            }

            for (var i = 0; i < this.numChunks; i++)
            {
                var isCompressed = this.Chunks[i].packSz != 0;
                var prev = this.BytesWritten;

                _ = reader.BaseStream.Seek((long)this.Chunks[i].offset, SeekOrigin.Begin);

                if (!decompress || !isCompressed)
                {
                    StreamUtils.WriteSectionToStream(reader.BaseStream,
                        this.Chunks[i].fullSz,
                        stream,
                        bytesWritten => this.BytesWritten = prev + bytesWritten);
                }
                else
                {
                    if ((this.Archive as BA2).Header.CompressionFormat == CompressionFormat.LZ4)
                    {
                        CompressionUtils.DecompressLZ4(reader.BaseStream,
                            this.Chunks[i].packSz,
                            this.Chunks[i].fullSz,
                            stream,
                            BytesWritten => this.BytesWritten = prev + BytesWritten);
                    }
                    else
                    {
                        CompressionUtils.Decompress(reader.BaseStream,
                            this.Chunks[i].packSz,
                            stream,
                            bytesWritten => this.BytesWritten = prev + bytesWritten,
                            extractParams);
                    }
                }
            }

            if (this.dataSizePosition > -1)
            {
                bw.WriteAt(this.dataSizePosition, (uint)bw.BaseStream.Length - 164);
            }
        }
    }
}
