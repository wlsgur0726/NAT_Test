using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NAT_Test
{
	public class MainServer
	{
		volatile bool m_run = false;

		Worker m_firstTcp = null;

		Worker m_secondTcp = null;

		Worker m_firstUdp = null;

		Worker m_secondUdp = null;

		Worker m_subServerTcp = null;

		Worker m_subServerUdp = null;

		IPEndPoint m_subServerAddr_tcp = null;

		IPEndPoint m_subServerAddr_udp = null;



		class Worker
		{
			public Socket m_socket = null;
			public IPEndPoint m_address = null;
			public SocketPoller m_poller = new SocketPoller();
			Thread m_thread = null;
			
			public Worker(ProtocolType a_protocol,
						  IPEndPoint a_bindAddress,
						  ParameterizedThreadStart a_work)
			{
				m_socket = Function.CreateSocket(a_protocol, a_bindAddress, false);
				m_address = (IPEndPoint)m_socket.LocalEndPoint;

				m_thread = new Thread(a_work);
			}

			public void Start()
			{
				m_thread.Start(this);
			}

			public void Stop()
			{
				m_poller.Stop();
				m_thread.Join();
			}
		}



		class ResponseParameter
		{
			public Socket m_sock;
			public Worker m_worker;
			public IPEndPoint m_dest;
			public Message m_msg;
			public Message.SenderType m_senderType;
			public string m_responseType;

			public ResponseParameter(Socket a_sock,
									 Worker a_worker,
									 IPEndPoint a_dest,
									 Message a_msg,
									 Message.SenderType a_senderType,
									 string a_responseType)
			{
				m_sock = a_sock;
				m_worker = a_worker;
				m_dest = a_dest;
				m_msg = a_msg;
				m_senderType = a_senderType;
				m_responseType = a_responseType;
			}
		}



		public MainServer(IPEndPoint a_bindAddr_tcp1,
						  IPEndPoint a_bindAddr_tcp2,
						  IPEndPoint a_subServer_tcp,
						  IPEndPoint a_bindAddr_udp1,
						  IPEndPoint a_bindAddr_udp2,
						  IPEndPoint a_subServer_udp)
		{
			bool nullArg = a_bindAddr_tcp1 == null
						|| a_bindAddr_tcp2 == null
						|| a_subServer_tcp == null
						|| a_bindAddr_udp1 == null
						|| a_bindAddr_udp2 == null
						|| a_subServer_udp == null;
			if (nullArg)
				throw new ArgumentNullException();

			m_firstTcp = new Worker(ProtocolType.Tcp, a_bindAddr_tcp1, FirstWorkerRoutine);
			m_firstUdp = new Worker(ProtocolType.Udp, a_bindAddr_udp1, FirstWorkerRoutine);

			m_secondTcp = new Worker(ProtocolType.Tcp, a_bindAddr_tcp2, SecondWorkerRoutine);
			m_secondUdp = new Worker(ProtocolType.Udp, a_bindAddr_udp2, SecondWorkerRoutine);

			m_subServerAddr_tcp = a_subServer_tcp;
			m_subServerAddr_udp = a_subServer_udp;

			m_subServerTcp = new Worker(ProtocolType.Tcp, null, HeartbeatRoutine);
			m_subServerUdp = new Worker(ProtocolType.Udp, null, HeartbeatRoutine);
		}



		public void Start()
		{
			m_run = true;
			m_firstTcp.Start();
			m_firstUdp.Start();
			m_secondTcp.Start();
			m_secondUdp.Start();
			m_subServerTcp.Start();
			m_subServerUdp.Start();
		}



		public void Stop()
		{
			m_run = false;
			m_firstTcp.Stop();
			m_firstUdp.Stop();
			m_secondTcp.Stop();
			m_secondUdp.Stop();
			m_subServerTcp.Stop();
			m_subServerUdp.Stop();
		}



		static string GetName(Socket a_sock)
		{
			string r;
			int port = ((IPEndPoint)a_sock.LocalEndPoint).Port;
			switch (a_sock.ProtocolType) {
				case ProtocolType.Tcp:
					r = "[TCP-" + port + "] ";
					break;
				case ProtocolType.Udp:
					r = "[UDP-" + port + "] ";
					break;
				default:
					throw new Exception("invalid protocol");
			}
			return r;
		}



		static void SendResponse(ResponseParameter a_param)
		{
			Thread t = new Thread((object a_param2) =>
			{
				ResponseParameter param = (ResponseParameter)a_param2;
				bool r = true;

				param.m_msg.m_senderType = param.m_senderType;
				string portname = GetName(param.m_sock);
				string ctxstr = Message.ContextString(param.m_msg);
				Config.OnEventDelegate(portname
					+ param.m_responseType + " to "
					+ param.m_dest.ToString() + ctxstr);

				SocketPoller poller = param.m_worker.m_poller;
				switch (param.m_sock.ProtocolType) {
					case ProtocolType.Tcp:
						if (poller.IsRegstered(param.m_sock) == false) {
							r = poller.ConnectAndSend(param.m_sock,
													  param.m_dest,
													  param.m_msg);
						}
						else {
							r = poller.Send(param.m_sock,
											param.m_msg,
											true);
						}
						break;

					case ProtocolType.Udp:
						r = poller.SendTo(param.m_sock,
										  param.m_dest,
										  param.m_msg, false);
						break;

					default:
						Debug.Assert(false);
						break;
				}

				if (r == false)
					Config.OnEventDelegate(portname + "Faild " + param.m_responseType + ctxstr);
			});

			t.IsBackground = true;
			t.Start(a_param);
		}



		void FirstWorkerRoutine(object a_worker)
		{
			Worker worker = (Worker)a_worker;
			ProtocolType protocol = worker.m_socket.ProtocolType;
			if (protocol == ProtocolType.Tcp) {
				worker.m_poller.Start_Acceptor(worker.m_socket);
			}
			else {
				Debug.Assert(protocol == ProtocolType.Udp);
				worker.m_poller.Start(worker.m_socket);
			}

			while (m_run) {
				Message msg;
				IPEndPoint sender;
				Socket sock;
				bool isTimeout = ! worker.m_poller.WaitForMessage(Config.Server_Poll_Timeout_Ms,
																  out msg,
																  out sock,
																  out sender);
				if (isTimeout)
					continue;

				Debug.Assert(protocol == sock.ProtocolType);

				++msg.m_contextSeq;
				msg.m_address = sender.Address.ToString();
				msg.m_port = sender.Port;

				ResponseParameter p1 = null;
				ResponseParameter p2 = null;
				ResponseParameter p3 = null;
				
				p1 = new ResponseParameter(sock,
										   worker,
										   sender,
										   new Message(msg),
										   Message.SenderType.MainServer_FirstPort,
										   "Response");
				switch (protocol) {
					case ProtocolType.Tcp:						
						p2 = new ResponseParameter(Function.CreateSocket(ProtocolType.Tcp, null, false),
												   worker,
												   sender,
												   new Message(msg),
												   Message.SenderType.MainServer_SecondPort,
												   "Response");
						p3 = new ResponseParameter(Function.CreateSocket(ProtocolType.Tcp, null, false),
												   worker,
												   m_subServerAddr_tcp,
												   new Message(msg),
												   Message.SenderType.SubServer,
												   "Pass");
						break;

					case ProtocolType.Udp:
						p2 = new ResponseParameter(m_secondUdp.m_socket,
												   m_secondUdp,
												   sender,
												   new Message(msg),
												   Message.SenderType.MainServer_SecondPort,
												   "Response");
						p3 = new ResponseParameter(m_subServerUdp.m_socket,
												   m_subServerUdp,
												   m_subServerAddr_udp,
												   new Message(msg),
												   Message.SenderType.SubServer,
												   "Pass");
						break;

					default:
						Debug.Assert(false);
						break;
				}

				string ctxstr = " " + Message.ContextString(msg);
				Config.OnEventDelegate(GetName(sock) + "Requested from " + sender.ToString() + ctxstr);

				SendResponse(p1);
				SendResponse(p2);
				SendResponse(p3);
			}
		}



		void SecondWorkerRoutine(object a_worker)
		{
			Worker worker = (Worker)a_worker;
			ProtocolType protocol = worker.m_socket.ProtocolType;
			if (protocol == ProtocolType.Tcp) {
				worker.m_poller.Start_Acceptor(worker.m_socket);
			}
			else {
				Debug.Assert(protocol == ProtocolType.Udp);
				worker.m_poller.Start(worker.m_socket);
			}

			while (m_run) {
				Message msg;
				Socket sock;
				IPEndPoint sender;
				bool isTimeout = ! worker.m_poller.WaitForMessage(Config.Server_Poll_Timeout_Ms,
																  out msg,
																  out sock,
																  out sender);
				if (isTimeout)
					continue;

				++msg.m_contextSeq;
				msg.m_senderType = Message.SenderType.MainServer_SecondPort;
				msg.m_address = sender.Address.ToString();
				msg.m_port = sender.Port;

				Socket resSock = null;
				switch (protocol) {
					case ProtocolType.Tcp:
						resSock = Function.CreateSocket(ProtocolType.Tcp, null, false);
						break;
					case ProtocolType.Udp:
						resSock = m_secondUdp.m_socket;
						break;
					default:
						Debug.Assert(false);
						break;
				}

				ResponseParameter param = new ResponseParameter(resSock,
																worker,
																sender,
																new Message(msg),
																Message.SenderType.Client_SecondPort,
																"Response");
				SendResponse(param);
			}
		}



		class Heartbeat
		{
			string m_name;
			int m_timeoutCount = 0;
			public SocketPoller m_poller = new SocketPoller();

			public Heartbeat(string a_name)
			{
				m_name = a_name;
			}

			public void WaitForInterval(string a_errComment)
			{
				int interval;
				if (m_timeoutCount == 0) {
					interval = Math.Max(Config.Response_Timeout_Ms,
										Config.Retransmission_Interval_Ms);
				}
				else {
					interval = Config.Retransmission_Interval_Ms;
					const int WarningCount = 5;
					if (m_timeoutCount >= WarningCount) {
						Config.OnErrorDelegate(m_name + "Cannot communicate with SubServer. " + a_errComment);
						m_timeoutCount = WarningCount - 1;
					}
				}
				Thread.Sleep(interval);
			}

			public void Timeout()
			{
				++m_timeoutCount;
			}

			public void Reset()
			{
				m_timeoutCount = 0;
			}
		}


		void HeartbeatRoutine(object a_worker)
		{
			Worker worker = (Worker)a_worker;
			ProtocolType protocol = worker.m_socket.ProtocolType;
			IPEndPoint dest;
			if (protocol == ProtocolType.Udp) {
				dest = m_subServerAddr_udp;
				worker.m_poller.Start(worker.m_socket);
			}
			else {
				Debug.Assert(protocol == ProtocolType.Tcp);
				dest = m_subServerAddr_tcp;
			}

			Heartbeat heartbeat = new Heartbeat(GetName(worker.m_socket));

			Message ping = new Message();
			int pingPongCtx = 0;
			ping.m_contextSeq = 1;
			ping.m_contextID = pingPongCtx;

			string errComment = "";
			while (m_run) {
				heartbeat.WaitForInterval(errComment);

				ping.m_pingTime = System.Environment.TickCount;

				bool succeedPing = false;
				switch (protocol) {
					case ProtocolType.Tcp:
						if (worker.m_poller.IsRegstered(worker.m_socket))
							succeedPing = worker.m_poller.Send(worker.m_socket, ping, true);
						else {
							if (ping.m_contextSeq > 1) {
								ping.m_contextSeq = 1;
								worker.m_socket.Close();
								worker.m_socket = Function.CreateSocket(ProtocolType.Tcp,
																		null,
																		false);
							}
							succeedPing = worker.m_poller.ConnectAndSend(worker.m_socket, dest, ping);
						}
						break;

					case ProtocolType.Udp:
						succeedPing = worker.m_poller.SendTo(worker.m_socket, dest, ping, true);
						break;

					default:
						Debug.Assert(false);
						break;
				}
				if (succeedPing == false) {
					errComment = "failed request.";
					heartbeat.Timeout();
					continue;
				}

				Message pong;
				Socket sock;
				IPEndPoint sender;
				bool isTimeout = ! worker.m_poller.WaitForMessage(Config.Response_Timeout_Ms,
																  out pong,
																  out sock,
																  out sender);
				if (isTimeout) {
					errComment = "response timeout.";
					heartbeat.Timeout();
					continue;
				}

				if (pong.m_contextID != pingPongCtx) {
					heartbeat.Timeout();
					errComment = "wrong message.";
					Config.OnErrorDelegate("잘못된 메시지를 수신 : " + pong.ToString());
					continue;
				}

				heartbeat.Reset();

				if (ping.AddressIsEmpty()) {
					ping.m_address = pong.m_address;
					ping.m_port = pong.m_port;
				}

				++ping.m_contextSeq;
			}
		}
	}
}
