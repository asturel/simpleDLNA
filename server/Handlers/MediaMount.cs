using NMaier.SimpleDlna.Server.Metadata;
using NMaier.SimpleDlna.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Xml;
using System.Linq;

namespace NMaier.SimpleDlna.Server
{
  internal sealed partial class MediaMount
    : Logging, IMediaServer, IPrefixHandler
  {
    private readonly Dictionary<IPAddress, Guid> guidsForAddresses =
      new Dictionary<IPAddress, Guid>();

    
    private readonly Dictionary<string, Tuple<string, DateTime>> subscribers =
      new Dictionary<string, Tuple<string, DateTime>>(StringComparer.InvariantCultureIgnoreCase);
      
    private int subseq = -1;

    private static uint mount = 0;

    private readonly string prefix;

    private readonly IMediaServer server;

    private uint systemID = 1;

    public MediaMount(IMediaServer aServer)
    {
      server = aServer;
      prefix = String.Format("/mm-{0}/", ++mount);
      var vms = server as IVolatileMediaServer;
      if (vms != null) {
        vms.Changed += ChangedServer;
      }
    }

    public IHttpAuthorizationMethod Authorizer
    {
      get
      {
        return server.Authorizer;
      }
    }

    public string DescriptorURI
    {
      get
      {
        return String.Format("{0}description.xml", prefix);
      }
    }

    public string FriendlyName
    {
      get
      {
        return server.FriendlyName;
      }
    }

    public string Prefix
    {
      get
      {
        return prefix;
      }
    }

    public Guid Uuid
    {
      get
      {
        return server.Uuid;
      }
    }

    private void SendNotify(string sid, string url, string ids)
    {
      try {
        HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
        webRequest.Method = "NOTIFY";
        webRequest.ContentType = "text/xml; charset=\"utf-8\"";
        webRequest.Headers.Add("NT", "upnp:event");
        webRequest.Headers.Add("NTS", "upnp:propchange");
        webRequest.Headers.Add("SID", "uuid:" + sid);
        webRequest.Headers.Add("SEQ", String.Format("{0}", ++subseq));

        var doc = new XmlDocument();
        doc.LoadXml(Properties.Resources.notify);
        System.Xml.XmlNamespaceManager nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("e", "urn:schemas-upnp-org:event-1-0");
        var xProperty = doc.SelectSingleNode("//e:property", nsmgr);

        
        var xElement = doc.CreateElement("SystemUpdateID");
        xElement.InnerText = systemID.ToString();

        /*
        var xElement = doc.CreateElement("ContainerUpdateIDs");
        //var upString = ids.Select(cid => String.Format("{0},{1}",cid,systemID)).ToArray();
        xElement.InnerText = String.Format("{0},{1}", ids, systemID);
        */
        xProperty.AppendChild(xElement);

        byte[] requestBytes = new System.Text.UTF8Encoding().GetBytes(doc.OuterXml);

        using (var reqstream = webRequest.GetRequestStream())
        {
          reqstream.Write(requestBytes, 0, requestBytes.Length);
        }

        using (var res = webRequest.GetResponse())
        {

        }
      } catch (System.Net.WebException e)
      {
        //subscribers.Remove(sid);
        Error(string.Format("SendNotify failed {0} {1}", sid, url), e);
      } catch (Exception exn)
      {
        Error("SendNotify failed" + exn.Message, exn);
      }

    }
    private void SendNotifyForAll(string[] ids)
    {
      var now = DateTime.Now;
      var list = new List<KeyValuePair<string, Tuple<string,DateTime>>>(subscribers);
      foreach (var notify in list)
      {
        if (notify.Value.Item2 > now)
        {
          if (notify.Value.Item1.Contains("ContentDirectory"))
          {
            SendNotify(notify.Key, notify.Value.Item1, "");
            /*
            Debug("SENDING NOTIFY TO: " + notify.Value);
            foreach (var id0 in ids)
            {
              SendNotify(notify.Key, notify.Value.Item1, id0);
            }
            */
          }
        } else {
          Debug(String.Format("Notify {0} expired, removing", notify.Key));
          subscribers.Remove(notify.Key);
        }
      }
    }

    private void ChangedServer(object sender, ChangeEvent e)
    {
      soapCache.Clear();
      InfoFormat("Rescanned mount {0}", Uuid);
      systemID++;
      SendNotifyForAll(e.ObjectIDs);

    }
 
    private string GenerateDescriptor(IPAddress source)
    {
      var doc = new XmlDocument();
      doc.LoadXml(Properties.Resources.description);
      var guid = Uuid;
      guidsForAddresses.TryGetValue(source, out guid);
      doc.SelectSingleNode("//*[local-name() = 'UDN']").InnerText =
        String.Format("uuid:{0}", guid);
      doc.SelectSingleNode("//*[local-name() = 'modelNumber']").InnerText =
        Assembly.GetExecutingAssembly().GetName().Version.ToString();
      doc.SelectSingleNode("//*[local-name() = 'friendlyName']").InnerText =
        FriendlyName + " — sdlna";

      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:ContentDirectory:1']/../*[local-name() = 'SCPDURL']").InnerText =
        String.Format("{0}contentDirectory.xml", prefix);
      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:ContentDirectory:1']/../*[local-name() = 'controlURL']").InnerText =
        String.Format("{0}control", prefix);
      doc.SelectSingleNode("//*[local-name() = 'eventSubURL']").InnerText =
        String.Format("{0}events", prefix);

      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:ConnectionManager:1']/../*[local-name() = 'SCPDURL']").InnerText =
        String.Format("{0}connectionManager.xml", prefix);
      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:ConnectionManager:1']/../*[local-name() = 'controlURL']").InnerText =
        String.Format("{0}control", prefix);
      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:ConnectionManager:1']/../*[local-name() = 'eventSubURL']").InnerText =
        String.Format("{0}events", prefix);

      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1']/../*[local-name() = 'SCPDURL']").InnerText =
        String.Format("{0}MSMediaReceiverRegistrar.xml", prefix);
      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1']/../*[local-name() = 'controlURL']").InnerText =
        String.Format("{0}control", prefix);
      doc.SelectSingleNode(
        "//*[text() = 'urn:schemas-upnp-org:service:X_MS_MediaReceiverRegistrar:1']/../*[local-name() = 'eventSubURL']").InnerText =
        String.Format("{0}events", prefix);

      return doc.OuterXml;
    }

    public void AddDeviceGuid(Guid guid, IPAddress address)
    {
      guidsForAddresses.Add(address, guid);
    }

    public IMediaItem GetItem(string id)
    {
      return server.GetItem(id);
    }
    public IResponse HandleRequest(IRequest request)
    {
      if (Authorizer != null &&
        !IPAddress.IsLoopback(request.RemoteEndpoint.Address) &&
        !Authorizer.Authorize(
          request.Headers,
          request.RemoteEndpoint,
          IP.GetMAC(request.RemoteEndpoint.Address)
         )) {
        throw new HttpStatusException(HttpCode.Denied);
      }

      var path = request.Path.Substring(prefix.Length);
      Debug(path);
      if (path == "description.xml") {
        return new StringResponse(
          HttpCode.Ok,
          "text/xml",
          GenerateDescriptor(request.LocalEndPoint.Address)
          );
      }
      if (path == "contentDirectory.xml") {
        return new ResourceResponse(
          HttpCode.Ok,
          "text/xml",
          "contentdirectory"
          );
      }
      if (path == "connectionManager.xml") {
        return new ResourceResponse(
          HttpCode.Ok,
          "text/xml",
          "connectionmanager"
          );
      }
      if (path == "MSMediaReceiverRegistrar.xml") {
        return new ResourceResponse(
          HttpCode.Ok,
          "text/xml",
          "MSMediaReceiverRegistrar"
          );
      }
      if (path == "control") {
        return ProcessSoapRequest(request);
      }
      if (path.StartsWith("file/", StringComparison.Ordinal)) {
        var id = path.Split('/')[1];
        InfoFormat("Serving file {0}", id);
        var item = GetItem(id) as IMediaResource;
        return new ItemResponse(prefix, request, item);
      }
      if (path.StartsWith("cover/", StringComparison.Ordinal)) {
        var id = path.Split('/')[1];
        InfoFormat("Serving cover {0}", id);
        var item = GetItem(id) as IMediaCover;
        return new ItemResponse(prefix, request, item.Cover, "Interactive");
      }
      if (path.StartsWith("subtitle/", StringComparison.Ordinal)) {
        var id = path.Split('/')[1];
        InfoFormat("Serving subtitle {0}", id);
        var item = GetItem(id) as IMetaVideoItem;
        return new ItemResponse(prefix, request, item.Subtitle, "Background");
      }

      if (string.IsNullOrEmpty(path) || path == "index.html") {
        return new Redirect(request, prefix + "index/0");
      }
      if (path.StartsWith("index/", StringComparison.Ordinal)) {
        var id = path.Substring("index/".Length);
        var item = GetItem(id);
        return ProcessHtmlRequest(item);
      }
      if (path.StartsWith("update/", StringComparison.Ordinal))
      {
        var id = path.Substring("update/".Length);
        var item = GetItem(id);
        
        SendNotifyForAll(new string[]{ id });
        return ProcessHtmlRequest(item);
      }

      if (request.Method == "SUBSCRIBE") {
        var res = new StringResponse(HttpCode.Ok, string.Empty);
        string notifySid;
        if (!request.Headers.TryGetValue("SID",out notifySid))
        {
          notifySid = Guid.NewGuid().ToString();
        } else
        {
          notifySid = notifySid.Remove(0, 5);
        }
        //string callback;
        Tuple<string, DateTime> subres;
        int timeout = System.Int32.Parse(request.Headers["timeout"].Remove(0, 7));
        DateTime dtimeout = System.DateTime.Now.AddSeconds(timeout);
        if (!subscribers.TryGetValue(notifySid, out subres)) {
          if (request.Headers.ContainsKey("CALLBACK"))
          {
            string callback = request.Headers["CALLBACK"].Replace("<", "").Replace(">", "");
            subscribers.Add(notifySid, new Tuple<string, DateTime>(callback, dtimeout));
            Debug("Subscribe: " + notifySid + ": " + callback);
          } else
          {
            Error("SUBSCRIBE WTF: " + request.Headers);
          }
        }
        else
        {
          //RENEW
          subscribers[notifySid] = new Tuple<string, DateTime>(subres.Item1, dtimeout);
        }
        res.Headers.Add("SID", string.Format("uuid:{0}", notifySid));
        res.Headers.Add("TIMEOUT", request.Headers["timeout"]);
        return res;
      }
      if (request.Method == "UNSUBSCRIBE") {
        //TODO: remove from subscribers
        return new StringResponse(HttpCode.Ok, string.Empty);
      }
      WarnFormat("Did not understand {0} {1}", request.Method, path);
      throw new HttpStatusException(HttpCode.NotFound);
    }
  }
}
