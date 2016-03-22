using NAT_Test;
using SampleCommon;
using System;

namespace SampleMainServer
{
	class SampleMainServer
	{
		static void Main(string[] args)
		{
			bool invalidParameter = true;
			MainServer mainServer = null;
			try {
				if (args.Length < 6)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				mainServer = new MainServer(Common.ParseURL(args[0]),
											Common.ParseURL(args[1]),
											Common.ParseURL(args[2]),
											Common.ParseURL(args[3]),
											Common.ParseURL(args[4]),
											Common.ParseURL(args[5]));

				invalidParameter = false;
				mainServer.Start();
				Console.WriteLine("MainServer 시작");
				while (Console.ReadKey().Key != ConsoleKey.Escape) {
				}
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
				if (invalidParameter) {
					Console.Error.WriteLine("");
					Console.Error.WriteLine("  1번째 인자  :  이 서버의 TCP 바인딩 주소1 (ex 0.0.0.0:10001)");
					Console.Error.WriteLine("  2번째 인자  :  이 서버의 TCP 바인딩 주소2 (ex 0.0.0.0:10002)");
					Console.Error.WriteLine("  3번째 인자  :  SubServer의 TCP 주소 (ex 11.22.33.44:11111)");
					Console.Error.WriteLine("  4번째 인자  :  이 서버의 UDP 바인딩 주소1 (ex 0.0.0.0:20001)");
					Console.Error.WriteLine("  5번째 인자  :  이 서버의 UDP 바인딩 주소2 (ex 0.0.0.0:20002)");
					Console.Error.WriteLine("  6번째 인자  :  SubServer의 UDP 주소 (ex 11.22.33.44:22222)");
				}
			}
			finally {
				Console.WriteLine("종료");
				if (mainServer != null)
					mainServer.Stop();
			}
		}
	}
}
