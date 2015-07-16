using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SampleCommon;

namespace Client
{
	class Program
	{
		static void Main(string[] args)
		{
			try {
				if (args.Length < 3)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				NAT_Test.Client testClient = new NAT_Test.Client(Common.ParseURL(args[0]),
																 Common.ParseURL(args[1]),
																 Common.ParseURL(args[2]));
				var result = testClient.StartTest();
				Console.WriteLine(result.ToString());
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
		}
	}
}
