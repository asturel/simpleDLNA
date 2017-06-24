using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using NMaier.SimpleDlna.Server;
using NMaier.SimpleDlna.Utilities;
using NMaier;
using log4net;

namespace NMaier
{
  public static class EpochTimeExtensions
  {
    /// <summary>
    /// Converts the given date value to epoch time.
    /// </summary>
    public static long ToEpochTime(this DateTime dateTime)
    {
      var date = dateTime.ToUniversalTime();
      var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).Ticks;
      var ts = ticks / TimeSpan.TicksPerSecond;
      return ts;
    }

    /// <summary>
    /// Converts the given date value to epoch time.
    /// </summary>
    public static long ToEpochTime(this DateTimeOffset dateTime)
    {
      var date = dateTime.ToUniversalTime();
      var ticks = date.Ticks - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
      var ts = ticks / TimeSpan.TicksPerSecond;
      return ts;
    }

    /// <summary>
    /// Converts the given epoch time to a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/> kind.
    /// </summary>
    public static DateTime ToDateTimeFromEpoch(this long intDate)
    {
      var timeInTicks = intDate * TimeSpan.TicksPerSecond;
      return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(timeInTicks);
    }

    /// <summary>
    /// Converts the given epoch time to a UTC <see cref="DateTimeOffset"/>.
    /// </summary>
    public static DateTimeOffset ToDateTimeOffsetFromEpoch(this long intDate)
    {
      var timeInTicks = intDate * TimeSpan.TicksPerSecond;
      return new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(timeInTicks);
    }
  }

  class TVStore
  {
    private readonly FileInfo DBPath;

    //private static FileInfo storeFile = new FileInfo("test.db");
    private static bool initialized = false;
    private DateTime lastUpdated;
    private static TimeSpan maxDiff = new System.TimeSpan(10,0,0);

    private readonly static ILog logger =
          LogManager.GetLogger(typeof(TVStore));

    private IDbConnection getConnection()
    {
      //return Sqlite.GetDatabaseConnection(storeFile);
      var conn = new SimpleDlna.Model.Store(DBPath);
      conn.Database.Connection.Open();
      return conn.Database.Connection;
    }

    private void StoreLastUpdateDate(DateTime time)
    {
      System.IO.File.WriteAllText("lastupdate.db", time.Ticks.ToString());
    }
    private DateTime GetLastUpdatedDate()
    {
      try {
        return (new System.DateTime(System.Int64.Parse(System.IO.File.ReadAllText("lastupdate.db"))));
      } catch (Exception) {
        return (new System.DateTime(1970, 1, 1));
      }
    }
    public void InitCache()
    {

      using (IDbConnection sqlconn = getConnection())
      {

        var col = new System.Collections.Concurrent.ConcurrentDictionary<int, TVShowInfo>();
        using (IDbCommand cmd = sqlconn.CreateCommand())
        {
          cmd.CommandText = "SELECT `id`, `name`, `lastupdated`, `imdb` FROM TVShow;";
          using (var rdr = cmd.ExecuteReader())
          {
            while (rdr.Read())
            {
              var tvshow = new TVShowInfo();
              tvshow.ID = rdr.GetInt32(0);
              tvshow.Name = rdr.GetString(1);
              tvshow.LastUpdated = rdr.GetInt64(2);
              tvshow.IMDBID = rdr.GetString(3);
              tvshow.TVEpisodes = new List<TVEpisode>();
              col.TryAdd(tvshow.ID, tvshow);
            }
          }
        }

        using (IDbCommand cmd = sqlconn.CreateCommand())
        {
          cmd.CommandText = "SELECT tvdbid, season, episode, episodeid, absolutenumber, title, aired FROM TVSHowEntry;";
          using (var rdr = cmd.ExecuteReader())
          {
            while (rdr.Read())
            {
              var tvshow = new TVEpisode();
              var id = rdr.GetInt32(0);
              tvshow.Season = rdr.GetInt32(1);
              tvshow.Episode = rdr.GetInt32(2);
              tvshow.EpisodeId = rdr.GetInt32(3);
              tvshow.AbsoluteNumber = rdr.GetInt32(4);
              tvshow.Title = rdr.GetString(5);
              tvshow.FirstAired = DateTime.Parse(rdr.GetString(6));

              var tv = col[id];
              tv.TVEpisodes.Add(tvshow);
              TheTVDB.cacheshow.AddOrUpdate(tv.ID, tv, (key, oldvalue) => (oldvalue.LastUpdated > tv.LastUpdated ? oldvalue : tv));
            }
          }

        }
      }
    }

    public void InsertMany(TVShowInfo[] data)
    {
      using (var sqlconn = getConnection())
      {
        using (var cmd = sqlconn.CreateCommand())
        using (var cmd2 = sqlconn.CreateCommand())
        {
          cmd.CommandText = "INSERT OR REPLACE INTO `TVSHow` (`id`, `name`, `imdb`, `lastupdated`) VALUES (@id, @name, @imdb, @lastupdated);";
          cmd2.CommandText = "INSERT OR REPLACE INTO TVSHowEntry (tvdbid, season, episode, episodeid, absolutenumber, title, aired) VALUES (@tvdbid, @season, @episode, @episodeid, @absolutenumber, @title, @aired);";

          var tid = cmd.CreateParameter();
          tid.DbType = DbType.Int32;
          tid.ParameterName = "@id";
          tid.Size = 4;
          cmd.Parameters.Add(tid);

          var tname = cmd.CreateParameter();
          tname.DbType = DbType.String;
          tname.ParameterName = "@name";
          tname.Size = 128;
          cmd.Parameters.Add(tname);

          var tlastupdated = cmd.CreateParameter();
          tlastupdated.DbType = DbType.UInt64;
          tlastupdated.ParameterName = "@lastupdated";
          cmd.Parameters.Add(tlastupdated);

          var timdbid = cmd.CreateParameter();
          timdbid.DbType = DbType.String;
          timdbid.ParameterName = "@imdb";
          timdbid.Size = 128;
          cmd.Parameters.Add(timdbid);


          var eid = cmd2.CreateParameter();
          eid.ParameterName = "@tvdbid";
          eid.DbType = DbType.Int32;
          cmd2.Parameters.Add(eid);

          var eepisode = cmd2.CreateParameter();
          eepisode.ParameterName = "@episode";
          eepisode.DbType = DbType.Int32;
          cmd2.Parameters.Add(eepisode);

          var eseason = cmd2.CreateParameter();
          eseason.ParameterName = "@season";
          eseason.DbType = DbType.Int32;
          cmd2.Parameters.Add(eseason);

          var etitle = cmd2.CreateParameter();
          etitle.ParameterName = "@title";
          etitle.DbType = DbType.String;
          cmd2.Parameters.Add(etitle);


          var eaired = cmd2.CreateParameter();
          eaired.ParameterName = "@aired";
          eaired.DbType = DbType.DateTime;
          cmd2.Parameters.Add(eaired);

          var eepisodeid = cmd2.CreateParameter();
          eepisodeid.ParameterName = "@episodeid";
          eepisodeid.DbType = DbType.Int32;
          cmd2.Parameters.Add(eepisodeid);

          var eabsolutenumber = cmd2.CreateParameter();
          eabsolutenumber.ParameterName = "@absolutenumber";
          eabsolutenumber.DbType = DbType.Int32;
          cmd2.Parameters.Add(eabsolutenumber);

          foreach (var tventry in data)
          {
            tid.Value = tventry.ID;
            tname.Value = tventry.Name;
            tlastupdated.Value = tventry.LastUpdated;
            timdbid.Value = tventry.IMDBID;
            cmd.ExecuteNonQuery();

            foreach (var ep in tventry.TVEpisodes)
            {
              eid.Value = tventry.ID;
              eepisode.Value = ep.Episode;
              eseason.Value = ep.Season;
              etitle.Value = ep.Title;
              eaired.Value = ep.FirstAired;
              eepisodeid.Value = ep.EpisodeId;
              eabsolutenumber.Value = ep.AbsoluteNumber;
              cmd2.ExecuteNonQuery();
            }
          }
        }
      }
    }

    public void Insert(TVShowInfo data)
    {
      TVShowInfo[] datax = { data };
      this.InsertMany(datax);
    }

    private void CheckUpdates(bool forced=false)
    {
      var _lastupdate = GetLastUpdatedDate();
      var now = DateTime.Now;
      var diff = now - _lastupdate;
      string since;
      if (diff <= new TimeSpan(1,0,0,0))
      {
        since = "day";
      }
      else if (diff <= new TimeSpan(7,0,0,0))
      {
        since = "week";
      } else if (diff <= new TimeSpan(30,0,0,0))
      {
        since = "month";
      } else
      {
        throw new Exception("too old");
      }

      int[] shouldUpdate;
      if (!forced)
      {
        //var updatesince = TheTVDB.UpdatesSince(since);
        var updatesince = TheTVDB.FetchUpdate(since);
        shouldUpdate = updatesince.Series.Where(id => TheTVDB.cacheshow.ContainsKey(id)).ToArray();

        var s2 = updatesince.Episodes.Select(epid => (TheTVDB.cacheshow.ToArray().Where(tv => tv.Value.TVEpisodes.Where(ep => ep.EpisodeId == 1).Count() > 0)).Select (x => x.Key).ToArray()).ToArray();
        foreach (var s22 in s2)
        {
          shouldUpdate = shouldUpdate.Concat(s22).ToArray();
        }
        shouldUpdate = shouldUpdate.Distinct().ToArray();

      } else
      {
        shouldUpdate = TheTVDB.cacheshow.Keys.ToArray();
      }
      var updatedData =
        (from id in shouldUpdate
        let tvinfo = TheTVDB.GetTVShowDetails(id, true).Item1
        select tvinfo).ToArray();

      foreach (var tv in updatedData)
      {
        TheTVDB.cacheshow.AddOrUpdate(tv.ID, tv, (key, oldvalue) => (oldvalue.LastUpdated > tv.LastUpdated ? oldvalue : tv));
      }

      InsertMany(updatedData);
      StoreLastUpdateDate(System.DateTime.Now);
    }
    private void updateDB(object state)
    {
      CheckUpdates(false);
    }
    public TVStore(FileInfo dbpath)
    {
      DBPath = dbpath;
      if (!initialized)
      {
        try {
          lastUpdated = GetLastUpdatedDate();
        } catch (Exception e)
        {
          logger.Error(String.Format("TV: Failed to get LastUpdated"), e);
        }
        
        using (var conn = Sqlite.GetDatabaseConnection(dbpath)) {
          using (var c = conn.CreateCommand())
          {
            c.CommandText = "CREATE TABLE IF NOT EXISTS TVSHow (id int PRIMARY KEY ON CONFLICT REPLACE, name varchar(128), lastupdated UNSIGNED BIG INT, imdb varchar(32));";
            c.ExecuteNonQuery();
          }
          using (var c0 = conn.CreateCommand())
          {
            c0.CommandText = "CREATE TABLE IF NOT EXISTS TVSHowEntry (int id, tvdbid int, season int, episode int, episodeid int, absolutenumber int, title varchar(128), aired DATETIME, UNIQUE (tvdbid, season, episode) ON CONFLICT REPLACE);";
            c0.ExecuteNonQuery();
          }
        }
        this.InitCache();
        if (System.DateTime.Now - lastUpdated > maxDiff)
        {
          try {
            //CheckUpdates(EpochTimeExtensions.ToEpochTime(lastUpdated), false);
            CheckUpdates(false);
          } catch (Exception e)
          {
            logger.Error(String.Format("TV: Failed to check updates"), e);
          }
        }
        initialized = true;
        new System.Threading.Timer(updateDB, null, TimeSpan.FromDays(1.0), TimeSpan.FromDays(1.0));
      }
    }
  }
}
