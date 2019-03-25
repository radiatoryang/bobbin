using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

namespace Bobbin
{

    [InitializeOnLoad]
    public class BobbinCore
    {
        public static BobbinCore Instance;

        public static double lastRefreshTime { get; private set; }
        static bool refreshInProgress = false;
        UnityWebRequest[] results;
        public static string lastReport { get; private set; }

        // called on InitializeOnLoad
        static BobbinCore()
        {
            if (Instance == null)
            {
                Instance = new BobbinCore();
            }
            EditorApplication.update += Instance.OnEditorUpdate;
        }

        /// <summary>
        /// Just a callback that attaches to EditorApplication.Update, so that Bobbin can automatically fetch files even if the editor window isn't open
        /// </summary>
        void OnEditorUpdate()
        {
            if (BobbinSettings.Instance.autoRefresh && EditorApplication.timeSinceStartup > lastRefreshTime + BobbinSettings.Instance.refreshInterval)
            {
                lastRefreshTime = EditorApplication.timeSinceStartup;
                StartRefresh(); // by default, no report
            }
        }

        [MenuItem("Bobbin/Force Refresh All Files")]
        public static void DoRefresh()
        {
            refreshInProgress = false;
            Instance.StartRefresh();
        }

        [MenuItem("Bobbin/Add URLs and Settings...")]
        public static void OpenSettingsAsset()
        {
            Selection.activeObject = BobbinSettings.Instance;
        }

        public void StartRefresh()
        {
            if (refreshInProgress == false)
            {
                EditorCoroutines.StartCoroutine(RefreshCoroutine(), this);
            }
        }

        /// <summary>
        /// The main editor coroutine that fetches URLS and saves the files into the project.
        /// </summary>
        IEnumerator RefreshCoroutine()
        {
            results = new UnityWebRequest[BobbinSettings.Instance.paths.Count];
            refreshInProgress = true;
            lastReport = "Bobbin started refresh at " + System.DateTime.Now.ToLongTimeString() + ", log is below:";

            for (int i = 0; i < results.Length; i++)
            {
                var currentPair = BobbinSettings.Instance.paths[i];
                if (currentPair != null && currentPair.depth > -1) // make sure tree element is !null and not the root element
                {
                    // basic error checking
                    if (currentPair.enabled == false)
                    {
                        lastReport += string.Format("\n- {0}: DISABLED", currentPair.name);
                        continue;
                    }
                    if (currentPair.url == null || currentPair.url.Length <= 4)
                    {
                        lastReport += string.Format("\n- [ERROR] {0}: no URL defined, nothing to download", currentPair.name);
                        continue;
                    }
                    if (currentPair.filePath == null || currentPair.filePath.Length <= 4)
                    {
                        lastReport += string.Format("\n- [ERROR] {0}: no asset file path defined, nowhere to save", currentPair.name);
                        continue;
                    }

                    // actually send the web request now
                    currentPair.url = FixURL( currentPair.url );
                    results[i] = UnityWebRequest.Get( currentPair.url );
                    yield return results[i].SendWebRequest();

                    // handle an unknown internet error
                    if (results[i].isNetworkError || results[i].isHttpError)
                    {
                        Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}> and error message was: {1}", results[i].url, results[i].error);
                        lastReport += string.Format("\n- [ERROR] {0}: {1}", currentPair.name, results[i].error);
                    }
                    else
                    { 
                        // make sure the fetched file isn't just a Google login page
                        if (results[i].downloadHandler.text.Contains("google-site-verification")) {
                            Debug.LogWarningFormat("Bobbin couldn't retrieve file at <{0}> because the Google Doc didn't have public link sharing enabled", results[i].url);
                            lastReport += string.Format("\n- [ERROR] {0}: This Google Docs share link does not have 'VIEW' access; make sure you enable link sharing.", currentPair.name, currentPair.url);
                            continue;
                        }

                        // reimport only newly changed assets (compare the file hash checksums)
                        var checksum = Md5Sum(results[i].downloadHandler.data);
                        //AssetDatabase.LoadAssetAtPath(currentPair.filePath,typeof(UnityEngine.Object)) == null ||
                        if (currentPair.assetReference == null ||  currentPair.lastFileHash.Equals(checksum) == false)
                        {
                            var fullPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length) + currentPair.filePath;
                            var directoryPath = Path.GetDirectoryName( fullPath );
                            if ( Directory.Exists(directoryPath) == false) {
                                Directory.CreateDirectory( directoryPath );
                            }
                            using (FileStream fls = new FileStream(fullPath, FileMode.Create))
                            {
                                fls.Write(results[i].downloadHandler.data, 0, results[i].downloadHandler.data.Length);
                            }
                            AssetDatabase.ImportAsset(currentPair.filePath);
                            currentPair.lastFileHash = checksum;
                            currentPair.assetReference = AssetDatabase.LoadAssetAtPath(currentPair.filePath, typeof(UnityEngine.Object));
                            lastReport += string.Format("\n- {0}: UPDATED {1}", currentPair.name, currentPair.filePath);
                        }
                        else
                        {
                            lastReport += string.Format("\n- {0}: UNCHANGED", currentPair.name);
                        }
                    }
                    results[i].Dispose(); // I don't know if this is actually necessary but let's do it anyway
                }
            }

            EditorUtility.SetDirty(BobbinSettings.Instance);
            refreshInProgress = false;
        }

        // from https://github.com/MartinSchultz/unity3d/blob/master/CryptographyHelper.cs
        /// <summary>
        /// used to calculate file checksums so we can avoid unnecessary ImportAsset operations
        /// </summary>
        public static string Md5Sum(byte[] bytes)
        {
            // encrypt bytes
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash(bytes);

            // Convert the encrypted bytes back to a string (base 16)
            string hashString = "";
            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashString += System.Convert.ToString(hashBytes[i], 16).PadLeft(2, "0"[0]);
            }
            return hashString.PadLeft(32, "0"[0]);
        }


        public static string FixURL(string url)
        {
            // if it's a Google Docs URL, then grab the document ID and reformat the URL
            if (url.StartsWith("https://docs.google.com/document/d/"))
            {
                var docID = url.Substring( "https://docs.google.com/document/d/".Length, 44 );
                return string.Format("https://docs.google.com/document/export?format=txt&id={0}&includes_info_params=true", docID);
            }
            if (url.StartsWith("https://docs.google.com/spreadsheets/d/"))
            {
                var docID = url.Substring( "https://docs.google.com/spreadsheets/d/".Length, 44 );
                return string.Format("https://docs.google.com/spreadsheets/export?format=csv&id={0}", docID);
            }
            return url;
        }

        public static string UnfixURL(string url)
        {
           // if it's a Google Docs URL, then grab the document ID and reformat the URL
            if (url.StartsWith("https://docs.google.com/document/export?format=txt"))
            {
                var docID = url.Substring( "https://docs.google.com/document/export?format=txt&id=".Length, 44 );
                return string.Format("https://docs.google.com/document/d/{0}/edit", docID);
            }
            if (url.StartsWith("https://docs.google.com/spreadsheets/export?format=csv"))
            {
                var docID = url.Substring( "https://docs.google.com/spreadsheets/export?format=csv&id=".Length, 44 );
                return string.Format("https://docs.google.com/spreadsheets/d/{0}", docID);
            }
            return url;
        }

    }



}
