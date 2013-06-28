﻿using Mono.Unix.Native;
using Mono.Unix;
using Mono.Zeroconf;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System;
using WaveBox.Model;
using WaveBox.Static;
using WaveBox.SessionManagement;
using WaveBox.TcpServer.Http;
using WaveBox.TcpServer;
using WaveBox.Transcoding;
using WaveBox.DeviceSync;
using Microsoft.Owin.Hosting;
using Cirrious.MvvmCross.Plugins.Sqlite;
using WaveBox.Core.Injected;
using Ninject;

namespace WaveBox
{
	class WaveBoxMain
	{
		// Logger
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		// Server GUID and URL, for publishing
		public string ServerGuid { get; set; }
		public string ServerUrl { get; set; }

		// HTTP server, which serves up the API
		private HttpServer httpServer;

		/// <summary>
		/// ServerSetup is used to generate a GUID which can be associated with the URL forwarding service, to 
		/// uniquely map an instance of WaveBox
		/// </summary>
		private void ServerSetup()
		{
			ISQLiteConnection conn = null;
			try
			{
				// Grab server GUID and URL from the database
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
				ServerGuid = conn.ExecuteScalar<string>("SELECT Guid FROM Server");
				ServerUrl = conn.ExecuteScalar<string>("SELECT Url FROM Server");
			}
			catch (Exception e)
			{
				logger.Error("exception loading server info", e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			// If it doesn't exist, generate a new one
			if ((object)ServerGuid == null)
			{
				// Generate the GUID
				Guid guid = Guid.NewGuid();
				ServerGuid = guid.ToString();

				// Store the GUID in the database
				try
				{
					conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
					int affected = conn.Execute("INSERT INTO Server (Guid) VALUES (?)", ServerGuid);

					if (affected == 0)
					{
						ServerGuid = null;
					}
				}
				catch (Exception e)
				{
					logger.Error("exception saving guid", e);
					ServerGuid = null;
				}
				finally
				{
					Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
				}
			}
		}

		/// <summary>
		/// The main program for WaveBox.  Launches the HTTP server, initializes settings, creates default user,
		/// begins file scan, and then sleeps forever while other threads handle the work.
		/// </summary>
		public void Start()
		{
			if (logger.IsInfoEnabled) logger.Info("Initializing WaveBox " + WaveBoxService.BuildVersion + " on " + WaveBoxService.Platform + " platform...");

			// Initialize ImageMagick
			try
			{
				ImageMagickInterop.WandGenesis();
			}
			catch (Exception e)
			{
				logger.Error("Error loading ImageMagick DLL:", e);
			}

			// Create directory for WaveBox's root path, if it doesn't exist
			string rootDir = ServerUtility.RootPath();
			if (!Directory.Exists(rootDir))
			{
				Directory.CreateDirectory(rootDir);
			}

			// Perform initial setup of Settings, Database
			Injection.Kernel.Get<IDatabase>().DatabaseSetup();
			Injection.Kernel.Get<IServerSettings>().SettingsSetup();

			// If configured, start NAT routing
			try
			{
				if (Injection.Kernel.Get<IServerSettings>().NatEnable)
				{
					Nat.Start();
				}
			}
			catch (Exception e)
			{
				logger.Warn("natEnable not set in configuration, disabling NAT");
				logger.Warn(e);
			}

			// Register server with registration service
			ServerSetup();
			DynamicDns.RegisterUrl(ServerUrl, ServerGuid);

			// Start the HTTP server
			try
			{
				httpServer = new HttpServer(Injection.Kernel.Get<IServerSettings>().Port);
				StartTcpServer(httpServer);
			}
			catch (Exception e)
			{
				logger.Error("Could not start WaveBox HTTP server, please check port in your configuration");
				logger.Error(e);
				Environment.Exit(-1);
			}

			// Start the SignalR server for real time device state syncing
			try
			{
				WebApplication.Start<DeviceSyncStartup>("http://localhost:" + Injection.Kernel.Get<IServerSettings>().WsPort + "/");
			}
			catch (Exception e)
			{
				logger.Warn("Could not start WaveBox SignalR server, please check wsPort in your configuration");
				logger.Warn(e);
			}

			// Start ZeroConf
			try
			{
				ZeroConf.PublishZeroConf(ServerUrl, Injection.Kernel.Get<IServerSettings>().Port);
			}
			catch (Exception e)
			{
				logger.Warn("Could not start WaveBox ZeroConf, please check port in your configuration");
				logger.Warn(e);
			}

			// Temporary: create test user
			new User.Factory().CreateUser("test", "test", null);

			// Start the UserManager
			UserManager.Setup();

			// Start file manager, calculate time it takes to run.
			if (logger.IsInfoEnabled) logger.Info("Scanning media directories...");
			FileManager.Setup();

			// Start podcast download queue
			PodcastManagement.DownloadQueue.FeedChecks.queueOperation(new FeedCheckOperation(0));
			PodcastManagement.DownloadQueue.FeedChecks.startQueue();

			// Start session scrub operation
			SessionScrub.Queue.queueOperation(new SessionScrubOperation(0));
			SessionScrub.Queue.startQueue();

			// Start checking for updates
			AutoUpdater.Start();

			return;
		}

		/// <summary>
		/// Initialize TCP server threads
		/// </summary>
		private void StartTcpServer(AbstractTcpServer server)
		{
			// Thread for server to run
			Thread t = null;

			// Attempt to start the server thread
			try
			{
				t = new Thread(new ThreadStart(server.Listen));
				t.IsBackground = true;
				t.Start();
			}
			// Catch any exceptions
			catch (Exception e)
			{
				// Print the message, quit.
				logger.Error(e);
				Environment.Exit(-1);
			}
		}

		/// <summary>
		/// Stop the WaveBox main
		/// </summary>
		public void Stop()
		{
			httpServer.Stop();

			// Disable any Nat routes
			Nat.Stop();

			// Dispose of ImageMagick
			try
			{
				ImageMagickInterop.WandTerminus();
			}
			catch (Exception e)
			{
				logger.Error("Error loading ImageMagick DLL:", e);
			}
		}

		/// <summary>
		/// Restart the WaveBox main
		/// </summary>
		public void Restart()
		{
			Stop();
			StartTcpServer(httpServer);
		}
	}
}