using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.Update
{
    public interface IUpdatePackageProvider
    {
        UpdatePackage GetLatestUpdate(string branch, Version currentVersion);
        List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null);
    }

    /// <summary>
    /// Where updates come from. Upstream asks a service that decides what each install should have; this
    /// fork has no such service and does not want one, so it reads its own GitHub releases directly. The
    /// releases already carry everything the update needs - the version in the tag, the notes in the body,
    /// one asset per runtime, and a digest to check the download against - and the artefacts are named the
    /// way upstream names them, so nothing downstream of here changes.
    /// </summary>
    public class UpdatePackageProvider : IUpdatePackageProvider
    {
        private const string ReleasesUrl = "https://api.github.com/repos/totza2010/Sonarr/releases?per_page=30";

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public UpdatePackageProvider(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public UpdatePackage GetLatestUpdate(string branch, Version currentVersion)
        {
            return GetPackages(branch).FirstOrDefault(p => p.Version > currentVersion);
        }

        public List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null)
        {
            // Everything since the version being replaced, so the modal that opens after an update can say
            // what the jump contained rather than only what the last release did.
            var oldest = previousVersion != null && previousVersion < currentVersion ? previousVersion : currentVersion;

            return GetPackages(branch).Where(p => p.Version >= oldest).ToList();
        }

        private List<UpdatePackage> GetPackages(string branch)
        {
            List<GitHubReleaseResource> releases;

            try
            {
                var request = new HttpRequestBuilder(ReleasesUrl).Build();

                // GitHub refuses anonymous calls without one, and pins the response shape to a version.
                request.Headers.Add("Accept", "application/vnd.github+json");

                releases = _httpClient.Get<List<GitHubReleaseResource>>(request).Resource;
            }
            catch (Exception ex)
            {
                // Being unable to look is not the same as there being nothing: an install that cannot reach
                // GitHub, or that has run into its rate limit, should carry on quietly rather than report a
                // failure the person can do nothing about.
                _logger.Debug(ex, "Unable to read releases from GitHub");

                return new List<UpdatePackage>();
            }

            return releases.Select(r => UpdatePackageMapper.Map(r, branch))
                           .Where(p => p != null)
                           .OrderByDescending(p => p.Version)
                           .ToList();
        }
    }
}
