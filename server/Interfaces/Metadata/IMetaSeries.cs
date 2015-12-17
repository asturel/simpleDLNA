namespace NMaier.SimpleDlna.Server.Metadata
{
  public interface IMetaSeries
  {
    bool IsSeries { get; }
    int? Season { get; }
    int? Episode { get; }
  }
}
