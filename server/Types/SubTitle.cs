using log4net;
using NMaier.SimpleDlna.Utilities;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace NMaier.SimpleDlna.Server
{
  [Serializable]
  public sealed class Subtitle : IMediaResource
  {
    [NonSerialized]
    private byte[] encodedText = null;

    [NonSerialized]
    private static readonly ILog logger =
      LogManager.GetLogger(typeof(Subtitle));

    [NonSerialized]
    private static readonly string[] exts = new string[] {
      ".srt", ".SRT",
      ".ass", ".ASS",
      ".ssa", ".SSA",
      ".sub", ".SUB",
      ".vtt", ".VTT"
      };

    [NonSerialized]
    private static Regex stylingRegex = new Regex(@"^<font.*?>(.*?)</font>$", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

    private string text = null;

    public bool isInternal = false;

    public string subPath = null;

    private DateTime lastmodified = DateTime.UtcNow;

    public Subtitle()
    {
    }

    public Subtitle(FileInfo file, bool hasASSSub)
    {
      Load(file, hasASSSub);
    }
    public Subtitle(string text)
    {
      this.text = text;
    }
    public Subtitle(string text, bool isinternal, string path, DateTime modified)
    {
      this.text = text;
      this.isInternal = isinternal;
      this.subPath = path;
      this.lastmodified = modified;
    }
    public string Text
    {
      get
      {
        return this.text;
      }
    }

    public IMediaCoverResource Cover
    {
      get
      {
        throw new NotImplementedException();
      }
    }

    public bool HasSubtitle
    {
      get
      {
        return !string.IsNullOrWhiteSpace(text);
      }
    }

    public string Id
    {
      get
      {
        return Path;
      }
      set
      {
        throw new NotImplementedException();
      }
    }

    public DateTime InfoDate
    {
      get
      {
        //return DateTime.UtcNow;
        return lastmodified;
      }
    }

    public long? InfoSize
    {
      get
      {
        try {
          using (var s = CreateContentStream()) {
            return s.Length;
          }
        }
        catch (Exception) {
          return null;
        }
      }
    }

    public DlnaMediaTypes MediaType
    {
      get
      {
        throw new NotImplementedException();
      }
    }

    public string Path
    {
      get
      {
        return "ad-hoc-subtitle:";
      }
    }

    public string PN
    {
      get
      {
        return DlnaMaps.MainPN[Type];
      }
    }

    public IHeaders Properties
    {
      get
      {
        var rv = new RawHeaders();
        rv.Add("Type", Type.ToString());
        if (InfoSize.HasValue) {
          rv.Add("SizeRaw", InfoSize.ToString());
          rv.Add("Size", InfoSize.Value.FormatFileSize());
        }
        rv.Add("Date", InfoDate.ToString());
        rv.Add("DateO", InfoDate.ToString("o"));
        rv.Add("Content", text);
        rv.Add("Internal", isInternal.ToString());
        return rv;
      }
    }

    public string Title
    {
      get
      {
        throw new NotImplementedException();
      }
    }

    public DlnaMime Type
    {
      get
      {
        try
        {
          if (!string.IsNullOrEmpty(subPath))
          {
            var ext = System.IO.Path.GetExtension(subPath.TrimStart('.'));
            return DlnaMaps.Ext2Dlna[ext];
          }
        } catch (Exception e)
        {
          logger.Error("Subtitle Type error", e);
        }
        return DlnaMime.SubtitleSRT;
      }
    }

    private System.Text.Encoding GetEncoding(string filename)
    {
      using (FileStream fs = File.OpenRead(filename))
      {
        Ude.CharsetDetector cdet = new Ude.CharsetDetector();
        cdet.Feed(fs);
        cdet.DataEnd();
        if (cdet.Charset != null)
        {
          //Console.WriteLine("Charset: {0}, confidence: {1}", cdet.Charset, cdet.Confidence);
          return System.Text.Encoding.GetEncoding(cdet.Charset);

        }
        else
        {
          //Console.WriteLine("Detection failed.");
          return null;
        }
      }
    }

    private void Load(FileInfo file, bool hasASSSub)
    {
      try {
        // Try external
        foreach (var i in exts) {
          var sti = new FileInfo(
            System.IO.Path.ChangeExtension(file.FullName, i));
          try {
            if (!sti.Exists) {
              sti = new FileInfo(file.FullName + i);
            }
            if (!sti.Exists) {
              continue;
            }
            //text = System.IO.File.ReadAllText(sti.FullName,System.Text.Encoding.GetEncoding("iso-8859-2")); //FFmpeg.GetSubtitleSubrip(sti);
            Encoding encoding = null;
            try {
              encoding = GetEncoding(sti.FullName);
            } catch (Exception e)
            {
              logger.Error("Encoding", e);
              encoding = System.Text.Encoding.GetEncoding("iso-8859-2");
            }
            // fallback
            if (encoding == null)
            {
              encoding = System.Text.Encoding.GetEncoding("iso-8859-2");
            }
            text = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.Convert(encoding, System.Text.Encoding.UTF8, (System.IO.File.ReadAllBytes(sti.FullName))));
            subPath = sti.FullName;
            lastmodified = sti.LastWriteTimeUtc;
          }
          catch (NotSupportedException) {
          }
          catch (Exception ex) {
            logger.Warn(string.Format(
              "Failed to get subtitle from {0}", sti.FullName), ex);
          }
        }
        try {
          if (string.IsNullOrEmpty(text) && hasASSSub)
          {
            logger.Info(string.Format("Extracting subtitle from {0}", file.FullName));
            text = stylingRegex.Replace(FFmpeg.GetSubtitleSubrip(file),"$1");
            isInternal = true;
          }

        }
        catch (NotSupportedException) {
        }
        catch (Exception ex) {
          logger.Warn(string.Format(
            "Failed to get subtitle from {0}", file.FullName), ex);
        }
      }
      catch (Exception ex) {
        logger.Error(string.Format(
          "Failed to load subtitle for {0}", file.FullName), ex);
      }
    }

    public int CompareTo(IMediaItem other)
    {
      throw new NotImplementedException();
    }

    public Stream CreateContentStream()
    {
      if (!HasSubtitle) {
        throw new NotSupportedException();
      }
      if (encodedText == null) {
        encodedText = Encoding.UTF8.GetBytes(text);
      }
      return new MemoryStream(encodedText, false);
    }

    public bool Equals(IMediaItem other)
    {
      throw new NotImplementedException();
    }

    public string ToComparableTitle()
    {
      throw new NotImplementedException();
    }
  }
}
