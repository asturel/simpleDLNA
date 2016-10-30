using NMaier.SimpleDlna.Server;
using NMaier.SimpleDlna.Utilities;
using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Linq;
using System.Data.Entity;

namespace NMaier.SimpleDlna.FileMediaServer
{
  internal sealed class FileStore : Logging
  {
    private static readonly object clock = new object();
    public readonly FileInfo StoreFile;

    internal FileStore(FileInfo storeFile)
    {
      StoreFile = storeFile;    

      /*
      try
      {
        //OpenConnection(storeFile, out connection);
        Assembly monoSqlite;
        try
        {
          monoSqlite = Assembly.Load(
            "Mono.Data.Sqlite, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
        }
        catch (Exception)
        {
          monoSqlite = Assembly.Load(
            "Mono.Data.Sqlite, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
        }
      }
      catch (Exception e) { };
      */
    }

    internal BaseFile MaybeGetFile(FileServer server, FileInfo info, DlnaMime type)
    {
      lock (clock)
      {
        using (var db = new Model.Store(StoreFile))
        {
          var basefile = db.Files.Where(f => f.Path == info.FullName && f.SizeRaw == info.Length).Include(f => f.Cover).FirstOrDefault();
          if (basefile != null)
          {
            var r = new BaseFile(server, info, type, basefile.MediaType);
            if (basefile is Model.VideoFile)
            {
              var v = basefile as Model.VideoFile;
              return new VideoFile(server, info, type, v);
            }
            else if (basefile is Model.AudioFile)
            {
              var v = basefile as Model.AudioFile;
              return new AudioFile(server, info, type, v);
            }
            else if (basefile is Model.ImageFile)
            {
              var v = basefile as Model.ImageFile;
              return new ImageFile(server, info, type, v);
            }
            return r;
          }
        }
      }
      return null;
    }

    internal void MaybeStoreFile(BaseFile file)
    {
      lock (clock)
      {
        using (var db = new Model.Store(StoreFile))
        {
          if (file is VideoFile)
          {
            var v = file as VideoFile;
            var i = db.Videos.Where(f => f.Path == v.Path).FirstOrDefault();
            if (i == null)
            {
              i = db.Videos.Add(new Model.VideoFile()
              {
                Subtitles = new System.Collections.Generic.List<Model.Subtitle>()
              });
            }
            v.GetData(db, i);

          }
          else if (file is AudioFile)
          {
            var a = file as AudioFile;
            var i = db.Audios.Where(f => f.Path == a.Path).FirstOrDefault();
            if (i == null)
            {
              i = db.Audios.Add(new Model.AudioFile());
            }
            a.GetData(db, i);
          }
          else if (file is ImageFile)
          {
            var i = file as ImageFile;
            var entity = db.Images.Where(f => f.Path == i.Path).FirstOrDefault();
            if (entity == null)
            {
              entity = db.Images.Add(new Model.ImageFile());
            }
            i.GetData(db, entity);

          }
          db.SaveChanges();
        }
      }
    }

    internal Cover MaybeGetCover(BaseFile file)
    {
      lock (clock)
      {
        using (var db = new Model.Store(StoreFile))
        {
          var r = db.Files.Where(f => f.Path == file.Path).Select(f => f.Cover).FirstOrDefault();
          if (r == null)
          {
            return null;
          }
          return new Cover(r, file.Item);

        }
      }
    }
  }
}
