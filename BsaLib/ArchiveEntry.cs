using System.IO;

namespace BsaLib
{
    /// <summary>
    /// An entry in an <see cref="Archive"/>.
    /// </summary>
    /// <param name="archive">The archive containing this entry.</param>
    public abstract class ArchiveEntry(Archive archive)
    {
        #region Properties

        /// <summary>
        /// Gets the number of bytes written to the output stream when extracting this entry.
        /// </summary>
        public ulong BytesWritten { get; protected set; }

        /// <summary>
        /// Gets the index of the entry in the <see cref="BsaLib.Archive"/>.
        /// </summary>
        public int Index { get; internal set; } = -1;

        /// <summary>
        /// Gets whether hash was translated back into a filename.
        /// </summary>
        public bool HadHashTranslated { get; internal set; }

        /// <summary>
        /// Gets the name hash of the entry.
        /// </summary>
        public uint NameHash { get; protected set; }

        /// <summary>
        /// Gets the directory hash of the entry.
        /// </summary>
        public uint DirHash { get; protected set; }

        /// <summary>
        /// Gets the file extension.
        /// </summary>
        public string Extension { get; protected set; }

        /// <summary>
        /// Gets the file name only including extension.
        /// </summary>
        public string FileName => Path.GetFileName(this.FullPath);

        /// <summary>
        /// Gets the folder.
        /// </summary>
        public string Folder => Path.GetDirectoryName(this.FullPath);

        /// <summary>
        /// Gets or sets the full file path.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Gets the full path in lower case.
        /// </summary>
        public string LowerPath => this.FullPath.ToLower();

        /// <summary>
        /// Gets the original unchanged full path.
        /// </summary>
        public string FullPathOriginal { get; internal set; }

        /// <summary>
        /// Gets if the file is compressed.
        /// </summary>
        public virtual bool Compressed { get; protected set; }

        /// <summary>
        /// Gets the offset of the entry in the archive.
        /// </summary>
        public virtual ulong Offset { get; protected set; }

        /// <summary>
        /// Gets the uncompressed file size.
        /// </summary>
        public virtual uint RealSize { get; protected internal set; }

        /// <summary>
        /// Gets the file size.
        /// </summary>
        public virtual uint Size { get; protected set; }

        /// <summary>
        /// Gets a file size more suited for display in GUIs.
        /// </summary>
        public abstract uint DisplaySize { get; }

        /// <summary>
        /// Gets the <see cref="BsaLib.Archive"/> containing this <see cref="ArchiveEntry"/>.
        /// </summary>
        public Archive Archive { get; private set; } = archive;

        #endregion

        public void Extract(bool preserveFolder)
        {
            this.Extract(string.Empty, preserveFolder);
        }
        public void Extract(string destination, bool preserveFolder)
        {
            this.Extract(destination, preserveFolder, this.FileName);
        }
        public void Extract(string destination, bool preserveFolder, string newName)
        {
            this.Extract(destination, preserveFolder, newName, new SharedExtractParams(this.Archive, false));
        }
        public void Extract(string destination, bool preserveFolder, string newName, SharedExtractParams extractParams)
        {
            var path = preserveFolder ? this.Folder : string.Empty;

            path = Path.Combine(path, newName);

            if (!string.IsNullOrEmpty(destination))
            {
                path = Path.Combine(destination, path);
            }

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            using (var fs = File.Create(path))
            {
                this.WriteDataToStream(fs, extractParams);
            }

            if (this.Archive.MatchLastWriteTime)
            {
                File.SetLastWriteTime(path, this.Archive.LastWriteTime);
            }
        }

        /// <summary>
        /// Extracts and uncompresses data and then returns the stream.
        /// </summary>
        public virtual MemoryStream GetDataStream() => this.GetDataStream(new SharedExtractParams(this.Archive, false));
        /// <summary>
        /// Extracts and uncompresses data and then returns the stream.
        /// </summary>
        public virtual MemoryStream GetDataStream(SharedExtractParams extractParams)
        {
            var ms = new MemoryStream();

            this.WriteDataToStream(ms, extractParams);

            _ = ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        /// <summary>
        /// Returns a <see cref="MemoryStream"/> of the raw data.
        /// </summary>
        public MemoryStream GetRawDataStream() => this.GetRawDataStream(new SharedExtractParams(this.Archive, false));
        /// <summary>
        /// Returns a <see cref="MemoryStream"/> of the raw data.
        /// </summary>
        public MemoryStream GetRawDataStream(SharedExtractParams extractParams)
        {
            var ms = new MemoryStream();

            this.WriteDataToStream(ms, extractParams, false);

            _ = ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        /// <summary>
        /// Returns exact size off entry in <see cref="Archive"/>, which can used to read into <see cref="Stream"/> with <see cref="Offset"/> for example.
        /// </summary>
        public abstract ulong GetSizeInArchive(SharedExtractParams extractParams);

        public virtual string GetToolTipText()
        {
            return "Undefined";
        }

        protected abstract void WriteDataToStream(Stream stream, SharedExtractParams extractParams, bool decompress = true);
    }
}
