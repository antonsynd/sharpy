using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Interface
    {
        public IPv4Address Ip { get; }
        public IPv4Network Network { get; }

        public int Version => 4;
        public int Prefixlen => Network.Prefixlen;
        public string WithPrefixlen => Ip + "/" + Network.Prefixlen;
        public string WithNetmask => Ip + "/" + Network.Netmask;
        public string WithHostmask => Ip + "/" + Network.Hostmask;

        public IPv4Interface(string address)
        {
            int slashIdx = address.IndexOf('/');
            if (slashIdx < 0)
            {
                Ip = new IPv4Address(address);
                Network = new IPv4Network(address + "/32", strict: false);
                return;
            }

            string addrPart = address.Substring(0, slashIdx);
            string prefixPart = address.Substring(slashIdx + 1);

            Ip = new IPv4Address(addrPart);
            Network = new IPv4Network(address, strict: false);
        }

        public override string ToString() => Ip + "/" + Network.Prefixlen;

        public override int GetHashCode() => Ip.GetHashCode() ^ Network.GetHashCode();

        public override bool Equals(object? obj)
        {
            return obj is IPv4Interface other && Ip.Equals(other.Ip) && Network.Equals(other.Network);
        }
    }

    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Interface
    {
        public IPv6Address Ip { get; }
        public IPv6Network Network { get; }

        public int Version => 6;
        public int Prefixlen => Network.Prefixlen;
        public string WithPrefixlen => Ip + "/" + Network.Prefixlen;
        public string WithNetmask => Ip + "/" + Network.Netmask;

        public IPv6Interface(string address)
        {
            int slashIdx = address.IndexOf('/');
            if (slashIdx < 0)
            {
                Ip = new IPv6Address(address);
                Network = new IPv6Network(address + "/128", strict: false);
                return;
            }

            string addrPart = address.Substring(0, slashIdx);
            Ip = new IPv6Address(addrPart);
            Network = new IPv6Network(address, strict: false);
        }

        public override string ToString() => Ip + "/" + Network.Prefixlen;

        public override int GetHashCode() => Ip.GetHashCode() ^ Network.GetHashCode();

        public override bool Equals(object? obj)
        {
            return obj is IPv6Interface other && Ip.Equals(other.Ip) && Network.Equals(other.Network);
        }
    }
}
