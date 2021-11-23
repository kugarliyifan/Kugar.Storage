using System;
using System.IO;
using System.Threading.Tasks;
using Kugar.Core.BaseStruct;

namespace Kugar.Storage
{
    public class LocalStorage: StorageBase
    {
        private string _baseFolder = string.Empty;

        public LocalStorage() : this(string.Empty)
        {
            _baseFolder = Path.Join(Directory.GetCurrentDirectory(), "/uploads");
        }

        public LocalStorage(string baseFolder)
        {

            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                throw new ArgumentOutOfRangeException(nameof(baseFolder));
            }

            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
            }

            _baseFolder = baseFolder;
        }

        public override async Task<ResultReturn<string>> StorageFileAsync(string path, Stream stream, bool isAutoOverwrite = true)
        {
            var realPath = getRealFilePath(path);

            if (File.Exists(realPath))
            {
                if (isAutoOverwrite)
                {
                    File.Delete(realPath);
                }
                else
                {
                    return new FailResultReturn<string>("文件已存在");
                }
            }

            var folder = Path.GetDirectoryName(realPath);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var position = -1l;

            if (stream.CanSeek)
            {
                position = stream.Position;
            }


            using(var fileStream=File.Create(realPath))
            {
                await stream.CopyToAsync(fileStream);

                await fileStream.FlushAsync();
            }

            if (stream.CanSeek)
            {
                stream.Position = position;
            }

            return new SuccessResultReturn<string>(path);
        }

        public override Task<bool> Exists(string path)
        {
            var realPath = getRealFilePath(path);

            return Task.FromResult(File.Exists(realPath));
        }

        public override async Task<ResultReturn<Stream>> ReadFileAsync(string path)
        {
            var realPath=getRealFilePath(path);

            if (File.Exists(realPath))
            {
                try
                {
                    var stream = File.OpenRead(realPath);

                    return new SuccessResultReturn<Stream>(stream);
                }
                catch (Exception e)
                {
                    return new FailResultReturn<Stream>(e);
                }
            }
            else
            {
                return new FailResultReturn<Stream>("文件不存在");
            }
        }

        private string getRealFilePath(string path)
        {
            var realPath = path;

            if (!Path.IsPathRooted(path) || path[0] == '/')
            {
                realPath = Path.Join(_baseFolder, path);
            }

            return realPath;
        }

        public override async Task<string> GetAbsoluteFilePath(string relativePath)
        {
            if (await Exists(relativePath))
            {
                return Path.Join(_baseFolder, relativePath);
            }
            else
            {
                return null;
            }
        }
    }
}
