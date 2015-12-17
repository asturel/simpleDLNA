using System.Collections.Generic;

namespace NMaier.SimpleDlna.Server.Metadata
{
  public interface IMetaVideoItem
    : IMetaInfo, IMetaDescription, IMetaGenre, IMetaDuration, IMetaResolution, IMetaSeries
  {
    IEnumerable<string> MetaActors { get; }

    string MetaDirector { get; }

    Subtitle Subtitle { get; }

    string MovieTitle { get;  }

  }
}
