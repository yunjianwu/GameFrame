using System;

namespace Starter.Runtime.Startup
{
    [Serializable]
    public sealed class VersionManifest
    {
        public int internalResourceVersion;
        public string updatePrefixUri;
        public int versionListLength;
        public int versionListHashCode;
        public int versionListCompressedLength;
        public int versionListCompressedHashCode;

        public void Validate()
        {
            if (internalResourceVersion < 1 || versionListLength < 1 || versionListCompressedLength < 1)
                throw new ArgumentException("Manifest resource version and version-list lengths must be positive.");
            if (!IsHttpUrl(updatePrefixUri, out Uri prefix) || !string.IsNullOrEmpty(prefix.Query) || !string.IsNullOrEmpty(prefix.Fragment))
                throw new ArgumentException("Manifest updatePrefixUri must be an absolute HTTP(S) directory URL without query or fragment.");
            // GF CRC32 values use signed int fields; negative hash codes are valid.
        }

        public static bool IsHttpUrl(string value, out Uri uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
                string.IsNullOrEmpty(uri.UserInfo);
        }
    }
}
