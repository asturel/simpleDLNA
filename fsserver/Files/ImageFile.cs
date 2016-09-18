using NMaier.SimpleDlna.Server;
using System;
using System.IO;
using System.Runtime.Serialization;

namespace NMaier.SimpleDlna.FileMediaServer
{
  [Serializable]
  internal sealed class ImageFile :
    BaseFile, IMediaImageResource
  {
    private string creator;

    private string description;

    private bool initialized = false;

    private string title;

    private int? width,

    height;

    internal ImageFile(FileServer server, FileInfo aFile, DlnaMime aType)
      : base(server, aFile, aType, DlnaMediaTypes.Image)
    {
    }

    public ImageFile(FileServer server, FileInfo aFile, DlnaMime aType, Model.ImageFile a) : this(server, aFile, aType)
    {
      creator = a.Creator;
      description = a.Description;
      title = a.Title;
      width = a.Width;
      height = a.Height;
      initialized = true;
    }


    public string MetaCreator
    {
      get
      {
        MaybeInit();
        return creator;
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
        if (creator != null) {
          rv.Add("Creator", creator);
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

    public override string Title
    {
      get
      {
        if (!string.IsNullOrWhiteSpace(title)) {
          return string.Format("{0} — {1}", base.Title, title);
        }
        return base.Title;
      }
    }

    private void MaybeInit()
    {
      if (initialized) {
        return;
      }

      try {
        using (var tl = TagLib.File.Create(new TagLibFileAbstraction(Item))) {
          try {
            width = tl.Properties.PhotoWidth;
            height = tl.Properties.PhotoHeight;
          }
          catch (Exception ex) {
            Debug("Failed to transpose Properties props", ex);
          }

          try {
            var t = (tl as TagLib.Image.File).ImageTag;
            title = t.Title;
            if (string.IsNullOrWhiteSpace(title)) {
              title = null;
            }
            description = t.Comment;
            if (string.IsNullOrWhiteSpace(description)) {
              description = null;
            }
            creator = t.Creator;
            if (string.IsNullOrWhiteSpace(creator)) {
              creator = null;
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

    public Model.ImageFile GetData(Model.Store s, Model.ImageFile f)
    {
      MaybeInit();
      base.GetData(s, f);
      f.Creator = creator;
      f.Description = description;
      f.Title = title;
      f.Width = width;
      f.Height = height;
      return f;
    }
  }
}
