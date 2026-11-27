using System;
using System.Collections.Generic;
using System.Linq;
using CustomCode;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// ---------- utilidades ----------
var lit = Shader.Find("Universal Render Pipeline/Lit");
Material M(string n, Color c, float metal = 0f, float smooth = 0.4f, bool emit = false)
{
    var p = "Assets/Aerogenerador/Materiales/" + n + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
    if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, p); }
    m.SetColor("_BaseColor", c); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
    if (emit) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2f); }
    EditorUtility.SetDirty(m); return m;
}
Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Aerogenerador/Materiales/" + n + ".mat");
Transform G(string n, Transform parent) { var t = new GameObject(n).transform; t.SetParent(parent, false); return t; }
GameObject B(string n, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? rot = null)
{
    var go = GameObject.CreatePrimitive(type); go.name = n; go.transform.SetParent(parent, false);
    go.transform.position = pos; go.transform.localScale = scale; go.transform.eulerAngles = rot ?? Vector3.zero;
    go.GetComponent<Renderer>().sharedMaterial = m; return go;
}
GameObject F(string path) { var g = GameObject.Find(path); if (g == null) throw new Exception("No encontrado: " + path); return g; }
TextMeshPro Texto(string n, Transform parent, Vector3 pos, float rotY, string txt, float size, Color color, Vector2 area)
{
    var go = new GameObject(n); go.transform.SetParent(parent, false);
    go.transform.position = pos; go.transform.eulerAngles = new Vector3(0, rotY, 0);
    var t = go.AddComponent<TextMeshPro>(); t.text = txt; t.fontSize = size; t.color = color;
    t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.Normal;
    t.rectTransform.sizeDelta = area; return t;
}
void NuevoId(Component c)
{
    var u = c.GetComponent<UniqueID>() ?? c.gameObject.AddComponent<UniqueID>();
    var so = new SerializedObject(u); so.FindProperty("guid").stringValue = Guid.NewGuid().ToString(); so.ApplyModifiedPropertiesWithoutUndo();
}

// ---------- materiales nuevos ----------
var mBlanco = M("Casco_Blanco", new Color(0.95f, 0.95f, 0.92f));
var mAislante = M("Pantalla_Aislante", new Color(0.95f, 0.55f, 0.15f), 0f, 0.6f);
var mNueva = M("Escobilla_Nueva", new Color(0.45f, 0.45f, 0.48f), 0.1f, 0.3f);
var mTarjeta = M("Tarjeta_Roja", new Color(0.85f, 0.1f, 0.1f));
var mPantalla = M("Pantalla_Oscura", new Color(0.05f, 0.08f, 0.1f), 0f, 0.8f);
var mAmarillo = Mat("Seguridad_Amarillo"); var mRojo = Mat("Seguridad_Rojo"); var mNaranja = Mat("Anclaje_Naranja");
var mAcero = Mat("Acero"); var mCobre = Mat("Cobre"); var mGrafito = Mat("Escobilla_Grafito"); var mCuadro = Mat("Cuadro_Electrico");
var mVerde = Mat("Piloto_Verde"); var mNegro = Mat("Negro"); var mGen = Mat("Generador");

// ---------- configuración de la plantilla ----------
var cfg = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Code/Editor/AssemblyPartEditor/Resources/Config/AssemblyPartEditorConfig.asset");
var cfgSo = new SerializedObject(cfg);
var partFeedbackMat = (Material)cfgSo.FindProperty("partFeedbackMaterial").objectReferenceValue;
var startFeedbackMat = (Material)cfgSo.FindProperty("startFeedbackMaterial").objectReferenceValue;
var targetFeedbackMat = (Material)cfgSo.FindProperty("targetFeedbackMaterial").objectReferenceValue;
var targetHoverMat = (Material)cfgSo.FindProperty("targetFeedbackHoverMaterial").objectReferenceValue;
var targetSM = (Unity.VisualScripting.ScriptGraphAsset)cfgSo.FindProperty("assemblyTargetCompletableSM").objectReferenceValue;
var clickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Completables/Clickable_Completion.prefab");
var toolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Completables/Tool_Completion.prefab");

// ---------- etiqueta del medidor ----------
var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var tagsProp = tagManager.FindProperty("tags");
bool hasTag = false; for (int i = 0; i < tagsProp.arraySize; i++) if (tagsProp.GetArrayElementAtIndex(i).stringValue == "Medidor") hasTag = true;
if (!hasTag) { tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize); tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "Medidor"; tagManager.ApplyModifiedPropertiesWithoutUndo(); }

