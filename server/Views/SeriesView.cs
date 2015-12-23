using NMaier.SimpleDlna.Utilities;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace NMaier.SimpleDlna.Server.Views
{
  internal sealed class SeriesView : BaseView
  {
    private bool cascade = true;

    private readonly static Regex movieclear = new Regex(
            @"(.*?)[._ ]?(([0-9]{4})|[0-9]{3,4}p)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
            );

    

    private readonly static Regex seriesreg = new Regex(
            //@"(([0-9]{1,2})x([0-9]{1,2})|S([0-9]{1,2})+E([0-9]{1,2}))",
            @"(([0-9]{1,2})x([0-9]{1,2})|[ \._\-]([0-9]{3})([ \._\-]|$)|(S([0-9]{1,2})+(E([0-9]{1,2})| )))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
            );

    private readonly static Regex re_series = new Regex(
      @"^(.+?)(?:s\d+[\s_-]*e\d+|" + // S01E10
      @"\d+[\s_-]*x[\s_-]*\d+|" + // 1x01
      @"\b[\s-_]*(?:19|20|21)[0-9]{2}[\s._-](?:0[1-9]|1[012])[\s._-](?:0[1-9]|[12][0-9]|3[01])|" + // 2014.02.20
      @"\b[\s-_]*(?:0[1-9]|[12][0-9]|3[01])[\s._-](?:0[1-9]|1[012])[\s._-](?:19|20|21)[0-9]{2}|" + // 20.02.2014 (sane)
      @"\b[\s-_]*(?:0[1-9]|1[012])[\s._-](?:0[1-9]|[12][0-9]|3[01])[\s._-](?:19|20|21)[0-9]{2}|" + // 02.20.2014 (US)
      @"\b[1-9](?:0[1-9]|[1-3]\d)\b)", // 101
      RegexOptions.Compiled | RegexOptions.IgnoreCase
      );

    public override string Description
    {
      get
      {
        return "Try to determine (TV) series from title and categorize accordingly";
      }
    }

    public override string Name
    {
      get
      {
        return "series";
      }
    }

    private static void SortFolder(IMediaFolder folder,
                                   SimpleKeyedVirtualFolder series)
    {
      foreach (var f in folder.ChildFolders.ToList()) {
        SortFolder(f, series);
      }
      foreach (var i in folder.ChildItems.ToList()) {
        var title = i.Title;
        if (string.IsNullOrWhiteSpace(title)) {
          continue;
        }
        var m = re_series.Match(title);
        if (!m.Success) {
          continue;
        }
        var ser = m.Groups[1].Value;
        if (string.IsNullOrEmpty(ser)) {
          continue;
        }
        series.GetFolder(ser.StemNameBase()).AddResource(i);
        folder.RemoveResource(i);
      }
    }

    public override void SetParameters(AttributeCollection parameters)
    {
      var sc = StringComparer.CurrentCultureIgnoreCase;
      foreach (var attr in parameters) {
        if (sc.Equals(attr.Key, "cascade") && !string.IsNullOrWhiteSpace(attr.Value) && !Formatting.Booley(attr.Value)) {
          cascade = false;
        }
        if (sc.Equals("no-cascade")) {
          cascade = true;
        }
      }
    }
    public override IMediaFolder Transform(IMediaFolder Root)
    {
      var root = new VirtualClonedFolder(Root);
      var series = new SimpleKeyedVirtualFolder(root, "Series");
      var movies = new SimpleKeyedVirtualFolder(root, "Movies");
      //SortFolder(root, series);
      /*
      foreach (var f in series.ChildFolders.ToList()) {
        var fsmi = f as VirtualFolder;
        root.AdoptFolder(fsmi);
      }
      if (!cascade) {
        return root;
      }

      var cascaded = new DoubleKeyedVirtualFolder(root, "Series"); */
      /*
      foreach (var i in root.ChildFolders.ToList()) {
        
//        var folder = cascaded.GetFolder(i.Title.StemCompareBase().Substring(0, 1).ToUpper());
//        folder.AdoptFolder(i);
               
        foreach (var c in i.ChildItems)
        {
          var c0 = c as IMediaVideoResource;
          var folder = cascaded.GetFolder(c0 != null ? c0.MovieTitle : i.Title);
          folder.AddResource(c);
        }
      }
      foreach (var i in root.ChildItems.ToList()) {
        //        var folder = cascaded.GetFolder(i.Title.StemCompareBase().Substring(0, 1).ToUpper());
        //        folder.AddResource(i);
        //        cascaded.AddResource(i);
        var c0 = i as IMediaVideoResource;
        var folder = cascaded.GetFolder(c0 != null ? c0.MovieTitle : i.Title);
        folder.AddResource(i);
      }
      */
      /*
      var items = (from i in root.AllItems.ToList()
                    let d = (i as IMediaVideoResource).InfoDate
                    orderby d
                    select i).ToList();
      */
      foreach (var c in root.AllItems.ToList())
      {
        var c0 = c as IMediaVideoResource;
        var folder = (c0 != null && c0.IsSeries ? series : movies).GetFolder(c0 != null ? c0.MovieTitle : c.Title);
        //var folder = new DoubleKeyedVirtualFolder((c0 != null && c0.IsSeries ? series : movies),(c0 != null ? c0.MovieTitle : c.Title) );
        if (c0.Progress <= 85)
        {
          folder.AddResource(c);
        } else
        {
          var folder1 = folder.ChildFolders.ToList().Find(f => f.Title == "WATCHED");
          if (folder1 == null) folder1 = new VirtualFolder(folder, "WATCHED");

          folder1.AddResource(c);
          folder.AdoptFolder(folder1);
        }
        
        root.RemoveResource(c);
      }
      foreach (var f in root.ChildFolders.ToList())
      {
        root.ReleaseFolder(f);
      }
      
      root.AdoptFolder(series);
      root.AdoptFolder(movies);
      return root;
    }
  }
}
