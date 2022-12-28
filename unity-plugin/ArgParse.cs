using System;
using UnityEngine;

public enum XRServerStartup
{
    None,
    Inbuilt,
    SourcePython,
}

public struct Config
{
    public XRServerStartup xrServerStartup = XRServerStartup.Inbuilt;
    public string xrServerStartupPath = "";

    public Config() { }
}

public class ArgParse
{
    public static Config? ParseArgs()
    {
        var config = new Config();

        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // XR Server Startup
            if (arg == "-tca-vr-server-startup")
            {
                if (i + 1 >= args.Length)
                {
                    Debug.LogError("Expected an argument after -tca-vr-server-startup, found nothing.");
                    Application.Quit(653);
                    return null;
                }

                var option = args[++i];
                if (option == "None")
                    config.xrServerStartup = XRServerStartup.None;
                else if (option == "Inbuilt")
                    config.xrServerStartup = XRServerStartup.Inbuilt;
                else // Parse the option as a path for SourcePython
                {
                    // Make sure we have a valid path to a folder, it pointing to the right folder is assumed
                    if (!System.IO.Directory.Exists(option))
                    {
                        Debug.LogError($"Unexpected argument after -tca-vr-server-startup, expected \"None\", \"Inbuilt\" or a valid path to the xr server project but found \"{option}\".");
                        Application.Quit(654);
                        return null;
                    };
                    config.xrServerStartup = XRServerStartup.SourcePython;
                    config.xrServerStartupPath = option;
                }
            }
        }

        return config;
    }
}