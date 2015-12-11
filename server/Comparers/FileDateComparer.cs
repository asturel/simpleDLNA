using NMaier.SimpleDlna.Server.Metadata;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace NMaier.SimpleDlna.Server.Comparers
{
  internal class FileDateComparer : TitleComparer
  {
    public override string Description
    {
      get
      {
        return "Sort directories by file date, files by title";
      }
    }

    public override string Name
    {
      get
      {
        return "datealt";
      }
    }

    public override int Compare(IMediaItem x, IMediaItem y)
    {
      var xm = x as IMetaVideoItem;
      var ym = y as IMetaVideoItem;
      if (xm != null && ym != null && xm.Season.HasValue && ym.Season.HasValue && xm.Episode.HasValue && ym.Episode.HasValue)
      {
        var rv = xm.Season.Value.CompareTo(ym.Season.Value);
        if (rv != 0)
        {
          return rv;
        }
        var rv2 = xm.Episode.Value.CompareTo(ym.Episode.Value);
        if (rv2 != 0)
        {
          return rv2;
        }
      }
      if (x is IMediaFolder && y is IMediaFolder)
      {
        DateTime defaultDate = new DateTime(1970, 1, 1);
        var xf = (x as IMediaFolder);
        var yf = (y as IMediaFolder);
        var xfiles = xf.ChildItems.ToList();
        var yfiles = yf.ChildItems.ToList();
        var xmax = 
          (from xfile in xfiles
           let metainfo = xfile as IMetaInfo
           where metainfo != null
           select metainfo.InfoDate).DefaultIfEmpty(defaultDate).Max();

        var ymax =
            (from yfile in yfiles
             let metainfo = yfile as IMetaInfo
             where metainfo != null
             select metainfo.InfoDate).DefaultIfEmpty(defaultDate).Max();

        var rv = ymax.CompareTo(xmax);
        if (rv != 0)
        {
          return rv;
        }
      }
      return base.Compare(x, y);
    }
  }
}
