using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NAT_Test
{
	public class SubServer
	{
		volatile bool m_run = false;

		Thread m_thread = null;
		

		public void Start(IPEndPoint a_bindAddr_udp)
		{
			if (a_bindAddr_udp == null)
				throw new ArgumentNullException();

			Socket sock = new Socket(AddressFamily.InterNetwork,
									 SocketType.Dgram,
									 ProtocolType.Udp);
			sock.Bind(a_bindAddr_udp);
			SocketIo io = new SocketIo(sock);
			io.Start();

			m_run = true;
			m_thread = new Thread((object a_data) =>
			{
				while (m_run) {
					Message msg;
					IPEndPoint sender;
					if (io.WaitForRecv(Config.Timeout_Ms, out msg, out sender) == false)
						continue;
					
					IPEndPoint dst;
					if (msg.AddressIsEmpty())
						dst = sender;
					else
						dst = new IPEndPoint(IPAddress.Parse(msg.m_address), msg.m_port);

					Config.OnEventDelegate(
						"Requested from " + sender.ToString() +
						", context=(" + msg.m_contextID + ":" + msg.m_contextSeq + ")");
					Config.OnEventDelegate("Response to " + dst.ToString());

					msg.m_address = sender.Address.ToString();
					msg.m_port = sender.Port;
					++msg.m_contextSeq;

					io.SendTo(msg, dst);
				}
			});

			m_thread.Start();
		}


		public void Stop()
		{
			m_run = false;
			if (m_thread != null) {
				m_thread.Join();
				m_thread = null;
			}
		}
	}
}
