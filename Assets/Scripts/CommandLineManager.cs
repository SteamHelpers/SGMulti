using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class CommandLineManager
{

    public static string[] GetCommandLineArgs()
    {
        return Environment.GetCommandLineArgs();
    }

    private static bool _IsServer;
    private static bool SearchedIsServer;

    public static bool IsServer
    {
        get
        {
            if (SearchedIsServer)
                return _IsServer;
            foreach(string arg in GetCommandLineArgs())
            {
                if(arg == "-server")
                {
                    _IsServer = true;
                    break;
                }
            }
            SearchedIsServer = true;
            return _IsServer;
        }
    }

}
