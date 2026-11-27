using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace Aerogenerador
{
    /// <summary>
    /// Muestra la consecuencia de los errores críticos (notificación VIROO + sonido) y lleva el registro
    /// de errores por paso, que después se enviará por xAPI (CU-A4).
    /// </summary>
    public class GestorErroresCriticos : MonoBehaviour
    {
        [Serializable]
        public class Registro
        {
            public string id;
            public int paso;
            public string titulo;
            public string objeto;
            public float tiempo;
        }

        public List<Registro> registro = new List<Registro>();
        public AudioSource sonido;

        [Header("Notificación (VIROO BasicNotification: ChangeTitle, ChangeText, Show)")]
        public UnityEvent<string> onTitulo = new UnityEvent<string>();
        public UnityEvent<string> onTexto = new UnityEvent<string>();
        public UnityEvent onMostrar = new UnityEvent();

        [Header("Visibilidad del aviso")]
        [Tooltip("Capa de la notificación. Se dibuja con una cámara superpuesta (URP) encima de la escena, " +
                 "para que ningún objeto de la góndola la tape.")]
        public string capaAviso = "SceneLoadingVisualizer";

        public int Total => registro.Count;

        Camera camaraAvisos;

        void AsegurarCamaraDeAvisos()
        {
            var principal = Camera.main;
            if (principal == null || (camaraAvisos != null && camaraAvisos.transform.parent == principal.transform)) return;
            int capa = LayerMask.NameToLayer(capaAviso);
            if (capa < 0) return;

            var go = new GameObject("Cámara de avisos (overlay)");
            go.transform.SetParent(principal.transform, false);
            camaraAvisos = go.AddComponent<Camera>();
            camaraAvisos.cullingMask = 1 << capa;
            camaraAvisos.nearClipPlane = principal.nearClipPlane;
            camaraAvisos.fieldOfView = principal.fieldOfView;
            var datos = camaraAvisos.GetUniversalAdditionalCameraData();
            datos.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
            var pila = principal.GetUniversalAdditionalCameraData().cameraStack;
            if (!pila.Contains(camaraAvisos)) pila.Add(camaraAvisos);
            principal.cullingMask &= ~(1 << capa); // que la cámara principal no la dibuje (y no quede tapada)
        }

        public int ErroresEnPaso(int paso) => registro.FindAll(r => r.paso == paso).Count;

        public void Registrar(ErrorCritico error, string objeto)
        {
            registro.Add(new Registro { id = error.id, paso = error.paso, titulo = error.titulo, objeto = objeto, tiempo = Time.time });
            Debug.Log($"[ErrorCritico] {error.id} · paso {error.paso} · {error.titulo} · intento: {objeto}");

            AsegurarCamaraDeAvisos();
            onTitulo.Invoke($"ERROR CRÍTICO · Paso {error.paso}");
            onTexto.Invoke($"<b>{error.titulo}</b>\n{error.consecuencia}\n\n<i>Se deshace la acción. Repite el paso.</i>");
            onMostrar.Invoke();
            if (sonido != null) sonido.Play();
        }
    }
}