// ---------- limpiar construcción anterior ----------
var pasos = F("Root/Curso Aerogenerador/PASOS").transform;
foreach (var t in pasos.Cast<Transform>().ToList()) UnityEngine.Object.DestroyImmediate(t.gameObject);
var group = pasos.GetComponent<AssemblyGroup>();
group.assemblyParts.Clear(); group.assemblyTargets.Clear();
var gond = F("Root/Scenery/Gondola").transform;
var oldExtra = gond.Find("Elementos del procedimiento"); if (oldExtra) UnityEngine.Object.DestroyImmediate(oldExtra.gameObject);
var extra = G("Elementos del procedimiento", gond);

// ---------- piezas de la plantilla (igual que AssemblyPartsEditor) ----------
AssemblyPart Pieza(GameObject go, string tipo)
{
    var part = go.AddComponent<AssemblyPart>(); group.assemblyParts.Add(part);
    part.id = go.name; part.partType = tipo;
    var fb = new GameObject("FEEDBACK " + go.name); fb.transform.SetParent(go.transform, false);
    var inst = fb.AddComponent<InstancedRenderersHierarchy>(); inst.material = partFeedbackMat; inst.source = go; inst.editorOnly = false;
    go.AddComponent<FeedBack>().feedbackGameObject = fb;
    var start = new GameObject("START " + go.name); start.transform.SetParent(go.transform.parent, false);
    start.transform.position = go.transform.position; start.transform.rotation = go.transform.rotation; start.transform.localScale = go.transform.localScale;
    var si = start.AddComponent<InstancedRenderersHierarchy>(); si.material = startFeedbackMat; si.source = go; si.editorOnly = true;
    part.startTransform = start.transform;
    NuevoId(part);
    return part;
}
CompletableItemData Objetivo(string nombre, AssemblyPart part, Vector3 pos, Vector3 rot, Transform parent)
{
    var go = new GameObject(nombre); go.transform.SetParent(parent, false);
    go.transform.position = pos; go.transform.eulerAngles = rot; go.transform.localScale = part.transform.lossyScale;
    var mesh = part.GetComponent<MeshFilter>().sharedMesh;
    var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = mesh.bounds.center; box.size = mesh.bounds.size * 1.6f;
    var at = go.AddComponent<AssemblyTarget>(); at.partType = part.partType; at.feedbackMaterial = targetFeedbackMat; at.feedbackMaterialHover = targetHoverMat;
    var cd = go.AddComponent<CompletableAssemblyTargetData>();
    cd.distanceTolerance = 2; cd.angleTolerance = 180; cd.disableInteractableWhenPlaced = true;
    var sm = go.AddComponent<Unity.VisualScripting.ScriptMachine>(); sm.nest.SwitchToMacro(targetSM);
    var fbGo = new GameObject(part.name + "-feedback"); fbGo.transform.SetParent(go.transform, false);
    var inst = fbGo.AddComponent<InstancedRenderersHierarchy>(); inst.material = targetFeedbackMat; inst.source = part.gameObject; inst.editorOnly = false;
    go.AddComponent<FeedBack>().feedbackGameObject = fbGo;
    group.assemblyTargets.Add(at);
    NuevoId(cd);
    return cd;
}
var clickColliders = new Dictionary<CompletableItemData, Collider>();
CompletableItemData Clic(string nombre, Vector3 pos, float diametro, Transform parent)
{
    var go = (GameObject)PrefabUtility.InstantiatePrefab(clickPrefab, parent);
    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    go.name = nombre; go.transform.position = pos; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one * diametro;
    var sphere = go.transform.Find("Sphere");
    var r = sphere.GetComponent<MeshRenderer>(); if (r) UnityEngine.Object.DestroyImmediate(r);
    var col = sphere.GetComponent<Collider>(); col.enabled = false;
    var cd = go.GetComponent<CompletableItemData>(); NuevoId(cd);
    clickColliders[cd] = col; return cd;
}
CompletableItemData Herramienta(string nombre, Vector3 pos, float radio, string tag, Transform parent)
{
    var go = (GameObject)PrefabUtility.InstantiatePrefab(toolPrefab, parent);
    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    go.name = nombre; go.transform.position = pos; go.transform.localScale = Vector3.one * radio * 2f;
    var cd = go.GetComponent<CompletableTriggerData>(); cd.tagToCheck = tag; NuevoId(cd); return cd;
}
UnityEvent Ev(CompletableItemData cd) { if (cd.onCompleted == null) cd.onCompleted = new UnityEvent(); return cd.onCompleted; }
void AlCompletar(CompletableItemData cd, GameObject go, bool activo) => UnityEventTools.AddBoolPersistentListener(Ev(cd), go.SetActive, activo);
void AlCompletarTexto(CompletableItemData cd, TMP_Text t, string txt) => UnityEventTools.AddStringPersistentListener(Ev(cd), new UnityAction<string>(t.SetText), txt);
void AlCompletarCollider(CompletableItemData cd, Collider c, bool on) => UnityEventTools.AddBoolPersistentListener(Ev(cd), (UnityAction<bool>)Delegate.CreateDelegate(typeof(UnityAction<bool>), c, "set_enabled"), on);

