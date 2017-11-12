using System;
using System.IO;
using System.Linq;
using System.Timers;
using log4net;
using NMaier.SimpleDlna.Utilities;

namespace NMaier.SimpleDlna.FileMediaServer
{
  internal class FileStreamCache
  {
    // ReSharper disable once UnusedMember.Local
    private static readonly Ticker ticker = new Ticker();

    private static readonly ILog logger =
      LogManager.GetLogger(typeof (FileReadStream));

    private static readonly LeastRecentlyUsedDictionary<string, CacheItem> streams =
      new LeastRecentlyUsedDictionary<string, CacheItem>(15);

    private static void Expire()
    {
      lock (streams) {
        var x = streams.ToArray();
        foreach (var item in x) {
          var diff = DateTime.UtcNow - item.Value.InsertionPoint;
          if (diff.TotalSeconds > 5) {
            item.Value.Stream?.Kill();
            streams.Remove(item.Key);
          }
        }
      }
    }

    internal static void Clear()
    {
      lock (streams) {
        foreach (var item in streams) {
          item.Value.Stream.Kill();
        }
        streams.Clear();
      }
    }

    internal static FileReadStream Get(FileInfo info, BaseFile baseFile)
    {
      var key = info.FullName;
      lock (streams) {
        CacheItem rv;
        if (streams.TryGetValue(key, out rv)) {
          streams.Remove(key);
          logger.DebugFormat("Retrieved file stream {0} from cache", key);
          return rv.Stream;
        }
      }
      logger.DebugFormat("Constructing file stream {0}", key);
      return new FileReadStream(info, baseFile);
    }

    internal static void Recycle(FileReadStream stream)
    {
      try {
        var key = stream.Name;
        lock (streams) {
          CacheItem ignore;
          if (!streams.TryGetValue(key, out ignore) ||
              Equals(ignore.Stream, stream)) {
            logger.DebugFormat("Recycling {0}", key);
            stream.Seek(0, SeekOrigin.Begin);
            var removed = streams.AddAndPop(key, new CacheItem(stream));
            removed?.Stream.Kill();
            return;
          }
        }
      }
      catch (Exception) {
        // no op
      }
      stream.Kill();
    }

    private class Ticker : Timer
    {
      public Ticker() : base(10000)
      {
        Enabled = true;
        Elapsed += (sender, args) => { Expire(); };
      }
    }

    private class CacheItem
    {
      public readonly DateTime InsertionPoint = DateTime.UtcNow;
      public readonly FileReadStream Stream;

      public CacheItem(FileReadStream stream)
      {
        Stream = stream;
      }
    }
  }
}
