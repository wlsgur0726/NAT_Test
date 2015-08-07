using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NAT_Test
{
	public class SubServer
	{
		volatile bool m_run = false;

		IPEndPoint m_tcp = null;

		IPEndPoint m_udp = null;

		Thread m_thread = null;

		SocketPoller m_poller = null;



		public SubServer(IPEndPoint a_bindAddr_tcp,
						 IPEndPoint a_bindAddr_udp)
		{
			if (a_bindAddr_tcp==null || a_bindAddr_udp==null)
				throw new ArgumentNullException();

			m_tcp = a_bindAddr_tcp;
			m_udp = a_bindAddr_udp;
		}



		public void Start()
		{
			Socket tcpSock = new Socket(AddressFamily.InterNetwork,
										SocketType.Stream,
										ProtocolType.Tcp);
			Socket udpSock = new Socket(AddressFamily.InterNetwork,
										SocketType.Dgram,
										ProtocolType.Udp);
			tcpSock.Bind(m_tcp);
			udpSock.Bind(m_udp);

			m_poller = new SocketPoller();
			m_poller.Start_Acceptor(tcpSock);
			m_poller.Start(udpSock);

			m_run = true;
			m_thread = new Thread((object a_data) =>
			{
				while (m_run) {
					Message msg;
					Socket sock;
					IPEndPoint sender;
					bool timeout = ! m_poller.WaitForMessage(Config.Timeout_Ms,
															 out msg,
															 out sock,
															 out sender);
					if (timeout)
						continue;

					string protocolName;
					if (sock.ProtocolType == ProtocolType.Tcp) {
						Debug.Assert(sock.Equals(tcpSock));
						protocolName = "[TCP] ";
					}
					else {
						Debug.Assert(sock.Equals(udpSock));
						protocolName = "[UDP] ";
					}

					bool isHeartbeatMessage = msg.m_contextID == 0;
					IPEndPoint dst;
					if (msg.AddressIsEmpty() || isHeartbeatMessage) {
						// Client로부터 직접 요청을 받은 경우
						dst = sender;
						msg.m_address = sender.Address.ToString();
						msg.m_port = sender.Port;
					}
					else {
						// Client의 요청을 MainServer로부터 전달받은 경우
						dst = new IPEndPoint(IPAddress.Parse(msg.m_address), msg.m_port);
						msg.m_address = dst.Address.ToString();
						msg.m_port = dst.Port;
					}

					++msg.m_contextSeq;
					msg.m_senderType = Message.SenderType.SubServer;

					if (isHeartbeatMessage) {
						// Heartbeat 메시지인 경우
						if (sock.ProtocolType == ProtocolType.Udp)
							m_poller.SendTo(sock, dst, msg, false);
						else
							m_poller.Send(sock, msg, false);
					}
					else {
						// Request 메시지인 경우
						string ctxstr = " " + Message.ContextString(msg.m_contextID, msg.m_contextSeq);
						Config.OnEventDelegate(protocolName + "Requested from " + sender.ToString() + ctxstr);
						Config.OnEventDelegate(protocolName + "Response to " + dst.ToString() + ctxstr);

						if (sock.ProtocolType == ProtocolType.Udp)
							m_poller.SendTo(sock, dst, msg, false);
						else {
							Socket newSocket = new Socket(AddressFamily.InterNetwork,
														  SocketType.Stream,
														  ProtocolType.Tcp);
							if (m_poller.ConnectAndSend(newSocket, dst, msg) == false)
								Config.OnEventDelegate(protocolName + "Failed response" + ctxstr);
							m_poller.Close(sock);
							m_poller.Close(newSocket);
						}
					}
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

			if (m_poller != null)
				m_poller.Stop();
		}
	}
}
