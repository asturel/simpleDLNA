using NMaier.SimpleDlna.Server.Metadata;
using NMaier.SimpleDlna.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO.Compression;
using System.Text.RegularExpressions;
using log4net;
using TvDbSharper;
using TvDbSharper.BaseSchemas;
using TvDbSharper.Clients.Series.Json;

namespace NMaier
{

  class TVEpisode
  {
    public int Season { get; set; }
    public int Episode { get; set; }
    public string Title { get; set; }
    public DateTime FirstAired { get; set; }
    public int AbsoluteNumber { get; set; }
    public int EpisodeId { get; set; }

  }
  class TVShowInfo
  {
    public int ID { get; set; }
    public string Name { get; set; }

    public List<TVEpisode> TVEpisodes { get; set; }

    public long LastUpdated { get; set; }

    public string IMDBID { get; set; }

    public TVShowInfo()
    {
      TVEpisodes = new List<TVEpisode>();
    }

    public string Find(int season, int episode)
    {
      int altepisode = episode;
      int altseason = season;
      if (season == 0)
      {
        altepisode = episode % 100;
        altseason = Math.Max((episode - altepisode) / 100, 1);
      }

      var res =
        this.TVEpisodes.Find(
            delegate (TVEpisode ep)
            {
              if (season != 0 || ep.AbsoluteNumber == 0 || TVEpisodes.Count < episode)
              {
                return ep.Episode == altepisode && ep.Season == altseason;
              }
              else
              {
                return ep.AbsoluteNumber == episode;
              }
            }
        );

      string ret = "";
      if (res != null)
      {
        ret = String.Format("{0}x{1}: {2}", res.Season, res.Episode, res.Title);
        if (season == 0 && res.AbsoluteNumber > 0 && res.Episode != altepisode)
        {
          ret = String.Format("{0} ({1})", ret, res.AbsoluteNumber);
        }
      }
      else
      {
        ret = String.Format("{0}x{1}", altseason, altepisode);
        if (season == 0 && episode != altepisode)
        {
          ret = String.Format("{0} ({1})", ret, episode);
        }
      }

      return ret;
    }
  }
  class TheTVDB
  {
    public static readonly ConcurrentDictionary<int, TVShowInfo> cacheshow = new ConcurrentDictionary<int, TVShowInfo>(); // :(
    private static readonly ConcurrentDictionary<string, int> cache = new ConcurrentDictionary<string, int>();
    private static readonly string tvdbkey = System.Configuration.ConfigurationSettings.AppSettings["TVShowDBKey"];
    private readonly static ILog logger =
          LogManager.GetLogger(typeof(TVStore));

    private static Regex seriesreg = new Regex(
             @"(.*?)(([^0-9][0-9]{1,2})x([0-9]{1,2}[^0-9])|S([0-9]{1,2})(E[0-9]{1,2})?|[_ -]([0-9]{1,3})[\._ -][^\dsS])",
             RegexOptions.Compiled | RegexOptions.IgnoreCase
             );

    private static Regex regreplace = new Regex(
          @"([a-z])[\._-]",
          RegexOptions.Compiled
          );

    private static Regex regreplace1 = new Regex(
          @"\[.*?\]",
          RegexOptions.Compiled | RegexOptions.IgnoreCase
          );

    private static Regex regreplace2 = new Regex(
          @"\(.*?\)",
          RegexOptions.Compiled | RegexOptions.IgnoreCase
          );


