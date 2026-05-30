using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using SCG = System.Collections.Generic;

namespace Sharpy
{
    public static partial class IpaddressModule
    {
        public static object IpAddress(string address)
        {
            if (address == null)
            {
                throw new ValueError("address cannot be None");
            }

            if (!IPAddress.TryParse(address, out IPAddress? parsed))
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv4 or IPv6 address");
            }

            if (parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPv4Address(address);
            }

            return new IPv6Address(address);
        }

        public static object IpNetwork(string address, bool strict = true)
        {
            if (address == null)
            {
                throw new ValueError("address cannot be None");
            }

            int slashIdx = address.IndexOf('/');
            string addrPart = slashIdx >= 0 ? address.Substring(0, slashIdx) : address;

            if (!IPAddress.TryParse(addrPart, out IPAddress? parsed))
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv4 or IPv6 network");
            }

            if (parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPv4Network(address, strict);
            }

            return new IPv6Network(address, strict);
        }

        public static object IpInterface(string address)
        {
            if (address == null)
            {
                throw new ValueError("address cannot be None");
            }

            int slashIdx = address.IndexOf('/');
            string addrPart = slashIdx >= 0 ? address.Substring(0, slashIdx) : address;

            if (!IPAddress.TryParse(addrPart, out IPAddress? parsed))
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv4 or IPv6 interface");
            }

            if (parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPv4Interface(address);
            }

            return new IPv6Interface(address);
        }

        public static SCG.List<object> CollapseAddresses(SCG.List<object> addresses)
        {
            var v4Networks = new SCG.List<IPv4Network>();
            var v6Networks = new SCG.List<IPv6Network>();

            foreach (var addr in addresses)
            {
                if (addr is IPv4Network v4n)
                {
                    v4Networks.Add(v4n);
                }
                else if (addr is IPv6Network v6n)
                {
                    v6Networks.Add(v6n);
                }
                else if (addr is IPv4Address v4a)
                {
                    v4Networks.Add(new IPv4Network(v4a.ToString() + "/32"));
                }
                else if (addr is IPv6Address v6a)
                {
                    v6Networks.Add(new IPv6Network(v6a.ToString() + "/128"));
                }
                else
                {
                    throw new TypeError("Expected IPv4 or IPv6 address/network objects");
                }
            }

            if (v4Networks.Count > 0 && v6Networks.Count > 0)
            {
                throw new TypeError("Cannot collapse mixed IPv4 and IPv6 addresses");
            }

            if (v4Networks.Count > 0)
            {
                return CollapseV4(v4Networks).Cast<object>().ToList();
            }

            return CollapseV6(v6Networks).Cast<object>().ToList();
        }

        public static SCG.List<object> SummarizeAddressRange(object first, object last)
        {
            if (first is IPv4Address f4 && last is IPv4Address l4)
            {
                return SummarizeV4Range(f4, l4).Cast<object>().ToList();
            }
            if (first is IPv6Address f6 && last is IPv6Address l6)
            {
                return SummarizeV6Range(f6, l6).Cast<object>().ToList();
            }

            throw new TypeError("first and last must be the same type (both IPv4Address or both IPv6Address)");
        }

        private static SCG.List<IPv4Network> CollapseV4(SCG.List<IPv4Network> networks)
        {
            if (networks.Count == 0)
                return new SCG.List<IPv4Network>();

            networks.Sort();

            var merged = new SCG.List<IPv4Network>();
            foreach (var net in networks)
            {
                if (merged.Count > 0)
                {
                    var prev = merged[merged.Count - 1];
                    if (prev.Overlaps(net) || prev.SupernetOf(net))
                    {
                        continue;
                    }
                }
                merged.Add(net);
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                var next = new SCG.List<IPv4Network>();

                int i = 0;
                while (i < merged.Count)
                {
                    if (i + 1 < merged.Count)
                    {
                        var a = merged[i];
                        var b = merged[i + 1];
                        if (a.Prefixlen == b.Prefixlen && a.Prefixlen > 0)
                        {
                            var super = a.Supernet();
                            if (super.Contains(a.NetworkAddress) && super.Contains(b.BroadcastAddress))
                            {
                                next.Add(super);
                                i += 2;
                                changed = true;
                                continue;
                            }
                        }
                    }
                    next.Add(merged[i]);
                    i++;
                }
                merged = next;
            }

            return merged;
        }

        private static SCG.List<IPv6Network> CollapseV6(SCG.List<IPv6Network> networks)
        {
            if (networks.Count == 0)
                return new SCG.List<IPv6Network>();

            networks.Sort();

            var merged = new SCG.List<IPv6Network>();
            foreach (var net in networks)
            {
                if (merged.Count > 0)
                {
                    var prev = merged[merged.Count - 1];
                    if (prev.Overlaps(net) || prev.SupernetOf(net))
                    {
                        continue;
                    }
                }
                merged.Add(net);
            }

            return merged;
        }

        private static SCG.List<IPv4Network> SummarizeV4Range(IPv4Address first, IPv4Address last)
        {
            var result = new SCG.List<IPv4Network>();
            uint start = first.Value;
            uint end = last.Value;

            if (start > end)
            {
                throw new ValueError("first address must be less than or equal to last address");
            }

            while (start <= end)
            {
                int nbits = 32;

                if (start != 0)
                {
                    int trailingZeros = CountTrailingZeros(start);
                    nbits = Math.Min(nbits, 32 - trailingZeros);
                }

                while (nbits > 0)
                {
                    long count = 1L << (32 - nbits);
                    if ((long)start + count - 1 <= end)
                        break;
                    nbits++;
                }

                result.Add(new IPv4Network(start, nbits));

                long next = (long)start + (1L << (32 - nbits));
                if (next > 0xFFFFFFFF)
                    break;
                start = (uint)next;
            }

            return result;
        }

        private static int CountTrailingZeros(uint value)
        {
            if (value == 0)
                return 32;
            int count = 0;
            while ((value & 1) == 0)
            {
                count++;
                value >>= 1;
            }
            return count;
        }

        private static SCG.List<IPv6Network> SummarizeV6Range(IPv6Address first, IPv6Address last)
        {
            var result = new SCG.List<IPv6Network>();
            System.Numerics.BigInteger start = first.ToInt();
            System.Numerics.BigInteger end = last.ToInt();
            System.Numerics.BigInteger maxAddr = (System.Numerics.BigInteger.One << 128) - 1;

            if (start > end)
            {
                throw new ValueError("first address must be less than or equal to last address");
            }

            while (start <= end)
            {
                int nbits = 128;

                if (start != 0)
                {
                    int trailingZeros = CountTrailingZerosBig(start);
                    nbits = Math.Min(nbits, 128 - trailingZeros);
                }

                while (nbits > 0)
                {
                    System.Numerics.BigInteger count = System.Numerics.BigInteger.One << (128 - nbits);
                    if (start + count - 1 <= end)
                        break;
                    nbits++;
                }

                byte[] addrBytes = new IPv6Address(start).Address.GetAddressBytes();
                result.Add(new IPv6Network(addrBytes, nbits));

                System.Numerics.BigInteger next = start + (System.Numerics.BigInteger.One << (128 - nbits));
                if (next > maxAddr)
                    break;
                start = next;
            }

            return result;
        }

        private static int CountTrailingZerosBig(System.Numerics.BigInteger value)
        {
            if (value == 0)
                return 128;
            int count = 0;
            while ((value & 1) == 0)
            {
                count++;
                value >>= 1;
            }
            return count;
        }
    }
}
