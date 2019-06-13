using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Text.RegularExpressions;
using System;
using UnityEngine.UI;
using System.Diagnostics;
using TMPro;

using Debug = UnityEngine.Debug;

public class Launcher : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI m_RemoteVersionLabel = null;
    [SerializeField] TextMeshProUGUI m_LocalVersionLabel = null;
    [SerializeField] Button m_RunButton = null;
    [SerializeField] GameObject m_DownloadPanel = null;
    [SerializeField] GameObject m_ProgressPanel = null;
    [SerializeField] Slider m_ProgressBar = null;

    DateTime m_RemoteVersion;
    DateTime m_LocalVersion;

    const string s_RepoPath = "ryan-pf/DemoGameLauncher";
    const string s_FilePath = "Builds/boh-build-win.zip";
    const string s_GameFolderName = "boh-build-win";
    const string s_GameExeName = "BagOfHolding.exe";

    [Serializable]
    struct GameMetaInfo
    {
        public string localVersion;
    }

    void Start()
    {
        GameMetaInfo metaInfo;
        metaInfo.localVersion = "well\\ of// \\/course" + DateTime.Now.ToString();

        string json = JsonUtility.ToJson(metaInfo);
        string metaInfoFilePath = GetMetaInfoFilePath();

        Debug.Log("object: " + metaInfo + " json: " + json);


        // Load local date 
        string path = Application.persistentDataPath + "/" + "GameMetaInfo";

        if (File.Exists(path))
        {
            Debug.Log("Loading local version info: " + path);
            string text = File.ReadAllText(path);
			string dateString = JsonUtility.FromJson<GameMetaInfo>(text).localVersion;
			if (dateString != null)
			{
				m_LocalVersion = DateTime.Parse(dateString);
				UpdateStatsPanel();
			}
			else
			{
				Debug.Log("Failed to load, so deleting local version info: " + path);
				File.Delete(path);
			}
        }
        else
        {
            Debug.Log("Local version info not found: " + path);
        }
    }

    public void Launch()
    {
        StartCoroutine(LaunchInternal());
    }

    public void Download()
    {
        Debug.Log("Downloading");
        StartCoroutine(DownloadInternal());
    }

    IEnumerator LaunchInternal()
    {
        m_RunButton.interactable = false;

        // Check if there's an update for the game
        string commitsURL = "https://api.github.com/repos/" + s_RepoPath + "/commits?path=" + s_FilePath + "&page=1&per_page=1";

        yield return DownloadCommits(commitsURL);

        // If there's an update then suggest a download
        if (m_RemoteVersion.CompareTo(m_LocalVersion) > 0)
        {
            m_DownloadPanel.SetActive(true);
            m_RunButton.gameObject.SetActive(false);
        }
        else
        {
            // If there's not an update then run the game
            string exePath = Application.persistentDataPath + "/" + s_GameFolderName + "/" + s_GameExeName;
            Debug.Log("Launching: " + exePath);
            Process.Start(exePath);
        }

        m_RunButton.interactable = true;
    }

    IEnumerator DownloadInternal()
    {
        m_DownloadPanel.SetActive(false);
        m_RunButton.gameObject.SetActive(true);
        m_RunButton.interactable = false;

        yield return DownloadFile();

        m_RunButton.interactable = true;
    }

    IEnumerator DownloadCommits(string url)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            string text = www.downloadHandler.text;
            Debug.Log("commits text: " + text);

            string s = Regex.Match(text,
                        @"""date"": ""(.+)""")
            .Groups[1].Value;

            Debug.Log("s: " + s);

            DateTime time = DateTime.Parse(s);

            Debug.Log("time: " + time);

            m_RemoteVersion = time;

            UpdateStatsPanel();
        }
    }

    IEnumerator DownloadFile()
    {
        string url = "https://github.com/" + s_RepoPath + "/raw/master/" + s_FilePath;

        UnityWebRequest www = UnityWebRequest.Get(url);

        www.SendWebRequest();

        m_ProgressPanel.SetActive(true);

        while (www.downloadProgress < 1.0f)
        {
            m_ProgressBar.value = www.downloadProgress;
            yield return null;
        }

        m_ProgressBar.value = 1.0f;

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            Debug.Log(www.downloadHandler.text);

            // Or retrieve results as binary data
            byte[] results = www.downloadHandler.data;

            string path = Application.persistentDataPath + "/" + Path.GetFileName(url);

            Debug.Log("Writing to: " + path);

            if (File.Exists(path))
                File.Delete(path);

            File.Create(path).Close();
            File.WriteAllBytes(path, results);

            // Get path without extension
            string pathWithoutExt = Application.persistentDataPath + "/" + Path.GetFileNameWithoutExtension(path);

            if (Directory.Exists(pathWithoutExt))
                DeleteDirectory(pathWithoutExt);

            string unzipLocationRoot = Application.persistentDataPath;

            Debug.Log("Unzip location: " + pathWithoutExt);

            // Unzip
            LightBuzz.Archiver.Archiver.Decompress(path, unzipLocationRoot);

            m_LocalVersion = m_RemoteVersion;
            

            GameMetaInfo metaInfo;
            metaInfo.localVersion = m_LocalVersion.ToString();

            string json = JsonUtility.ToJson(metaInfo);
            string metaInfoFilePath = GetMetaInfoFilePath();

            Debug.Log("object: " + metaInfo + " json: " + json);

            if (!File.Exists(metaInfoFilePath))
                File.CreateText(metaInfoFilePath).Close();

            File.WriteAllText(metaInfoFilePath, json);

            UpdateStatsPanel();
        }

        yield return new WaitForSeconds(0.3f); // Stops clicks being stored and clicking GO after synchronous work

        m_ProgressPanel.SetActive(false);
    }
    
    void UpdateStatsPanel()
    {
        if (m_RemoteVersion == default(DateTime))
        {
            m_RemoteVersionLabel.text = "Remote: Click GO";
        }
        else
        {
            m_RemoteVersionLabel.text = "Remote: " + m_RemoteVersion.ToString();
        }

        if (m_LocalVersion == default(DateTime))
        {
            m_LocalVersionLabel.text = "Local: Not downloaded";
        }
        else
        {
            m_LocalVersionLabel.text = "Local: " + m_LocalVersion.ToString();
        }
    }

    string GetMetaInfoFilePath()
    {
        return Application.persistentDataPath + "/" + "GameMetaInfo";
    }

    public static void DeleteDirectory(string target_dir)
    {
        string[] files = Directory.GetFiles(target_dir);
        string[] dirs = Directory.GetDirectories(target_dir);

        foreach (string file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in dirs)
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(target_dir, false);
    }
}
