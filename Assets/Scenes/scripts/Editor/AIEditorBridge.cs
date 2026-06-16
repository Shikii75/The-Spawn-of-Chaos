using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A Unity Editor extension that runs a lightweight HTTP listener.
/// Enables external AI assistants to send editor-safe command requests
/// (e.g. querying scene hierarchies, attaching scripts, triggering UI styling, saving).
/// Commands are received on background threads and safely queued for execution on Unity's main thread.
/// </summary>
[InitializeOnLoad]
public static class AIEditorBridge
{
    private static HttpListener listener;
    private static ConcurrentQueue<EditorCommand> commandQueue = new ConcurrentQueue<EditorCommand>();
    private const string PORT = "5002";

    [Serializable]
    private class EditorCommand
    {
        public string action;
        public string targetName;
        public string componentName;
        public string propertyName;
        public string propertyValue;
        public string scriptName;
        public string responseId;
    }

    [Serializable]
    private class CommandResponse
    {
        public string status;
        public string message;
        public string data;
    }

    private static ConcurrentDictionary<string, CommandResponse> responses = new ConcurrentDictionary<string, CommandResponse>();

    static AIEditorBridge()
    {
        // Subscribe to editor updates so we execute commands on Unity's main thread
        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        EditorApplication.quitting += Cleanup;
        StartServer();
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Update;
        if (listener != null)
        {
            try
            {
                listener.Stop();
                listener.Close();
                Debug.Log("[AIEditorBridge] Server stopped successfully.");
            }
            catch (Exception e)
            {
                // Ignore
            }
            listener = null;
        }
    }

