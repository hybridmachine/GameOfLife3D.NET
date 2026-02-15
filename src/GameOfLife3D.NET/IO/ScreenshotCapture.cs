using System.IO.Compression;

namespace GameOfLife3D.NET.IO;

public static class ScreenshotCapture
{
    public static void SavePng(string path, byte[] rgbaPixels, int width, int height)
    {
        using var fs = File.Create(path);
        WritePng(fs, rgbaPixels, width, height);
    }

    public static string SaveToDesktop(byte[] rgbaPixels, int width, int height)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(desktop))
            desktop = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"GameOfLife3D_{timestamp}.png";
        string path = Path.Combine(desktop, filename);

        SavePng(path, rgbaPixels, width, height);
        return path;
    }

    private static void WritePng(Stream output, byte[] rgbaPixels, int width, int height)
    {
        // PNG signature
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        // IHDR chunk
        var ihdr = new byte[13];
        WriteInt32BE(ihdr, 0, width);
        WriteInt32BE(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // color type: RGBA
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(output, "IHDR", ihdr);

        // IDAT chunk - compress raw image data
        using var compressedMs = new MemoryStream();

        // Use zlib format (deflate with zlib header)
        // Write zlib header: CMF=0x78 (deflate, window 32K), FLG=0x01 (no dict, check bits)
        compressedMs.WriteByte(0x78);
        compressedMs.WriteByte(0x01);

        uint adler = 1;
        using (var deflate = new DeflateStream(compressedMs, CompressionLevel.Fastest, leaveOpen: true))
        {
            int rowBytes = width * 4;
            var row = new byte[1 + rowBytes]; // filter byte + row data
            for (int y = 0; y < height; y++)
            {
                row[0] = 0; // no filter
                Array.Copy(rgbaPixels, y * rowBytes, row, 1, rowBytes);
                deflate.Write(row, 0, row.Length);

                // Update Adler-32
                adler = UpdateAdler32(adler, row);
            }
        }

        // Write Adler-32 checksum
        compressedMs.WriteByte((byte)(adler >> 24));
        compressedMs.WriteByte((byte)(adler >> 16));
        compressedMs.WriteByte((byte)(adler >> 8));
        compressedMs.WriteByte((byte)adler);

        WriteChunk(output, "IDAT", compressedMs.ToArray());

        // IEND chunk
        WriteChunk(output, "IEND", []);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var lengthBytes = new byte[4];
        WriteInt32BE(lengthBytes, 0, data.Length);
        output.Write(lengthBytes);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        // CRC32 over type + data
        uint crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteInt32BE(crcBytes, 0, (int)crc);
        output.Write(crcBytes);
    }

    private static void WriteInt32BE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] typeBytes, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in typeBytes)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdateAdler32(uint adler, byte[] data)
    {
        uint a = adler & 0xFFFF;
        uint b = (adler >> 16) & 0xFFFF;
        foreach (byte d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static readonly uint[] Crc32Table = GenerateCrc32Table();

    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
