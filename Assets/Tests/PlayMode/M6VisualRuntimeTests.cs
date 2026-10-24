using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class M6VisualRuntimeTests
    {
        private const string C3 = "Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity";
        private const string Selection = "Assets/Scenes/M6_DevelopmentSelection.unity";
        private static readonly StringBuilder report = new StringBuilder();

        private static IEnumerator Settle()
        {
            for (int i = 0; i < 12; i++) yield return null;
        }

        private static double Capture(string label)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "C3 camera");
            var probe = RenderSettings.ambientProbe;
            report.AppendLine(label + " ambientL00=" + probe[0, 0] + "," + probe[1, 0] + "," + probe[2, 0] +
                " skybox=" + (RenderSettings.skybox == null ? "NULL" : RenderSettings.skybox.name) +
                " ambientIntensity=" + RenderSettings.ambientIntensity +
                " lightmaps=" + LightmapSettings.lightmaps.Length +
                " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                " cameraClear=" + camera.clearFlags + " cameraPos=" + camera.transform.position +
                " cameraRot=" + camera.transform.rotation.eulerAngles);

            RenderTexture texture = new RenderTexture(800, 450, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                Texture2D image = new Texture2D(800, 450, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                long sum = 0;
                int dark = 0;
                foreach (Color32 pixel in pixels)
                {
                    int brightness = (pixel.r + pixel.g + pixel.b) / 3;
                    sum += brightness;
                    if (brightness < 20) dark++;
                }
                double darkFraction = dark / (double)pixels.Length;
                report.AppendLine(label + " meanBrightness=" + (sum / (double)pixels.Length).ToString("F2") +
                    " darkFraction=" + darkFraction.ToString("F4"));
                Directory.CreateDirectory("DevelopmentReports/M6/VisualRuntime");
                File.WriteAllBytes("DevelopmentReports/M6/VisualRuntime/" + label + ".png", image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                return darkFraction;
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator DirectVersusSelectionSceneLighting()
        {
            report.Clear();
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return Settle();
            Assert.Less(Capture("DirectC3"), 0.05, "Direct C3 rendering has excessive black pixels.");
            Assert.Greater(RenderSettings.ambientProbe[0, 0], 0.05f, "Skybox ambient probe was not generated.");

            if (Application.CanStreamedLevelBeLoaded(Selection))
            {
                yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
                Type coordinatorType = Type.GetType("SDGAdventure.ModuleSystem.ModuleCoordinator, Assembly-CSharp", true);
                object coordinator = coordinatorType.GetProperty("Instance").GetValue(null);
                Assert.IsNotNull(coordinator);
                object[] args = { "c3_treasure_puzzle", null };
                Assert.IsTrue((bool)coordinatorType.GetMethod("TryLaunch").Invoke(coordinator, args), args[1]?.ToString());
                float deadline = Time.realtimeSinceStartup + 15f;
                while (SceneManager.GetActiveScene().path != C3 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(C3, SceneManager.GetActiveScene().path);
                yield return Settle();
                Assert.Less(Capture("FromSelection"), 0.05, "Selection-to-C3 rendering has excessive black pixels.");
                Assert.Greater(RenderSettings.ambientProbe[0, 0], 0.05f, "Skybox ambient probe was not generated after transition.");
            }
            DynamicGI.UpdateEnvironment();
            yield return Settle();
            Capture("AfterGIUpdate");
            File.WriteAllText("DevelopmentReports/M6/VisualRuntime/RuntimeProbe.txt", report.ToString());
        }
    }
}
