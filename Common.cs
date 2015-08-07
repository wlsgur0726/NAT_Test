using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NAT_Test
{
	public class Config
	{
		public delegate void OnEvent(string a_eventMessage);

		public static int Message_Max_Length = 8 * 1024;

		// 응답을 기다리는 시간
		public static int Response_Timeout_Ms = 30 * 1000;

		// 재전송 주기 (UDP Drop 대비 => 단순히 무조건 재전송)
		public static int Retransmission_Interval_Ms = Response_Timeout_Ms / 5;

		// 서버의 폴링 타임아웃 (서버 종료에 걸리는 시간과 관련)
		public static int Server_Poll_Timeout_Ms = 1000;

		public static Formatting JsonFormatting = Formatting.None;

		public static Random Random = new Random();

		public static bool PrintEvent = true;

		public static OnEvent OnEventDelegate = (string a_eventMessage) =>
		{
			if (PrintEvent)
				System.Console.WriteLine(a_eventMessage);
		};

		public static OnEvent OnErrorDelegate = (string a_errorMessage) =>
		{
			System.Console.Error.WriteLine(a_errorMessage);
		};
	}


	class Message
	{
		public enum SenderType
		{
			Do_Not_Care,
			Client_FirstPort,
			Client_SecondPort,
			MainServer_FirstPort,
			MainServer_SecondPort,
			SubServer
		}

		public int m_contextID = -1;
		public int m_contextSeq = -1;
		public int m_pingTime = -1;
		public string m_address = "";
		public int m_port = -1;
		public SenderType m_senderType = SenderType.Do_Not_Care;
		public string m_otherMessage = "";


		public Message()
		{
		}

		public Message(Message a_src)
		{
			m_contextID = a_src.m_contextID;
			m_contextSeq = a_src.m_contextSeq;
			m_pingTime = a_src.m_pingTime;
			m_address = a_src.m_address;
			m_port = a_src.m_port;
			m_senderType = a_src.m_senderType;
			m_otherMessage = a_src.m_otherMessage;
		}

		public bool AddressIsEmpty()
		{
			return m_port == -1 || m_address.Equals("");
		}

		public static string ContextString(Message a_msg)
		{
			return ContextString(a_msg.m_contextID, a_msg.m_contextSeq);
		}

		public static string ContextString(int a_contextID,
										   int a_contextSeq)
		{
			return "[" + string.Format("{0:X}", a_contextID) + ":" + a_contextSeq + "]";
		}
	}


	class SocketPoller
	{
		class SocketContext
		{
			public Socket m_socket;
			public byte[] m_recvBuffer = new byte[Config.Message_Max_Length];
			public int m_offset = 0;
			public int m_payloadSize = 0;
			public volatile bool m_closed = false;

			public SocketContext(Socket a_socket)
			{
				m_socket = a_socket;
			}

			public void BeginReceive(AsyncCallback a_callback)
			{
				Debug.Assert(m_socket.ProtocolType == ProtocolType.Tcp);
				Debug.Assert(m_socket.SocketType == SocketType.Stream);
				Debug.Assert(m_offset >= 0);

				m_socket.BeginReceive(m_recvBuffer,
									  m_offset,
									  m_recvBuffer.Length - m_offset,
									  SocketFlags.None,
									  a_callback,
									  this);
			}

			public void BeginReceiveFrom(AsyncCallback a_callback)
			{
				Debug.Assert(m_socket.ProtocolType == ProtocolType.Udp);
				Debug.Assert(m_socket.SocketType == SocketType.Dgram);

				EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
				m_socket.BeginReceiveFrom(m_recvBuffer,
										  0,
										  m_recvBuffer.Length,
										  SocketFlags.None,
										  ref sender,
										  a_callback,
										  this);
			}
		}


		struct RecvedMessage
		{
			public SocketContext m_sockCtx;
			public IPEndPoint m_sender;
			public Message m_message;
		}


		Dictionary<Socket, SocketContext> m_sockets = new Dictionary<Socket, SocketContext>();

		Queue<RecvedMessage> m_recvedMessageQueue = new Queue<RecvedMessage>();
		Semaphore m_recvedEvent = new Semaphore(0, int.MaxValue);

		Queue<Socket> m_acceptedSocketQueue = new Queue<Socket>();
		Semaphore m_acceptedEvent = new Semaphore(0, int.MaxValue);


		public void Start_Acceptor(Socket a_sock)
		{
			Debug.Assert(a_sock != null);
			a_sock.Listen(int.MaxValue);
			SocketContext sockCtx = new SocketContext(a_sock);
			lock (m_sockets) {
				m_sockets.Add(a_sock, sockCtx);
			}
			Try_Accept(sockCtx);
		}


		public void Start(Socket a_sock)
		{
			Debug.Assert(a_sock != null);
			SocketContext sockCtx = new SocketContext(a_sock);
			lock (m_sockets) {
				m_sockets.Add(a_sock, sockCtx);
			}

			if (a_sock.ProtocolType == ProtocolType.Tcp)
				Try_Receive(sockCtx);
			else
				Try_ReceiveFrom(sockCtx);
		}


		public void Stop()
		{
			lock (m_sockets) {
				foreach (var sock in m_sockets) {
					Debug.Assert(sock.Key.Equals(sock.Value.m_socket));
					sock.Value.m_closed = true;
					sock.Key.Close();
				}
				m_sockets.Clear();
			}

			lock(m_acceptedSocketQueue) {
				m_acceptedSocketQueue.Clear();
			}
		}


		void Try_Accept(SocketContext a_sockCtx)
		{
			try {
				a_sockCtx.m_socket.BeginAccept(AcceptCompletion, a_sockCtx);
			}
			catch (Exception e) {
				if (a_sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
		}


		void AcceptCompletion(IAsyncResult a_result)
		{
			SocketContext sockCtx = (SocketContext)a_result.AsyncState;
			try {
				Socket newSocket = sockCtx.m_socket.EndAccept(a_result);
				lock (m_acceptedSocketQueue) {
					m_acceptedSocketQueue.Enqueue(newSocket);
				}
				m_acceptedEvent.Release();

				Start(newSocket);
			}
			catch (Exception e) {
				if (sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
			finally {
				if (sockCtx.m_closed == false)
					Try_Accept(sockCtx);
			}
		}


		void Try_Receive(SocketContext a_sockCtx)
		{
			try {
				a_sockCtx.BeginReceive(ReceiveCompletion);
			}
			catch (Exception e) {
				if (a_sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
		}


		void ReceiveCompletion(IAsyncResult a_result)
		{
			SocketContext sockCtx = (SocketContext)a_result.AsyncState;
			try {
				int recvedBytes = sockCtx.m_socket.EndReceive(a_result);
				if (recvedBytes < 0) {
					if (sockCtx.m_closed == false)
						Config.OnErrorDelegate("ReceiveCompletion Error : recvedBytes=" + recvedBytes);
					Close(sockCtx.m_socket);
				}
				else if (recvedBytes == 0) {
					Close(sockCtx.m_socket);
				}
				else {
					while(true) {
						sockCtx.m_offset += recvedBytes;
						if (sockCtx.m_payloadSize == 0) {
							if (sockCtx.m_offset < 2)
								break;

							sockCtx.m_payloadSize = BitConverter.ToInt16(sockCtx.m_recvBuffer, 0);
							if (sockCtx.m_payloadSize >= sockCtx.m_recvBuffer.Length || sockCtx.m_payloadSize < 0) {
								Config.OnErrorDelegate("invalid payload : length=" + sockCtx.m_payloadSize);
								break;
							}
						}
						else {
							Debug.Assert(sockCtx.m_payloadSize > 0);
						}

						int msgLength = sockCtx.m_payloadSize + 2;
						int remnant = sockCtx.m_offset - msgLength;
						if (remnant >= 0) {
							// 하나의 메시지를 모두 수신 완료
							EnqueueMessage(sockCtx,
										   (IPEndPoint)sockCtx.m_socket.RemoteEndPoint,
										   2,
										   sockCtx.m_payloadSize);

							sockCtx.m_offset = 0;
							sockCtx.m_payloadSize = 0;
							recvedBytes = remnant;
							if (remnant > 0) {
								// Shift
								for (int i=0; i<remnant; ++i)
									sockCtx.m_recvBuffer[i] = sockCtx.m_recvBuffer[i + msgLength];
								continue;
							}
						}
						break;
					}
				}
			}
			catch (Exception e) {
				if (sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
			finally {
				if (sockCtx.m_closed == false)
					Try_Receive(sockCtx);
			}
		}


		void Try_ReceiveFrom(SocketContext a_sockCtx)
		{
			try {
				a_sockCtx.BeginReceiveFrom(ReceiveFromCompletion);
			}
			catch (Exception e) {
				if (a_sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
		}


		void ReceiveFromCompletion(IAsyncResult a_result)
		{
			SocketContext sockCtx = (SocketContext)a_result.AsyncState;
			try {
				EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
				int recvedBytes = sockCtx.m_socket.EndReceiveFrom(a_result, ref sender);
				if (recvedBytes <= 0) {
					if (sockCtx.m_closed == false)
						Config.OnErrorDelegate("ReceiveFromCompletion Error : recvedBytes=" + recvedBytes);
				}
				else {
					Int16 len = BitConverter.ToInt16(sockCtx.m_recvBuffer, 0);
					if (len + 2 != recvedBytes) {
						Config.OnErrorDelegate("invalid payload : length=" + len);
					}
					else {
						EnqueueMessage(sockCtx,
									   (IPEndPoint)sender,
									   2,
									   len);
					}
				}
			}
			catch (Exception e) {
				if (sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
			finally {
				if (sockCtx.m_closed == false)
					Try_ReceiveFrom(sockCtx);
			}
		}


		void EnqueueMessage(SocketContext a_sockCtx,
							IPEndPoint a_sender,
							int a_startOffset,
							int a_length)
		{
			string jsonMsg = Encoding.UTF8.GetString(a_sockCtx.m_recvBuffer,
													 a_startOffset,
													 a_length);
			Message msg = JsonConvert.DeserializeObject<Message>(jsonMsg);
			lock (m_recvedMessageQueue) {
				m_recvedMessageQueue.Enqueue(
					new RecvedMessage {
						m_sockCtx = a_sockCtx,
						m_sender = a_sender,
						m_message = msg
					}
				);
			}
			m_recvedEvent.Release();
		}


		public bool WaitForAccept(int a_timeoutMs,
								  out Socket a_newSocket)
		{
			a_newSocket = null;

			if (m_acceptedEvent.WaitOne(a_timeoutMs) == false) {
				return false;
			}

			lock(m_acceptedSocketQueue) {
				Debug.Assert(m_acceptedSocketQueue.Count > 0);
				Socket sock = m_acceptedSocketQueue.Dequeue();
				a_newSocket = sock;
			}
			return true;
		}


		public bool WaitForMessage(int a_timeoutMs,
								   out Message a_message,
								   out Socket a_socket,
								   out IPEndPoint a_sender)
		{
			a_message = null;
			a_socket = null;
			a_sender = null;

			if (m_recvedEvent.WaitOne(a_timeoutMs) == false) {
				return false;
			}

			lock (m_recvedMessageQueue) {
				Debug.Assert(m_recvedMessageQueue.Count > 0);
				RecvedMessage msg = m_recvedMessageQueue.Dequeue();
				a_message = msg.m_message;
				a_socket = msg.m_sockCtx.m_socket;
				a_sender = msg.m_sender;
			}
			return true;
		}


		public bool ConnectAndSend(Socket a_sock,
								   IPEndPoint a_dest,
								   Message a_message)
		{
			try {
				a_sock.SendTimeout = Config.Response_Timeout_Ms;
				a_sock.Connect(a_dest);
				Start(a_sock);
				int transBytes = a_sock.Send(CreatePacket(a_message));
				if (transBytes <= 0) {
					Config.OnErrorDelegate("Send Error : transBytes=" + transBytes);
					return false;
				}
			}
			catch (Exception e) {
				Config.OnErrorDelegate(e.ToString() + "\n");
				return false;
			}
			return true;
		}


		public bool Send(Socket a_sock,
						 Message a_message,
						 bool a_blocking)
		{
			SocketContext sockCtx = null;
			lock (m_sockets) {
				if (m_sockets.TryGetValue(a_sock, out sockCtx)) {
					if (sockCtx.m_closed)
						return false;
				}
				else
					return false;
			}
			Debug.Assert(sockCtx != null);

			byte[] packet = CreatePacket(a_message);

			try {
				if (a_blocking) {
					int transBytes = a_sock.Send(packet);
					if (transBytes <= 0) {
						Config.OnErrorDelegate("Send Error : transBytes=" + transBytes);
						return false;
					}
				}
				else {
					a_sock.BeginSend(packet,
									 0,
									 packet.Length,
									 SocketFlags.None,
									 SendCompletion,
									 sockCtx);
				}
			}
			catch (Exception e) {
				if (sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
				return false;
			}
			return true;
		}


		public bool SendTo(Socket a_sock,
						   IPEndPoint a_dest,
						   Message a_message,
						   bool a_blocking)
		{
			if (a_sock.ProtocolType==ProtocolType.Tcp && a_dest==null) {
				return Send(a_sock, a_message, a_blocking);
			}

			SocketContext sockCtx = null;
			lock (m_sockets) {
				if (m_sockets.TryGetValue(a_sock, out sockCtx)) {
					if (sockCtx.m_closed)
						return false;
				}
				else
					return false;
			}
			Debug.Assert(sockCtx != null);

			byte[] packet = CreatePacket(a_message);

			try {
				if (a_blocking) {
					int transBytes = a_sock.SendTo(packet, a_dest);
					if (transBytes <= 0) {
						Config.OnErrorDelegate("SendTo Error : transBytes=" + transBytes);
						return false;
					}
				}
				else {
					a_sock.BeginSendTo(packet,
									   0,
									   packet.Length,
									   SocketFlags.None,
									   a_dest,
									   SendCompletion,
									   sockCtx);
				}
			}
			catch (Exception e) {
				if (sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
				return false;
			}
			return true;
		}


		void SendCompletion(IAsyncResult a_result)
		{
			SocketContext sockCtx = (SocketContext)a_result.AsyncState;
			try {
				int transBytes = sockCtx.m_socket.EndSendTo(a_result);
				if (transBytes <= 0) {
					if (sockCtx.m_closed == false)
						Config.OnErrorDelegate("SendCompletion Error : transBytes=" + transBytes);
				}
			}
			catch (Exception e) {
				if (sockCtx.m_closed == false)
					Config.OnErrorDelegate(e.ToString() + "\n");
			}
		}


		public bool IsRegstered(Socket a_sock)
		{
			lock (m_sockets) {
				return m_sockets.ContainsKey(a_sock);
			}
		}


		public void Close(Socket a_sock)
		{
			SocketContext sockCtx = null;

			lock (m_sockets) {
				if (m_sockets.TryGetValue(a_sock, out sockCtx)) {
					Debug.Assert(sockCtx.m_closed == false);
					sockCtx.m_closed = true;
					m_sockets.Remove(a_sock);
				}
			}

			if (sockCtx != null) {
				a_sock.Close();
			}
		}


		static byte[] CreatePacket(Message a_message)
		{
			if (a_message.m_pingTime == -1)
				a_message.m_pingTime = System.Environment.TickCount;

			string jsonMessage = JsonConvert.SerializeObject(a_message, Config.JsonFormatting);
			byte[] payload = Encoding.UTF8.GetBytes(jsonMessage);
			byte[] len = BitConverter.GetBytes((Int16)payload.Length);
			byte[] packet = new byte[payload.Length + len.Length];
			len.CopyTo(packet, 0);
			payload.CopyTo(packet, len.Length);

			return packet;
		}
	}



	class Function
	{
		public static Socket CreateSocket(ProtocolType a_protocol,
										  IPEndPoint a_bindAddr,
										  bool a_reusable)
		{
			SocketType sockType = SocketType.Unknown;
			if (a_protocol == ProtocolType.Tcp)
				sockType = SocketType.Stream;
			else if (a_protocol == ProtocolType.Udp)
				sockType = SocketType.Dgram;
			else {
				throw new Exception("fail CreateSocket()");
			}

			Socket sock = new Socket(AddressFamily.InterNetwork,
									 sockType,
									 a_protocol);

			if (a_reusable) {
				sock.ExclusiveAddressUse = false;
				sock.SetSocketOption(SocketOptionLevel.Socket,
									 SocketOptionName.ReuseAddress,
									 true);
			}

			if (a_bindAddr == null)
				a_bindAddr = new IPEndPoint(IPAddress.Any, 0);
			sock.Bind(a_bindAddr);
			return sock;
		}



		public static Socket CreateListenr(IPEndPoint a_bindAddr,
										   SocketPoller a_poller,
										   bool a_reusable)
		{
			Debug.Assert(a_bindAddr.Port > 0);
			Socket listener = CreateSocket(ProtocolType.Tcp, a_bindAddr, a_reusable);
			a_poller.Start_Acceptor(listener);

			return listener;
		}
	}
}