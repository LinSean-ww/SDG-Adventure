using System.Collections;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class PlayerInputServiceTests
    {
        private const string Selection = "Assets/Scenes/M6_DevelopmentSelection.unity";
        private const string C3 = "Assets/_Modules_/C3_TreasurePuzzle/ProjectContent/Assets/_C3_TreasurePuzzle/Scenes/C_Main.unity";

        [Test]
        public void EscapeHasOneEnabledActionInEachContext()
        {
            PlayerInputService service = PlayerInputService.Instance;
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Actions);
            var pause = service.Actions.FindActionMap("Global").FindAction("Pause");
            var cancel = service.Actions.FindActionMap("UI").FindAction("Cancel");
            try
            {
                service.SetContext(PlayerInputContext.Gameplay);
                Assert.IsTrue(service.Actions.FindActionMap("Gameplay").enabled);
                Assert.IsTrue(service.Actions.FindActionMap("Global").enabled);
                Assert.IsFalse(service.Actions.FindActionMap("UI").enabled);
                Assert.IsTrue(pause.enabled);
                Assert.IsFalse(cancel.enabled);

                service.SetContext(PlayerInputContext.UI);
                Assert.IsFalse(service.Actions.FindActionMap("Gameplay").enabled);
                Assert.IsFalse(service.Actions.FindActionMap("Global").enabled);
                Assert.IsTrue(service.Actions.FindActionMap("UI").enabled);
                Assert.IsFalse(pause.enabled);
                Assert.IsTrue(cancel.enabled);
            }
            finally
            {
                service.SetContext(PlayerInputContext.Inactive);
            }
        }

        [UnityTest]
        public IEnumerator OneServiceSurvivesM6C3SceneChanges()
        {
            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            PlayerInputService original = PlayerInputService.Instance;
            Assert.IsNotNull(original);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);

            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            Assert.AreSame(original, PlayerInputService.Instance);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(PlayerInputContext.Inactive, original.Context);

            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            Assert.AreSame(original, PlayerInputService.Instance);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
        }
    }
}
