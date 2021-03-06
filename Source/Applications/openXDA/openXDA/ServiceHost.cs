﻿//*********************************************************************************************************************
// ServiceHost.cs
// Version 1.1 and subsequent releases
//
//  Copyright © 2013, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
// --------------------------------------------------------------------------------------------------------------------
//
// Version 1.0
//
// Copyright 2012 ELECTRIC POWER RESEARCH INSTITUTE, INC. All rights reserved.
//
// openXDA ("this software") is licensed under BSD 3-Clause license.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the 
// following conditions are met:
//
// •    Redistributions of source code must retain the above copyright  notice, this list of conditions and 
//      the following disclaimer.
//
// •    Redistributions in binary form must reproduce the above copyright notice, this list of conditions and 
//      the following disclaimer in the documentation and/or other materials provided with the distribution.
//
// •    Neither the name of the Electric Power Research Institute, Inc. (“EPRI”) nor the names of its contributors 
//      may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
// DISCLAIMED. IN NO EVENT SHALL EPRI BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL 
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; 
// OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, 
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
// POSSIBILITY OF SUCH DAMAGE.
//
//
// This software incorporates work covered by the following copyright and permission notice: 
//
// •    TVA Code Library 4.0.4.3 - Tennessee Valley Authority, tvainfo@tva.gov
//      No copyright is claimed pursuant to 17 USC § 105. All Other Rights Reserved.
//
//      Licensed under TVA Custom License based on NASA Open Source Agreement (TVA Custom NOSA); 
//      you may not use TVA Code Library except in compliance with the TVA Custom NOSA. You may  
//      obtain a copy of the TVA Custom NOSA at http://tvacodelibrary.codeplex.com/license.
//
//      TVA Code Library is provided by the copyright holders and contributors "as is" and any express 
//      or implied warranties, including, but not limited to, the implied warranties of merchantability 
//      and fitness for a particular purpose are disclaimed.
//
//*********************************************************************************************************************
//
//  Code Modification History:
//  -------------------------------------------------------------------------------------------------------------------
//  09/10/2012 - Stephen C. Wills, Grid Protection Alliance
//       Generated original version of source code.
//
//*********************************************************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GSF;
using GSF.Configuration;
using GSF.Console;
using GSF.Data;
using GSF.Identity;
using GSF.IO;
using GSF.Reflection;
using GSF.Security.Model;
using GSF.ServiceProcess;
using GSF.Web.Hosting;
using GSF.Web.Model;
using GSF.Web.Security;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using Microsoft.Owin.Hosting;
using openXDA.Logging;
using openXDA.Model;
using Channel = openXDA.Model.Channel;
using Meter = openXDA.Model.Meter;
using MeterLine = openXDA.Model.MeterLine;
using MeterLocation = openXDA.Model.MeterLocation;
using MeterMeterGroup = openXDA.Model.MeterMeterGroup;
using Setting = openXDA.Model.Setting;

namespace openXDA
{
    public partial class ServiceHost : ServiceBase
    {
        #region [ Members ]

        // Events

        /// <summary>
        /// Raised when there is a new status message reported to service.
        /// </summary>
        public event EventHandler<EventArgs<Guid, string, UpdateType>> UpdatedStatus;

        /// <summary>
        /// Raised when there is a new exception logged to service.
        /// </summary>
        public event EventHandler<EventArgs<Exception>> LoggedException;


        // Fields
        private ServiceMonitors m_serviceMonitors;
        private ExtensibleDisturbanceAnalysisEngine m_extensibleDisturbanceAnalysisEngine;
        private Thread m_startEngineThread;
        private bool m_serviceStopping;
        private IDisposable m_webAppHost;
        private bool m_disposed;



        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the configured default web page for the application.
        /// </summary>
        public string DefaultWebPage
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the model used for the application.
        /// </summary>
        public AppModel Model
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets current performance statistics.
        /// </summary>
        public string PerformanceStatistics => m_extensibleDisturbanceAnalysisEngine.Status;

        #endregion

        #region [ Constructors ]

