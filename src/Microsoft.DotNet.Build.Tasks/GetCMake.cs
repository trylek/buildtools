// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks
{
    public class GetCMake : Microsoft.Build.Utilities.Task
    {
        public string CMakeVersion { get; set; }

        public string CMakeBinaryFolder { get; set; }

        public string Architecture { get; set; }

        [Output]
        public string CMakePath { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Starting GetCMake");
            CMakePath = string.Empty;

            if (string.IsNullOrWhiteSpace(CMakeVersion))
            {
                // If no version was specified then, set it to known good version.
                // Consider getting version number from a prerequisites file.
                CMakeVersion = "3.7";
            }

            // TODO: If the specified version of CMake already exists then, return the path here.

            if (string.IsNullOrWhiteSpace(CMakeBinaryFolder))
            {
                // Binary will be downloaded to CMake folder under temp directory.
                CMakeBinaryFolder = Path.Combine(Path.GetTempPath(), "CMake");
            }

            if (Directory.Exists(CMakeBinaryFolder))
            {
                Directory.Delete(CMakeBinaryFolder, true);
            }
            Directory.CreateDirectory(CMakeBinaryFolder);

            string compressedBinaryFilename = string.Format("cmake-{0}.1-", CMakeVersion);

            // TODO: Take Architecture as a parameter from MSBuild.
            if (string.Equals(System.Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"), "AMD64", StringComparison.OrdinalIgnoreCase))
            {
                compressedBinaryFilename += "win64-x64.zip";
            }
            else
            {
                compressedBinaryFilename += "win32-x86.zip";
            }

            string downloadUrl = string.Format("https://cmake.org/files/v{0}/{1}", CMakeVersion, compressedBinaryFilename);
            string compressedBinaryPath = Path.Combine(CMakeBinaryFolder, compressedBinaryFilename);
            Log.LogMessage(MessageImportance.Low, $"Attempting to download CMake binary {CMakeVersion} from {downloadUrl} to {compressedBinaryPath}.");
            
            if (DownloadCMake(downloadUrl, compressedBinaryPath).Result)
            {
                Log.LogMessage(MessageImportance.Low, $"Attempting to extract {compressedBinaryPath} to {CMakeBinaryFolder}.");
                ZipFile.ExtractToDirectory(compressedBinaryPath, CMakeBinaryFolder);

                CMakePath = Path.Combine(CMakeBinaryFolder, Path.GetFileNameWithoutExtension(compressedBinaryPath), @"bin\cmake.exe");
            }

            if (File.Exists(compressedBinaryPath))
            {
                File.Delete(compressedBinaryPath);
            }

            return !Log.HasLoggedErrors;
        }

        private async System.Threading.Tasks.Task<bool> DownloadCMake(string downloadUrl, string compressedBinaryPath)
        {
            bool isDownloadSuccessful = true;
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                    {
                        using (
                        Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                        stream = new FileStream(compressedBinaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                        {
                            await contentStream.CopyToAsync(stream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError("Failed to download CMake with error:\n" + e.Message);
                isDownloadSuccessful = false;
            }
            return isDownloadSuccessful;
        }
    }
}
