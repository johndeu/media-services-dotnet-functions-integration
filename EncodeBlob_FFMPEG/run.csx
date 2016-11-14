#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#load "../Shared/copyBlobHelpers.csx"
#load "../Shared/mediaServicesHelpers.csx"

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


static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

private static CloudStorageAccount _destinationStorageAccount = null;

// Field for service context.
private static CloudMediaContext _context = null;
private static MediaServicesCredentials _cachedCredentials = null;

private static int lineCount = 0;
private static StringBuilder output = new StringBuilder();


public static void Run(CloudBlockBlob inputBlob, string fileName, string fileExtension, CloudBlockBlob outputBlob, TraceWriter log)
{
    // NOTE that the variables {fileName} and {fileExtension} here come from the path setting in function.json
    // and are passed into the  Run method signature above. We can use this to make decisions on what type of file
    // was dropped into the input container for the function. 

    // No need to do any Retry strategy in this function, By default, the SDK calls a function up to 5 times for a 
    // given blob. If the fifth try fails, the SDK adds a message to a queue named webjobs-blobtrigger-poison.

    log.Info($"C# Blob  trigger function processed: {fileName}.{fileExtension}");
    log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

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
        
        StorageCredentials mediaServicesStorageCredentials =
            new StorageCredentials(_storageAccountName, _storageAccountKey);

        Process process = new Process();

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.FileName = "\bin\ffmpeg\ffmpeg.exe";
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.Arguments = "-v";
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            // Prepend line numbers to each line of the output.
            if (!String.IsNullOrEmpty(e.Data))
            {
                lineCount++;
                output.Append("\nFFMPEG: [" + lineCount + "]: " + e.Data);
            }
        });

        process.Start();
        
        // Asynchronously read the standard output of the spawned process. 
        // This raises OutputDataReceived events for each line of output.
        process.BeginOutputReadLine();
        process.WaitForExit();
        log.Info(output);
       
        
        log.Info("Done!");

    }
    catch (Exception ex)
    {
        log.Error("ERROR: failed.");
        log.Info($"StackTrace : {ex.StackTrace}");
        throw ex;
    }
}