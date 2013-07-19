﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using Cirrious.MvvmCross.Plugins.Sqlite;
using Newtonsoft.Json;
using Ninject;
using TagLib;
using WaveBox.Core.Injection;
using WaveBox.Model;
using WaveBox.Static;
using WaveBox.Model.Repository;

namespace WaveBox.Model
{
	public class Song : MediaItem
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public static readonly string[] ValidExtensions = { "mp3", "m4a", "flac", "wv", "mpc", "ogg", "wma" };

		[JsonIgnore]
		public override ItemType ItemType { get { return ItemType.Song; } }

		[JsonProperty("itemTypeId")]
		public override int ItemTypeId { get { return (int)ItemType.Song; } }

		[JsonProperty("artistId")]
		public int? ArtistId { get; set; }

		[JsonProperty("artistName"), IgnoreWrite]
		public string ArtistName { get; set; }

		[JsonProperty("albumId")]
		public int? AlbumId { get; set; }

		[JsonProperty("albumName"), IgnoreWrite]
		public string AlbumName { get; set; }

		[JsonProperty("songName")]
		public string SongName { get; set; }

		[JsonProperty("trackNumber")]
		public int? TrackNumber { get; set; }

		[JsonProperty("discNumber")]
		public int? DiscNumber { get; set; }

		[JsonProperty("releaseYear")]
		public int? ReleaseYear { get; set; }

		public Song()
		{
		}

		public override void InsertMediaItem()
		{
			ISQLiteConnection conn = null;
			try
			{
				// insert the song into the database
				conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
				conn.InsertLogged(this, InsertType.Replace);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
			}

			Injection.Kernel.Get<IArtRepository>().UpdateArtItemRelationship(ArtId, ItemId, true);
			Injection.Kernel.Get<IArtRepository>().UpdateArtItemRelationship(ArtId, AlbumId, true);
			Injection.Kernel.Get<IArtRepository>().UpdateArtItemRelationship(ArtId, FolderId, false); // Only update a folder art relationship if it has no folder art
		}

		public static int CompareSongsByDiscAndTrack(Song x, Song y)
		{
			if (x.DiscNumber == y.DiscNumber && x.TrackNumber == y.TrackNumber)
			{
				return 0;
			}
			// if the disc numbers are equal, we have to compare by track
			else if (x.DiscNumber == y.DiscNumber)
			{
				return x.TrackNumber > y.TrackNumber ? 1 : -1;
			}
			// if the disc numbers are not equal, the one with the higher disc number is greater.
			else
			{
				return x.DiscNumber > y.DiscNumber ? 1 : -1;
			}
		}

		/*
		 * Factory
		 */

		public new class Factory
		{
			public Song CreateSong(int songId)
			{
				ISQLiteConnection conn = null;
				try
				{
					conn = Injection.Kernel.Get<IDatabase>().GetSqliteConnection();
					IEnumerable result = conn.DeferredQuery<Song>("SELECT Song.*, Artist.ArtistName, Album.AlbumName, Genre.GenreName FROM Song " +
																  "LEFT JOIN Artist ON Song.ArtistId = Artist.ArtistId " +
																  "LEFT JOIN Album ON Song.AlbumId = Album.AlbumId " +
																  "LEFT JOIN Genre ON Song.GenreId = Genre.GenreId " +
																  "WHERE Song.ItemId = ? LIMIT 1", songId);

					foreach (Song song in result)
					{
						// Record exists, so return it
						return song;
					}
				}
				catch (Exception e)
				{
					logger.Error(e);
				}
				finally
				{
					Injection.Kernel.Get<IDatabase>().CloseSqliteConnection(conn);
				}

				// No record found, so return an empty Song object
				return new Song();
			}
		}
	}
}
