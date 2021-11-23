using System;
using System.IO;
using System.Threading.Tasks;
using Kugar.Core.BaseStruct;
using Newtonsoft.Json.Linq;

namespace Kugar.Storage.JDOSS
{
    public class JDOSSProvider:IOSSStorage
    {
        public Task<ResultReturn<string>> StorageFileAsync(string path, byte[] data, bool isAutoOverwrite = true)
        {
            throw new NotImplementedException();
        }

        public Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream, bool isAutoOverwrite = true)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Exists(string path)
        {
            throw new NotImplementedException();
        }

        public string Name { get; set; }
        public Task<ResultReturn<OSSBucketInfo>> GetFilesInfo(int queryCount = 100, string lastMarker = "", string objectPrefixKeyword = "")
        {
            throw new NotImplementedException();
        }

        public Task<ResultReturn<JObject>> GetClientUploadTemplateTicket(string allowPrefixOrFileName)
        {
            throw new NotImplementedException();
        }

        public Task<ResultReturn<Stream>> DownloadFile(string path)
        {
            throw new NotImplementedException();
        }

        public Task<ResultReturn<Stream>> ReadFileAsync(string path)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetAbsoluteFilePath(string relativePath)
        {
            throw new NotImplementedException();
        }
    }

    public class JDOSSOption
    {
        private string _serviceUrl;

        public string ServiceUrl
        {
            set
            {
                if (value.StartsWith("http",StringComparison.CurrentCultureIgnoreCase))
                {
                    value = $"http://{value}";
                }

                _serviceUrl = value;
            }
            get => _serviceUrl;
        }

        public string SignatureVersion { set; get; }="4";

        public bool UseHttp { set; get; } = true;

        public string BucketName { set; get; }

        public string FilePath { set; get; }
        
        public string AccessKeyID { set; get; }

        public string AccessKeySecret { set; get; }
    }
}
