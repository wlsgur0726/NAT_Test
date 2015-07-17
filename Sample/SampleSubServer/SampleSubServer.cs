using NAT_Test;
using SampleCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SampleSubServer
{
	class SampleSubServer
	{
		static void Main(string[] args)
		{
			SubServer subServer = null;
			try {
				if (args.Length < 1)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				subServer = new SubServer();
				subServer.Start(Common.ParseURL(args[0]));
				Console.WriteLine("SubServer 시작");
				while (Console.ReadKey().Key != ConsoleKey.Escape) {
				}
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
			finally {
				Console.WriteLine("종료");
				if (subServer != null)
					subServer.Stop();
			}
		}
	}
}
