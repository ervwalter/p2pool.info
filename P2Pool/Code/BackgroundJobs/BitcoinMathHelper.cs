using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace P2Pool
{
    public class BitcoinMathHelper
    {
        private static readonly BigInteger TargetAtDifficultyOne = BigInteger.Parse("00000000ffff0000000000000000000000000000000000000000000000000000", System.Globalization.NumberStyles.HexNumber);
        private static object _lock = new object();

        public static double Difficulty(long bits)
        {
            int shiftAmount = 8 * ((int)(bits >> 24) - 3);
            BigInteger target = new BigInteger(bits & 0xFFFFFF);
            target = target << shiftAmount;
            double difficulty = Math.Exp(BigInteger.Log(TargetAtDifficultyOne) - BigInteger.Log(target));
            return difficulty;
        }

        public static double Difficulty(string targetAsBigEndianHex)
        {
            string fixedTargetString = ReverseTarget(targetAsBigEndianHex);
            BigInteger target = BigInteger.Parse(fixedTargetString, System.Globalization.NumberStyles.HexNumber);
            return Math.Exp(BigInteger.Log(TargetAtDifficultyOne) - BigInteger.Log(target));  // eqivilent to TargetAtDifficultyOne / target
        }

        public static string ReverseTarget(string target)
        {
            char[] chars = target.ToCharArray();
            if (chars.Length % 2 != 0)
            {
                throw new DataMisalignedException("target must be an even number of characters");
            }
            Array.Reverse(chars);
            for (var i = 0; i < chars.Length; i += 2)
            {
                char tmp = chars[i];
                chars[i] = chars[i + 1];
                chars[i + 1] = tmp;
            }
            return new string(chars);
        }

        public static bool HashIsBelowTarget(string hash, string target)
        {
            BigInteger targetInt = BigInteger.Parse("0" + target, System.Globalization.NumberStyles.HexNumber);
            BigInteger hashInt = BigInteger.Parse("0" + hash, System.Globalization.NumberStyles.HexNumber);
            return (hashInt <= targetInt);
        }

        public static byte[] Decode(string str)
        {
            if (str.Length % 8 != 0)
                throw new DataMisalignedException("Hex string must be multiple of 8 characters long.");

            var buf = new byte[str.Length / 2];
            for (int i = 0, j = 0; j < str.Length; i++, j += 2)
                buf[i] = Convert.ToByte(str.Substring(j, 2), 16);
            return buf;
        }


        public static string Encode(byte[] buf)
        {
            var b = new StringBuilder(buf.Length * 2);
            for (int i = 0; i < buf.Length; i++)
                b.AppendFormat("{0:x2}", buf[i]);
            return b.ToString();
        }

        public static string Hash(string data)
        {
            byte[] header = Decode(data.Substring(0, 160));

            for (var i = 0; i < 80; i += 4)
            {
                byte zero = header[i];
                byte one = header[i + 1];
                header[i] = header[i + 3];
                header[i + 1] = header[i + 2];
                header[i + 2] = one;
                header[i + 3] = zero;
            }

            SHA256 hasher = SHA256.Create();
            byte[] hash1 = hasher.ComputeHash(header);
            byte[] hash2 = hasher.ComputeHash(hash1);
            Array.Reverse(hash2);
            return Encode(hash2);
        }

        public static bool CheckHash(string hash, string target)
        {
            BigInteger targetInt = BigInteger.Parse("0" + target, System.Globalization.NumberStyles.HexNumber);
            BigInteger hashInt = BigInteger.Parse("0" + hash, System.Globalization.NumberStyles.HexNumber);
            return hashInt < targetInt;
        }

        private static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static double HashRate(double shares, int seconds)
        {
            return shares * 4294967296 / seconds / 1000000; // 4294967296 = 2^32
        }
    }
}