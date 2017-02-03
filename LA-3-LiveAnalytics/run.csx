#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
#r "System.Web"
#r "System.XML"
#r "System.XML.Linq"
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
using System.Xml;
using System.Xml.Linq;

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

private static int _taskindex = 0;


// Submit an encoding job
// Required : data.AssetId (Example : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc")
// with MES (default)
// with Premium Encoder if WorkflowAssetId is specified (Example : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc")
// with Indexer v1 if IndexV1Language is specified (Example : "English")
// with Indexer v2 if IndexV2Language is specified (Example : "EnUs")
// with Video OCR if OCRLanguage is specified (Example: "AutoDetect" or "English")
// with Face Detection if FaceDetectionMode is specified (Example : "PerFaceEmotion")
// with Motion Detection if MotionDetectionLevel is specified (Example : "medium")
// with Video Summarization if SummarizationDuration is specified (Example : "0.0" for automatic)
// with Hyperlapse if HyperlapseSpeed is specified (Example : "8" for speed x8)
//
// Option: data.IntervalSec (Example: "30")

public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"Webhook was triggered!");

    string jsonContent = await req.Content.ReadAsStringAsync();
    dynamic data = JsonConvert.DeserializeObject(jsonContent);

    log.Info(jsonContent);

    if (data.ProgramId == null)
    {
        // for test
        /*
        data.ProgramId = "nb:pgid:UUID:e1a61286-2467-4be3-84b6-5a4e8006d43d";
        data.IndexV2Language = "EnUs";
        data.OCRLanguage = "AutoDetect";
        data.FaceDetectionMode = "PerFaceEmotion";
        data.MotionDetectionLevel = "medium";
        */

        return req.CreateResponse(HttpStatusCode.BadRequest, new
        {
            error = "Please pass program ID in the input object (ProgramId)"
        });
    }

    int intervalsec = 30; // Interval for each subclip job (sec)
    if (data.IntervalSec != null)
    {
        intervalsec = (int)data.IntervalSec;
    }

    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

    IJob job = null;
    ITask taskEncoding = null;

    int OutputMES = -1;
    int OutputPremium = -1;
    int OutputIndex1 = -1;
    int OutputIndex2 = -1;
    int OutputOCR = -1;
    int OutputFace = -1;
    int OutputMotion = -1;
    int OutputSummarization = -1;
    int OutputHyperlapse = -1;

    TimeSpan starttime = TimeSpan.FromSeconds(0);
    TimeSpan duration = TimeSpan.FromSeconds(intervalsec);

    try
    {
        // Create and cache the Media Services credentials in a static class variable.
        _cachedCredentials = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

        // Used the chached credentials to create CloudMediaContext.
        _context = new CloudMediaContext(_cachedCredentials);

        // find the Asset
        string programid = (string)data.ProgramId;
        var asset = GetAssetFromProgram(programid);

        if (asset == null)
        {
            log.Info($"Asset or Program not found {programid}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new
            {
                error = "Asset or Program not found"
            });
        }

        log.Info($"Using asset Id : {asset.Id}");

        // Get the manifest data (timestamps)
        var assetmanifestdata = GetManifestTimingData(asset);

        starttime = TimeSpan.FromSeconds((double)assetmanifestdata.TimestampOffset / (double)assetmanifestdata.TimeScale) + assetmanifestdata.AssetDuration.Subtract(TimeSpan.FromSeconds(intervalsec));
        string ConfigurationSubclip = File.ReadAllText(@"D:\home\site\wwwroot\Presets\LiveSubclip.json").Replace("0:00:00.000000", starttime.ToString()).Replace("0:00:30.000000", duration.ToString());


        //MES Subclipping TASK
        // Declare a new encoding job with the Standard encoder
        job = _context.Jobs.Create("Azure Function - Job for Live Analytics");
        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor processor = GetLatestMediaProcessorByName("Media Encoder Standard");

        // Change or modify the custom preset JSON used here.
        // string preset = File.ReadAllText("D:\home\site\wwwroot\Presets\H264 Multiple Bitrate 720p.json");

        // Create a task with the encoding details, using a string preset.
        // In this case "H264 Multiple Bitrate 720p" system defined preset is used.
        taskEncoding = job.Tasks.AddNew("Subclipping task",
           processor,
           ConfigurationSubclip,
           TaskOptions.None);

        // Specify the input asset to be encoded.
        taskEncoding.InputAssets.Add(asset);
        OutputMES = _taskindex++;

        // Add an output asset to contain the results of the job. 
        // This output is specified as AssetCreationOptions.None, which 
        // means the output asset is not encrypted. 
        var subclipasset = taskEncoding.OutputAssets.AddNew(asset.Name + " subclipped", AssetCreationOptions.None);

        log.Info($"Adding media analytics tasks");

        // Media Analytics
        OutputIndex1 = AddTask(job, subclipasset, (string)data.IndexV1Language, "Azure Media Indexer", "IndexerV1.xml", "English");
        OutputIndex2 = AddTask(job, subclipasset, (string)data.IndexV2Language, "Azure Media Indexer 2 Preview", "IndexerV2.json", "EnUs");
        OutputOCR = AddTask(job, subclipasset, (string)data.OCRLanguage, "Azure Media OCR", "OCR.json", "AutoDetect");
        OutputFace = AddTask(job, subclipasset, (string)data.FaceDetectionMode, "Azure Media Face Detector", "FaceDetection.json", "PerFaceEmotion");
        OutputMotion = AddTask(job, subclipasset, (string)data.MotionDetectionLevel, "Azure Media Motion Detector", "MotionDetection.json", "medium");
        OutputSummarization = AddTask(job, subclipasset, (string)data.SummarizationDuration, "Azure Media Video Thumbnails", "Summarization.json", "0.0");
        OutputHyperlapse = AddTask(job, subclipasset, (string)data.HyperlapseSpeed, "Azure Media Hyperlapse", "Hyperlapse.json", "8");

        job.Submit();
        log.Info("Job Submitted");

        log.Info($"Output MES index {OutputMES}");
        // Let store some data in altid of subclipped asset
        var sid = ReturnId(job, OutputMES);
        log.Info($"SID {sid}");

        var subclipassetrefreshed = _context.Assets.Where(a => a.Id == sid).FirstOrDefault();
        log.Info($"subclipassetrefreshed ID {subclipassetrefreshed.Id}");
        subclipassetrefreshed.AlternateId = JsonConvert.SerializeObject(new SubclipInfo() { ProgramId = programid, StartTime = starttime, Duration = duration });
        subclipassetrefreshed.Update();

    }
    catch (Exception ex)
    {
        log.Info($"Exception {ex}");
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    log.Info("Job Id: " + job.Id);
    log.Info("Output asset Id: " + ((OutputMES > -1) ? ReturnId(job, OutputMES) : ReturnId(job, OutputPremium)));

    return req.CreateResponse(HttpStatusCode.OK, new
    {
        JobId = job.Id,
        OutputAssetId = ReturnId(job, OutputMES),
        OutputAssetIndexV1Id = ReturnId(job, OutputIndex1),
        OutputAssetIndexV2Id = ReturnId(job, OutputIndex2),
        OutputAssetOCRId = ReturnId(job, OutputOCR),
        OutputAssetFaceDetectionId = ReturnId(job, OutputFace),
        OutputAssetMotionDetectionId = ReturnId(job, OutputMotion),
        OutputAssetSummarizationId = ReturnId(job, OutputSummarization),
        OutputAssetHyperlapseId = ReturnId(job, OutputHyperlapse),
    });
}

