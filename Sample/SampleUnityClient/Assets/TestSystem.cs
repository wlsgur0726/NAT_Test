using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

public class TestSystem : MonoBehaviour
{
	static Semaphore InputEvent = new Semaphore(0, int.MaxValue);
	static Queue<string> InputStrings = new Queue<string>();
	static Queue<string> OutputStrings = new Queue<string>();


	public static bool TryGetOutputString(out string a_str)
	{
		a_str = null;
		bool exist;
		lock (OutputStrings) {
			exist = OutputStrings.Count > 1;
			if (exist)
				a_str = OutputStrings.Dequeue();
		}
		return exist;
	}


	public static void PrintLine(string a_msg)
	{
		lock (OutputStrings) {
			OutputStrings.Enqueue(a_msg);
		}
	}


	public static void EnqueueInputString(string a_string)
	{
		PrintLine(a_string);
		lock (InputStrings) {
			InputStrings.Enqueue(a_string);
		}
		InputEvent.Release();
	}


	public static string GetCommand()
	{
		InputEvent.WaitOne();
		lock (InputStrings) {
			return InputStrings.Dequeue();
		}
	}

	
	void Start()
	{
	}

	void Update()
	{
	}
}
