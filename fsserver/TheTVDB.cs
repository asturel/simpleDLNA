using NMaier.SimpleDlna.Server.Metadata;
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
namespace NMaier
{

  class TVEpisode
  {
    public int Season { get; set; }
    public int Episode { get; set; }
    public string Title { get; set; }
  }
  class TVShowInfo
  {
    public int ID { get; set; }
    public string Name { get; set; }

    public List<TVEpisode> TVEpisodes { get; set; }

    public long LastUpdated { get; set; }

    public TVShowInfo()
    {
      TVEpisodes = new List<TVEpisode>();
    }

    public string Find(int season, int episode)
    {
      var res =
        this.TVEpisodes.Find(
            delegate (TVEpisode ep)
            {
              return ep.Episode == episode && ep.Season == season;
            }
        );
      if (res != null)
      {
        return String.Format("{0}x{1}: {2}", season, episode, res.Title);
      }
      else
      {
        return String.Format("{0}x{1}", season, episode);
      }
    }
  }
  class TheTVDB
  {
    public static readonly ConcurrentDictionary<int, TVShowInfo> cacheshow = new ConcurrentDictionary<int, TVShowInfo>(); // :(
    private static readonly ConcurrentDictionary<string, int> cache = new ConcurrentDictionary<string, int>();
    private static readonly string tvdbkey = System.Configuration.ConfigurationSettings.AppSettings["TVShowDBKey"];  

    private static Regex seriesreg = new Regex(
             @"(.*?)(([0-9]{1,2})x([0-9]{1,2})|S([0-9]{1,2})(E[0-9]{1,2})?)",
             RegexOptions.Compiled | RegexOptions.IgnoreCase
             );

    private static Regex regreplace = new Regex(
          @"([a-z])[\._-]",
          RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase
          );

    private static Regex regreplace1 = new Regex(
          @"\[.*?\]",
          RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase
          );

    private static Regex regreplace2 = new Regex(
          @"\(.*?\)",
          RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase
          );


    public static TVShowInfo GetTVShowDetails(int showid)
    {
      TVShowInfo entry;
      if (!cacheshow.TryGetValue(showid, out entry))
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
            entry.TVEpisodes = new List<TVEpisode>();

            var episodes = xmlDoc.SelectNodes("//Episode");
            foreach (XmlNode ep in episodes)
            {
              var seasoninfo = new TVEpisode();

              var epnum = ep.SelectSingleNode(".//Combined_episodenumber").InnerText;
              var seasonnum = ep.SelectSingleNode(".//Combined_season").InnerText;
              var title = ep.SelectSingleNode(".//EpisodeName").InnerText;


              seasoninfo.Episode = (int)Math.Ceiling(System.Double.Parse(epnum, new System.Globalization.CultureInfo("en-US")));
              seasoninfo.Season = (int)Math.Ceiling(System.Double.Parse(seasonnum, new System.Globalization.CultureInfo("en-US")));
              seasoninfo.Title = title;
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

        var p = System.IO.Directory.GetParent(path);
        var sorozat = seriesreg.Match(p.Name);
        if (!sorozat.Success)
        {
          sorozat = seriesreg.Match(System.IO.Path.GetFileNameWithoutExtension(path));
        }

        if (sorozat.Success)
        {
          var hit = sorozat.Groups[1].Value;
          hit = regreplace.Replace(hit, @"$1 ");
          hit = regreplace1.Replace(hit, "");
          hit = regreplace2.Replace(hit, "");

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

            try
            {
              entry = System.Int32.Parse(xmlDoc.SelectSingleNode("//seriesid").InnerText);
              cache.TryAdd(hit, entry);
            }
            catch (Exception)
            {
              cache.TryAdd(hit, -1);
              return -1;
            }
          }
          return entry;
        }
        return -1;
      }
      catch (Exception)
      {
        return -1;

      }
    }
    public static int[] UpdatesSince(long time)
    {

      var url = String.Format("http://thetvdb.com/api/Updates.php?type=all&time={0}", time);
      string xmlStr;
      using (var wc = new System.Net.WebClient())
      {
        xmlStr = wc.DownloadString(url);
      }
      var xmlDoc = new System.Xml.XmlDocument();
      xmlDoc.LoadXml(xmlStr);

      var l = xmlDoc.SelectNodes("//Series").OfType<XmlNode>();

      return
        (from n in l
         select System.Int32.Parse(n.InnerText)).ToArray();






    }
  }


}


