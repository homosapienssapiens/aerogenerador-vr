using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Recorre la secuencia desde el paso actual hasta "hasta" (exclusivo, 0 = todos) simulando la interacción real.
int hasta = 0; // se sobrescribe abajo
hasta = 0;
var sb = new StringBuilder();
var seq = GameObject.Find("Root/Curso Aerogenerador/SEQUENCE Manager").GetComponent<CompletableSequenceData>();
var items = seq.completableItems;
var grp = seq.assemblyGroup;
var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
var dummyInteractor = UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None).FirstOrDefault(i => !(i is XRSocketInteractor));
var hand = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None).First(i => i.transform.parent.name.StartsWith("Right"));

int Actual() { for (int i = 0; i < items.Count; i++) if (!items[i].Completed) return i; return items.Count; }
int limite = hasta == 0 ? items.Count : hasta;
int guard = 0;
while (Actual() < limite && guard++ < 40)
{
    int idx = Actual(); var it = items[idx];
    string accion;
    if (it is CompletableAssemblyTargetData)
    {
        var target = it.GetComponent<AssemblyTarget>();
        var part = grp.assemblyParts.First(p => p.partType == target.partType);
        var grab = part.GetComponent<XRGrabInteractable>();
        var sock = it.GetComponent<XRSocketInteractor>();
        var rb = part.GetComponent<Rigidbody>();
        manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        await Task.Delay(400);
        bool activoAlAgarrar = sock.socketActive;
        bool filtro = sock.startingSelectFilters.Count + sock.startingHoverFilters.Count >= 0;
        manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        part.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
        if (rb) { rb.position = target.transform.position; rb.rotation = target.transform.rotation; }
        await Task.Delay(100);
        if (!sock.hasSelection) manager.SelectEnter((IXRSelectInteractor)sock, (IXRSelectInteractable)grab);
        accion = "agarrar '" + part.name + "' (encaje activo al agarrar=" + activoAlAgarrar + ") y soltar en el objetivo";
    }
    else if (it is CompletableTriggerData trig)
    {
        var tool = GameObject.FindGameObjectsWithTag(trig.tagToCheck).First();
        var grab = tool.GetComponent<XRGrabInteractable>();
        var rb = tool.GetComponent<Rigidbody>();
        manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        await Task.Delay(300);
        grab.trackPosition = false;
        var from = tool.transform.position; float tm = Time.time;
        while (!it.Completed && Time.time - tm < 2f) { var p = Vector3.Lerp(from, it.transform.position, Mathf.Clamp01((Time.time - tm) / 1f)); rb.MovePosition(p); await Task.Delay(20); }
        grab.trackPosition = true;
        manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
        accion = "agarrar '" + tool.name + "' y llevarlo a la zona";
    }
    else
    {
        var xr = it.GetComponent<XRSimpleInteractable>();
        var col = it.GetComponentInChildren<Collider>(true);
        if (col != null && !col.enabled) { sb.AppendLine($"  !! {idx} {it.name}: el collider del clic está DESACTIVADO cuando le toca"); }
        var args = new SelectEnterEventArgs { manager = manager, interactorObject = dummyInteractor as IXRSelectInteractor, interactableObject = xr };
        xr.selectEntered.Invoke(args);
        var act = new ActivateEventArgs { interactorObject = dummyInteractor as IXRActivateInteractor, interactableObject = xr };
        xr.activated.Invoke(act);
        accion = "clic";
    }
    float t0 = Time.time;
    while (!it.Completed && Time.time - t0 < 4f) await Task.Delay(100);
    sb.AppendLine($"{idx,2} {(it.Completed ? "OK " : "NO ")} {it.name}  [{accion}] {(Time.time - t0):0.0}s");
    if (!it.Completed) break;
    await Task.Delay(300);
}
// estado visible
string A(string p) { var g = GameObject.Find(p); return g == null ? "(oculto/no existe)" : "visible"; }
var ctrl = GameObject.Find("Root/Scenery/Gondola/Elementos del procedimiento/Texto pantalla controlador")?.GetComponent<TMPro.TMP_Text>();
sb.AppendLine("pantalla controlador: " + ctrl?.text.Replace("\n", " | "));
var med = GameObject.Find("Medidor de tensión")?.GetComponentInChildren<TMPro.TMP_Text>();
sb.AppendLine("medidor: " + med?.text);
sb.AppendLine("clics con collider activo ahora: " + string.Join(", ", items.Where(i => !(i is CompletableAssemblyTargetData) && !(i is CompletableTriggerData) && i.GetComponentInChildren<Collider>(true).enabled).Select(i => i.name)));
sb.AppendLine("paso actual: " + (Actual() < items.Count ? items[Actual()].name : "SECUENCIA COMPLETA"));
return sb.ToString();
