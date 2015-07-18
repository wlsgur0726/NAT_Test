using UnityEngine;
using System.Collections;
using System.Threading;
using System;

public class Main : MonoBehaviour
{
	bool m_terminate = false;
	Thread m_mainThread = null;

	void Start () 
	{
		m_mainThread = new Thread((object p) =>
		{
			try {
				TestSystem.PrintLine("Start MainThread");
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
				TestSystem.PrintLine("End MainThread");	
				m_mainThread = null;
		}
	}
	

	void TestMain()
	{
		TestSystem.PrintLine("Hello World");
		TestSystem.PrintLine("Wait For Input...");
		string str = TestSystem.GetCommand();

		TestSystem.PrintLine("str : " + str);
	}
}
