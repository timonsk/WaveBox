using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Ninject;
using WaveBox.Core.Model;
using WaveBox.Static;
using WaveBox.Service.Services.Http;
using WaveBox.Core.Model.Repository;
using WaveBox.Core.ApiResponse;
using WaveBox.Core;

namespace WaveBox.ApiHandler.Handlers
{
	class SongsApiHandler : IApiHandler
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public string Name { get { return "songs"; } }

		/// <summary>
		/// Process generates a JSON list of songs
		/// </summary>
		public void Process(UriWrapper uri, IHttpProcessor processor, User user)
		{
			// Return list of songs
			IList<Song> listOfSongs = new List<Song>();

			// Check for valid ID
			if (uri.Id != null)
			{
				// Add song by ID to the list
				listOfSongs.Add(Injection.Kernel.Get<ISongRepository>().SongForId((int)uri.Id));
			}
			// Check for a request for range of songs
			else if (uri.Parameters.ContainsKey("range"))
			{
				string[] range = uri.Parameters["range"].Split(',');

				// Ensure valid range was parsed
				if (range.Length != 2)
				{
					processor.WriteJson(new SongsResponse("Parameter 'range' requires a valid, comma-separated character tuple", null));
					return;
				}

				// Validate as characters
				char start, end;
				if (!Char.TryParse(range[0], out start) || !Char.TryParse(range[1], out end))
				{
					processor.WriteJson(new SongsResponse("Parameter 'range' requires characters which are single alphanumeric values", null));
					return;
				}

				// Grab range of songs
				listOfSongs = Injection.Kernel.Get<ISongRepository>().RangeSongs(start, end);
			}

			// Check for a request to limit/paginate songs, like SQL
			// Note: can be combined with range or all songs
			if (uri.Parameters.ContainsKey("limit") && uri.Id == null)
			{
				string[] limit = uri.Parameters["limit"].Split(',');

				// Ensure valid limit was parsed
				if (limit.Length < 1 || limit.Length > 2 )
				{
					processor.WriteJson(new SongsResponse("Parameter 'limit' requires a single integer, or a valid, comma-separated integer tuple", null));
					return;
				}

				// Validate as integers
				int index = 0;
				int duration = Int32.MinValue;
				if (!Int32.TryParse(limit[0], out index))
				{
					processor.WriteJson(new SongsResponse("Parameter 'limit' requires a valid integer start index", null));
					return;
				}

				// Ensure positive index
				if (index < 0)
				{
					processor.WriteJson(new SongsResponse("Parameter 'limit' requires a non-negative integer start index", null));
					return;
				}

				// Check for duration
				if (limit.Length == 2)
				{
					if (!Int32.TryParse(limit[1], out duration))
					{
						processor.WriteJson(new SongsResponse("Parameter 'limit' requires a valid integer duration", null));
						return;
					}

					// Ensure positive duration
					if (duration < 0)
					{
						processor.WriteJson(new SongsResponse("Parameter 'limit' requires a non-negative integer duration", null));
						return;
					}
				}

				// Check if results list already populated by range
				if (listOfSongs.Count > 0)
				{
					// No duration?  Return just specified number of songs
					if (duration == Int32.MinValue)
					{
						listOfSongs = listOfSongs.Skip(0).Take(index).ToList();
					}
					else
					{
						// Else, return songs starting at index, up to count duration
						listOfSongs = listOfSongs.Skip(index).Take(duration).ToList();
					}
				}
				else
				{
					// If no songs in list, grab directly using model method
					listOfSongs = Injection.Kernel.Get<ISongRepository>().LimitSongs(index, duration);
				}
			}

			// Finally, if no songs already in list, send the whole list
			if (listOfSongs.Count == 0)
			{
				listOfSongs = Injection.Kernel.Get<ISongRepository>().AllSongs();
			}

			// Send it!
			processor.WriteJson(new SongsResponse(null, listOfSongs));
		}
	}
}
