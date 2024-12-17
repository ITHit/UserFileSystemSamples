using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WebDAVDrive.Utils
{
    /// <summary>
    /// Represents the extracted protocol parameters including Item URLs, Mount URL, and Command.
    /// </summary>
    public class ProtocolParameters
    {
        /// <summary>
        /// Gets or sets the list of item URLs.
        /// </summary>
        public List<string> ItemUrls { get; set; } = new();

        /// <summary>
        /// Gets or sets the WebDAV mount URL.
        /// </summary>
        public required Uri MountUrl { get; set; }

        /// <summary>
        /// Gets or sets the command to be executed (e.g., lock, unlock, etc.).
        /// </summary>
        public CommandType Command { get; set; }

        /// <summary>
        /// Parses the given URI and extracts the Item URLs, Mount URL, and Command.
        /// </summary>
        /// <param name="uri">The input URI to parse.</param>
        /// <returns>An instance of <see cref="ProtocolParameters"/> containing parsed data.</returns>
        /// <exception cref="ArgumentException">Thrown when the provided URI is invalid.</exception>
        public static ProtocolParameters Parse(Uri uri)
        {
            if (uri == null || string.IsNullOrEmpty(uri.AbsoluteUri))
                throw new ArgumentException("Invalid URI");

            // Extract parameters from the URI.
            var parameters = ParseUriParameters(uri);

            return new ProtocolParameters
            {
                ItemUrls = GetItemUrls(parameters),
                MountUrl = new Uri(HttpUtility.UrlDecode(parameters["MountUrl"])),
                Command = parameters.ContainsKey("Command") && Enum.TryParse(HttpUtility.UrlDecode(parameters["Command"]), true, out CommandType command)
                    ? command
                    : CommandType.Open // Default if parsing fails
            };
        }

        /// <summary>
        /// Extracts key-value parameters from the provided URI.
        /// </summary>
        /// <param name="uri">The input URI containing parameters.</param>
        /// <returns>A dictionary of parameter names and values.</returns>
        private static Dictionary<string, string> ParseUriParameters(Uri uri)
        {
            // Extract the protocol prefix dynamically.
            int colonIndex = uri.AbsoluteUri.IndexOf(':');
            string uriWithoutProtocol = colonIndex >= 0
                ? uri.AbsoluteUri.Substring(colonIndex + 1)
                : uri.AbsoluteUri;

            // Split and parse key-value pairs.
            return uriWithoutProtocol
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .ToDictionary(
                    pair => pair[0],
                    pair => pair.Length > 1 ? pair[1] : string.Empty
                );
        }

        /// <summary>
        /// Extracts the list of item URLs from the parsed parameters.
        /// </summary>
        /// <param name="parameters">A dictionary of key-value pairs extracted from the URI.</param>
        /// <returns>A list of item URLs.</returns>
        private static List<string> GetItemUrls(Dictionary<string, string> parameters)
        {
            if (!parameters.ContainsKey("ItemUrl"))
                return new List<string>();

            string itemUrlRaw = HttpUtility.UrlDecode(parameters["ItemUrl"]);
            string trimmedUrls = itemUrlRaw.Trim('[', '\"', ']');
            return trimmedUrls.Split(new[] { "\",\"" }, StringSplitOptions.None).ToList();
        }
    }

    /// <summary>
    /// Represents the types of commands that can be executed.
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// Represents a lock command.
        /// </summary>
        Lock,

        /// <summary>
        /// Represents an unlock command.
        /// </summary>
        Unlock,

        /// <summary>
        /// Represents an open with command.
        /// </summary>
        OpenWith,

        /// <summary>
        /// Represents a print command.
        /// </summary>
        Print,

        /// <summary>
        /// Represents an open command.
        /// </summary>
        Open,

        /// <summary>
        /// Represents an edit command.
        /// </summary>
        Edit
    }
}
