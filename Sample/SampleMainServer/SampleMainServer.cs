using NAT_Test;
using SampleCommon;
using System;

namespace SampleMainServer
{
	class SampleMainServer
	{
		static void Main(string[] args)
		{
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
				mainServer.Start();
				Console.WriteLine("MainServer 시작");
				while (Console.ReadKey().Key != ConsoleKey.Escape) {
				}
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
			finally {
				Console.WriteLine("종료");
				if (mainServer != null)
					mainServer.Stop();
			}
		}
	}
}
