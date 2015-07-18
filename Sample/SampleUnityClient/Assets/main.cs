using UnityEngine;
using System.Collections;
using System.Threading;
using System;

public class main : MonoBehaviour 
{
	Thread m_mainThread = null;
	bool m_terminate = false;

	void Start ()
	{
		m_mainThread = new Thread((object p) =>
		{
			try {
				TestSystem.PrintLine("Start MainThread");	
				Main();
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

	void Main()
	{
		TestSystem.PrintLine("Hello World");
		TestSystem.PrintLine("Wait For Input...");
		string str = TestSystem.GetCommand();

		TestSystem.PrintLine("str : " + str);
	}
}
