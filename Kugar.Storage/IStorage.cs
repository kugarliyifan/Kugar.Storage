using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kugar.Core.BaseStruct;
using Kugar.Core.ExtMethod;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

namespace Kugar.Storage
{
    public interface IStorage
    {
        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="isAutoOverwrite"></param>
        /// <returns></returns>
        Task<ResultReturn<string>> StorageFileAsync(string path, byte[] data, bool isAutoOverwrite = true);

        /// <summary>
        /// 保存一个文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="stream"></param>
        /// <param name="isAutoOverwrite"></param>
        /// <returns></returns>
        Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream, bool isAutoOverwrite = true);

        /// <summary>
        /// 保存一个文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="stream"></param>
        /// <param name="isAutoOverwrite"></param>
        /// <returns></returns>
        ResultReturn<string> StorageFile(string path, Stream stream, bool isAutoOverwrite = true);

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="data"></param>
        /// <param name="isAutoOverwrite"></param>
        /// <returns></returns>
        ResultReturn<string> StorageFile(string path, byte[] data, bool isAutoOverwrite = true);

        /// <summary>
        /// 读取文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<ResultReturn<Stream>> ReadFileAsync(string path);

        ResultReturn<Stream> ReadFile(string path);

        Task<ResultReturn<byte[]>> ReadFileBytesAsync(string path);

        ResultReturn<byte[]> ReadFileBytes(string path);

        /// <summary>
        /// 获取文件绝对路径地址,如果是本地的Storage则为本地地址,如果是OSS的Storage则为web地址
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        Task<string> GetAbsoluteFilePathAsync(string relativePath);

        /// <summary>
        /// 获取文件绝对路径地址,如果是本地的Storage则为本地地址,如果是OSS的Storage则为web地址
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        string GetAbsoluteFilePath(string relativePath);

        /// <summary>
        /// 判断文件是否存在
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        Task<bool> ExistsAsync(string path);

        /// <summary>
        /// 判断文件是否存在
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        bool Exists(string path);

