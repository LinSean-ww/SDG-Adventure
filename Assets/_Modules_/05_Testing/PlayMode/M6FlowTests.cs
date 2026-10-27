using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class M6FlowTests
    {
        private const string Selection = "Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity";
        private const string C3 = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Get(object target, string member)
        {
            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(member);
            return property != null ? property.GetValue(target) : type.GetField(member).GetValue(target);
        }
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(target, args);
        private static Component Find(string type) =>
            (Component)UnityEngine.Object.FindObjectOfType(RuntimeType(type));
        private static object Coordinator => RuntimeType("SDGAdventure.ModuleSystem.ModuleCoordinator")
            .GetProperty("Instance").GetValue(null);

        private static IEnumerator WaitForScene(string path)
        {
            float until = Time.realtimeSinceStartup + 15f;
            while (SceneManager.GetActiveScene().path != path && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(path, SceneManager.GetActiveScene().path);
        }

        private static void Launch(object coordinator)
        {
            object[] args = { "c3_treasure_puzzle", null };
            Assert.IsTrue((bool)Call(coordinator, "TryLaunch", args), args[1]?.ToString());
        }

        [UnityTest]
        public IEnumerator C3CompletionRestartReturnAndReentry()
        {
            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            object coordinator = Coordinator;
            Assert.IsNotNull(coordinator);
            Launch(coordinator);
            yield return WaitForScene(C3);

            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Assert.IsNotNull(progress);
            Assert.IsNotNull(puzzle);
            Assert.IsNotNull(device);
            Assert.IsFalse((bool)Call(device, "TryActivate"), "Unfinished level must not activate.");
            Assert.IsNull(Get(coordinator, "LastResult"));

            Assert.IsTrue((bool)Call(progress, "TryCollect", "fuse", "Fuse"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "control_chip", "Chip"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "filter_module", "Filter"));
            Assert.IsFalse((bool)Call(device, "TryActivate"), "Collection alone must not complete.");
            Type stepType = RuntimeType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer = Array.CreateInstance(stepType, 3);
            answer.SetValue(Enum.Parse(stepType, "Sorting"), 0);
            answer.SetValue(Enum.Parse(stepType, "Recycling"), 1);
            answer.SetValue(Enum.Parse(stepType, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsNull(Get(coordinator, "LastResult"), "Solving without activation must not complete.");
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Assert.IsTrue((bool)Get(device, "LevelCompleted"));
            object result = Get(coordinator, "LastResult");
            Assert.IsNotNull(result);
            Assert.IsTrue((bool)Get(result, "Completed"));
            Assert.AreEqual("c3_treasure_puzzle", Get(result, "ModuleId"));
            Assert.AreEqual(3, Get(result, "CollectedParts"));
            Assert.IsFalse((bool)Call(device, "TryActivate"), "Activation must be one-shot.");
            Component adapter = Find("SDGAdventure.C3.C3ModuleAdapter");
            Assert.IsNotNull(adapter);
            Assert.IsFalse((bool)Call(coordinator, "ReportCompletion", adapter, result), "Coordinator must reject repeat results.");
            Component completionScreen = Find("C3TreasurePuzzle.C3CompletionScreen");
            Assert.IsTrue((bool)Get(completionScreen, "Visible"));
            Call(completionScreen, "RestartLevel");

            yield return WaitForScene(C3);
            yield return null;
            Assert.AreEqual(0, Get(Find("C3TreasurePuzzle.C3TreasureProgress"), "CollectedCount"));
            Assert.IsNull(Get(coordinator, "LastResult"));

            progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            device = Find("C3TreasurePuzzle.C3DeviceState");
            Assert.IsTrue((bool)Call(progress, "TryCollect", "fuse", "Fuse"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "control_chip", "Chip"));
            Assert.IsTrue((bool)Call(progress, "TryCollect", "filter_module", "Filter"));
            Assert.IsTrue((bool)Call(puzzle, "SubmitAnswer", answer));
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            completionScreen = Find("C3TreasurePuzzle.C3CompletionScreen");
            Assert.IsTrue((bool)Get(completionScreen, "Visible"));
            Call(completionScreen, "ReturnToSelection");
            yield return WaitForScene(Selection);
            Assert.IsNull(Get(coordinator, "CurrentModuleId"));
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsNotNull(Get(coordinator, "LastResult"));
            Launch(coordinator);
            yield return WaitForScene(C3);
            yield return null;
            Assert.AreEqual(0, Get(Find("C3TreasurePuzzle.C3TreasureProgress"), "CollectedCount"));
            Assert.IsNull(Get(coordinator, "LastResult"));
        }
    }
}
