# Azure Cloud File System Provider for Essential JS2 File Manager

This repository contains the ASP.NET Core Azure storage file system providers for the Essential JS 2 File Manager component.

## Key Features

Azure file system provider serves the file system support for the FileManager component with the Microsoft Azure blob storage.

The following actions can be performed with Azure file system Provider.

- Read     - Read the files from Azure blob container.
- Details  - Provides details about files Type, Size, Location and Modified date.
- Download - Download the selected file or folder from the Azure blob.
- Upload   - Upload a files to Azure blob. It accepts uploaded media with the following characteristics:
                - Maximum file size:  30MB
- Create   - Create a new folder.
- Delete   - Remove a file from Azure blob.
- Copy     - Copy the selected Files from target.
- Move     - Paste the copied files to the desired location
- Rename   - Rename a folder or file
- Search   - Search a file or folder in Azure blob

## Prerequisites

In order to run the service, we need to create the [Azure blob storage account](https://docs.microsoft.com/en-us/azure/storage/common/storage-quickstart-create-account?tabs=azure-portal) and register the Azure storage details like  account name, password and blob name details with in the RegisterAzure method.

```

  RegisterAzure(string accountName, string accountKey, string blobName)

```

## How to run this application?

To run this application, clone the [`ej2-azure-aspcore-file-provider`](https://github.com/SyncfusionExamples/ej2-azure-aspcore-file-provider) repository and then navigate to its appropriate path where it has been located in your system.

To do so, open the command prompt and run the below commands one after the other.

```

git clone https://github.com/SyncfusionExamples/ej2-azure-aspcore-file-provider  ej2-azure-aspcore-file-provider
cd ej2-azure-aspcore-file-provider

```

## Running application

Once cloned, open solution file in visual studio.Then build the project after restoring the nuget packages and run it.

## Support

Product support is available for through following mediums.

* Creating incident in Syncfusion [Direct-trac](https://www.syncfusion.com/support/directtrac/incidents?utm_source=npm&utm_campaign=filemanager) support system or [Community forum](https://www.syncfusion.com/forums/essential-js2?utm_source=npm&utm_campaign=filemanager).
* New [GitHub issue](https://github.com/syncfusion/ej2-javascript-ui-controls/issues/new).
* Ask your query in [Stack Overflow](https://stackoverflow.com/?utm_source=npm&utm_campaign=filemanager) with tag `syncfusion` and `ej2`.

## License

Check the license detail [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/license).

## Changelog

Check the changelog [here](https://github.com/syncfusion/ej2-javascript-ui-controls/blob/master/controls/filemanager/CHANGELOG.md)

© Copyright 2019 Syncfusion, Inc. All Rights Reserved. The Syncfusion Essential Studio license and copyright applies to this distribution.
