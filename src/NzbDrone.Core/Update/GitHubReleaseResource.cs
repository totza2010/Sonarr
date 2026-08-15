using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.Update
{
    /// <summary>
    /// What the GitHub releases API returns. This fork publishes its own releases and has no update
    /// service behind it, so the releases are the source: they carry the version in the tag, the notes
    /// in the body, and one asset per runtime with a digest to check the download against.
    /// </summary>
    public class GitHubReleaseResource
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonProperty("prerelease")]
        public bool Prerelease { get; set; }

        [JsonProperty("draft")]
        public bool Draft { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("assets")]
        public List<GitHubReleaseAssetResource> Assets { get; set; }
    }

    public class GitHubReleaseAssetResource
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }

        /// <summary>
        /// Prefixed with the algorithm, as in "sha256:abc...". Absent on releases published before
        /// GitHub started recording it, which is why nothing here insists on having it.
        /// </summary>
        [JsonProperty("digest")]
        public string Digest { get; set; }
    }
}