        public ServiceHost()
        {
            InitializeComponent();

            // Register event handlers.
            m_serviceHelper.ServiceStarted += ServiceHelper_ServiceStarted;
            m_serviceHelper.ServiceStopping += ServiceHelper_ServiceStopping;
        }

        public ServiceHost(IContainer container)
            : this()
        {
            if (container != null)
                container.Add(this);
        }

        #endregion

        #region [ Methods ]

        private void WebServer_StatusMessage(object sender, EventArgs<string> e)
        {
            //DisplayStatusMessage(e.Argument, UpdateType.Information);
        }

        private void ServiceHelper_ServiceStarted(object sender, EventArgs e)
        {
            ServiceHelperAppender serviceHelperAppender;
            RollingFileAppender fileAppender;

            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // Set current working directory to fix relative paths
            Directory.SetCurrentDirectory(FilePath.GetAbsolutePath(""));

            // Set up logging
            serviceHelperAppender = new ServiceHelperAppender(m_serviceHelper);

            fileAppender = new RollingFileAppender();
            fileAppender.StaticLogFileName = false;
            fileAppender.AppendToFile = true;
            fileAppender.RollingStyle = RollingFileAppender.RollingMode.Composite;
            fileAppender.MaxSizeRollBackups = 10;
            fileAppender.PreserveLogFileNameExtension = true;
            fileAppender.MaximumFileSize = "1MB";
            fileAppender.Layout = new PatternLayout("%date [%thread] %-5level %logger - %message%newline");

            try
            {
                if (!Directory.Exists("Debug"))
                    Directory.CreateDirectory("Debug");

                fileAppender.File = @"Debug\openXDA.log";
            }
            catch (Exception ex)
            {
                fileAppender.File = "openXDA.log";
                m_serviceHelper.ErrorLogger.Log(ex);
            }

            fileAppender.ActivateOptions();
            BasicConfigurator.Configure(serviceHelperAppender, fileAppender);

            // Set up heartbeat and client request handlers
            m_serviceHelper.AddScheduledProcess(ServiceHeartbeatHandler, "ServiceHeartbeat", "* * * * *");
            m_serviceHelper.AddScheduledProcess(ReloadConfigurationHandler, "ReloadConfiguration", "0 0 * * *");
            m_serviceHelper.ClientRequestHandlers.Add(new ClientRequestHandler("ReloadSystemSettings", "Reloads system settings from the database", ReloadSystemSettingsRequestHandler));
            m_serviceHelper.ClientRequestHandlers.Add(new ClientRequestHandler("EngineStatus", "Displays status information about the XDA engine", EngineStatusHandler));
            m_serviceHelper.ClientRequestHandlers.Add(new ClientRequestHandler("TweakFileProcessor", "Modifies the behavior of the file processor at runtime", TweakFileProcessorHandler));
            m_serviceHelper.ClientRequestHandlers.Add(new ClientRequestHandler("MsgServiceMonitors", "Sends a message to all service monitors", MsgServiceMonitorsRequestHandler));

            // Set up adapter loader to load service monitors
            m_serviceMonitors = new ServiceMonitors();
            m_serviceMonitors.AdapterCreated += ServiceMonitors_AdapterCreated;
            m_serviceMonitors.AdapterLoaded += ServiceMonitors_AdapterLoaded;
            m_serviceMonitors.AdapterUnloaded += ServiceMonitors_AdapterUnloaded;
            m_serviceMonitors.Initialize();

            // Set up the analysis engine
            m_extensibleDisturbanceAnalysisEngine = new ExtensibleDisturbanceAnalysisEngine();

            // Set up separate thread to start the engine
            m_startEngineThread = new Thread(() =>
            {
                const int RetryDelay = 1000;
                const int SleepTime = 200;
                const int LoopCount = RetryDelay / SleepTime;

                bool engineStarted = false;
                bool webUIStarted = false;

                while (true)
                {
                    engineStarted = engineStarted || TryStartEngine();
                    webUIStarted = webUIStarted || TryStartWebUI();

                    if (engineStarted && webUIStarted)
                        break;

                    for (int i = 0; i < LoopCount; i++)
                    {
                        if (m_serviceStopping)
                            return;

                        Thread.Sleep(SleepTime);
                    }
                }
            });

            m_startEngineThread.Start();
        }

