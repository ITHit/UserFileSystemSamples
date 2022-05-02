namespace CommonShellExtensionRpc
{
    public sealed class StorageProviderGetPathForContentUriResult
    {
        public StorageProviderGetPathForContentUriResult(string path, int status)
        {
            Path = path;
            Status = status;
        }

        public string Path { get; set; }

        public int Status { get; set; }
    }
}
