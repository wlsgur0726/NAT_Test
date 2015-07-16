using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SampleCommon
{
	public class Common
	{
		public static IPEndPoint ParseURL(string urlStr)
		{
			string[] split = urlStr.Split(':');
			if (split.Length != 2)
				throw new ArgumentException("주소 정보가 잘못되었습니다.");

			return new IPEndPoint(IPAddress.Parse(split[0]), int.Parse(split[1]));
		}
	}
}
