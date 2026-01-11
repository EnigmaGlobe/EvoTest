using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class LoginController : MonoBehaviour
{
    public static LoginController Instance;
    public static string SERVER_API => "http://localhost:3003";
    public static string PROD_SERVER_API => "https://apiprod.classlet.space";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string envId;
    public string worldId;

    public string userId;

    public List<Dialogue> dialogues;

    public TMP_InputField codeTxt;
    public TMP_InputField username;

    public GameObject loginPanel, startPanel;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Login()
    {
        Debug.Log($"Login called with username {username.text} and code {codeTxt.text}");
        var code = codeTxt.text
       .Replace("\u200B", "")  // Zero Width Space
       .Replace("\u200C", "")  // Zero Width Non-Joiner
       .Replace("\u200D", "")  // Zero Width Joiner
       .Replace("\uFEFF", "")  // BOM / Zero Width No-Break Space
       .Trim();
        Debug.Log("Login button clicked. Code: " + code);
        var path = $"/api/world/getWorldByCode?code={Uri.EscapeDataString(code)}";

        StartLogin(path);
    }

    public async void StartLogin(string path)
    {
        Debug.Log("Starting login process...");
        await Connect(path);
        Debug.Log("Connected to server. Now registering user...");
        await registerUser(username.text);
        Debug.Log("User registered. Transitioning to start panel...");
        loginPanel.SetActive(false);
        startPanel.SetActive(true);
    }

    public async Task Connect(string text)
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var response = await GetAsync(text, "Login", cancellationToken);
        if (response != null)
        {
            Debug.Log("Login successful. Response: " + response);
            var loadDataJson = JsonConvert.DeserializeObject<LoadDataJson>(response);
            worldId = loadDataJson!.world!._id!;
            envId = loadDataJson!.environments![0]._id!;

            //Proceed to load the environment data
            GetInteractables(envId);
        }
        else
        {
            Debug.LogError("Login failed.");
        }
    }

    public async void GetInteractables(string envId)
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var path = $"/api/worldObject/getInteractables?envId={Uri.EscapeDataString(envId)}";
        Debug.Log("Getting interactables from path: " + path);
        var response = await GetAsync(path, "GetInteractables", cancellationToken);
        if (response != null)
        {
            Debug.Log("GetInteractables successful. Response: " + response);
            var dialogues = JsonConvert.DeserializeObject<DialogueJsonData>(response);
            Debug.Log($"Number of dialogues received: {dialogues!.dialogues.Count}");
            this.dialogues = dialogues!.dialogues;
        }
        else
        {
            Debug.LogError("GetInteractables failed.");
        }
    }



    /// <summary>
    /// Register a new user with the given name using a HTTP POST
    /// </summary>
    /// <param name="name"></param>
    public async Task registerUser(string name)
    {
        //    var cancellationToken = new CancellationTokenSource().Token;

        var path = $"/api/player/";
        Debug.Log("Getting user from path: " + path);
        var jsonData = JsonConvert.SerializeObject(new { userName = name });
        var response = await httpPost(path, jsonData, false, "Registering User", "Registering User");
        if (response != null)
        {
            Debug.Log("RegisterUser successful. Response: " + response);
            var user = JsonConvert.DeserializeObject<PlayerStat>(response);
            userId = user!._id!;
            Debug.Log("Registered user ID: " + userId);
        }
        else
        {
            Debug.LogError("RegisterUser failed." + response);
        }
        //call a post
    }

    public async Task<string> httpPost(string path, string jsonData, bool llm = false, string status = null, string info = null)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            Debug.LogError("http post is empty");
            return default;
        }

        //CachedPost cachedPost = OfflineHttpHandler.instance.CachePost(path, jsonData);
        //if (GameStat.instance.IsOfflineMode())
        //{
        //    Debug.Log("httpPost offline mode, returning null");
        //    cachedPost.completed = false;
        //    return null;
        //}
        try
        {

            string url = SERVER_API + path;

            Debug.Log("new json data to send 3" + jsonData + " url " + url);
            if (string.IsNullOrEmpty(jsonData))
            {
                throw new Exception("JSON data to send is empty or null" + jsonData);
            }


            //                notificationSO.TriggerLoader(true, status != null ? status : status);

            UnityWebRequest www = UnityWebRequest.PostWwwForm(url, jsonData);

            www.SetRequestHeader("Content-Type", "application/json");

            www.uploadHandler.contentType = "application/json";
            www.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));

            var operation = www.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            //             notificationSO.TriggerLoader(false, status != null ? status : status);


            Debug.Log("final result " + www.result);
            if (www.result != UnityWebRequest.Result.Success)
            {
                // PopUpManager.instance.PopUpError("ERROR WITH httpPost ", www.error + " | json data "+jsonData);
                Debug.LogError($"Failed: {www.error}");
            }


            Debug.Log("got the result!" + www.downloadHandler.text);

            //                OfflineHttpHandler.instance.ModifyCachedPost(cachedPost.id, true);

            return www.downloadHandler.text;
        }
        catch (Exception ex)
        {
            Debug.Log("exception on http Post " + ex.ToString());
            return null;
        }
    }
    public async Task<string> GetAsync(string path, string statusTag = null, CancellationToken ct = default)
    {
        try
        {
            string url = SERVER_API + path;
            Debug.Log($"[HTTPManager] GET {statusTag} url={url}");

            using var www = UnityWebRequest.Get(url);

            var operation = www.SendWebRequest();


            while (!operation.isDone)
            {
                if (ct.IsCancellationRequested)
                    return null;

                await Task.Yield();
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"[HTTPManager] GET failed: {www.error} url={url}");
                return www.error;
            }
            return www.downloadHandler.text;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[HTTPManager] Exception: " + ex);
            return null;
        }
    }
}



