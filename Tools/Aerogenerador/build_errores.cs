using System;
using System.Collections.Generic;
using System.Linq;
using Aerogenerador;
using CustomCode;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Construye los errores críticos de 2_aprendizaje/errores-criticos.md sobre la escena Aerogenerador.
// Idempotente: borra y vuelve a crear el grupo «Errores críticos».

Transform Hijo(Transform raiz, string nombre)
{
    foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
    throw new Exception("No encontrado: " + nombre + " bajo " + raiz.name);
}
var gond = GameObject.Find("Root/Scenery/Gondola").transform;
var curso = GameObject.Find("Root/Curso Aerogenerador").transform;
var pasos = Hijo(curso, "PASOS");
var seq = Hijo(curso, "SEQUENCE Manager").GetComponent<CompletableSequenceData>();
CompletableItemData P(string prefijo) => seq.completableItems.Single(c => c.name.StartsWith(prefijo + " "));

// ---------- detectores en el escenario (pulsar con el rayo / la mano) ----------
GameObject Detector(string nombre)
{
    var go = Hijo(gond, nombre).gameObject;
    if (go.GetComponent<XRSimpleInteractable>() == null) go.AddComponent<XRSimpleInteractable>();
    return go;
}
GameObject Pieza(string nombre) => Hijo(pasos, nombre).gameObject; // reciben XRGrabInteractable en ejecución

var generador = new[] { "Carcasa del generador", "Caja de bornes", "Borne L1", "Borne L2", "Borne L3", "Borne de tierra",
    "Carcasa de anillos", "Tapa de acceso", "Portaescobillas 1", "Portaescobillas 2", "Portaescobillas 3" }.Select(Detector).ToList();
var tapa = Detector("Tapa de acceso");
var bornes = new[] { "Caja de bornes", "Borne L1", "Borne L2", "Borne L3", "Borne de tierra" }.Select(Detector).ToList();
var seccCerrado = Detector("Seccionador cerrado");
var seccAbierto = Detector("Seccionador abierto");
var manetaAccionada = Detector("Maneta accionada");
var alojamiento = Detector("Alojamiento del pasador");

var gastadas = Enumerable.Range(1, 3).Select(i => Pieza("Escobilla gastada " + i)).ToList();
var candado = Pieza("Candado"); var tarjeta = Pieza("Tarjeta de señalización"); var medidor = Pieza("Medidor de tensión");
var tierra = Pieza("Equipo de puesta a tierra"); var pantalla = Pieza("Pantalla aislante");

// ---------- grupo, gestor, notificación VIROO y punto seguro ----------
var viejo = curso.Find("Errores críticos"); if (viejo) UnityEngine.Object.DestroyImmediate(viejo.gameObject);
var grupo = new GameObject("Errores críticos").transform; grupo.SetParent(curso, false);
var gestor = grupo.gameObject.AddComponent<GestorErroresCriticos>();
var audio = grupo.gameObject.AddComponent<AudioSource>();
audio.playOnAwake = false; audio.spatialBlend = 0f;
audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Media/AudioClips/ping-82822.mp3");
gestor.sonido = audio;

var notifPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.viroo.interactions/Runtime/Viroo.Interactions.Unity/Prefabs/Notification/BasicNotification.prefab");
var notifGo = (GameObject)PrefabUtility.InstantiatePrefab(notifPrefab, grupo);
notifGo.name = "Notificación de error (VIROO)";
var notif = notifGo.GetComponents<MonoBehaviour>().First(m => m.GetType().Name == "BasicNotification");
var nso = new SerializedObject(notif);
nso.FindProperty("startVisible").boolValue = false;
nso.FindProperty("autoHide").boolValue = true;
nso.FindProperty("autoHideTime").floatValue = 8f;
nso.FindProperty("delay").intValue = 0; // un error crítico se avisa al instante
nso.ApplyModifiedPropertiesWithoutUndo();
// El prefab viene en la capa SceneLoadingVisualizer; el gestor la dibuja con una cámara superpuesta (URP)
// para que ningún objeto de la góndola tape el aviso.
gestor.capaAviso = LayerMask.LayerToName(notifGo.GetComponentInChildren<Canvas>(true).gameObject.layer);
UnityEventTools.AddPersistentListener(gestor.onTitulo, (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), notif, "ChangeTitle"));
UnityEventTools.AddPersistentListener(gestor.onTexto, (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), notif, "ChangeText"));
UnityEventTools.AddPersistentListener(gestor.onMostrar, (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), notif, "Show"));

// Punto seguro + acción VIROO de teletransporte (solo el usuario que comete el error: LocalExecute)
var tpType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).First(t => t.FullName == "Viroo.Interactions.TeleportSingleAction");
var puntoSeguro = new GameObject("Punto seguro · entrada").transform; puntoSeguro.SetParent(grupo, false);
puntoSeguro.position = new Vector3(0f, 0f, -4.8f); puntoSeguro.rotation = Quaternion.identity;
var tpGo = new GameObject("Volver a la entrada (TeleportSingleAction)"); tpGo.transform.SetParent(grupo, false);
var tp = tpGo.AddComponent(tpType);
var tso = new SerializedObject(tp);
tso.FindProperty("teleportPosition").objectReferenceValue = puntoSeguro;
var pId = tso.FindProperty("id"); if (pId != null && string.IsNullOrEmpty(pId.stringValue)) pId.stringValue = Guid.NewGuid().ToString();
tso.ApplyModifiedPropertiesWithoutUndo();

