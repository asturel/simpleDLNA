using System;

namespace NMaier.SimpleDlna.Server
{
    public class ChangeEvent : EventArgs
    {
      public string[] ObjectIDs { get; }
      public ChangeEvent(string[] objectIds)
      {
        ObjectIDs = objectIds;
      }
      public ChangeEvent()
      {
        ObjectIDs = new string[] { };
      }
    }
  public interface IVolatileMediaServer
  {
    bool Rescanning { get; set; }

    void Rescan();

    event EventHandler<ChangeEvent> Changed;
  }
}