public class World
{
    public string? passcode; /* ... */
    public string? _id;
}

[System.Serializable]
public class Environment
{
    public string? _id;
    public string? name;
    public string? asset;
    public string? worldId;

    public string? rubrics;
    public string? context;

}
[System.Serializable]
public class LoadDataJson
{
    public World? world;
    public List<Environment>? environments;

}
[System.Serializable]
public class DialogueJsonData
{
    public List<Dialogue> dialogues;
    public string envId;
    public string userId;
    public List<string> allCategories;

}

[System.Serializable]
public class Dialogue
{
    //to match the server model, needed when deserialization of the json from server.
    public string _id;

    public string id;

    public string category;

    public string name;

    public string startAudio;

    public string image;

    public string asset;

    public string correctAnswer;

    public string selectedAnswer;

    public string userTextInput;


    public bool submitVoiceChatEssay;
    public bool submitVoiceRecord;

    public FormItemData[]? formItemDatas;
    public FormResponse[]? formResponse;

    public bool randomFormItems;

    public string npcId; //the worldobject id
    public string[]? npcDialogue;
    [TextArea(3, 10)]
    public string[]? playerDialogue;
    public string[]? playerDialogueType;

    public string[]? languageCode;

    public string notifyArea;
    public string group;

    public bool defaultOn;
    public string profilePic;
    public string triggerType;
    public bool indicatorType; //3d only
    public bool completed;
    public string status;
    public string shortDesc;

    public bool mandatory;

    public List<string> ongoingFIDs = new List<string>();
}


[Serializable]
public class FormItemData
{

    //public UserActivity[] auditTrails;
    public string name;
    public string shortName;
    public string question;
    public string description;
    public string value1;
    public string value2;
    public bool hint;
    public List<string>? questionText;

    public List<string>? questionLg;
    public string type;
    public string recordAudio;
    public string audioChat;
    public string _id;
    public string formId;
    public string lg; //language
    public string answer1;
    public string answer2;
    public string answer3;
    public string answer4;
    public string answer5;
    public string answer1lg;
    public string answer2lg;
    public string answer3lg;
    public string answer4lg;
    public string answer5lg;
    public string answer1Res; //what to reply if user selects answer1?
    public string answer2Res; //what to reply if user selects answer2?
    public string answer3Res; //what to reply if user selects answer3?
    public string answer4Res;
    public string answer5Res;

    public string answer1Img; //what to reply if user selects answer1?
    public string answer2Img; //what to reply if user selects answer2?
    public string answer3Img;
    public string answer4Img;
    public string answer5Img;
    public string answer1pt;
    public string answer2pt;
    public string answer3pt;
    public string answer4pt;
    public string answer5pt;
    public string answer1pttype;
    public string answer2pttype;
    public string answer3pttype;
    public string answer4pttype;
    public string correctAnswer;
    public string mcOpen;
    public bool isLocked;
    public bool noRepeat;
    public string questionImg;
    public bool completed;

    public string answer1ResVoice;
    public string answer2ResVoice;
    public string answer3ResVoice;
    public string answer4ResVoice;
    public string answer1ResVoiceTxt;
    public string answer2ResVoiceTxt;
    public string answer3ResVoiceTxt;
    public string answer4ResVoiceTxt;


}

public class PlayerStat
{
    public string? x;
    public string? y;
    public string? z;
    public string? rotW;
    public string? rotX;
    public string? rotY;
    public string? rotZ;

    public string? currentEnvId;
    public string? currentBattleId;

    public string? battleId;
    public string? name;
    public string? role;
    public string? userName;


    public Vector3 pos;
    public Vector3 rot;

    public string? _id;
    public string? userId;
    public string? avatar;
    public string? recipe;
    public string? email;
    public string? selectedTop;
    public string? selectedBottom;
    public string? selectedAccessory;
    public string? selectedShoe;
    public int points;

    public string? currentAction;
    public Texture? sprayImage;
    public Vector3 actionPos;
    public Quaternion actionRot;
    public string? chosenImageName;

    public Transform? eyeAnchor;

    public string[]? codes;

    public GameObject? playerGO;

    public bool checkName;

}