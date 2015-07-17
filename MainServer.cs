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

		SocketIo m_subUdp = null;

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

			// Address_and_Port_Dependent 여부를 가려내려면 MainServer는 동일한 IP에 서로 다른 두 Port를 사용해야 한다.
			if (a_bindAddr_udp1.Address.Equals(a_bindAddr_udp2.Address)  == false) {
				throw new ArgumentException("a_bindAddr_udp1와 a_bindAddr_udp2의 IP가 다릅니다."
											+ " a_bindAddr_udp1:" + a_bindAddr_udp1.Address.ToString()
											+ " a_bindAddr_udp2:" + a_bindAddr_udp2.Address.ToString());
			}
			if (a_bindAddr_udp1.Port == a_bindAddr_udp2.Port) {
				throw new ArgumentException("a_bindAddr_udp1와 a_bindAddr_udp2의 Port가 같습니다."
											+ " a_bindAddr_udp1:" + a_bindAddr_udp1.Port
											+ " a_bindAddr_udp2:" + a_bindAddr_udp2.Port);
			}

			// Address_Dependent 여부를 가려내려면 MainServer와 SubServer의 IP가 서로 달라야 한다.
			if (a_subServer_udp.Address.Equals(a_bindAddr_udp1.Address)) {
				throw new ArgumentException("mainServer와 subServer의 IP가 같습니다."
											+ " a_bindAddr_udp:" + a_bindAddr_udp1.Address.ToString()
											+ " a_subServer_udp:" + a_subServer_udp.Address.ToString());
			}

			m_subServerAddr_udp = a_subServer_udp;

			m_udp1 = CreateSocketIO(a_bindAddr_udp1, ProtocolType.Udp);
			m_udp2 = CreateSocketIO(a_bindAddr_udp2, ProtocolType.Udp);
			m_subUdp = CreateSocketIO(new IPEndPoint(IPAddress.Any, 0), ProtocolType.Udp);
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
			m_subUdp.Start();

			m_run = true;
			ParameterizedThreadStart threadRoutine = (object a_data) =>
			{
				SocketIo io = (SocketIo)a_data;

				while (m_run) {
					Message msg;
					IPEndPoint sender;
					if (io.WaitForRecv(Config.Timeout_Ms, out msg, out sender) == false)
						continue;

					++msg.m_contextSeq;
					msg.m_address = sender.Address.ToString();
					msg.m_port = sender.Port;

					io.SendTo(msg, sender);
					m_subUdp.SendTo(msg, sender);
				}
			};

			m_thread1 = new Thread(threadRoutine);
			m_thread2 = new Thread(threadRoutine);
			m_heartbeatThread = new Thread((object a_data) =>
			{
				int timeoutCount = 0;

				Message ping = new Message();
				int pingPongCtx = Config.Random.Next(int.MaxValue);
				ping.m_contextID = pingPongCtx;

				while (m_run) {
					if (timeoutCount >= 5) {
						Config.OnErrorDelegate("SubServer와의 통신이 되지 않고 있습니다.");
						--timeoutCount;
					}
					m_subUdp.SendTo(ping, m_subServerAddr_udp);

					Message pong;
					IPEndPoint sender;
					if (m_subUdp.WaitForRecv(Config.Timeout_Ms, out pong, out sender) == false) {
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

			m_thread1.Start(m_udp1);
			m_thread2.Start(m_udp2);
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
