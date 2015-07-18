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
		m_mainThread = new Thread((object p) =>
		{
			try {
				TestSystem.PrintLine("Start MainThread\n");
				TestMain();
			}
			catch (Exception e) {
				TestSystem.PrintLine(e.ToString());
			}
			finally {
				m_terminate = true;
			}
		});
		m_mainThread.Start();
	}
	

	void Update () 
	{
		if (m_terminate && m_mainThread!=null) {
			if (m_mainThread.Join(0))
				TestSystem.PrintLine("\nEnd MainThread\n");	
				m_mainThread = null;
		}
	}
	

	static void TestMain()
	{
#if true
		string ip1 = "1.1.1.1";	
		string port1 = "1111";

		string ip2 = "1.1.1.1";
		string port2 = "2222";

		string ip3 = "3.3.3.3";
		string port3 = "3333";
#else
		TestSystem.Print("MainServer의 First UDP 주소\n");
		TestSystem.Print("  IP 입력 : ");
		string ip1 = TestSystem.GetCommand();
		TestSystem.Print("  Port 입력 : ");
		string port1 = TestSystem.GetCommand();

		TestSystem.PrintLine();

		TestSystem.Print("MainServer의 Second UDP 주소\n");
		TestSystem.Print("  IP 입력 : ");
		string ip2 = TestSystem.GetCommand();
		TestSystem.Print("  Port 입력 : ");
		string port2 = TestSystem.GetCommand();

		TestSystem.PrintLine();

		TestSystem.Print("SubServer의 UDP 주소\n");
		TestSystem.Print("  IP 입력 : ");
		string ip3 = TestSystem.GetCommand();
		TestSystem.Print("  Port 입력 : ");
		string port3 = TestSystem.GetCommand();

		TestSystem.PrintLine();
#endif

		Config.OnEventDelegate += (string a_msg) =>
		{
			TestSystem.PrintLine(a_msg);
		};
		Config.OnErrorDelegate += (string a_msg) =>
		{
			TestSystem.PrintLine(a_msg);
		};

		Client testClient = new Client(new IPEndPoint(IPAddress.Parse(ip1), int.Parse(port1)),
									   new IPEndPoint(IPAddress.Parse(ip2), int.Parse(port2)),
									   new IPEndPoint(IPAddress.Parse(ip3), int.Parse(port3)));
		var result = testClient.StartTest();
		TestSystem.PrintLine();
		TestSystem.PrintLine("Test Result\n" + result.ToString() + "\n");
	}
}
