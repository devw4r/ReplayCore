using System;
using System.IO;
using System.Text;
using System.IO.Compression;

namespace SniffBrowser.Core
{
    public static class StringCompressor
    {
        public static byte[] ZipStr(string str)
        {
            if (string.IsNullOrEmpty(str)) 
                return null;

            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream gzip = new DeflateStream(output, CompressionMode.Compress))
                {
                    using (StreamWriter writer = new StreamWriter(gzip, Encoding.UTF8))
                    {
                        writer.Write(str);
                    }
                }

                return output.ToArray();
            }
        }

        public static string UnZipStr(byte[] input)
        {
            if (input == null)
                return string.Empty;

            using (MemoryStream inputStream = new MemoryStream(input))
                using (DeflateStream gzip = new DeflateStream(inputStream, CompressionMode.Decompress))
                    using (StreamReader reader = new StreamReader(gzip, Encoding.UTF8))
                        return reader.ReadToEnd();
        }

        public static string CompressString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Do not compress below 200 characters, compressed b64 would probably be bigger.
            if (text.Length < 700)
                return text;

            byte[] buffer = Encoding.UTF8.GetBytes(text);
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            return Convert.ToBase64String(gZipBuffer);
        }

        public static string DecompressString(string compressedText)
        {
            if(string.IsNullOrEmpty(compressedText)) 
                return string.Empty;

            // Not really compressed.
            if (!compressedText[compressedText.Length - 1].Equals("="))
                return compressedText;

            byte[] gZipBuffer = Convert.FromBase64String(compressedText);
            using (var memoryStream = new MemoryStream())
            {
                int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                var buffer = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }
    }
}
