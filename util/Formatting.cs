using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Linq;

namespace NMaier.SimpleDlna.Utilities
{
  public static class Formatting
  {
    private readonly static Regex sanitizer = new Regex(
      @"\b(?:the|an?|ein(?:e[rs]?)?|der|die|das)\b",
      RegexOptions.IgnoreCase | RegexOptions.Compiled
      );

    private readonly static Regex trim = new Regex(
      @"\s+|^[._+)}\]-]+|[._+({\[-]+$",
      RegexOptions.Compiled
      );

    private readonly static Regex trimmore =
      new Regex(@"^[^\d\w]+|[^\d\w]+$", RegexOptions.Compiled);

    private readonly static Regex respace =
      new Regex(@"[.+]+", RegexOptions.Compiled);

    public static bool Booley(string str)
    {
      str = str.Trim();
      var sc = StringComparer.CurrentCultureIgnoreCase;
      return sc.Equals("yes", str) || sc.Equals("1", str) || sc.Equals("true", str);
    }

    public static string FormatFileSize(this long size)
    {
      if (size < 900) {
        return string.Format("{0} B", size);
      }
      var ds = size / 1024.0;
      if (ds < 900) {
        return string.Format("{0:F2} KB", ds);
      }
      ds /= 1024.0;
      if (ds < 900) {
        return string.Format("{0:F2} MB", ds);
      }
      ds /= 1024.0;
      if (ds < 900) {
        return string.Format("{0:F3} GB", ds);
      }
      ds /= 1024.0;
      if (ds < 900) {
        return string.Format("{0:F3} TB", ds);
      }
      ds /= 1024.0;
      return string.Format("{0:F4} PB", ds);
    }

    public static string GetSystemName()
    {
      var buf = Marshal.AllocHGlobal(8192);
      // This is a hacktastic way of getting sysname from uname ()
      if (SafeNativeMethods.uname(buf) != 0) {
        throw new ArgumentException("Failed to get uname");
      }
      var rv = Marshal.PtrToStringAnsi(buf);
      Marshal.FreeHGlobal(buf);
      return rv;
    }

