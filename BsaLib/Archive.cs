using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BsaLib.Enums;

namespace BsaLib
{
    /// <summary>
    /// The archive.
    /// </summary>
    public abstract class Archive
    {
        public bool MatchLastWriteTime { get; set; }
        public bool RetrieveRealSize { get; protected set; }

        public long FileSize { get; protected set; }

        public string FullPath { get; protected set; }
        public string FileName => Path.GetFileName(this.FullPath);

        public DateTime LastWriteTime { get; protected set; }

        public virtual int Chunks { get; set; }
        public virtual int FileCount { get; set; }
        public virtual bool HasNameTable { get; set; }
        public virtual string VersionString { get; set; } = "None";

        public virtual ArchiveTypes Type { get; protected set; }

        public Encoding Encoding { get; protected set; }
        public List<ArchiveEntry> Files { get; protected set; } = [];
        public BinaryReader BinaryReader { get; protected set; }

        static Archive()
        {
        }

#pragma warning disable SYSLIB0001 // Type or member is obsolete
        protected Archive(string filePath) : this(filePath, Encoding.UTF7) { }
#pragma warning restore SYSLIB0001 // Type or member is obsolete
        protected Archive(string filePath, Encoding encoding) : this(filePath, encoding, false) { }
        protected Archive(string filePath, Encoding encoding, bool retrieveRealSize)
        {
            this.FullPath = filePath;
            this.Encoding = encoding;
            this.LastWriteTime = File.GetLastWriteTime(this.FullPath);
            this.RetrieveRealSize = retrieveRealSize;
            this.BinaryReader = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read), encoding);
            this.FileSize = this.BinaryReader.BaseStream.Length;

            this.Open(filePath);
        }

        public void Close()
        {
            this.BinaryReader?.Close();
        }

        protected abstract void Open(string filePath);
    }
}
