using NAT_Test;
using SampleCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace SampleMainServer
{
	class SampleMainServer
	{
		static void Main(string[] args)
		{
			try {
				if (args.Length < 3)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				MainServer mainServer = new MainServer(Common.ParseURL(args[0]),
													   Common.ParseURL(args[1]),
													   Common.ParseURL(args[2]));
				mainServer.Start();
				Console.WriteLine("MainServer 시작");
				while (Console.ReadKey().Key != ConsoleKey.Escape) {
				}
				Console.WriteLine("종료");
				mainServer.Stop();
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
		}
	}
}
