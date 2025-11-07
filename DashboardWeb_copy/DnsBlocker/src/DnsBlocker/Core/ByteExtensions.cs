using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DnsBlocker.Core
{
    /// <summary>
    /// Provides utility extension methods for byte array and numeric manipulation,
    /// primarily used for handling network byte order (endianness) in DNS packet processing.
    /// This static class is required for defining extension methods in C#.
    /// </summary>
    public static class ByteExtensions
    {
        /// <summary>
        /// Reverses the order of bytes in a sequence.
        /// This is used to convert host byte order to network byte order when building packets.
        /// </summary>
        /// <param name="data">The byte array to reverse.</param>
        /// <returns>The reversed byte array.</returns>
        public static byte[] Reverse(this byte[] data)
        {
            // Check if the current machine architecture is little-endian. 
            // If so, we must reverse the bytes to get network (big-endian) order.
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return data;
        }

        /// <summary>
        /// Appends a byte array to an existing List of bytes.
        /// </summary>
        /// <param name="list">The list to which the data will be added.</param>
        /// <param name="data">The byte array to append.</param>
        public static void AddRange(this List<byte> list, byte[] data)
        {
            list.AddRange(data);
        }

        /// <summary>
        /// Converts a ushort (2-byte integer) from network byte order (Big-Endian) to host byte order.
        /// This is essential for correctly reading fields like ID, Flags, and Counts from the DNS packet header.
        /// </summary>
        public static ushort ToHostOrder(this ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                // NetworkToHostOrder requires a signed short, so we cast to short and back to ushort
                return (ushort)IPAddress.NetworkToHostOrder((short)value);
            }
            return value;
        }
    }
}
