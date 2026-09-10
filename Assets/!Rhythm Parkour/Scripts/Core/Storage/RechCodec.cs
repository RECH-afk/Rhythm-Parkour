using System;
using System.Text;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.Core.Storage
{
    public sealed class RechCodec : IDataCodec
    {
        private readonly char[] _alphabet;

        public RechCodec() : this(new[] { 'R', 'E', 'C', 'H' })
        {
        }

        public RechCodec(char[] alphabet)
        {
            _alphabet = alphabet;
        }

        public string Encode(string plain)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(plain);
            StringBuilder sb = new StringBuilder(bytes.Length * 4);
            foreach (byte b in bytes)
            {
                sb.Append(_alphabet[(b >> 6) & 0b11]);
                sb.Append(_alphabet[(b >> 4) & 0b11]);
                sb.Append(_alphabet[(b >> 2) & 0b11]);
                sb.Append(_alphabet[b & 0b11]);
            }
            return sb.ToString();
        }

        public string Decode(string encoded)
        {
            if (encoded.Length % 4 != 0)
                throw new FormatException("Invalid RECH format");
            byte[] bytes = new byte[encoded.Length / 4];
            for (int i = 0; i < encoded.Length; i += 4)
            {
                int a = Array.IndexOf(_alphabet, encoded[i]);
                int b = Array.IndexOf(_alphabet, encoded[i + 1]);
                int c = Array.IndexOf(_alphabet, encoded[i + 2]);
                int d = Array.IndexOf(_alphabet, encoded[i + 3]);
                bytes[i / 4] = (byte)((a << 6) | (b << 4) | (c << 2) | d);
            }
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
