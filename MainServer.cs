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
				m_thread.Start(this);
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



		static bool SendResponse(Socket a_sock,
								 Worker a_worker,
								 IPEndPoint a_dest,
								 Message a_msg)
		{
			bool r = true;
			SocketPoller poller = a_worker.m_poller;
			switch (a_sock.ProtocolType) {
				case ProtocolType.Tcp:
					if (poller.IsRegstered(a_sock) == false) {
						if (poller.ConnectAndSend(a_sock, a_dest, a_msg) == false)
							r = false;
					}
					else {
						if (poller.Send(a_sock, a_msg, true) == false)
							r = false;
					}
					poller.Close(a_sock);
					break;

				case ProtocolType.Udp:
					if (poller.SendTo(a_sock, a_dest, a_msg, false) == false)
						r = false;
					break;

				default:
					Debug.Assert(false);
					break;
			}
			return r;
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
				bool isTimeout = ! worker.m_poller.WaitForMessage(Config.Timeout_Ms,
																  out msg,
																  out sock,
																  out sender);
				if (isTimeout)
					continue;

				Debug.Assert(protocol == sock.ProtocolType);

				++msg.m_contextSeq;
				msg.m_address = sender.Address.ToString();
				msg.m_port = sender.Port;

				Socket resSock1 = sock;
				Socket resSock2 = null;
				Socket passSock = null;
				Worker worker1 = worker;
				Worker worker2 = null;
				Worker passWorker = null;
				string portName1 = GetName(resSock1);
				string portName2 = null;
				string passPortName = null;
				IPEndPoint pass = null;

				switch (protocol) {
					case ProtocolType.Tcp:
						resSock2 = Function.CreateSocket(ProtocolType.Tcp, null, false);
						passSock = Function.CreateSocket(ProtocolType.Tcp, null, false);
						passWorker = worker2 = worker1;
						portName2 = GetName(resSock2);
						passPortName = GetName(passSock);
						pass = m_subServerAddr_tcp;
						break;

					case ProtocolType.Udp:
						resSock2 = m_secondUdp.m_socket;
						passSock = m_subServerUdp.m_socket;
						worker2 = m_secondUdp;
						passWorker = m_subServerUdp;
						portName2 = GetName(resSock2);
						passPortName = GetName(passSock);
						pass = m_subServerAddr_udp;
						break;

					default:
						Debug.Assert(false);
						break;
				}

				string ctxstr = " " + Message.ContextString(msg.m_contextID, msg.m_contextSeq);
				Config.OnEventDelegate(portName1 + "Requested from " + sender.ToString() + ctxstr);

				Config.OnEventDelegate(portName2 + "Response to " + sender.ToString() + ctxstr);
				msg.m_senderType = Message.SenderType.MainServer_SecondPort;
				if (SendResponse(resSock2, worker2, sender, msg) == false)
					Config.OnEventDelegate(portName2 + "Faild response" + ctxstr);

				Config.OnEventDelegate(portName1 + "Response to " + sender.ToString() + ctxstr);
				msg.m_senderType = Message.SenderType.MainServer_FirstPort;
				if (SendResponse(resSock1, worker1, sender, msg) == false)
					Config.OnEventDelegate(portName1 + "Faild response" + ctxstr);

				Config.OnEventDelegate(passPortName + "Pass to " + sender.ToString() + ctxstr);
				msg.m_senderType = Message.SenderType.SubServer;
				if (SendResponse(passSock, passWorker, pass, msg) == false)
					Config.OnEventDelegate(passPortName + "Faild pass" + ctxstr);
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
				bool isTimeout = ! worker.m_poller.WaitForMessage(Config.Timeout_Ms,
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

				string portName = GetName(sock);
				string ctxstr = " " + Message.ContextString(msg.m_contextID, msg.m_contextSeq);
				Config.OnEventDelegate(portName + "Requested from " + sender.ToString() + ctxstr);

				Config.OnEventDelegate(portName + "Response to " + sender.ToString() + ctxstr);
				if (SendResponse(resSock, worker, sender, msg) == false)
					Config.OnEventDelegate(portName + "Faild response" + ctxstr);

			}
		}



		class Heartbeat
		{
			int m_timeoutCount = 0;
			public SocketPoller m_poller = new SocketPoller();

			public void WaitForInterval()
			{
				int interval;
				if (m_timeoutCount == 0)
					interval = Config.Timeout_Ms;
				else {
					interval = Config.Retransmission_Interval_Ms;
					if (m_timeoutCount >= 5) {
						Config.OnErrorDelegate("SubServer와 통신이 되지 않고 있습니다.");
						--m_timeoutCount;
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

			Heartbeat heartbeat = new Heartbeat();

			Message ping = new Message();
			int pingPongCtx = 0;
			ping.m_contextSeq = 1;
			ping.m_contextID = pingPongCtx;
			
			while (m_run) {
				heartbeat.WaitForInterval();

				ping.m_pingTime = System.Environment.TickCount;

				bool succeedPing = false;
				switch (protocol) {
					case ProtocolType.Tcp:
						if (worker.m_socket.Connected)
							succeedPing = worker.m_poller.Send(worker.m_socket, ping, true);
						else
							succeedPing = worker.m_poller.ConnectAndSend(worker.m_socket, dest, ping);
						break;

					case ProtocolType.Udp:
						succeedPing = worker.m_poller.SendTo(worker.m_socket, dest, ping, true);
						break;

					default:
						Debug.Assert(false);
						break;
				}
				if (succeedPing == false) {
					heartbeat.Timeout();
					continue;
				}

				Message pong;
				Socket sock;
				IPEndPoint sender;
				bool isTimeout = ! worker.m_poller.WaitForMessage(Config.Timeout_Ms,
																  out pong,
																  out sock,
																  out sender);
				if (isTimeout) {
					heartbeat.Timeout();
					continue;
				}

				if (pong.m_contextID != pingPongCtx) {
					heartbeat.Timeout();
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
