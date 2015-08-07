﻿using UnityEngine;
using UnityEngine.UI;

public class ButtonClicked : MonoBehaviour 
{
	InputField m_input = null;
	InputField m_output = null;
	
	void Start()
	{
		m_input = GameObject.Find("StdIn").GetComponent<InputField>();
		m_output = GameObject.Find("StdOut").GetComponent<InputField>();
		//m_output.enabled = false;
	}
	
	public void OnEnterClicked()
	{
		string newCommand = m_input.text;
		TestSystem.EnqueueInputString(newCommand);

		m_input.text = "";
		m_input.MoveTextStart(false);
	}

	public void OnClearClicked()
	{
		m_output.text = "";
		m_output.MoveTextEnd(false);
	}

	void Update()
	{
		string str;
		while (TestSystem.TryGetOutputString(out str)) {
			m_output.text += str;
			m_output.MoveTextEnd(false);
		}

		if (Input.GetKey(KeyCode.Escape)) {
			Application.Quit();
			return;
		}
	}
}
