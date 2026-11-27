using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomCode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Aerogenerador
{
    /// <summary>
    /// Patrón «error con rebobinado» (estándar VR FP, 2_aprendizaje/errores-criticos.md).
    /// Vigila un intento (agarrar o pulsar uno de los objetos vigilados, o entrar en una zona) y, si los
    /// pasos que lo hacen seguro no están completados, muestra la consecuencia simulada, deshace la acción
    /// y registra el error. No es editable por el formador.
    /// </summary>
    public class ErrorCritico : MonoBehaviour
    {
        [Tooltip("Identificador del error en errores-criticos.md")]
        public string id;
        public int paso;
        public string titulo;
        [TextArea(2, 5)] public string consecuencia;

        [Tooltip("La regla solo se vigila cuando estos pasos ya están completados")]
        public List<CompletableItemData> aplicaSi = new List<CompletableItemData>();
        [Tooltip("Hay error si se intenta la acción con alguno de estos pasos sin completar")]
        public List<CompletableItemData> requiere = new List<CompletableItemData>();

        [Header("Intento: agarrar o pulsar")]
        public List<GameObject> objetosVigilados = new List<GameObject>();

        [Header("Intento: entrar en una zona (cabeza del jugador)")]
        public bool vigilarZona;
        public Bounds zonaProhibida;

        [Header("Rebobinado adicional (p. ej. teletransporte VIROO a un punto seguro)")]
        public UnityEvent onError = new UnityEvent();

        const float Espera = 2f;
        GestorErroresCriticos gestor;
        readonly HashSet<XRBaseInteractable> suscritos = new HashSet<XRBaseInteractable>();
        float ultimo = -10f;
        bool zonaArmada, errorEnEstaEntrada;
        float reintento;

        void Awake()
        {
            gestor = GetComponentInParent<GestorErroresCriticos>();
            if (gestor == null) gestor = FindFirstObjectByType<GestorErroresCriticos>();
        }

        void Update()
        {
            // Las piezas reciben su XRGrabInteractable en ejecución (gestor de agarrables de la plantilla):
            // nos suscribimos en cuanto aparece.
            foreach (var go in objetosVigilados)
            {
                if (go == null) continue;
                foreach (var xr in go.GetComponents<XRBaseInteractable>())
                    if (suscritos.Add(xr)) xr.selectEntered.AddListener(args => AlSeleccionar(xr, args));
            }

            if (vigilarZona && Camera.main != null)
            {
                bool dentro = zonaProhibida.Contains(Camera.main.transform.position);
                // Hasta que VIROO coloca al jugador en su punto de inicio la cámara puede estar en cualquier sitio:
                // la zona solo se vigila después de ver al jugador fuera de ella.
                if (!dentro) { zonaArmada = true; errorEnEstaEntrada = false; }
                else if (zonaArmada && Aplica && !Cumplido)
                {
                    // Un error por cada entrada en la zona; mientras siga dentro se reintenta el rebobinado
                    // (la primera orden de teletransporte de VIROO puede perderse).
                    if (!errorEnEstaEntrada) { errorEnEstaEntrada = true; reintento = Time.time; Comprobar(null, null); }
                    else if (Time.time - reintento > 0.5f) { reintento = Time.time; onError.Invoke(); }
                }
            }
        }

        public bool Aplica => aplicaSi.All(c => c != null && c.Completed);
        public bool Cumplido => requiere.All(c => c != null && c.Completed);

        void AlSeleccionar(XRBaseInteractable xr, SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return; // colocar en su objetivo no es un intento
            Comprobar(xr, args.interactorObject);
        }

        void Comprobar(XRBaseInteractable xr, IXRSelectInteractor interactor)
        {
            if (!Aplica || Cumplido) return;
            if (Time.time - ultimo < Espera) return;
            ultimo = Time.time;

            StartCoroutine(Rebobinar(xr, interactor));
            onError.Invoke();
            if (gestor != null) gestor.Registrar(this, xr != null ? xr.name : "zona");
        }

        IEnumerator Rebobinar(XRBaseInteractable xr, IXRSelectInteractor interactor)
        {
            if (xr == null) yield break;
            yield return null; // no cambiar la selección dentro del propio evento de selección

            if (interactor != null && xr.interactionManager != null && xr.isSelected && xr is IXRSelectInteractable sel)
                xr.interactionManager.SelectExit(interactor, sel);

            var part = xr.GetComponent<AssemblyPart>();
            if (part != null && part.startTransform != null)
            {
                var rb = xr.GetComponent<Rigidbody>();
                part.transform.SetPositionAndRotation(part.startTransform.position, part.startTransform.rotation);
                if (rb != null)
                {
                    rb.position = part.startTransform.position;
                    rb.rotation = part.startTransform.rotation;
                    if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!vigilarZona) return;
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawCube(zonaProhibida.center, zonaProhibida.size);
        }
    }
}
