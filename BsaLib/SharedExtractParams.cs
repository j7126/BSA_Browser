using System;
using System.IO;

namespace BsaLib
{
    /// <summary>
    /// The shared extract params.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="reader">True if a new <see cref="BinaryReader"/> should be created.</param>
    public class SharedExtractParams(Archive archive, bool reader) : IDisposable
    {
        public BinaryReader Reader { get; private set; } = reader
            ? new BinaryReader(new FileStream(archive.FullPath, FileMode.Open, FileAccess.Read), archive.Encoding)
            : archive.BinaryReader;

        public void Dispose()
        {
            ((IDisposable)this.Reader).Dispose();
        }
    }
}
