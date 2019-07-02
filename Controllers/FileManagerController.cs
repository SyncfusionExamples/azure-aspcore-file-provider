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

namespace EJ2FileManagerServices.Controllers
{

    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class FileManagerController : Controller
    {

        public string basePath;
        public AzureFileProvider operation;
    
        public FileManagerController(IHostingEnvironment hostingEnvironment)
        {            
            this.operation = new AzureFileProvider();
            this.operation.RegisterAzure("ej2filemanager", "xXmW7B3PCUtbcj8kCtRBhJoy7IsarKP99jv0VK8VxvIKhKqQr/xd12BgRAzc0Yv6DVNLBh9DtVncc+4eBiPN9Q==", "files");
            this.operation.setBlobContainer("https://ej2filemanager.blob.core.windows.net/files/", "https://ej2filemanager.blob.core.windows.net/files/Files");
            this.operation.setDownloadPath(@"D:\");
        }
        [Route("AzureFileOperations")]
        public object AzureFileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                var originalPath = "https://ej2filemanager.blob.core.windows.net/files/Files/";
                string startPath = "https://ej2filemanager.blob.core.windows.net/files/";
                originalPath = originalPath.Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
                args.TargetPath = (originalPath + args.TargetPath).Replace("//", "/");
            }
            switch (args.Action)
            {
                case "read":
                    return Json(this.ToCamelCase(this.operation.Read(args.Path, args.Data)));
                case "delete":
                    return this.ToCamelCase(this.operation.Delete(args.Path, args.Names, args.Data));
                case "details":
                    return this.ToCamelCase(this.operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    return this.ToCamelCase(this.operation.Create(args.Path, args.Name));
                case "search":
                    return this.ToCamelCase(this.operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    return this.ToCamelCase(this.operation.Rename(args.Path, args.Name, args.NewName, false, args.Data));
                case "copy":
                    return this.ToCamelCase(this.operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "move":
                    return this.ToCamelCase(this.operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles,args.TargetData, args.Data));

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

        [Route("AzureUpload")]
        public ActionResult AzureUpload(FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                var originalPath = "https://ej2filemanager.blob.core.windows.net/files/Files/";
                string startPath = "https://ej2filemanager.blob.core.windows.net/files/";
                originalPath = originalPath.Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
            }
            operation.Upload(args.Path, args.UploadFiles,args.Action, args.Data);
            return Json("");
        }

        [Route("AzureDownload")]
        public object AzureDownload(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return operation.Download(args.Path, args.Names, args.Data);
        }


        [Route("AzureGetImage")]
        public IActionResult AzureGetImage(FileManagerDirectoryContent args)
        {

            return this.operation.GetImage(args.Path, args.Id, true, null, args.Data);
        }
    }

}
