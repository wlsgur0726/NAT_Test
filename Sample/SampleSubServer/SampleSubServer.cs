using NAT_Test;
using SampleCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleSubServer
{
	class SampleSubServer
	{
		static void Main(string[] args)
		{
			try {
				if (args.Length < 1)
					throw new ArgumentException("IP:Port 정보들을 인자로 넘겨주세요.");

				SubServer SubServer = new SubServer();
				SubServer.Start(Common.ParseURL(args[0]));
				Console.WriteLine("SubServer 시작");
				while (Console.ReadKey().Key != ConsoleKey.Escape) {
				}
				Console.WriteLine("종료");
				SubServer.Stop();
			}
			catch (Exception e) {
				Console.Error.WriteLine(e.ToString());
			}
		}
	}
}
