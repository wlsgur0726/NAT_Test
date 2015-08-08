using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace NAT_Test
{
	public class TestResult
	{
		public enum Behavior 
		{
			// NAT가 없는 경우.
			None,

			// 종단 주소에 무관하게 일관성 있는 동작.
			Endpoint_Independent,

			// 종단의 IP주소에 따라 다르게 취급하고 Port는 무관한 경우.
			Address_Dependent,

			// 종단의 IP뿐 아니라 Port가 달라져도 다르게 취급하는 경우.
			Address_and_Port_Dependent
		};


		public enum Hairpin
		{
			// Hairpin 지원 안함.
			No,

			// 통신은 가능하지만
			// 송신자 주소가 내부주소로 매핑된 경우.
			Available_Communication,

			// Hairpin 메시지를 수신 시
			// 송신자 주소가 외부주소로 매핑된 경우.
			Perfect
		}


		// 테스트 할 프로토콜
		public ProtocolType Protocol = ProtocolType.Unknown;


		// UDP Drop 등 예기치 못한 문제로 테스트를 끝까지 완료하지 못한 경우 false.
		public bool Complete = false;


		// 테스트 결과에 대한 부연설명.
		public string Comment = "";


		// 통신이 불가능한 경우 false.
		// 이 값이 false라면 테스트가 불가능하므로 이하 다른 변수들은 의미없다.
		public bool Available_Communication = true;


		// 서버와 클라 사이에 NAT가 존재하는지 여부.
		// 이 값이 false라면 NAT가 없으므로 이하 다른 변수들은 의미없다.
		public bool Exist_NAT = false;


		// First Local Socket의 내부주소
		public IPEndPoint PrivateAddress_1 = null;


		// Second Local Socket의 내부주소
		public IPEndPoint PrivateAddress_2 = null;


		// MainServer의 First Port와 연결된 외부주소
		public IPEndPoint PublicAddress_1 = null;


		// MainServer의 Second Port와 연결된 외부주소
		public IPEndPoint PublicAddress_2 = null;


		// SubServer와 연결된 외부주소
		public IPEndPoint PublicAddress_3 = null;


		// Hairpin 시 수신자에서 본 송신자 주소 (First Local Socket의 외부주소)
		public IPEndPoint PublicAddress_4 = null;


		// Hairpin 시 수신자에서 본 송신자 주소 (Second Local Socket의 외부주소)
		public IPEndPoint PublicAddress_5 = null;


		// NAT에서 Outbound Traffic 발생 시 
		// 송신자의 내부주소를 외부주소로 매핑하는 방식.
		//   - Endpoint_Independent        :  목적지의 주소에 상관 없이 동일한 외부주소로 매핑
		//   - Address_Dependent           :  목적지의 IP가 다르면 다른 외부주소로 매핑
		//   - Address_and_Port_Dependent  :  목적지의 IP 또는 Port가 다르면 다른 외부주소로 매핑
		public Behavior MappingBehavior = Behavior.None;


		// NAT에서 Inbound Traffic 발생 시
		// 송신자의 주소에 따라 패킷을 필터링하는 방식.
		//   - Endpoint_Independent        :  송신자의 주소에 상관없이 허용
		//   - Address_Dependent           :  송신자의 IP가 최초 outbound 시 각인된 IP와 다르면 드랍
		//   - Address_and_Port_Dependent  :  송신자의 IP와 Port가 최초 outbound 시 각인된 IP, Port와 다르면 드랍
		public Behavior FilteringBehavior = Behavior.None;


		// 헤어핀 기능 지원 여부.
		public Hairpin Supported_Hairpin = Hairpin.No;



		public override string ToString()
		{
			string str = base.ToString()
				+ "\n Protocol                 :  " + Protocol
				+ "\n Complete                 :  " + Complete
				+ "\n Comment                  :  " + Comment
				+ "\n Available_Communication  :  " + Available_Communication
				+ "\n Exist_NAT                :  " + Exist_NAT
				+ "\n PrivateAddress_1         :  " + PrivateAddress_1
				+ "\n PrivateAddress_2         :  " + PrivateAddress_2
				+ "\n PublicAddress_1          :  " + PublicAddress_1
				+ "\n PublicAddress_2          :  " + PublicAddress_2
				+ "\n PublicAddress_3          :  " + PublicAddress_3
				+ "\n PublicAddress_4          :  " + PublicAddress_4
				+ "\n PublicAddress_5          :  " + PublicAddress_5
				+ "\n MappingBehavior          :  " + MappingBehavior.ToString()
				+ "\n FilteringBehavior        :  " + FilteringBehavior.ToString()
				+ "\n Supported_Hairpin        :  " + Supported_Hairpin.ToString();

			return str;
		}
	}



	public class Client
	{
		ProtocolType m_protocol = ProtocolType.Unknown;

		IPEndPoint m_mainServer_port1 = null;

		IPEndPoint m_mainServer_port2 = null;

		IPEndPoint m_subServer = null;

		SocketPoller m_poller = null;

		TestResult m_testResult = null;

		Socket m_requester = null;



		public Client(ProtocolType a_protocol,
					  IPEndPoint a_mainServer_port1,
					  IPEndPoint a_mainServer_port2,
					  IPEndPoint a_subServer)
		{
			// must not null
			if (a_mainServer_port1==null || a_mainServer_port2==null || a_subServer==null)
				throw new ArgumentNullException();

			// 현재 지원 가능한 프로토콜인지 체크.
			if (a_protocol!=ProtocolType.Udp && a_protocol!=ProtocolType.Tcp)
				throw new ArgumentException(a_protocol + " is not supported.");

			// Address_and_Port_Dependent 여부를 가려내려면 MainServer는 동일한 IP에 서로 다른 두 Port를 사용해야 한다.
			if (a_mainServer_port1.Address.Equals(a_mainServer_port2.Address) == false) {
				throw new ArgumentException("a_mainServer_port1과 a_mainServer_port2의 IP가 다릅니다."
											+ " a_mainServer_port1:" + a_mainServer_port1.Address.ToString()
											+ " a_mainServer_port2:" + a_mainServer_port2.Address.ToString());
			}
			if (a_mainServer_port1.Port == a_mainServer_port2.Port) {
				throw new ArgumentException("a_mainServer_port1과 a_mainServer_port2의 Port가 같습니다."
											+ " a_mainServer_port1:" + a_mainServer_port1.Port
											+ " a_mainServer_port2:" + a_mainServer_port2.Port);
			}

			// Address_Dependent 여부를 가려내려면 MainServer와 SubServer의 IP가 서로 달라야 한다.
			if (a_subServer.Address.Equals(a_mainServer_port2.Address)) {
				throw new ArgumentException("mainServer와 subServer의 IP가 같습니다."
											+ " a_mainServer:" + a_mainServer_port1.Address.ToString()
											+ " a_subServer:" + a_subServer.Address.ToString());
			}

			m_protocol = a_protocol;
			m_mainServer_port1 = a_mainServer_port1;
			m_mainServer_port2 = a_mainServer_port2;
			m_subServer = a_subServer;
		}



		public TestResult StartTest()
		{
			Debug.Assert(m_protocol==ProtocolType.Tcp || m_protocol==ProtocolType.Udp);
			m_poller = new SocketPoller();
			m_testResult = new TestResult();
			m_testResult.Protocol = m_protocol;

			// 소켓 준비
			m_requester = Function.CreateSocket(m_protocol,
												null,
												m_protocol == ProtocolType.Tcp);
			m_testResult.PrivateAddress_1 = (IPEndPoint)m_requester.LocalEndPoint;
			if (m_protocol == ProtocolType.Udp) {
				// UDP는 하나의 소켓으로 모두 처리한다.
				m_poller.Start(m_requester);
			}
			else {
				Function.CreateListenr(m_testResult.PrivateAddress_1,
									   m_poller,
									   true);
			}

			// 테스트 수행
			try {
				// Step 1. Filtering Behavior Test
				if (Step1())
					return m_testResult;

				Debug.Assert(m_testResult.Exist_NAT);
				Debug.Assert(m_testResult.FilteringBehavior != TestResult.Behavior.None);

				// Step 2. Mapping Behavior Test (1)  :  APDM인지 여부를 테스트.
				if (m_testResult.PublicAddress_2 == null) {
					if (Step2())
						return m_testResult;
				}

				// Step 3. Mapping Behavior Test (2)  :  EIM인지 ADM인지 여부를 테스트.
				if (m_testResult.MappingBehavior == TestResult.Behavior.None) {
					Debug.Assert(m_testResult.PublicAddress_1.Equals(m_testResult.PublicAddress_2));
					if (Step3())
						return m_testResult;
				}

				Debug.Assert(m_testResult.MappingBehavior != TestResult.Behavior.None);

				// Step 4. Hairpin Test
				Step4();

				// 완료
				m_testResult.Complete = true;
				return m_testResult;
			}
			finally {
				m_poller.Stop();
			}
		}



		// StepN 함수들은 더이상 테스트를 진행 할 필요가 없을 때 true를 리턴한다.
		bool Step1()
		{
			Config.OnEventDelegate("\nStep 1. Filtering Behavior Test");
			Config.OnEventDelegate("MainServer의 First Port(" + m_mainServer_port1.ToString() + ")로 Request");

			// 요청, 응답 기다림
			if (m_protocol == ProtocolType.Tcp)
				Step1_ReqAndWait_TCP();
			else
				Step1_ReqAndWait_UDP();

			// 결과 처리
			{
				if (m_testResult.PublicAddress_1 == null) {
					m_testResult.Complete = false;
					m_testResult.Comment = "Response를 수신하지 못함.";
					if (m_testResult.PublicAddress_2!=null || m_testResult.PublicAddress_3!=null) {
						if (m_protocol == ProtocolType.Udp)
							m_testResult.Comment += " UDP Drop이 의심됩니다.";
					}
					else {
						m_testResult.Available_Communication = false;
						m_testResult.Comment += " 통신이 불가능합니다.";
					}
					Config.OnEventDelegate(m_testResult.Comment);
					return true;
				}

				if (IsLocalAddress(m_testResult.PrivateAddress_1, m_testResult.PublicAddress_1)) {
					// 내부주소와 외부주소가 같은 경우
					string comment =
						"내부주소와 외부주소가 같습니다. (" + m_testResult.PrivateAddress_1.ToString() + ")";
					Config.OnEventDelegate(comment);

					if (m_testResult.PublicAddress_2 == null) {
						Config.OnErrorDelegate(
							"NAT가 없는 상황인데 MainServer의 Second Port로부터 응답을 받지 못함.");
					}
					if (m_testResult.PublicAddress_3 == null) {
						Config.OnErrorDelegate(
							"NAT가 없는 상황인데 SubServer로부터 응답을 받지 못함. " +
							"Client측 방화벽의 Inbound 설정을 확인바랍니다.");
					}

					m_testResult.Exist_NAT = false;
					m_testResult.Complete = true;
					m_testResult.Comment = comment;
					return true;
				}

				m_testResult.Exist_NAT = true;
				if (m_testResult.PublicAddress_3 != null) {
					// IP와 Port가 아예 다른 SubServer로부터 메시지를 수신한 경우
					if (m_testResult.PublicAddress_2 == null) {
						Config.OnErrorDelegate(
							"SubServer로부터는 응답을 받았으나 MainSever의 Second Port로부터 응답을 받지 못함. " +
							"Client와 SubServer가 같은 호스트에서 실행된건 아닌지 확인 바랍니다.");
					}
					else {
						// MappingBehavior 확정 가능
						m_testResult.MappingBehavior = TestResult.Behavior.Endpoint_Independent;
					}
					m_testResult.FilteringBehavior = TestResult.Behavior.Endpoint_Independent;
					Config.OnEventDelegate("Full-Cone NAT입니다.");
				}
				else if (m_testResult.PublicAddress_2 != null) {
					// result.PublicAddress_3은 수신하지 못했는데 result.PublicAddress_2는 수신한 경우
					// MappingBehavior는 APDM은 아니지만 EIM인지 ADM인지 불분명
					m_testResult.FilteringBehavior = TestResult.Behavior.Address_Dependent;
					Config.OnEventDelegate("SubServer에게서만 Response를 받지 못했으므로 ADF입니다.");
				}
				else {
					// result.PublicAddress_1를 제외하고 모두 수신하지 못한 경우
					// MappingBehavior는 불분명
					Config.OnEventDelegate("Outbound가 없었던 Port로 부터는 Response를 받지 못했으므로 APDF입니다.");
					m_testResult.FilteringBehavior = TestResult.Behavior.Address_and_Port_Dependent;
				}
			}
			return false;
		}



		bool Step2()
		{
			Debug.Assert(m_testResult.PublicAddress_2 == null);
			Config.OnEventDelegate("\nStep 2. Mapping Behavior Test (1)  :  APDM인지 여부를 테스트.");
			Config.OnEventDelegate("MainServer의 Second Port(" + m_mainServer_port2.ToString() + ")로 Request");

			// 요청, 응답 기다림
			if (m_protocol == ProtocolType.Udp) {
				Step2_ReqAndWait_UDP();
			}
			else {
				m_requester = ReuseSocket(m_requester);
				Step2_ReqAndWait_TCP();
			}

			// 결과 처리
			if (m_testResult.PublicAddress_2 == null) {
				m_testResult.Complete = false;
				m_testResult.Comment = "Mapping Behavior 테스트 실패. MainServer로부터 Response를 수신하지 못함.";
				Config.OnEventDelegate(m_testResult.Comment);
				return true;
			}
			else {
				if (m_testResult.PublicAddress_2.Equals(m_testResult.PublicAddress_1)) {
					Config.OnEventDelegate("First Port로부터 받은 주소와 동일하므로 APDM은 아님.");
				}
				else {
					m_testResult.MappingBehavior = TestResult.Behavior.Address_and_Port_Dependent;
					Config.OnEventDelegate("First Port로부터 받은 주소와 다르므로 APDM입니다.");
				}
			}
			return false;
		}



		bool Step3()
		{
			Debug.Assert(m_testResult.PublicAddress_3 == null);
			Config.OnEventDelegate("\nStep 3. Mapping Behavior Test (2)  :  EIM인지 ADM인지 여부를 테스트.");
			Config.OnEventDelegate("SubServer(" + m_subServer.ToString() + ")로 Request");

			// 요청, 응답 기다림
			if (m_protocol == ProtocolType.Udp) {
				Step3_ReqAndWait_UDP();
			}
			else {
				m_requester = ReuseSocket(m_requester);
				Step3_ReqAndWait_TCP();
			}

			// 결과 처리
			if (m_testResult.PublicAddress_3 == null) {
				m_testResult.Complete = false;
				m_testResult.Comment = "Mapping Behavior 테스트 실패. SubServer로부터 Response를 수신하지 못함.";
				Config.OnEventDelegate(m_testResult.Comment);
				return true;
			}
			else {
				if (m_testResult.PublicAddress_3.Equals(m_testResult.PublicAddress_1)) {
					m_testResult.MappingBehavior = TestResult.Behavior.Endpoint_Independent;
					Config.OnEventDelegate("모든 외부주소가 동일하므로 EIM입니다.");
				}
				else {
					m_testResult.MappingBehavior = TestResult.Behavior.Address_Dependent;
					Config.OnEventDelegate("목적지 주소가 다를 경우에만 외부주소가 동일하므로 ADM입니다.");
				}
			}
			return false;
		}



		bool Step4()
		{
			Debug.Assert(m_testResult.PrivateAddress_1.Equals(m_testResult.PrivateAddress_2) == false);
			Debug.Assert(m_testResult.PublicAddress_4 == null);
			Debug.Assert(m_testResult.PublicAddress_5 == null);
			Config.OnEventDelegate("\nStep 4. Hairpin Test");
			
			// Second 소켓 준비
			Socket secondRequester = Function.CreateSocket(m_protocol,
														   null,
														   m_protocol == ProtocolType.Tcp);
			m_testResult.PrivateAddress_2 = (IPEndPoint)secondRequester.LocalEndPoint;
			if (m_protocol == ProtocolType.Udp) {
				m_poller.Start(secondRequester);
			}
			else {
				Function.CreateListenr((IPEndPoint)secondRequester.LocalEndPoint,
									   m_poller,
									   true);
			}

			// Second에서 First로 Request 시도
			Config.OnEventDelegate(
				"Local Second Port(" + m_testResult.PrivateAddress_2.ToString() + ")를 만들어서 " +
				"Local First Port의 외부주소(" + m_testResult.PublicAddress_1.ToString() + ")로 Request");

			IPEndPoint resAddr = null;
			if (m_protocol == ProtocolType.Udp) {
				Step4_ReqAndWait_UDP(secondRequester,
									 m_testResult.PublicAddress_1,
									 Message.SenderType.Client_SecondPort,
									 out resAddr);
			}
			else {
				Step4_ReqAndWait_TCP(secondRequester,
									 m_testResult.PublicAddress_1,
									 Message.SenderType.Client_SecondPort,
									 out resAddr);
			}
			m_testResult.PublicAddress_5 = resAddr;

			// 응답이 오지 않았다면 테스트 종료
			if (m_testResult.PublicAddress_5 == null) {
				m_testResult.Supported_Hairpin = TestResult.Hairpin.No;
				m_testResult.Comment = "hairpin message를 수신받지 못함 (Second->First)";
				Config.OnEventDelegate(m_testResult.Comment);
				return true;
			}

			// First에서 Second로 Request 시도
			Config.OnEventDelegate(
				"Local First Port(" + m_testResult.PrivateAddress_1.ToString() + ")에서 " +
				"Local Second Port(" + m_testResult.PublicAddress_5.ToString() + ")에게 Request");

			resAddr = null;
			if (m_protocol == ProtocolType.Udp) {
				Step4_ReqAndWait_UDP(m_requester,
									 m_testResult.PublicAddress_5,
									 Message.SenderType.Client_FirstPort,
									 out resAddr);
			}
			else {
				m_requester = ReuseSocket(m_requester);
				Step4_ReqAndWait_TCP(m_requester,
									 m_testResult.PublicAddress_5,
									 Message.SenderType.Client_FirstPort,
									 out resAddr);
			}
			m_testResult.PublicAddress_4 = resAddr;

			// 결과 처리
			if (m_testResult.PublicAddress_4 == null) {
				// First에서 Second로 회신이 불가능
				m_testResult.Supported_Hairpin = TestResult.Hairpin.No;
				m_testResult.Comment = "hairpin message를 수신받지 못함 (First->Second)";
				Config.OnEventDelegate(m_testResult.Comment);
				return true;
			}
			else {
				bool isPrivate1 = m_testResult.Exist_NAT
							   && IsLocalAddress(m_testResult.PrivateAddress_1,
												 m_testResult.PublicAddress_4);
				bool isPrivate2 = m_testResult.Exist_NAT
							   && IsLocalAddress(m_testResult.PrivateAddress_2,
												 m_testResult.PublicAddress_5);

				if (isPrivate1 || isPrivate2) {
					// 송신자 주소가 내부주소인 경우
					m_testResult.Supported_Hairpin = TestResult.Hairpin.Available_Communication;
					m_testResult.Comment = "수신측에서 본 src address가 private address입니다";
					if (isPrivate1)
						m_testResult.Comment +=
							", (First " + m_testResult.PublicAddress_4.ToString() + ")";
					if (isPrivate2)
						m_testResult.Comment +=
							", (Second " + m_testResult.PublicAddress_5.ToString() + ")";
					Config.OnEventDelegate(m_testResult.Comment);
				}
				else {
					// 송신자 주소가 다른 주소로(아마도 외부주소)로 매핑된 경우
					m_testResult.Supported_Hairpin = TestResult.Hairpin.Perfect;
				}
			}
			return false;
		}



		Socket ReuseSocket(Socket a_socket)
		{
			m_poller.Close(a_socket);			
			Socket ret = Function.CreateSocket(m_protocol,
											   m_testResult.PrivateAddress_1,
											   true);
			return ret;
		}



		static bool IsLocalAddress(IPEndPoint a_localAddr, IPEndPoint a_cmpAddr)
		{
			if (a_localAddr.Equals(a_cmpAddr))
				return true;

			if (a_localAddr.Port != a_cmpAddr.Port)
				return false;

			if (a_localAddr.Address.Equals(IPAddress.Any)) {
				IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
				foreach (IPAddress addr in host.AddressList) {
					if (addr.AddressFamily == AddressFamily.InterNetwork) {
						if (addr.Equals(a_cmpAddr.Address))
							return true;
					}
				}
			}
			return false;
		}



		static Message NewRequest(Message.SenderType a_senderType,
								  out int a_contextID)
		{
			do {
				a_contextID = Config.Random.Next(int.MaxValue);
			} while (a_contextID == 0);

			Message req = new Message();
			req.m_contextSeq = 1;
			req.m_contextID = a_contextID;
			req.m_senderType = a_senderType;

			return req;
		}



		Timer CreateSendWorker(Socket a_socket,
							   IPEndPoint a_dest,
							   Message.SenderType a_senderType,
							   out int a_contextID)
		{
			Debug.Assert(a_socket.ProtocolType == ProtocolType.Udp);

			Message req = NewRequest(a_senderType, out a_contextID);
			Timer timer = timer = new Timer();
			timer.Interval = Config.Retransmission_Interval_Ms;
			timer.Elapsed += new ElapsedEventHandler((object a_sender, ElapsedEventArgs a_eArgs) =>
			{
				Config.OnEventDelegate(
					" request to " + a_dest.ToString() + "... " + Message.ContextString(req));
				req.m_pingTime = System.Environment.TickCount;
				m_poller.SendTo(a_socket, a_dest, req, false);
			});
			timer.Start();

			return timer;
		}



		bool TCPRequest(Socket a_sock,
						IPEndPoint a_dest,
						Message.SenderType a_senderType,
						out int a_ctxID)
		{
			Message req = NewRequest(a_senderType, out a_ctxID);
			Config.OnEventDelegate(
					" request to " + a_dest.ToString() + "... " + Message.ContextString(req));

			if (m_poller.ConnectAndSend(a_sock, a_dest, req) == false) {
				Config.OnEventDelegate("Failed request.");
				return false;
			}

			return true;
		}



		struct Response
		{
			public Socket m_socket;
			public IPEndPoint m_sender;
			public IPEndPoint m_publicAddress;
			public Message.SenderType m_senderType;
			public int m_contextID;
			public int m_contextSeq;

			public string GetContextString()
			{
				return Message.ContextString(m_contextID, m_contextSeq);
			}
		}

		// 이 delegate에서는 유효한 메시지이면 true를,
		// 중복수신, 잘못된 메시지 등의 경우는 false를 리턴한다.
		delegate bool OnResponse(Response a_response);

		bool WaitForRecvEvent(int a_contextID,
							  ref int a_timeoutMs,
							  OnResponse a_callback)
		{
			Message resMsg;
			IPEndPoint sender;

			do {
				Stopwatch stopwatch = new Stopwatch();
				stopwatch.Start();

				Socket sock;
				bool isTimeout = ! m_poller.WaitForMessage(a_timeoutMs,
														   out resMsg,
														   out sock,
														   out sender);
				if (isTimeout)
					return false;

				IPEndPoint publicAddress = null;
				if (resMsg.AddressIsEmpty() == false) {
					publicAddress = new IPEndPoint(IPAddress.Parse(resMsg.m_address),
												   resMsg.m_port);
				}

				bool valid = resMsg.m_contextID == a_contextID;
				if (valid) {
					Response res;
					res.m_socket = sock;
					res.m_sender = sender;
					res.m_publicAddress = publicAddress;
					res.m_contextID = resMsg.m_contextID;
					res.m_contextSeq = resMsg.m_contextSeq;
					res.m_senderType = resMsg.m_senderType;
					valid = a_callback(res);
				}
				if (valid == false) {
					a_timeoutMs -= (int)stopwatch.ElapsedMilliseconds;
					if (a_timeoutMs < 0)
						a_timeoutMs = 0;
					continue;
				}

				break;
			} while (true);
			return true;
		}



		void Step1_ReqAndWait_UDP()
		{
			Debug.Assert(m_testResult.PublicAddress_1 == null);
			Debug.Assert(m_testResult.PublicAddress_2 == null);
			Debug.Assert(m_testResult.PublicAddress_3 == null);
			int ctxID;

			// MainServer의 First Port에게 Request를 시도
			var sendWorker = CreateSendWorker(m_requester,
											  m_mainServer_port1,
											  Message.SenderType.Do_Not_Care,
											  out ctxID);

			// 다음 3개의 EndPoint로부터 응답을 기다린다.
			//  - MainServer의 First Port
			//  - MainServer의 Second Port
			//  - SubServer
			int recvTimeout = Config.Response_Timeout_Ms;
			bool isNotTimeout = true;
			bool existNullAddr = true;
			do {
				isNotTimeout = WaitForRecvEvent(ctxID, ref recvTimeout,
					(Response a_response) =>
					{
						string ctxstr = a_response.GetContextString();
						string addrstr = "(" + a_response.m_publicAddress.ToString() + ")";

						if (a_response.m_sender.Equals(m_mainServer_port1)) {
							if (m_testResult.PublicAddress_1 != null)
								return false;
							Config.OnEventDelegate(
								"MainServer의 First Port로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
							m_testResult.PublicAddress_1 = a_response.m_publicAddress;
						}
						else if (a_response.m_sender.Equals(m_mainServer_port2)) {
							if (m_testResult.PublicAddress_2 != null)
								return false;
							Config.OnEventDelegate(
								"MainServer의 Second Port로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
							m_testResult.PublicAddress_2 = a_response.m_publicAddress;
						}
						else if (a_response.m_sender.Equals(m_subServer)) {
							if (m_testResult.PublicAddress_3 != null)
								return false;
							Config.OnEventDelegate(
								"SubServer로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
							m_testResult.PublicAddress_3 = a_response.m_publicAddress;
						}
						else {
							Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
							return false;
						}

						return true;
					}
				);
				existNullAddr = m_testResult.PublicAddress_1 == null
							 || m_testResult.PublicAddress_2 == null
							 || m_testResult.PublicAddress_3 == null;
			} while (isNotTimeout && existNullAddr);

			sendWorker.Close();
		}


		void Step1_ReqAndWait_TCP()
		{
			Debug.Assert(m_testResult.PublicAddress_1 == null);
			Debug.Assert(m_testResult.PublicAddress_2 == null);
			Debug.Assert(m_testResult.PublicAddress_3 == null);
			int ctxID;

			// MainServer의 First Port에게 Request를 시도
			bool reqSuccess = TCPRequest(m_requester,
										 m_mainServer_port1,
										 Message.SenderType.Do_Not_Care,
										 out ctxID);
			if (reqSuccess == false)
				return;

			// 다음 3개의 EndPoint로부터 응답을 기다린다.
			//  - MainServer의 First Port
			//  - MainServer의 Second Port
			//  - SubServer
			int recvTimeout = Config.Response_Timeout_Ms;
			bool isNotTimeout = true;
			bool existNullAddr = true;
			do {
				isNotTimeout = WaitForRecvEvent(ctxID, ref recvTimeout,
					(Response a_response) =>
					{
						m_poller.Close(a_response.m_socket);
						string ctxstr = a_response.GetContextString();
						string addrstr = "(" + a_response.m_publicAddress.ToString() + ")";

						switch (a_response.m_senderType) {
							case Message.SenderType.MainServer_FirstPort:
								Config.OnEventDelegate(
									"MainServer의 First Port로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
								m_testResult.PublicAddress_1 = a_response.m_publicAddress;
								break;

							case Message.SenderType.MainServer_SecondPort:
								Config.OnEventDelegate(
									"MainServer의 Second Port로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
								m_testResult.PublicAddress_2 = a_response.m_publicAddress;
								break;

							case Message.SenderType.SubServer:
								Config.OnEventDelegate(
									"SubServer로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
								m_testResult.PublicAddress_3 = a_response.m_publicAddress;
								break;

							default:
								Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
								return false;
						}
						return true;
					}
				);
				existNullAddr = m_testResult.PublicAddress_1 == null
							 || m_testResult.PublicAddress_2 == null
							 || m_testResult.PublicAddress_3 == null;
			} while (isNotTimeout && existNullAddr);
		}



		void Step2_ReqAndWait_UDP()
		{
			Debug.Assert(m_testResult.PublicAddress_2 == null);
			int ctxID;

			// MainServer의 Second Port로 Request를 시도
			var sendWorker = CreateSendWorker(m_requester,
											  m_mainServer_port2,
											  Message.SenderType.Do_Not_Care,
											  out ctxID);

			// 응답을 기다림
			int recvTimeout = Config.Response_Timeout_Ms;
			WaitForRecvEvent(ctxID, ref recvTimeout,
				(Response a_response) =>
				{
					if (a_response.m_sender.Equals(m_mainServer_port2) == false) {
						Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
						return false;
					}
					if (m_testResult.PublicAddress_2 != null)
						return false;

					string ctxstr = a_response.GetContextString();
					string addrstr = "(" + a_response.m_publicAddress.ToString() + ")";
					Config.OnEventDelegate(
						"MainServer의 Second Port로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
					m_testResult.PublicAddress_2 = a_response.m_publicAddress;
					return true;
				}
			);

			sendWorker.Close();
		}



		void Step2_ReqAndWait_TCP()
		{
			Debug.Assert(m_testResult.PublicAddress_2 == null);
			int ctxID;

			// MainServer의 Second Port로 Request를 시도
			bool reqSuccess = TCPRequest(m_requester,
										 m_mainServer_port2,
										 Message.SenderType.Do_Not_Care,
										 out ctxID);
			if (reqSuccess == false)
				return;

			// 응답을 기다림
			int recvTimeout = Config.Response_Timeout_Ms;
			WaitForRecvEvent(ctxID, ref recvTimeout,
				(Response a_response) =>
				{
					m_poller.Close(a_response.m_socket);
					if (a_response.m_senderType != Message.SenderType.MainServer_SecondPort) {
						Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
						return false;
					}

					string ctxstr = a_response.GetContextString();
					string addrstr = "(" + a_response.m_publicAddress.ToString() + ")";
					Config.OnEventDelegate(
						"MainServer의 Second Port로부터 Response" + addrstr + " 수신 성공. " + ctxstr);
					m_testResult.PublicAddress_2 = a_response.m_publicAddress;
					return true;
				}
			);
		}



		void Step3_ReqAndWait_UDP()
		{
			Debug.Assert(m_testResult.PublicAddress_3 == null);
			int ctxID;

			// SubServer에게 Request를 시도
			var sendWorker = CreateSendWorker(m_requester,
											  m_subServer,
											  Message.SenderType.Do_Not_Care,
											  out ctxID);

			// 응답을 기다림
			int recvTimeout = Config.Response_Timeout_Ms;
			WaitForRecvEvent(ctxID, ref recvTimeout,
				(Response a_response) =>
				{
					if (a_response.m_sender.Equals(m_subServer) == false) {
						Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
						return false;
					}
					if (m_testResult.PublicAddress_3 != null)
						return false;

					string ctxstr = a_response.GetContextString();
					string addrstr = "(" + a_response.m_publicAddress.ToString() + ")";
					Config.OnEventDelegate(
						"SubServer로부터 Response" + addrstr + " 수신 성공." + ctxstr);
					m_testResult.PublicAddress_3 = a_response.m_publicAddress;
					return true;
				}
			);

			sendWorker.Close();
		}



		void Step3_ReqAndWait_TCP()
		{
			Debug.Assert(m_testResult.PublicAddress_3 == null);
			int ctxID;

			// SubServer에게 Request를 시도
			bool reqSuccess = TCPRequest(m_requester,
										 m_subServer,
										 Message.SenderType.Do_Not_Care,
										 out ctxID);
			if (reqSuccess == false)
				return;

			// 응답을 기다림
			int recvTimeout = Config.Response_Timeout_Ms;
			WaitForRecvEvent(ctxID, ref recvTimeout,
				(Response a_response) =>
				{
					m_poller.Close(a_response.m_socket);
					if (a_response.m_senderType != Message.SenderType.SubServer) {
						Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
						return false;
					}

					string ctxstr = a_response.GetContextString();
					string addrstr = "(" + a_response.m_publicAddress.ToString() + ")";
					Config.OnEventDelegate(
						"SubServer로부터 Response" + addrstr + " 수신 성공" + ctxstr);
					m_testResult.PublicAddress_3 = a_response.m_publicAddress;
					return true;
				}
			);
		}



		void Step4_ReqAndWait_UDP(Socket a_socket,
								  IPEndPoint a_reqDest,
								  Message.SenderType a_senderType,
								  out IPEndPoint a_publicAddress)
		{
			int ctxID;
			var sendWorker = CreateSendWorker(a_socket,
											  a_reqDest,
											  a_senderType,
											  out ctxID);
			IPEndPoint sender = null;
			int recvTimeout = Config.Response_Timeout_Ms;
			WaitForRecvEvent(ctxID, ref recvTimeout,
				(Response a_response) =>
				{
					if (a_response.m_senderType != a_senderType) {
						Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
						return false;
					}

					if (sender != null)
						return false;

					string ctxstr = a_response.GetContextString();
					Config.OnEventDelegate(
						a_response.m_sender.ToString() + "로부터 Request 수신 " + ctxstr);
					sender = a_response.m_sender;
					return true;
				}
			);

			a_publicAddress = sender;
			sendWorker.Close();
		}



		void Step4_ReqAndWait_TCP(Socket a_socket,
								  IPEndPoint a_reqDest,
								  Message.SenderType a_senderType,
								  out IPEndPoint a_publicAddress)
		{
			a_publicAddress = null;
			int ctxID;
			bool reqSuccess = TCPRequest(a_socket,
										 a_reqDest,
										 a_senderType,
										 out ctxID);
			if (reqSuccess == false)
				return;

			IPEndPoint sender = null;
			int recvTimeout = Config.Response_Timeout_Ms;
			WaitForRecvEvent(ctxID, ref recvTimeout,
				(Response a_response) =>
				{
					m_poller.Close(a_response.m_socket);
					if (a_response.m_senderType != a_senderType) {
						Config.OnErrorDelegate("엉뚱한 sender : " + a_response.m_sender.ToString());
						return false;
					}

					string ctxstr = a_response.GetContextString();
					Config.OnEventDelegate(
						a_response.m_sender.ToString() + "로부터 Request 수신 " + ctxstr);
					sender = a_response.m_sender;
					return true;
				}
			);

			a_publicAddress = sender;
		}
	}
}