using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aerogenerador;
using CustomCode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Prueba en Play de los errores críticos. Ejecutar recién entrado en Play (secuencia sin empezar).
var sb = new StringBuilder();
int fase = FASE;
if (fase == 1) await Task.Delay(8000);
var seq = GameObject.Find("Root/Curso Aerogenerador/SEQUENCE Manager").GetComponent<CompletableSequenceData>();
var items = seq.completableItems; var grp = seq.assemblyGroup;
var gestor = UnityEngine.Object.FindFirstObjectByType<GestorErroresCriticos>();
var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
var hand = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None).First(i => i.transform.parent.name.StartsWith("Right"));
var mouse = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None).First(i => i.GetType().Name == "XRMouseInteractor");

int Actual() { for (int i = 0; i < items.Count; i++) if (!items[i].Completed) return i; return items.Count; }

async Task<bool> Completar(int idx)
{
    var it = items[idx];
    if (it is CompletableAssemblyTargetData)
    {
        var target = it.GetComponent<AssemblyTarget>();
        var part = grp.assemblyParts.First(p => p.partType == target.partType);
        var grab = part.GetComponent<XRGrabInteractable>(); var rb = part.GetComponent<Rigidbody>();
        manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab); await Task.Delay(300);
        grab.trackPosition = false; grab.trackRotation = false;
        var from = part.transform.position; float t0 = Time.time;
        while (Time.time - t0 < 0.8f) { rb.MovePosition(Vector3.Lerp(from, it.transform.position, Mathf.Clamp01((Time.time - t0) / 0.6f))); await Task.Delay(20); }
        grab.trackPosition = true; grab.trackRotation = true;
        if (grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
    }
    else if (it is CompletableTriggerData trig)
    {
        var tool = GameObject.FindGameObjectsWithTag(trig.tagToCheck).First();
        var grab = tool.GetComponent<XRGrabInteractable>(); var rb = tool.GetComponent<Rigidbody>();
        manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab); await Task.Delay(300);
        grab.trackPosition = false; var from = tool.transform.position; float t0 = Time.time;
        while (!it.Completed && Time.time - t0 < 1.5f) { rb.MovePosition(Vector3.Lerp(from, it.transform.position, Mathf.Clamp01((Time.time - t0) / 0.8f))); await Task.Delay(20); }
        grab.trackPosition = true; if (grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
    }
    else
    {
        var xr = it.GetComponent<XRSimpleInteractable>();
        xr.selectEntered.Invoke(new SelectEnterEventArgs { manager = manager, interactorObject = hand, interactableObject = xr });
        xr.activated.Invoke(new ActivateEventArgs { interactorObject = hand, interactableObject = xr });
    }
    float t1 = Time.time; while (!it.Completed && Time.time - t1 < 3f) await Task.Delay(50);
    await Task.Delay(250);
    return it.Completed;
}
async Task AvanzarHasta(string prefijo)
{
    while (Actual() < items.Count && !items[Actual()].name.StartsWith(prefijo + " "))
        if (!await Completar(Actual())) { sb.AppendLine("  !! no se pudo completar " + items[Actual()].name); return; }
}
Transform Buscar(string nombre) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == nombre);

