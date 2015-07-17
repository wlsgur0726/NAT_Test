using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
				if (args.Length < 3)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				testClient = new NAT_Test.Client(Common.ParseURL(args[0]),
												 Common.ParseURL(args[1]),
												 Common.ParseURL(args[2]));
				var result = testClient.StartTest();
				Console.WriteLine("\n" + result.ToString());
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
		}
	}
}