var secuencia = new List<CompletableItemData>();

// ======================================================================
// Paso 1 · Orden de trabajo y viento
// ======================================================================
var p1 = G("Paso 1 · Orden de trabajo", pasos);
var otTxt = Texto("Texto orden de trabajo", extra, new Vector3(1.755f, 1.58f, -5.4f), 90f,
    "<b>ORDEN DE TRABAJO OT-0427</b>\nAerogenerador WTG-07 · Góndola\n\nTarea: cambio de escobillas del generador\n\nViento actual: <b>8 m/s</b>\nLímite para intervenir: 12 m/s\n\n<size=70%>Confirma la orden para empezar</size>",
    1.1f, Color.black, new Vector2(0.75f, 0.55f));
var otBoton = B("Botón confirmar orden", PrimitiveType.Cylinder, extra, new Vector3(1.74f, 1.18f, -5.4f), new Vector3(0.08f, 0.02f, 0.08f), mVerde, new Vector3(0, 0, 90));
var c1 = Clic("1. Confirmar la orden de trabajo", otBoton.transform.position, 0.14f, p1);
AlCompletarTexto(c1, otTxt, "<b>ORDEN DE TRABAJO OT-0427</b>\n\n<color=#1a7f2e><b>CONFIRMADA</b></color>\n\nViento: 8 m/s (límite 12 m/s)\nSe puede intervenir");
secuencia.Add(c1);

// ======================================================================
// Paso 2 · EPI y anclaje del arnés
// ======================================================================
var p2 = G("Paso 2 · EPI y arnés", pasos);
var casco = B("Casco", PrimitiveType.Sphere, extra, new Vector3(-1.6f, 1.13f, -5.75f), new Vector3(0.24f, 0.17f, 0.26f), mBlanco);
var guantes = B("Guantes", PrimitiveType.Cube, extra, new Vector3(-1.6f, 1.05f, -5.5f), new Vector3(0.16f, 0.05f, 0.2f), mAmarillo);
var arnes = B("Arnés", PrimitiveType.Cube, extra, new Vector3(-1.6f, 1.09f, -5.28f), new Vector3(0.22f, 0.13f, 0.16f), mNaranja);
var c2a = Clic("2a. Ponerse el casco", casco.transform.position, 0.32f, p2); AlCompletar(c2a, casco, false);
var c2b = Clic("2b. Ponerse los guantes", guantes.transform.position, 0.26f, p2); AlCompletar(c2b, guantes, false);
var c2c = Clic("2c. Ponerse el arnés", arnes.transform.position, 0.28f, p2); AlCompletar(c2c, arnes, false);
var mosq = B("Mosquetón", PrimitiveType.Cube, p2, new Vector3(-1.62f, 1.07f, -5.08f), new Vector3(0.05f, 0.1f, 0.03f), mAcero);
var mosqPart = Pieza(mosq, "Mosqueton");
var c2d = Objetivo("2d. Anclar el arnés al punto de anclaje", mosqPart, new Vector3(-1.64f, 1.93f, -4.6f), Vector3.zero, p2);
secuencia.AddRange(new[] { c2a, c2b, c2c, c2d });

// ======================================================================
// Paso 3 · Modo mantenimiento
// ======================================================================
var p3 = G("Paso 3 · Modo mantenimiento", pasos);
var ctrlTxt = Texto("Texto pantalla controlador", extra, new Vector3(-1.425f, 1.65f, -0.8f), -90f,
    "<b>WTG-07</b>\nEstado: <color=#2ecc40>PRODUCCIÓN</color>\nRotor: 14 rpm", 0.9f, Color.white, new Vector2(0.5f, 0.3f));
