using UnityEngine;

public class SunAudioListener : MonoBehaviour
{
    void Start()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;

        #if STEAMAUDIO_ENABLED && !MESH_RIR_SPATIALIZER
        SteamAudio.SteamAudioManager.NotifyAudioListenerChanged();
        #endif
    }

    void Update()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
    }
}
