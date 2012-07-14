﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using WaveBox.DataModel.Singletons;
using Newtonsoft.Json;

namespace WaveBox.DataModel.Model
{
	public class Artist
	{
		/// <summary>
		/// Properties
		/// </summary>
		/// 
		[JsonProperty("itemTypeId")]
		public int ItemTypeId
		{
			get
			{
				return ItemType.ARTIST.GetItemTypeId();
			}
		}

		[JsonProperty("artistId")]
		public int ArtistId { get; set; }

		[JsonProperty("artistName")]
		public string ArtistName { get; set; }

		[JsonProperty("artId")]
		public int ArtId { get; set; }


		/// <summary>
		/// Constructors
		/// </summary>
		
		public Artist()
		{
		}

		public Artist(SQLiteDataReader reader)
		{
			SetPropertiesFromQueryResult(reader);
		}

		public Artist(int artistId)
		{
			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					conn = Database.GetDbConnection();

					var q = new SQLiteCommand("SELECT * FROM artist WHERE artist_id = @artistid");
					q.Connection = conn;
					q.Parameters.AddWithValue("@artistid", artistId);
					q.Prepare();
					reader = q.ExecuteReader();

					if (reader.Read())
					{
						SetPropertiesFromQueryResult(reader);
					}
					else
					{
						Console.WriteLine("Artist constructor query returned no results");
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.Close(conn, reader);
				}
			}
		}

		public Artist(string artistName)
		{
			if (artistName == null || artistName == "")
			{
				return;
			}

			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					conn = Database.GetDbConnection();
					var q = new SQLiteCommand("SELECT * FROM artist WHERE artist_name = @artistname");
					q.Connection = conn;
					q.Parameters.AddWithValue("@artistname", artistName);
					q.Prepare();
					reader = q.ExecuteReader();

					if (reader.Read())
					{
						SetPropertiesFromQueryResult(reader);
					}
					else
					{
						ArtistName = artistName;
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.Close(conn, reader);
				}
			}
		}

		/// <summary>
		/// Private methods
		/// </summary>

		private void SetPropertiesFromQueryResult(SQLiteDataReader reader)
		{
			try
			{
				ArtistId = reader.GetInt32(reader.GetOrdinal("artist_id"));
				ArtistName = reader.GetString(reader.GetOrdinal("artist_name"));

				if 
					(reader.GetValue(reader.GetOrdinal("artist_art_id")) == DBNull.Value) ArtId = 0;
				else 
					ArtId = reader.GetInt32(reader.GetOrdinal("artist_art_id"));
			}

			catch (SQLiteException e)
			{
				if (e.InnerException.ToString() == "SqlNullValueException") { }
			}
		}

		private static bool InsertArtist(string artistName)
		{
			bool success = false;
			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					conn = Database.GetDbConnection();
					var q = new SQLiteCommand("INSERT INTO artist (artist_name) VALUES (@artistname)");
					q.Connection = conn;
					q.Parameters.AddWithValue("@artistname", artistName);
					q.Prepare();
					int affected = q.ExecuteNonQuery();

					if (affected == 1)
					{
						success = true;
					}
					else
						success = false;
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.Close(conn, reader);
				}
			}

			return success;
		}

		/// <summary>
		/// Public methods
		/// </summary>

		public List<Album> ListOfAlbums()
		{
			var albums = new List<Album>();

			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					conn = Database.GetDbConnection();
					var q = new SQLiteCommand("SELECT * FROM album WHERE artist_id = @artistid");
					q.Connection = conn;
					q.Parameters.AddWithValue("@artistid", ArtistId);
					q.Prepare();
					reader = q.ExecuteReader();

					while (reader.Read())
					{
						albums.Add(new Album(reader));
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.Close(conn, reader);
				}
			}

			albums.Sort(Album.CompareAlbumsByName);
			return albums;
		}

		public List<Song> ListOfSongs()
		{
			var songs = new List<Song>();


			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("SELECT song.*, artist.artist_name, album.album_name FROM song " + 
						"LEFT JOIN artist ON song_artist_id = artist_id " +
						"LEFT JOIN album ON song_album_id = album_id " +
						"WHERE song_artist_id = @artistid"
					);

					q.Parameters.AddWithValue("@artistid", ArtistId);

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					reader = q.ExecuteReader();

					while (reader.Read())
					{
						songs.Add(new Song(reader));
					}

					reader.Close();
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.Close(conn, reader);
				}
			}

			songs.Sort(Song.CompareSongsByDiscAndTrack);
			return songs;
		}

		public static Artist ArtistForName(string artistName)
		{
			if (artistName == null || artistName == "")
			{
				return new Artist();
			}

			// check to see if the artist exists
			var anArtist = new Artist(artistName);

			// if not, create it.
			if (anArtist.ArtistId == 0)
			{
				anArtist = null;
				if (InsertArtist(artistName))
				{
					anArtist = ArtistForName(artistName);
				}
			}

			// then return the artist object retrieved or created.
			return anArtist;
		}

		public List<Artist> AllArtists()
		{
			var artists = new List<Artist>();

			SQLiteConnection conn = null;
			SQLiteDataReader result = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("SELECT * FROM artist");

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					result = q.ExecuteReader();

					while (result.Read())
					{
						artists.Add(new Artist(result));
					}

					result.Close();
				}
				catch (Exception e)
				{
					Console.WriteLine(e.ToString());
				}
				finally
				{
					Database.Close(conn, result);
				}
			}

			artists.Sort(Artist.CompareArtistsByName);

			return artists;
		}

		public static int CompareArtistsByName(Artist x, Artist y)
		{
			return StringComparer.OrdinalIgnoreCase.Compare(x.ArtistName, y.ArtistName);
		}
	}
}