        string Name { set; get; }
    }

    public interface IOSSStorage:IStorage
    {
        Task<ResultReturn<OSSBucketInfo>> GetFilesInfo(int queryCount = 100, string lastMarker = "", string objectPrefixKeyword = "");

        Task<ResultReturn<JObject>> GetClientUploadTemplateTicket(string allowPrefixOrFileName);

        //Task<ResultReturn<Stream>> DownloadFile(string path);
    }

    public class OSSBucketInfo
    {
        public (string key, long size)[] Files { set; get; }

        public string[] Dirs { set; get; }

        public string NextMarker { set; get; }
    }

    public interface IOSSManager  
    {
        Task<ResultReturn> CreateBucket(string name);

        Task<IOSSStorage> GetBucket(string bucketName, bool autoCreateBucket = true);
    }

    /// <summary>
    /// Storage的基础类
    /// </summary>
    public abstract class StorageBase:IStorage
    {
        public virtual async Task<ResultReturn<string>> StorageFileAsync(string path, byte[] data, bool isAutoOverwrite = true)
        {
            using (var ds = new ByteStream(data))
            {
                return await StorageFileAsync(path, ds, isAutoOverwrite);
            }
        }

        public abstract Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream,
            bool isAutoOverwrite = true);

        public abstract ResultReturn<string> StorageFile(string path, Stream stream, bool isAutoOverwrite = true);

        public ResultReturn<string> StorageFile(string path, byte[] data, bool isAutoOverwrite = true)
        {
            using (var ds = new ByteStream(data))
            {
                return StorageFile(path, ds, isAutoOverwrite);
            }
        }

        public abstract Task<bool> ExistsAsync(string path);
         
        public abstract  bool Exists(string path);

        public abstract Task<ResultReturn<Stream>> ReadFileAsync(string path);

        public abstract ResultReturn<Stream> ReadFile(string path);

        public async Task<ResultReturn<byte[]>> ReadFileBytesAsync(string path)
        {
            var ret=await ReadFileAsync(path);

            if (ret.IsSuccess)
            {
                using (var stream=ret.ReturnData)
                {
                    var data= await stream.ReadAllBytesAsync();

                    return new SuccessResultReturn<byte[]>(data);
                }
            }

            return ret.Cast((byte[])null);
        }

        public ResultReturn<byte[]> ReadFileBytes(string path)
        {
            var ret = ReadFile(path);

            if (ret.IsSuccess)
            {
                using (var stream = ret.ReturnData)
                {
                    var data = stream.ReadAllBytes();

                    return new SuccessResultReturn<byte[]>(data);
                }
            }

            return ret.Cast((byte[])null);
        }

        public abstract Task<string> GetAbsoluteFilePathAsync(string relativePath);

        public abstract string GetAbsoluteFilePath(string relativePath);


        public virtual string Name { get; set; }
    }

    /// <summary>
    /// 同时上传多个Storage数据,需要全部成功,才会返回成功标志
    /// </summary>
    public class LinkedStorage:IStorage
    {
        private readonly List<IStorage> _storages=new List<IStorage>();
        private int _returnUrlIndex = -1;

        public async Task<ResultReturn<string>> StorageFileAsync(string path, byte[] data, bool isAutoOverwrite = true)
        {
            var result = await Task.WhenAll(_storages.Select(x => x.StorageFileAsync(path, data, isAutoOverwrite)));

            if (!result.All(x=>x.IsSuccess))
            {
                return new FailResultReturn<string>("上传失败");
            }

            if (_returnUrlIndex > 0)
            {
                return result[_returnUrlIndex];
            }
            else
            {
                return result[0];
            }
        }

        public async Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream, bool isAutoOverwrite = true)
        {
            var result = await Task.WhenAll(_storages.Select(x => x.StorageFileAsync(path, stream, isAutoOverwrite)));

            if (!result.All(x => x.IsSuccess))
            {
                return new FailResultReturn<string>("上传失败");
            }

            if (_returnUrlIndex>0)
            {
                return result[_returnUrlIndex];
            }
            else
            {
                return result[0];
            }
        }

        public ResultReturn<string> StorageFile(string path, Stream stream, bool isAutoOverwrite = true)
        {
            var result = _storages.Select(x => x.StorageFile(path, stream, isAutoOverwrite)).ToArrayEx();

            if (!result.All(x => x.IsSuccess))
            {
                return new FailResultReturn<string>("上传失败");
            }

            if (_returnUrlIndex > 0)
            {
                return result[_returnUrlIndex];
            }
            else
            {
                return result[0];
            }
        }

        public ResultReturn<string> StorageFile(string path, byte[] data, bool isAutoOverwrite = true)
        {
            var result = _storages.Select(x => x.StorageFile(path, data, isAutoOverwrite)).ToArrayEx();

            if (!result.All(x => x.IsSuccess))
            {
                return new FailResultReturn<string>("上传失败");
            }

            if (_returnUrlIndex > 0)
            {
                return result[_returnUrlIndex];
            }
            else
            {
                return result[0];
            }
        }

        public async Task<bool> ExistsAsync(string path)
        {
            var result = await Task.WhenAll(_storages.Select(x => x.ExistsAsync(path)));

            if (!result.All(x => x))
            {
                return false;
            }

            return true;
        }
         
        public bool Exists(string path)
        {
            var result =_storages.Select(x => x.Exists(path)).ToArrayEx();

            if (!result.All(x => x))
            {
                return false;
            }

            return true;
        }

        public IStorage this[string name]
        {
            get
            {
                return _storages.FirstOrDefault(x => x.Name == name);
            }
        }

        public string Name { get; set; } = "LinkedStorage";

        /// <summary>
        /// 添加一个Storage到列表中
        /// </summary>
        /// <param name="storage"></param>
        /// <param name="isReturnThisPath">如果所有都成功,是否单独返回这个Storage返回的值,如果所有Storage都不设置为true,则返回第一个Storage的返回值</param>
        /// <returns></returns>
        public LinkedStorage AddStorage(IStorage storage,bool isReturnThisPath=false)
        {
            _storages.Add(storage);

            if (isReturnThisPath)
            {
                _returnUrlIndex = _storages.IndexOf(storage);
            }

            return this;
        }

        /// <summary>
        /// 将所有storage注入到容器中
        /// </summary>
        /// <param name="collection"></param>
        public void InjectToService(IServiceCollection collection)
        {
            foreach (var storage in _storages)
            {
                collection.AddSingleton(storage.GetType(), storage);
            }
        }

        public async Task<ResultReturn<Stream>> ReadFileAsync(string path)
        {
            foreach(var storage in _storages)
            {
                if (await storage.ExistsAsync(path))
                {
                    return await storage.ReadFileAsync(path);
                }
            }

            return new FailResultReturn<Stream>("文件不存在");
        }

        public ResultReturn<Stream> ReadFile(string path)
        {
            foreach (var storage in _storages)
            {
                if (storage.Exists(path))
                {
                    return storage.ReadFile(path);
                }
            }

            return new FailResultReturn<Stream>("文件不存在");
        }

        public async Task<ResultReturn<byte[]>> ReadFileBytesAsync(string path)
        {
            foreach (var storage in _storages)
            {
                if (await storage.ExistsAsync(path))
                {
                    return await storage.ReadFileBytesAsync(path);
                }
            }

            return new FailResultReturn<byte[]>("文件不存在");
        }

        public ResultReturn<byte[]> ReadFileBytes(string path)
        {
            foreach (var storage in _storages)
            {
                if (storage.Exists(path))
                {
                    return storage.ReadFileBytes(path);
                }
            }

            return new FailResultReturn<byte[]>("文件不存在");
        }

        public async Task<string> GetAbsoluteFilePathAsync(string relativePath)
        {
            foreach (var storage in _storages)
            {
                if (await storage.ExistsAsync(relativePath))
                {
                    return await storage.GetAbsoluteFilePathAsync(relativePath);
                }
            }

            throw new FileNotFoundException();
        }

        public string GetAbsoluteFilePath(string relativePath)
        {
            foreach (var storage in _storages)
            {
                if (storage.Exists(relativePath))
                {
                    return storage.GetAbsoluteFilePath(relativePath);
                }
            }

            throw new FileNotFoundException();
        }
    }

    public class StorageFactory
    {
        private readonly Dictionary<string,IStorage> _storages=new Dictionary<string, IStorage>(StringComparer.CurrentCultureIgnoreCase);

        public StorageFactory()
        {

        }

        public IStorage this[string name]
        {
            get
            {
                return _storages[name];
            }
        }

        public StorageFactory AddStorage(string name, IStorage storage)
        {
            _storages.Add(name,storage);

            return this;
        }


    }

}
