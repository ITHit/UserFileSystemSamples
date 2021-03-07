using System;
using System.Threading.Tasks;
using ITHit.FileSystem;
using System.IO;

namespace VirtualFilesystemMacCommon
{
    public class FsEngine : Engine
    {
        private const string TestLicense = "<?xml version=\"1.0\" encoding=\"utf - 8\"?><License><Data><Product>IT Hit User File System</Product><LicensedTo><![CDATA[Apriorit]]></LicensedTo><Quantity>1</Quantity><IssueDate><![CDATA[Monday, February 22, 2021]]></IssueDate><ExpirationDate><![CDATA[Monday, March 22, 2021]]></ExpirationDate><SupportExpirationDate><![CDATA[]]></SupportExpirationDate><Type>Evaluation</Type><Modules></Modules><Id>22c3c7be-a10f-4eec-bb52-5a2b92b0f24b</Id><Plan></Plan></Data><Signature><![CDATA[iS7N6uAt/Ez0FQXG18vCb1nwrl6+NbVmaIyA5MOiVRPZ9h+f8wjI93Jc3oqfe+l6oTGnISIblSUUUNEJ3Fq2wcsDogAayKH9eVrRU52/0SbHLLkxQoGGWQZXvXvTg5MbnkOL9aC+swfiA46SlEmqvYFyW3dIlYDlO9pq4ckeWo4=]]></Signature></License>";
        private ConsoleLogger Logger; 

        public FsEngine(string localRootPath)
            : base (TestLicense, localRootPath)
        {
            Logger = new ConsoleLogger(GetType().Name);
        }

        public override Task<IFileSystemItem> GetFileSystemItemAsync(string path)
        {
            return null;
        }

        public IFileSystemItem GetFileSystemItem(string path)
        {
            try
            {
                if (File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    return new UserFolder(path);
                }
            }
            catch (FileNotFoundException exception)
            {
                Logger.LogError("Specified file not found: " + path + ". With error: " + exception.ToString());
                return null;
            }

            return new UserFile(path);
        }

        public override Task StartAsync()
        {
            Task<IFileSystemItem> task = new Task<IFileSystemItem>(() =>
            {
                return null;
            });

            return task;
        }

        protected override void Stop()
        {
        }
    }
}
