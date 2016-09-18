using NMaier.SimpleDlna.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using NMaier.SimpleDlna.Utilities;
using System.Linq;
using System.Reflection;

namespace NMaier.SimpleDlna.FileMediaServer
{
  [Serializable]
  internal sealed class VideoFile
    : BaseFile, IMediaVideoResource, IBookmarkable
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

    private int? season;
    private int? episode;

    private bool hasInternalSubtitle = false;
    private bool isInternalSubtitleASS = false;

    private bool isSeries = false;

    private static BindingFlags tagLibBindigFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static FieldInfo tagLibField = typeof(TagLib.Matroska.Track).GetField("track_codec_id", tagLibBindigFlags);


    private void FetchTV()
    {
      try
      {

        if (tvshowid == null || tvshowid == -1)
        {
          this.tvshowid = TheTVDB.GetTVShowID(this.Path);
        }
        var steszt = (System.IO.Directory.GetParent(base.Path).Name).TryGetName();
        if (steszt == null || (steszt is Formatting.NiceSeriesName && (steszt as Formatting.NiceSeriesName).Episode == 0))
        {
          steszt = base.Title.TryGetName();
        }
        if (steszt == null)
        {
          steszt = base.Item.Name.TryGetName();
        }
        TVShowInfo tvinfo = null;

        if (tvshowid != null && tvshowid > 0)
        {
          tvinfo = TheTVDB.GetTVShowDetails(this.tvshowid.Value);
          Server.UpdateTVCache(tvinfo);
          this.seriesname = tvinfo.Name;
          isSeries = true;
        }

        if (steszt is Utilities.Formatting.NiceSeriesName)
        {
          isSeries = true;
          var steszt2 = steszt as Utilities.Formatting.NiceSeriesName;
          if (String.IsNullOrEmpty(this.seriesname))
          {
            this.seriesname = steszt2.Name;
          }

          if (/*steszt2.Season > 0 &&*/ steszt2.Episode > 0)
          {
            if (tvinfo != null)
            {
              this.title = tvinfo.Find(steszt2.Season, steszt2.Episode);
            }
            else
            {
              this.title = String.Format("{0}x{1}", steszt2.Season, steszt2.Episode);
            }
            this.season = steszt2.Season;
            this.episode = steszt2.Episode;
          }
          else
          {
            this.title = base.Title;
          }

          if (!String.IsNullOrEmpty(steszt.Releaser))
          {
            this.title = String.Format("{0} ({1},{2})", this.title, steszt.Resolution, steszt.Releaser);
          }

        }
        else if (steszt is Formatting.MovieName)
        {
          var n = steszt as Formatting.MovieName;
          this.seriesname = String.Format("{0} ({1})", n.Name, n.Year);
        }
        else
        {
          this.seriesname = System.IO.Directory.GetParent(this.Path).Name;
        }
      }
      catch (Exception exn)
      {
        if (exn is System.ArgumentNullException)
        {
        }
        else
        {
          this.tvshowid = TheTVDB.GetTVShowID(this.Path);
        }

      }
    }

    public VideoFile(FileServer server, FileInfo aFile, DlnaMime aType, Model.VideoFile v)
   : this(server, aFile,aType)
    {
      actors = v.Actors.Split(',');
      description = v.Description;
      director = v.Director;
      genre = v.Genre;
      lastpos = v.Progress;
      width = v.Width;
      height = v.Height;
      duration = new TimeSpan(v.Duration);
      bookmark = v.Bookmark;
      title = v.Title;
      hasInternalSubtitle = v.HasInternalSubtitle;
      isInternalSubtitleASS = v.IsInternalSubtitleASS;


      if (v.CoverId.HasValue)
      {
        cover = new Cover(v.Cover);
      }

      try
      {
        Subtitle sub = null;
        var ssub = v.Subtitles.FirstOrDefault();
        try
        {
          if (ssub != null)
          {
            sub = new Subtitle(ssub.Data, ssub.Internal, ssub.Path, ssub.Modified);
          }

          if (sub != null && !string.IsNullOrEmpty(sub.subPath))
          {
            var finfo = new System.IO.FileInfo(sub.subPath);

            if (sub.InfoDate >= finfo.LastWriteTimeUtc)
            {
              subTitle = sub;
            }
            else
            {
              subTitle = new Subtitle(new System.IO.FileInfo(this.Path), isInternalSubtitleASS);
            }
          }
          else
          {
            if (sub != null && sub.isInternal)
            {
              subTitle = sub;
            }
            else
            {
              subTitle = new Subtitle(new System.IO.FileInfo(this.Path), isInternalSubtitleASS);
            }
          }
        }
        catch (Exception e)
        {
          subTitle = new Subtitle(new System.IO.FileInfo(this.Path), isInternalSubtitleASS);
        }
        //subTitle = new Subtitle(new System.IO.FileInfo(this.Path), sub);
      }
      catch (Exception)
      {
        subTitle = null;
      }
      try
      {
        tvshowid = v.TVDBId;
        if (tvshowid.HasValue && tvshowid.Value > 0)
        {
          FetchTV();
        }
      }
      catch (Exception) { }

      //Server.UpdateFileCache(this);
      initialized = true;

    }
  
