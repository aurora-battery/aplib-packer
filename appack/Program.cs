using System;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using aplibsharp;
namespace appack
{
    class Program
    {
        const byte FWFILE_BOXMON_V1 = 0x01;
        const byte FWFILE_TOPSTM_V1 = 0x02;
        const byte FWFILE_TOPESP_V1 = 0x03; // not used
        const byte FWFILE_COMPR_NONE = 0x00;
        const byte FWFILE_COMPR_APLIB = 0x01;
        const byte FWFILE_COMPR_UPKR = 0x02;

        static void Main(string[] args)
        {
            if (!ValidInput(args))
            {
                PrintUsage();
                System.Environment.Exit(-1);
            }

            string command = args[0];
            string loadname = args[1];
            string savename = args[2];

            var input = LoadFile(loadname);
            var output = new byte[0];
            if (command == "e")
            {
                output = aplib.encode(input);
                output = AddHeader(input, output);
            }
            else if (command == "d")
            {
                byte[] compressed = input;
                bool goodHeader = true;
                bool hasHeader = input[0] == 'L' && input[1] == 'i' && input[2] == 'F' && input[3] == 'W';
                if (hasHeader)
                {
                    compressed = input[24..];
                    string compression = input[4] switch
                    {
                        FWFILE_COMPR_NONE => "none",
                        FWFILE_COMPR_APLIB => "aplib",
                        FWFILE_COMPR_UPKR => "upkr"
                    };
                    string fwType = input[5] switch
                    {
                        FWFILE_BOXMON_V1 => "BoxMon",
                        FWFILE_TOPSTM_V1 => "TopBox"
                    };
                    Console.WriteLine($"Type: {fwType} Compression: {compression}");
                    if (input[4] != FWFILE_COMPR_APLIB) Environment.Exit(-1);
                    goodHeader &= BitConverter.ToUInt32(input[8..12]) == (input.Length - 24);
                    goodHeader &= BitConverter.ToUInt32(input[12..16]) == LiFwCrc.HashToUInt32(compressed);
                }
                output = aplib.decode(compressed);
                if (hasHeader)
                {
                    goodHeader &= BitConverter.ToUInt32(input[16..20]) == output.Length;
                    goodHeader &= BitConverter.ToUInt32(input[20..24]) == LiFwCrc.HashToUInt32(output);
                    if (!goodHeader) Console.WriteLine("INVALID HEADER!");
                }
            }

            if (output.Length > 0)
            {
                SaveFile(output, savename);
            }
        }

        private static byte[] AddHeader(byte[] input, byte[] compressed)
        {
            bool isWb0Fw = input[20..24].SequenceEqual(Encoding.ASCII.GetBytes("EULB"));
            byte fwType = isWb0Fw ? FWFILE_BOXMON_V1 : FWFILE_TOPSTM_V1;

            var buf = new MemoryStream();
            buf.Write(Encoding.ASCII.GetBytes("LiFW"));
            buf.Write(new byte[] { FWFILE_COMPR_APLIB, fwType, 0x00, 0x00 });
            buf.Write(BitConverter.GetBytes((UInt32)compressed.Length));
            buf.Write(BitConverter.GetBytes(LiFwCrc.HashToUInt32(compressed)));
            buf.Write(BitConverter.GetBytes((UInt32)input.Length));
            buf.Write(BitConverter.GetBytes(LiFwCrc.HashToUInt32(input)));
            buf.Write(compressed);
            return buf.ToArray();
        }

        private static byte[] LoadFile(string filename)
        {
            var data = new byte[0];

            var fs = System.IO.File.OpenRead(filename);

            try
            {
                if (fs.Length == 0)
                {
                    System.Console.WriteLine("Empty file");
                    System.Environment.Exit(-1);
                }
                data = new byte[fs.Length];
                fs.Read(data, 0, (int)fs.Length);
                return data;
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("Exception: {0}", e);
            }
            finally
            {
                fs.Close();
            }

            return data;
        }



        private static void SaveFile(byte[] input, string filename)
        {
            if (filename.Length == 0)
            {
                System.Console.WriteLine("No filename");
                System.Environment.Exit(-1);
            }

            if (System.IO.File.Exists(filename))
            {
                System.IO.File.Delete(filename);
            }

            var fs = System.IO.File.OpenWrite(filename);

            try
            {
                fs.Write(input, 0, input.Length);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("Exception: {0}", e);
            }
            finally
            {
                fs.Close();
            }
        }

        private static void PrintUsage()
        {
            System.Console.WriteLine($"appack 1.3 (LiFW header, max offset: {constant.threshold_offset})");
            System.Console.WriteLine();
            System.Console.WriteLine("Usage:");
            System.Console.WriteLine("appack d[ecode]|e[ncode] infile outfile");
        }
        private static bool ValidInput(string[] input)
        {
            return (
                input.Length == 3 &&
                input[0].Length == 1 &&
                (input[0] == "e" || input[0] == "d") &&
                System.IO.File.Exists(input[1]) &&
                !input[1].Equals(input[2])
            );
        }

    }

    static class LiFwCrc
    {
        static UInt32[] short_poly_lut = { 0x00000000, 0x3F079B52, 0x7E0F36A4, 0x4108ADF6, 0xFC1E6D48, 0xC319F61A, 0x82115BEC, 0xBD16C0BE, 0xA814498F, 0x9713D2DD, 0xD61B7F2B, 0xE91CE479, 0x540A24C7, 0x6B0DBF95, 0x2A051263, 0x15028931 };
        static UInt32[] long_poly_lut =  { 0x00000000, 0x934E3AE6, 0x89BC3E5F, 0x1AF204B9, 0xBC58372D, 0x2F160DCB, 0x35E40972, 0xA6AA3394, 0xD79025C9, 0x44DE1F2F, 0x5E2C1B96, 0xCD622170, 0x6BC812E4, 0xF8862802, 0xE2742CBB, 0x713A165D };


        static UInt32 CRC32_Calculate_Ex(ReadOnlySpan<byte> data, UInt32 prev_crc32, bool long_poly)
        {
            UInt32[] lut = long_poly ? long_poly_lut : short_poly_lut;
            UInt32 crc = ~prev_crc32;
            foreach (var current in data)
            {
                crc = lut[(crc ^ current) & 0x0F] ^ (crc >> 4);
                crc = lut[(crc ^ (current >> 4)) & 0x0F] ^ (crc >> 4);
            }

            return ~crc;
        }

        public static UInt32 HashToUInt32(ReadOnlySpan<byte> data) => CRC32_Calculate_Ex(data, 0, true);
    }
}
