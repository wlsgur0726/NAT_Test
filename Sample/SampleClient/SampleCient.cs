using System;
using SampleCommon;
using NAT_Test;

namespace SampleClient
{
	class Program
	{
		static void Main(string[] args)
		{
			Client testClient = null;
			try {
				if (args.Length < 4)
					throw new ArgumentException("프로토콜과 IP:Port 정보들을 인자로 넘겨주세요.");

				testClient = new NAT_Test.Client(Common.ParseProtocol(args[0]),
												 Common.ParseURL(args[1]),
												 Common.ParseURL(args[2]),
												 Common.ParseURL(args[3]));
				var result = testClient.StartTest();
				Console.WriteLine("\n" + result.ToString());
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
		}
	}
}
