using NAT_Test;
using SampleCommon;
using System;

namespace SampleSubServer
{
	class SampleSubServer
	{
		static void Main(string[] args)
		{
			bool invalidParameter = true;
			SubServer subServer = null;
			try {
				if (args.Length < 2)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");
				subServer = new SubServer(Common.ParseURL(args[0]),
										  Common.ParseURL(args[1]));

				invalidParameter = false;
				subServer.Start();
				Console.WriteLine("SubServer 시작");
				while (Console.ReadKey().Key != ConsoleKey.Escape) {
				}
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
				if (invalidParameter) {
					Console.Error.WriteLine("");
					Console.Error.WriteLine("  1번째 인자  :  이 서버의 TCP 바인딩 주소 (ex 0.0.0.0:11111)");
					Console.Error.WriteLine("  2번째 인자  :  이 서버의 UDP 바인딩 주소 (ex 0.0.0.0:22222)");
				}
			}
			finally {
				Console.WriteLine("종료");
				if (subServer != null)
					subServer.Stop();
			}
		}
	}
}