    private static void StartServer()
    {
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{PORT}/command/");
            listener.Start();
            Task.Run(() => ListenLoop());
            Debug.Log($"[AIEditorBridge] Server started on http://localhost:{PORT}/command/");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AIEditorBridge] Failed to start server: {e.Message}");
        }
    }

    private static void ListenLoop()
    {
        while (listener.IsListening)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                if (request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string json = reader.ReadToEnd();
                        var cmd = JsonUtility.FromJson<EditorCommand>(json);
                        cmd.responseId = Guid.NewGuid().ToString();

                        // Queue for execution on the main Unity thread
                        commandQueue.Enqueue(cmd);

                        // Block background thread and wait for main thread to execute it
                        CommandResponse resp = null;
                        int timeout = 5000; // 5 seconds execution timeout limit
                        int waited = 0;
                        while (waited < timeout)
                        {
                            if (responses.TryRemove(cmd.responseId, out resp))
                            {
                                break;
                            }
                            System.Threading.Thread.Sleep(50);
                            waited += 50;
                        }

                        if (resp == null)
                        {
                            resp = new CommandResponse { status = "error", message = "Command execution timed out." };
                        }

                        string responseJson = JsonUtility.ToJson(resp);
                        byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                        response.ContentLength64 = buffer.Length;
                        response.ContentType = "application/json";
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                }
                else
                {
                    response.StatusCode = 405; // Method Not Allowed
                }
                response.OutputStream.Close();
            }
            catch (Exception e)
            {
                if (listener.IsListening)
                {
                    Debug.LogWarning($"[AIEditorBridge] Listener loop exception: {e.Message}");
                }
            }
        }
    }

    private static void Update()
    {
        // Safely pull and process command on Unity main thread
        if (commandQueue.TryDequeue(out EditorCommand cmd))
        {
            CommandResponse resp = ExecuteCommand(cmd);
            responses[cmd.responseId] = resp;
        }
    }

    private static CommandResponse ExecuteCommand(EditorCommand cmd)
    {
        try
        {
            switch (cmd.action.ToLower())
            {
                case "ping":
                    return new CommandResponse { status = "success", message = "Connection successful." };

                case "gethierarchy":
                    return GetHierarchy();

                case "selectobject":
                    return SelectObject(cmd.targetName);

                case "attachscript":
                    return AttachScript(cmd.targetName, cmd.scriptName);

                case "applystyler":
                    return ApplyStyler(cmd.targetName);

                case "applydialoguestyler":
                    return ApplyDialogueStyler(cmd.targetName);

                case "getcomponents":
                    return GetComponents(cmd.targetName);

                case "createprefab":
                    return CreatePrefab(cmd.targetName, cmd.propertyValue);

                case "getproperties":
                    return GetProperties(cmd.targetName, cmd.componentName);

                case "setproperty":
                    return SetProperty(cmd.targetName, cmd.componentName, cmd.propertyName, cmd.propertyValue);

                case "removecomponent":
                    return RemoveComponent(cmd.targetName, cmd.componentName);

                case "findobjects":
                    return FindObjects(cmd.componentName);

                case "savescene":
                    EditorSceneManager.SaveOpenScenes();
                    return new CommandResponse { status = "success", message = "Active scene saved successfully." };

                case "reloadscene":
                    string activeScenePath = EditorSceneManager.GetActiveScene().path;
                    EditorSceneManager.OpenScene(activeScenePath);
                    return new CommandResponse { status = "success", message = "Active scene reloaded from disk." };

                case "toggleplay":
                    EditorApplication.isPlaying = !EditorApplication.isPlaying;
                    return new CommandResponse { status = "success", message = $"Play mode toggled. IsPlaying: {EditorApplication.isPlaying}" };

                case "createobject":
                    return CreateLevelObject(cmd);

                case "buildlevel":
                    return BuildLevelRemainder();

                default:
                    return new CommandResponse { status = "error", message = $"Unknown action: '{cmd.action}'" };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AIEditorBridge] Error executing command '{cmd.action}': {e.Message}\n{e.StackTrace}");
            return new CommandResponse { status = "error", message = e.Message };
        }
    }

    private static CommandResponse GetHierarchy()
    {
        var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < roots.Length; i++)
        {
            SerializeHierarchy(roots[i].transform, sb);
            if (i < roots.Length - 1) sb.Append(",");
        }
        sb.Append("]");
        return new CommandResponse { status = "success", message = "Hierarchy retrieved.", data = sb.ToString() };
    }

    private static void SerializeHierarchy(Transform t, StringBuilder sb)
    {
        sb.Append("{");
        sb.Append($"\"name\":\"{t.name}\"");
        if (t.childCount > 0)
        {
            sb.Append(",\"children\":[");
            for (int i = 0; i < t.childCount; i++)
            {
                SerializeHierarchy(t.GetChild(i), sb);
                if (i < t.childCount - 1) sb.Append(",");
            }
            sb.Append("]");
        }
        sb.Append("}");
    }

    private static CommandResponse SelectObject(string name)
    {
        GameObject go = FindGameObjectIncludingInactive(name);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{name}' not found." };
        }
        Selection.activeGameObject = go;
        return new CommandResponse { status = "success", message = $"Selected GameObject '{name}'." };
    }

    private static CommandResponse AttachScript(string targetName, string scriptName)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        System.Type type = System.Type.GetType(scriptName);
        if (type == null)
        {
            // Search loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(scriptName);
                if (type != null) break;
            }
        }

        if (type == null)
        {
            return new CommandResponse { status = "error", message = $"Script class '{scriptName}' not found in loaded assemblies." };
        }

        Component comp = go.GetComponent(type);
        if (comp == null)
        {
            comp = go.AddComponent(type);
            Undo.RegisterCreatedObjectUndo(comp, $"Attach {scriptName}");
            return new CommandResponse { status = "success", message = $"Attached script '{scriptName}' to GameObject '{targetName}'." };
        }
        return new CommandResponse { status = "success", message = $"Script '{scriptName}' already attached to GameObject '{targetName}'." };
    }

    private static CommandResponse ApplyStyler(string targetName)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        var styler = go.GetComponent<NyxarisUIStyler>();
        if (styler == null)
        {
            styler = go.GetComponentInChildren<NyxarisUIStyler>(true);
        }

        if (styler == null)
        {
            return new CommandResponse { status = "error", message = $"NyxarisUIStyler component not found on '{targetName}' or its children." };
        }

        styler.ApplyStyling();
        EditorUtility.SetDirty(styler.gameObject);
        return new CommandResponse { status = "success", message = "NyxarisUIStyler applied styling successfully in editor mode." };
    }

    private static CommandResponse ApplyDialogueStyler(string targetName)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        var npcUI = go.GetComponent<NPCDialogueUI>();
        if (npcUI == null)
        {
            npcUI = go.GetComponentInChildren<NPCDialogueUI>(true);
        }

        if (npcUI == null)
        {
            return new CommandResponse { status = "error", message = $"NPCDialogueUI component not found on '{targetName}' or its children." };
        }

        npcUI.ApplyStyling();
        EditorUtility.SetDirty(npcUI.gameObject);
        return new CommandResponse { status = "success", message = "NPCDialogueUI applied styling successfully in editor mode." };
    }

    private static CommandResponse GetComponents(string targetName)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        Component[] comps = go.GetComponents<Component>();
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            sb.Append($"\"{comps[i].GetType().FullName}\"");
            if (i < comps.Length - 1) sb.Append(",");
        }
        sb.Append("]");
        return new CommandResponse { status = "success", message = $"Components retrieved for '{targetName}'.", data = sb.ToString() };
    }

    private static CommandResponse CreatePrefab(string targetName, string path)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
        if (prefab != null)
        {
            return new CommandResponse { status = "success", message = $"Prefab created and connected successfully at '{path}'." };
        }
        return new CommandResponse { status = "error", message = "Failed to create prefab." };
    }

    private static CommandResponse GetProperties(string targetName, string componentName)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        Component comp = go.GetComponent(componentName);
        if (comp == null)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                {
                    comp = c;
                    break;
                }
            }
        }

        if (comp == null)
        {
            return new CommandResponse { status = "error", message = $"Component '{componentName}' not found on '{targetName}'." };
        }

        var sb = new StringBuilder();
        sb.Append("{");
        var fields = comp.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            object val = fields[i].GetValue(comp);
            string valStr = val != null ? val.ToString() : "null";
            valStr = valStr.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            sb.Append($"\"{fields[i].Name}\":\"{valStr}\"");
            if (i < fields.Length - 1) sb.Append(",");
        }
        sb.Append("}");
        return new CommandResponse { status = "success", message = "Properties retrieved.", data = sb.ToString() };
    }

    private static CommandResponse SetProperty(string targetName, string componentName, string propertyName, string propertyValue)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        Component comp = go.GetComponent(componentName);
        if (comp == null)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                {
                    comp = c;
                    break;
                }
            }
        }

        if (comp == null)
        {
            return new CommandResponse { status = "error", message = $"Component '{componentName}' not found on '{targetName}'." };
        }

        var field = comp.GetType().GetField(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var prop = comp.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field == null && prop == null)
        {
            return new CommandResponse { status = "error", message = $"Field or Property '{propertyName}' not found on '{componentName}'." };
        }

        try
        {
            if (field != null)
            {
                object val = Convert.ChangeType(propertyValue, field.FieldType);
                field.SetValue(comp, val);
            }
            else
            {
                object val = Convert.ChangeType(propertyValue, prop.PropertyType);
                prop.SetValue(comp, val);
            }
            EditorUtility.SetDirty(comp.gameObject);
            return new CommandResponse { status = "success", message = $"Property '{propertyName}' set to '{propertyValue}'." };
        }
        catch (Exception e)
        {
            return new CommandResponse { status = "error", message = $"Failed to set property: {e.Message}" };
        }
    }

    private static GameObject FindGameObjectIncludingInactive(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        // Try standard active Find first
        GameObject go = GameObject.Find(name);
        if (go != null) return go;

        // Search active scene hierarchy roots
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            go = FindChildRecursive(root.transform, name);
            if (go != null) return go;
        }

        // Try trimmed name
        string trimmed = name.Trim();
        if (trimmed != name)
        {
            go = GameObject.Find(trimmed);
            if (go != null) return go;

            foreach (var root in roots)
            {
                go = FindChildRecursive(root.transform, trimmed);
                if (go != null) return go;
            }
        }

        return null;
    }

    private static GameObject FindChildRecursive(Transform parent, string name)
    {
        if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            return parent.gameObject;

        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject result = FindChildRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    private static CommandResponse RemoveComponent(string targetName, string componentName)
    {
        GameObject go = FindGameObjectIncludingInactive(targetName);
        if (go == null)
        {
            return new CommandResponse { status = "error", message = $"GameObject '{targetName}' not found." };
        }

        Component comp = go.GetComponent(componentName);
        if (comp == null)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null && c.GetType().Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                {
                    comp = c;
                    break;
                }
            }
        }

        if (comp == null)
        {
            return new CommandResponse { status = "success", message = $"Component '{componentName}' already not present on '{targetName}'." };
        }

        Undo.DestroyObjectImmediate(comp);
        return new CommandResponse { status = "success", message = $"Removed component '{componentName}' from '{targetName}'." };
    }

    private static CommandResponse FindObjects(string componentName)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var list = new System.Collections.Generic.List<string>();
        foreach (var root in roots)
        {
            FindObjectsRecursive(root.transform, componentName, list);
        }
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"\"{list[i]}\"");
            if (i < list.Count - 1) sb.Append(",");
        }
        sb.Append("]");
        return new CommandResponse { status = "success", message = $"Found {list.Count} objects with component '{componentName}'.", data = sb.ToString() };
    }

    private static void FindObjectsRecursive(Transform parent, string componentName, System.Collections.Generic.List<string> list)
    {
        foreach (var c in parent.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(parent.name);
                break;
            }
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            FindObjectsRecursive(parent.GetChild(i), componentName, list);
        }
    }

    // =========================================================================
    //  LEVEL BUILDER HELPERS
    // =========================================================================

    /// <summary>
    /// Parses a JSON command of the form:
    /// { "action":"createobject", "targetName":"MyBlock",
    ///   "propertyValue":"cx,cy,width,height,r,g,b,tag" }
    /// and creates the corresponding block in the scene.
    /// </summary>
    private static CommandResponse CreateLevelObject(EditorCommand cmd)
    {
        string[] parts = cmd.propertyValue.Split(',');
        if (parts.Length < 8)
            return new CommandResponse { status = "error", message = "propertyValue must be: cx,cy,width,height,r,g,b,tag" };

        float cx     = float.Parse(parts[0]);
        float cy     = float.Parse(parts[1]);
        float width  = float.Parse(parts[2]);
        float height = float.Parse(parts[3]);
        byte  r      = byte.Parse(parts[4]);
        byte  g      = byte.Parse(parts[5]);
        byte  b      = byte.Parse(parts[6]);
        string tag   = parts[7].Trim();

        MakeLevelBlock(cmd.targetName, cx, cy, width, height, new Color32(r, g, b, 255), tag);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return new CommandResponse { status = "success", message = $"Created block '{cmd.targetName}' at ({cx},{cy}) size ({width}x{height})." };
    }

    /// <summary>
    /// Builds the entire remaining level in one shot.
    /// Coordinate reference (existing content):
    ///   Village ground: X=0..100, Y≈-4
    ///   Hairy platforms (floating): X=130..295, Y=-10..14
    ///   Mountain staircase (existing): X≈2..25, Y=-145..-248
    /// New content goes right of village and climbs UP.
    /// </summary>
    private static CommandResponse BuildLevelRemainder()
    {
        // colour palette
        Color32 mountainGray  = new Color32(80,  80,  90, 255);
        Color32 mountainLight = new Color32(110, 112, 120, 255);
        Color32 dojoWood      = new Color32(139, 90,  43, 255);
        Color32 dojoRoof      = new Color32(60,  40,  20, 255);
        Color32 caveRock      = new Color32(55,  50,  60, 255);
        Color32 caveDark      = new Color32(35,  30,  40, 255);
        Color32 groundDirt    = new Color32(100, 75,  50, 255);
        Color32 orbPurple     = new Color32(80,  60, 130, 255);
        Color32 saveCrystal   = new Color32(0,  200, 255, 255);
        Color32 stoneTrap     = new Color32(90,  88,  80, 255);
        Color32 bossCave      = new Color32(40,  20,  30, 255);
        Color32 snow          = new Color32(200, 200, 215, 255);

        int created = 0;

        // ── SECTION 1 : Telepathy Orb Platform ────────────────────────────────
        // Approach ramp from village (X=90..108)
        M("OrbApproach_Ground",   99,  -4.5f, 20,  2,  groundDirt,   "Ground", ref created);
        M("OrbStep1",             95,   1,     6,   1,  mountainLight,"Ground", ref created);
        M("OrbStep2",            101,   3,     6,   1,  mountainLight,"Ground", ref created);
        M("OrbStep3",            107,   5,     6,   1,  mountainLight,"Ground", ref created);
        // Floating orb platform
        M("OrbPlatform",         113,   9,    16,  1.5f,orbPurple,   "Ground", ref created);
        M("OrbPillar_L",         113,   4.5f,  1,  5f,  orbPurple,   "Untagged",ref created);
        M("OrbPillar_R",         128,   4.5f,  1,  5f,  orbPurple,   "Untagged",ref created);
        // Visual orb object (no collider — decorative)
        M("TelepathyOrb",        121,  11,     2,   2,  new Color32(180,120,255,255),"Untagged",ref created);

        // ── SECTION 2 : Mountain Path → Dojo 1 ───────────────────────────────
        // Cliff backing wall
        M("Mt1_CliffFace",       130,  18,   2.5f,60,  mountainGray,"Ground",  ref created);
        // Ascending ledge staircase (12 steps, each +2 Y, +7 X)
        float[] lx1 = {132,139,146,153,160,167,174,181,188,195,202,209};
        float[] ly1 = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31};
        for (int i=0;i<lx1.Length;i++)
            M("Mt1_Ledge"+i, lx1[i],ly1[i], 9f, 1f, mountainLight,"Ground",ref created);
        // Boulders for atmosphere
        M("Mt1_Boulder1",        145,  12,   4,   2,  mountainGray,"Untagged",ref created);
        M("Mt1_Boulder2",        172,  20,   3,   1.5f,mountainGray,"Untagged",ref created);
        M("Mt1_Boulder3",        199,  28,   4,   2,  mountainGray,"Untagged",ref created);

        // ── SECTION 3 : Dojo 1 ───────────────────────────────────────────────
        float d1x=209, d1y=31;
        M("Dojo1_Floor",   d1x,      d1y,      50,  1.5f,dojoWood,"Ground",  ref created);
        M("Dojo1_WallL",   d1x,      d1y+1,     2,  13,  dojoWood,"Ground",  ref created);
        M("Dojo1_WallR",   d1x+48,   d1y+1,     2,  13,  dojoWood,"Ground",  ref created);
        M("Dojo1_BackWall",d1x+2,    d1y+1,    46,  0.5f,dojoWood,"Untagged",ref created);
        M("Dojo1_RoofL",   d1x,      d1y+13,   28,  2,   dojoRoof,"Untagged",ref created);
        M("Dojo1_RoofR",   d1x+22,   d1y+15,   28,  2,   dojoRoof,"Untagged",ref created);
        M("Dojo1_RoofPeak",d1x+11,   d1y+17,   28,  1.5f,dojoRoof,"Untagged",ref created);
        M("Dojo1_Pillar1", d1x+2,    d1y+1,    1.5f,11,  dojoRoof,"Untagged",ref created);
        M("Dojo1_Pillar2", d1x+15,   d1y+1,    1.5f,10,  dojoRoof,"Untagged",ref created);
        M("Dojo1_Pillar3", d1x+33,   d1y+1,    1.5f,10,  dojoRoof,"Untagged",ref created);
        M("Dojo1_Pillar4", d1x+46,   d1y+1,    1.5f,11,  dojoRoof,"Untagged",ref created);
        M("Dojo1_Mat",     d1x+5,    d1y+0.8f,  40, 0.5f,dojoRoof,"Untagged",ref created);

        // ── SECTION 4 : Mountain Path → Dojo 2 ───────────────────────────────
        M("Mt2_CliffFace",       259,  45,   2.5f,60, mountainGray,"Ground", ref created);
        float[] lx2 = {261,269,277,285,293,301,309,317,325,333,341,349,357,365};
        float[] ly2 = { 31, 33, 35, 37, 39, 41, 43, 45, 47, 49, 51, 53, 55, 57};
        for (int i=0;i<lx2.Length;i++)
            M("Mt2_Ledge"+i, lx2[i],ly2[i],10f,1f,mountainLight,"Ground",ref created);
        M("Mt2_Snow1",           295,  42,  14,  0.5f,snow,      "Untagged",ref created);
        M("Mt2_Snow2",           330,  50,  12,  0.5f,snow,      "Untagged",ref created);
        M("Mt2_Snow3",           355,  56,   8,  0.5f,snow,      "Untagged",ref created);
        M("Mt2_Boulder1",        275,  34,   3,  1.5f,mountainGray,"Untagged",ref created);
        M("Mt2_Boulder2",        315,  44,   4,   2,  mountainGray,"Untagged",ref created);

        // ── SECTION 5 : Dojo 2 ───────────────────────────────────────────────
        float d2x=365, d2y=57;
        M("Dojo2_Floor",   d2x,      d2y,      55,  1.5f,dojoWood,"Ground",  ref created);
        M("Dojo2_WallL",   d2x,      d2y+1,     2,  14,  dojoWood,"Ground",  ref created);
        M("Dojo2_WallR",   d2x+53,   d2y+1,     2,  14,  dojoWood,"Ground",  ref created);
        M("Dojo2_BackWall",d2x+2,    d2y+1,    51,  0.5f,dojoWood,"Untagged",ref created);
        M("Dojo2_RoofL",   d2x,      d2y+14,   30,  2,   dojoRoof,"Untagged",ref created);
        M("Dojo2_RoofR",   d2x+25,   d2y+16,   30,  2,   dojoRoof,"Untagged",ref created);
        M("Dojo2_RoofPeak",d2x+12,   d2y+18,   31,  1.5f,dojoRoof,"Untagged",ref created);
        M("Dojo2_Pillar1", d2x+2,    d2y+1,    1.5f,12,  dojoRoof,"Untagged",ref created);
        M("Dojo2_Pillar2", d2x+17,   d2y+1,    1.5f,11,  dojoRoof,"Untagged",ref created);
        M("Dojo2_Pillar3", d2x+37,   d2y+1,    1.5f,11,  dojoRoof,"Untagged",ref created);
        M("Dojo2_Pillar4", d2x+51,   d2y+1,    1.5f,12,  dojoRoof,"Untagged",ref created);
        M("Dojo2_Mat",     d2x+5,    d2y+0.8f,  45, 0.5f,dojoRoof,"Untagged",ref created);

        // ── SECTION 6 : Mountain Descent to Cave ─────────────────────────────
        M("Descent_CliffFace",   418,  30,   2.5f,60, mountainGray,"Ground", ref created);
        float[] dx = {420,428,436,444,452,460,468,476,484,492,500};
        float[] dy = { 54, 48, 42, 36, 29, 22, 15,  8,  2, -3, -6};
        for (int i=0;i<dx.Length;i++)
            M("Descent_Ledge"+i, dx[i],dy[i],10f,1f,mountainLight,"Ground",ref created);

        // ── SECTION 7 : Cave Network ──────────────────────────────────────────
        M("Cave_Floor",          502, -7,  130, 2,   caveRock, "Ground",  ref created);
        M("Cave_Ceiling",        502, 14,  130, 3,   caveDark, "Ground",  ref created);
        M("Cave_EntranceWall",   500,  4,   3,  22,  caveRock, "Ground",  ref created);
        // Stalactites (decorative)
        M("Cave_Stal1",          515,  11, 1.5f,4,   caveDark, "Untagged",ref created);
        M("Cave_Stal2",          530,  12, 1.2f,5,   caveDark, "Untagged",ref created);
        M("Cave_Stal3",          548,  11, 1.8f,4,   caveDark, "Untagged",ref created);
        M("Cave_Stal4",          563,  12, 1.0f,5,   caveDark, "Untagged",ref created);
        M("Cave_Stal5",          580,  11, 1.4f,4,   caveDark, "Untagged",ref created);
        M("Cave_Stal6",          596,  12, 1.6f,5,   caveDark, "Untagged",ref created);
        // Stalagmites
        M("Cave_Stalag1",        520, -7,  1f,  2.5f,caveRock, "Untagged",ref created);
        M("Cave_Stalag2",        540, -7,  0.8f,2f,  caveRock, "Untagged",ref created);
        M("Cave_Stalag3",        575, -7,  1.2f,3f,  caveRock, "Untagged",ref created);
        // Inner chamber dividers
        M("Cave_Wall1",          545, -6,  2,   22,  caveRock, "Ground",  ref created);
        M("Cave_Wall2",          565, -6,  2,   22,  caveRock, "Ground",  ref created);
        // Ledge platforms inside cave
        M("Cave_Ledge1",         520,  4,  10,  1,   caveRock, "Ground",  ref created);
        M("Cave_Ledge2",         555,  0,  10,  1,   caveRock, "Ground",  ref created);
        M("Cave_Ledge3",         578,  6,  10,  1,   caveRock, "Ground",  ref created);

        // ── SECTION 8 : Save Point Room (Pulley Trap) ────────────────────────
        M("Save_Floor",          568, -7,  62,  2,   caveRock,  "Ground",  ref created);
        M("Save_Ceiling",        568, 16,  62,  2.5f,caveDark,  "Ground",  ref created);
        M("Save_WallL",          568,  4,  2.5f,24,  caveRock,  "Ground",  ref created);
        M("Save_WallR",          628,  4,  2.5f,24,  caveRock,  "Ground",  ref created);
        // Crystal checkpoint marker
        M("SavePoint_Crystal",   595, -5.5f,3,  4,   saveCrystal,"Untagged",ref created);
        M("SavePoint_Glow",      595, -1.5f,6,  0.3f,saveCrystal,"Untagged",ref created);
        // Pulley system: horizontal track, rope, stone
        M("Pulley_Track",        588, 14,  24,  0.6f,stoneTrap, "Untagged",ref created);
        M("Pulley_RopeL",        591,  6,  0.3f,9,   stoneTrap, "Untagged",ref created);
        M("Pulley_RopeR",        607,  6,  0.3f,9,   stoneTrap, "Untagged",ref created);
        M("CrushingStone",       597,  10,  8,  5,   stoneTrap, "Untagged",ref created);
        // Pulley wheel (small square at top of ropes)
        M("Pulley_WheelL",       591, 14,  1.2f,1.2f,stoneTrap,"Untagged",ref created);
        M("Pulley_WheelR",       607, 14,  1.2f,1.2f,stoneTrap,"Untagged",ref created);

        // ── SECTION 9 : Tsuchigumo's Cave (Boss Arena) ───────────────────────
        // Connecting tunnel
        M("Boss_Tunnel_Floor",   628, -7,  45,  2,   bossCave, "Ground",  ref created);
        M("Boss_Tunnel_Ceiling", 628, 16,  45,  2,   bossCave, "Ground",  ref created);
        M("Boss_Tunnel_WallR",   671,  4,  2.5f,24,  bossCave, "Ground",  ref created);
        // Descent into arena
        M("Boss_Descent1",       673,  0, 10,  1,   bossCave, "Ground",  ref created);
        M("Boss_Descent2",       683, -7, 10,  1,   bossCave, "Ground",  ref created);
        M("Boss_Descent3",       693,-14, 10,  1,   bossCave, "Ground",  ref created);
        // Boss arena
        float bx=696, by=-22;
        M("Boss_Floor",    bx,     by,    110, 2,   bossCave,  "Ground",  ref created);
        M("Boss_Ceiling",  bx,     by+42, 110, 3,   bossCave,  "Ground",  ref created);
        M("Boss_WallL",    bx,     by+20,   3, 44,  bossCave,  "Ground",  ref created);
        M("Boss_WallR",    bx+107, by+20,   3, 44,  bossCave,  "Ground",  ref created);
        // Arena platforms (give the player room to dodge)
        M("Boss_Platform1",bx+12,  by+10,  16,  1,  caveRock,  "Ground",  ref created);
        M("Boss_Platform2",bx+45,  by+18,  20,  1,  caveRock,  "Ground",  ref created);
        M("Boss_Platform3",bx+78,  by+10,  16,  1,  caveRock,  "Ground",  ref created);
        // Atmospheric web strands
        M("Boss_WebL1",    bx+3,   by+35,   9,  0.4f,bossCave, "Untagged",ref created);
        M("Boss_WebL2",    bx+3,   by+28,   6,  0.4f,bossCave, "Untagged",ref created);
        M("Boss_WebR1",    bx+98,  by+33,   9,  0.4f,bossCave, "Untagged",ref created);
        M("Boss_WebR2",    bx+101, by+26,   6,  0.4f,bossCave, "Untagged",ref created);
        // Tsuchigumo altar / spawn marker
        M("Boss_Altar",    bx+49,  by+1.5f, 12, 2.5f,new Color32(80,0,30,255),"Untagged",ref created);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return new CommandResponse { status = "success", message = $"Level built successfully! {created} objects created." };
    }

    // Shorthand alias for BuildLevelRemainder
    private static void M(string name, float cx, float cy, float w, float h, Color32 col, string tag, ref int count)
    {
        MakeLevelBlock(name, cx, cy, w, h, col, tag);
        count++;
    }

    private static void MakeLevelBlock(string name, float cx, float cy, float w, float h, Color32 color, string tag)
    {
        var go = new GameObject(name);

        // Apply tag safely
        try   { go.tag = tag; }
        catch { go.tag = "Untagged"; }

        // SpriteRenderer with Unity's built-in white square
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sr.sprite == null)
        {
            // fallback: try to find any white sprite in the project
            string[] guids = AssetDatabase.FindAssets("t:Sprite Square");
            if (guids.Length > 0)
                sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        sr.color = color;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 0;

        // Solid Ground objects get a BoxCollider2D
        if (tag == "Ground")
            go.AddComponent<BoxCollider2D>();

        go.transform.position   = new Vector3(cx, cy, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);

        Undo.RegisterCreatedObjectUndo(go, "LevelBuilder: " + name);
    }
}

