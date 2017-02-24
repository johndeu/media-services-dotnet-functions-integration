# Logic Apps which use Azure Functions and Azure Media Services

## First Logic App : Simple VOD workflow

### Presentation

This template creates a Logic app that listens to an onedrive folder and will copy it to an Azure Media Services asset, triggers an encoding job, publish the output asset and send an email when the process is complete.

![Screen capture](images/simplevod-1.png?raw=true)
![Screen capture](images/simplevod-2.png?raw=true)

### 1. Prerequisite
If not already done : Deploy Azure Functions and select the **"200-logic-app"** Project (important !)

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

### 2. Deploy the logic app

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fmedia-services-dotnet-functions-integration%2Fmaster%2F200-logic-app%2Flogicapp-simplevod-deploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

