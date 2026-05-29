using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    public static partial class IpaddressModule
    {
        /// <summary>
        /// Create an IPv4Address or IPv6Address object from a string.
        /// Auto-detects the version based on the input.
        /// </summary>
        /// <param name="address">The string representation of the IP address.</param>
        /// <returns>An <see cref="IPv4Address"/> or <see cref="IPv6Address"/>.</returns>
        public static object IpAddress(string address)
        {
            if (address == null)
            {
                throw new ValueError("address cannot be None");
            }

            if (!IPAddress.TryParse(address, out IPAddress? parsed))
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 address");
            }

            if (parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPv4Address(parsed);
            }

            return new IPv6Address(parsed);
        }

        /// <summary>
        /// Create an IPv4Network or IPv6Network object from a CIDR string.
        /// </summary>
        /// <param name="address">The string representation of the network (e.g., "192.168.1.0/24").</param>
        /// <param name="strict">If true, host bits must not be set. Default is true.</param>
        /// <returns>An <see cref="IPv4Network"/> or <see cref="IPv6Network"/>.</returns>
        public static object IpNetwork(string address, bool strict = true)
        {
            if (address == null)
            {
                throw new ValueError("address cannot be None");
            }

            string[] parts = address.Split('/');
            if (parts.Length != 2)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 network");
            }

            if (!IPAddress.TryParse(parts[0], out IPAddress? networkAddress))
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 network");
            }

            if (!int.TryParse(parts[1], out int prefixLength))
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 network");
            }

            if (networkAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPv4Network(networkAddress, prefixLength, strict);
            }

            return new IPv6Network(networkAddress, prefixLength, strict);
        }

        /// <summary>
        /// Create an IPv4Interface or IPv6Interface object from a CIDR string.
        /// </summary>
        /// <param name="address">The string representation of the interface (e.g., "192.168.1.1/24").</param>
        /// <returns>An <see cref="IPv4Interface"/> or <see cref="IPv6Interface"/>.</returns>
        public static object IpInterface(string address)
        {
            if (address == null)
            {
                throw new ValueError("address cannot be None");
            }

            string[] parts = address.Split('/');
            if (parts.Length != 2)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 interface");
            }

            if (!IPAddress.TryParse(parts[0], out IPAddress? ifaceAddress))
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 interface");
            }

            if (!int.TryParse(parts[1], out int prefixLength))
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 or IPv6 interface");
            }

            if (ifaceAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPv4Interface(ifaceAddress, prefixLength);
            }

            return new IPv6Interface(ifaceAddress, prefixLength);
        }
    }
}
