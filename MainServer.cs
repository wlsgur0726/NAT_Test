﻿using System;
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

			m_thread1 = new Thread((object a_data) =>
			{
				while (m_run) {
					Message msg;
					IPEndPoint sender;
					if (m_udp1.WaitForRecv(Config.Timeout_Ms, out msg, out sender) == false)
						continue;

					++msg.m_contextSeq;
					msg.m_address = sender.Address.ToString();
					msg.m_port = sender.Port;

					string ctxstr = " " + Message.ContextString(msg.m_contextID, msg.m_contextSeq);
					Config.OnEventDelegate("[UDP1] Requested from " + sender.ToString() + ctxstr);
					Config.OnEventDelegate("[UDP2] Response to " + sender.ToString() + ctxstr);
					m_udp2.SendTo(msg, sender);
					Config.OnEventDelegate("[UDP1] Response to " + sender.ToString() + ctxstr);
					m_udp1.SendTo(msg, sender);
					Config.OnEventDelegate("[UDP1] Pass to " + m_subServerAddr_udp.ToString() + ctxstr);
					m_subServer_udp.SendTo(msg, m_subServerAddr_udp);
				}
			});

			m_thread2 = new Thread((object a_data) =>
			{
				while (m_run) {
					Message msg;
					IPEndPoint sender;
					if (m_udp2.WaitForRecv(Config.Timeout_Ms, out msg, out sender) == false)
						continue;

					++msg.m_contextSeq;
					msg.m_address = sender.Address.ToString();
					msg.m_port = sender.Port;

					string ctxstr = " " + Message.ContextString(msg.m_contextID, msg.m_contextSeq);
					Config.OnEventDelegate("[UDP2] Requested from " + sender.ToString() + ctxstr);
					Config.OnEventDelegate("[UDP2] Response to " + sender.ToString() + ctxstr);
					m_udp2.SendTo(msg, sender);
				}
			});

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

			m_run = true;
			m_thread1.Start();
			m_thread2.Start();
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
