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

namespace NMaier
{
  class TVStore
  { 
    private static FileInfo storeFile = new FileInfo("tvstore.sqlite");
    private static bool initialized = false;

    private IDbConnection getConnection()
    {
      return Sqlite.GetDatabaseConnection(storeFile);
    }
    public void InitCache()
    {

      using (IDbConnection sqlconn = getConnection())
      {

        var col = new System.Collections.Concurrent.ConcurrentDictionary<int, TVShowInfo>();
        using (IDbCommand cmd = sqlconn.CreateCommand())
        {
          cmd.CommandText = "SELECT * FROM TVShow;";
          using (var rdr = cmd.ExecuteReader())
          {
            while (rdr.Read())
            {
              var tvshow = new TVShowInfo();
              tvshow.ID = rdr.GetInt32(0);
              tvshow.Name = rdr.GetString(1);
              tvshow.TVEpisodes = new List<TVEpisode>();
              col.TryAdd(tvshow.ID, tvshow);
            }
          }
        }

        using (IDbCommand cmd = sqlconn.CreateCommand())
        {
          cmd.CommandText = "SELECT * FROM TVSHowEntry;";
          using (var rdr = cmd.ExecuteReader())
          {
            while (rdr.Read())
            {
              var tvshow = new TVEpisode();
              var id = rdr.GetInt32(0);
              tvshow.Season = rdr.GetInt32(1);
              tvshow.Episode = rdr.GetInt32(2);
              tvshow.Title = rdr.GetString(3);

              var tv = col[id];
              tv.TVEpisodes.Add(tvshow);
              TheTVDB.cacheshow.TryAdd(tv.ID, tv);
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
          cmd.CommandText = "INSERT OR REPLACE INTO `TVSHow` (`id`, `name`) VALUES (@id, @name);";
          cmd2.CommandText = "INSERT OR REPLACE INTO TVSHowEntry (id, season, episode, title) VALUES (@id, @season, @episode, @title);";

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



          var eid = cmd2.CreateParameter();
          eid.ParameterName = "@id";
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

          foreach (var tventry in data)
          {
            tid.Value = tventry.ID;
            tname.Value = tventry.Name;
            cmd.ExecuteNonQuery();

            foreach (var ep in tventry.TVEpisodes)
            {
              eid.Value = tventry.ID;
              eepisode.Value = ep.Episode;
              eseason.Value = ep.Season;
              etitle.Value = ep.Title;
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
    public TVStore()
    {
      if (!initialized)
      {
        using (var conn = Sqlite.GetDatabaseConnection(storeFile)) {
          using (var c = conn.CreateCommand())
          {
            c.CommandText = "CREATE TABLE IF NOT EXISTS TVSHow (id int PRIMARY KEY ON CONFLICT REPLACE, name varchar(128));";
            c.ExecuteNonQuery();
          }
          using (var c0 = conn.CreateCommand())
          {
            c0.CommandText = "CREATE TABLE IF NOT EXISTS TVSHowEntry (id int, season int, episode int, title varchar(128), UNIQUE (id, season, episode) ON CONFLICT REPLACE);";
            c0.ExecuteNonQuery();
          }
        }
        this.InitCache();
        initialized = true;
      }
    }
  }
}
