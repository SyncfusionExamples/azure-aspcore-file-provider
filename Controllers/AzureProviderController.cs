using System;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.EJ2.FileManager.Base;
using Syncfusion.EJ2.FileManager.AzureFileProvider;
using System.Collections.Generic;
using System.Text.Json;

namespace EJ2AzureASPCoreFileProvider.Controllers
{
    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class AzureProviderController : Controller
    {
        public AzureFileProvider operation;
        public string blobPath { get; set; }
        public string filePath { get; set; }
        public AzureProviderController(IWebHostEnvironment hostingEnvironment)
        {
            this.operation = new AzureFileProvider();
            blobPath = "<--blobPath-->";
            filePath = "<--filePath-->";
            blobPath = blobPath.Replace("../", "");
            filePath = filePath.Replace("../", "");
            blobPath = (blobPath.Substring(blobPath.Length - 1) != "/") ? blobPath + "/" : blobPath.TrimEnd(new[] { '/', '\\' }) + "/";
            filePath = (filePath.Substring(filePath.Length - 1) == "/") ? filePath.TrimEnd(new[] { '/', '\\' }) : filePath;
            this.operation.SetBlobContainer(blobPath, filePath);            
            this.operation.RegisterAzure("<--accountName-->", "<--accountKey-->", "<--blobName-->");
            //----------
            //For example 
            //this.operation.setBlobContainer("https://azure_service_account.blob.core.windows.net/files/", "https://azure_service_account.blob.core.windows.net/files/Files");
            //this.operation.RegisterAzure("azure_service_account", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", "files");
            //---------
        }
        [HttpPost("AzureFileOperations")]
        public object AzureFileOperations([FromBody] FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                string startPath = blobPath;
                string originalPath = (filePath).Replace(startPath, "");
                //-----------------
                //For example
                //string startPath = "https://azure_service_account.blob.core.windows.net/files/";
                //string originalPath = ("https://azure_service_account.blob.core.windows.net/files/Files").Replace(startPath, "");
                //-------------------
                args.Path = !args.Path.Contains(originalPath) ? (originalPath + args.Path).Replace("//", "/") : (args.Path).Replace("//", "/");
                args.TargetPath = (originalPath + args.TargetPath).Replace("//", "/");
            }
            switch (args.Action)
            {
                case "read":
                    // Reads the file(s) or folder(s) from the given path.
                    return this.operation.ToCamelCase(this.operation.GetFiles(args.Path, args.ShowHiddenItems, args.Data));
                case "delete":
                    // Deletes the selected file(s) or folder(s) from the given path.
                    return this.operation.ToCamelCase(this.operation.Delete(args.Path, args.Names, args.Data));
                case "details":
                    // Gets the details of the selected file(s) or folder(s).
                    return this.operation.ToCamelCase(this.operation.Details(args.Path, args.Names, args.Data));
                case "create":
                    // Creates a new folder in a given path.
                    return this.operation.ToCamelCase(this.operation.Create(args.Path, args.Name, args.Data));
                case "search":
                    // Gets the list of file(s) or folder(s) from a given path based on the searched key string.
                    return this.operation.ToCamelCase(this.operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data));
                case "rename":
                    // Renames a file or folder.
                    return this.operation.ToCamelCase(this.operation.Rename(args.Path, args.Name, args.NewName, false, args.ShowFileExtension, args.Data));
                case "copy":
                    // Copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.operation.ToCamelCase(this.operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
                case "move":
                    // Cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                    return this.operation.ToCamelCase(this.operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data));
            }
            return null;
        }

        // Uploads the file(s) into a specified path
        [HttpPost("AzureUpload")]
        public ActionResult AzureUpload(FileManagerDirectoryContent args)
        {
            if (args.Path != "")
            {
                string startPath = blobPath;
                string originalPath = (filePath).Replace(startPath, "");
                args.Path = (originalPath + args.Path).Replace("//", "/");
                //----------------------
                //For example
                //string startPath = "https://azure_service_account.blob.core.windows.net/files/";
                //string originalPath = ("https://azure_service_account.blob.core.windows.net/files/Files").Replace(startPath, "");
                //args.Path = (originalPath + args.Path).Replace("//", "/");
                //----------------------
            }
            int chunkIndex = Convert.ToInt32(HttpContext.Request.Form["chunk-index"]);
            int totalChunk = Convert.ToInt32(HttpContext.Request.Form["total-chunk"]);
            FileManagerResponse uploadResponse = operation.Upload(args.Path, args.UploadFiles, args.Action, chunkIndex, totalChunk, args.Data);
            if (uploadResponse.Error != null)
            {
                Response.Clear();
                Response.ContentType = "application/json; charset=utf-8";
                Response.StatusCode = Convert.ToInt32(uploadResponse.Error.Code);
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = uploadResponse.Error.Message;
            }
            return Json("");
        }

        // Downloads the selected file(s) and folder(s)
        [HttpPost("AzureDownload")]
        public object AzureDownload(string downloadInput)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            FileManagerDirectoryContent args = JsonSerializer.Deserialize<FileManagerDirectoryContent>(downloadInput, options);
            return operation.Download(args.Path, args.Names, args.Data);
        }

        // Gets the image(s) from the given path
        [HttpGet("AzureGetImage")]
        public IActionResult AzureGetImage(FileManagerDirectoryContent args)
        {
            return this.operation.GetImage(args.Path, args.Id, true, null, args.Data);
        }
    }

}
