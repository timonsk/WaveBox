﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using WaveBox.DataModel.Singletons;
using Newtonsoft.Json;

namespace WaveBox.DataModel.Model
{
	public class Album
	{
		[JsonProperty("itemTypeId")]
		public int ItemTypeId
		{
			get
			{
				return ItemType.ALBUM.GetItemTypeId();
			}
		}

		[JsonProperty("artistId")]
		public int ArtistId { get; set; }

		[JsonProperty("albumId")]
		public int AlbumId { get; set; }

		[JsonProperty("albumName")]
		public string AlbumName { get; set; }

		[JsonProperty("releaseYear")]
		public int ReleaseYear { get; set; }

		[JsonProperty("artId")]
		public int ArtId { get; set; }

		public Album()
		{
		}

		public Album(SQLiteDataReader reader)
		{
			SetPropertiesFromQueryResult(reader);
		}

		public Album(int albumId)
		{
			SQLiteConnection conn = null;
			SQLiteDataReader result = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("SELECT * FROM album WHERE album_id = @albumid");
					q.Parameters.AddWithValue("@albumid", albumId);

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					result = q.ExecuteReader();

					if (result.Read())
					{
						SetPropertiesFromQueryResult(result);
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
		}

		public Album(string albumName)
		{
			if (albumName == null || albumName == "")
			{
				return;
			}

			AlbumName = albumName;

			SQLiteConnection conn = null;
			SQLiteDataReader result = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("SELECT * FROM album WHERE album_name  = @albumname");
					q.Parameters.AddWithValue("@itemtypeid", ItemTypeId);
					q.Parameters.AddWithValue("@albumname", AlbumName);

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					result = q.ExecuteReader();

					if (result.Read())
					{
						SetPropertiesFromQueryResult(result);
					}
					else
					{
						AlbumName = albumName;
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
		}

		private static bool InsertAlbum(string albumName, int artistId)
		{
			bool success = false;

			SQLiteConnection conn = null;
			SQLiteDataReader result = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("INSERT INTO album (album_name, artist_id) VALUES(@albumname, @artistid)");
					q.Parameters.AddWithValue("@albumname", albumName);
					q.Parameters.AddWithValue("@artistid", artistId);

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					int affected = (int)q.ExecuteNonQuery();

					if (affected >= 1)
						success = true;
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

			return success;
		}

		private void SetPropertiesFromQueryResult(SQLiteDataReader reader)
		{
			if(reader.IsDBNull(reader.GetOrdinal("artist_id")))
			{
				this.ArtistId = 0;
			}
			else ArtistId = reader.GetInt32(reader.GetOrdinal("artist_id"));

			if (reader.IsDBNull(reader.GetOrdinal("album_id")))
			{
				this.AlbumId = 0;
			}
			else this.AlbumId = reader.GetInt32(reader.GetOrdinal("album_id"));

			if (reader.IsDBNull(reader.GetOrdinal("album_name")))
			{
				this.AlbumName = "";
			}
			else this.AlbumName = reader.GetString(reader.GetOrdinal("album_name"));

			if (reader.IsDBNull(reader.GetOrdinal("album_art_id")))
			{
				this.ArtId = 0;
			}
			else this.ArtId = reader.GetInt32(reader.GetOrdinal("album_art_id"));
		}

		public Artist Artist()
		{
			return new Artist(ArtistId);
		}

		// TO DO
		public void AutoTag()
		{
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
						"LEFT JOIN artist ON song_artist_id = artist.artist_id " +
						"LEFT JOIN album ON song_album_id = album.album_id " +
						"WHERE song_album_id = @albumid"
					);

					q.Parameters.AddWithValue("@albumid", AlbumId);

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					reader = q.ExecuteReader();

					while (reader.Read())
					{
						songs.Add(new Song(reader));
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

			songs.Sort(Song.CompareSongsByDiscAndTrack);
			return songs;
		}

		public static Album AlbumForName(string albumName, int artistId)
		{
			if (albumName == "" || albumName == null || artistId == 0)
			{
				return new Album();
			}

			Album a = new Album(albumName);

			if (a.AlbumId == 0)
			{
				a = null;

				if(InsertAlbum(albumName, artistId))
				{
					a = AlbumForName(albumName, artistId);
				}
			}

			return a;
		}

		public static List<Album> AllAlbums()
		{
			var albums = new List<Album>();

			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("SELECT * FROM album");

					q.Parameters.AddWithValue("@itemtypeid", new Album().ItemTypeId);

					conn = Database.GetDbConnection();
					q.Connection = conn;
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

			albums.Sort(CompareAlbumsByName);
			return albums;
		}

		public static List<Album> RandomAlbums()
		{
			var random = new List<Album>();
			SQLiteConnection conn = null;
			SQLiteDataReader reader = null;

			lock (Database.dbLock)
			{
				try
				{
					var q = new SQLiteCommand("SELECT TOP @count * FROM album" +
						"ORDER BY NEWID()"
					);

					q.Parameters.AddWithValue("@itemtypeid", new Album().ItemTypeId);

					conn = Database.GetDbConnection();
					q.Connection = conn;
					q.Prepare();
					reader = q.ExecuteReader();

					while (reader.Read())
					{
						random.Add(new Album(reader));
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

			return random;
		}

		public static int CompareAlbumsByName(Album x, Album y)
		{
			return StringComparer.OrdinalIgnoreCase.Compare(x.AlbumName, y.AlbumName);
		}
	}
}