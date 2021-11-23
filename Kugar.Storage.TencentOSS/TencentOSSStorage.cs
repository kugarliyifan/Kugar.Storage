using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using COSSTS;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Bucket;
using COSXML.Model.Object;
using COSXML.Model.Tag;
using COSXML.Network;
using COSXML.Transfer;
using COSXML.Utils;
using Kugar.Core.BaseStruct;
using Kugar.Core.Configuration;
using Kugar.Core.ExtMethod;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kugar.Storage
{
    public class TencentOSSStorage : StorageBase,IOSSStorage
    {
        private string _appID;
        private string _region;
        private string _secretId;
        private string _secretKey;
        private string _bucket;
        private bool _isReturnFullUrl=false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appID">腾讯云AppID</param>
        /// <param name="region">所在区域名称</param>
        /// <param name="secretId">云 API 密钥 SecretId</param>
        /// <param name="secretKey">云 API 密钥 SecretKey</param>
        /// <param name="isReturnFullUrl"></param>
        public TencentOSSStorage(string appID, string region,string bucket, string secretId, string secretKey,bool isReturnFullUrl=false)
        {
            _appID = appID;
            _region = region;
            _secretId = secretId;
            _secretKey = secretKey;
            _bucket = bucket;
            _isReturnFullUrl = isReturnFullUrl;
        }

        public override async Task<ResultReturn<string>> StorageFileAsync(string path, byte[] data, bool isAutoOverwrite = true)
        {
            CosXmlServer cosXml = createServer();

            try
            {
                var request = new PutObjectRequest(_bucket, path, data);
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                //执行请求
                var result = await cosXml.executeAsync<PutObjectResult>(request); //.PutObject(request);

                if (_isReturnFullUrl)
                {
                    return new SuccessResultReturn<string>(result.uploadResult.processResults.results[0].Location);
                }
                else
                {
                    return new SuccessResultReturn<string>(path);
                }

            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                return new FailResultReturn<string>(clientEx);

                //请求失败
                Console.WriteLine("CosClientException: " + clientEx);
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                return new FailResultReturn<string>(serverEx);
                //请求失败
                Console.WriteLine("CosServerException: " + serverEx.GetInfo());
            }
            catch (Exception ex)
            {
                return new FailResultReturn<string>(ex);
            }
            catch
            {
                return new FailResultReturn<string>("上传失败");
            }
        }

        public override async Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream, bool isAutoOverwrite = true)
        {
            var data = await stream.ReadAllBytesAsync();

            return await StorageFileAsync(path, data, isAutoOverwrite);
        }

        public override async Task<bool> Exists(string path)
        {
            CosXmlServer cosXml = createServer();

            try
            {
                HeadObjectRequest request = new HeadObjectRequest(_bucket, path);
                
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                //执行请求
                var result = await cosXml.executeAsync<HeadObjectResult>(request); //.PutObject(request);

                if (!string.IsNullOrEmpty(result.GetResultInfo()))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        

        public async Task<ResultReturn<OSSBucketInfo>> GetFilesInfo( int queryCount=100, string lastMarker="", string objectPrefixKeyword="")
        {
            CosXmlServer cosXml = createServer();

            try
            {
                GetBucketRequest request = new GetBucketRequest($"{_bucket}-{_appID}"){};
       
                request.SetMarker(lastMarker);
                request.SetMaxKeys(queryCount.ToStringEx());
                request.SetPrefix(objectPrefixKeyword);


                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                //执行请求
                var result = await cosXml.executeAsync<GetBucketResult>(request); //.PutObject(request);

                //bucket的相关信息
                ListBucket info = result.listBucket;

                var files = info.contentsList.Select(x => (x.key, x.size)).ToArrayEx();
                var dirs = info.commonPrefixesList.Select(x => x.prefix).ToArrayEx();
                var nextMarker = info.isTruncated ? info.nextMarker : "";

                return new SuccessResultReturn<OSSBucketInfo>(new OSSBucketInfo()
                {
                    Files = files,
                    Dirs = dirs,
                    NextMarker = nextMarker
                });
            }
            catch (Exception ex)
            {
                return new FailResultReturn<OSSBucketInfo>(ex);
            }
        }

        public async Task<ResultReturn<JObject>> GetClientUploadTemplateTicket(string allowPrefixOrFileName)
        {
            if (allowPrefixOrFileName[0]=='/')
            {
                allowPrefixOrFileName = allowPrefixOrFileName.Substring(1);
            }

            //string bucket = "examplebucket-1253653367";  // 您的 bucket
            //string region = "ap-guangzhou";  // bucket 所在区域
            //string allowPrefix = "exampleobject"; // 这里改成允许的路径前缀，可以根据自己网站的用户登录态判断允许上传的具体路径，例子： a.jpg 或者 a/* 或者 * (使用通配符*存在重大安全风险, 请谨慎评估使用)
            string[] allowActions = new string[] {  // 允许的操作范围，这里以上传操作为例
                "name/cos:PutObject",
                "name/cos:PostObject",
                "name/cos:InitiateMultipartUpload",
                "name/cos:ListMultipartUploads",
                "name/cos:ListParts",
                "name/cos:UploadPart",
                "name/cos:CompleteMultipartUpload"
            };

            Dictionary<string, object> values = new Dictionary<string, object>();
            values.Add("bucket", _bucket);
            values.Add("region", _region);
            values.Add("allowPrefix", allowPrefixOrFileName);
            // 也可以通过 allowPrefixes 指定路径前缀的集合
            // values.Add("allowPrefixes", new string[] {
            //     "path/to/dir1/*",
            //     "path/to/dir2/*",
            // });
            values.Add("allowActions", allowActions);
            values.Add("durationSeconds", 1800);

            values.Add("secretId", _secretId);
            values.Add("secretKey", _secretKey);

            values.Add("Domain", "sts.tencentcloudapi.com");

            // Credentials = {
            //   "Token": "4oztDXOAAI3c6qUE5TkNuVzSP1tUQz15f3f946eb08f9411d3d61505cc4bc74cczCZLchkvRmmrqzE09ELVw35gzYlBXsQp03PBpL79ubLvoAMWbBgSMmI6eApmhqv7NFeDdKJlikVe0fNCU2NNUe7cHrgttfTIK87ZnC86kww-HysFgIGeBNWpwo4ih0lV0z9a2WiTIjPoeDBwPU4YeeAVQAGPnRgHALoL2FtxNsutFzDjuryRZDK7Am4Cs9YxpZHhG7_F_II6363liKNsHTk8ONRZrNxKiOqvFvyhsJ-oTTUg0I0FT4_xo0lq5zR9yyySXHbE7z-2im4rgnK3sBagN47zkgltJyefJmaPUdDgGmvaQBO6TqxiiszOsayS7CxCZK1yi90H2KS3xRUYTLf94aVaZlufrIwntXIXZaHOKHmwuZuXl7HnHoXbfg_YENoLP6JAkDCw0GOFEGNOrkCuxRtcdJ08hysrwBw1hmYawDHkbyxYkirY-Djg7PswiC4_juBvG0iwjzVwE0W_rhxIa7YtamLnZJxQk9dyzbbl0F4DTYwS101Hq9wC7jtifkXFjBFTGRnfPe85K-hEnJLaEy7eYfulIPI9QiIUxi4BLPbzjD9j3qJ4Wdt5oqk9XcF9y5Ii2uQx1eymNl7qCA",
            //   "TmpSecretId": "AKIDjjyp4_pAix_hgxv5WsXk72GGk8edXeWpH4NXerPA5ZLxJzr8Z_TGSnjXM42--qXI",
            //   "TmpSecretKey": "PZ/WWfPZFYqahPSs8URUVMc8IyJH+T24zdn8V1cZaMs="
            // }
            // ExpiredTime = 1597916602
            // Expiration = 2020/8/20 上午9:43:22
            // RequestId = 2b731be1-ebe8-4638-8a72-906bc564a55a
            // StartTime = 1597914802

            try
            {
                Dictionary<string, object> credential = STSClient.genCredential(values);
                
                var credentials = (Dictionary<string, object>)credential[""];

                return new SuccessResultReturn<JObject>( new JObject()
                {
                    ["Token"] = credentials["Token"].ToStringEx(),
                    ["SecretId"] = credentials["TmpSecretId"].ToStringEx(),
                    ["SecretKey"] = credentials["TmpSecretKey"].ToStringEx(),
                    ["RequestId"] = credential["RequestId"].ToStringEx(),
                    ["StartTime"] = credential["StartTime"].ToStringEx(),
                    ["ExpiredTime"] = credential["ExpiredTime"].ToStringEx(),
                    ["Bucket"] = _bucket,
                    ["Region"] = _region,
                    ["FileNameOrPrefix"] = allowPrefixOrFileName
                });
            }
            catch (Exception e)
            {
                return new FailResultReturn<JObject>(e);
            }
            

        }

        public async override Task<ResultReturn<Stream>> ReadFileAsync(string path)
        {
            CosXmlServer cosXml = createServer();
             
            try
            {
                var request = new GetObjectBytesRequest(_bucket, path);

                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                //执行请求
                var result = await cosXml.executeAsync<GetObjectBytesResult>(request); //.PutObject(request);


                return new SuccessResultReturn<Stream>(new ByteStream(result.content));

            }
            catch (Exception ex)
            {
                return new FailResultReturn<Stream>(ex);
            }
        }

        private CosXmlServer createServer()
        {
            CosXmlConfig config = new CosXmlConfig.Builder()
                .SetConnectionTimeoutMs(60000)  //设置连接超时时间，单位毫秒，默认45000ms
                .SetReadWriteTimeoutMs(40000)  //设置读写超时时间，单位毫秒，默认45000ms
                .IsHttps(true)  //设置默认 HTTPS 请求
                .SetAppid(_appID)  //设置腾讯云账户的账户标识 APPID
                .SetRegion(_region)  //设置一个默认的存储桶地域
                //.SetDebugLog(true)  //显示日志
                .Build();  //创建 CosXmlConfig 对象

            long durationSecond = 600;  //每次请求签名有效时长，单位为秒
            var cosCredentialProvider = new DefaultQCloudCredentialProvider(_secretId, _secretKey, durationSecond);

            CosXmlServer cosXml = new CosXmlServer(config, cosCredentialProvider);

            return cosXml;
        }

        public override Task<string> GetAbsoluteFilePath(string relativePath)
        {
            throw new NotImplementedException();
        }
    }

    public class TencentOSSManager : IOSSManager
    {
        private string _appID;
        private string _region;
        private string _secretId;
        private string _secretKey;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appID">腾讯云AppID</param>
        /// <param name="region">所在区域名称</param>
        /// <param name="secretId">云 API 密钥 SecretId</param>
        /// <param name="secretKey">云 API 密钥 SecretKey</param>
        /// <param name="isReturnFullUrl"></param>
        public TencentOSSManager(string appID, string region, string secretId, string secretKey)
        {
            _appID = appID;
            _region = region;
            _secretId = secretId;
            _secretKey = secretKey;
        }
        
        public async Task<ResultReturn> CreateBucket(string name)
        {
            var cosXml = createServer();

            try
            {
                PutBucketRequest request = new PutBucketRequest(name);

                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                //执行请求
                var result = await cosXml.executeAsync<PutBucketResult>(request); //.PutObject(request);


                return new ResultReturn(result.httpCode == 200, message: result.httpMessage); ;

            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                return new FailResultReturn<string>(clientEx);

            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                return new FailResultReturn<string>(serverEx);
            }
            catch (Exception ex)
            {
                return new FailResultReturn<string>(ex);
            }
            catch
            {
                return new FailResultReturn<string>("上传失败");
            }



        }

        public async Task<IOSSStorage> GetBucket(string bucketName,bool autoCreateBucket=true)
        {
            HeadBucketRequest request = new HeadBucketRequest(bucketName);
            //执行请求
            var result = await createServer().executeAsync<HeadBucketResult>(request);

            if (result.httpCode==404)
            {
                if (autoCreateBucket)
                {
                    await CreateBucket(bucketName);
                }
                else
                {
                    throw new EntryPointNotFoundException("存储桶不存在");
                }
            }
            else if (result.httpCode== 403)
            {
                throw new Exception("存储桶无访问权限"); 
            }

            return new TencentOSSStorage(_appID, _region, bucketName, _secretId, _secretKey);
        }

        private CosXmlServer createServer()
        {
            CosXmlConfig config = new CosXmlConfig.Builder()
                .SetConnectionTimeoutMs(60000)  //设置连接超时时间，单位毫秒，默认45000ms
                .SetReadWriteTimeoutMs(40000)  //设置读写超时时间，单位毫秒，默认45000ms
                .IsHttps(true)  //设置默认 HTTPS 请求
                .SetAppid(_appID)  //设置腾讯云账户的账户标识 APPID
                .SetRegion(_region)  //设置一个默认的存储桶地域
                //.SetDebugLog(true)  //显示日志
                .Build();  //创建 CosXmlConfig 对象

            long durationSecond = 600;  //每次请求签名有效时长，单位为秒
            var cosCredentialProvider = new DefaultQCloudCredentialProvider(_secretId, _secretKey, durationSecond);

            CosXmlServer cosXml = new CosXmlServer(config, cosCredentialProvider);

            return cosXml;
        }
    }
}
