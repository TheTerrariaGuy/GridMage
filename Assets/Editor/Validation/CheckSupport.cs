using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CheckSupport
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void SetTile(int row, int col, int type)
    {
        Assets.Scripts.GameLogic.INSTANCE.Board.Set(row, col, type);
        Assets.Scripts.GameLogic.INSTANCE.UpdateTile(row, col);
    }

    public static Texture2D RenderCamera(Camera camera, int width, int height)
    {
        var active = RenderTexture.active;
        var previous = camera.targetTexture;
        var target = new RenderTexture(width, height, 24);
        target.Create();
        try
        {
            camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image;
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    public sealed class Runner
    {
        private IEnumerator checks;
        public void Start(IEnumerator sequence, string output)
        {
            Require(checks == null, "Checks are already running.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "Results.txt"), "RUNNING");
            checks = sequence;
            EditorApplication.update += Tick;
            void Tick()
            {
                try
                {
                    if (checks.MoveNext()) return;
                    File.WriteAllText(Path.Combine(output, "Results.txt"), "PASS");
                    Debug.Log("CHECKS PASSED: " + output);
                }
                catch (Exception exception)
                {
                    File.WriteAllText(Path.Combine(output, "Results.txt"), "FAIL: " + exception);
                    Debug.LogException(exception);
                }
                try { (checks as IDisposable)?.Dispose(); }
                finally { checks = null; EditorApplication.update -= Tick; }
            }
        }
    }
}