// ---------- reglas ----------
var reglas = new List<ErrorCritico>();
ErrorCritico Regla(string id, int paso, string titulo, string consecuencia, CompletableItemData[] aplicaSi, CompletableItemData[] requiere, IEnumerable<GameObject> objetos)
{
    var go = new GameObject($"{id} · paso {paso}"); go.transform.SetParent(grupo, false);
    var e = go.AddComponent<ErrorCritico>();
    e.id = id; e.paso = paso; e.titulo = titulo; e.consecuencia = consecuencia;
    e.aplicaSi = aplicaSi.ToList(); e.requiere = requiere.ToList(); e.objetosVigilados = (objetos ?? new GameObject[0]).Distinct().ToList();
    reglas.Add(e); return e;
}

// Bitácora (CU-A4; NTP 1022)
Regla("rotor-no-bloqueado", 4, "Has tocado el generador sin el rotor bloqueado",
    "Con el rotor sin bloquear, una racha de viento puede hacerlo girar y arrastrar el generador: riesgo de atrapamiento. Bloquea el rotor con el freno y el pasador antes de tocarlo.",
    new CompletableItemData[0], new[] { P("4b.") }, generador.Concat(gastadas));

// Bitácora (CU-A1 3a, CU-A2)
Regla("solo-freno", 4, "Has confiado solo en el freno",
    "El freno solo no basta para inmovilizar el rotor: si cede, el rotor gira. Freno y pasador son dos acciones: coloca también el pasador de bloqueo.",
    new[] { P("4a.") }, new[] { P("4b.") }, new[] { seccCerrado, candado, tarjeta, medidor, tierra, pantalla });

// NTP 1024
var arnes = Regla("arnes-sin-anclar", 2, "Te has desplazado sin el arnés anclado",
    "Moverse por la góndola sin el arnés anclado expone a una caída de altura, uno de los accidentes más graves (NTP 1024). Vuelves a la entrada: ancla el arnés antes de moverte.",
    new CompletableItemData[0], new[] { P("2d.") }, null);
arnes.vigilarZona = true;
arnes.zonaProhibida = new Bounds(new Vector3(0f, 1.6f, 1.05f), new Vector3(3.6f, 3.4f, 10.1f)); // de z = -4.0 hasta el fondo de la góndola
UnityEventTools.AddVoidPersistentListener(arnes.onError, (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), tp, tpType.GetMethod("LocalExecute", Type.EmptyTypes)));

// RD 614/2001: cinco reglas de oro, en orden
Regla("regla-de-oro", 5, "Te has saltado una regla de oro: desconectar",
    "Antes de bloquear, señalizar o verificar hay que desconectar: abre el seccionador del generador (RD 614/2001, regla 1).",
    new[] { P("4b.") }, new[] { P("5a.") }, new[] { candado, tarjeta, medidor, tierra, pantalla });
Regla("regla-de-oro", 5, "Te has saltado una regla de oro: bloquear",
    "Sin el candado, cualquiera podría volver a cerrar el seccionador mientras trabajas (RD 614/2001, regla 2).",
    new[] { P("5a.") }, new[] { P("5b.") }, new[] { tarjeta, medidor, tierra, pantalla });
Regla("regla-de-oro", 5, "Te has saltado una regla de oro: señalizar",
    "Sin la tarjeta nadie sabe por qué está bloqueado ni quién interviene (RD 614/2001, regla 2).",
    new[] { P("5b.") }, new[] { P("5c.") }, new[] { medidor, tierra, pantalla });
Regla("sin-verificar-tension", 6, "Has intervenido sin verificar la ausencia de tensión",
    "Sin comprobarlo con el medidor no sabes si queda tensión: riesgo de contacto eléctrico (RD 614/2001, regla 3).",
    new[] { P("5c.") }, new[] { P("6.") }, new[] { tierra, pantalla, tapa }.Concat(bornes).Concat(gastadas));
Regla("regla-de-oro", 7, "Te has saltado una regla de oro: poner a tierra",
    "Sin puesta a tierra y en cortocircuito, una tensión de retorno o inducida llegaría a tus manos (RD 614/2001, regla 4).",
    new[] { P("6.") }, new[] { P("7a.") }, new[] { pantalla, tapa }.Concat(gastadas));
Regla("regla-de-oro", 7, "Te has saltado una regla de oro: proteger",
    "El embarrado próximo sigue en tensión: protégelo con la pantalla aislante antes de trabajar (RD 614/2001, regla 5).",
    new[] { P("7a.") }, new[] { P("7b.") }, new[] { tapa }.Concat(gastadas));

// [borrador] equipo: reposición
Regla("reponer-fuera-de-orden", 9, "Has cerrado el seccionador antes de retirar la tierra y el bloqueo",
    "Devolver tensión con la puesta a tierra o el candado puestos provoca un cortocircuito o pone en riesgo a quien interviene. Repón en orden inverso.",
    new[] { P("5a.") }, new[] { P("9c.") }, new[] { seccAbierto });
Regla("liberar-rotor-antes", 9, "Has liberado el rotor antes de terminar",
    "Liberar el freno o el pasador antes de reponer el resto devuelve movimiento a la máquina mientras aún se interviene. Repón en orden inverso.",
    new[] { P("4b.") }, new[] { P("9d.") }, new[] { manetaAccionada, alojamiento });

EditorUtility.SetDirty(gestor);
EditorSceneManager.MarkSceneDirty(grupo.gameObject.scene);
EditorSceneManager.SaveScene(grupo.gameObject.scene);
return "reglas: " + reglas.Count + " | detectores en el escenario: " + gond.GetComponentsInChildren<XRSimpleInteractable>(true).Length + " | notificación: " + notif.GetType().FullName + " | punto seguro: " + tpType.Name;
