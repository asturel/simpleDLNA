using NMaier.SimpleDlna.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace NMaier.SimpleDlna.FileMediaServer
{
  [Serializable]
  internal sealed class VideoFile
    : BaseFile, IMediaVideoResource, ISerializable, IBookmarkable
  {
    private string[] actors;

    private long? bookmark;

    private string description;

    private string director;

    private TimeSpan? duration;

    private static readonly TimeSpan EmptyDuration = new TimeSpan(0);

    private string genre;

    private int? height;

    private bool initialized = false;

    private Subtitle subTitle;

    private string title;

    private int? width;

    private int? tvshowid;

    private string seriesname;

    private bool isSeries = false;

    private readonly static Regex seriesreg = new Regex(
            //@"(([0-9]{1,2})x([0-9]{1,2})|S([0-9]{1,2})+E([0-9]{1,2}))",
            @"(([0-9]{1,2})x([0-9]{1,2})|[ \._\-]([0-9]{3})([ \._\-]|$)|(S([0-9]{1,2})+(E([0-9]{1,2})| )))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
            );
    private readonly static Regex movieclear = new Regex(
            @"(.*?)[._ ]?(([0-9]{4})|[0-9]{3,4}p)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
            );

    private VideoFile(SerializationInfo info, StreamingContext ctx)
      : this(info, ctx.Context as DeserializeInfo)
    {
    }


    private VideoFile(SerializationInfo info, DeserializeInfo di)
      : this(di.Server, di.Info, di.Type)
    {
      actors = info.GetValue("a", typeof(string[])) as string[];
      description = info.GetString("de");
      director = info.GetString("di");
      genre = info.GetString("g");
      //title = info.GetString("t");
      try {
        width = info.GetInt32("w");
        height = info.GetInt32("h");
      }
      catch (Exception) {
      }
      var ts = info.GetInt64("du");
      if (ts > 0) {
        duration = new TimeSpan(ts);
      }
      try {
        bookmark = info.GetInt64("b");
      }
      catch (Exception) {
        bookmark = 0;
      }
      try {
        //subTitle = info.GetValue("st", typeof(Subtitle)) as Subtitle;
        subTitle = new Subtitle(new System.IO.FileInfo(this.Path));
      }
      catch (Exception) {
        subTitle = null;
      }
      try {
        this.tvshowid = info.GetInt32("tvid");

        if (tvshowid == null)
        {
          this.tvshowid = TheTVDB.GetTVShowID(this.Path);
        }
        if (tvshowid != null && tvshowid > 0)
        {

          var tvinfo = TheTVDB.GetTVShowDetails(this.tvshowid.Value);
          Server.UpdateTVCache(tvinfo);
          this.seriesname = tvinfo.Name;

          var seriesreg0 = seriesreg.Match(base.Title);               
          if (seriesreg0.Success)
          {
            isSeries = true;
            var season = 0;
            var episode = 0;
            if (seriesreg0.Groups[2].Value != "")
            {
              season = System.Int32.Parse(seriesreg0.Groups[2].Value);
              episode = System.Int32.Parse(seriesreg0.Groups[3].Value);
            }
            else if (seriesreg0.Groups[6].Value != "")
            {
              season = System.Int32.Parse(seriesreg0.Groups[7].Value);
              System.Int32.TryParse(seriesreg0.Groups[9].Value, out episode);
            }
            else if (seriesreg0.Groups[4].Value != "")
            {
              var seasonandep = System.Int32.Parse(seriesreg0.Groups[4].Value);
              episode = seasonandep % 100;
              season = (seasonandep - episode) / 100;
            }
            if (season > 0 && episode > 0)
            {
              var t = tvinfo.Find(season, episode);
              this.title = t;
            }
          } else
          {
            this.title = tvinfo.Name;
          }
        }

      } catch (Exception exn)
      {
        if (exn is System.ArgumentNullException)
        {
        }
        else
        {
          this.tvshowid = TheTVDB.GetTVShowID(this.Path);
        }

      }
      if (this.seriesname == null)
      {
        var seriesreg0 = seriesreg.Match(base.Title);

        if (seriesreg0.Success)
        {
          var season = 0;
          var episode = 0;
          if (seriesreg0.Groups[2].Value != "")
          {
            season = System.Int32.Parse(seriesreg0.Groups[2].Value);
            episode = System.Int32.Parse(seriesreg0.Groups[3].Value);
          }
          else if (seriesreg0.Groups[6].Value != "")
          {
            season = System.Int32.Parse(seriesreg0.Groups[7].Value);
            System.Int32.TryParse(seriesreg0.Groups[9].Value, out episode);
          }
          else if (seriesreg0.Groups[4].Value != "")
          {
            var seasonandep = System.Int32.Parse(seriesreg0.Groups[4].Value);
            episode = seasonandep % 100;
            season = (seasonandep - episode) / 100;
          }

          var xx = "-";
          if (episode > 0 && season > 0)
          {
            xx = String.Format("{0}x{1}", season, episode);
          }
          else
          {
            if (episode > 0)
            {
              xx = episode.ToString();
            }
            else
            {
              xx = season.ToString();
            }
          }
          this.title = xx;
          this.seriesname = System.IO.Directory.GetParent(this.Path).Name;
        } else {
          var t = movieclear.Match(System.IO.Directory.GetParent(this.Path).Name);
          if (t.Success)
          {
            this.seriesname = String.Format("{0} ({1})", t.Groups[1].Value, t.Groups[3].Value);
          }
          else
          {
            this.seriesname = System.IO.Directory.GetParent(this.Path).Name;
          }
          this.seriesname = this.seriesname.Replace(".", " ").Replace("_", " ");


        }
      }

      Server.UpdateFileCache(this);
      initialized = true;
    }

    internal VideoFile(FileServer server, FileInfo aFile, DlnaMime aType)
      : base(server, aFile, aType, DlnaMediaTypes.Video)
    {
    }

    public long? Bookmark
    {
      get
      {
        return bookmark;
      }
      set
      {
        bookmark = value;
        Server.UpdateFileCache(this);
      }
    }

    public IEnumerable<string> MetaActors
    {
      get
      {
        MaybeInit();
        return actors;
      }
    }
    public string MovieTitle
    {
      get
      {
        if (this.seriesname != null)
        {
          return this.seriesname;
        } else { return base.Title; }
        
      }
    }
    public bool IsSeries
    {
      get
      {
        return this.isSeries;
      }
    }
    public string MetaDescription
    {
      get
      {
        MaybeInit();
        return description;
      }
    }

    public string MetaDirector
    {
      get
      {
        MaybeInit();
        return director;
      }
    }

    public int? TVShowDBId
    {
      get
      {
        //MaybeInit();
        return tvshowid;
      }
    }

    public TimeSpan? MetaDuration
    {
      get
      {
        MaybeInit();
        return duration;
      }
    }

    public string MetaGenre
    {
      get
      {
        MaybeInit();
        if (string.IsNullOrWhiteSpace(genre)) {
          throw new NotSupportedException();
        }
        return genre;
      }
    }

    public int? MetaHeight
    {
      get
      {
        MaybeInit();
        return height;
      }
    }

    public int? MetaWidth
    {
      get
      {
        MaybeInit();
        return width;
      }
    }

    public override IHeaders Properties
    {
      get
      {
        MaybeInit();
        var rv = base.Properties;
        if (description != null) {
          rv.Add("Description", description);
        }
        if (actors != null && actors.Length != 0) {
          rv.Add("Actors", string.Join(", ", actors));
        }
        if (director != null) {
          rv.Add("Director", director);
        }
        if (duration != null) {
          rv.Add("Duration", duration.Value.ToString("g"));
        }
        if (genre != null) {
          rv.Add("Genre", genre);
        }
        if (width != null && height != null) {
          rv.Add(
            "Resolution",
            string.Format("{0}x{1}", width.Value, height.Value)
          );
        }
        return rv;
      }
    }

    public Subtitle Subtitle
    {
      get
      {
        try {
          if (subTitle == null) {
            subTitle = new Subtitle(Item);
            Server.UpdateFileCache(this);
          }
        }
        catch (Exception ex) {
          Error("Failed to look up subtitle", ex);
          subTitle = new Subtitle();
        }
        return subTitle;
      }
    }

    public override string Title
    {
      get
      {
        if (!string.IsNullOrWhiteSpace(this.title)) {
          //return string.Format("{0} — {1}", base.Title, title);
          return this.title;
        }
        return base.Title;
      }
    }

    private void MaybeInit()
    {
      if (initialized) {
        return;
      }
      
      if (tvshowid == null)
      {
        tvshowid = TheTVDB.GetTVShowID(this.Path);
      }
      

      try {
        using (var tl = TagLib.File.Create(new TagLibFileAbstraction(Item))) {
          try {
            duration = tl.Properties.Duration;
            if (duration.HasValue && duration.Value.TotalSeconds < 0.1) {
              duration = null;
            }
            width = tl.Properties.VideoWidth;
            height = tl.Properties.VideoHeight;
          }
          catch (Exception ex) {
            Debug("Failed to transpose Properties props", ex);
          }

          try {
            var t = tl.Tag;
            genre = t.FirstGenre;
            //title = t.Title;
            description = t.Comment;
            director = t.FirstComposerSort;
            if (string.IsNullOrWhiteSpace(director)) {
              director = t.FirstComposer;
            }
            actors = t.PerformersSort;
            if (actors == null || actors.Length == 0) {
              actors = t.PerformersSort;
              if (actors == null || actors.Length == 0) {
                actors = t.Performers;
                if (actors == null || actors.Length == 0) {
                  actors = t.AlbumArtists;
                }
              }
            }
          }
          catch (Exception ex) {
            Debug("Failed to transpose Tag props", ex);
          }
        }

        initialized = true;

        Server.UpdateFileCache(this);
      }
      catch (TagLib.CorruptFileException ex) {
        Debug(
          "Failed to read meta data via taglib for file " + Item.FullName, ex);
        initialized = true;
      }
      catch (TagLib.UnsupportedFormatException ex) {
        Debug(
          "Failed to read meta data via taglib for file " + Item.FullName, ex);
        initialized = true;
      }
      catch (Exception ex) {
        Warn(
          "Unhandled exception reading meta data for file " + Item.FullName,
          ex);
      }
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (info == null) {
        throw new ArgumentNullException("info");
      }
      MaybeInit();
      info.AddValue("a", actors, typeof(string[]));
      info.AddValue("de", description);
      info.AddValue("di", director);
      info.AddValue("g", genre);
      info.AddValue("t", title);
      info.AddValue("w", width);
      info.AddValue("h", height);
      info.AddValue("b", bookmark);
      info.AddValue("du", duration.GetValueOrDefault(EmptyDuration).Ticks);
      //info.AddValue("st", subTitle);
      info.AddValue("tvid", (tvshowid.HasValue ? tvshowid.Value : -1));
      //info.AddValue("tvname", tvshow.Name);
    }
  }
}