        private void ServiceHelper_ServiceStopping(object sender, EventArgs e)
        {
            if (!m_disposed)
            {
                try
                {
                   m_webAppHost?.Dispose();
                }
                finally
                {
                    m_disposed = true;          // Prevent duplicate dispose.
                    base.Dispose();    // Call base class Dispose().
                }
            }

            // If the start engine thread is still
            // running, wait for it to stop
            m_serviceStopping = true;
            m_startEngineThread.Join();

            // Dispose of adapter loader for service monitors
            m_serviceMonitors.AdapterLoaded -= ServiceMonitors_AdapterLoaded;
            m_serviceMonitors.AdapterUnloaded -= ServiceMonitors_AdapterUnloaded;
            m_serviceMonitors.Dispose();

            // Dispose of the analysis engine
            m_extensibleDisturbanceAnalysisEngine.Stop();
            m_extensibleDisturbanceAnalysisEngine.Dispose();

            // Save updated settings to the configuration file
            ConfigurationFile.Current.Save();
        }

        // Attempts to start the engine and logs startup errors.
        private bool TryStartEngine()
        {
            try
            {
                // Start the analysis engine
                m_extensibleDisturbanceAnalysisEngine.Start();
                return true;
            }
            catch (Exception ex)
            {
                string message;

                // Stop the analysis engine
                m_extensibleDisturbanceAnalysisEngine.Stop();

                // Log the exception
                message = "Failed to start XDA engine due to exception: " + ex.Message;
                HandleException(new InvalidOperationException(message, ex));

                return false;
            }
        }

