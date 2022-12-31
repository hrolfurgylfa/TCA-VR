#nullable enable

using System;
using System.Diagnostics;
using BepInEx.Logging;
using UnityEngine;

namespace TCA_VR.Extras;

public class XRServerRunner : IDisposable
{
    private Process? process = null;
    private EventHandler? processEventHandler = null;
    private ManualLogSource Logger;

    public XRServerRunner(ManualLogSource logger)
    {
        Logger = logger;
    }

    public void Dispose()
    {
        Logger.LogInfo("Disposing of the XRServerRunner.");
        if (process != null) Stop();
    }

    public void Start(Config config)
    {
        // Skip if the config doesn't request the XR server to be started
        if (config.xrServerStartup == XRServerStartup.None)
            return;

        if (process != null)
        {
            Logger.LogError($"XRServerRunner: Tried to start XRServerRunner while it is already running with a process: {process}");
            return;
        }
        process = new Process();
        if (config.xrServerStartup == XRServerStartup.Inbuilt)
        {
            process.StartInfo.FileName = System.IO.Path.Combine(
                Application.dataPath, "../TCA_VR-xr_server/xr_server.exe");
            process.StartInfo.Arguments = "";
        }
        else
        {
            // A lot of assumptions in this path below. Assumes virtual environment is named
            // env and that its Windows because of "Scripts" instated of "bin" for Linux.
            process.StartInfo.FileName = System.IO.Path.Combine(
                config.xrServerStartupPath, "xr-server/env/Scripts/python.exe");
            process.StartInfo.Arguments = System.IO.Path.Combine(
                config.xrServerStartupPath, "xr-server/src/main.py");
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        processEventHandler = new EventHandler((sender, e) => { Stop(); });
        process.Exited += processEventHandler;
        string logHeader = "[XR Server] ";
        process.OutputDataReceived += (sender, args) => { Logger.LogInfo(logHeader + args.Data); };
        process.ErrorDataReceived += (sender, args) => { Logger.LogError(logHeader + args.Data); };
        process.Start();
        process.BeginOutputReadLine();
    }

    public void Stop()
    {
        if (process == null)
        {
            Logger.LogError("XRServerRunner: Tried to Stop while no process was running.");
            return;
        }
        if (processEventHandler == null)
        {
            Logger.LogError("XRServerRunner: Tried to Stop but the processEventHandler was null.");
            return;
        }

        // Kill the process
        process.Exited -= processEventHandler;
        processEventHandler = null;
        try { process.Kill(); }
        // The process is already dead
        catch (System.InvalidOperationException) { }
        process = null;
    }
}