async Task Intento(string caso, string objeto, string esperado)
{
    await Task.Delay(2200); // supera la espera entre errores de una misma regla
    int antes = gestor.Total;
    var go = Buscar(objeto).gameObject;
    var part = go.GetComponent<AssemblyPart>();
    Vector3 inicio = part != null && part.startTransform != null ? part.startTransform.position : go.transform.position;
    var grab = go.GetComponent<XRGrabInteractable>();
    if (grab != null)
    {
        manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        await Task.Delay(600);
        bool suelto = !grab.isSelected;
        float dist = Vector3.Distance(go.transform.position, inicio);
        if (grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        var nuevo = gestor.Total > antes ? gestor.registro.Last().id : "ninguno";
        sb.AppendLine($"{(nuevo == esperado ? "OK " : "FALLO")} {caso}: agarrar '{objeto}' → error={nuevo} (esperado {esperado}); soltado por el sistema={suelto}, vuelve a su sitio={(dist < 0.05f)}");
    }
    else
    {
        var xr = go.GetComponent<XRSimpleInteractable>();
        xr.selectEntered.Invoke(new SelectEnterEventArgs { manager = manager, interactorObject = hand, interactableObject = xr });
        await Task.Delay(400);
        var nuevo = gestor.Total > antes ? gestor.registro.Last().id : "ninguno";
        sb.AppendLine($"{(nuevo == esperado ? "OK " : "FALLO")} {caso}: pulsar '{objeto}' → error={nuevo} (esperado {esperado})");
    }
}

sb.AppendLine($"[fase {fase}] paso actual al empezar: " + (Actual() < items.Count ? items[Actual()].name : "fin"));
var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
if (fase == 1)
{
    await Intento("E1", "Escobilla gastada 1", "rotor-no-bloqueado");
    await Intento("E1", "Tapa de acceso", "rotor-no-bloqueado");
    await Intento("sin error", "Seccionador cerrado", "ninguno");
    // E3: entrar en la góndola sin el arnés anclado
    await Task.Delay(500);
    int antesZona = gestor.Total;
    origin.MoveCameraToWorldLocation(new Vector3(-1.2f, 1.7f, 1.0f));
    float tz = Time.time; while (Time.time - tz < 3f && Camera.main.transform.position.z > -4f) await Task.Delay(50);
    var cam = Camera.main.transform.position;
    await Task.Delay(1500);
    sb.AppendLine($"{(gestor.Total == antesZona + 1 && gestor.registro.Last().id == "arnes-sin-anclar" ? "OK " : "FALLO")} E3: entrar sin arnés → errores nuevos={gestor.Total - antesZona} ({(gestor.Total > antesZona ? gestor.registro.Last().id : "ninguno")}); vuelve a la entrada en {(Time.time - tz - 1.5f):0.00}s, z={cam.z:0.00}");
}
if (fase == 2)
{
    await AvanzarHasta("4b."); // freno activado, sin pasador
    await Intento("E2", "Candado", "solo-freno");
    await Intento("E1 con freno", "Carcasa del generador", "rotor-no-bloqueado");
    int antesZona2 = gestor.Total;
    origin.MoveCameraToWorldLocation(new Vector3(-1.2f, 1.7f, 1.0f)); await Task.Delay(1200);
    sb.AppendLine($"{(gestor.Total == antesZona2 ? "OK " : "FALLO")} sin error: moverse con el arnés anclado → errores nuevos={gestor.Total - antesZona2}");
}
if (fase == 3)
{
    await AvanzarHasta("5a.");
    await Intento("regla 1", "Medidor de tensión", "regla-de-oro");
    await AvanzarHasta("5b.");
    await Intento("regla 2", "Tarjeta de señalización", "regla-de-oro");
    await AvanzarHasta("6.");
    await Intento("regla 3", "Equipo de puesta a tierra", "sin-verificar-tension");
}
if (fase == 4)
{
    await AvanzarHasta("7b.");
    await Intento("regla 5", "Tapa de acceso", "regla-de-oro");
    await AvanzarHasta("8b.");
    await Intento("sin error", "Escobilla gastada 1", "ninguno");
}
if (fase == 5)
{
    await AvanzarHasta("9c.");
    await Intento("reposición", "Seccionador abierto", "reponer-fuera-de-orden");
    await Intento("reposición", "Maneta accionada", "liberar-rotor-antes");
    await AvanzarHasta("FIN");
}
sb.AppendLine("Secuencia: " + (Actual() >= items.Count ? "COMPLETA" : "en " + items[Actual()].name));
sb.AppendLine("Registro de errores (" + gestor.Total + "): " + string.Join(" | ", gestor.registro.Select(r => $"p{r.paso} {r.id} [{r.objeto}]")));
var notif = GameObject.Find("Notificación de error (VIROO)");
var textos = notif ? notif.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text.Replace("\n", " / ")) : new string[0];
sb.AppendLine("Notificación: " + string.Join(" || ", textos));
return sb.ToString();
