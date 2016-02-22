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
              } else
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


    public static TVShowInfo GetTVShowDetails(int showid, bool noncache=false)
    {
      TVShowInfo entry;
      if (!cacheshow.TryGetValue(showid, out entry) || noncache)
      {
        var url = String.Format("http://thetvdb.com/api/{1}/series/{0}/all/en.zip", showid, tvdbkey);
        byte[] xmlData;
        
        using (var wc = new System.Net.WebClient())
        {
          xmlData = wc.DownloadData(url);
        }

        var xmlStream = new System.IO.MemoryStream(xmlData);
        ZipArchive archive = new ZipArchive(xmlStream, ZipArchiveMode.Read);

        foreach (var a in archive.Entries)
        {
          if (a.Name == "en.xml")
          {
            var memoryStream = new System.IO.MemoryStream();
            var x = a.Open();
            x.CopyTo(memoryStream);
            var t = memoryStream.ToArray();

            string xmlStr = System.Text.Encoding.UTF8.GetString(t);
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(xmlStr);

            entry = new TVShowInfo();
            entry.ID = showid;
            entry.Name = xmlDoc.SelectSingleNode("//SeriesName").InnerText;
            entry.LastUpdated = System.Int64.Parse(xmlDoc.SelectSingleNode(".//lastupdated").InnerText);
            entry.IMDBID = xmlDoc.SelectSingleNode("//IMDB_ID").InnerText;
            entry.TVEpisodes = new List<TVEpisode>();

            var airtime = xmlDoc.SelectSingleNode("//Airs_Time").InnerText;

            var episodes = xmlDoc.SelectNodes("//Episode");
            foreach (XmlNode ep in episodes)
            {
              var seasoninfo = new TVEpisode();
              seasoninfo.FirstAired = new System.DateTime();

              var epnum = ep.SelectSingleNode(".//EpisodeNumber").InnerText;
              var seasonnum = ep.SelectSingleNode(".//SeasonNumber").InnerText;
              var title = ep.SelectSingleNode(".//EpisodeName").InnerText;
              var firstaired = ep.SelectSingleNode(".//FirstAired").InnerText;
              var absnumber = ep.SelectSingleNode(".//absolute_number").InnerText;

              DateTime faired;
              if (!String.IsNullOrEmpty(firstaired))
              {
                if (DateTime.TryParse(firstaired + " " + airtime, out faired))
                {
                  seasoninfo.FirstAired = faired;
                }
              }

              seasoninfo.Episode = (int)Math.Ceiling(System.Double.Parse(epnum, new System.Globalization.CultureInfo("en-US")));
              seasoninfo.Season = (int)Math.Ceiling(System.Double.Parse(seasonnum, new System.Globalization.CultureInfo("en-US")));
              seasoninfo.Title = title;
              if (!string.IsNullOrEmpty(absnumber))
              {
                seasoninfo.AbsoluteNumber = (System.Int32.Parse(absnumber, new System.Globalization.CultureInfo("en-US")));
              }
              seasoninfo.EpisodeId = (System.Int32.Parse(ep.SelectSingleNode(".//EpisodeNumber").InnerText, new System.Globalization.CultureInfo("en-US")));

              entry.TVEpisodes.Add(seasoninfo);
            }
            cacheshow.AddOrUpdate(showid, entry, (key, oldvalue) => (oldvalue.LastUpdated > entry.LastUpdated ? oldvalue : entry));
          }
        }

      }
      return entry;

    }

    public static int? GetTVShowID(string path)
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
        } else
        {
          return null;
        }


        int entry;

        if (!cache.TryGetValue(hit, out entry))
        {
          var url = String.Format("http://thetvdb.com/api/GetSeries.php?seriesname={0}", hit);
          string xmlStr;
          using (var wc = new System.Net.WebClient())
          {
            xmlStr = wc.DownloadString(url);
          }
          var xmlDoc = new System.Xml.XmlDocument();
          xmlDoc.LoadXml(xmlStr);

          var seriesidText = xmlDoc.SelectSingleNode("//seriesid");
          if (seriesidText != null)
          {
            entry = System.Int32.Parse(xmlDoc.SelectSingleNode("//seriesid").InnerText);
            cache.TryAdd(hit, entry);
          } else { 
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
    public static UpdateInfo UpdatesSince(long time)
    {
      UpdateInfo info = new UpdateInfo();
      var url = String.Format("http://thetvdb.com/api/Updates.php?type=all&time={0}", time);
      string xmlStr;
      using (var wc = new System.Net.WebClient())
      {
        xmlStr = wc.DownloadString(url);
      }
      var xmlDoc = new System.Xml.XmlDocument();
      xmlDoc.LoadXml(xmlStr);

      var s = xmlDoc.SelectNodes("//Series").OfType<XmlNode>();
      info.Series = (from n in s
                   select System.Int32.Parse(n.InnerText)).ToArray();

      var e = xmlDoc.SelectNodes("//Episode").OfType<XmlNode>();
      info.Episodes = (from n in e
                   select System.Int32.Parse(n.InnerText)).ToArray();

      return info;

    }
  }
}


