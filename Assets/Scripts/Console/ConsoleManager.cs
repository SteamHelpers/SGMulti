using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Windows;

public class ConsoleManager : MonoBehaviour {

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

    ConsoleWindow console = new ConsoleWindow();
    ConsoleInput input = new ConsoleInput();

    string strInput;

    //
    // Create console window, register callbacks
    //
    void Awake()
    {
        if (!CommandLineManager.IsServer)
            return;
        DontDestroyOnLoad(gameObject);

        console.Initialize();
        console.SetTitle(Application.productName + " Server");

        input.OnInputText += OnInputText;

        Application.logMessageReceived += HandleLog;
        

        Debug.Log("Console Started, connecting to steam.");
        
    }

    private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
    {
        using (System.IO.StreamWriter writer = new System.IO.StreamWriter("serverlog.log"))
            writer.WriteLine(String.Format("[{0}][{1}] {2}"), DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), type, condition);
    }

    IEnumerator help(string[] args)
    {
        foreach(string str in args)
        {
            Debug.LogFormat(this, "Argument: {0}", str);
        }
        Debug.Log("Help");
        yield return new WaitForEndOfFrame();
    }

    //
    // Text has been entered into the console
    // Run it as a console command
    //
    void OnInputText(string obj)
    {
        bool causeCrash = false;
        try
        {
            string[] args = obj.Split(' ');
            string command = args[0].ToLower();
            if(args.Length > 1)
            {
                // Arguments
                List<string> nargs = new List<string>();
                for(int i = 1; i < args.Length; i++)
                {
                    nargs.Add(args[i]);
                }

                if (command == "crash")
                    causeCrash = true;
                else
                    StartCoroutine(command, nargs.ToArray());
            }
            else
            {
                // No Arguments
                StartCoroutine(command);
            }
        }
        catch(Exception ex)
        {
            Debug.LogError("An error ocurred while running your command.");
            Debug.LogException(ex, this);
        }

        if (causeCrash)
            while (true) { } // HEEERRRREEEE WWEEEEEE HGOOOOOOOO!!!!!
    }

    //
    // Debug.Log* callback
    //
    void HandleLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
            System.Console.ForegroundColor = ConsoleColor.Yellow;
        else if (type == LogType.Error)
            System.Console.ForegroundColor = ConsoleColor.Red;
        else
            System.Console.ForegroundColor = ConsoleColor.White;

        // We're half way through typing something, so clear this line ..
        if (Console.CursorLeft != 0)
            input.ClearLine();

        System.Console.WriteLine(message);

        // If we were typing something re-add it.
        input.RedrawInputLine();
        Application_logMessageReceived(message, stackTrace, type);
    }

    //
    // Update the input every frame
    // This gets new key input and calls the OnInputText callback
    //
    void Update()
    {
        if (!CommandLineManager.IsServer)
            return;
        input.Update();
    }

    //
    // It's important to call console.ShutDown in OnDestroy
    // because compiling will error out in the editor if you don't
    // because we redirected output. This sets it back to normal.
    //
    void OnDestroy()
    {
        if (!CommandLineManager.IsServer)
            return;
        console.Shutdown();
    }

#endif
}
