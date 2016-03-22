using UnityEngine;
using UnityEngine.UI;
using System;

public class ButtonClicked : MonoBehaviour 
{
	InputField m_input = null;
	InputField m_output = null;
	
	void Start()
	{
		m_input = GameObject.Find("StdIn").GetComponent<InputField>();
		m_output = GameObject.Find("StdOut").GetComponent<InputField>();
		//m_output.enabled = false;

		// 해상도에 따른 폰트 크기 조절
		double screenSize = Math.Sqrt(Math.Pow(Screen.height, 2) + Math.Pow(Screen.width, 2));
		int fontSize = (int)(screenSize / 50);
		m_input.placeholder.GetComponent<Text>().fontSize = (int)(fontSize * 0.75);
		m_input.textComponent.fontSize = fontSize;
		m_output.textComponent.fontSize = fontSize;
		GameObject.Find("EnterButton").GetComponent<Button>().GetComponentInChildren<Text>().fontSize = fontSize;
		GameObject.Find("ClearButton").GetComponent<Button>().GetComponentInChildren<Text>().fontSize = fontSize;
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
