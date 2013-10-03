using System;
using System.Collections.Generic;
using Cirrious.MvvmCross.Plugins.Sqlite;
using WaveBox.Core.Extensions;
using WaveBox.Core.Static;
using System.Linq;

namespace WaveBox.Core.Model.Repository
{
	public class GenreRepository : IGenreRepository
	{
		private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly IDatabase database;

		public GenreRepository(IDatabase database)
		{
			if (database == null)
				throw new ArgumentNullException("database");

			this.database = database;
		}

		public Genre GenreForId(int? genreId)
		{
			return this.database.GetSingle<Genre>("SELECT * FROM Genre WHERE GenreId = ?", genreId);
		}

		private static List<Genre> memCachedGenres = new List<Genre>();
		public Genre GenreForName(string genreName)
		{
			if ((object)genreName == null)
			{
				return new Genre();
			}

			lock (memCachedGenres)
			{
				// First check to see if the genre is in mem cache
				Genre genre = (from g in memCachedGenres where g.GenreName.Equals(genreName) select g).FirstOrDefault();

				if ((object)genre != null)
				{
					// We got a match, so use the genre id
					return genre;
				}
				else
				{
					Genre g = this.database.GetSingle<Genre>("SELECT * FROM Genre WHERE GenreName = ?", genreName);
					if (g != null)
					{
						return g;
					}

					// If this genre didn't exist, generate an id and insert it
					Genre genre2 = new Genre();
					genre2.GenreName = genreName;
					genre2.InsertGenre();
					return genre2;
				}
			}
		}

		public IList<Genre> AllGenres()
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.Query<Genre>("SELECT * FROM Genre ORDER BY GenreName COLLATE NOCASE");
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return new List<Genre>();
		}

		public IList<Artist> ListOfArtists(int genreId)
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.Query<Artist>("SELECT Artist.* " +
				                          "FROM Genre " +
				                          "LEFT JOIN Song ON Song.GenreId = Genre.GenreId " +
				                          "LEFT JOIN Artist ON Song.ArtistId = Artist.ArtistId " +
				                          "WHERE Genre.GenreId = ? GROUP BY Artist.ArtistId", genreId);
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

		public IList<Album> ListOfAlbums(int genreId)
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.Query<Album>("SELECT Album.*, ArtItem.ArtId " +
				                         "FROM Genre " +
				                         "LEFT JOIN Song ON Song.GenreId = Genre.GenreId " +
				                         "LEFT JOIN Album ON Song.AlbumId = Album.ItemId " +
				                         "LEFT JOIN ArtItem ON Album.AlbumId = ArtItem.ItemId " +
				                         "WHERE Genre.GenreId = ? GROUP BY Album.ItemId", genreId);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return new List<Album>();
		}

		public IList<Song> ListOfSongs(int genreId)
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.Query<Song>("SELECT Song.*, Genre.GenreName " +
				                        "FROM Genre " +
				                        "LEFT JOIN Song ON Song.GenreId = Genre.GenreId " +
				                        "WHERE Genre.GenreId = ? GROUP BY Song.ItemId", genreId);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return new List<Song>();
		}

		public IList<Folder> ListOfFolders(int genreId)
		{
			ISQLiteConnection conn = null;
			try
			{
				conn = database.GetSqliteConnection();
				return conn.Query<Folder>("SELECT Folder.* " +
				                          "FROM Genre " +
				                          "LEFT JOIN Song ON Song.GenreId = Genre.GenreId " +
				                          "LEFT JOIN Folder ON Song.FolderId = Folder.FolderId " +
				                          "WHERE Genre.GenreId = ? GROUP BY Folder.FolderId", genreId);
			}
			catch (Exception e)
			{
				logger.Error(e);
			}
			finally
			{
				database.CloseSqliteConnection(conn);
			}

			return new List<Folder>();
		}
	}
}