var btnParada = F("Root/Scenery/Gondola/Controlador/Botón Parada");
var btnMant = F("Root/Scenery/Gondola/Controlador/Botón Mantenimiento");
var btnProd = F("Root/Scenery/Gondola/Controlador/Botón Producción");
var c3a = Clic("3a. Parar la turbina", btnParada.transform.position, 0.14f, p3);
AlCompletarTexto(c3a, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#ff4136>PARADA</color>\nRotor: 2 rpm (girando en vacío)");
var c3b = Clic("3b. Pasar a modo mantenimiento", btnMant.transform.position, 0.14f, p3);
AlCompletarTexto(c3b, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#ffdc00>MANTENIMIENTO</color>\nRotor: 2 rpm (girando en vacío)");
secuencia.AddRange(new[] { c3a, c3b });

// ======================================================================
// Paso 4 · Freno + pasador de bloqueo
// ======================================================================
var p4 = G("Paso 4 · Freno y pasador", pasos);
var palanca = F("Root/Scenery/Gondola/Freno de disco/Palanca del freno").transform;
var suelta = G("Maneta suelta", palanca); suelta.localPosition = Vector3.zero;
palanca.Find("Maneta").SetParent(suelta, true); palanca.Find("Pomo").SetParent(suelta, true);
var accionada = UnityEngine.Object.Instantiate(suelta.gameObject, palanca).transform; accionada.name = "Maneta accionada";
accionada.localPosition = Vector3.zero; accionada.RotateAround(palanca.position + Vector3.up * 1.0f, Vector3.right, -55f);
accionada.gameObject.SetActive(false);
var c4a = Clic("4a. Activar el freno de disco", palanca.position + Vector3.up * 1.2f, 0.32f, p4);
AlCompletar(c4a, suelta.gameObject, false); AlCompletar(c4a, accionada.gameObject, true);
AlCompletarTexto(c4a, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#ffdc00>MANTENIMIENTO</color>\nFreno: ACTIVADO\nRotor: 0 rpm");
var pasador = B("Pasador de bloqueo", PrimitiveType.Cylinder, p4, new Vector3(1.66f, 1.36f, 2.3f), new Vector3(0.09f, 0.2f, 0.09f), mRojo, new Vector3(90, 0, 0));
var pasadorPart = Pieza(pasador, "Pasador");
var c4b = Objetivo("4b. Bloquear el rotor con el pasador", pasadorPart, new Vector3(0.98f, 1.3f, 2.86f), new Vector3(90, 0, 0), p4);
AlCompletarTexto(c4b, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#ffdc00>MANTENIMIENTO</color>\nFreno: ACTIVADO\nRotor: BLOQUEADO");
secuencia.AddRange(new[] { c4a, c4b });

// ======================================================================
// Paso 5 · Desconectar, bloquear y señalizar
// ======================================================================
var p5 = G("Paso 5 · Desconectar, bloquear, señalizar", pasos);
var secc = F("Root/Scenery/Gondola/Cuadro de desconexión/Seccionador");
secc.name = "Seccionador cerrado";
var seccAbierto = UnityEngine.Object.Instantiate(secc, secc.transform.parent); seccAbierto.name = "Seccionador abierto";
seccAbierto.transform.position = secc.transform.position; seccAbierto.transform.eulerAngles = new Vector3(90, 0, 0);
seccAbierto.GetComponent<Renderer>().sharedMaterial = mVerde; seccAbierto.SetActive(false);
var c5a = Clic("5a. Abrir el seccionador del generador", secc.transform.position, 0.4f, p5);
AlCompletar(c5a, secc, false); AlCompletar(c5a, seccAbierto, true);
var candado = B("Candado", PrimitiveType.Cube, p5, new Vector3(-1.45f, 0.92f, -3.45f), new Vector3(0.03f, 0.08f, 0.06f), mRojo);
var candadoPart = Pieza(candado, "Candado");
var c5b = Objetivo("5b. Bloquear con el candado", candadoPart, new Vector3(1.37f, 1.1f, -4.4f), Vector3.zero, p5);
var tarjeta = B("Tarjeta de señalización", PrimitiveType.Cube, p5, new Vector3(-1.45f, 0.885f, -3.25f), new Vector3(0.07f, 0.01f, 0.12f), mTarjeta);
var tarjetaPart = Pieza(tarjeta, "Tarjeta");
var c5c = Objetivo("5c. Colocar la tarjeta de señalización", tarjetaPart, new Vector3(1.39f, 0.98f, -4.4f), new Vector3(0, 0, 90), p5);
secuencia.AddRange(new[] { c5a, c5b, c5c });

// ======================================================================
// Paso 6 · Verificar ausencia de tensión
// ======================================================================
var p6 = G("Paso 6 · Verificar ausencia de tensión", pasos);
var medidor = B("Medidor de tensión", PrimitiveType.Cube, p6, new Vector3(-1.45f, 0.96f, -3.0f), new Vector3(0.08f, 0.15f, 0.04f), mAmarillo);
medidor.tag = "Medidor";
var medTxt = Texto("Pantalla del medidor", medidor.transform, medidor.transform.position + new Vector3(0, 0.03f, -0.021f), 0f, "--- V", 0.35f, Color.black, new Vector2(0.06f, 0.04f));
medTxt.transform.localScale = new Vector3(1f / 0.08f, 1f / 0.15f, 1f / 0.04f) * 0.08f;
Pieza(medidor, "Medidor");
var c6 = Herramienta("6. Verificar la ausencia de tensión en los bornes", new Vector3(1.15f, 1.45f, -2.3f), 0.22f, "Medidor", p6);
AlCompletarTexto(c6, medTxt, "0 V");
secuencia.Add(c6);

// ======================================================================
// Paso 7 · Tierra y protección
// ======================================================================
var p7 = G("Paso 7 · Tierra y protección", pasos);
B("Embarrado próximo en tensión", PrimitiveType.Cube, extra, new Vector3(1.74f, 1.5f, -3.2f), new Vector3(0.12f, 0.5f, 0.6f), mCobre);
var pinza = B("Equipo de puesta a tierra", PrimitiveType.Cube, p7, new Vector3(-1.45f, 0.92f, -2.78f), new Vector3(0.06f, 0.08f, 0.22f), mVerde);
var pinzaPart = Pieza(pinza, "PuestaTierra");
var c7a = Objetivo("7a. Poner a tierra y en cortocircuito", pinzaPart, new Vector3(1.2f, 1.45f, -2.2f), new Vector3(0, 0, 90), p7);
var pantalla = B("Pantalla aislante", PrimitiveType.Cube, p7, new Vector3(-1.7f, 0.33f, -1.9f), new Vector3(0.03f, 0.6f, 0.6f), mAislante);
var pantallaPart = Pieza(pantalla, "PantallaAislante");
var c7b = Objetivo("7b. Proteger el embarrado próximo con la pantalla", pantallaPart, new Vector3(1.6f, 1.5f, -3.2f), Vector3.zero, p7);
secuencia.AddRange(new[] { c7a, c7b });

// ======================================================================
// Paso 8 · Cambiar las escobillas
// ======================================================================
var p8 = G("Paso 8 · Cambiar escobillas", pasos);
var tapa = F("Root/Scenery/Gondola/Generador/Anillos rozantes/Tapa de acceso");
var tapaAbierta = UnityEngine.Object.Instantiate(tapa, tapa.transform.parent); tapaAbierta.name = "Tapa de acceso (retirada)";
tapaAbierta.transform.position = new Vector3(0.72f, 0.31f, -3.9f); tapaAbierta.transform.eulerAngles = new Vector3(0, 0, 80); tapaAbierta.SetActive(false);
var c8a = Clic("8a. Retirar la tapa de los anillos rozantes", tapa.transform.position, 0.7f, p8);
AlCompletar(c8a, tapa, false); AlCompletar(c8a, tapaAbierta, true);
secuencia.Add(c8a);
B("Bandeja de escobillas usadas", PrimitiveType.Cube, extra, new Vector3(-0.95f, 1.22f, -3.9f), new Vector3(0.25f, 0.03f, 0.45f), mAcero);
B("Soporte de la bandeja", PrimitiveType.Cube, extra, new Vector3(-0.95f, 0.6f, -3.9f), new Vector3(0.05f, 1.2f, 0.05f), mAcero);
float[] xs = { -0.25f, 0f, 0.25f }; float[] zs = { -4.04f, -3.9f, -3.76f };
var gastadas = new List<CompletableItemData>(); var nuevas = new List<CompletableItemData>();
for (int i = 0; i < 3; i++)
{
    var g = B("Escobilla gastada " + (i + 1), PrimitiveType.Cube, p8, new Vector3(xs[i], 2.02f, -3.9f), new Vector3(0.05f, 0.08f, 0.07f), mGrafito);
    var gp = Pieza(g, "EscobillaGastada" + (i + 1));
    gastadas.Add(Objetivo("8b. Retirar la escobilla gastada " + (i + 1), gp, new Vector3(-0.95f, 1.28f, zs[i]), Vector3.zero, p8));
}
for (int i = 0; i < 3; i++)
{
    var n = B("Escobilla nueva " + (i + 1), PrimitiveType.Cube, p8, new Vector3(-1.66f + i * 0.13f, 0.92f, -2.52f), new Vector3(0.05f, 0.08f, 0.07f), mNueva);
    var np = Pieza(n, "EscobillaNueva" + (i + 1));
    nuevas.Add(Objetivo("8c. Colocar la escobilla nueva " + (i + 1), np, new Vector3(xs[i], 2.02f, -3.9f), Vector3.zero, p8));
}
secuencia.AddRange(gastadas); secuencia.AddRange(nuevas);
var c8d = Clic("8d. Volver a colocar la tapa", tapaAbierta.transform.position, 0.7f, p8);
AlCompletar(c8d, tapaAbierta, false); AlCompletar(c8d, tapa, true);
secuencia.Add(c8d);

// ======================================================================
// Paso 9 · Reponer en orden inverso y liberar el rotor
// ======================================================================
var p9 = G("Paso 9 · Reponer en orden inverso", pasos);
var c9a = Clic("9a. Retirar la pantalla aislante", new Vector3(1.6f, 1.5f, -3.2f), 0.75f, p9); AlCompletar(c9a, pantalla, false);
var c9b = Clic("9b. Retirar la puesta a tierra", new Vector3(1.2f, 1.45f, -2.2f), 0.3f, p9); AlCompletar(c9b, pinza, false);
var c9c = Clic("9c. Retirar la tarjeta y el candado", new Vector3(1.38f, 1.05f, -4.4f), 0.3f, p9); AlCompletar(c9c, tarjeta, false); AlCompletar(c9c, candado, false);
var c9d = Clic("9d. Cerrar el seccionador", secc.transform.position, 0.4f, p9); AlCompletar(c9d, seccAbierto, false); AlCompletar(c9d, secc, true);
var c9e = Clic("9e. Retirar el pasador de bloqueo", new Vector3(0.98f, 1.3f, 2.86f), 0.3f, p9); AlCompletar(c9e, pasador, false);
AlCompletarTexto(c9e, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#ffdc00>MANTENIMIENTO</color>\nFreno: ACTIVADO\nRotor: 0 rpm");
var c9f = Clic("9f. Liberar el freno", palanca.position + Vector3.up * 1.2f, 0.32f, p9); AlCompletar(c9f, accionada.gameObject, false); AlCompletar(c9f, suelta.gameObject, true);
AlCompletarTexto(c9f, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#ffdc00>MANTENIMIENTO</color>\nFreno: LIBERADO\nRotor: 2 rpm");
var c9g = Clic("9g. Volver a modo producción", btnProd.transform.position, 0.14f, p9);
AlCompletarTexto(c9g, ctrlTxt, "<b>WTG-07</b>\nEstado: <color=#2ecc40>PRODUCCIÓN</color>\nRotor: 14 rpm");
secuencia.AddRange(new[] { c9a, c9b, c9c, c9d, c9e, c9f, c9g });

// ---------- cadena de activación de los clics: solo responde el clic del paso actual ----------
for (int i = 0; i < secuencia.Count; i++)
{
    var cd = secuencia[i];
    if (clickColliders.TryGetValue(cd, out var own)) AlCompletarCollider(cd, own, false);
    if (i + 1 < secuencia.Count && clickColliders.TryGetValue(secuencia[i + 1], out var next)) AlCompletarCollider(cd, next, true);
}
if (clickColliders.TryGetValue(secuencia[0], out var first)) first.enabled = true;

// ---------- secuencia ----------
var seq = F("Root/Curso Aerogenerador/SEQUENCE Manager").GetComponent<CompletableSequenceData>();
seq.completableItems = secuencia;
EditorUtility.SetDirty(seq); EditorUtility.SetDirty(group);
AssetDatabase.SaveAssets();
EditorSceneManager.MarkSceneDirty(pasos.gameObject.scene);
EditorSceneManager.SaveScene(pasos.gameObject.scene);
return "pasos en la secuencia: " + secuencia.Count + " | piezas: " + group.assemblyParts.Count + " | objetivos: " + group.assemblyTargets.Count + " | clics: " + clickColliders.Count;
