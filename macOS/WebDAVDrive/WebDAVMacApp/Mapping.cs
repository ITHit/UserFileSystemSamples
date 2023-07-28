using System;
using WebDAVCommon;

namespace WebDAVMacApp
{
    internal static class Mapping
    {
        /// <summary>
        /// Returns a full remote URI with domain that corresponds to the <paramref name="relativePath"/>.
        /// </summary>
        /// <param name="relativePath">Remote storage URI.</param>
        /// <returns>Full remote URI with domain that corresponds to the <paramref name="relativePath"/>.</returns>
        public static string GetAbsoluteUri(string relativePath)
        {
            Uri webDavServerUri = new Uri(AppGroupSettings.GetWebDAVServerUrl());
            string host = webDavServerUri.GetLeftPart(UriPartial.Authority);

            string path = $"{host}/{relativePath}";
            return path;
        }
    }
}
