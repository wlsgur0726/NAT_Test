using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
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
			
			// Hairpin 메시지를 수신 시
			// 송신자 주소가 외부주소로 매핑된 경우
			Perfect,

			// 통신은 가능하지만
			// 송신자 주소가 내부주소로 매핑된 경우
			Available_Communication
		}

		// UDP Drop 등 예기치 못한 문제로 테스트를 끝까지 완료하지 못한 경우 false.
		public bool Complete = false;


		// 테스트 결과에 대한 부연설명.
		public string Comment = "";


		// 통신이 불가능한 경우 false.
		// 이 값이 false라면 이하 다른 변수들은 의미없다.
		public bool Available_Communication = true;

		
		// 서버와 클라 사이에 NAT가 존재하는지 여부.
		// 이 값이 false라면 이하 다른 변수들은 의미없다.
		public bool Exit_NAT = false;


		// First Local UDP Socket의 내부주소
		public IPEndPoint PrivateUdpAddress_1 = null;


		// Second Local UDP Socket의 내부주소
		public IPEndPoint PrivateUdpAddress_2 = null;


		// MainServer의 First UDP와 연결된 외부주소
		public IPEndPoint PublicUdpAddress_1 = null;


		// MainServer의 Second UDP와 연결된 외부주소
		public IPEndPoint PublicUdpAddress_2 = null;


		// SubServer와 연결된 외부주소
		public IPEndPoint PublicUdpAddress_3 = null;


		// Hairpin 시 수신자에서 본 송신자 주소 (First UDP의 외부주소)
		public IPEndPoint PublicUdpAddress_4 = null;


		// Hairpin 시 수신자에서 본 송신자 주소 (Second UDP의 외부주소)
		public IPEndPoint PublicUdpAddress_5 = null;


		// NAT에서 Outbound Traffic 발생 시 송신자의 내부주소를 외부주소로 매핑하는 방식.
		// 이 값이 Address_and_Port_Dependent라면 홀펀칭이 불가능하므로 이하 다른 변수들은 의미없다.
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
		public Hairpin SupportedHairpin = Hairpin.No;


		public override string ToString()
		{
			string str = base.ToString()
				+ "\nComplete                 :  " + Complete
				+ "\nComment                  :  " + Comment
				+ "\nAvailable_Communication  :  " + Available_Communication
				+ "\nExit_NAT                 :  " + Exit_NAT
				+ "\nPrivateUdpAddress_1      :  " + PrivateUdpAddress_1
				+ "\nPrivateUdpAddress_2      :  " + PrivateUdpAddress_2
				+ "\nPublicUdpAddress_1       :  " + PublicUdpAddress_1
				+ "\nPublicUdpAddress_2       :  " + PublicUdpAddress_2
				+ "\nPublicUdpAddress_3       :  " + PublicUdpAddress_3
				+ "\nPublicUdpAddress_4       :  " + PublicUdpAddress_4
				+ "\nPublicUdpAddress_5       :  " + PublicUdpAddress_5
				+ "\nMappingBehavior          :  " + MappingBehavior.ToString()
				+ "\nFilteringBehavior        :  " + FilteringBehavior.ToString()
				+ "\nSupportedHairpin         :  " + SupportedHairpin.ToString();

			return str;
		}
	}



	public class Client
	{
		IPEndPoint m_mainServer_udp1 = null;

		IPEndPoint m_mainServer_udp2 = null;

		IPEndPoint m_subServer_udp = null;

		public Client(IPEndPoint a_mainServer_udp1,
					  IPEndPoint a_mainServer_udp2,
					  IPEndPoint a_subServer_udp)
		{
			// must not null
			if (a_mainServer_udp1==null || a_mainServer_udp2==null || a_subServer_udp==null)
				throw new ArgumentNullException();

			// Address_and_Port_Dependent 여부를 가려내려면 MainServer는 동일한 IP에 서로 다른 두 Port를 사용해야 한다.
			if (a_mainServer_udp1.Address.Equals(a_mainServer_udp2.Address) == false) {
				throw new ArgumentException("a_mainServer_udp1와 a_mainServer_udp2의 IP가 다릅니다."
											+ " a_mainServer_udp1:" + a_mainServer_udp1.Address.ToString()
											+ " a_mainServer_udp2:" + a_mainServer_udp2.Address.ToString());
			}
			if (a_mainServer_udp1.Port == a_mainServer_udp2.Port) {
				throw new ArgumentException("a_mainServer_udp1와 a_mainServer_udp2의 Port가 같습니다."
											+ " a_mainServer_udp1:" + a_mainServer_udp1.Port
											+ " a_mainServer_udp2:" + a_mainServer_udp2.Port);
			}

			// Address_Dependent 여부를 가려내려면 MainServer와 SubServer의 IP가 서로 달라야 한다.
			if (a_subServer_udp.Address.Equals(a_mainServer_udp2.Address)) {
				throw new ArgumentException("mainServer와 subServer의 IP가 같습니다."
											+ " a_mainServer_udp:" + a_mainServer_udp1.Address.ToString()
											+ " a_subServer_udp:" + a_subServer_udp.Address.ToString());
			}

			m_mainServer_udp1 = a_mainServer_udp1;
			m_mainServer_udp2 = a_mainServer_udp2;
			m_subServer_udp = a_subServer_udp;
		}



		public TestResult StartTest()
		{
			TestResult result = new TestResult();

			SocketIo mainIO;
			CreateSocketIO(ProtocolType.Udp, out mainIO, out result.PrivateUdpAddress_1);

			// Step 1. Filtering Behavior Test
			{
				Config.OnEventDelegate("\nStep 1. Filtering Behavior Test");
				Config.OnEventDelegate("MainServer의 First UDP(" + m_mainServer_udp1.ToString() + ")로 Request");
				int ctxID;
				Timer sendWorker = CreateSendWorker(mainIO, m_mainServer_udp1, out ctxID);

				int recvTimeout = Config.Timeout_Ms;
				bool isTimeout = false;
				bool addrIsNull = true;
				do
				{
					isTimeout = ! WaitForRecvEvent(ctxID, mainIO, ref recvTimeout,
						(IPEndPoint a_sender, IPEndPoint a_publicAddress, int a_contextID, int a_contextSeq) =>
						{
							string ctxstr = Message.ContextString(a_contextID, a_contextSeq);
							string addrstr = "(" + a_publicAddress.ToString() + ")";
							if (a_sender.Equals(m_mainServer_udp1)) {
								if (result.PublicUdpAddress_1 != null)
									return false;
								Config.OnEventDelegate(
									"MainServer의 First UDP로부터 Response" + addrstr + " 수신 성공 " + ctxstr);
								result.PublicUdpAddress_1 = a_publicAddress;
							}
							else if (a_sender.Equals(m_mainServer_udp2)) {
								if (result.PublicUdpAddress_2 != null)
									return false;
								Config.OnEventDelegate(
									"MainServer의 Second UDP로부터 Response" + addrstr + " 수신 성공" + ctxstr);
								result.PublicUdpAddress_2 = a_publicAddress;
							}
							else if (a_sender.Equals(m_subServer_udp)) {
								if (result.PublicUdpAddress_3 != null)
									return false;
								Config.OnEventDelegate(
									"SubServer로부터 Response" + addrstr + " 수신 성공" + ctxstr);
								result.PublicUdpAddress_3 = a_publicAddress;
							}
							else {
								Config.OnErrorDelegate("엉뚱한 sender : " + a_sender.ToString());
								return false;
							}

							return true;
						}
					);
					addrIsNull = result.PublicUdpAddress_1 == null
							  || result.PublicUdpAddress_2 == null
							  || result.PublicUdpAddress_3 == null;
				} while (isTimeout==false && addrIsNull);
				sendWorker.Stop();

				if (result.PublicUdpAddress_1 == null) {
					result.Complete = false;
					result.Comment = "Response를 수신하지 못함.";
					if (result.PublicUdpAddress_2!=null || result.PublicUdpAddress_3!=null || isTimeout==false) {
						result.Comment += " UDP Drop이 의심됩니다.";
					}
					else {
						result.Available_Communication = false;
						result.Comment += " UDP 통신이 불가능합니다.";
					}
					Config.OnEventDelegate(result.Comment);
					return result;
				}

				if (result.PublicUdpAddress_1.Equals(result.PrivateUdpAddress_1)) {
					// 내부주소와 외부주소가 같은 경우
					if (result.PublicUdpAddress_2 == null) {
						Config.OnErrorDelegate("NAT가 없는 상황에서 PublicAddress_2를 수신하지 못함.");
					}
					if (result.PublicUdpAddress_3 == null) {
						Config.OnErrorDelegate("NAT가 없는 상황에서 PublicAddress_3을 수신하지 못함.");
					}

					result.Exit_NAT = false;
					result.Complete = true;
					result.Comment = "내부주소와 외부주소가 같습니다. (" + result.PrivateUdpAddress_1.ToString() + ")";
					Config.OnEventDelegate(result.Comment);
					return result;
				}

				result.Exit_NAT = true;
				if (result.PublicUdpAddress_3 != null) {
					// IP와 Port가 아예 다른 SubServer로부터 메시지를 수신한 경우
					if (result.PublicUdpAddress_2 == null) {
						Config.OnErrorDelegate("Full-Cone NAT 상황에서 PublicAddress_2를 수신하지 못함.");
					}
					// MappingBehavior 확정 가능
					result.MappingBehavior = TestResult.Behavior.Endpoint_Independent;
					result.FilteringBehavior = TestResult.Behavior.Endpoint_Independent;
					Config.OnEventDelegate("Full-Cone NAT입니다.");
				}
				else if (result.PublicUdpAddress_2 != null) {
					// result.PublicAddress_3은 수신하지 못했는데 result.PublicAddress_2는 수신한 경우
					// MappingBehavior는 APDM은 아니지만 EIM인지 ADM인지 불분명
					result.FilteringBehavior = TestResult.Behavior.Address_Dependent;
					Config.OnEventDelegate("SubServer에게서만 Response를 받지 못했으므로 ADF입니다.");
				}
				else {
					// result.PublicAddress_1를 제외하고 모두 수신하지 못한 경우
					// MappingBehavior는 불분명
					Config.OnEventDelegate("Outbound가 없었던 Port로 부터는 Response를 받지 못했으므로 APDF입니다.");
					result.FilteringBehavior = TestResult.Behavior.Address_and_Port_Dependent;
				}
			}


			Debug.Assert(result.Exit_NAT == true);
			Debug.Assert(result.FilteringBehavior != TestResult.Behavior.None);


			// Step 2. Mapping Behavior Test (1)  :  APDM인지 여부를 테스트.
			if (result.PublicUdpAddress_2 == null) {
				Config.OnEventDelegate("\nStep 2. Mapping Behavior Test (1)  :  APDM인지 여부를 테스트.");

				Debug.Assert(result.MappingBehavior == TestResult.Behavior.None);
				Debug.Assert(result.FilteringBehavior == TestResult.Behavior.Address_and_Port_Dependent);

				Config.OnEventDelegate("MainServer의 Second UDP(" + m_mainServer_udp2.ToString() + ")로 Request");
				int ctxID;
				Timer sendWorker = CreateSendWorker(mainIO, m_mainServer_udp2, out ctxID);

				int recvTimeout = Config.Timeout_Ms;
				WaitForRecvEvent(ctxID, mainIO, ref recvTimeout,
					(IPEndPoint a_sender, IPEndPoint a_publicAddress, int a_contextID, int a_contextSeq) =>
					{
						string ctxstr = Message.ContextString(a_contextID, a_contextSeq);
						string addrstr = "(" + a_publicAddress.ToString() + ")";
						if (a_sender.Equals(m_mainServer_udp2) == false) {
							Config.OnErrorDelegate("엉뚱한 sender : " + a_sender.ToString());
							return false;
						}
						if (result.PublicUdpAddress_2 != null)
							return false;

						Config.OnEventDelegate(
							"MainServer의 Second UDP로부터 Response" + addrstr + " 수신 성공" + ctxstr);
						result.PublicUdpAddress_2 = a_publicAddress;
						return true;
					}
				);
				sendWorker.Stop();

				if (result.PublicUdpAddress_2 == null) {
					result.Complete = false;
					result.Comment = "Mapping Behavior 테스트 실패. MainServer로부터 Response를 수신하지 못함.";
					Config.OnEventDelegate(result.Comment);
					return result;
				}
				else {
					if (result.PublicUdpAddress_2.Equals(result.PublicUdpAddress_1)) {
						Config.OnEventDelegate("First UDP로부터 받은 주소와 동일하므로 APDM은 아님.");
					}
					else {
						result.MappingBehavior = TestResult.Behavior.Address_and_Port_Dependent;
						Config.OnEventDelegate("First UDP로부터 받은 주소와 다르므로 APDM입니다.");
					}
				}
			}


			// Step 3. Mapping Behavior Test (2)  :  EIM인지 ADM인지 여부를 테스트.
			if (result.MappingBehavior == TestResult.Behavior.None) {
				Config.OnEventDelegate("\nStep 3. Mapping Behavior Test (2)  :  EIM인지 ADM인지 여부를 테스트.");

				Debug.Assert(result.PublicUdpAddress_1.Equals(result.PublicUdpAddress_2));
				Debug.Assert(result.PublicUdpAddress_3 == null);

				Config.OnEventDelegate("SubServer의 UDP(" + m_subServer_udp.ToString() + ")로 Request");
				int ctxID;
				Timer sendWorker = CreateSendWorker(mainIO, m_subServer_udp, out ctxID);

				int recvTimeout = Config.Timeout_Ms;
				WaitForRecvEvent(ctxID, mainIO, ref recvTimeout,
					(IPEndPoint a_sender, IPEndPoint a_publicAddress, int a_contextID, int a_contextSeq) =>
					{
						string ctxstr = Message.ContextString(a_contextID, a_contextSeq);
						string addrstr = "(" + a_publicAddress.ToString() + ")";
						if (a_sender.Equals(m_subServer_udp) == false) {
							Config.OnErrorDelegate("엉뚱한 sender : " + a_sender.ToString());
							return false;
						}
						if (result.PublicUdpAddress_3 != null)
							return false;

						Config.OnEventDelegate(
							"SubServer로부터 Response" + addrstr + " 수신 성공" + ctxstr);
						result.PublicUdpAddress_3 = a_publicAddress;
						return true;
					}
				);
				sendWorker.Stop();

				if (result.PublicUdpAddress_3 == null) {
					result.Complete = false;
					result.Comment = "Mapping Behavior 테스트 실패. SubServer로부터 Response를 수신하지 못함.";
					Config.OnEventDelegate(result.Comment);
					return result;
				}
				else {
					if (result.PublicUdpAddress_3.Equals(result.PublicUdpAddress_1)) {
						result.MappingBehavior = TestResult.Behavior.Endpoint_Independent;
						Config.OnEventDelegate("모든 외부주소가 동일하므로 EIM입니다.");
					}
					else {
						result.MappingBehavior = TestResult.Behavior.Address_Dependent;
						Config.OnEventDelegate("목적지 주소가 다를 경우에만 외부주소가 동일하므로 ADM입니다.");
					}
				}
			}


			Debug.Assert(result.MappingBehavior != TestResult.Behavior.None);


			// Step 4. Hairpin Test
			{
				Config.OnEventDelegate("\nStep 4. Hairpin Test");

				SocketIo subIO;
				CreateSocketIO(ProtocolType.Udp, out subIO, out result.PrivateUdpAddress_2);
				Debug.Assert(result.PrivateUdpAddress_1.Equals(result.PrivateUdpAddress_2) == false);

				// Second -> First
				Config.OnEventDelegate(
					"Second UDP(" + result.PrivateUdpAddress_2.ToString() + ")를 만들어서 " +
					"First UDP의 외부주소(" + result.PublicUdpAddress_1.ToString() + ")로 Request");
				int ctxID;
				Timer sendWorker = CreateSendWorker(subIO, result.PublicUdpAddress_1, out ctxID);

				int recvTimeout = Config.Timeout_Ms;
				WaitForRecvEvent(ctxID, mainIO, ref recvTimeout,
					(IPEndPoint a_sender, IPEndPoint a_publicAddress, int a_contextID, int a_contextSeq) =>
					{
						string ctxstr = Message.ContextString(a_contextID, a_contextSeq);
						if (result.PublicUdpAddress_5 != null)
							return false;

						Config.OnEventDelegate(a_sender.ToString() + "로부터 Request 수신 " + ctxstr);
						result.PublicUdpAddress_5 = a_sender;
						return true;
					}
				);
				sendWorker.Stop();

				if (result.PublicUdpAddress_5 == null) {
					result.SupportedHairpin = TestResult.Hairpin.No;
					result.Comment = "hairpin message를 수신받지 못함 (Second->First)";
					Config.OnEventDelegate(result.Comment);
				}
				else {
					// First -> Second
					Config.OnEventDelegate(
						"First UDP(" + result.PrivateUdpAddress_1.ToString() + ")에서 " +
						"Second UDP(" + result.PublicUdpAddress_5.ToString() + ")에게 Request");
					sendWorker = CreateSendWorker(mainIO, result.PublicUdpAddress_5, out ctxID);

					recvTimeout = Config.Timeout_Ms;
					WaitForRecvEvent(ctxID, subIO, ref recvTimeout,
						(IPEndPoint a_sender, IPEndPoint a_publicAddress, int a_contextID, int a_contextSeq) =>
						{
							string ctxstr = Message.ContextString(a_contextID, a_contextSeq);
							if (result.PublicUdpAddress_4 != null)
								return false;

							Config.OnEventDelegate(a_sender.ToString() + "로부터 Request 수신 " + ctxstr);
							result.PublicUdpAddress_4 = a_sender;
							return true;
						}
					);
					sendWorker.Stop();

					if (result.PublicUdpAddress_5 == null) {
						// First에서 Second로 회신이 불가능
						result.SupportedHairpin = TestResult.Hairpin.No;
						result.Comment = "hairpin message를 수신받지 못함 (First->Second)";
						Config.OnEventDelegate(result.Comment);
					}
					else {
						bool isPrivate1 = result.PublicUdpAddress_4.Equals(result.PrivateUdpAddress_1);
						bool isPrivate2 = result.PublicUdpAddress_5.Equals(result.PrivateUdpAddress_2);
						if (isPrivate1 || isPrivate2) {
							// 송신자 주소가 내부주소인 경우
							result.SupportedHairpin = TestResult.Hairpin.Available_Communication;
							result.Comment = "수신측에서 본 src address가 private address입니다";
							if (isPrivate1)
								result.Comment += ", (First " + result.PublicUdpAddress_4.ToString() + ")";
							if (isPrivate2)
								result.Comment += ", (Second " + result.PublicUdpAddress_5.ToString() + ")";
							Config.OnEventDelegate(result.Comment);
						}
						else {
							// 송신자 주소가 다른 주소로(아마도 외부주소)로 매핑된 경우
							result.SupportedHairpin = TestResult.Hairpin.Perfect;
						}
					}
				}
			}

			result.Complete = true;
			return result;
		}



		void CreateSocketIO(ProtocolType a_protocol,
							out SocketIo a_io,
							out IPEndPoint a_privateAddress)
		{
			SocketType sockType = SocketType.Unknown;
			if (a_protocol == ProtocolType.Tcp)
				sockType = SocketType.Stream;
			else if (a_protocol == ProtocolType.Udp)
				sockType = SocketType.Dgram;
			else {
				a_io = null;
				a_privateAddress = null;
				throw new Exception("fail CreateSocketIO()");
			}

			Socket sock = new Socket(AddressFamily.InterNetwork,
									 sockType,
									 a_protocol);
			sock.Bind(new IPEndPoint(IPAddress.Any, 0));
			a_privateAddress = (IPEndPoint)sock.LocalEndPoint;
			a_io = new SocketIo(sock);
			a_io.Start();
		}



		Timer CreateSendWorker(SocketIo a_io, 
							   IPEndPoint a_dest,
							   out int a_contextID)
		{
			do {
				a_contextID = Config.Random.Next(int.MaxValue);
			} while (a_contextID == 0);

			Message req = new Message();
			req.m_contextSeq = 1;
			req.m_contextID = a_contextID;

			Timer timer = new Timer();
			timer.Interval = Config.Retransmission_Interval_Ms;
			timer.Elapsed += new ElapsedEventHandler((object a_sender, ElapsedEventArgs a_eArgs) =>
			{
				Config.OnEventDelegate(
					" request to " + a_dest.ToString() + "... " + 
					Message.ContextString(req.m_contextID, req.m_contextSeq));
				req.m_pingTime = System.Environment.TickCount;
				a_io.SendTo(req, a_dest);
			});
			timer.Start();
			return timer;
		}



		delegate bool OnResponse(IPEndPoint a_sender, 
								 IPEndPoint a_publicAddress,
								 int a_contextID,
								 int a_contexSeq);

		bool WaitForRecvEvent(int a_contextID,
							  SocketIo a_io, 
							  ref int a_timeoutMs,
							  OnResponse a_callback)
		{
			Message res;
			IPEndPoint sender;
			
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			bool loop = true;
			while (loop) {
				bool isTimeout = ! a_io.WaitForRecv(a_timeoutMs,
													out res,
													out sender);
				if (isTimeout)
					return false;

				IPEndPoint publicAddress = new IPEndPoint(IPAddress.Parse(res.m_address),
														  res.m_port);

				bool valid = res.m_contextID == a_contextID;
				if (valid) {
					valid = a_callback(sender,
									   publicAddress,
									   a_contextID,
									   res.m_contextSeq);
				}
				if (valid == false) {
					a_timeoutMs -= (int)stopwatch.ElapsedMilliseconds;
					if (a_timeoutMs > 0)
						continue;
					else
						a_timeoutMs = 0;
				}

				loop = false;
			}
			return true;
		}
	}
}