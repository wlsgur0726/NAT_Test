using UnityEngine;
using System.Collections;
using System.Threading;
using System;
using NAT_Test;
using System.Net;

public class Main : MonoBehaviour
{
	bool m_terminate = false;
	Thread m_mainThread = null;

	void Start () 
	{
	}
	

	void Update () 
	{
		if (m_terminate && m_mainThread!=null) {
			if (m_mainThread.Join(0)) {
				m_mainThread = null;
			}
		}

		if (m_mainThread == null) {
			m_mainThread = new Thread((object p) =>
			{
				m_terminate = false;
				try {
					TestSystem.PrintLine("================================");
                    TestSystem.PrintLine("Start MainThread\n");
					TestMain();
				}
				catch (Exception e) {
					TestSystem.PrintLine(e.ToString());
				}
				finally {
					TestSystem.PrintLine("\nEnd MainThread");
					TestSystem.PrintLine("================================");
					m_terminate = true;
				}
			});
			m_mainThread.Start();
		}
	}
	

	static void TestMain()
	{
#if false
		string main_ip = "1.1.1.1";	
		string main_port1 = "11111";
		string main_port2 = "22222";

		string sub_ip = "2.2.2.2";
		string sub_port = "33333";

		TestSystem.Print("Enter를 누르면 시작합니다.");
		TestSystem.GetCommand();
#else
		TestSystem.Print("MainServer의 UDP 주소\n");
		TestSystem.Print("  IP 입력 : ");
		string main_ip = TestSystem.GetCommand();
		TestSystem.Print("  First Port 입력 : ");
		string main_port1 = TestSystem.GetCommand();
		TestSystem.Print("  Second Port 입력 : ");
		string main_port2 = TestSystem.GetCommand();

		TestSystem.PrintLine();

		TestSystem.Print("SubServer의 UDP 주소\n");
		TestSystem.Print("  IP 입력 : ");
		string sub_ip = TestSystem.GetCommand();
		TestSystem.Print("  Port 입력 : ");
		string sub_port = TestSystem.GetCommand();

		TestSystem.PrintLine();
#endif

		Config.OnEventDelegate = (string a_msg) =>
		{
			TestSystem.PrintLine(a_msg);
		};
		Config.OnErrorDelegate = (string a_msg) =>
		{
			TestSystem.PrintLine(a_msg);
		};

		Client testClient = new Client(new IPEndPoint(IPAddress.Parse(main_ip), int.Parse(main_port1)),
									   new IPEndPoint(IPAddress.Parse(main_ip), int.Parse(main_port2)),
									   new IPEndPoint(IPAddress.Parse(sub_ip), int.Parse(sub_port)));
		var result = testClient.StartTest();
		TestSystem.PrintLine();
		TestSystem.PrintLine("Test Result\n" + result.ToString() + "\n");
	}
}
