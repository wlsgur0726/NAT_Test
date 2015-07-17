using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NAT_Test
{
	public class MainServer
	{
		SocketIo m_udp1 = null;

		SocketIo m_udp2 = null;

		SocketIo m_subServer_udp = null;

		IPEndPoint m_subServerAddr_udp = null;

		volatile bool m_run = false;

		Thread m_thread1 = null;

		Thread m_thread2 = null;

		Thread m_heartbeatThread = null;



		public MainServer(IPEndPoint a_bindAddr_udp1,
						  IPEndPoint a_bindAddr_udp2,
						  IPEndPoint a_subServer_udp)
		{
			// must not null
			if (a_bindAddr_udp1 == null || a_bindAddr_udp2 == null || a_subServer_udp == null)
				throw new ArgumentNullException();

			m_subServerAddr_udp = a_subServer_udp;

			m_udp1 = CreateSocketIO(a_bindAddr_udp1, ProtocolType.Udp);
			m_udp2 = CreateSocketIO(a_bindAddr_udp2, ProtocolType.Udp);
			m_subServer_udp = CreateSocketIO(new IPEndPoint(IPAddress.Any, 0), ProtocolType.Udp);
		}



		SocketIo CreateSocketIO(IPEndPoint a_bindAddr, ProtocolType a_protocol)
		{
			SocketType sockType = SocketType.Unknown;
			if (a_protocol == ProtocolType.Tcp)
				sockType = SocketType.Stream;
			else if (a_protocol == ProtocolType.Udp)
				sockType = SocketType.Dgram;
			else
				throw new Exception("fail CreateSocketIO()");

			Socket sock = new Socket(AddressFamily.InterNetwork,
									 sockType,
									 a_protocol);
			sock.Bind(a_bindAddr);
			return new SocketIo(sock);
		}


		
		public void Start()
		{
			m_udp1.Start();
			m_udp2.Start();
			m_subServer_udp.Start();

			m_run = true;
			ParameterizedThreadStart threadRoutine = (object a_data) =>
			{
				var param = (Tuple<SocketIo, bool, string>)(a_data);
				SocketIo io = param.Item1;
				bool isFirstUdp = param.Item2;
				string name = param.Item3;

				while (m_run) {
					Message msg;
					IPEndPoint sender;
					if (io.WaitForRecv(Config.Timeout_Ms, out msg, out sender) == false)
						continue;

					++msg.m_contextSeq;
					msg.m_address = sender.Address.ToString();
					msg.m_port = sender.Port;

					Config.OnEventDelegate(
						"[" + name + "] Requested from " + sender.ToString() +
						", context=(" + msg.m_contextID + ":" + msg.m_contextSeq + ")");

					io.SendTo(msg, sender);
					if (isFirstUdp)
						m_subServer_udp.SendTo(msg, m_subServerAddr_udp);
				}
			};

			m_thread1 = new Thread(threadRoutine);
			m_thread2 = new Thread(threadRoutine);
			m_heartbeatThread = new Thread((object a_data) =>
			{
				int timeoutCount = 0;

				Message ping = new Message();
				int pingPongCtx = 0;
				ping.m_contextSeq = 1;
				ping.m_contextID = pingPongCtx;

				while (m_run) {
					int interval;
					if (timeoutCount == 0)
						interval = Config.Timeout_Ms;
					else {
						interval = Config.Retransmission_Interval_Ms;
						if (timeoutCount >= 5) {
							Config.OnErrorDelegate("SubServer와의 통신이 되지 않고 있습니다.");
							--timeoutCount;
						}
					}

					Thread.Sleep(interval);
					ping.m_pingTime = System.Environment.TickCount;
					m_subServer_udp.SendTo(ping, m_subServerAddr_udp);

					Message pong;
					IPEndPoint sender;
					if (m_subServer_udp.WaitForRecv(Config.Timeout_Ms, out pong, out sender) == false) {
						++timeoutCount;
						continue;
					}

					if (pong.m_contextID != pingPongCtx) {
						++timeoutCount;
						Config.OnErrorDelegate("잘못된 메시지를 수신 : " + pong.ToString());
						continue;
					}

					timeoutCount = 0;

					if (ping.AddressIsEmpty()) {
						ping.m_address = pong.m_address;
						ping.m_port = pong.m_port;
					}

					++ping.m_contextSeq;
				}
			});

			m_thread1.Start(new Tuple<SocketIo, bool, string>(m_udp1, true, "FirstUDP"));
			m_thread2.Start(new Tuple<SocketIo, bool, string>(m_udp2, false, "SecondUDP"));
			m_heartbeatThread.Start();
		}



		public void Stop()
		{
			m_run = false;
			if (m_thread1 != null) {
				m_thread1.Join();
				m_thread1 = null;
			}
			if (m_thread2 != null) {
				m_thread2.Join();
				m_thread2 = null;
			}
			if (m_heartbeatThread != null) {
				m_heartbeatThread.Join();
				m_heartbeatThread = null;
			}
		}
	}
}
