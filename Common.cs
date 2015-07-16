using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NAT_Test
{
	class Config
	{
		public static readonly int Message_Max_Length = 8 * 1024;
		public static readonly int Timeout_Ms = 5 * 1000;
		public static readonly int Retransmission_Interval_Ms = 1000;
		public static readonly Formatting JsonFormatting = Formatting.None;
	}



	class Message
	{
		public static readonly string Type_Request = "Request";
		public static readonly string Type_Response = "Response";

		public string m_contextID = "";
		public int m_contextSeq = -1;
		public int m_tick = Environment.TickCount;
		public string m_type = "";
		public string m_address = "";
		public int m_port = -1;
		public string m_otherMessage = "";

		public bool IsValid()
		{
			return m_contextSeq > 0
				&& m_type != "";
		}

		public static Message NewRequest()
		{
			Message msg = new Message();
			msg.m_contextSeq = 1;
			msg.m_type = Type_Request;
			return msg;
		}

		public static Message NewResponse(Message a_request, IPEndPoint a_publicAddress)
		{
			Message msg = new Message();
			msg.m_contextSeq = a_request.m_contextSeq + 1;
			msg.m_type = Type_Response;
			msg.m_address = a_publicAddress.Address.ToString();
			msg.m_port = a_publicAddress.Port;
			return msg;
		}
	}



	class SocketIo
	{
		struct RecvedMessage
		{
			public IPEndPoint m_sender;
			public Message m_message;
		}

		Socket m_socket = null;
		SocketAsyncEventArgs m_recvArgs = new SocketAsyncEventArgs();
		Queue<RecvedMessage> m_recvedMessageQueue = new Queue<RecvedMessage>();
		Semaphore m_recvedEvent = new Semaphore(0, int.MaxValue);

		public SocketIo(Socket a_sock)
		{
			m_socket = a_sock;
		}

		public void Start()
		{
			byte[] recvBuf = new byte[Config.Message_Max_Length];
			m_recvArgs.SetBuffer(recvBuf, 0, recvBuf.Length);

			if (m_socket.ProtocolType == ProtocolType.Tcp) {
			}
			else {
				m_recvArgs.Completed += (object a_sender, SocketAsyncEventArgs a_evCtx) =>
				{
					if (a_evCtx.BytesTransferred < 0) {
						m_socket.Close();
						m_socket.Dispose();
						return;
					}

					a_evCtx.SocketError.ToString();
					Int16 len = BitConverter.ToInt16(a_evCtx.Buffer, 0);
					if (len + 2 != a_evCtx.BytesTransferred) {
						System.Console.Error.WriteLine("invalid payload length : " + len);
					}
					else {
						string jsonMsg = Encoding.UTF8.GetString(a_evCtx.Buffer, 2, len);
						Message msg = JsonConvert.DeserializeObject<Message>(jsonMsg);
						if (msg.IsValid() == false) {
							System.Console.Error.WriteLine("invalid message : " + jsonMsg);
						}
						else {
							lock (m_recvedMessageQueue) {
								m_recvedMessageQueue.Enqueue(new RecvedMessage {
									m_sender = (IPEndPoint)a_evCtx.RemoteEndPoint,
									m_message = msg
								});
							}
							m_recvedEvent.Release();
						}
					}

					m_recvArgs.SetBuffer(0, recvBuf.Length);
					m_socket.ReceiveFromAsync(m_recvArgs);

				};
				m_socket.ReceiveFromAsync(m_recvArgs);
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
			string jsonMessage = JsonConvert.SerializeObject(a_message, Config.JsonFormatting);
			byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
			byte[] len = BitConverter.GetBytes((Int16)data.Length);
			var bufList = new List<ArraySegment<byte>>();
			bufList.Add(new ArraySegment<byte>(len));
			bufList.Add(new ArraySegment<byte>(data));

			SocketAsyncEventArgs ioArgs = new SocketAsyncEventArgs();
			ioArgs.BufferList = bufList;
			ioArgs.RemoteEndPoint = a_dest;
			ioArgs.Completed += (object a_sender, SocketAsyncEventArgs a_evCtx) =>
			{
				if (a_evCtx.SocketError != SocketError.Success) {
					System.Console.Error.WriteLine(a_evCtx.SocketError.ToString());
				}
			};
			m_socket.SendToAsync(ioArgs);
		}
	}
}