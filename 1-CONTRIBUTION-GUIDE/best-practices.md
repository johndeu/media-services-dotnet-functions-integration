# Azure Media Services Integration Samples for Azure Functions and Logic Apps

This repository contains all currently available Functions and Logic App solution templates for use with Azure Media Services. These solution sample quick starts provide a collection of real-world scenarios to help you build your own unique media encoding, processing, analytics, and streaming solutions. 

The following information is relevant to get started with contributing to this repository.

+ [**Contribution guide**](/1-CONTRIBUTION-GUIDE/README.md#contribution-guide). Describes the minimal guidelines for contributing.
+ [**Best practices**](/1-CONTRIBUTION-GUIDE/best-practices.md#best-practices). Best practices for improving the quality of your template design.
+ [**Git tutorial**](/1-CONTRIBUTION-GUIDE/git-tutorial.md#git-tutorial). Step by step to get you started with Git.

You are currently reading the best practices.

## Best practices for Creating a New Solution

+ It is required to create a new solution folder with the naming convention of "LEVEL-solution-name", where LEVEL should indicate the complexity of the solution and the amount of code or customization that may be required to use the solution sample. Always use a short descriptive name for the "solution-name" so that it is easy to discover. Avoid using really long solution names that won't display in the GitHub user interface. 
+ Always include a README.md that explains your solution
+ Always keep your solution folder self contained. Do not take dependency on files or scripts in the root of the repo. You are welcome to take a dependence on another set of Azure Functions that may be in the repo, but make note of that in your README.md if there is a requirement to deploy another template prior to using your solution folder. 
+ Always use the latest verion of the Azure Media Services .NET SDK by importing it as a dependency in your **project.json** file. You can also take a dependency on the extensions library for Media Services. 

```
{
    "frameworks": {
        "net46":{
        "dependencies": {
            "windowsazure.mediaservices": "3.8.0.5",
            "windowsazure.mediaservices.extensions": "3.8.0.3"
        }
        }
    }
}
```
+ There are some common, shared scripts that are used across many functions. You can find examples of these scripts in the "/shared" folder.  If you wish to modify the scripts, it is recommended that you first move them into your own solution folder in a **shared** folder so that you do not break other solutions. Keep everything self-contained. 
+ Always update the primary root level README.md with information about your solution and a linke to your detailed README.md

## Best practices for ARM templates

+ It is a good practice to pass your ARM template through a JSON linter to remove extraneous commas, parenthesis, brackets that may break the "Deploy to Azure" experience. Try http://jsonlint.com/ or a linter package for your favorite editing environment (Visual Studio Code, Atom, Sublime Text, Visual Studio etc.)
+ It's also a good idea to format your JSON for better readability. You can use a JSON formatter package for your local editor or [format online using this link](https://www.bing.com/search?q=json+formatter).

For more best practices on creating a custom ARM template for deploying your solution please follow the guidelines in the [**Azure Quick Start Best Practices**](https://github.com/Azure/azure-quickstart-templates/blob/master/1-CONTRIBUTION-GUIDE/best-practices.md)]
