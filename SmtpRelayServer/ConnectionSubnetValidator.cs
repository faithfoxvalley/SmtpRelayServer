using SmtpServer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace SmtpRelayServer;

internal class ConnectionSubnetValidator
{
    private List<Subnet> subnets = new List<Subnet>();

    public ConnectionSubnetValidator(IEnumerable<string> subnetMasks) 
    {
        foreach (string subnetMask in subnetMasks)
            subnets.Add(Subnet.Parse(subnetMask));
    }

    public bool IsValid(IPAddress address)
    {
        if (subnets.Count == 0)
            return true;

        if (address == null)
        {
            Log.Warn("Null address for subnet filter test");
            return false;
        }

        foreach (Subnet subnet in subnets)
        {
            if(subnet.Contains(address))
                return true;
        }

        Log.Warn($"{address} is not in any of the allowed subnets");
        return false;
    }

    internal bool IsValid(ISessionContext context)
    {
        if (subnets.Count == 0)
            return true;

        if (context.Properties == null || !context.Properties.TryGetValue("EndpointListener:RemoteEndPoint", out object contextProp))
        {
            Log.Warn("Failed to get remote address for connection");
            return false;
        }
        return contextProp is IPEndPoint endpoint && IsValid(endpoint.Address);
    }

    private class Subnet
    {
        private readonly IPAddress maskAddress;
        private readonly int maskLength;

        public Subnet(IPAddress maskAddress, int maskLength)
        {
            this.maskAddress = maskAddress;
            this.maskLength = maskLength;
        }

        public static Subnet Parse(string subnetMask)
        {
            int slashIdx = subnetMask.IndexOf("/");
            if (slashIdx == -1) // We only handle netmasks in format "IP/PrefixLength".
                throw new NotSupportedException("Only SubNetMasks with a given prefix length are supported.");

            // First parse the address of the netmask before the prefix length.
            if(!IPAddress.TryParse(subnetMask.Substring(0, slashIdx), out IPAddress maskAddress))
            {
                Log.Warn("Unable to parse subnet mask address for " + subnetMask);
                throw new InvalidOperationException();
            }


            // Now find out how long the prefix is.
            if (!int.TryParse(subnetMask.Substring(slashIdx + 1), out int maskLength))
            {
                Log.Warn("Unable to parse subnet mask length for " + subnetMask);
                throw new InvalidOperationException();
            }

            if (maskLength < 0)
                throw new NotSupportedException("A Subnetmask should not be less than 0.");

            return new Subnet(maskAddress, maskLength);
        }

        public bool Contains(IPAddress address)
        {
            if (maskAddress.AddressFamily != address.AddressFamily) // We got something like an IPV4-Address for an IPv6-Mask. This is not valid.
                return false;

            if (maskLength == 0)
                return true;

            if (maskAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                // Convert the mask address to an unsigned integer.
                uint maskAddressBits = BitConverter.ToUInt32(maskAddress.GetAddressBytes().Reverse().ToArray(), 0);

                // And convert the IpAddress to an unsigned integer.
                uint ipAddressBits = BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray(), 0);

                // Get the mask/network address as unsigned integer.
                uint mask = uint.MaxValue << (32 - maskLength);

                // https://stackoverflow.com/a/1499284/3085985
                // Bitwise AND mask and MaskAddress, this should be the same as mask and IpAddress
                // as the end of the mask is 0000 which leads to both addresses to end with 0000
                // and to start with the prefix.
                return (maskAddressBits & mask) == (ipAddressBits & mask);
            }

            if (maskAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Convert the mask address to a BitArray. Reverse the BitArray to compare the bits of each byte in the right order.
                BitArray maskAddressBits = new BitArray(maskAddress.GetAddressBytes().Reverse().ToArray());

                // And convert the IpAddress to a BitArray. Reverse the BitArray to compare the bits of each byte in the right order.
                BitArray ipAddressBits = new BitArray(address.GetAddressBytes().Reverse().ToArray());
                int ipAddressLength = ipAddressBits.Length;

                if (maskAddressBits.Length != ipAddressBits.Length)
                {
                    Log.Warn("Subnet filter: Length of IP Address and Subnet Mask do not match.");
                    return false;
                }

                // Compare the prefix bits.
                for (int i = ipAddressLength - 1; i >= ipAddressLength - maskLength; i--)
                {
                    if (ipAddressBits[i] != maskAddressBits[i])
                        return false;
                }

                return true;
            }

            Log.Warn("Subnet filter: Only InterNetworkV6 or InterNetwork address families are supported.");
            return false;
        }
    }
}
