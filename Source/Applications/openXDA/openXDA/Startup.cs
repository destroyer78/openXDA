﻿//******************************************************************************************************
//  Startup.cs - Gbtc
//
//  Copyright © 2016, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  01/12/2016 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Net;
using System.Web.Http;
using GSF.Configuration;
using GSF.Web.Hosting;
using GSF.Web.Security;
using openXDA.Adapters;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Json;
using Newtonsoft.Json;
using Owin;
using openXDA.Model;

namespace openXDA
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Modify the JSON serializer to serialize dates as UTC - otherwise, timezone will not be appended
            // to date strings and browsers will select whatever timezone suits them
            JsonSerializerSettings settings = JsonUtility.CreateDefaultSerializerSettings();
            settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

            JsonSerializer serializer = JsonSerializer.Create(settings);

            GlobalHost.DependencyResolver.Register(typeof(JsonSerializer), () => serializer);

            // Load security hub in application domain before establishing SignalR hub configuration
            using (new SecurityHub()) { }
            using (new openXDA.Adapters.JSONApiController()) { }
            //using (new GrafanaController()) { }

            // Configuration Windows Authentication for self-hosted web service
            HttpListener listener = (HttpListener)app.Properties["System.Net.HttpListener"];
            listener.AuthenticationSchemeSelectorDelegate = AuthenticationSchemeForClient;

            HubConfiguration hubConfig = new HubConfiguration();
            HttpConfiguration httpConfig = new HttpConfiguration();

            // Setup resolver for web page controller instances
            httpConfig.DependencyResolver = WebPageController.GetDependencyResolver(WebServer.Default, Program.Host.DefaultWebPage, new AppModel(), typeof(AppModel));

#if DEBUG
            // Enabled detailed client errors
            hubConfig.EnableDetailedErrors = true;
#endif

            string allowedDomainList = ConfigurationFile.Current.Settings["systemSettings"]["AllowedDomainList"]?.Value;

            if (allowedDomainList == "*")
                app.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);
            else if ((object)allowedDomainList != null)
                httpConfig.EnableCors(new System.Web.Http.Cors.EnableCorsAttribute(allowedDomainList, "*", "*"));

            // Load ServiceHub SignalR class
            app.MapSignalR(hubConfig);
            
            // Map custom API controllers
            httpConfig.Routes.MapHttpRoute(
                name: "CustomAPIs",
                routeTemplate: "api/{controller}/{action}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Set configuration to use reflection to setup routes
            httpConfig.MapHttpAttributeRoutes();

           // Load the WebPageController class and assign its routes
            app.UseWebApi(httpConfig);

            // Check for configuration issues before first request
            httpConfig.EnsureInitialized();
        }

        private static AuthenticationSchemes AuthenticationSchemeForClient(HttpListenerRequest request)
        {
            if (request.Url.PathAndQuery.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                return AuthenticationSchemes.Anonymous;

            // Explicitly select NTLM, since Negotiate seems to fail
            // when accessing the page using the system's domain name
            // while the application is running as a domain account
            return AuthenticationSchemes.Ntlm;
        }
    }
}
