using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;

namespace DnsBlocker.Core
{
    public class DnsPacket
    {
        // ... (Properties remain the same)
        public ushort Id { get; set; }
        public ushort Flags { get; set; }
        public ushort QDCount { get; set; }
        public ushort ANCount { get; set; }
        public ushort NSCount { get; set; }
        public ushort ARCount { get; set; }

        public List<DnsQuestion> Questions { get; set; } = new List<DnsQuestion>();


        public static DnsPacket Parse(byte[] data)
        {
            // ... (Parse implementation remains the same)
            var packet = new DnsPacket();
            int offset = 0;

            packet.Id = ReadUShort(data, ref offset);
            packet.Flags = ReadUShort(data, ref offset);
            packet.QDCount = ReadUShort(data, ref offset);
            packet.ANCount = ReadUShort(data, ref offset);
            packet.NSCount = ReadUShort(data, ref offset);
            packet.ARCount = ReadUShort(data, ref offset);

            for (int i = 0; i < packet.QDCount; i++)
            {
                var question = new DnsQuestion
                {
                    DomainName = ReadDomainName(data, ref offset),
                    QType = ReadUShort(data, ref offset),
                    QClass = ReadUShort(data, ref offset)
                };
                packet.Questions.Add(question);
            }

            return packet;
        }

        private static ushort ReadUShort(byte[] data, ref int offset)
        {
            // This is only called internally, so it remains private
            ushort value = (ushort)(data[offset] << 8 | data[offset + 1]);
            offset += 2;
            return value;
        }

        // --- FIX 2: Changed to public static to be accessible by DnsServer.cs ---
        public static string ReadDomainName(byte[] data, ref int offset)
        {
            var labels = new List<string>();
            int length = data[offset++];

            while (length != 0)
            {
                if ((length & 0xC0) == 0xC0)
                {
                    // Handles compression pointer
                    offset++;
                    break;
                }
                if (length > 0)
                {
                    int bytesToRead = Math.Min(length, data.Length - offset);
                    if (bytesToRead > 0)
                    {
                        labels.Add(Encoding.ASCII.GetString(data, offset, bytesToRead));
                        offset += bytesToRead;
                    }
                }

                if (offset < data.Length)
                {
                    length = data[offset++];
                }
                else
                {
                    break;
                }
            }

            return string.Join(".", labels);
        }
    }
    public class DnsQuestion
    {
        public string DomainName { get; set; } = string.Empty;
        public ushort QType { get; set; }
        public ushort QClass { get; set; }
    }
}