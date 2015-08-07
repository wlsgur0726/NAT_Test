using NAT_Test;
using SampleCommon;
using System;

namespace SampleSubServer
{
	class SampleSubServer
	{
		static void Main(string[] args)
		{
			SubServer subServer = null;
			try {
				if (args.Length < 2)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				subServer = new SubServer(Common.ParseURL(args[0]),
										  Common.ParseURL(args[1]));
				subServer.Start();
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
