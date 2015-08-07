using System;
using System.Net;
using System.Net.Sockets;

namespace SampleCommon
{
	public class Common
	{
		public static ProtocolType ParseProtocol(string protocol)
		{
			string t = protocol.ToUpper();
			switch (t) {
				case "UDP":
					return ProtocolType.Udp;
				case "TCP":
					return ProtocolType.Tcp;
				default:
					break;
			}
			throw new ArgumentException("프로토콜 정보가 잘못되었습니다. (" + protocol + ")");
		}

		public static IPEndPoint ParseURL(string url)
		{
			string[] split = url.Split(':');
			if (split.Length != 2)
				throw new ArgumentException("주소 정보가 잘못되었습니다. (" + url + ")");

			return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
		}
	}
}
