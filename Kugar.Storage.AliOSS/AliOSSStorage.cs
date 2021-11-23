﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Aliyun.OSS;
using Kugar.Core.BaseStruct;
using Kugar.Core.ExtMethod;
using Newtonsoft.Json.Linq;

namespace Kugar.Storage.AliOSS
{
    public class AliOSSStorage : StorageBase,IOSSStorage
    {
        private string _endpoint;
        private string _accessKeyId;
        private string _accessKeySecret;
        private string _bucketName;

        public AliOSSStorage(string endpoint,string bucketName,string accessKeyId, string accessKeySecret)
        {
            _endpoint = endpoint;
            _accessKeyId = accessKeyId;
            _accessKeySecret = accessKeySecret;
            _bucketName = bucketName;
        }

        public override async Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream, bool isAutoOverwrite = true)
        {
            OssClient client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);

            try
            {
                using (var result = await Task.Factory.FromAsync(
                    (cb, o) => client.BeginPutObject(_bucketName, path, stream, cb, o), client.EndPutObject, null))
                {
                    if (result.HttpStatusCode == HttpStatusCode.OK)
                    {
                        return new SuccessResultReturn<string>(path);
                    }
                    else
                    {
                        return new FailResultReturn<string>("上传失败");
                    }
                }
            }
            catch (Exception e)
            {
                return new FailResultReturn<string>(e);
            }
            


        }

        public override async Task<bool> Exists(string path)
        {
            OssClient client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);

            try
            {

                return client.DoesObjectExist(_bucketName, path);

                //using (var result = await Task.Factory.FromAsync(
                //    (cb, o) => client.DoesObjectExist(_bucketName, path), client.EndPutObject, null))
                //{
                //    if (result.HttpStatusCode == HttpStatusCode.OK)
                //    {
                //        return new SuccessResultReturn<string>(path);
                //    }
                //    else
                //    {
                //        return new FailResultReturn<string>("上传失败");
                //    }
                //}
            }
            catch (Exception e)
            {
                return new FailResultReturn<string>(e);
            }

        }

        public async Task<ResultReturn<OSSBucketInfo>> GetFilesInfo(int queryCount = 100, string lastMarker = "", string objectPrefixKeyword = "")
        {
            OssClient client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);

            try
            {
                var lstre=new ListObjectsRequest(_bucketName);
                lstre.Marker = lastMarker;
                lstre.MaxKeys = queryCount;
                lstre.Prefix = objectPrefixKeyword;
        

                var result = await Task.Factory.FromAsync(
                    (cb, o) => client.BeginListObjects(lstre, cb, o), client.EndListObjects, null);

                if (result.HttpStatusCode == HttpStatusCode.OK)
                {
                    var files = result.ObjectSummaries.Select(x => (x.Key, x.Size)).ToArrayEx();
                    var dirs = result.CommonPrefixes.ToArrayEx();
                    
                    return new SuccessResultReturn<OSSBucketInfo>(new OSSBucketInfo()
                    {
                        Files = files,
                        Dirs = dirs,
                        NextMarker = result.NextMarker
                    });

                }
                else
                {
                    return new FailResultReturn<OSSBucketInfo>("上传失败");
                }
                
            }
            catch (Exception e)
            {
                return new FailResultReturn<OSSBucketInfo>(e);
            }
        }

        public async Task<ResultReturn<JObject>> GetClientUploadTemplateTicket(string allowPrefixOrFileName)
        {
            OssClient client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);

            try
            {
                var generatePresignedUriRequest = new GeneratePresignedUriRequest(_bucketName, allowPrefixOrFileName, SignHttpMethod.Put)
                {
                    Expiration = DateTime.Now.AddHours(1),
                };
                var signedUrl = client.GeneratePresignedUri(generatePresignedUriRequest);

                return new SuccessResultReturn<JObject>(new JObject()
                {
                    ["Url"]=signedUrl
                });
            }
            catch (Exception e)
            {

                return new FailResultReturn<JObject>(e);
            }
            
            return null;
        }

        public async Task<ResultReturn<Stream>> DownloadFile(string path)
        {
            OssClient client = new OssClient(_endpoint, _accessKeyId, _accessKeySecret);

            try
            {
                using (var result = await Task.Factory.FromAsync(
                    (cb, o) => client.BeginGetObject(_bucketName,path, cb, o), client.EndGetObject ,null))
                {
                    return new SuccessResultReturn<Stream>(result.Content);
                }

            }
            catch (Exception e)
            {
                
                return new FailResultReturn<Stream>(e);
            }
        }
    }
}
