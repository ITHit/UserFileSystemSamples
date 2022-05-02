namespace CommonShellExtensionRpc
{
    public sealed class StorageProviderGetContentInfoForPathResult
    {
        public StorageProviderGetContentInfoForPathResult(string contentId, string contentUri, int status)
        {
            ContentId = contentId;
            ContentUri = contentUri;
            Status = status;
        }

        public string ContentId { get; set; }

        public string ContentUri { get; set; }

        public int Status { get; set; }
    }
}
