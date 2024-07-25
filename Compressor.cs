using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DecompilationDiffer;

internal static class Compressor
{
    private static readonly char[] s_padding = { '=' };

    public static string Compress(string baseCode, string version1, string version2)
    {
        var separator = (char)7;
        return Compress(baseCode + separator + version1 + separator + version2);

        static string Compress(string input)
        {
            using var ms = new MemoryStream();
            using (var compressor = new DeflateStream(ms, CompressionLevel.Optimal))
            {
                var inputBytes = Encoding.Unicode.GetBytes(input);
                compressor.Write(inputBytes);
            }
            return ToBase64(ms.ToArray());
        }
    }

    private static string ToBase64(byte[] input)
        => Convert.ToBase64String(input).TrimEnd(s_padding).Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64(string input)
        => Convert.FromBase64String(input.Replace('_', '/').Replace('-', '+') +
            (input.Length % 4) switch
            {
                0 => "",
                2 => "==",
                3 => "=",
                _ => throw new ArgumentException()
            });

    public static string Uncompress(string slug)
    {
        try
        {
            var bytes = FromBase64(slug);

            using var ms = new MemoryStream(bytes);
            using (var compressor = new DeflateStream(ms, CompressionMode.Decompress))
            using (var sr = new StreamReader(compressor, Encoding.Unicode))
            {
                return sr.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }
}