public static string ReturnId(IJob job, int index)
{
    return index > -1 ? job.OutputMediaAssets[index].Id : "";
}

public static int AddTask(IJob job, IAsset sourceAsset, string value, string processor, string presetfilename, string stringtoreplace)
{
    if (value != null)
    {
        // Get a media processor reference, and pass to it the name of the 
        // processor to use for the specific task.
        IMediaProcessor mediaProcessor = GetLatestMediaProcessorByName(processor);

        string Configuration = File.ReadAllText(@"D:\home\site\wwwroot\Presets\" + presetfilename).Replace(stringtoreplace, value);

        // Create a task with the encoding details, using a string preset.
        var task = job.Tasks.AddNew(processor + " task",
           mediaProcessor,
           Configuration,
           TaskOptions.None);

        // Specify the input asset to be indexed.
        task.InputAssets.Add(sourceAsset);

        // Add an output asset to contain the results of the job.
        task.OutputAssets.AddNew(processor + " Output Asset", AssetCreationOptions.None);

        return _taskindex++;
    }
    else
    {
        return -1;
    }
}

static IAsset GetAssetFromProgram(string programId)
{
    IAsset asset = null;

    try
    {
        IProgram program = _context.Programs.Where(p => p.Id == programId).FirstOrDefault();
        if (program != null)
        {
            asset = program.Asset;
        }
    }
    catch
    {
    }
    return asset;
}

static public ManifestTimingData GetManifestTimingData(IAsset asset)
// Parse the manifest and get data from it
{
    ManifestTimingData response = new ManifestTimingData() { IsLive = false, Error = false, TimestampOffset = 0 };

    try
    {
        ILocator mytemplocator = null;
        Uri myuri = GetValidOnDemandURI(asset);
        if (myuri == null)
        {
            mytemplocator = CreatedTemporaryOnDemandLocator(asset);
            myuri = GetValidOnDemandURI(asset);
        }
        if (myuri != null)
        {
            XDocument manifest = XDocument.Load(myuri.ToString());
            var smoothmedia = manifest.Element("SmoothStreamingMedia");
            var videotrack = smoothmedia.Elements("StreamIndex").Where(a => a.Attribute("Type").Value == "video");

            // TIMESCALE
            string timescalefrommanifest = smoothmedia.Attribute("TimeScale").Value;
            if (videotrack.FirstOrDefault().Attribute("TimeScale") != null) // there is timescale value in the video track. Let's take this one.
            {
                timescalefrommanifest = videotrack.FirstOrDefault().Attribute("TimeScale").Value;
            }
            ulong timescale = ulong.Parse(timescalefrommanifest);
            response.TimeScale = (timescale == TimeSpan.TicksPerSecond) ? null : (ulong?)timescale; // if 10000000 then null (default)

            // Timestamp offset
            if (videotrack.FirstOrDefault().Element("c").Attribute("t") != null)
            {
                response.TimestampOffset = ulong.Parse(videotrack.FirstOrDefault().Element("c").Attribute("t").Value);
            }
            else
            {
                response.TimestampOffset = 0; // no timestamp, so it should be 0
            }

            if (smoothmedia.Attribute("IsLive") != null && smoothmedia.Attribute("IsLive").Value == "TRUE")
            { // Live asset.... No duration to read (but we can read scaling and compute duration if no gap)
                response.IsLive = true;

                long duration = 0;
                long r, d;
                foreach (var chunk in videotrack.Elements("c"))
                {
                    if (chunk.Attribute("t") != null)
                    {
                        duration = long.Parse(chunk.Attribute("t").Value) - (long)response.TimestampOffset; // new timestamp, perhaps gap in live stream....
                    }
                    d = chunk.Attribute("d") != null ? long.Parse(chunk.Attribute("d").Value) : 0;
                    r = chunk.Attribute("r") != null ? long.Parse(chunk.Attribute("r").Value) : 1;
                    duration += d * r;
                }
                response.AssetDuration = TimeSpan.FromSeconds((double)duration / ((double)timescale));
            }
            else
            {
                ulong duration = ulong.Parse(smoothmedia.Attribute("Duration").Value);
                response.AssetDuration = TimeSpan.FromSeconds((double)duration / ((double)timescale));
            }
        }
        else
        {
            response.Error = true;
        }
        if (mytemplocator != null) mytemplocator.Delete();
    }
    catch
    {
        response.Error = true;
    }
    return response;
}



public static ILocator CreatedTemporaryOnDemandLocator(IAsset asset)
{
    ILocator tempLocator = null;

    try
    {
        var locatorTask = Task.Factory.StartNew(() =>
        {
            try
            {
                tempLocator = asset.GetMediaContext().Locators.Create(LocatorType.OnDemandOrigin, asset, AccessPermissions.Read, TimeSpan.FromHours(1));
            }
            catch
            {
                throw;
            }
        });
        locatorTask.Wait();
    }
    catch (Exception ex)
    {
        throw ex;
    }

    return tempLocator;
}


public class ManifestTimingData
{
    public TimeSpan AssetDuration { get; set; }
    public ulong TimestampOffset { get; set; }
    public ulong? TimeScale { get; set; }
    public bool IsLive { get; set; }
    public bool Error { get; set; }
}

public class SubclipInfo
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string ProgramId { get; set; }
}



