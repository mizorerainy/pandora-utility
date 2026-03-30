using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using MizoreRainy.Pandora;
using MizoreRainy.Pandora.BuildUtility;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MizoreRainy.Pandora.CustomTasks
{
    [Serializable]
    public class ZipAndUploadToGoogleDrive : ManagedPostBuildTask
    {
        #region Fields & Properties
        
        [Tooltip("The ID of the target Google Drive Folder.")]
        [MizoreRainy.Pandora.Editor.Attributes.UrlButton("Open Google Drive", "https://drive.google.com/drive/folders/{0}")]
        public string GoogleDriveFolderId = "";

        [Tooltip("If true, automatically opens the Google Drive folder in your browser after a successful upload.")]
        public bool AutoOpenGoogleDriveFolder = true;

        [Tooltip("Absolute path to the Google Service Account JSON key file.")]
        [MizoreRainy.Pandora.Editor.Attributes.FileSelector("json")]
        public string ServiceAccountJsonPath = "";

        // Track global state for background upload
        private static bool _isUploading = false;
        private static CancellationTokenSource _uploadCancellationTokenSource;
        private static float _progressValue = 0f;
        private static string _progressMessage = "";

        #endregion

        #region Public API

        public override void Execute(ManagedBuildProfile profile, string buildOutputPath)
        {
            var buildRoot = Path.GetDirectoryName(buildOutputPath);
            PandoraLogger.LogBuild($"[ZipAndUpload] Starting Zip and Upload task for {profile.Name} at: {buildRoot}");

            if (string.IsNullOrEmpty(GoogleDriveFolderId))
            {
                PandoraLogger.LogBuildError("[ZipAndUpload] Error: GoogleDriveFolderId is empty. Skipping upload.");
                return;
            }

            if (!File.Exists(ServiceAccountJsonPath))
            {
                PandoraLogger.LogBuildError($"[ZipAndUpload] Error: Service Account JSON not found at {ServiceAccountJsonPath}. Skipping upload.");
                return;
            }

            string productName = profile.ProductNameOverride?.ToLower() ?? Application.productName.ToLower();
            string suffix = profile.BuildSuffix;
            string suffixPart = string.IsNullOrEmpty(suffix) ? "" : $"{suffix}-";
            
            string rawVersion = Application.version.TrimStart('v', 'V').Replace(".", "-");
            string versionStr = $"v{rawVersion}";
            
            string commitHash = GetGitCommitHash(Application.dataPath);
            
            string zipFileName = $"{productName}-{suffixPart}{versionStr}-{commitHash}.zip";
            string zipPath = Path.Combine(Directory.GetParent(buildRoot).FullName, zipFileName);
            
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                
                PandoraLogger.LogBuild($"[ZipAndUpload] Zipping {buildRoot} to {zipPath}...");
                ZipFile.CreateFromDirectory(buildRoot, zipPath);
                
                PandoraLogger.LogBuild("[ZipAndUpload] Zip completed. Scheduling Google Drive background upload...");

                // Cancel previous upload process if it's still running
                if (_isUploading && _uploadCancellationTokenSource != null)
                {
                    PandoraLogger.LogBuild("[ZipAndUpload] Cancelling previous upload task...");
                    _uploadCancellationTokenSource.Cancel();
                    _uploadCancellationTokenSource.Dispose();
                }

                _uploadCancellationTokenSource = new CancellationTokenSource();
                var token = _uploadCancellationTokenSource.Token;

                _isUploading = true;
                _progressValue = 0f;
                _progressMessage = "Authenticating with Google API...";

#if UNITY_EDITOR
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
#endif

                // Fire and forget so we don't stall the Unity Editor GUI
                Task.Run(async () =>
                {
                    try
                    {
                        await UploadToGoogleDriveAsync(zipPath, token);
                        _progressMessage = "Upload Complete!";
                        _progressValue = 1f;
                        PandoraLogger.LogBuild("[ZipAndUpload] Successfully uploaded to Google Drive!");

#if UNITY_EDITOR
                        if (AutoOpenGoogleDriveFolder && !string.IsNullOrEmpty(GoogleDriveFolderId))
                        {
                            var url = $"https://drive.google.com/drive/folders/{GoogleDriveFolderId}";
                            EditorApplication.delayCall += () => Application.OpenURL(url);
                        }
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        PandoraLogger.LogBuildWarning("[ZipAndUpload] Background upload was cancelled (e.g., new build triggered).");
                    }
                    catch (Exception ex)
                    {
                        PandoraLogger.LogBuildError($"[ZipAndUpload] Upload Failed: {ex.Message}");
                    }
                    finally
                    {
                        _isUploading = false;
                        if (File.Exists(zipPath))
                        {
                            File.Delete(zipPath);
                            PandoraLogger.LogBuild($"[ZipAndUpload] Cleaned up temporary zip file {zipPath}");
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                PandoraLogger.LogBuildError($"[ZipAndUpload] Zipping Failed: {ex.Message}");
            }
        }
        
        #endregion

#if UNITY_EDITOR
        #region Unity Lifecycle & Initialization

        private static void OnEditorUpdate()
        {
            if (_isUploading)
            {
                // This draws a popup window with progress and a Cancel button in the Unity Editor
                if (EditorUtility.DisplayCancelableProgressBar("Google Drive Upload", _progressMessage, _progressValue))
                {
                    if (_uploadCancellationTokenSource != null)
                    {
                        _uploadCancellationTokenSource.Cancel();
                    }
                    _isUploading = false;
                }
            }
            else
            {
                EditorUtility.ClearProgressBar();
                EditorApplication.update -= OnEditorUpdate;
            }
        }

        #endregion
#endif

        #region Internal & Interface Implementations


        private async Task UploadToGoogleDriveAsync(string filePath, CancellationToken ct)
        {
            string jsonCredentials = File.ReadAllText(ServiceAccountJsonPath);
            
            string clientEmail = Regex.Match(jsonCredentials, @"""client_email""\s*:\s*""(.*?)""").Groups[1].Value;
            string privateKey = Regex.Match(jsonCredentials, @"""private_key""\s*:\s*""(.*?)""").Groups[1].Value;
            privateKey = privateKey.Replace("\\n", "").Replace("-----BEGIN PRIVATE KEY-----", "").Replace("-----END PRIVATE KEY-----", "").Replace("\r", "").Replace("\n", "").Trim();

            if (string.IsNullOrEmpty(clientEmail) || string.IsNullOrEmpty(privateKey))
                throw new Exception("Failed to parse client_email or private_key from JSON.");

            ct.ThrowIfCancellationRequested();

            string token = await GetGoogleAccessToken(clientEmail, privateKey, ct);
            _progressMessage = "Preparing to upload...";
            _progressValue = 0.1f;

            ct.ThrowIfCancellationRequested();

            await UploadFile(token, filePath, ct);
        }

        private async Task<string> GetGoogleAccessToken(string clientEmail, string privateKeyBase64, CancellationToken ct)
        {
            string header = EncodeBase64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));

            long iat = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            long exp = iat + 3600;
            string claimStr = $"{{\"iss\":\"{clientEmail}\",\"scope\":\"https://www.googleapis.com/auth/drive\",\"aud\":\"https://oauth2.googleapis.com/token\",\"exp\":{exp},\"iat\":{iat}}}";
            string claim = EncodeBase64Url(Encoding.UTF8.GetBytes(claimStr));

            string signatureInput = $"{header}.{claim}";
            byte[] signatureBytes;

            using (RSA rsa = RSA.Create())
            {
                byte[] privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
                rsa.ImportParameters(GetRSAParametersFromPkcs8(privateKeyBytes));
                signatureBytes = rsa.SignData(Encoding.UTF8.GetBytes(signatureInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            string signature = EncodeBase64Url(signatureBytes);
            string jwt = $"{signatureInput}.{signature}";

            using (HttpClient client = new HttpClient())
            {
                var dict = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                    { "assertion", jwt }
                };

                var response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(dict), ct);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to get access token: {responseStr}");

                return Regex.Match(responseStr, @"""access_token""\s*:\s*""(.*?)""").Groups[1].Value;
            }
        }

        private async Task UploadFile(string accessToken, string filePath, CancellationToken ct)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(30);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var content = new MultipartContent("related");

                string metadataObj = $"{{\"name\": \"{Path.GetFileName(filePath)}\", \"parents\": [\"{GoogleDriveFolderId}\"]}}";
                var jsonContent = new StringContent(metadataObj, Encoding.UTF8, "application/json");
                content.Add(jsonContent);

                // Setup Progressable stream wrapper for tracking bytes written out to HttpClient
                var fs = File.OpenRead(filePath);
                var progressableContent = new ProgressableStreamContent(fs, (uploaded, total) =>
                {
                    _progressValue = 0.1f + (0.9f * ((float)uploaded / total));
                    _progressMessage = $"Uploading: {(uploaded / 1024f / 1024f):F1}MB / {(total / 1024f / 1024f):F1}MB";
                });
                
                progressableContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                content.Add(progressableContent);

                string uploadUrl = "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&supportsAllDrives=true";
                var response = await client.PostAsync(uploadUrl, content, ct);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"File upload failed: {responseStr}");
            }
        }

        private string EncodeBase64Url(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .Trim('=');
        }

        private string GetGitCommitHash(string workingDirectory)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --short HEAD",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null) return "unknown";
                    process.WaitForExit();
                    return process.StandardOutput.ReadToEnd().Trim();
                }
            }
            catch (Exception ex)
            {
                PandoraLogger.LogBuildError($"[ZipAndUpload] Failed to get git commit hash: {ex.Message}");
                return "unknown";
            }
        }

        #region PKCS#8 DER RSAParameters Parser
        private RSAParameters GetRSAParametersFromPkcs8(byte[] pkcs8)
        {
            using (var mem = new MemoryStream(pkcs8))
            using (var br = new BinaryReader(mem))
            {
                br.ReadByte(); ReadLenAsn(br);
                br.ReadByte(); ReadLenAsn(br); br.ReadByte();
                br.ReadByte(); int algoLen = ReadLenAsn(br); br.ReadBytes(algoLen);
                br.ReadByte(); int pkeyLen = ReadLenAsn(br);
                
                byte[] inner = br.ReadBytes(pkeyLen);

                using (var inMem = new MemoryStream(inner))
                using (var inBr = new BinaryReader(inMem))
                {
                    inBr.ReadByte(); ReadLenAsn(inBr);
                    inBr.ReadByte(); ReadLenAsn(inBr); inBr.ReadByte();

                    var p = new RSAParameters();
                    p.Modulus = ReadIntegerAsn(inBr);
                    p.Exponent = ReadIntegerAsn(inBr);
                    p.D = ReadIntegerAsn(inBr);
                    p.P = ReadIntegerAsn(inBr);
                    p.Q = ReadIntegerAsn(inBr);
                    p.DP = ReadIntegerAsn(inBr);
                    p.DQ = ReadIntegerAsn(inBr);
                    p.InverseQ = ReadIntegerAsn(inBr);
                    return p;
                }
            }
        }

        private int ReadLenAsn(BinaryReader br)
        {
            int len = br.ReadByte();
            if (len == 0x81) return br.ReadByte();
            if (len == 0x82)
            {
                var b = br.ReadBytes(2);
                return (b[0] << 8) | b[1];
            }
            return len;
        }

        private byte[] ReadIntegerAsn(BinaryReader br)
        {
            br.ReadByte();
            int len = ReadLenAsn(br);
            var bytes = br.ReadBytes(len);
            if (bytes.Length > 1 && bytes[0] == 0x00)
            {
                var b2 = new byte[bytes.Length - 1];
                Array.Copy(bytes, 1, b2, 0, b2.Length);
                return b2;
            }
            return bytes;
        }
        
        #endregion
        
        #endregion

        #region Nested Types

        // Custom Stream Content to report back the bytes transferred
        public class ProgressableStreamContent : HttpContent
        {
            private Stream content;
            private Action<long, long> progress;

            public ProgressableStreamContent(Stream content, Action<long, long> progress)
            {
                this.content = content;
                this.progress = progress;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                var buffer = new byte[81920];
                long totalLength = content.Length;
                long uploaded = 0;

                using (content)
                {
                    int bytesRead;
                    while ((bytesRead = await content.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await stream.WriteAsync(buffer, 0, bytesRead);
                        uploaded += bytesRead;
                        progress?.Invoke(uploaded, totalLength);
                    }
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = content.Length;
                return true;
            }
        }
        
        #endregion
    }
}
