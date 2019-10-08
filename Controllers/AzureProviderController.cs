using Syncfusion.EJ2.FileManager.AzureFileProvider;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using Syncfusion.EJ2.FileManager.Base;
using System;

namespace EJ2AzureASPCoreFileProvider.Controllers
{

    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class AzureProviderController : Controller
    {
        public AzureFileProvider operation;
        public AzureProviderController(IHostingEnvironment hostingEnvironment)
        {
            this.operation = new AzureFileProvider();
            this.operation.RegisterAzure("ej2syncfusionfilemanager", "cgKqBPKOGYjPPKn/0eHa9XrYvhPThD43yDAk6QXiEW34kN5cTYY+rD0m/+aHTB1c7TbFSiq3MPDEn8mKMX7jjA==", "files");
            this.operation.setBlobContainer("https://ej2syncfusionfilemanager.blob.core.windows.net/files/", "https://ej2syncfusionfilemanager.blob.core.windows.net/files/Files");
        }
        [Route("AzureFileOperations")]
        public object AzureFileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                string startPath = "https://ej2syncfusionfilemanager.blob.core.windows.net/files/";
                string originalPath = ("https://ej2syncfusionfilemanager.blob.core.windows.net/files/Files/").Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
                args.TargetPath = (originalPath + args.TargetPath).Replace("//", "/");
            }
            switch (args.Action)
            {
                case "read":
                    // reads the file(s) or folder(s) from the given path.
                    return Json(this.ToCamelCase(this.operation.GetFiles(args.Path, args.Data)));
                case "delete":
                    // deletes the selected file(s) or folder(s) from the given path.
                    return this.ToCamelCase(this.operation.Delete(args.Path, args.Names, args.Data));
                case "details":
                    // gets the details of the selected file(s) or folder(s).
                    return this.ToCamelCase(this.operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    // creates a new folder in a given path.
                    return this.ToCamelCase(this.operation.Create(args.Path, args.Name));
                case "search":
                    // gets the list of file(s) or folder(s) from a given path based on the searched key string.
                    return this.ToCamelCase(this.operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    // renames a file or folder.
                    return this.ToCamelCase(this.operation.Rename(args.Path, args.Name, args.NewName, false, args.Data));
                case "copy":
                    // copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.ToCamelCase(this.operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "move":
                    // cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.ToCamelCase(this.operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));

            }
            return null;
        }
        public string ToCamelCase(object userData)
        {
            return JsonConvert.SerializeObject(userData, new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
        }

        // uploads the file(s) into a specified path
        [Route("AzureUpload")]
        public ActionResult AzureUpload(FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                string startPath = "https://ej2filemanager.blob.core.windows.net/files/";
                string originalPath = ("https://ej2filemanager.blob.core.windows.net/files/Files/").Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
            }
            operation.Upload(args.Path, args.UploadFiles, args.Action, args.Data);
            return Json("");
        }

        // downloads the selected file(s) and folder(s)
        [Route("AzureDownload")]
        public object AzureDownload(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return operation.Download(args.Path, args.Names, args.Data);
        }

        // gets the image(s) from the given path
        [Route("AzureGetImage")]
        public IActionResult AzureGetImage(FileManagerDirectoryContent args)
        {
            return this.operation.GetImage(args.Path, args.Id, true, null, args.Data);
        }
    }

}
