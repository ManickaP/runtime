// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Xunit;

namespace System.Net.NetworkInformation.Tests
{
    public class AddressParsingTests : FileCleanupTestBase
    {
        [Fact]
        public void HexIPAddressParsing()
        {
            if (BitConverter.IsLittleEndian)
            {
                Assert.Equal(IPAddress.Parse("10.105.128.1"), StringParsingHelpers.ParseHexIPAddress("0180690A"));
                Assert.Equal(IPAddress.Parse("103.69.35.1"), StringParsingHelpers.ParseHexIPAddress("01234567"));
                Assert.Equal(IPAddress.Parse("152.186.220.254"), StringParsingHelpers.ParseHexIPAddress("FEDCBA98"));

                Assert.Equal(IPAddress.Parse("::"), StringParsingHelpers.ParseHexIPAddress("00000000000000000000000000000000"));
                Assert.Equal(IPAddress.Parse("::1"), StringParsingHelpers.ParseHexIPAddress("00000000000000000000000001000000"));
                Assert.Equal(IPAddress.Parse("fec0::1"), StringParsingHelpers.ParseHexIPAddress("0000C0FE000000000000000001000000"));
                Assert.Equal(IPAddress.Parse("fe80::222:222"), StringParsingHelpers.ParseHexIPAddress("000080FE000000000000000022022202"));
                Assert.Equal(IPAddress.Parse("fe80::215:5dff:fe00:402"), StringParsingHelpers.ParseHexIPAddress("000080FE00000000FF5D1502020400FE"));
            }
            else
            {
                Assert.Equal(IPAddress.Parse("10.105.128.1"), StringParsingHelpers.ParseHexIPAddress("0A698001"));
                Assert.Equal(IPAddress.Parse("103.69.35.1"), StringParsingHelpers.ParseHexIPAddress("67452301"));
                Assert.Equal(IPAddress.Parse("152.186.220.254"), StringParsingHelpers.ParseHexIPAddress("98BADCFE"));

                Assert.Equal(IPAddress.Parse("::"), StringParsingHelpers.ParseHexIPAddress("00000000000000000000000000000000"));
                Assert.Equal(IPAddress.Parse("::1"), StringParsingHelpers.ParseHexIPAddress("00000000000000000000000000000001"));
                Assert.Equal(IPAddress.Parse("fec0::1"), StringParsingHelpers.ParseHexIPAddress("FEC00000000000000000000000000001"));
                Assert.Equal(IPAddress.Parse("fe80::222:222"), StringParsingHelpers.ParseHexIPAddress("FE800000000000000000000002220222"));
                Assert.Equal(IPAddress.Parse("fe80::215:5dff:fe00:402"), StringParsingHelpers.ParseHexIPAddress("FE8000000000000002155DFFFE000402"));
            }
        }

        [Fact]
        public void IPv4GatewayAddressParsing()
        {
            string fileName = GetTestFilePath();
            if (BitConverter.IsLittleEndian)
            {
                FileUtil.NormalizeLineEndings("NetworkFiles/route", fileName);
            }
            else
            {
                FileUtil.NormalizeLineEndings("NetworkFiles/route-be", fileName);
            }
            List<GatewayIPAddressInformation> gatewayAddresses = new List<GatewayIPAddressInformation>();
            StringParsingHelpers.ParseIPv4GatewayAddressesFromRouteFile(gatewayAddresses, File.ReadAllLines(fileName), "wlan0");
            Assert.Equal(3, gatewayAddresses.Count);

            Assert.Equal(IPAddress.Parse("10.105.128.1"), gatewayAddresses[0].Address);
            Assert.Equal(IPAddress.Parse("103.69.35.1"), gatewayAddresses[1].Address);
            Assert.Equal(IPAddress.Parse("152.186.220.254"), gatewayAddresses[2].Address);
        }

        [Fact]
        public void IPv6GatewayAddressParsing()
        {
            string fileName = GetTestFilePath();
            FileUtil.NormalizeLineEndings("NetworkFiles/ipv6_route", fileName);
            List<GatewayIPAddressInformation> gatewayAddresses = new List<GatewayIPAddressInformation>();
            StringParsingHelpers.ParseIPv6GatewayAddressesFromRouteFile(gatewayAddresses, File.ReadAllLines(fileName), "lo", 42);
            Assert.Equal(0, gatewayAddresses.Count);

            StringParsingHelpers.ParseIPv6GatewayAddressesFromRouteFile(gatewayAddresses, File.ReadAllLines(fileName), "foo", 42);
            Assert.Equal(0, gatewayAddresses.Count);

            StringParsingHelpers.ParseIPv6GatewayAddressesFromRouteFile(gatewayAddresses, File.ReadAllLines(fileName), "enp0s5", 42);
            Assert.Equal(2, gatewayAddresses.Count);

            Assert.Equal(IPAddress.Parse("2002:2c26:f4e4:0:21c:42ff:fe20:4636"), gatewayAddresses[0].Address);
            Assert.Equal(IPAddress.Parse("fe80::21c:42ff:fe00:18%42"), gatewayAddresses[1].Address);

            gatewayAddresses = new List<GatewayIPAddressInformation>();
            StringParsingHelpers.ParseIPv6GatewayAddressesFromRouteFile(gatewayAddresses, File.ReadAllLines(fileName), "wlan0", 21);
            Assert.Equal(IPAddress.Parse("fe80::21c:42ff:fe00:18%21"), gatewayAddresses[0].Address);

            gatewayAddresses = new List<GatewayIPAddressInformation>();
            StringParsingHelpers.ParseIPv6GatewayAddressesFromRouteFile(gatewayAddresses, File.ReadAllLines(fileName), null, 0);
            Assert.Equal(3, gatewayAddresses.Count);
        }

        [Fact]
        public void DhcpServerAddressParsing()
        {
            string fileName = GetTestFilePath();
            FileUtil.NormalizeLineEndings("NetworkFiles/dhclient.leases", fileName);
            List<IPAddress> dhcpServerAddresses = new List<IPAddress>();
            StringParsingHelpers.ParseDhcpServerAddressesFromLeasesFile(dhcpServerAddresses, fileName, "wlan0");
            Assert.Equal(1, dhcpServerAddresses.Count);
            Assert.Equal(IPAddress.Parse("10.105.128.4"), dhcpServerAddresses[0]);
        }

        [Theory]
        [InlineData("NetworkFiles/smb.conf")]
        [InlineData("NetworkFiles/smb_with_commented_wins.conf")]
        public void WinsServerAddressParsing(string source)
        {
            string fileName = GetTestFilePath();
            FileUtil.NormalizeLineEndings(source, fileName);

            List<IPAddress> winsServerAddresses = StringParsingHelpers.ParseWinsServerAddressesFromSmbConfFile(fileName);
            Assert.Equal(1, winsServerAddresses.Count);
            Assert.Equal(IPAddress.Parse("255.1.255.1"), winsServerAddresses[0]);
        }

        [Fact]
        public void WinsServerAddressParsingWhenFileHasNotAny()
        {
            string fileName = GetTestFilePath();
            FileUtil.NormalizeLineEndings("NetworkFiles/smb_without_wins.conf", fileName);

            List<IPAddress> winsServerAddresses = StringParsingHelpers.ParseWinsServerAddressesFromSmbConfFile(fileName);
            Assert.Empty(winsServerAddresses);
        }
    }
}
