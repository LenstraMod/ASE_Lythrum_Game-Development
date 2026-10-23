using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// Camera helpers shared by the shard pickup and the boss intro. To look at something, point cam.Follow at it
// and run Zoom(); Cinemachine pans the camera, this eases the zoom (orthographic size) over time.
public static class CameraFocus
{
    // Eased (slow start, slow end) so the zoom never jerks.
    public static IEnumerator Zoom(CinemachineCamera cam, float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetSize(cam, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)));
            yield return null;
        }
        SetSize(cam, to);
    }

    // Cinemachine damping makes the camera lag behind its target. This waits until the camera has caught up (or stopped
    // moving, e.g. against the map edge), but never longer than maxWait, so nothing starts while the camera is still sliding.
    public static IEnumerator WaitUntilSettled(CinemachineCamera cam, float maxWait = 1f, float closeEnough = 0.3f)
    {
        if (cam.Follow == null) yield break;

        Vector3 last = cam.transform.position;
        for (float t = 0f; t < maxWait; t += Time.deltaTime)
        {
            yield return null;

            Vector3 now = cam.transform.position;
            bool close = ((Vector2)(now - cam.Follow.position)).sqrMagnitude < closeEnough * closeEnough;
            bool still = (now - last).sqrMagnitude < 0.0001f;
            last = now;
            if (close || (still && t > 0.15f)) yield break;
        }
    }

    static void SetSize(CinemachineCamera cam, float size)
    {
        LensSettings lens = cam.Lens;
        lens.OrthographicSize = size;
        cam.Lens = lens;
    }
}