    public static async Task<System.Tuple<TVShowInfo, bool>> GetTVShowDetails(int showid, bool noncache = false)
    {
      TVShowInfo entry;
      if (!cacheshow.TryGetValue(showid, out entry) || noncache)
      {

        var client = new TvDbClient();
        await client.Authentication.AuthenticateAsync(tvdbkey);
        var tasks = new List<Task<TvDbResponse<BasicEpisode[]>>>();

        var firstResponse = await client.Series.GetEpisodesAsync(showid, 1);

        for (int i = 2; i <= firstResponse.Links.Last; i++)
        {
          tasks.Add(client.Series.GetEpisodesAsync(showid, i));
        }

        var series = await client.Series.GetAsync(showid);

        var results = await Task.WhenAll(tasks);

        var episodes = firstResponse.Data.Concat(results.SelectMany(x => x.Data));
        entry = new TVShowInfo
        {
          ID = series.Data.Id,
          IMDBID = series.Data.ImdbId,
          LastUpdated = series.Data.LastUpdated,
          Name = series.Data.SeriesName,
          TVEpisodes = episodes.Select(e =>
          {
            DateTime firstaired;
            if (!DateTime.TryParse(e.FirstAired, out firstaired))
            {
              firstaired = DateTime.MinValue;
            }
            return new TVEpisode
            {
              AbsoluteNumber = e.AbsoluteNumber ?? -1,
              Episode = e.AiredEpisodeNumber ?? -1,
              EpisodeId = e.AiredEpisodeNumber ?? -1,
              FirstAired = firstaired,
              Season = e.AiredSeason ?? -1,
              Title = e.EpisodeName
            };
          }).ToList()
        };
        cacheshow.AddOrUpdate(showid, entry, (key, oldvalue) => (oldvalue.LastUpdated > entry.LastUpdated ? oldvalue : entry));
        return new System.Tuple<TVShowInfo, bool>(entry, true);
      }
      return new System.Tuple<TVShowInfo, bool>(entry, false);
    }

    public static async Task<int?> GetTVShowID(string path)
    {
      try
      {
        if (path.ToLower().Contains("movies"))
        {
          return null;
        }
        var p = System.IO.Directory.GetParent(path);
        string hit = p.Name;

        /*
        var sorozat = seriesreg.Match(p.Name);
        if (!sorozat.Success)
        {
          sorozat = seriesreg.Match(System.IO.Path.GetFileNameWithoutExtension(path));
        }


        if (sorozat.Success){
          hit = sorozat.Groups[1].Value;
          hit = regreplace.Replace(hit, @"$1 ");
          hit = regreplace1.Replace(hit, "");
          hit = regreplace2.Replace(hit, "");
        }
        */

        var sorozat = p.Name.TryGetName();
        if ((sorozat is SimpleDlna.Utilities.Formatting.NiceSeriesName) == false || String.IsNullOrEmpty(sorozat.Name))
        {
          sorozat = System.IO.Path.GetFileNameWithoutExtension(path).TryGetName();
        }
        if (sorozat is SimpleDlna.Utilities.Formatting.NiceSeriesName)
        {
          hit = sorozat.Name;
        }
        else
        {
          return null;
        }


        int entry;

        if (!cache.TryGetValue(hit, out entry))
        {


          var client = new TvDbClient();
          await client.Authentication.AuthenticateAsync(tvdbkey);

          var res = await client.Search.SearchSeriesByNameAsync(hit);
          if (res != null && res.Data.Length > 0)
          {
            entry = res.Data.First().Id;
            cache.TryAdd(hit, entry);

          }
          else
          {
            cache.TryAdd(hit, -1);
            logger.InfoFormat("TVDB: Cant find in database {0} -- {1}", path, hit);
            return 0;
          }
        }
        return entry;
      }

      catch (Exception e)
      {
        logger.Error(String.Format("TV: Failed to get TVShowID for {0}", path), e);
        return null;
      }
    }
    public class UpdateInfo
    {
      public Int32[] Series { get; set; }
      public Int32[] Episodes { get; set; }

    }
    public static async Task<UpdateInfo> FetchUpdate(DateTime timeframe)
    {
      var client = new TvDbClient();
      await client.Authentication.AuthenticateAsync(tvdbkey);
      var response = await client.Updates.GetAsync(timeframe, DateTime.Now);

      return new UpdateInfo
      {
        Series = response?.Data?.Select(s => s.Id)?.ToArray() ?? new Int32[] { },
        Episodes = new Int32[] { }

      };

    }

  }
}