    internal VideoFile(FileServer server, FileInfo aFile, DlnaMime aType)
      : base(server, aFile, aType, DlnaMediaTypes.Video)
    {
      FetchTV();
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

    public int? TVDBId
    {
      get
      {
        return tvshowid;
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
    public long Progress
    {
      get
      {
        if (this.InfoSize.HasValue)
        {
          return this.lastpos * 100L / this.InfoSize.Value;
        }
        return 0;
      }
    }
    public string MovieTitle
    {
      get
      {
        MaybeInit();
        if (this.seriesname != null)
        {
          return this.seriesname;
        }
        else { return base.Title; }

      }
    }
    public bool IsSeries
    {
      get
      {
        MaybeInit();
        return this.isSeries;
      }
    }
    public int? Season
    {
      get
      {
        MaybeInit();
        return this.season;
      }
    }
    public int? Episode
    {
      get
      {
        MaybeInit();
        return this.episode;
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
        if (description != null)
        {
          rv.Add("Description", description);
        }
        if (actors != null && actors.Length != 0)
        {
          rv.Add("Actors", string.Join(", ", actors));
        }
        if (director != null)
        {
          rv.Add("Director", director);
        }
        if (duration != null)
        {
          rv.Add("Duration", duration.Value.ToString("g"));
        }
        if (genre != null)
        {
          rv.Add("Genre", genre);
        }
        if (width != null && height != null)
        {
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
        try
        {
          if (subTitle == null)
          {
            subTitle = new Subtitle(Item, isInternalSubtitleASS);
            Server.UpdateFileCache(this);
          }
        }
        catch (Exception ex)
        {
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
        MaybeInit();
        var t = base.Title;
        if (!string.IsNullOrWhiteSpace(this.title))
        {
          //return string.Format("{0} — {1}", base.Title, title);
          t = this.title;
        }
        t = (this.Subtitle.HasSubtitle ? "*" : "") + (t);
        return t;
      }
    }

    private void MaybeInit()
    {
      if (initialized)
      {
        return;
      }

      FetchTV();
      try
      {
        using (var tl = TagLib.File.Create(new TagLibFileAbstraction(Item)))
        {
          try
          {
            duration = tl.Properties.Duration;
            if (duration.HasValue && duration.Value.TotalSeconds < 0.1)
            {
              duration = null;
            }
            width = tl.Properties.VideoWidth;
            height = tl.Properties.VideoHeight;
            var subs = tl.Properties.Codecs.Where(c => c is TagLib.Matroska.SubtitleTrack).Select(c => c as TagLib.Matroska.Track).ToArray();
            hasInternalSubtitle = subs.Length > 0;

            if (hasInternalSubtitle)
            {
              isInternalSubtitleASS = subs.Any(s => (string)tagLibField.GetValue(s) == "S_TEXT/ASS");

            }
            isInternalSubtitleASS = false; //FIXME
          }
          catch (Exception ex)
          {
            Debug("Failed to transpose Properties props", ex);
          }

          try
          {
            var t = tl.Tag;
            genre = t.FirstGenre;
            //title = t.Title;
            description = t.Comment;
            director = t.FirstComposerSort;
            if (string.IsNullOrWhiteSpace(director))
            {
              director = t.FirstComposer;
            }
            actors = t.PerformersSort;
            if (actors == null || actors.Length == 0)
            {
              actors = t.PerformersSort;
              if (actors == null || actors.Length == 0)
              {
                actors = t.Performers;
                if (actors == null || actors.Length == 0)
                {
                  actors = t.AlbumArtists;
                }
              }
            }
          }
          catch (Exception ex)
          {
            Debug("Failed to transpose Tag props", ex);
          }
        }

        initialized = true;

        Server.UpdateFileCache(this);
      }
      catch (TagLib.CorruptFileException ex)
      {
        Debug(
          "Failed to read meta data via taglib for file " + Item.FullName, ex);
        initialized = true;
      }
      catch (TagLib.UnsupportedFormatException ex)
      {
        Debug(
          "Failed to read meta data via taglib for file " + Item.FullName, ex);
        initialized = true;
      }
      catch (Exception ex)
      {
        Warn(
          "Unhandled exception reading meta data for file " + Item.FullName,
          ex);
      }
    }

    public Model.VideoFile GetData(Model.Store store, Model.VideoFile v)
    {
      MaybeInit();
      base.GetData(store, v);
      //v.Path = this.Path;
      //v.MediaType = this.MediaType;
      //v.Type = this.Type;
      //v.SizeRaw = this.InfoSize;
      //v.Size = this.InfoSize.HasValue ? InfoSize.Value.FormatFileSize() : null;
      //v.Date = this.InfoDate;
      //v.DateO = this.InfoDate.ToString("o");
      v.Actors = string.Join(",", actors);
      v.Description = description;
      v.Director = director;
      v.Genre = genre;
      v.Title = string.IsNullOrEmpty(title) ? base.Title : title;
      v.Width = width;
      v.Height = height;
      v.Bookmark = bookmark;
      v.Duration = duration.GetValueOrDefault(EmptyDuration).Ticks;
      v.TVDBId = (tvshowid.HasValue && tvshowid.Value > 0 ? tvshowid : null);
      v.Progress = lastpos;
      v.HasInternalSubtitle = hasInternalSubtitle;
      v.IsInternalSubtitleASS = isInternalSubtitleASS;


      if (cover != null)
      {
        if (!v.CoverId.HasValue)
        {
          v.Cover = store.Covers.Add(new Model.Cover());
        }
        v.Cover = cover.GetData(store, v.Cover);
      }
      if (subTitle != null && subTitle.HasSubtitle)
      {
        Model.Subtitle sssss = v.Subtitles.FirstOrDefault();
        if (sssss == null)
        {
          sssss = new Model.Subtitle();
          //sssss.VideoFile = v;
          v.Subtitles = new List<Model.Subtitle>() { sssss };
        } 
        sssss.Internal = subTitle.isInternal;
        sssss.Modified = subTitle.InfoDate;
        sssss.Path = subTitle.subPath;
        sssss.Data = subTitle.Text;
        
      }

      return v;
    }
  }
}
