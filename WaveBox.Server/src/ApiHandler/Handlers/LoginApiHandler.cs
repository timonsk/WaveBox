using System;
using Newtonsoft.Json;
using Ninject;
using WaveBox.Core;
using WaveBox.Core.ApiResponse;
using WaveBox.Core.Extensions;
using WaveBox.Core.Model;
using WaveBox.Service.Services.Http;
using WaveBox.Static;

namespace WaveBox.ApiHandler.Handlers
{
	public class LoginApiHandler : IApiHandler
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public string Name { get { return "login"; } }

		// API handler is read-only, so no permissions checks needed
		public bool CheckPermission(User user, string action)
		{
			return true;
		}

		/// <summary>
		/// Process returns a new session key for this user upon successful login
		/// <summary>
		public void Process(UriWrapper uri, IHttpProcessor processor, User user)
		{
			logger.IfInfo(String.Format("Authenticated user, generated new session: [user: {0}, key: {1}]", user.UserName, user.SessionId));

			processor.WriteJson(new LoginResponse(null, user.SessionId));
			return;
		}
	}
}
