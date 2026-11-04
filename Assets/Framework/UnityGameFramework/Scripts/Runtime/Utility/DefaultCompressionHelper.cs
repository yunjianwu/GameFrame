// Game Framework - Copyright (c) 2013-2021 Jiang Yin. MIT License.
// Starter modification: replace the legacy compression DLL with .NET GZipStream.
using System;
using System.IO;
using System.IO.Compression;
using GameFramework;
namespace UnityGameFramework.Runtime
{
    public class DefaultCompressionHelper : Utility.Compression.ICompressionHelper
    {
        public bool Compress(byte[] bytes, int offset, int length, Stream output)
        {
            if (!ValidRange(bytes, offset, length) || output == null || !output.CanWrite) return false;
            try
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                    gzip.Write(bytes, offset, length);
                return true;
            }
            catch (Exception) { return false; }
        }

        public bool Compress(Stream input, Stream output)
        {
            if (input == null || !input.CanRead || output == null || !output.CanWrite) return false;
            try
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
                    input.CopyTo(gzip);
                return true;
            }
            catch (Exception) { return false; }
        }

        public bool Decompress(byte[] bytes, int offset, int length, Stream output)
        {
            if (!ValidRange(bytes, offset, length)) return false;
            using (var input = new MemoryStream(bytes, offset, length, false))
                return Decompress(input, output);
        }

        public bool Decompress(Stream input, Stream output)
        {
            if (input == null || !input.CanRead || output == null || !output.CanWrite) return false;
            try
            {
                using (var gzip = new GZipStream(input, CompressionMode.Decompress, true))
                    gzip.CopyTo(output);
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool ValidRange(byte[] bytes, int offset, int length)
        {
            return bytes != null && offset >= 0 && length >= 0 && offset <= bytes.Length - length;
        }
    }
}
