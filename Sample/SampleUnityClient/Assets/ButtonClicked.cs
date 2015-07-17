using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using NAT_Test;

public class ButtonClicked : MonoBehaviour 
{
	InputField StdIn = null;
	InputField StdOut = null;

	// Use this for initialization
	void Start()
	{
		StdIn = GameObject.Find("StdIn").GetComponent<InputField>();
		StdOut = GameObject.Find("StdOut").GetComponent<InputField>();
		StdOut.text = "> ";
		StdIn.Select();
		//StdOut.enabled = false;

		Client client = new Client(null, null, null);
		client.StartTest();
	}
	
	// Update is called once per frame
	void Update () {
	}

	public void OnEnterClicked()
	{
		string newCommand = StdIn.text;
		StdIn.text = "";
		StdOut.text += newCommand + "\n> ";
		StdOut.MoveTextEnd(false);
		StdIn.MoveTextStart(false);
		StdIn.Select();
		Debug.Log("OnEnterClicked : " + newCommand);
	}

	public void OnClearClicked()
	{
		StdOut.text = "> ";
		StdOut.MoveTextEnd(false);
		Debug.Log("OnClearClicked");
	}
	
}
