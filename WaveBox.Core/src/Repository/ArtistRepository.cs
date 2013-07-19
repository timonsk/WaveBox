using System;
using WaveBox.Core.Injection;
using System.Collections.Generic;
using Cirrious.MvvmCross.Plugins.Sqlite;
using System.Linq;
using WaveBox.Static;
using Ninject;

namespace WaveBox.Model.Repository
{
	public class ArtistRepository : IArtistRepository
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly IDatabase database;

		public ArtistRepository(IDatabase database)
		{
			if (database == null)
				throw new ArgumentNullException("database");

			this.database = database;
		}

		public bool InsertArtist(string artistName)
		{
			int? itemId = Injection.Kernel.Get<IItemRepository>().GenerateItemId(ItemType.Artist);
			if (itemId == null)
			{
				return false;
			}

			bool success = false;
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				Artist artist = new Artist();
				artist.ArtistId = itemId;
				artist.ArtistName = artistName;
				int affected = conn.InsertLogged(artist, InsertType.InsertOrIgnore);

				success = affected > 0;
			}
			catch (Exception e)
			{
				logger.Error("Error inserting artist " + artistName, e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return success;
		}

		public Artist ArtistForName(string artistName)
		{
			if (artistName == null || artistName == "")
			{
				return new Artist();
			}

			// check to see if the artist exists
			Artist anArtist = new Artist.Factory().CreateArtist(artistName);

			// if not, create it.
			if (anArtist.ArtistId == null)
			{
				anArtist = null;
				if (InsertArtist(artistName))
				{
					anArtist = ArtistForName(artistName);
				}
				else 
				{
					// The insert failed because this album was inserted by another
					// thread, so grab the artist id, it will exist this time
					anArtist = new Artist.Factory().CreateArtist(artistName);
				}
			}

			// then return the artist object retrieved or created.
			return anArtist;
		}

		public List<Artist> AllArtists()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.Query<Artist>("SELECT * FROM Artist ORDER BY ArtistName");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return new List<Artist>();
		}

		public int CountArtists()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.ExecuteScalar<int>("SELECT COUNT(ArtistId) FROM Artist");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return 0;
		}

		public List<Artist> SearchArtists(string field, string query, bool exact = true)
		{
			if (query == null)
			{
				return new List<Artist>();
			}

			// Set default field, if none provided
			if (field == null)
			{
				field = "ArtistName";
			}

			// Check to ensure a valid query field was set
			if (!new string[] {"ArtistId", "ArtistName"}.Contains(field))
			{
				return new List<Artist>();
			}

			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();

				if (exact)
				{
					// Search for exact match
					return conn.Query<Artist>("SELECT * FROM Artist WHERE " + field + " = ? ORDER BY ArtistName", query);
				}
				else
				{
					// Search for fuzzy match (containing query)
					return conn.Query<Artist>("SELECT * FROM Artist WHERE " + field + " LIKE ? ORDER BY ArtistName", "%" + query + "%");
				}
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return new List<Artist>();
		}

		// Return a list of artists titled between a range of (a-z, A-Z, 0-9 characters)
		public List<Artist> RangeArtists(char start, char end)
		{
			// Ensure characters are alphanumeric, return empty list if either is not
			if (!Char.IsLetterOrDigit(start) || !Char.IsLetterOrDigit(end))
			{
				return new List<Artist>();
			}

			string s = start.ToString();
			// Add 1 to character to make end inclusive
			string en = Convert.ToChar((int)end + 1).ToString();

			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();

				List<Artist> artists;
				artists = conn.Query<Artist>("SELECT * FROM Artist " +
				                             "WHERE Artist.ArtistName BETWEEN LOWER(?) AND LOWER(?) " +
				                             "OR Artist.ArtistName BETWEEN UPPER(?) AND UPPER(?)", s, en, s, en);

				artists.Sort(Artist.CompareArtistsByName);
				return artists;
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			// We had an exception somehow, so return an empty list
			return new List<Artist>();
		}

		// Return a list of artists using SQL LIMIT x,y where X is starting index and Y is duration
		public List<Artist> LimitArtists(int index, int duration = Int32.MinValue)
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();

				// Begin building query
				List<Artist> artists;

				string query = "SELECT * FROM Artist LIMIT ? ";

				// Add duration to LIMIT if needed
				if (duration != Int32.MinValue && duration > 0)
				{
					query += ", ?";
				}

				// Run query, sort, send it back
				artists = conn.Query<Artist>(query, index, duration);
				artists.Sort(Artist.CompareArtistsByName);
				return artists;
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			// We had an exception somehow, so return an empty list
			return new List<Artist>();
		}
	}
}

