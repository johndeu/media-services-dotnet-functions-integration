# Azure Media Services Functions and Logic Apps Contribution Guide

This repo contains a collection of scenario based Azure Media Services templates contributed by the community. 
The following information is relevant to get started with contributing to this repository.

+ [**Contribution guide**](/1-CONTRIBUTION-GUIDE/README.md#contribution-guide). Describes the minimal guidelines for contributing a new Function or Logic App to this repo
+ [**Best practices**](/1-CONTRIBUTION-GUIDE/best-practices.md#best-practices). Best practices for improving the quality of your custom Azure Function or Logic App soltion.
+ [**Git tutorial**](/1-CONTRIBUTION-GUIDE/git-tutorial.md#git-tutorial). Step by step to get you started with Git.

## Project Organization

The repo is currently organized into seperate folders by high level scenario. For example, the 100-basic-encoding folder is considered the simplest "100" level example of using Azure Functions in combination with blob storage triggers to ingest a file dropped into a container in a storage account and immediately encode it.   

As the samples get more complicated, we are incrementing the solution level number to indicate the overall complexity of the solution.  When contributing a new folder of Functions to this repo, please start by creating an appropriately leveled folder with a descriptive name of the Media solution that you are solving with the functions.

The [media-functions-for-logic-app](/media-functions-for-logic-app) folder is a special folder that contains a set of Functions maintained by the Media Services team for use with Logic Apps. You are welcome to use these functions in your own logic apps, or submit bug fixes against this folder, but please check with one of the community leads before submitting new functions into this folder.  You are more than welcome to create new Logic App examples that utilize these functions and name them accordingly based on the level of complexity. 

## Deploying Samples

You can deploy these samples directly through the Azure Portal or by using the ARM template supplied in the root of the repo or folder

To deploy a sample using the Azure Portal, click the **Deploy to Azure** button found in the README.md at the root of this repo, or in the README.md in each sample you wish to deploy.  

The root [azuredeploy.json](/azuredeploy.json) ARM template contains a paramter called "Project" that will allow you to select which folder of Azure Functions you wish to deploy. 

To deploy the sample via the command line (using [Azure PowerShell or the Azure CLI](https://azure.microsoft.com/en-us/downloads/)) you can use the scripts.


## Contribution guide

To make sure your Function or Logic App template is approved to be added to this project repo, please follow these guidelines. 

## Files, folders and naming conventions

1. Every Azure Function or Logic App and associated ARM deployment template must be self-contained in its own **folder**. Name this folder something that describes what your solution does. Usually this naming pattern looks like **level-platformCapability** (e.g. 100-basic-encoding) 
 + **Required** - Numbering should start at 101. 100 is reserved for things that need to be at the top.
 + **Protip** - Try to keep the name of your template folder short so that it fits inside the Github folder name column width. 
 + **Required** - Don't repeat an existing solution scenario. (e.g. No need to create another basic encoding project. Do something unique to a real-world customer solution or problem)
2. Github uses ASCII for ordering files and folder. For consistent ordering **create all files and folders in lowercase**. The only **exception** to this guideline is the **README.md**, that should be in the format **UPPERCASE.lowercase**.
3. Include a **README.md** file that explains how the solution works. This should include images, and a description of the deployment process and settings required.  Please test your own documentation first to make approval of the Pull Request go faster. 
 + Guidelines on the README.md file below.
4. The deployment template file must be named **azuredeploy.json**.
5. There should be a parameters file named **azuredeploy.parameters.json**. 
 + Please fill out the values for the parameters according to rules defined in the template (allowed values etc.)
6. The template folder must contain a **metadata.json** file to allow the template to be indexed on [Azure.com](http://azure.microsoft.com/). 
 + Guidelines on the metadata.json file below.
7. Any helper .csx scripts that are shared across your Functions should be placed in a folder called **helpers** or  **shared**.
8. Any custom encoding settings or presets should be placed into a folder called **presets**
9. Linked ARM deployment templates must be placed in a folder called **nested**.
10. Images used in the README.md must be placed in a folder called **images**. 


## README.md

The README.md describes your Function or Logic App in detail. A good description helps other community members to understand your solution and what it solves. Please try to use real-world customer based solutions when contributing and if possible write out the full customer use-case scenario in the README. 

The README.md uses [Github Flavored Markdown](https://guides.github.com/features/mastering-markdown/) for formatting text. If you want to add images to your README.md file, store the images in the **images** folder. Reference the images in the README.md with a relative path (e.g. `![alt text](images/namingConvention.png "Files, folders and naming conventions")`). This ensures the link will reference the target repository if the source repository is forked. 

A good README.md contains the following sections
+ Deploy to Azure button
+ Visualize button (optional)
+ Description of what the solution will deploy
+ Tags, that can be used for search. Specify the tags comma separated and enclosed between two back-ticks (e.g Tags: `cluster, ha, sql`)
+ *Optional: Prerequisites (other functions in the project for example)
+ *Optional: Description on how to use the solution
+ *Optional: Logic Apps connectors that require registration and setup post deployment (e.g. OneDrive, Twitter, Twilio,etc.)
+ *Optional: Notes

Do **not include** any **service or API keys** in the deployment template.


## metadata.json

A valid metadata.json must adhere to the following structure

```
{
  "itemDisplayName": "",
  "description": "",
  "summary": "",
  "githubUsername": "",
  "dateUpdated": "<e.g. 2015-12-20>"
}
```

The metadata.json file will be validated using these rules

**itemDisplayName**

+ Cannot be more than 60 characters

**description**

+ Cannot be more than 1000 characters
+ Cannot contain HTML This is used for the template description on the Azure.com index template details page

**summary**

+ Cannot be more than 200 characters
+ This is shown for template description on the main Azure.com template index page

**githubUsername**

+ This is the username of the original template author. Do not change this
+ This is used to display template author and Github profile pic in the Azure.com index

**dateUpdated**

+ Must be in yyyy-mm-dd format.
+ The date must not be in the future to the date of the pull request


### raw.githubusercontent.com Links

If you're making use of **raw.githubusercontent.com** links within your template contribution (within the template file itself or any scripts in your contribution) please ensure the following:

+ Ensure any raw.githubusercontent.com links which refer to content within your pull request points to `https://raw.githubusercontent.com/Azure-Samples/media-services-dotnet-functions-integration/...` and **NOT** your fork.
+ All raw.githubusercontent.com links are placed in your azuredeploy.json and you pass the link down into your scripts & linked templates via this top-level template. This ensures we re-link correctly from your pull-request repository and branch.
+ Although pull requests with links pointing to `https://raw.githubusercontent.com/Azure-Samples/media-services-dotnet-functions-integration/master/...` may not exist in the Azure repo at the time of your pull-request, at CI run-time, those links will be converted to `https://raw.githubusercontent.com/{your_user_name}/media-services-dotnet-functions-integration/{your_branch}/...`. Be sure to check the casing of `https://raw.githubusercontent.com/Azure-Samples/media-services-dotnet-functions-integration/master/...` as this is case-sensitive.




This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). 

For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
