using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
		public static int Timeout_Ms = 5 * 1000;

		// 재전송 주기 (UDP Drop 대비)
		public static int Retransmission_Interval_Ms = 1000;

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
		public int m_contextID = -1;
		public int m_contextSeq = -1;
		public int m_pingTime = -1;
		public string m_address = "";
		public int m_port = -1;
		public string m_otherMessage = "";

		public bool AddressIsEmpty()
		{
			return m_port == -1 || m_address.Equals("");
		}

		public static string ContextString(int a_contextID, int a_contextSeq)
		{
			return "[" + string.Format("{0:X}", a_contextID) + ":" + a_contextSeq + "]";
		}
	}



	class SocketIo
	{
		struct RecvedMessage
		{
			public IPEndPoint m_sender;
			public Message m_message;
		}

		static readonly IPEndPoint AnyAddr = new IPEndPoint(IPAddress.Any, 0);
		Socket m_socket = null;
		byte[] m_recvBuffer = new byte[Config.Message_Max_Length];
		Queue<RecvedMessage> m_recvedMessageQueue = new Queue<RecvedMessage>();
		Semaphore m_recvedEvent = new Semaphore(0, int.MaxValue);
		volatile bool m_run = false;


		public SocketIo(Socket a_sock)
		{
			m_socket = a_sock;
		}


		public void Start()
		{
			m_run = true;
			if (m_socket.ProtocolType == ProtocolType.Tcp) {
				// ...
			}
			else {
				Try_ReceiveFrom();
			}
		}


		public void Stop()
		{
			Debug.Assert(m_run);

			m_run = false;
			m_socket.Close();
		}


		void Try_ReceiveFrom()
		{
			Debug.Assert(m_socket.ProtocolType == ProtocolType.Udp);
			Debug.Assert(m_socket.SocketType == SocketType.Dgram);

			try {
				EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
				m_socket.BeginReceiveFrom(m_recvBuffer,
										  0,
										  m_recvBuffer.Length,
										  SocketFlags.None,
										  ref sender,
										  ReceiveCompletion,
										  this);
			}
			catch (Exception e) {
				Config.OnErrorDelegate(e.ToString());
			}
		}


		void ReceiveCompletion(IAsyncResult a_result)
		{
			Debug.Assert(this.Equals((SocketIo)a_result.AsyncState));
			try {
				EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
				int recvedBytes = m_socket.EndReceiveFrom(a_result, ref sender);
				if (recvedBytes <= 0) {
					if (m_run)
						Config.OnErrorDelegate("ReceiveCompletion Error : recvedBytes=" + recvedBytes);
				}
				else {
					Int16 len = BitConverter.ToInt16(m_recvBuffer, 0);
					if (len + 2 != recvedBytes) {
						Config.OnErrorDelegate("invalid payload : length=" + len);
					}
					else {
						string jsonMsg = Encoding.UTF8.GetString(m_recvBuffer, 2, len);
						Message msg = JsonConvert.DeserializeObject<Message>(jsonMsg);
						lock (m_recvedMessageQueue) {
							m_recvedMessageQueue.Enqueue(
								new RecvedMessage {
									m_sender = (IPEndPoint)sender,
									m_message = msg
								}
							);
						}
						m_recvedEvent.Release();
					}
				}
			}
			catch (Exception e) {
				if (m_run)
					Config.OnErrorDelegate(e.ToString());
			}
			finally {
				if (m_run)
					Try_ReceiveFrom();
			}
		}


		public bool WaitForRecv(int a_timeoutMs,
								out Message a_message,
								out IPEndPoint a_sender)
		{
			a_message = null;
			a_sender = null;

			if (m_recvedEvent.WaitOne(a_timeoutMs) == false) {
				return false;
			}

			lock (m_recvedMessageQueue) {
				RecvedMessage msg = m_recvedMessageQueue.Dequeue();
				a_message = msg.m_message;
				a_sender = msg.m_sender;
			}
			return true;
		}


		public void SendTo(Message a_message, IPEndPoint a_dest)
		{
			if (a_message.m_pingTime == -1)
				a_message.m_pingTime = System.Environment.TickCount;

			string jsonMessage = JsonConvert.SerializeObject(a_message, Config.JsonFormatting);
			byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
			byte[] len = BitConverter.GetBytes((Int16)data.Length);
			byte[] packet = new byte[data.Length + len.Length];
			len.CopyTo(packet, 0);
			data.CopyTo(packet, len.Length);

			try {
				m_socket.BeginSendTo(packet,
									 0,
									 packet.Length,
									 SocketFlags.None,
									 a_dest,
									 SendCompletion,
									 this);
			}
			catch (Exception e) {
				if (m_run)
					Config.OnErrorDelegate(e.ToString());
			}
		}


		void SendCompletion(IAsyncResult a_result)
		{
			Debug.Assert(this.Equals((SocketIo)a_result.AsyncState));
			try {
				int transBytes = m_socket.EndSendTo(a_result);
				if (transBytes <= 0)
					Config.OnErrorDelegate("SendCompletion Error : transBytes=" + transBytes);
			}
			catch (Exception e) {
				if (m_run)
					Config.OnErrorDelegate(e.ToString());
			}
		}
	}
}