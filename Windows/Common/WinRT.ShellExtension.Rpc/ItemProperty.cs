namespace CommonShellExtensionRpc
{
    public sealed class ItemProperty
    {
        public ItemProperty(int id, string value, string iconResource)
        {
            Id = id;
            Value = value;
            IconResource = iconResource;
        }

        public string IconResource { get; set; }

        public int Id { get; set; }

        public string Value { get; set; }
    }
}
