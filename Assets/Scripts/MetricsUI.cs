using TMPro;
using UnityEngine;
using FarmSim.UnityBridge;

namespace FarmSim.UnityBridge
{
    public class MetricsUI : MonoBehaviour
    {
        [Header("Fuente de datos")]
        public WorldController worldController;

        [Header("Textos (arrastra el TMP_Text de cada VALOR, no el label fijo)")]
        public TMP_Text tiempoTotalValue;
        public TMP_Text combustibleValue;
        public TMP_Text cultivosValue; // fila "15/20" -> se muestra como "cosechados/total"
        public TMP_Text distanciaValue;

        private void Update()
        {
            if (worldController == null || worldController.World == null) return;

            var world = worldController.World;
            var model = world.Model;

            if (tiempoTotalValue != null) tiempoTotalValue.text = world.StepCount.ToString();
            if (combustibleValue != null) combustibleValue.text = world.FuelConsumed.ToString("0");
            if (cultivosValue != null) cultivosValue.text = $"{model.HarvestedCrops}/{model.TotalCrops}";
            if (distanciaValue != null) distanciaValue.text = world.TotalDistance.ToString();
        }
    }
}