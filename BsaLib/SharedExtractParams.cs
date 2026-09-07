using ICSharpCode.SharpZipLib.Zip.Compression;
using System;
using System.IO;

namespace BsaLib
{
    /// <param name="reader">True if a new <see cref="BinaryReader"/> should be created.</param>
    /// <param name="inflater">True if a new <see cref="Inflater"/> should be created.</param>
    public class SharedExtractParams(Archive archive, bool reader, bool inflater) : IDisposable
    {
        public Inflater Inflater { get; private set; } = inflater ? new Inflater() : archive.Inflater;
        public BinaryReader Reader { get; private set; } = reader
                ? new BinaryReader(new FileStream(archive.FullPath, FileMode.Open, FileAccess.Read), archive.Encoding)
                : archive.BinaryReader;

        public void Dispose()
        {
            ((IDisposable)Reader).Dispose();
        }
    }
}
