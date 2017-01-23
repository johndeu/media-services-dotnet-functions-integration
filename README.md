---
services: media-services,functions
platforms: dotnet
author: johndeu
---

# Deployment: Azure Resource Management Template
<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https://github.com/Azure-Samples/media-services-dotnet-functions-integration/blob/master/arm-deployment.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
<a href="http://armviz.io/#/?load=https://github.com/Azure-Samples/media-services-dotnet-functions-integration/blob/master/arm-deployment.json" target="_blank">
    <img src="http://armviz.io/visualizebutton.png"/>
</a>

# TEST
<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https://github.com/johndeu/media-services-dotnet-functions-integration/blob/master/arm-deployment.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>
<a href="http://armviz.io/?load=https://github.com/johndeu/media-services-dotnet-functions-integration/blob/master/arm-deployment.json" target="_blank">
    <img src="http://armviz.io/visualizebutton.png"/>
</a>


# Media Services: Integrating Azure Media Services with Azure Functions
This project contains examples of using Azure Functions with Azure Media Services. 
The project includes several folders of sample Azure Functions for use with Azure Media Services that show workflows related
to ingesting content directly from blob storage, encoding, and writing content back to blob storage. It also includes examples of
how to monitor job notifications via WebHooks and Azure Queues. 

## How to run the sample

To run the samples, simply fork this project into your own repository and attach your Github account with a new
Azure Functions application. 

To configure the sample Functions, you need to set the following values in your
function's Application Settings.

* **AMSAccount** - your Media Services Account name. 
* **AMSKey** - your Media Services key. 
* **MediaServicesStorageAccountName** - the storage account name tied to your Media Services account. 
* **MediaServicesStorageAccountKey** - the storage account key tied to your Media Services account. 
* **StorageConnection** -  the functions.json file contains a "StorageConnection" property which must be set to an App Setting value that 
  contains a connection string for your input storage account. Otherwise, you may end up with an error message at startup.
  Make sure to add a new AppSetting to your Functions project with the storage account name and connection string, and update
  the functions.json file if you see this error:
* **SigningKey** - the 64-byte Base64 encoded signing key to use to protect and secure your WebHooks callbacks from Azure Media Services

    Example value: `wOlDEUJ4/VN1No8HxVxpsRvej0DZrO5DXvImGLjFhfctPGFiMkUA0Cj8HSfJW7lePX9XsfHAMhw30p0yYqG+1A==`

* **WebHookEndpoint** - the Webhook URL endpoint for the deployed Notification_Webhook_Function in this project to be used by Azure Media Services
  to callback to your Function from the Encoding job Functions. 
  

  ### Connection Strings:
  To find the connection string for your storage account, open the storage account in the 
  Azure portal(Ibiza). Go to Access Keys in Settings. In the Access Keys blade
  go to Key1, or Key2, click the "..." menu and select "view connection string". Copy the connection string.
  
  ### Code Modifications Required:
  The output container name can be modifed in run.csx by changing the value of the static string _outputContainerName.
  It's set to "output" by default. 

## EncodeBlob_SingleOut_Function
The EncodeBlob_SingleOut_Function demonstrates how to use an Output binding and the "InOut" direction binding to 
allow the Azure functions framework to create the output blob for you automatically. 

In the function.json, you will notice that we use a binding direction of "InOut" and also set the name to "outputBlob".
The path is also updated to point to a specific output container, and a pattern is provided for naming the output file. 
Notice that we are binding the input {filename} to the output {filename} pattern match, and also specifying a default
extension of "-Output.mp4". 

    {
      "name": "outputBlob",
      "type": "blob",
      "direction": "InOut",
      "path": "output/{fileName}-Output.mp4",
      "connection": "StorageConnection"
    }

In the run.csx file, we then bind this outputBlob to the Run method signature as a CloudBlockBlob. 

    public static void Run( CloudBlockBlob inputBlob, 
                            string fileName, 
                            string fileExtension, 
                            CloudBlockBlob outputBlob, 
                            TraceWriter log)

To output data to this outputBlob, we have to copy data into it. The CopyBlob() helper method (in 'Shared/copyBlobHelpers.csx') is used to copy the stream 
from the source blob to the output blob. Since the copy is done async, we have to call Wait() and halt the function execution until the copy is complete.

    CopyBlob(jobOutput,outputBlob).Wait();

Finally, we can set a few properties on the outputBlob before the function returns, and the blob is written to the configured 
output storage account set in the function.json binding.

          
    // Change some settings on the output blob.
    outputBlob.Metadata["Custom1"] = "Some Custom Metadata";
    outputBlob.Properties.ContentType = "video/mp4";
    outputBlob.SetProperties();

## EncodeBlob_Notify_Webhook_Function

This function demonstrates how to use WebHooks to listen to a basic encoding job's progress.  
The function works in combination with the Notification_Webhook_Function, which acts as that "callback" for the Job status
Notifications.

When setting up the Job in this function, you will note that the webhook is passed in as a Notification endpoint along with its
signing key for securing the payload.  You must set the signingKey and the WebHook endpoint in the App settings as specified above.

This workflow for this function waits for content to be copied into the input container in blob storage. 
This is configured in the function.json file's bindings.

    {
        "name": "inputBlob",
        "type": "blobTrigger",
        "direction": "in",
        "path": "input/{fileName}.{fileExtension}",
        "connection": "StorageConnection"
    }

The name property sets the name of the CloudBlockBlob property that is passed into the Run method. 
The path property sets the container name and file matching pattern to use. In this example,
we set the {fileName} and {fileExtension} matching patterns to pass the two values into the Run function.

    public static void Run(CloudBlockBlob inputBlob, TraceWriter log, string fileName, string fileExtension)

You can monitor the callbacks in the Notification_Webhook_Function logs while the job is running. To test the method, drop 
a new media file into the container specified in the binding's input path. 


## EncodeBlob_MultiOut_Function

This function can call a Logic App at the end.
Specify the call back Url in **LogicAppCallbackUrl** in your function's Application Settings.


## EncodeBlob_MultiOut_MultiFilesInput_Function (Multiple files / single asset Function)
This function will upload several files into a single asset.
A json file must be uploaded to the blob container withh the referenced files.

The format of the json file is:

    [
      {
        "fileName": "BigBuckBunny.mp4",
        "isPrimary": true
      },
      {
        "fileName": "Logo.png"
      }
    ]

## LA-1-Ingest, LA-2-SubmitJob, LA-3-CheckJobStatus, LA-4-Publish functions
These functions are designed to be called by a Logic App. More details and Logic App samples to come. 


### License
This sample project is licensed under [the MIT License](LICENSE.txt)

## ToDO 
- [ ] The Azure Queue notification function is not yet complete
- [ ] Copy Blobs currently is using Streams, and copies in an inefficient way.
