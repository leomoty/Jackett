﻿using Jackett.Common.Models.Config;
using Jackett.Common.Services;
using Jackett.Common.Services.Interfaces;
using Jackett.Common.Utils;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace Jackett.Service
{
    public partial class Service : ServiceBase
    {
        private IProcessService processService;
        private Process consoleProcess;
        private Logger logger;
        private bool serviceStopInitiated;

        public Service()
        {
            InitializeComponent();

            RuntimeSettings runtimeSettings = new RuntimeSettings()
            {
                CustomLogFileName = "ServiceLog.txt"
            };

            LogManager.Configuration = LoggingSetup.GetLoggingConfiguration(runtimeSettings);
            logger = LogManager.GetCurrentClassLogger();

            logger.Info("Initiating Jackett Service v" + EnvironmentUtil.JackettVersion);

            processService = new ProcessService(logger);
        }

        protected override void OnStart(string[] args)
        {
            logger.Info("Service starting");
            serviceStopInitiated = false;
            StartConsoleApplication();
        }

        protected override void OnStop()
        {
            logger.Info("Service stopping");
            serviceStopInitiated = true;
            StopConsoleApplication();
        }

        private void StartConsoleApplication()
        {
            string applicationFolder = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            var exePath = Path.Combine(applicationFolder, "JackettConsole.exe");

            var startInfo = new ProcessStartInfo()
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                FileName = exePath,
                RedirectStandardInput = true,
                RedirectStandardError = true
            };

            consoleProcess = Process.Start(startInfo);
            consoleProcess.EnableRaisingEvents = true;
            consoleProcess.Exited += ProcessExited;
            consoleProcess.ErrorDataReceived += ProcessErrorDataReceived;
        }

        private void ProcessErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            logger.Error(e.Data);
        }

        private void ProcessExited(object sender, EventArgs e)
        {
            logger.Info("Console process exited");

            if (!serviceStopInitiated)
            {
                logger.Info("Service stop not responsible for process exit");
                Stop();
            }
        }

        private void StopConsoleApplication()
        {
            if (consoleProcess != null && !consoleProcess.HasExited)
            {
                consoleProcess.StandardInput.Close();
                consoleProcess.WaitForExit(2000);
                if (consoleProcess != null && !consoleProcess.HasExited)
                {
                    consoleProcess.Kill();
                }
            }
        }
    }
}
