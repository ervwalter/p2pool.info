using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace P2Pool
{
	public static class BitcoinAddresses
	{
		public static string PublicKeyToPubHash(string publicKey)
		{
			byte[] bytes = ValidateAndGetHexPublicKey(publicKey);
			if (bytes == null)
			{
				return null;
			}

			SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
			byte[] shaofpubkey = sha256.ComputeHash(bytes);

			RIPEMD160 rip = System.Security.Cryptography.RIPEMD160.Create();
			byte[] ripofpubkey = rip.ComputeHash(shaofpubkey);

			return ByteArrayToString(ripofpubkey);
		}

		public static string PubHashToAddress(string pubHash)
		{
			byte[] bytes = ValidateAndGetHexPublicHash(pubHash);
			if (bytes == null)
			{
				return null;
			}

			byte[] bytes2 = new byte[21];
			Array.Copy(bytes, 0, bytes2, 1, 20);

			int cointype = 0;

			//if (cboCoinType.Text == "Testnet") cointype = 111;
			//if (cboCoinType.Text == "Namecoin") cointype = 52;
			bytes2[0] = (byte)(cointype & 0xff);
			return ByteArrayToBase58Check(bytes2);

		}

		public static string AddressToPubHash(string address)
		{
			byte[] bytes = Base58ToByteArray(address);
			if (bytes == null || bytes.Length != 21)
			{
				return null;
			}
			return ByteArrayToString(bytes, 1, 20);
		}

		private static byte[] ValidateAndGetHexPublicKey(string hex)
		{
			byte[] publicKey = GetHexBytes(hex, 64);

			if (publicKey == null || publicKey.Length < 64 || publicKey.Length > 65)
			{
				return null;
			}

			// if leading 00, change it to 0x80
			if (publicKey.Length == 65)
			{
				if (publicKey[0] == 0 || publicKey[0] == 4)
				{
					publicKey[0] = 4;
				}
				else
				{
					return null;
				}
			}

			// add 0x80 byte if not present
			if (publicKey.Length == 64)
			{
				byte[] hex2 = new byte[65];
				Array.Copy(publicKey, 0, hex2, 1, 64);
				hex2[0] = 4;
				publicKey = hex2;
			}
			return publicKey;
		}

		private static byte[] ValidateAndGetHexPublicHash(string hex)
		{
			byte[] publicHash = GetHexBytes(hex, 20);

			if (publicHash == null || publicHash.Length != 20)
			{
				return null;
			}
			return publicHash;
		}

		private static byte[] GetHexBytes(string source, int minimum)
		{
			byte[] hex = GetHexBytes(source);
			if (hex == null) return null;
			// assume leading zeroes if we're short a few bytes
			if (hex.Length > (minimum - 6) && hex.Length < minimum)
			{
				byte[] hex2 = new byte[minimum];
				Array.Copy(hex, 0, hex2, minimum - hex.Length, hex.Length);
				hex = hex2;
			}
			// clip off one overhanging leading zero if present
			if (hex.Length == minimum + 1 && hex[0] == 0)
			{
				byte[] hex2 = new byte[minimum];
				Array.Copy(hex, 1, hex2, 0, minimum);
				hex = hex2;

			}

			return hex;
		}

		private static byte[] GetHexBytes(string source)
		{


			List<byte> bytes = new List<byte>();
			// copy s into ss, adding spaces between each byte
			string s = source;
			string ss = "";
			int currentbytelength = 0;
			foreach (char c in s.ToCharArray())
			{
				if (c == ' ')
				{
					currentbytelength = 0;
				}
				else
				{
					currentbytelength++;
					if (currentbytelength == 3)
					{
						currentbytelength = 1;
						ss += ' ';
					}
				}
				ss += c;
			}

			foreach (string b in ss.Split(' '))
			{
				int v = 0;
				if (b.Trim() == "") continue;
				foreach (char c in b.ToCharArray())
				{
					if (c >= '0' && c <= '9')
					{
						v *= 16;
						v += (c - '0');

					}
					else if (c >= 'a' && c <= 'f')
					{
						v *= 16;
						v += (c - 'a' + 10);
					}
					else if (c >= 'A' && c <= 'F')
					{
						v *= 16;
						v += (c - 'A' + 10);
					}

				}
				v &= 0xff;
				bytes.Add((byte)v);
			}
			return bytes.ToArray();
		}


		private static byte[] Base58ToByteArray(string base58)
		{

			Org.BouncyCastle.Math.BigInteger bi2 = new Org.BouncyCastle.Math.BigInteger("0");
			string b58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

			bool IgnoreChecksum = false;

			foreach (char c in base58)
			{
				if (b58.IndexOf(c) != -1)
				{
					bi2 = bi2.Multiply(new Org.BouncyCastle.Math.BigInteger("58"));
					bi2 = bi2.Add(new Org.BouncyCastle.Math.BigInteger(b58.IndexOf(c).ToString()));
				}
				else if (c == '?')
				{
					IgnoreChecksum = true;
				}
				else
				{
					return null;
				}
			}

			byte[] bb = bi2.ToByteArrayUnsigned();

			// interpret leading '1's as leading zero bytes
			foreach (char c in base58)
			{
				if (c != '1') break;
				byte[] bbb = new byte[bb.Length + 1];
				Array.Copy(bb, 0, bbb, 1, bb.Length);
				bb = bbb;
			}

			if (bb.Length < 4) return null;

			if (IgnoreChecksum == false)
			{
				SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
				byte[] checksum = sha256.ComputeHash(bb, 0, bb.Length - 4);
				checksum = sha256.ComputeHash(checksum);
				for (int i = 0; i < 4; i++)
				{
					if (checksum[i] != bb[bb.Length - 4 + i]) return null;
				}
			}

			byte[] rv = new byte[bb.Length - 4];
			Array.Copy(bb, 0, rv, 0, bb.Length - 4);
			return rv;
		}

		private static string ByteArrayToString(byte[] ba)
		{
			return ByteArrayToString(ba, 0, ba.Length);
		}

		private static string ByteArrayToString(byte[] ba, int offset, int count)
		{
			string rv = "";
			int usedcount = 0;
			for (int i = offset; usedcount < count; i++, usedcount++)
			{
				rv += String.Format("{0:X2}", ba[i]) + " ";
			}
			return rv;
		}

		private static string ByteArrayToBase58(byte[] ba)
		{
			Org.BouncyCastle.Math.BigInteger addrremain = new Org.BouncyCastle.Math.BigInteger(1, ba);

			Org.BouncyCastle.Math.BigInteger big0 = new Org.BouncyCastle.Math.BigInteger("0");
			Org.BouncyCastle.Math.BigInteger big58 = new Org.BouncyCastle.Math.BigInteger("58");

			string b58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

			string rv = "";

			while (addrremain.CompareTo(big0) > 0)
			{
				int d = Convert.ToInt32(addrremain.Mod(big58).ToString());
				addrremain = addrremain.Divide(big58);
				rv = b58.Substring(d, 1) + rv;
			}

			// handle leading zeroes
			foreach (byte b in ba)
			{
				if (b != 0) break;
				rv = "1" + rv;

			}
			return rv;
		}


		private static string ByteArrayToBase58Check(byte[] ba)
		{

			byte[] bb = new byte[ba.Length + 4];
			Array.Copy(ba, bb, ba.Length);
			SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
			byte[] thehash = sha256.ComputeHash(ba);
			thehash = sha256.ComputeHash(thehash);
			for (int i = 0; i < 4; i++) bb[ba.Length + i] = thehash[i];
			return ByteArrayToBase58(bb);
		}


	}
}
