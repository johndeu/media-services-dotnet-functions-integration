# Logic Apps which use Azure Functions and Azure Media Services

## First Logic App : Simple VOD workflow

### Presentation

This template creates a Logic app that listens to an onedrive folder and will copy it to an Azure Media Services asset, triggers an encoding job, publish the output asset and send an email when the process is complete.

![Screen capture](images/simplevod-1.png?raw=true)
![Screen capture](images/simplevod-2.png?raw=true)

[See the detailed view of the logic app.](logicapp-simplevod-screen.md)

### 1. Prerequisite
If not already done : fork the repo, deploy Azure Functions and select the **"200-logic-app"** Project (important !)

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

### 2. Deploy the logic app

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2F200-logic-app%2Flogicapp-simplevod-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

![Screen capture](images/form-simplevod.png?raw=true)

It is recommended to use the same resource group for the functions and the logic app.
The functions and Logic App must be deployed in the same region.
Please specify the name of the storage account used by Media Services.

## Functions documentation
This section list the functions available and describes the input and output parameters.

### create-empty-asset

This function creates an empty asset.
```c#
Input:
{
    "assetName" : "the name of the asset"
}

Output:
{
    "assetId" : "the Id of the asset created",
    "containerPath" : "the url to the storage container of the asset"
}
```

### sync-asset

This function create the asset files based on the blobs in the asset container.
```c#
Input:
{
    "assetId" : "the Id of the asset"
}
```

### submit-job

This function submits a job wth encoding and/or analytics.
```c#
Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "mesPreset" : "H264 Multiple Bitrate 720p", // Optional. If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from D:\home\site\wwwroot\Presets
    "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Optional, Id for the workflow asset
    "indexV1Language" : "English", // Optional
    "indexV2Language" : "EnUs", // Optional
    "ocrLanguage" : "AutoDetect" or "English", // Optional
    "faceDetectionMode" : "PerFaceEmotion, // Optional
    "motionDetectionLevel" : "medium", // Optional
    "summarizationDuration" : "0.0", // Optional. 0.0 for automatic
    "hyperlapseSpeed" : "8" // Optional
}

Output:
{
        "jobId" :  // job id
        "outputAssetMESId" : "", 
        "outputAssetMEPWId" : "",
        "outputAssetIndexV1Id" : "",
        "outputAssetIndexV2Id" : "",
        "outputAssetOCRId" : "",
        "outputAssetFaceDetectionId" : "",
        "outputAssetMotionDetectionId" : "",
        "outputAssetSummarizationId" : "",
        "outputAssetHyperlapseId" : ""
}
```

### check-job-status

This function chevck a job status.
```c#
Input:
{
    "jobId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "extendedInfo" : true // optional. Returns ams account unit size, nb units, nb of jobs in queue, scheduled and running states. Only if job is complete or error
 }

Output:
{
    "jobState" : 2, // The state of the job (int)
    "errorText" : "" // error(s) text if job state is error
    "startTime" : ""
    "endTime" : "",
    "runningDuration": ""
    "mediaUnitNumber" : 2,   // if extendedInfo is true and job is finished or in error
    "mediaUnitSize" : "S2", // if extendedInfo is true and job is finished or in error
    "jobQueue" : 3, // if extendedInfo is true and job is finished or in error
    "jobScheduled" : 1, // if extendedInfo is true and job is finished or in error
    "jobProcessing" : 2, // if extendedInfo is true and job is finished or in error
}
```

### publish-asset

This function publishes an asset.
```c#
Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
}

Output:
{
    playerUrl : "", // Url of demo AMP with content
    smoothUrl : "", // Url for the published asset (contains name.ism/manifest at the end) for dynamic packaging
    pathUrl : ""    // Url of the asset (path)
}
```

### return-subtitles

This function returns subtitles from an asset.
```c#
Input:
{
    "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the source asset
}

Output:
{
    "vttUrl" : "",      // the full path to vtt file if asset is published
    "ttmlUrl" : "",     // the full path to vtt file if asset is published
    "pathUrl" : "",     // the path to the asset if asset is published
    "vttDocument" : "", // the full vtt document
    "ttmlDocument : ""  // the full ttml document
 }
```


### add-textfile-to-asset

This function adds a text file to an existing asset.
As a option, the text can be converted from ttml to vtt (useful when the ttml has been translated with MS Translator and the user wants a VTT file for Azure Media Player).

```c#
Input:
{
    "document" : "", // content of the text file to create
    "fileName" : "subtitle-en.ttml", // file name to create
    "assetId" : "nb:cid:UUID:88432c30-cb4a-4496-88c2-b2a05ce9033b", // Mandatory, Id of the asset
    "convertTtml" :true // optional, convert the document from ttml to vtt, and create another file in the asset : subtitle-en.vtt
}

Output:
{
}
```
