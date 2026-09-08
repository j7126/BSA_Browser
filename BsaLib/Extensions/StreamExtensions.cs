using System;
using System.IO;

namespace BsaLib.Extensions
{
    public static class StreamExtensions
    {
        public static byte[] ReadBytes(this Stream stream, int count)
        {
            var data = new byte[count];
            var read = stream.Read(data, 0, count);
            var trimmed = new byte[read];

            Array.Copy(data, trimmed, read);

            return trimmed;
        }
    }
}