    public static string StemCompareBase(this string name)
    {
      if (name == null) {
        throw new ArgumentNullException("name");
      }

      var san = trimmore.Replace(
        sanitizer.Replace(name, string.Empty),
        string.Empty).Trim();
      if (string.IsNullOrWhiteSpace(san)) {
        return name;
      }
      return san.StemNameBase();
    }
/*
    private readonly static Regex seriesreg = new Regex(
        @"(?<title>.*?)((?<season>[^0-9][0-9]{1,2})x(?<episode>[0-9]{1,2}[^0-9])|[ \._\-](?<episode>[0-9]{3})([ \._\-]|$)|(S(?<season>[0-9]{1,2})+(E(?<episode>[0-9]{1,2})| ))|[_ \-](?<episode>[0-9]{1,3})[\._ \-][^\dsS])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly static string[] sregs = new string[] {
      @"(?<title>.*?)((?<season>[^0-9][0-9]{1,2})x(?<episode>[0-9]{1,2}[^0-9])", @"[ \._\-](?<episode>[0-9]{3})([ \._\-]|$)",@"(S(?<season>[0-9]{1,2})+(E(?<episode>[0-9]{1,2})| ))",@"[_ \-](?<episode>[0-9]{1,3})[\._ \-][^\dsS])"};
*/
    private readonly static Regex[] seriesregs = new Regex[] {
      new Regex(@"(?<title>.*?)([^0-9](?<season>[0-9]{1,2})x((?<episode>[0-9]{1,2})[^0-9]))",RegexOptions.Compiled | RegexOptions.IgnoreCase),
      new Regex(@"(?<title>.*?)(S(?<season>[0-9]{1,2}).?(E(?<episode>[0-9]{1,2})|[ \._-]))",RegexOptions.Compiled | RegexOptions.IgnoreCase),
      new Regex(@"(?<title>.*?)[ \._\-](?<episode>[0-9]{3})([ \._\-]|$)",RegexOptions.Compiled | RegexOptions.IgnoreCase),
      new Regex(@"(?<title>.*?)[_ \-](?<episode>[0-9]{2,3})[\._ \-][^\dsS]",RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private readonly static Regex movieclear = new Regex(
            @"(?<title>.*?)[._ ]?((?<year>[0-9]{4})|[0-9]{3,4}p)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly static Regex cleanstr = new Regex(
            @"^\[.*?\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly static Regex resolionRegex = new Regex(
            @"[_ \.](\d+p)[_ \.]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly static Regex releaserRegex = new Regex(
            @"[\- \.](\w+?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public class NiceName
    {
      public string Name
      {
        get; set;
      }
      public string Releaser
      {
        get; set;
      }
      public string Resolution
      {
        get; set;
      }
    }
    public class NiceSeriesName : NiceName
    {
      public int Season { get; set; }
      public int Episode { get; set; }
    }
    public class MovieName : NiceName
    {
      public int Year { get; set; }
    }

    public static NiceName TryGetName(this string name)
    {
      var releaser = releaserRegex.Match(name);
      var resolution = resolionRegex.Match(name);
      Match res = null;
      foreach (var r in seriesregs)
      {
        res = r.Match(name);
        if (res.Success)
        {
          break;
        }
      }

      int season = 0;
      int episode = 0;
      string nicename = "";
      string resultionText = "";
      string releaserText = "";

      if (releaser.Success)
      {
        resultionText = releaser.Groups[1].Value;
      } else
      {

      }
      if (resolution.Success)
      {
        releaserText = resolution.Groups[1].Value;
      }



      if (res.Success)
      {
        if (!string.IsNullOrEmpty(res.Groups["title"].Value))
        {
          nicename = res.Groups["title"].Value;
        } else
        {
          Console.WriteLine(name);
          Console.WriteLine("ERROR PARSING " + res.Groups["title"].Value);
        }
        if (!string.IsNullOrEmpty(res.Groups["season"].Value))
        {
          //season = Int32.Parse(res.Groups["season"].Value);
          if (!Int32.TryParse(res.Groups["season"].Value,out season))
          {
            Console.WriteLine(name);
            Console.WriteLine("ERROR PARSING " + res.Groups["season"].Value);
          }
        }

        if (!string.IsNullOrEmpty(res.Groups["episode"].Value))
        {
          //episode = Int32.Parse(res.Groups["episode"].Value);
          if (!Int32.TryParse(res.Groups["episode"].Value, out episode))
          {
            Console.WriteLine(name);
            Console.WriteLine("ERROR PARSING " + res.Groups["episode"].Value);
          } else
          {
            /*
            if (season == 0)
            {
              var seasonandep = episode;
              episode = episode % 100;
              season = Math.Max((seasonandep - episode) / 100, 1);
            }
            */
          }
        }
        nicename = cleanstr.Replace(nicename.StemNameBase(), "");
        /*
        if (!String.IsNullOrEmpty(resultionText))
        {
          nicename = String.Format("{0} ({1},{2})", nicename, resultionText, releaserText);
        }
        */


        return new NiceSeriesName() { Name = nicename, Episode = episode, Season = season, Resolution = resultionText, Releaser = releaserText };

      } else
      {
        var res2 = movieclear.Match(name);
        int year = 0;
        if (res2.Success)
        {
          if (!string.IsNullOrEmpty(res2.Groups["title"].Value))
          {
            nicename = res2.Groups["title"].Value;
          } else
          {
            Console.WriteLine(name);
            Console.WriteLine("ERROR PARSING " + res.Groups["title"].Value);
          }
          if (!string.IsNullOrEmpty(res2.Groups["year"].Value))
          {
            year = Int32.Parse(res2.Groups["year"].Value);
          }
          return new MovieName() { Name = cleanstr.Replace(nicename.StemNameBase(), ""), Year = year, Resolution = resultionText, Releaser = releaserText };
        }
        
      }
      return null;
    }

    public static string StemNameBase(this string name)
    {
      if (name == null) {
        throw new ArgumentNullException("name");
      }

      if (!name.Contains(" ")) {
        name = name.Replace('_', ' ');
        if (!name.Contains(" ")) {
          name = name.Replace('-', ' ');
        }
        name = respace.Replace(name, " ");
      }
      var ws = name;
      var wsprev = name;
      do {
        wsprev = ws;
        ws = trim.Replace(wsprev.Trim(), " ").Trim();
      }
      while (wsprev != ws);
      if (string.IsNullOrWhiteSpace(ws)) {
        return name;
      }
      return ws;
    }
  }
}
