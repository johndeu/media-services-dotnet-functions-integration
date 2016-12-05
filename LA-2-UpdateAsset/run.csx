#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#load "../Shared/mediaServicesHelpers.csx"
#load "../Shared/copyBlobHelpers.csx"

using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;


// Read values from the App.config file.
static string _sourceStorageAccountName = Environment.GetEnvironmentVariable("SourceStorageAccountName");
static string _sourceStorageAccountKey = Environment.GetEnvironmentVariable("SourceStorageAccountKey");

private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;
private static CloudStorageAccount _destinationStorageAccount = null;

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.Path == null)
    {
        // for test
        // data.Path = "/input/WP_20121015_081924Z.mp4";

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass Path in the input object"
        });
    }

    string FileName = Path.GetFileName((string)data.Path);

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    IAsset newAsset = null;

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        // Step 1:  Copy the Blob into a new Input Asset for the Job
        // ***NOTE: Ideally we would have a method to ingest a Blob directly here somehow. 
        // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

        log.Info($"Create storage credentials : {_sourceStorageAccountName}");
        StorageCredentials SourceStorageCredentials = new StorageCredentials(_sourceStorageAccountName, _sourceStorageAccountKey);

        log.Info("Create storage account");
        var sourceStorageAccount = new CloudStorageAccount(SourceStorageCredentials, false);

        log.Info("Create storage blob client");
        var sourceCloudBlobClient = sourceStorageAccount.CreateCloudBlobClient();

        var sourceUri = new Uri(sourceStorageAccount.BlobStorageUri.PrimaryUri, (string)data.Path);
        log.Info($"sourceuri {sourceUri}");

        var sourceCloudBlob = (CloudBlockBlob)sourceCloudBlobClient.GetBlobReferenceFromServer(sourceUri);
        log.Info($"sourceCloudBlob name {sourceCloudBlob.Name}");

        newAsset = CreateAssetFromBlob(sourceCloudBlob, FileName, log).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info("Asset ID: " + newAsset.Id);

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        AssetId = newAsset.Id
    });
}




