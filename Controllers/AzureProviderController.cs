using System;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Syncfusion.EJ2.FileManager.Base;
using Syncfusion.EJ2.FileManager.AzureFileProvider;

namespace EJ2AzureASPCoreFileProvider.Controllers
{
    ///<Summary>
    /// Azure file provider controller
    ///</Summary>
    [Route("api/[controller]")]
    [EnableCors("AllowAllOrigins")]
    public class AzureProviderController : Controller
    {
        ///<Summary>
        /// Azure file provider object
        ///</Summary>
        public AzureFileProvider operation;
        ///<Summary>
        /// Azure blob path
        ///</Summary>
        public string blobPath { get; set; }
        ///<Summary>
        /// Azure file path
        ///</Summary>
        public string filePath { get; set; }

        ///<Summary>
        /// Azure file provider controller
        ///</Summary>
        public AzureProviderController()
        {
            operation = new AzureFileProvider();
            blobPath = "<--blobPath-->";
            filePath = "<--filePath-->";
            blobPath = (blobPath.Substring(blobPath.Length - 1) != "/") ? blobPath + "/" : blobPath.TrimEnd(new[] { '/', '\\' }) + "/";
            filePath = (filePath.Substring(filePath.Length - 1) == "/") ? filePath.TrimEnd(new[] { '/', '\\' }) : filePath;
            operation.SetBlobContainer(blobPath, filePath);
            operation.RegisterAzure("<--accountName-->", "<--accountKey-->", "<--blobName-->");
            //----------
            //For example
            //operation.setBlobContainer("https://azure_service_account.blob.core.windows.net/files/", "https://azure_service_account.blob.core.windows.net/files/Files");
            //operation.RegisterAzure("azure_service_account", "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", "files");
            //---------
        }

        ///<Summary>
        /// Azure file operations for service
        ///</Summary>
        [Route("AzureFileOperations")]
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
                args.Path = (originalPath + args.Path).Replace("//", "/");
                args.TargetPath = (originalPath + args.TargetPath).Replace("//", "/");
            }
            return args.Action switch {
                "read" => Json(ToCamelCase(operation.GetFiles(args.Path, args.Data))), // Reads the file(s) or folder(s) from the given path.
                "delete" => ToCamelCase(operation.Delete(args.Path, args.Names, args.Data)), // Deletes the selected file(s) or folder(s) from the given path.
                "details" => ToCamelCase(operation.Details(args.Path, args.Names, args.Data)), // Gets the details of the selected file(s) or folder(s).
                "create" => ToCamelCase(operation.Create(args.Path, args.Name, args.Data)), // Creates a new folder in a given path.
                "search" => ToCamelCase(operation.Search(args.Path, args.SearchString, args.ShowHiddenItems, args.CaseSensitive, args.Data)), // Gets the list of file(s) or folder(s) from a given path based on the searched key string.
                "rename" => ToCamelCase(operation.Rename(args.Path, args.Name, args.NewName, false, args.Data)), // Renames a file or folder.
                "copy" => ToCamelCase(operation.Copy(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data)), // Copies the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                "move" => ToCamelCase(operation.Move(args.Path, args.TargetPath, args.Names, args.RenameFiles, args.TargetData, args.Data)), // Cuts the selected file(s) or folder(s) from a path and then pastes them into a given target path.
                _ => null,
            };
        }

        ///<Summary>
        /// Convert to camel case
        ///</Summary>
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

        ///<Summary>
        /// Uploads the file(s) into a specified path
        ///</Summary>
        [Route("AzureUpload")]
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
            FileManagerResponse uploadResponse = operation.Upload(args.Path, args.UploadFiles, args.Action, args.Data);
            if (uploadResponse.Error != null)
            {
                Response.Clear();
                Response.ContentType = "application/json; charset=utf-8";
                Response.StatusCode = Convert.ToInt32(uploadResponse.Error.Code);
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = uploadResponse.Error.Message;
            }
            return Json("");
        }

        ///<Summary>
        /// Downloads the selected file(s) and folder(s)
        ///</Summary>
        [Route("AzureDownload")]
        public object AzureDownload(string downloadInput)
        {
            FileManagerDirectoryContent args = JsonConvert.DeserializeObject<FileManagerDirectoryContent>(downloadInput);
            return operation.Download(args.Path, args.Names, args.Data);
        }

        ///<Summary>
        /// Gets the image(s) from the given path
        ///</Summary>
        [Route("AzureGetImage")]
        public IActionResult AzureGetImage(FileManagerDirectoryContent args)
        {
            return operation.GetImage(args.Path, args.Id, true, null, args.Data);
        }
    }
}