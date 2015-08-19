using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bismuth
{
    public static class EncodingManager
    {
        static Dictionary<string, Func<byte[], byte[]>> EncodingMethods = new Dictionary<string, Func<byte[], byte[]>>(StringComparer.OrdinalIgnoreCase);
        
        public static void Setup()
        {
            AddEncodingFunction("deflate", deflateEncode);
            AddEncodingFunction("gzip", gzipEncode);
        }

        public static void AddEncodingFunction(string encodeType, Func<byte[], byte[]> func)
        {
            if(EncodingMethods.ContainsKey(encodeType))
                EncodingMethods[encodeType] = func;
            else
                EncodingMethods.Add(encodeType, func);
        }

        public static bool CanEncode(string encodeType)
        {
            return EncodingMethods.ContainsKey(encodeType);
        }

        public static bool Encode(string encodeType, byte[] input, out byte[] output)
        {
            if (EncodingMethods.ContainsKey(encodeType))
            {
                output = EncodingMethods[encodeType](input);
                return true;
            }

            LogManager.Error("EncodingManager", "Could not encode using '" + encodeType + "'. Encoding function not found.");
            output = input;
            return false;
        }

        private static byte[] deflateEncode(byte[] input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (DeflateStream dfs = new DeflateStream(ms, CompressionMode.Compress, true))
                {
                    dfs.Write(input, 0, input.Length);
                }
                return ms.ToArray();
            }
        }

        private static byte[] gzipEncode(byte[] input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gzs = new GZipStream(ms, CompressionMode.Compress, true))
                {
                    gzs.Write(input, 0, input.Length);
                }
                return ms.ToArray();
            }
        }
    }
}