        // Attempts to start the web UI and logs startup errors.
        private bool TryStartWebUI()
        {
            try
            {
                ConfigurationFile.Current.Reload();
                AdoDataConnection.ReloadConfigurationSettings();

                CategorizedSettingsElementCollection systemSettings = ConfigurationFile.Current.Settings["systemSettings"];
                CategorizedSettingsElementCollection securityProvider = ConfigurationFile.Current.Settings["securityProvider"];

                systemSettings.Add("DataProviderString", "AssemblyName={System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089}; ConnectionType=System.Data.SqlClient.SqlConnection; AdapterType=System.Data.SqlClient.SqlDataAdapter", "Configuration database ADO.NET data provider assembly type creation string used when ConfigurationType=Database");
                systemSettings.Add("NodeID", "00000000-0000-0000-0000-000000000000", "Unique Node ID");
                systemSettings.Add("CompanyName", "Grid Protection Alliance", "The name of the company who owns this instance of the openMIC.");
                systemSettings.Add("CompanyAcronym", "GPA", "The acronym representing the company who owns this instance of the openMIC.");
                systemSettings.Add("WebHostURL", "http://+:8989", "The web hosting URL for remote system management.");
                systemSettings.Add("DefaultWebPage", "index.cshtml", "The default web page for the hosted web server.");
                systemSettings.Add("DateFormat", "MM/dd/yyyy", "The default date format to use when rendering timestamps.");
                systemSettings.Add("TimeFormat", "HH:mm.ss.fff", "The default time format to use when rendering timestamps.");
                systemSettings.Add("BootstrapTheme", "Content/bootstrap.min.css", "Path to Bootstrap CSS to use for rendering styles.");

                securityProvider.Add("ConnectionString", "Eval(systemSettings.ConnectionString)", "Connection connection string to be used for connection to the backend security datastore.");
                securityProvider.Add("DataProviderString", "Eval(systemSettings.DataProviderString)", "Configuration database ADO.NET data provider assembly type creation string to be used for connection to the backend security datastore.");

                ValidateAccountsAndGroups(new AdoDataConnection("securityProvider"));

                DefaultWebPage = systemSettings["DefaultWebPage"].Value;

                Model = new AppModel();
                Model.Global.CompanyName = systemSettings["CompanyName"].Value;
                Model.Global.CompanyAcronym = systemSettings["CompanyAcronym"].Value;
                Model.Global.ApplicationName = "openXDA";
                Model.Global.ApplicationDescription = "open eXtensible Disturbance Analytics";
                Model.Global.ApplicationKeywords = "open source, utility, software, meter, interrogation";
                Model.Global.DateFormat = systemSettings["DateFormat"].Value;
                Model.Global.TimeFormat = systemSettings["TimeFormat"].Value;
                Model.Global.DateTimeFormat = $"{Model.Global.DateFormat} {Model.Global.TimeFormat}";
                Model.Global.BootstrapTheme = systemSettings["BootstrapTheme"].Value;

                // Attach to default web server events
                WebServer webServer = WebServer.Default;
                webServer.StatusMessage += WebServer_StatusMessage;

                // Define types for Razor pages - self-hosted web service does not use view controllers so
                // we must define configuration types for all paged view model based Razor views here:
                webServer.PagedViewModelTypes.TryAdd("Config/Users.cshtml", new Tuple<Type, Type>(typeof(UserAccount), typeof(SecurityHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/Groups.cshtml", new Tuple<Type, Type>(typeof(SecurityGroup), typeof(SecurityHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/Settings.cshtml", new Tuple<Type, Type>(typeof(Setting), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/XSLTemplate.cshtml", new Tuple<Type, Type>(typeof(XSLTemplate), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Assets/Meters.cshtml", new Tuple<Type, Type>(typeof(Meter), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Assets/Sites.cshtml", new Tuple<Type, Type>(typeof(MeterLocation), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/MeterGroups.cshtml", new Tuple<Type, Type>(typeof(MeterGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/LineGroups.cshtml", new Tuple<Type, Type>(typeof(LineGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/MeterMeterGroupView.cshtml", new Tuple<Type, Type>(typeof(MeterMeterGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/LineLineGroupView.cshtml", new Tuple<Type, Type>(typeof(LineLineGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Assets/Lines.cshtml", new Tuple<Type, Type>(typeof(LineView), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Assets/MeterLines.cshtml", new Tuple<Type, Type>(typeof(MeterLine), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Assets/Channels.cshtml", new Tuple<Type, Type>(typeof(Channel), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/DashSettings.cshtml", new Tuple<Type, Type>(typeof(DashSettings), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/UserDashSettings.cshtml", new Tuple<Type, Type>(typeof(UserDashSettings), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/AlarmSettings.cshtml", new Tuple<Type, Type>(typeof(AlarmRangeLimitView), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/DefaultAlarmSettings.cshtml", new Tuple<Type, Type>(typeof(DefaultAlarmRangeLimitView), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/UserAccountMeterGroupView.cshtml", new Tuple<Type, Type>(typeof(UserAccountMeterGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/EmailTypes.cshtml", new Tuple<Type, Type>(typeof(EmailType), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/EmailGroups.cshtml", new Tuple<Type, Type>(typeof(EmailGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/EmailGroupType.cshtml", new Tuple<Type, Type>(typeof(EmailGroupType), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/EmailGroupMeterGroup.cshtml", new Tuple<Type, Type>(typeof(EmailGroupMeterGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/EmailGroupLineGroup.cshtml", new Tuple<Type, Type>(typeof(EmailGroupLineGroup), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Config/EmailGroupUserAccount.cshtml", new Tuple<Type, Type>(typeof(EmailGroupUserAccount), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/Filters.cshtml", new Tuple<Type, Type>(typeof(WorkbenchFilter), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/Events.cshtml", new Tuple<Type, Type>(typeof(Event), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/Event.cshtml", new Tuple<Type, Type>(typeof(SingleEvent), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/Breaker.cshtml", new Tuple<Type, Type>(typeof(BreakerOperation), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/EventsForDate.cshtml", new Tuple<Type, Type>(typeof(EventForDate), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/EventsForDay.cshtml", new Tuple<Type, Type>(typeof(EventForDay), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/BreakersForDay.cshtml", new Tuple<Type, Type>(typeof(BreakersForDay), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/EventsForMeter.cshtml", new Tuple<Type, Type>(typeof(EventForMeter), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/MeterEventsByLine.cshtml", new Tuple<Type, Type>(typeof(MeterEventsByLine), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/FaultsDetailsByDate.cshtml", new Tuple<Type, Type>(typeof(FaultsDetailsByDate), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/DisturbancesForDay.cshtml", new Tuple<Type, Type>(typeof(DisturbancesForDay), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/DisturbancesForMeter.cshtml", new Tuple<Type, Type>(typeof(DisturbancesForMeter), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/FaultsForMeter.cshtml", new Tuple<Type, Type>(typeof(FaultForMeter), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/SiteSummaryPVM.cshtml", new Tuple<Type, Type>(typeof(SiteSummary), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/AuditLog.cshtml", new Tuple<Type, Type>(typeof(AuditLog), typeof(DataHub)));
                webServer.PagedViewModelTypes.TryAdd("Workbench/DataFiles.cshtml", new Tuple<Type, Type>(typeof(openXDA.Model.DataFile), typeof(DataHub)));

                // Create new web application hosting environment
                m_webAppHost = WebApp.Start<Startup>(systemSettings["WebHostURL"].Value);

                // Initiate pre-compile of base templates
                if (AssemblyInfo.EntryAssembly.Debuggable)
                {
                    RazorEngine<CSharpDebug>.Default.PreCompile(HandleException);
                    RazorEngine<VisualBasicDebug>.Default.PreCompile(HandleException);
                }
                else
                {
                    RazorEngine<CSharp>.Default.PreCompile(HandleException);
                    RazorEngine<VisualBasic>.Default.PreCompile(HandleException);
                }

                return true;
            }
            catch (TargetInvocationException ex)
            {
                string message;

                // Log the exception
                message = "Failed to start web UI due to exception: " + ex.InnerException.Message;
                HandleException(new InvalidOperationException(message, ex));

                return false;
            }
            catch (Exception ex)
            {
                string message;

                // Log the exception
                message = "Failed to start web UI due to exception: " + ex.Message;
                HandleException(new InvalidOperationException(message, ex));

                return false;
            }
        }

        private void ServiceHeartbeatHandler(string s, object[] args)
        {
            // Go through all service monitors to notify of the heartbeat
            foreach (IServiceMonitor serviceMonitor in m_serviceMonitors.Adapters)
            {
                try
                {
                    // If the service monitor is enabled, notify it of the heartbeat
                    if (serviceMonitor.Enabled)
                        serviceMonitor.HandleServiceHeartbeat();
                }
                catch (Exception ex)
                {
                    // Handle each service monitor's exceptions individually
                    HandleException(ex);
                }
            }
        }

        private void ReloadConfigurationHandler(string s, object[] args)
        {
            m_extensibleDisturbanceAnalysisEngine.ReloadConfiguration();
            ValidateAccountsAndGroups(new AdoDataConnection("securityProvider"));
        }

        // Reloads system settings from the database.
        private void ReloadSystemSettingsRequestHandler(ClientRequestInfo requestInfo)
        {
            m_extensibleDisturbanceAnalysisEngine.ReloadSystemSettings();
            SendResponse(requestInfo, true);
        }

        // Displays status information about the XDA engine.
        private void EngineStatusHandler(ClientRequestInfo requestInfo)
        {
            if (m_extensibleDisturbanceAnalysisEngine != null)
                DisplayResponseMessage(requestInfo, m_extensibleDisturbanceAnalysisEngine.Status);
            else
                SendResponseWithAttachment(requestInfo, false, null, "Engine is not ready.");
        }

        // Modifies the behavior of the file processor at runtime.
        private void TweakFileProcessorHandler(ClientRequestInfo requestInfo)
        {
            if (requestInfo.Request.Arguments.ContainsHelpRequest)
            {
                string helpMessage = m_extensibleDisturbanceAnalysisEngine.TweakFileProcessor(new string[] { "-?" });
                DisplayResponseMessage(requestInfo, helpMessage);
                return;
            }

            string[] args = Arguments.ToArgs(requestInfo.Request.Arguments.ToString());
            string message = m_extensibleDisturbanceAnalysisEngine.TweakFileProcessor(args);
            DisplayResponseMessage(requestInfo, message);
        }

        // Send a message to the service monitors on request.
        private void MsgServiceMonitorsRequestHandler(ClientRequestInfo requestInfo)
        {
            Arguments arguments = requestInfo.Request.Arguments;

            if (arguments.ContainsHelpRequest)
            {
                StringBuilder helpMessage = new StringBuilder();

                helpMessage.Append("Sends a message to all service monitors.");
                helpMessage.AppendLine();
                helpMessage.AppendLine();
                helpMessage.Append("   Usage:");
                helpMessage.AppendLine();
                helpMessage.Append("       MsgServiceMonitors [Options] [Args...]");
                helpMessage.AppendLine();
                helpMessage.AppendLine();
                helpMessage.Append("   Options:");
                helpMessage.AppendLine();
                helpMessage.Append("       -?".PadRight(20));
                helpMessage.Append("Displays this help message");

                DisplayResponseMessage(requestInfo, helpMessage.ToString());
            }
            else
            {
                string[] args = Enumerable.Range(1, arguments.OrderedArgCount)
                    .Select(arg => arguments[arguments.OrderedArgID + arg])
                    .ToArray();

                // Go through all service monitors and handle the message
                foreach (IServiceMonitor serviceMonitor in m_serviceMonitors.Adapters)
                {
                    try
                    {
                        // If the service monitor is enabled, notify it of the message
                        if (serviceMonitor.Enabled)
                            serviceMonitor.HandleClientMessage(args);
                    }
                    catch (Exception ex)
                    {
                        // Handle each service monitor's exceptions individually
                        HandleException(ex);
                    }
                }

                SendResponse(requestInfo, true);
            }
        }

        // Send the error to the service helper, error logger, and each service monitor
        private void HandleException(Exception ex)
        {
            string newLines = string.Format("{0}{0}", Environment.NewLine);

            m_serviceHelper.ErrorLogger.Log(ex);
            m_serviceHelper.UpdateStatus(UpdateType.Alarm, "{0}", ex.Message + newLines);

            foreach (IServiceMonitor serviceMonitor in m_serviceMonitors.Adapters)
            {
                try
                {
                    if (serviceMonitor.Enabled)
                        serviceMonitor.HandleServiceError(ex);
                }
                catch (Exception ex2)
                {
                    // Exceptions encountered while handling exceptions can be tricky,
                    // so we just log them rather than risk a recursive loop
                    m_serviceHelper.ErrorLogger.Log(ex2);
                    m_serviceHelper.UpdateStatus(UpdateType.Alarm, ex2.Message + newLines);
                }
            }
        }

        /// <summary>
        /// Validate accounts and groups to ensure that account names and group names are converted to SIDs.
        /// </summary>
        /// <param name="database">Data connection to use for database operations.</param>
        private static void ValidateAccountsAndGroups(AdoDataConnection database)
        {
            const string SelectUserAccountQuery = "SELECT ID, Name, UseADAuthentication FROM UserAccount";
            const string SelectSecurityGroupQuery = "SELECT ID, Name FROM SecurityGroup";
            const string UpdateUserAccountFormat = "UPDATE UserAccount SET Name = '{0}' WHERE ID = '{1}'";
            const string UpdateSecurityGroupFormat = "UPDATE SecurityGroup SET Name = '{0}' WHERE ID = '{1}'";

            string id;
            string sid;
            string accountName;
            Dictionary<string, string> updateMap;

            updateMap = new Dictionary<string, string>();

            // Find user accounts that need to be updated
            using (IDataReader userAccountReader = database.Connection.ExecuteReader(SelectUserAccountQuery))
            {
                while (userAccountReader.Read())
                {
                    id = userAccountReader["ID"].ToNonNullString();
                    accountName = userAccountReader["Name"].ToNonNullString();

                    if (userAccountReader["UseADAuthentication"].ToNonNullString().ParseBoolean())
                    {
                        sid = UserInfo.UserNameToSID(accountName);

                        if (!ReferenceEquals(accountName, sid) && UserInfo.IsUserSID(sid))
                            updateMap.Add(id, sid);
                    }
                }
            }

            // Update user accounts
            foreach (KeyValuePair<string, string> pair in updateMap)
                database.Connection.ExecuteNonQuery(string.Format(UpdateUserAccountFormat, pair.Value, pair.Key));

            updateMap.Clear();

            // Find security groups that need to be updated
            using (IDataReader securityGroupReader = database.Connection.ExecuteReader(SelectSecurityGroupQuery))
            {
                while (securityGroupReader.Read())
                {
                    id = securityGroupReader["ID"].ToNonNullString();
                    accountName = securityGroupReader["Name"].ToNonNullString();

                    if (accountName.Contains('\\'))
                    {
                        sid = UserInfo.GroupNameToSID(accountName);

                        if (!ReferenceEquals(accountName, sid) && UserInfo.IsGroupSID(sid))
                            updateMap.Add(id, sid);
                    }
                }
            }

            // Update security groups
            foreach (KeyValuePair<string, string> pair in updateMap)
                database.Connection.ExecuteNonQuery(string.Format(UpdateSecurityGroupFormat, pair.Value, pair.Key));
        }


        /// <summary>
        /// Logs a status message to connected clients.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="type">Type of message to log.</param>
        public void LogStatusMessage(string message, UpdateType type = UpdateType.Information)
        {
            DisplayStatusMessage(message, type);
        }


        /// <summary>
        /// Displays a broadcast message to all subscribed clients.
        /// </summary>
        /// <param name="status">Status message to send to all clients.</param>
        /// <param name="type"><see cref="UpdateType"/> of message to send.</param>
        protected virtual void DisplayStatusMessage(string status, UpdateType type)
        {
            try
            {
                status = status.Replace("{", "{{").Replace("}", "}}");
                m_serviceHelper.UpdateStatus(type, string.Format("{0}\r\n\r\n", status));
            }
            catch (Exception ex)
            {
                HandleException(ex);
                m_serviceHelper.UpdateStatus(UpdateType.Alarm, "Failed to update client status \"" + status.ToNonNullString() + "\" due to an exception: " + ex.Message + "\r\n\r\n");
            }
        }

        /// <summary>
        /// Sends a command request to the service.
        /// </summary>
        /// <param name="clientID">Client ID of sender.</param>
        /// <param name="userInput">Request string.</param>
        public void SendRequest(Guid clientID, string userInput)
        {
            ClientRequest request = ClientRequest.Parse(userInput);

            if ((object)request != null)
            {
                ClientRequestHandler requestHandler = m_serviceHelper.FindClientRequestHandler(request.Command);

                if ((object)requestHandler != null)
                    requestHandler.HandlerMethod(new ClientRequestInfo(new ClientInfo() { ClientID = clientID }, request));
                else
                    DisplayStatusMessage($"Command \"{request.Command}\" is not supported\r\n\r\n", UpdateType.Alarm);
            }
        }

        /// <summary>
        /// Sends a command request to the service to reprocess files.
        /// </summary>
        /// <param name="fileGroups">List of fileGroups to reprocess files for.</param>
        public void ReprocessFiles(Dictionary<int,int> fileGroups)
        {
            m_extensibleDisturbanceAnalysisEngine.ReprocessFiles(fileGroups);
        }

        /// <summary>
        /// Sends a command request to the service to reprocess a file.
        /// </summary>
        /// <param name="events">List of events to reprocess files for.</param>
        public void ReprocessFile(int dataFileId, int fileGroupId, int meterId)
        {
            m_extensibleDisturbanceAnalysisEngine.ReprocessFile(dataFileId ,fileGroupId, meterId);
        }

        public void DisconnectClient(Guid clientID)
        {
            m_serviceHelper.DisconnectClient(clientID);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            foreach (Exception ex in e.Exception.Flatten().InnerExceptions)
                HandleException(ex);

            e.SetObserved();
        }

        #region [ Service Monitor Handlers ]

        // Ensure that service monitors save their settings to the configuration file
        private void ServiceMonitors_AdapterCreated(object sender, EventArgs<IServiceMonitor> e)
        {
            e.Argument.PersistSettings = true;
        }

        // Display a message when service monitors are loaded
        private void ServiceMonitors_AdapterLoaded(object sender, EventArgs<IServiceMonitor> e)
        {
            m_serviceHelper.UpdateStatusAppendLine(UpdateType.Information, "{0} has been loaded", e.Argument.GetType().Name);
        }

        // Display a message when service monitors are unloaded
        private void ServiceMonitors_AdapterUnloaded(object sender, EventArgs<IServiceMonitor> e)
        {
            m_serviceHelper.UpdateStatusAppendLine(UpdateType.Information, "{0} has been unloaded", e.Argument.GetType().Name);
        }

        #endregion

        #region [ Broadcast Message Handling ]

        /// <summary>
        /// Sends an actionable response to client.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="success">Flag that determines if this response to client request was a success.</param>
        protected virtual void SendResponse(ClientRequestInfo requestInfo, bool success)
        {
            SendResponseWithAttachment(requestInfo, success, null, null);
        }

        /// <summary>
        /// Sends an actionable response to client with a formatted message and attachment.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="success">Flag that determines if this response to client request was a success.</param>
        /// <param name="attachment">Attachment to send with response.</param>
        /// <param name="status">Formatted status message to send with response.</param>
        /// <param name="args">Arguments of the formatted status message.</param>
        protected virtual void SendResponseWithAttachment(ClientRequestInfo requestInfo, bool success, object attachment, string status, params object[] args)
        {
            try
            {
                // Send actionable response
                m_serviceHelper.SendActionableResponse(requestInfo, success, attachment, status, args);

                // Log details of client request as well as response
                if (m_serviceHelper.LogStatusUpdates && m_serviceHelper.StatusLog.IsOpen)
                {
                    string responseType = requestInfo.Request.Command + (success ? ":Success" : ":Failure");
                    string arguments = requestInfo.Request.Arguments.ToString();
                    string message = responseType + (string.IsNullOrWhiteSpace(arguments) ? "" : "(" + arguments + ")");

                    if (status != null)
                    {
                        if (args.Length == 0)
                            message += " - " + status;
                        else
                            message += " - " + string.Format(status, args);
                    }

                    m_serviceHelper.StatusLog.WriteTimestampedLine(message);
                }
            }
            catch (Exception ex)
            {
                string message = string.Format("Failed to send client response due to an exception: {0}", ex.Message);
                HandleException(new InvalidOperationException(message, ex));
            }
        }

        /// <summary>
        /// Displays a response message to client requestor.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="status">Formatted status message to send to client.</param>
        protected virtual void DisplayResponseMessage(ClientRequestInfo requestInfo, string status)
        {
            DisplayResponseMessage(requestInfo, "{0}", status);
        }

        /// <summary>
        /// Displays a response message to client requestor.
        /// </summary>
        /// <param name="requestInfo"><see cref="ClientRequestInfo"/> instance containing the client request.</param>
        /// <param name="status">Formatted status message to send to client.</param>
        /// <param name="args">Arguments of the formatted status message.</param>
        protected virtual void DisplayResponseMessage(ClientRequestInfo requestInfo, string status, params object[] args)
        {
            try
            {
                m_serviceHelper.UpdateStatus(requestInfo.Sender.ClientID, UpdateType.Information, string.Format("{0}{1}{1}", status, Environment.NewLine), args);
            }
            catch (Exception ex)
            {
                string message = string.Format("Failed to update client status \"{0}\" due to an exception: {1}", status.ToNonNullString(), ex.Message);
                HandleException(new InvalidOperationException(message, ex));
            }
        }

        #endregion

        #endregion
    }
}
