#r "Microsoft.WindowsAzure.Storage"

using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Azure; 
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;

private static IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName)
{
    var processor = _context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
    ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

    if (processor == null)
        throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

    return processor;
}

public static Uri GetValidOnDemandURI(IAsset asset)
{
    var aivalidurls = GetValidURIs(asset);
    if (aivalidurls != null)
    {
        return aivalidurls.FirstOrDefault();
    }
    else
    {
        return null;
    }
}

public static IEnumerable<Uri> GetValidURIs(IAsset asset)
{
    IEnumerable<Uri> ValidURIs;
    var ismFile = asset.AssetFiles.AsEnumerable().Where(f => f.Name.EndsWith(".ism")).OrderByDescending(f => f.IsPrimary).FirstOrDefault();
    if (ismFile != null)
    {
        var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

        var template = new UriTemplate("{contentAccessComponent}/{ismFileName}/manifest");
        ValidURIs = locators.SelectMany(l =>
            _context
                .StreamingEndpoints
                .AsEnumerable()
                  .Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o)))
                  .OrderByDescending(o => o.CdnEnabled)
                .Select(
                    o =>
                        template.BindByPosition(new Uri("http://" + o.HostName), l.ContentAccessComponent,
                            ismFile.Name)))
            .ToArray();

        return ValidURIs;
    }
    else
    {
        return null;
    }
}

static public bool CanDoDynPackaging(IStreamingEndpoint mySE)
{
    return ReturnTypeSE(mySE) != StreamEndpointType.Classic;
}

static public StreamEndpointType ReturnTypeSE(IStreamingEndpoint mySE)
{
    if (mySE.ScaleUnits != null && mySE.ScaleUnits > 0)
    {
        return StreamEndpointType.Premium;
    }
    else
    {
        if (new Version(mySE.StreamingEndpointVersion) == new Version("1.0"))
        {
            return StreamEndpointType.Classic;
        }
        else
        {
            return StreamEndpointType.Standard;
        }
    }
}

public enum StreamEndpointType
{
    Classic = 0,
    Standard,
    Premium
}