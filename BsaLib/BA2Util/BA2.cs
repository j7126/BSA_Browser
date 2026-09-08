using BsaLib.Enums;
using BsaLib.Extensions;
using BsaLib.Utils;
using System;
using System.IO;
using System.Text;

namespace BsaLib.BA2Util
{
    public class BA2 : Archive
    {
        public BA2Header Header { get; set; }

        public override int FileCount => (int)this.Header.NumFiles;
        public override bool HasNameTable => this.Header.NameTableOffset > 0;
        public override string VersionString => this.Header.Version.ToString();

        public BA2(string filePath) : base(filePath) { }
        public BA2(string filePath, Encoding encoding) : base(filePath, encoding) { }
        public BA2(string filePath, Encoding encoding, bool retrieveRealSize) : base(filePath, encoding, retrieveRealSize) { }

        protected override void Open(string filePath)
        {
            this.Header = new BA2Header(this.BinaryReader);
            // Set more detailed archive type, used for comparing
            this.Type = this.ConvertType(this.Header.Type);

            this.Files.Capacity = this.FileCount;

            for (var i = 0; i < this.FileCount; i++)
            {
                switch (this.Header.Type)
                {
                    case BA2HeaderType.GNRL:
                        this.Files.Add(new BA2FileEntry(this));
                        break;
                    case BA2HeaderType.DX10:
                        this.Files.Add(new BA2TextureEntry(this));
                        break;
                    case BA2HeaderType.GNMF:
                        this.Files.Add(new BA2GNFEntry(this));
                        break;
                    default:
                        throw new Exception($"Unknown {nameof(BA2HeaderType)} value: " + this.Header.Type);
                }
                this.Files[i].Index = i;
            }

            if (this.HasNameTable)
            {
                // Seek to name table
                _ = this.BinaryReader.BaseStream.Seek((long)this.Header.NameTableOffset, SeekOrigin.Begin);

                // Assign full names to each file
                for (var i = 0; i < this.FileCount; i++)
                {
                    try
                    {
                        var fullPath = this.BinaryReader.ReadString(this.BinaryReader.ReadInt16());

                        this.Files[i].FullPath = PathUtils.NormalizePath(fullPath);
                        this.Files[i].FullPathOriginal = this.Files[i].FullPath;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error reading name table. Try different encoding.", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Converts <see cref="BA2HeaderType"/> to <see cref="ArchiveTypes"/> for more detailed information.
        /// </summary>
        private ArchiveTypes ConvertType(BA2HeaderType type)
        {
            return Enum.TryParse("BA2_" + type, out ArchiveTypes typeConverted)
                ? typeConverted
                : throw new Exception($"Unable to convert value '{type}' to {nameof(ArchiveTypes)}");
        }
    }
}
