using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;

[System.Serializable] public class Obs { public int px, py, ex, ey, step, player_score; }
[System.Serializable] public class ActReq { public Obs obs; public ActReq(Obs o){ obs=o; } }
[System.Serializable] public class ActResp { public int action; public float aggression; }

public class NPCController : MonoBehaviour {
    [Header("Scene refs")] public Transform player;
    [Header("Tuning")]    public float baseSpeed = 3f;
    public float pollHz = 10f;
    public string serverBase = "http://localhost:8000";

    [Header("Demo controls")]
    [Range(50,150)] public int playerScore = 100; // ← adjustable with [ and ]
    public bool logDecisions = false;

    // internal state
    int step = 0;
    Vector3 currentDir = Vector3.zero;
    float currentAgg = 0.5f;
    SpriteRenderer sr;

    void Awake(){
        sr = GetComponent<SpriteRenderer>();
    }

    void Start(){
        StartCoroutine(PollLoop());
    }

    void Update(){
        // Hotkeys to simulate "skill"
        if (Input.GetKeyDown(KeyCode.RightBracket)) playerScore = Mathf.Min(150, playerScore + 5);
        if (Input.GetKeyDown(KeyCode.LeftBracket))  playerScore = Mathf.Max(50,  playerScore - 5);

        // Speed scales with aggression (make it obvious)
        float speed = baseSpeed * Mathf.Lerp(0.6f, 2.0f, currentAgg);
        transform.position += currentDir * speed * Time.deltaTime;

        // Color scales with aggression (white → light red)
        if (sr) sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f), currentAgg);
    }

    IEnumerator PollLoop(){
        float interval = 1f / Mathf.Max(1f, pollHz);
        while (true){
            yield return SendAct();
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator SendAct(){
        step++;
        Obs o = new Obs {
            px = Mathf.RoundToInt(player.position.x),
            py = Mathf.RoundToInt(player.position.y),
            ex = Mathf.RoundToInt(transform.position.x),
            ey = Mathf.RoundToInt(transform.position.y),
            step = step,
            player_score = playerScore     // ← use knob
        };

        string json = JsonUtility.ToJson(new ActReq(o));
        using (UnityWebRequest req = new UnityWebRequest(serverBase + "/act", "POST")){
            byte[] body = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success){
                var r = JsonUtility.FromJson<ActResp>(req.downloadHandler.text);
                ApplyAction(r);
            }
        }
    }

    void ApplyAction(ActResp r){
        currentDir = Vector3.zero;
        if (r.action == 0) currentDir = Vector3.up;
        if (r.action == 1) currentDir = Vector3.down;
        if (r.action == 2) currentDir = Vector3.left;
        if (r.action == 3) currentDir = Vector3.right;
        currentAgg = Mathf.Clamp01(r.aggression);
        if (logDecisions) Debug.Log($"act={r.action} agg={currentAgg:F2} score={playerScore}");
    }
}