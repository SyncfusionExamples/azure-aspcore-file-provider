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
using Microsoft.Extensions.Configuration;

namespace EJ2AzureASPCoreFileProvider.Controllers
{

    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class AzureProviderController : Controller
    {
        public AzureFileProvider _operation;
        public IWebHostEnvironment _env;
        public IConfiguration _configuration;
        public string _blobPath;
        public string _filePath;
        public string _containerName;
        public AzureProviderController(IWebHostEnvironment HostingEnvironment, IConfiguration Configuration)
        {
            _env = HostingEnvironment;
            _configuration = Configuration;
            var AccountName = _configuration.GetSection("CloudBlobStorageSettings").GetValue<string>("AccountName");
            var AccountKey = _configuration.GetSection("CloudBlobStorageSettings").GetValue<string>("AccountKey");
            _blobPath = _configuration.GetSection("CloudBlobStorageSettings").GetValue<string>("BlobPath");
            _filePath = _configuration.GetSection("CloudBlobStorageSettings").GetValue<string>("FilePath");
            _containerName = _configuration.GetSection("CloudBlobStorageSettings").GetValue<string>("ContainerName");
            this._operation = new AzureFileProvider();
            this._operation.RegisterAzure(AccountName, AccountKey, _containerName);
            this._operation.setBlobContainer(_blobPath, _filePath);
        }

        [Route("AzureFileOperations")]
        public object AzureFileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                string startPath = _blobPath;
                string originalPath = _filePath.Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
                args.TargetPath = (originalPath + args.TargetPath).Replace("//", "/");
            }
            switch (args.Action)
            {
                case "read":
                    // reads the file(s) or folder(s) from the given path.
                    return Json(this.ToCamelCase(this._operation.GetFiles(args.Path, args.Data)));
                case "delete":
                    // deletes the selected file(s) or folder(s) from the given path.
                    return this.ToCamelCase(this._operation.Delete(args.Path, args.Names, args.Data));
                case "details":
                    // gets the details of the selected file(s) or folder(s).
                    return this.ToCamelCase(this._operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    // creates a new folder in a given path.
                    return this.ToCamelCase(this._operation.Create(args.Path, args.Name));
                case "search":
                    // gets the list of file(s) or folder(s) from a given path based on the searched key string.
                    return this.ToCamelCase(this._operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    // renames a file or folder.
                    return this.ToCamelCase(this._operation.Rename(args.Path, args.Name, args.NewName, false, args.Data));
                case "copy":
                    // copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.ToCamelCase(this._operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "move":
                    // cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.ToCamelCase(this._operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));

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
                string startPath = _blobPath;
                string originalPath = _filePath.Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
            }
            _operation.Upload(args.Path, args.UploadFiles, args.Action, args.Data);
            return Json("");
        }

        // downloads the selected file(s) and folder(s)
        [Route("AzureDownload")]
        public object AzureDownload(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return _operation.Download(args.Path, args.Names, args.Data);
        }

        // gets the image(s) from the given path
        [Route("AzureGetImage")]
        public IActionResult AzureGetImage(FileManagerDirectoryContent args)
        {
            return this._operation.GetImage(args.Path, args.Id, true, null, args.Data);
        }
    }

}
